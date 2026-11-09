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
    public int toolShortSeen;                           // undiggable walls met at the search frontier
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
        private readonly int _dmg, _maxExpand;
        private readonly Func<int2, bool> _skip;
        private readonly HashSet<int2> _objBlocked;
        private readonly Dictionary<int, (bool ok, int hp, int red)> _wallInfo = new Dictionary<int, (bool, int, int)>();
        private readonly Dictionary<string, (int2 t, int nov, int cost)> _best = new Dictionary<string, (int2, int, int)>();
        private readonly Dictionary<int2, int> _cost = new Dictionary<int2, int>();
        private readonly Heap _heap = new Heap();
        private static readonly int2[] Dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        public int Expanded;
        public bool Done;
        public List<(int2 t, string dir, int novelty, int cost)> Result;
        public readonly System.Diagnostics.Stopwatch Time = new System.Diagnostics.Stopwatch();

        public FrontierJob(World server, Entity player, int maxExpand, Func<int2, bool> skip)
        {
            _server = server; _maxExpand = maxExpand; _skip = skip;
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
            while (_heap.Count > 0 && Expanded < _maxExpand && _best.Count < 4 && Expanded < until)
            {
                var (f, c) = _heap.Pop();
                if (_cost.TryGetValue(c, out int cc) && f > cc) continue;
                Expanded++;
                int2 v = c - Start;
                if (math.cmax(math.abs(v)) >= 3 && ((c.x ^ c.y) & 1) == 0 && !_skip(c))   // every other tile is enough to find a frontier
                {
                    string dir = math.abs(v.x) >= math.abs(v.y) ? (v.x > 0 ? "동" : "서") : (v.y > 0 ? "북" : "남");
                    if (!_best.ContainsKey(dir))
                    {
                        int nov = AIMemory.Novelty(c);
                        if (nov > 0) _best[dir] = (c, nov, f);
                    }
                }
                foreach (var d in Dirs)
                {
                    int2 nb = c + d;
                    if (math.cmax(math.abs(nb - Start)) > 120) continue;
                    int k = Cost(nb);
                    if (k < 0) continue;
                    int ng = f + k;
                    if (_cost.TryGetValue(nb, out int og) && og <= ng) continue;
                    _cost[nb] = ng;
                    _heap.Push(ng, nb);
                }
            }
            Time.Stop();
            if (_heap.Count > 0 && Expanded < _maxExpand && _best.Count < 4) return false;
            Done = true;
            Result = new List<(int2, string, int, int)>();
            foreach (var kv in _best) Result.Add((kv.Value.t, kv.Key, kv.Value.nov, kv.Value.cost));
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
        Calls++;
        reason = "";
        var tr = new AITileReader(server);
        float3 pos = AIBody.Pos(server, player);
        int2 start = AIEdge.StandTile(server, pos);   // 재실측 결함 2: the stand tile, not a shut door tile
        var plan = new TunnelPlan();
        plan.damage = PlannedDamage(server, player, out plan.miningEffect, out plan.damageNote);
        if (damageOverride > 0) { plan.damage = damageOverride; plan.damageNote = $"시험용 가정 피해 {damageOverride}"; }
        // 40-tile margin: known routes often detour (e.g. back through the core area); only known tiles are expanded.
        int2 lo = math.min(start, target) - 40, hi = math.max(start, target) + 40;
        if (math.any(hi - lo > 260)) { reason = "대상이 너무 멂(계획 범위 260칸 초과)"; return null; }

        HashSet<int2> objBlocked;
        try { objBlocked = AIObjects.BlockedInView(server, player, out _); } catch { objBlocked = new HashSet<int2>(); }
        var wallInfo = new Dictionary<int, (bool ok, int hp, int red, ObjectID obj)>();
        var tileCost = new Dictionary<int2, int>();     // -1 = blocked
        var toolShort = new HashSet<int2>();
        int unknown = 0;
        int Cost(int2 t)
        {
            if (tileCost.TryGetValue(t, out int c)) return c;
            c = -1;
            bool known = AIVision.CanSee(pos, t) || AIMemory.SeenTile(t);
            if (!known) unknown++;
            else if (tr.HasWall(t, out TileCD w, out _, out _))
            {
                if (!wallInfo.TryGetValue(w.tileset, out var wi))
                {
                    bool ok = AIWallData.TryGet(server, w.tileset, out int hp, out int red, out ObjectID obj);
                    wi = (ok, hp, red, obj);
                    wallInfo[w.tileset] = wi;
                }
                if (wi.ok && plan.damage > wi.red) c = 1 + 2 * (int)math.ceil(wi.hp / (float)(plan.damage - wi.red));
                else { toolShort.Add(t); if (plan.toolShortExample == ObjectID.None) plan.toolShortExample = wi.obj; if (wi.ok) plan.toolShortRed = plan.toolShortRed == 0 ? wi.red : math.min(plan.toolShortRed, wi.red); }
            }
            // 제작대-놓기 실게임 D1: an own edge door or my door is a way through (the walk opens it, goes through, closes
            // it: AINavigator door step), not an object that blocks — so a route by the door is found before one that
            // mines the walls beside it or the edge. Cost DoorStepCost (the open/close time). 검수 E4: in view the
            // object check comes first — another object seen on a remembered door tile (no door there) blocks it.
            else if (tr.Walkable(t) && AIEdge.IsWalkDoorTile(t))
            {
                bool seen = AIVision.CanSee(pos, t), blockedNow = objBlocked.Contains(t);
                c = AITunnelSearch.DoorTileCost(seen, blockedNow, seen && blockedNow && AIEdge.DoorAt(server, t, pos) != null);
            }
            else if (tr.Walkable(t) && !objBlocked.Contains(t)) c = 1;
            tileCost[t] = c;
            return c;
        }

        // 검수 E1: two passes (AITunnelSearch.FindAvoidingEdge) — first with the walls that open the base edge closed
        // (a way by a door or around wins however much longer), then, only when there is none, with them open; such a
        // plan is marked (edgeRoute) and its options say so with the edge sentences. Every caller of Plan gets this.
        var swp = System.Diagnostics.Stopwatch.StartNew();
        bool edgeOn = AIEdge.PlanActive && !allowEdge;
        // 우회 사실 (재검수 G1): when the first way (edge closed) is DetourMinTiles long or mines walls, the edge way is
        // searched too; DetourGainTiles walking tiles or more shorter in estimated time -> plan.alt (its own option, the
        // first way's option says it). G2: the edge test once per tile. G3: a first pass stopped at its cap is said so.
        int HitsAt(int x, int y)
        {
            var q = new int2(x, y);
            if (!tr.HasWall(q, out TileCD w, out _, out _) || !wallInfo.TryGetValue(w.tileset, out var wi) || !wi.ok || plan.damage <= wi.red) return 0;
            return (int)math.ceil(wi.hp / (float)(plan.damage - wi.red));
        }
        var found = AITunnelSearch.FindWithDetour((start.x, start.y), (target.x, target.y), (lo.x, lo.y), (hi.x, hi.y),
            (x, y) => Cost(new int2(x, y)),
            (x, y) => edgeOn && tr.HasWall(new int2(x, y), out _, out _, out _) && AIEdge.OpensEdge(new int2(x, y)),
            HitsAt, detourCheck && !allowEdge,
            out plan.edgeRoute, out plan.edgeClosed, out var detour, out plan.firstCapped, out bool allCapped, out float est1, out float est2, out int passes, out double ms1, out double ms2);
        plan.searchMs = (float)swp.Elapsed.TotalMilliseconds; plan.passes = passes;
        if (passes == 2) TwoPass++;
        if (plan.edgeClosed > 0)
        {
            // 검수 H3: once per target and result (the times change every call; the build's sum line stays every decision)
            string result = $"{(plan.edgeRoute ? (plan.firstCapped ? "the first pass hit its search cap, the second opens them" : "no way without them, the second pass opens them") : found == null ? (allCapped ? "not found within the search (cap)" : "no way either way") : detour != null ? $"first way {found.Count} tiles est {est1:F0}, edge way {detour.Count} tiles est {est2:F0} -> offered" : passes == 2 ? $"first way {found.Count} tiles est {est1:F0} kept (edge way est {est2:F0})" : "a way without them")}" +
                $" (edge walls met {plan.edgeClosed}; detour check {(detourCheck ? $"on at {AITunnelSearch.DetourMinTiles} tiles or walls, gain {AITunnelSearch.DetourGainTiles} tiles" : "off")})";
            AIPlaceGoal.LogChangedBy($"tunnel:{target.x},{target.y}", result,
                $"TUNNEL: plan to ({target.x},{target.y}) (cache miss) with the edge walls closed: passes {passes} ({ms1:F1} ms{(passes == 2 ? $" + {ms2:F1} ms" : "")}); {result}");
        }
        plan.toolShortSeen = toolShort.Count;
        LastToolShortRed = plan.toolShortRed;
        if (found == null)
        {
            reason = toolShort.Count > 0
                ? $"뚫을 수 있는 길 없음: 도구 부족 벽 {toolShort.Count}개({plan.toolShortExample}, 추정 피해 {plan.damage})로 막힘"
                : allCapped ? "뚫을 수 있는 길을 찾기 범위 안에서 못 찾음(탐색 상한)"   // 검수 H2: the cap is no proof of "no way"
                : unknown > 0 ? "뚫을 수 있는 길 없음: 아직 못 본 곳을 지나야 함" : "뚫을 수 있는 길 없음";
            return null;
        }
        void Fill(TunnelPlan to, List<(int x, int y)> route)
        {
            foreach (var q in route) to.path.Add(new int2(q.x, q.y));
            foreach (var t in to.path)
            {
                if (!tr.HasWall(t, out TileCD w, out _, out _)) continue;
                var wi = wallInfo[w.tileset];
                int hits = (int)math.ceil(wi.hp / (float)(to.damage - wi.red));
                to.walls.Add((t, wi.obj, hits));
                to.totalHits += hits;
            }
        }
        Fill(plan, found);
        if (detour != null)
        {
            plan.alt = new TunnelPlan { damage = plan.damage, miningEffect = plan.miningEffect, damageNote = plan.damageNote, edgeRoute = true };
            Fill(plan.alt, detour);
        }
        return plan;
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
    private int _wallsMined, _steps, _sameFail;
    private int2 _lastFailWall = new int2(int.MinValue, int.MinValue);
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
            _sameFail = 0;
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
            if (math.all(k == _lastFailWall)) _sameFail++; else { _sameFail = 1; _lastFailWall = k; }
            // where the AI was when it failed, not only the child's target: a walk segment's target is a point ahead on
            // the route, so "같은 칸 (-35,50)" read as if the AI stood there while it was stuck 26 tiles back at (-61,52)
            // (2026-11-04 memory test, walk to a remembered Furnace)
            int2 at = AIEdge.StandTile(server, AIBody.Pos(server, player));
            string where = c is MineSkill ? $"같은 칸 ({k.x},{k.y})에서" : $"({at.x},{at.y})에 서서 ({k.x},{k.y}) 쪽으로 가다가 같은 곳에서";
            if (_sameFail >= 2) { Finish(SkillStatus.Failed, Summary(server, player, $"{where} 두 번 실패: {c.Reason}")); return false; }
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
        if (now - _lastProgress > 30f) return Finish(SkillStatus.Failed, Summary(server, player, "30초 동안 진전 없음"));
        if (now - StartTime > 300f) return Finish(SkillStatus.Failed, Summary(server, player, "시간 초과(300초)"));
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
        try { var plan = AITunnel.Plan(server, player, _target, out _, -1, _allowEdge); if (plan != null) left = $", 남은 벽 {plan.walls.Count}개"; } catch { }
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
