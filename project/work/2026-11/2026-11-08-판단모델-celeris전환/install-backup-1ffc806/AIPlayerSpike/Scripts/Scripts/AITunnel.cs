// AITunnel — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// --------------------------------------------------------------------------------------------- tunnel planning

public class TunnelPlan
{
    public List<int2> path = new List<int2>();          // tiles after the start, ending at the target
    public List<(int2 p, ObjectID obj, int hits)> walls = new List<(int2, ObjectID, int)>();
    public int totalHits;
    public int toolShortSeen;                           // undiggable walls observed by this search
    public bool sharedSearch;                           // diagnostic counts cover the shared search, not a target prefix
    public int toolShortRed;                            // lowest damage reduction among them (the easiest blocking wall; 0 = none)
    public ObjectID toolShortExample = ObjectID.None;
    public int damage, miningEffect;
    public string damageNote = "";
    public bool edgeRoute;      // 검수 E1: no way without mining the base edge (the second pass); the options say so
    public int edgeClosed;      // edge walls closed in the first pass
    public float searchMs;      // the search time (both passes)
    public int passes = 1;      // 우회 사실: searches run (1 or 2)
    public bool firstCapped;    // 재검수 G3: the first pass (edge closed) stopped at its search cap
    public TunnelPlan alt;      // 우회 사실: the edge way when it is DetourGainTiles shorter than this (long) door way, else null
    private World _riskWorld;
    private int _riskWallCount;
    private string _riskText;
    internal void SetRiskText(World server, string text)
    {
        _riskWorld = server; _riskWallCount = walls.Count; _riskText = text;
    }
    public float EstimatedSeconds => AIMoveEstimate.Seconds(path.Count, totalHits);
    public string RiskText(World server)
    {
        if (_riskText != null && ReferenceEquals(_riskWorld, server) && _riskWallCount == walls.Count) return _riskText;
        var seen = new HashSet<ObjectID>();
        foreach (var w in walls)
            if (seen.Add(w.obj))
            {
                string risk = AIWallData.RiskText(server, w.obj);
                if (risk != "") return risk;
            }
        return "";
    }
    public string WallSummary()
    {
        var d = new Dictionary<ObjectID, int>();
        foreach (var w in walls) { d.TryGetValue(w.obj, out int c); d[w.obj] = c + 1; }
        var s = new List<string>();
        foreach (var kv in d) s.Add($"{kv.Key} {kv.Value}");
        return s.Count == 0 ? "벽 없음" : string.Join(", ", s);
    }
}

public static class AITunnel
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public readonly Dictionary<ObjectID, int> ToolBonus = new Dictionary<ObjectID, int>();
        public int LastToolShortRed;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Tunnel);
    private static State S => StateOf(AIAgent.Cur);

    // Mining-effect bonus remembered per held object (filled when the AI holds it); used to plan with the best tool
    // in the hotbar even while it is not held.
    public static Dictionary<ObjectID, int> ToolBonus => S.ToolBonus;
    public static int LastToolShortRed { get => S.LastToolShortRed; set => S.LastToolShortRed = value; }   // reduction of the easiest tool-short wall met by the last Plan (0 = none)

    public static void RecordHeld(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!em.HasComponent<EquippedObjectCD>(player)) return;
        var held = em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID;
        AIWallData.EstimateMiningDamage(server, player, out int eff);
        if (held == ObjectID.None) return;
        if (!ToolBonus.TryGetValue(held, out int old) || old != eff)
        {
            ToolBonus[held] = eff;
            AIMemory.MarkDirty();
            AIPlayerSpikeMod.Log($"TUNNEL: mining bonus while holding {held} = {eff}");
        }
    }

    // Damage the AI will do when MineSkill picks its tool (best pick in the hotbar, else bare hands).
    public static int PlannedDamage(World server, Entity player, out int eff, out string note)
    {
        AIMemory.Load(); // remembered tool bonuses come with the per-world memory
        int cur = AIWallData.EstimateMiningDamage(server, player, out eff);
        // the tool HandSlot will take (a pick only in the bag is moved into the hotbar when mining starts)
        if (!AIBody.CanHold(server, player, true, out string tool, out _)) tool = "맨손";
        note = $"도구 {tool}";
        if (Enum.TryParse(tool, out ObjectID toolId) && toolId != ObjectID.None)
        {
            var em = server.EntityManager;
            var held = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID : ObjectID.None;
            if (held == toolId) return cur;
            if (ToolBonus.TryGetValue(toolId, out int b)) { eff = b; note += $"(기억한 캐기 +{b})"; return 20 + b; }
            note += "(캐기 보너스 모름, 맨손 기준 추정)";
            eff = 0;
            return 20;
        }
        return 20 + (tool == "맨손" ? 0 : eff);
    }

    private class Heap
    {
        private readonly List<(int f, int2 p)> _a = new List<(int, int2)>();
        public int Count => _a.Count;
        public void Push(int f, int2 p)
        {
            _a.Add((f, p));
            int i = _a.Count - 1;
            while (i > 0) { int up = (i - 1) / 2; if (_a[up].f <= _a[i].f) break; (_a[up], _a[i]) = (_a[i], _a[up]); i = up; }
        }
        public (int f, int2 p) Pop()
        {
            var top = _a[0];
            var last = _a[_a.Count - 1];
            _a.RemoveAt(_a.Count - 1);
            if (_a.Count > 0)
            {
                _a[0] = last;
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _a.Count && _a[l].f < _a[m].f) m = l;
                    if (r < _a.Count && _a[r].f < _a[m].f) m = r;
                    if (m == i) break;
                    (_a[m], _a[i]) = (_a[i], _a[m]); i = m;
                }
            }
            return top;
        }
    }

    // Remembered exploration frontier: when nothing within one screen would show a not-yet-seen cell (e.g. at the core,
    // where the seen area reaches farther than a screen in every direction), the nearest places by known route (same
    // costs as Plan: walk 1, diggable wall 1 + 2*hits, only tiles seen now or before) where standing would show
    // unseen cells, one per direction (E/W/N/S of the AI). Uses only the AI's own memory of what it has seen.
    // Remembered-frontier search as a job that can be advanced a few hundred tiles per frame (AIBrain runs it between combat
    // calls, so a ~9000-tile search of 60-100 ms no longer lands in one frame). Main thread only (reads the tile map).
    public sealed class FrontierJob
    {
        private readonly World _server;
        private readonly AITileReader _tr;
        private readonly float3 _pos;
        public readonly int2 Start;
        private readonly int _dmg;
        private readonly Func<int2, bool> _skip;
        private readonly HashSet<int2> _objBlocked;
        private readonly Dictionary<int, (bool ok, int hp, int red)> _wallInfo = new Dictionary<int, (bool, int, int)>();
        private readonly Dictionary<int2, (int2 t, string dir, int nov, int cost)> _best = new Dictionary<int2, (int2, string, int, int)>();
        private readonly Dictionary<int2, int> _cost = new Dictionary<int2, int>();
        private readonly Heap _heap = new Heap();
        private static readonly int2[] Dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        public int Expanded;
        public bool Done;
        public List<(int2 t, string dir, int novelty, int cost)> Result;
        public readonly System.Diagnostics.Stopwatch Time = new System.Diagnostics.Stopwatch();

        public FrontierJob(World server, Entity player, Func<int2, bool> skip)
        {
            _server = server; _skip = skip;
            _tr = new AITileReader(server);
            _pos = AIBody.Pos(server, player);
            Start = AIEdge.StandTile(server, _pos);   // 재실측 결함 2: not a shut door tile it is pressed on
            _dmg = PlannedDamage(server, player, out _, out _);
            try { _objBlocked = AIObjects.BlockedInView(server, player, out _); } catch { _objBlocked = new HashSet<int2>(); }
            _cost[Start] = 0;
            _heap.Push(0, Start);
        }

        private int Cost(int2 t)
        {
            if (!(AIVision.CanSee(_pos, t) || AIMemory.SeenTile(t))) return -1;
            if (_tr.HasWall(t, out TileCD w, out _, out _))
            {
                if (!_wallInfo.TryGetValue(w.tileset, out var wi))
                {
                    bool ok = AIWallData.TryGet(_server, w.tileset, out int hp, out int red, out _);
                    wi = (ok, hp, red); _wallInfo[w.tileset] = wi;
                }
                return wi.ok && _dmg > wi.red ? 1 + 2 * (int)math.ceil(wi.hp / (float)(_dmg - wi.red)) : -1;
            }
            return _tr.Walkable(t) && !_objBlocked.Contains(t) ? 1 : -1;
        }

        // Expands up to n more tiles; true when the search is finished (Result filled).
        public bool Step(int n)
        {
            if (Done) return true;
            Time.Start();
            int until = Expanded + n;
            while (_heap.Count > 0 && Expanded < until)
            {
                var (f, c) = _heap.Pop();
                if (_cost.TryGetValue(c, out int cc) && f > cc) continue;
                Expanded++;
                int2 v = c - Start;
                if (!_skip(c))   // every other tile is enough to find a frontier
                {
                    string dir = math.abs(v.x) >= math.abs(v.y) ? (v.x > 0 ? "동" : "서") : (v.y > 0 ? "북" : "남");
                    if (!_best.ContainsKey(c))
                    {
                        int nov = AIMemory.Novelty(c);
                        if (nov > 0) _best[c] = (c, dir, nov, f);
                    }
                }
                foreach (var d in Dirs)
                {
                    if ((d.x > 0 && c.x == int.MaxValue) || (d.x < 0 && c.x == int.MinValue) || (d.y > 0 && c.y == int.MaxValue) || (d.y < 0 && c.y == int.MinValue)) continue;
                    int2 nb = c + d;
                    int k = Cost(nb);
                    if (k < 0) continue;
                    int ng = f + k;
                    if (_cost.TryGetValue(nb, out int og) && og <= ng) continue;
                    _cost[nb] = ng;
                    _heap.Push(ng, nb);
                }
            }
            Time.Stop();
            // Continue next frame until the finite observed/remembered graph is exhausted.
            // The per-frame n remains the CPU budget; a partial search is never reported as complete.
            if (_heap.Count > 0) return false;
            Done = true;
            Result = new List<(int2, string, int, int)>();
            foreach (var kv in _best) Result.Add((kv.Value.t, kv.Value.dir, kv.Value.nov, kv.Value.cost));
            Result.Sort((a, b) => a.Item4.CompareTo(b.Item4));
            return true;
        }
    }

    // Cheapest route from the AI's tile to `target` through known tiles (seen now or before). A walkable tile costs 1,
    // a diggable wall 1 + 2*hits. Returns null with `reason` when there is none.
    // damageOverride > 0: TEST ONLY (/ai plan x z N) — plan as if the next hit did N damage, to see tool-short avoidance.
    public const int DoorStepCost = AITunnelSearch.DoorStepCost;   // 실게임 D1: an own / my door tile on a dig route
    // allowEdge (우회 사실): plan with the edge walls open (one pass, the old search) — the "mine the edge" option's walk
    public static int Calls, TwoPass;   // 우회 사실: Plan calls and two-pass searches since the last reset (AIBrain counts them per build)
    // detourCheck (검수 H1): the detour check (the edge way searched beside a long first way, plan.alt) — only the walks
    // whose options offer the edge way (goto / place, home, the enemy dig: PlanC detour); all else (and the dig skill's own
    // replans) only fall back to the edge when there is no other way
    public static TunnelPlan Plan(World server, Entity player, int2 target, out string reason, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false)
    {
        var job = BeginPlan(server, player, target, damageOverride, allowEdge, detourCheck);
        job.RunUntilDone();
        reason = job.Reason;
        return job.Result;
    }

    public static PlanJob BeginPlan(World server, Entity player, int2 target, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false) =>
        new PlanJob(server, player, target, damageOverride, allowEdge, detourCheck);

    public static PlanBatch BeginBatch(World server, Entity player, float3? capturedPos = null) =>
        new PlanBatch(server, player, capturedPos, false);

    // Main-thread only. Lazy facts span an observation interval, without an atomic world/epoch guarantee.
    // The owner withholds every batch result until the unique read set validates without Changed.
    public sealed class PlanBatch
    {
        internal readonly World server;
        internal readonly Entity player;
        private readonly float3? capturedPos;
        private readonly bool checkMovement;
        private AITileReader tr;
        private int capturePhase, pendingJobs, searchPhase;
        private bool validationStarted;
        private readonly Dictionary<int, SharedTrees> sharedTrees = new Dictionary<int, SharedTrees>();
        public bool SearchesReady => searchPhase == 2;
        public int SearchCount { get; private set; }
        public long ExpandedTotal { get; private set; }
        public int CapturedTileCount => tileFacts.Count;
        internal bool edgeActive;
        private bool usesEdgePolicy;
        internal int Damage, MiningEffect;
        internal string DamageNote;
        private HashSet<int2> objBlocked;
        private readonly Dictionary<int, (bool ok, int hp, int red, ObjectID obj)> wallInfo = new Dictionary<int, (bool, int, int, ObjectID)>();
        private readonly Dictionary<ObjectID, string> risks = new Dictionary<ObjectID, string>();
        internal struct TileFact : IEquatable<TileFact>
        {
            public bool Known, Seen, Wall, Walkable, WalkDoor, Blocked, DoorThere, WallOk;
            public int Tileset, Hp, Red, Cost;
            public ObjectID Object;
            public string Risk;
            public bool Equals(TileFact other) => Known == other.Known && Seen == other.Seen && Wall == other.Wall &&
                Walkable == other.Walkable && WalkDoor == other.WalkDoor && Blocked == other.Blocked &&
                DoorThere == other.DoorThere && WallOk == other.WallOk && Tileset == other.Tileset &&
                Hp == other.Hp && Red == other.Red && Cost == other.Cost &&
                Object == other.Object && Risk == other.Risk;
        }
        private readonly Dictionary<int2, TileFact> tileFacts = new Dictionary<int2, TileFact>();
        private readonly List<int2> readTiles = new List<int2>();
        private readonly Dictionary<int2, bool> edgeFacts = new Dictionary<int2, bool>();
        private int validationPhase, validationIndex;
        public bool Ready { get; private set; }
        public float3 Position { get; private set; }
        public int2 Start { get; private set; }
        public bool Changed { get; private set; }
        public string Reason { get; private set; } = "";
        public bool ValidationCompleted { get; private set; }
        public int LastStepWork { get; private set; }

        internal PlanBatch(World server, Entity player, float3? capturedPos, bool checkMovement)
        {
            this.server = server; this.player = player; this.capturedPos = capturedPos; this.checkMovement = checkMovement;
        }

        public bool StepCapture(int workBudget)
        {
            LastStepWork = 0;
            while (!Ready && LastStepWork < workBudget)
            {
                LastStepWork++;
                switch (capturePhase++)
                {
                    case 0:
                        Position = capturedPos ?? AIBody.Pos(server, player);
                        Start = AIEdge.StandTile(server, Position);
                        edgeActive = AIEdge.PlanActive;
                        break;
                    case 1:
                        Damage = PlannedDamage(server, player, out MiningEffect, out DamageNote);
                        break;
                    case 2:
                        try { objBlocked = checkMovement ? AIObjects.BlockedInView(server, player, out _) : AIObjects.BlockedInViewAt(server, player, Position); }
                        catch { objBlocked = new HashSet<int2>(); MarkChanged("장애물 관측 실패"); }
                        Ready = true;
                        break;
                }
            }
            return Ready;
        }

        // Prewarm both production-cost graphs before synchronous candidate consumers enter. Later queries
        // only reconstruct paths. TEST ONLY damage overrides own separately budgeted graph pairs.
        public bool StepSearches(int workBudget)
        {
            LastStepWork = 0;
            if (searchPhase == 2 || workBudget <= 0) return searchPhase == 2;
            if (!Ready) return false;
            if (validationStarted) throw new InvalidOperationException("Batch validation has already started.");
            BeginReadStep();
            var trees = Trees(Damage);
            while (searchPhase < 2 && LastStepWork < workBudget)
            {
                var tree = trees.Tree(searchPhase == 1);
                bool done = trees.Step(tree, workBudget - LastStepWork);
                LastStepWork += tree.LastStepWork;
                if (!done) return false;
                searchPhase++;
            }
            return searchPhase == 2;
        }

        public PlanJob BeginPlan(int2 target, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false)
        {
            if (validationStarted) throw new InvalidOperationException("Batch validation has already started.");
            return new PlanJob(this, target, damageOverride, allowEdge, detourCheck, false, true);
        }

        // Small synchronous consumers can use the captured batch facts without warming the whole map.
        // The batch still owns final validation, just as it does for shared-tree plans.
        public PlanJob BeginSinglePlan(int2 target, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false)
        {
            if (validationStarted) throw new InvalidOperationException("Batch validation has already started.");
            return new PlanJob(this, target, damageOverride, allowEdge, detourCheck, false, false);
        }

        internal SharedTrees Trees(int damage)
        {
            if (!sharedTrees.TryGetValue(damage, out var trees))
            {
                trees = new SharedTrees(this, damage);
                sharedTrees.Add(damage, trees);
            }
            return trees;
        }
        internal void CountSearch() { SearchCount++; }
        internal bool StepTree(AITunnelSearch.SharedSearchJob tree, int workBudget)
        {
            int before = tree.ExpandedTiles;
            bool done = tree.Step(workBudget);
            ExpandedTotal += tree.ExpandedTiles - before;
            return done;
        }

        internal void RegisterJob(bool allowEdge) { pendingJobs++; usesEdgePolicy |= !allowEdge; }
        internal void FinishJob() { pendingJobs--; }
        internal void BeginReadStep() { tr = null; }
        internal TileFact Fact(int2 t)
        {
            if (tileFacts.TryGetValue(t, out var f)) return f;
            if (tr == null) tr = new AITileReader(server);
            f = ReadFact(t, objBlocked, true);
            tileFacts.Add(t, f); readTiles.Add(t);
            return f;
        }
        internal bool TryFact(int2 t, out TileFact f) => tileFacts.TryGetValue(t, out f);
        internal bool OpensEdge(int2 t)
        {
            if (edgeFacts.TryGetValue(t, out bool opens)) return opens;
            opens = edgeActive && tileFacts.TryGetValue(t, out var f) && f.Wall && AIEdge.OpensEdge(t);
            edgeFacts.Add(t, opens);
            return opens;
        }

        // This record is immutable after its first cost read. Later path/hit/risk consumers never mix it with
        // another frame's wall layers or prefab data. Validation uses fresh records without updating the snapshot.
        private TileFact ReadFact(int2 t, HashSet<int2> blocked, bool cached)
        {
            var f = new TileFact { Cost = -1, Risk = "" };
            f.Seen = AIVision.CanSee(Position, t);
            f.Known = f.Seen || AIMemory.SeenTile(t);
            if (!f.Known) return f;
            f.Wall = tr.HasWall(t, out TileCD w, out _, out _);
            if (f.Wall)
            {
                f.Tileset = w.tileset;
                if (!cached || !wallInfo.TryGetValue(w.tileset, out var wi))
                {
                    bool ok = AIWallData.TryGet(server, w.tileset, out int hp, out int red, out ObjectID obj);
                    wi = (ok, hp, red, obj);
                    if (cached) wallInfo[w.tileset] = wi;
                }
                f.WallOk = wi.ok; f.Hp = wi.hp; f.Red = wi.red; f.Object = wi.obj;
                if (wi.ok)
                {
                    if (!cached || !risks.TryGetValue(wi.obj, out string risk))
                    {
                        risk = AIWallData.RiskText(server, wi.obj);
                        if (cached) risks[wi.obj] = risk;
                    }
                    f.Risk = risk;
                }
                return f;
            }
            f.Walkable = tr.Walkable(t);
            if (!f.Walkable) return f;
            f.WalkDoor = AIEdge.IsWalkDoorTile(t);
            f.Blocked = blocked.Contains(t);
            if (f.WalkDoor)
            {
                f.DoorThere = f.Seen && f.Blocked && AIEdge.DoorAt(server, t, Position) != null;
                f.Cost = AITunnelSearch.DoorTileCost(f.Seen, f.Blocked, f.DoorThere);
            }
            else if (!f.Blocked) f.Cost = 1;
            return f;
        }

        // Can be restarted by the build owner before publishing an already completed plan. Changed is sticky;
        // the owner cancels the build instead of treating this as a proved unreachable target.
        public void BeginValidation()
        {
            if (pendingJobs != 0) throw new InvalidOperationException("Finish batch plans before validation.");
            validationStarted = true;
            validationPhase = 0; validationIndex = 0;
            ValidationCompleted = Changed;
            LastStepWork = 0;
        }

        private void MarkChanged(string detail)
        {
            Changed = true; ValidationCompleted = true;
            Reason = "경로 계산 중 관측 상태 변경: " + detail;
        }

        public bool StepValidation(int workBudget)
        {
            LastStepWork = 0;
            if (ValidationCompleted || workBudget <= 0) return ValidationCompleted;
            if (!Ready || pendingJobs != 0) return false;
            tr = new AITileReader(server);
            int budget = Math.Min(32, workBudget);
            while (!ValidationCompleted && LastStepWork < budget)
            {
                LastStepWork++;
                switch (validationPhase)
                {
                    case 0:
                        if ((checkMovement && !math.all(AIBody.Pos(server, player) == Position)) ||
                            !math.all(AIEdge.StandTile(server, Position) == Start) ||
                            (usesEdgePolicy && AIEdge.PlanActive != edgeActive))
                        { MarkChanged("위치 또는 경계 조건"); break; }
                        int damage = PlannedDamage(server, player, out int effect, out string note);
                        if (damage != Damage || effect != MiningEffect || note != DamageNote)
                        { MarkChanged("계획 도구 피해"); break; }
                        validationPhase = 1;
                        break;
                    case 1:
                    case 2:
                        // 2026-11-08 user decision: moving objects/critters (blocked-object count or position) no longer
                        // cancel a finished preparation. The choices are sent; each chosen action plans its route again
                        // from the current objects when it starts. Terrain and edge changes below still apply.
                        validationPhase = 3;
                        break;
                    case 3:
                        if (validationIndex >= readTiles.Count) { validationPhase = 4; break; }
                        var t = readTiles[validationIndex++];
                        var original = tileFacts[t];
                        var current = ReadFact(t, objBlocked, false);
                        if (!original.Equals(current))
                        { MarkChanged("타일 (" + t.x + "," + t.y + ")"); break; }
                        if (edgeFacts.TryGetValue(t, out bool edge) &&
                            edge != (edgeActive && current.Wall && AIEdge.OpensEdge(t)))
                            MarkChanged("경계 타일 (" + t.x + "," + t.y + ")");
                        break;
                    case 4:
                        // Recheck moving/tool inputs at the end as validation itself may span several frames.
                        if ((checkMovement && !math.all(AIBody.Pos(server, player) == Position)) ||
                            !math.all(AIEdge.StandTile(server, Position) == Start) ||
                            (usesEdgePolicy && AIEdge.PlanActive != edgeActive))
                        { MarkChanged("검증 중 위치"); break; }
                        int finalDamage = PlannedDamage(server, player, out int finalEffect, out string finalNote);
                        if (finalDamage != Damage || finalEffect != MiningEffect || finalNote != DamageNote)
                        { MarkChanged("검증 중 계획 도구 피해"); break; }
                        ValidationCompleted = true;
                        break;
                }
            }
            return ValidationCompleted;
        }

    }

    // One pair per effective damage. Production plans all use the batch's captured damage; TEST ONLY
    // damage overrides retain their distinct costs without restarting either tree for each target.
    internal sealed class SharedTrees
    {
        private readonly PlanBatch batch;
        private readonly int damage;
        private readonly Dictionary<int2, int> costs = new Dictionary<int2, int>();
        private AITunnelSearch.SharedSearchJob closed, open;
        public int Unknown { get; private set; }
        public int UnknownWalls { get; private set; }
        public int ToolShortCount { get; private set; }
        public int ToolShortRed { get; private set; }
        public ObjectID ToolShortExample { get; private set; }

        internal SharedTrees(PlanBatch batch, int damage) { this.batch = batch; this.damage = damage; }

        public AITunnelSearch.SharedSearchJob Tree(bool allowEdge)
        {
            // With no active edge policy both modes are exactly the same graph.
            bool openMode = allowEdge || !batch.edgeActive;
            var tree = openMode ? open : closed;
            if (tree != null) return tree;
            tree = new AITunnelSearch.SharedSearchJob((batch.Start.x, batch.Start.y),
                (int.MinValue, int.MinValue), (int.MaxValue, int.MaxValue), Cost,
                openMode ? (Func<int, int, bool>)null : OpensEdge);
            if (openMode) open = tree; else closed = tree;
            batch.CountSearch();
            return tree;
        }

        public bool Step(AITunnelSearch.SharedSearchJob tree, int workBudget) => batch.StepTree(tree, workBudget);
        private bool OpensEdge(int x, int y) => batch.OpensEdge(new int2(x, y));
        public int Hits(int x, int y)
        {
            return batch.TryFact(new int2(x, y), out var f) && f.WallOk && damage > f.Red
                ? (int)math.ceil(f.Hp / (float)(damage - f.Red)) : 0;
        }
        private int Cost(int x, int y)
        {
            var t = new int2(x, y);
            if (costs.TryGetValue(t, out int value)) return value;
            var f = batch.Fact(t);
            value = f.Wall ? (f.WallOk && damage > f.Red ? 1 + 2 * Hits(x, y) : -1) : f.Cost;
            costs.Add(t, value);
            if (!f.Known) Unknown++;
            else if (f.Wall && !f.WallOk) UnknownWalls++;
            else if (f.Wall && value < 0)
            {
                ToolShortCount++;
                if (ToolShortExample == ObjectID.None) ToolShortExample = f.Object;
                ToolShortRed = ToolShortRed == 0 ? f.Red : math.min(ToolShortRed, f.Red);
            }
            return value;
        }
    }

    // Target work consists of a tree query, route reconstruction and the same two-pass choice.
    // A closed tree query handles a protected target as a terminal edge; it never starts another search.
    private sealed class SharedPlanSearch
    {
        public readonly SharedTrees Trees;
        private readonly int2 target;
        private readonly bool allowEdge, detourCheck;
        private AITunnelSearch.SharedSearchJob firstTree, secondTree;
        private AITunnelSearch.SharedQueryJob first, second;
        private int phase;
        public bool IsCompleted => phase == 8;
        public int LastStepWork { get; private set; }
        public List<(int x, int y)> Result { get; private set; }
        public List<(int x, int y)> Detour { get; private set; }
        public bool UsedEdge { get; private set; }
        public bool FirstCapped => first != null && first.Capped;
        public bool AllCapped { get; private set; }
        public int EdgeClosed => allowEdge || first == null ? 0 : first.EdgeClosed;
        public int Passes { get; private set; } = 1;
        public float FirstEst { get; private set; } = -1f;
        public float DetourEst { get; private set; } = -1f;
        public double Ms1 => (firstTree?.ElapsedMilliseconds ?? 0) + (first?.ElapsedMilliseconds ?? 0);
        public double Ms2 => (secondTree?.ElapsedMilliseconds ?? 0) + (second?.ElapsedMilliseconds ?? 0);

        public SharedPlanSearch(SharedTrees trees, int2 target, bool allowEdge, bool detourCheck)
        {
            Trees = trees; this.target = target; this.allowEdge = allowEdge;
            this.detourCheck = detourCheck && !allowEdge;
        }
        private void Finish(List<(int x, int y)> route) { Result = route; phase = 8; }

        public bool Step(int workBudget)
        {
            LastStepWork = 0;
            while (!IsCompleted && LastStepWork < workBudget)
            {
                if (phase == 0 || phase == 4)
                {
                    if (phase == 0 && firstTree == null) firstTree = Trees.Tree(allowEdge);
                    if (phase == 4 && secondTree == null) secondTree = Trees.Tree(true);
                    var tree = phase == 0 ? firstTree : secondTree;
                    bool done = Trees.Step(tree, workBudget - LastStepWork);
                    LastStepWork += tree.LastStepWork;
                    if (!done) return false;
                    phase++;
                    continue;
                }
                if (phase == 2 || phase == 6)
                {
                    var query = phase == 2 ? first : second;
                    bool done = query.Step(workBudget - LastStepWork);
                    LastStepWork += query.LastStepWork;
                    if (!done) return false;
                    phase++;
                    continue;
                }
                LastStepWork++;
                switch (phase)
                {
                    case 1:
                        first = firstTree.CreateQuery((target.x, target.y), Trees.Hits);
                        phase = 2;
                        break;
                    case 3:
                        if (first.Result != null) FirstEst = AITunnelSearch.EstTiles(first.Result.Count, first.Hits);
                        if (EdgeClosed == 0)
                        {
                            AllCapped = first.Result == null && FirstCapped;
                            Finish(first.Result);
                        }
                        else if (first.Result == null ||
                            (detourCheck && AITunnelSearch.NeedsDetourCheck(first.Result.Count, first.IntermediateHits > 0, EdgeClosed)))
                        { Passes = 2; phase = 4; }
                        else Finish(first.Result);
                        break;
                    case 5:
                        second = secondTree.CreateQuery((target.x, target.y), Trees.Hits);
                        phase = 6;
                        break;
                    case 7:
                        if (second.Result != null) DetourEst = AITunnelSearch.EstTiles(second.Result.Count, second.Hits);
                        if (first.Result == null)
                        {
                            UsedEdge = second.Result != null;
                            // An exhausted closed graph proves only that the edge must be opened. If the
                            // open query is capped, reachability is still unknown regardless of the first pass.
                            AllCapped = second.Result == null && second.Capped;
                            Finish(second.Result);
                        }
                        else
                        {
                            if (second.Result != null && AITunnelSearch.OfferDetour(FirstEst, DetourEst)) Detour = second.Result;
                            Finish(first.Result);
                        }
                        break;
                }
            }
            return IsCompleted;
        }
    }

    // Main-thread continuation. Standalone jobs validate their own batch before returning.
    // Shared jobs retain the captured position while the character keeps walking.
    public sealed class PlanJob
    {
        private readonly World server;
        private readonly int2 target;
        private readonly int damageOverride;
        private readonly bool allowEdge, detourCheck;
        private readonly PlanBatch batch;
        private readonly bool validateOnCompletion, useShared;
        private readonly TunnelPlan plan = new TunnelPlan();
        private readonly HashSet<int2> visited = new HashSet<int2>();
        private readonly HashSet<int2> toolShort = new HashSet<int2>();
        private readonly System.Diagnostics.Stopwatch searchTime = new System.Diagnostics.Stopwatch();
        private int unknown, phase, fillIndex;
        private string reason = "", fillRisk = "";
        public bool Changed => batch.Changed;
        public bool ValidationCompleted => batch.ValidationCompleted;
        public int LastValidationWork { get; private set; }
        private AITunnelSearch.FindWithDetourJob search;
        private SharedPlanSearch sharedSearch;
        private List<(int x, int y)> found, detour;
        private bool allCapped;
        private float est1, est2;
        private double ms1, ms2;
        public bool IsCompleted { get; private set; }
        public int LastStepWork { get; private set; }
        private TunnelPlan result;
        public TunnelPlan Result { get => Changed ? null : result; private set => result = value; }
        public string Reason => Changed ? batch.Reason : reason;

        public PlanJob(World server, Entity player, int2 target, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false)
            : this(new PlanBatch(server, player, null, true), target, damageOverride, allowEdge, detourCheck, true, false) { }

        internal PlanJob(PlanBatch batch, int2 target, int damageOverride, bool allowEdge, bool detourCheck, bool validateOnCompletion, bool useShared)
        {
            this.batch = batch; server = batch.server; this.target = target;
            this.damageOverride = damageOverride; this.allowEdge = allowEdge; this.detourCheck = detourCheck;
            this.validateOnCompletion = validateOnCompletion; this.useShared = useShared;
            batch.RegisterJob(allowEdge);
        }

        public void RunUntilDone()
        {
            while (!Step(4096)) { }
        }

        // One unit is one initialization/finalization operation, one core search work unit, or one route tile.
        // Capturing the batch object scope is one indivisible query; work units do not imply a millisecond cap.
        public bool Step(int workBudget)
        {
            LastStepWork = 0;
            if (IsCompleted || workBudget <= 0) return IsCompleted;
            while (!IsCompleted && LastStepWork < workBudget)
            {
                if (phase == 4)
                {
                    batch.BeginReadStep();
                    searchTime.Start();
                    bool done;
                    try
                    {
                        if (sharedSearch != null)
                        {
                            done = sharedSearch.Step(workBudget - LastStepWork);
                            LastStepWork += sharedSearch.LastStepWork;
                        }
                        else
                        {
                            done = search.Step(workBudget - LastStepWork);
                            LastStepWork += search.LastStepWork;
                        }
                    }
                    finally { searchTime.Stop(); }
                    if (!done) return false;
                    phase = 5;
                    continue;
                }
                if (phase == 9)
                {
                    bool validated = StepValidation(Math.Min(32, workBudget - LastStepWork));
                    LastStepWork += LastValidationWork;
                    if (!validated) return false;
                    if (Changed) { Result = null; IsCompleted = true; return true; }
                    phase = 10;
                    continue;
                }
                if (phase == 0 && !batch.Ready)
                {
                    batch.StepCapture(workBudget - LastStepWork);
                    LastStepWork += batch.LastStepWork;
                    if (!batch.Ready || LastStepWork >= workBudget) return false;
                }
                LastStepWork++;
                switch (phase)
                {
                    case 0:
                        Calls++;
                        plan.damage = batch.Damage; plan.miningEffect = batch.MiningEffect; plan.damageNote = batch.DamageNote;
                        if (damageOverride > 0) { plan.damage = damageOverride; plan.damageNote = $"시험용 가정 피해 {damageOverride}"; }
                        phase = 3;
                        break;
                    case 3:
                        if (!useShared)
                            search = new AITunnelSearch.FindWithDetourJob((batch.Start.x, batch.Start.y), (target.x, target.y),
                                (int.MinValue, int.MinValue), (int.MaxValue, int.MaxValue),
                                (x, y) => Cost(new int2(x, y)), OpensEdgeAt, HitsAt, detourCheck && !allowEdge);
                        else sharedSearch = new SharedPlanSearch(batch.Trees(plan.damage), target, allowEdge, detourCheck);
                        phase = 4;
                        break;
                    case 5:
                        ReadSearchResult();
                        if (plan.passes == 2) TwoPass++;
                        phase = found == null ? 8 : 6;
                        break;
                    case 6:
                        if (FillNext(plan, found)) break;
                        plan.SetRiskText(server, fillRisk);
                        if (detour == null) { phase = 8; break; }
                        plan.alt = new TunnelPlan { damage = plan.damage, miningEffect = plan.miningEffect, damageNote = plan.damageNote, edgeRoute = true, sharedSearch = plan.sharedSearch };
                        fillIndex = 0; fillRisk = ""; phase = 7;
                        break;
                    case 7:
                        if (FillNext(plan.alt, detour)) break;
                        plan.alt.SetRiskText(server, fillRisk);
                        phase = 8;
                        break;
                    case 8:
                        batch.FinishJob();
                        if (validateOnCompletion) { BeginValidation(); phase = 9; }
                        else phase = 10;
                        break;
                    case 10:
                        FinishSearch();
                        LastToolShortRed = plan.toolShortRed;
                        Result = found == null ? null : plan;
                        IsCompleted = true;
                        break;
                }
            }
            return IsCompleted;
        }

        private int Hits(PlanBatch.TileFact f) => f.WallOk && plan.damage > f.Red
            ? (int)math.ceil(f.Hp / (float)(plan.damage - f.Red)) : 0;

        private int Cost(int2 t)
        {
            var f = batch.Fact(t);
            int hits = f.Wall ? Hits(f) : 0;
            int cost = f.Wall ? (f.WallOk && plan.damage > f.Red ? 1 + 2 * hits : -1) : f.Cost;
            if (visited.Add(t))
            {
                if (!f.Known) unknown++;
                else if (f.Wall && cost < 0)
                {
                    toolShort.Add(t);
                    if (plan.toolShortExample == ObjectID.None) plan.toolShortExample = f.Object;
                    if (f.WallOk) plan.toolShortRed = plan.toolShortRed == 0 ? f.Red : math.min(plan.toolShortRed, f.Red);
                }
            }
            return cost;
        }

        private bool OpensEdgeAt(int x, int y) => !allowEdge && batch.OpensEdge(new int2(x, y));
        private int HitsAt(int x, int y) => batch.TryFact(new int2(x, y), out var f) ? Hits(f) : 0;

        public void BeginValidation() => batch.BeginValidation();
        public bool StepValidation(int workBudget)
        {
            bool done = batch.StepValidation(workBudget);
            LastValidationWork = batch.LastStepWork;
            if (Changed) Result = null;
            return done;
        }

        private void ReadSearchResult()
        {
            if (sharedSearch != null)
            {
                found = sharedSearch.Result; detour = sharedSearch.Detour;
                plan.edgeRoute = sharedSearch.UsedEdge; plan.edgeClosed = sharedSearch.EdgeClosed;
                plan.firstCapped = sharedSearch.FirstCapped; plan.passes = sharedSearch.Passes;
                allCapped = sharedSearch.AllCapped; est1 = sharedSearch.FirstEst; est2 = sharedSearch.DetourEst;
                ms1 = sharedSearch.Ms1; ms2 = sharedSearch.Ms2;
                plan.sharedSearch = true;
                plan.toolShortSeen = sharedSearch.Trees.ToolShortCount;
                plan.toolShortRed = sharedSearch.Trees.ToolShortRed;
                plan.toolShortExample = sharedSearch.Trees.ToolShortExample;
                unknown = sharedSearch.Trees.Unknown;
            }
            else
            {
                found = search.Result; detour = search.Detour;
                plan.edgeRoute = search.UsedEdge; plan.edgeClosed = search.EdgeClosed;
                plan.firstCapped = search.FirstCapped; plan.passes = search.Passes;
                allCapped = search.AllCapped; est1 = search.FirstEst; est2 = search.DetourEst;
                ms1 = search.Ms1; ms2 = search.Ms2;
                plan.toolShortSeen = toolShort.Count;
            }
        }

        private void FinishSearch()
        {
            plan.searchMs = (float)searchTime.Elapsed.TotalMilliseconds;
            int passes = plan.passes;
            if (plan.edgeClosed > 0)
            {
                // 검수 H3: once per target and result (the times change every call; the build's sum line stays every decision)
                string result = $"{(plan.edgeRoute ? (plan.firstCapped ? "the first pass hit its search cap, the second opens them" : "no way without them, the second pass opens them") : found == null ? (allCapped ? "not found within the search (cap)" : "no way either way") : detour != null ? $"first way {found.Count} tiles est {est1:F0}, edge way {detour.Count} tiles est {est2:F0} -> offered" : passes == 2 ? $"first way {found.Count} tiles est {est1:F0} kept (edge way est {est2:F0})" : "a way without them")}" +
                    $" ({(plan.sharedSearch ? "shared search observed edge walls" : "edge walls met")} {plan.edgeClosed}; detour check {(detourCheck ? $"on at {AITunnelSearch.DetourMinTiles} tiles or walls, gain {AITunnelSearch.DetourGainTiles} tiles" : "off")})";
                AIPlaceGoal.LogChangedBy($"tunnel:{target.x},{target.y}", result,
                    $"TUNNEL: plan to ({target.x},{target.y}) ({(plan.sharedSearch ? "shared tree query; tree times cumulative" : "cache miss")}) with the edge walls closed: passes {passes} ({ms1:F1} ms{(passes == 2 ? $" + {ms2:F1} ms" : "")}); {result}");
            }
            LastToolShortRed = plan.toolShortRed;
            if (found == null)
            {
                if (plan.sharedSearch)
                {
                    reason = allCapped ? "공유 검색 상한 안에 경로를 찾지 못함(길 없음은 미확정)"
                        : "공유 검색에서 관측한 범위에 뚫을 수 있는 경로 없음";
                    if (plan.toolShortSeen > 0) reason += $"; 도구 부족 벽 {plan.toolShortSeen}개 관측({plan.toolShortExample}, 추정 피해 {plan.damage})";
                    if (unknown > 0) reason += $"; 미관측 경계 {unknown}칸";
                    if (sharedSearch.Trees.UnknownWalls > 0) reason += $"; 벽 정보 미확인 {sharedSearch.Trees.UnknownWalls}칸";
                    return;
                }
                reason = allCapped ? $"이번 검색 예산 안에 경로를 찾지 못함(탐색 상한, 길 없음은 미확정); 관측한 도구 부족 벽 {toolShort.Count}개"
                    : toolShort.Count > 0 ? $"뚫을 수 있는 길 없음: 도구 부족 벽 {toolShort.Count}개({plan.toolShortExample}, 추정 피해 {plan.damage})로 막힘"
                    : unknown > 0 ? "뚫을 수 있는 길 없음: 아직 못 본 곳을 지나야 함" : "뚫을 수 있는 길 없음";
                return;
            }
        }

        // Each call adds at most one route tile and inspects its wall/risk data, so long paths and alternatives
        // cannot turn the completion frame into a synchronous path-copy/metadata scan.
        private bool FillNext(TunnelPlan to, List<(int x, int y)> route)
        {
            if (fillIndex >= route.Count) return false;
            var q = route[fillIndex++];
            var t = new int2(q.x, q.y);
            to.path.Add(t);
            if (batch.TryFact(t, out var f) && f.Wall)
            {
                int hits = Hits(f);
                to.walls.Add((t, f.Object, hits));
                to.totalHits += hits;
                if (fillRisk == "") fillRisk = f.Risk;
            }
            return true;
        }
    }

}

// --------------------------------------------------------------------------------------------- tunnel skill

// Digs toward a target: mines the next wall on the planned route, walks when the route is open, replans after every
// step. Done when the target wall is gone (wall target), the AI is next to the target (walkable target), or — for a
// creature — when the route is open (attacking is a separate decision).
public class TunnelSkill : AISkill
{
    private readonly int2 _target;
    private readonly bool _targetIsWall;
    private readonly bool _openIsEnough;
    private readonly string _what;
    private AISkill _child;
    private int _wallsMined, _steps;
    private int2 _childTarget;
    private bool _minedTarget, _goneOnArrival;
    private const int MaxWalkSegment = 30;
    private Dictionary<ObjectID, int> _invBefore;
    private float _lastProgress;
    private float _bestDist = float.MaxValue;
    private int _firstWalls = -1;

    // 제작대-놓기 실게임 D1 (코어-안전지대와 맞춘 조건): a goto / place / home walk or an enemy dig (its option set this; null = no check). A route
    // replanned while running onto edge walls the option did not state stops here with the edge sentence (the decision
    // gets it back); the same edge walls the option stated go on.
    public List<int2> EdgeOk;
    // 우회 사실: the "mine the edge" option's walk — plans with the edge open (its own key, digedge:x,y)
    private bool _allowEdge;
    public bool AllowEdge { get => _allowEdge; set { _allowEdge = value; if (value) Key = $"digedge:{_target.x},{_target.y}"; } }

    public TunnelSkill(int2 target, bool targetIsWall, bool openIsEnough, string what)
    {
        Name = "파들어가기";
        _target = target; _targetIsWall = targetIsWall; _openIsEnough = openIsEnough; _what = what;
        Key = $"dig:{target.x},{target.y}";
    }

    public override bool Start(World server, Entity player, float now)
    {
        _invBefore = AIBody.InventoryCounts(server, player);
        _lastProgress = now;
        var plan = AITunnel.Plan(server, player, _target, out string why, -1, _allowEdge);
        if (plan == null) { Status = SkillStatus.Failed; Reason = why; return false; }
        _firstWalls = plan.walls.Count;
        Facts.Add($"target={_what} ({_target.x},{_target.y}) wall={_targetIsWall}; plan: path {plan.path.Count} tiles, walls {plan.walls.Count} [{plan.WallSummary()}], est hits {plan.totalHits}, damage {plan.damage} ({plan.damageNote}), tool-short walls avoided {plan.toolShortSeen}");
        AIPlayerSpikeMod.Log($"SKILL 파들어가기: plan to {_what} ({_target.x},{_target.y}): path {plan.path.Count}, walls {plan.walls.Count} [{plan.WallSummary()}], hits {plan.totalHits}, damage {plan.damage} ({plan.damageNote}), tool-short {plan.toolShortSeen}");
        return NextStep(server, player, now, plan);
    }

    private bool TargetDone(World server, Entity player, out string why)
    {
        why = "";
        var tr = new AITileReader(server);
        float3 pos = AIBody.Pos(server, player);
        if (_targetIsWall)
        {
            // judged only when the target is in view (a remembered target is not known until seen again)
            if (!AIVision.CanSee(pos, _target)) return false;
            if (!tr.HasWall(_target, out _, out _, out _))
            {
                if (_minedTarget) { why = $"목표 벽 ({_target.x},{_target.y}) 부숨"; return true; }
                why = $"와서 보니 목표 ({_target.x},{_target.y})에 벽이 이미 없음(기억이 옛것)";
                _goneOnArrival = true;
                return true;
            }
            return false;
        }
        if (math.distance(pos.xz, (float2)_target) <= 1.2f) { why = $"목표 ({_target.x},{_target.y}) 도착"; return true; }
        return false;
    }

    // Picks and starts the next child step. Returns false when the skill finished (Status set).
    private bool NextStep(World server, Entity player, float now, TunnelPlan plan)
    {
        if (TargetDone(server, player, out string doneWhy)) { Finish(_goneOnArrival ? SkillStatus.Failed : SkillStatus.Done, Summary(server, player, doneWhy)); return false; }
        if (plan == null)
        {
            plan = AITunnel.Plan(server, player, _target, out string why, -1, _allowEdge);
            if (plan == null) { Finish(SkillStatus.Failed, Summary(server, player, why)); return false; }
        }
        if (EdgeOk != null && AIEdge.PlanActive)
        {
            var edgeOk = new HashSet<(int x, int y)>(); foreach (var e in EdgeOk) edgeOk.Add((e.x, e.y));
            var edge = AIBaseGuard.EdgeWallsOn(plan.walls.ConvertAll(w => (w.p.x, w.p.y)), t => AIEdge.WallFact(new int2(t.x, t.y)), edgeOk);
            if (edge.Count > 0)
            {
                string stop = AIBaseGuard.DigEdgeStopText(edge);
                AIPlayerSpikeMod.Log($"SKILL 파들어가기: {stop}");
                try { AIPlaceGoal.NoteStop(stop); } catch (Exception e) { AIPlayerSpikeMod.LogError("place: note stop failed", e); }
                // 검수 E3: this target's cached route is dropped, so the next option for it plans afresh: it comes back
                // only with the edge walls in its facts and EdgeOk (not offered again blind; a choice of it goes on); the
                // failure also counts toward leaving the key out (AIBrain fails)
                AIBrain.ForgetPlan(_target);
                Finish(SkillStatus.Failed, Summary(server, player, stop));
                return false;
            }
        }
        _steps++;
        if (plan.walls.Count == 0)
        {
            if (_openIsEnough) { Finish(SkillStatus.Done, Summary(server, player, "길이 열림(이제 걸어서 갈 수 있음)")); return false; }
            // Long walks go in segments along the planned (known-tile) route: the game's pathfinding searches 40
            // tiles around, and each segment end is a replanning point.
            int2 wpt = plan.path.Count > MaxWalkSegment ? plan.path[MaxWalkSegment - 1] : _target;
            _child = new GotoSkill(wpt, math.all(wpt == _target) ? _what : _what + " 경유");
            _childTarget = wpt;
            if (!math.all(wpt == _target)) AIPlayerSpikeMod.Log($"SKILL 파들어가기: step {_steps}: walk segment to ({wpt.x},{wpt.y}) (route {plan.path.Count} tiles, open)");
        }
        else
        {
            var w = plan.walls[0];
            int idx = plan.path.IndexOf(w.p);
            float3 pos = AIBody.Pos(server, player);
            int2 before = idx > 0 ? plan.path[idx - 1] : AIEdge.StandTile(server, pos);
            if (idx - 1 > MaxWalkSegment - 1) before = plan.path[MaxWalkSegment - 1];
            if (idx > 0 && math.distance(pos.xz, (float2)before) > 1.0f)
            {
                // The route is open up to the first wall: walk there first (the wall may still be out of view).
                _child = new GotoSkill(before, "굴 입구") { HoldSlot = AIBody.ChooseSlot(server, player, true, out _) };
                _childTarget = before;
                AIPlayerSpikeMod.Log($"SKILL 파들어가기: step {_steps}: walk to ({before.x},{before.y}) before wall {w.obj} ({w.p.x},{w.p.y}); walls left on route {plan.walls.Count}, est hits {plan.totalHits}");
            }
            else
            {
                string limit = AIGathering.WallLimit(server, player, w.p, "TunnelSkill 다음 파괴 자식 시작 전");
                if (limit != null) { ObserveOnce("explicit-max:" + w.p, limit); Finish(SkillStatus.Failed, Summary(server, player, limit)); return false; }
                _child = new MineSkill(w.p, false) { NoPickupWait = !math.all(w.p == _target) };
                _childTarget = w.p;
                AIPlayerSpikeMod.Log($"SKILL 파들어가기: step {_steps}: mine {w.obj} ({w.p.x},{w.p.y}) ~{w.hits} hits; walls left on route {plan.walls.Count}, est hits {plan.totalHits}");
            }
        }

        _child.StartTime = now;
        _child.StartPos = AIBody.Pos(server, player);
        bool ok;
        try { ok = _child.Start(server, player, now); }
        catch (Exception e) { AIPlayerSpikeMod.LogError("SKILL 파들어가기: child start threw", e); ok = false; _child.Status = SkillStatus.Failed; _child.Reason = e.GetType().Name; }
        if (!ok || _child.Status != SkillStatus.Running)
        {
            if (_child.Status == SkillStatus.Running) _child.Status = SkillStatus.Failed;
            return ChildEnded(server, player, now);
        }
        return true;
    }

    // Handles a finished child; returns false when this skill finished.
    private bool ChildEnded(World server, Entity player, float now)
    {
        var c = _child;
        _child = null;
        try { c.Cleanup(server); } catch { }
        string st = c.Status == SkillStatus.Done ? "완료" : c.Status == SkillStatus.Failed ? "실패" : "취소";
        AIPlayerSpikeMod.Log($"SKILL 파들어가기: child {c.Name} {c.Key} {st}: {c.Reason}");
        if (c.Status == SkillStatus.Done)
        {
            if (c is MineSkill) { _wallsMined++; if (math.all(_childTarget == _target)) _minedTarget = true; }
            _lastProgress = now;
        }
        else
        {
            // refactor S2b: decided by the value (AISkill.End); with the test switch, the sentence is compared (OUTCOME-MISMATCH)
            bool down = c.End == SkillEnd.Down;
            if (AITestSwitches.Test)
            {
                string mm = AIOutcomes.Mismatch("skill-down", AIOutcomes.DownText(c.Reason), down, $"skill={c.Name} key={c.Key} end={c.End} reason='{c.Reason}' (child of {Name})");
                if (mm != null) AIPlayerSpikeMod.Log("OUTCOME-MISMATCH " + mm);
            }
            if (down) {Finish(SkillStatus.Failed, Summary(server, player, "AI가 쓰러짐"), SkillEnd.Down); return false; }
            int2 k = _childTarget;
            // where the AI was when it failed, not only the child's target: a walk segment's target is a point ahead on
            // the route, so "같은 칸 (-35,50)" read as if the AI stood there while it was stuck 26 tiles back at (-61,52)
            // (2026-11-04 memory test, walk to a remembered Furnace)
            int2 at = AIEdge.StandTile(server, AIBody.Pos(server, player));
            string where = c is MineSkill ? $"같은 칸 ({k.x},{k.y})에서" : $"({at.x},{at.y})에 서서 ({k.x},{k.y}) 쪽으로 가다가 같은 곳에서";
            Finish(SkillStatus.Failed, Summary(server, player, $"{where} 실행 실패: {c.Reason}"), c.End);
            return false;
        }
        return NextStep(server, player, now, null);
    }

    private string Summary(World server, Entity player, string why)
    {
        var after = AIBody.InventoryCounts(server, player);
        string diff = AIBody.InventoryDiff(_invBefore ?? new Dictionary<ObjectID, int>(), after);
        Facts.Add($"walls mined {_wallsMined}/{(_firstWalls < 0 ? "?" : _firstWalls.ToString())} planned at start, steps {_steps}, inventory {diff}");
        return $"{why} · 벽 {_wallsMined}개 뚫음 · 가방 {diff}";
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        float3 pos = AIBody.Pos(server, player);
        float d = math.distance(pos.xz, (float2)_target);
        if (d < _bestDist - 0.5f) { _bestDist = d; _lastProgress = now; }
        if (now - _lastProgress > 30f) ObserveOnce("tunnel-no-progress", "30초 동안 진전 없음");
        if (now - StartTime > 300f) ObserveOnce("tunnel-duration", "파들어가기 300초 경과");
        if (_child == null)
        {
            if (!NextStep(server, player, now, null)) return BodyInput.Idle;
        }
        BodyInput bi;
        try { bi = _child.Tick(server, player, now); }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("SKILL 파들어가기: child tick threw", e);
            _child.Status = SkillStatus.Failed; _child.Reason = "오류 " + e.GetType().Name; bi = BodyInput.Idle;
        }
        if (_child.Status != SkillStatus.Running)
        {
            if (!ChildEnded(server, player, now)) return BodyInput.Idle;
            return BodyInput.Idle;
        }
        return bi;
    }

    public override void Cleanup(World server)
    {
        if (_child != null) { try { _child.Cleanup(server); } catch { } _child = null; }
    }

    public override string Progress(World server, Entity player)
    {
        float d = math.distance(AIBody.Pos(server, player).xz, (float2)_target);
        string left = "";
        try { var plan = AITunnel.Plan(server, player, _target, out _, -1, _allowEdge); if (plan != null) left = $", 남은 벽 {plan.walls.Count}개{plan.RiskText(server)}"; } catch { }
        string step = _child == null ? "" : $", 지금 단계: {_child.Name}{(_child is MineSkill ? $" ({_childTarget.x},{_childTarget.y})" : "")}";
        return $"대상 {_what} ({_target.x},{_target.y})까지 {d:F1}칸{left}, 지금까지 벽 {_wallsMined}개 뚫음{step}";
    }

    // Ends when the target is reached/mined: only the last step can be close to the end.
    public override float EtaSeconds(World server, Entity player)
    {
        if (_child == null) return -1f;
        if (_child is MineSkill && math.all(_childTarget == _target) && _targetIsWall) return _child.EtaSeconds(server, player);
        if (_child is GotoSkill && math.all(_childTarget == _target) && !_targetIsWall)
            return math.max(0f, math.distance(AIBody.Pos(server, player).xz, (float2)_target) - 1.2f) / AIBody.WalkSpeed;
        return -1f;
    }
}
