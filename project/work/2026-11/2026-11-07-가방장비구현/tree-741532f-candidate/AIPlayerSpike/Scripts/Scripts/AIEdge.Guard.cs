// AIEdge.Guard — keeping the base in the game (코어-안전지대 단계 3; the logic is Core/AIBaseGuard.cs, results in
// 12-단계3-구현.md):
// - Reach: the walk grid around the AI (open / own door / unknown / fence-water / closed) and the reach map; per enemy
//   the way (open N tiles, through an own door N tiles, unseen, behind a wall) and whether it can come. AIBrain uses it
//   for the kill facts on attack options, for leaving out attacks that cannot be done, and for the fight state of the
//   combat re-decision and self-defence (an enemy that cannot come drives neither).
// - Broken parts: the remembered edge against the tiles in view now; repair batches (EdgeFillSkill with the opening as
//   its closing group), putting a gone door back, pick-up (PickupSkill), repair materials, the retreat point inside.
// - All of these are options with facts for the action model (progress=false, review G6); test commands start them.
using System;
using System.Collections.Generic;
using System.Text;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static partial class AIEdge
{

    // ------------------------------------------------------------------ the remembered base

    public static BaseShape ShapeNow(World server = null)
    {
        var s = S;
        if (!s.planSet) return null;
        var ins = new List<(int x, int y)>(); foreach (var t in s.inside) ins.Add((t.x, t.y));
        var gp = new List<(int x, int y)>(); foreach (var g in s.gaps) foreach (var t in g) gp.Add((t.x, t.y));
        // placed walls the repair covers (off the inside; a door's side walls also inside, recheck R4) are part of the
        // edge memory too (a door spot's side walls off the cut, review F5)
        var sw = new List<(int x, int y)>(); foreach (var t in s.walls.Keys) if (RepairCovers(t, server)) sw.Add((t.x, t.y));
        // live-run fix 5: once the plan's door is hung, its two side tiles are edge memory even when natural walls
        bool hung = s.door.HasValue && s.doors.ContainsKey(s.door.Value);
        bool? ew = hung ? DoorSidesEastWest(server) : null;
        return AIBaseGuard.Shape(ins, gp, sw, hung ? (s.door.Value.x, s.door.Value.y) : ((int, int)?)null, ew);
    }

    // the hung door's side tiles (live-run fix 5): edge memory (no material, a repair when gone)
    public static bool IsHungDoorSide(int2 t, World server = null)
    {
        var s = S;
        return s.planSet && s.door.HasValue && s.doors.ContainsKey(s.door.Value) && IsDoorSideTile(t, server);
    }

    // the shape once per frame (the facts of many walls / enemies in one build)
    public static BaseShape ShapeCached()
    {
        var s = S;
        if (s.shapeFrame != Time.frameCount) { s.shape = ShapeNow(); s.shapeFrame = Time.frameCount; }
        return s.shape;
    }

    // Recheck R2: the axis of the door's side walls — from the placed door (its passage runs across them), before the
    // door from the plan's door spot (the axis DoorSides counted, kept by Pick / Batches); null when not known yet
    public static bool? DoorSidesEastWest(World server)
    {
        var s = S;
        if (!s.door.HasValue) return null;
        if (server != null && s.doors.ContainsKey(s.door.Value))
        {
            var dv = DoorAt(server, s.door.Value, float3.zero);
            if (dv != null) s.doorSidesEW = !AIDoorPass.PassageEastWest(dv.variation);
        }
        return s.doorSidesEW;
    }

    // a door spot's side tile: one of the two along the door's wall axis (recheck R2: not the passage tiles in front of
    // and behind it); a wall placed there is edge memory
    public static bool IsDoorSideTile(int2 t, World server = null)
    {
        var s = S;
        if (!s.planSet || !s.door.HasValue) return false;
        bool? ew = DoorSidesEastWest(server);
        return ew.HasValue && AIBaseEdgeCore.IsDoorSide((t.x, t.y), (s.door.Value.x, s.door.Value.y), ew.Value);
    }

    // a remembered placed wall the repair takes back when it is gone: off the inside, or a door's side wall (recheck R4)
    public static bool RepairCovers(int2 t, World server = null) { var s = S; return s.walls.ContainsKey(t) && (!s.inside.Contains(t) || IsDoorSideTile(t, server)); }

    // recheck R5: the fact on a mine / dig option for a wall of the base ("" when it is none)
    public static EdgeWallFact WallFact(int2 t) => PlanActive ? AIBaseGuard.WallFact(ShapeCached(), (t.x, t.y)) : default;
    public static string EdgeWallNote(int2 t) => WallFact(t).Text;
    public static bool OpensEdge(int2 t) => WallFact(t).OpensEdge;   // live-run fix 5: a hung door's side wall too (the door falls)
    // live-run fix 7: a big root / an object on an edge tile ("" when none)
    public static string EdgeTileNote(int2 t, string what) => PlanActive ? AIBaseGuard.EdgeTileNote(ShapeCached(), (t.x, t.y), what) : "";

    // an edge tile of the plan: a gap tile, a remembered placed wall the repair covers (also a door's side wall), or not
    // inside with an inside 4-neighbour
    public static bool IsEdgeTile(int2 t)
    {
        var s = S;
        if (!s.planSet) return false;
        if (RepairCovers(t)) return true;
        if (IsHungDoorSide(t) && !s.inside.Contains(t)) return true;   // live-run fix 5: no material wall (mining it lets the door fall)
        if (s.inside.Contains(t)) return false;
        if (s.gaps.Exists(g => g.Contains(t))) return true;
        return s.inside.Contains(t + new int2(1, 0)) || s.inside.Contains(t + new int2(-1, 0)) || s.inside.Contains(t + new int2(0, 1)) || s.inside.Contains(t + new int2(0, -1));
    }

    // The kill facts of one enemy: side and distance from the edge (with a base), the way, its kind.
    public static string KillFacts(World server, Entity player, SeenEnemy e, out EnemyReach r)
    {
        int2 t = EnemyTile(server, e.pos);   // 재실측: an enemy pressed against a shut door is on its side
        r = ReachOf(server, player, t, e.id, e.entity);
        return AIBaseGuard.KillFacts(PlanActive ? ShapeNow(server) : null, (t.x, t.y), r, e.id.ToString(), e.dist);
    }

    // ------------------------------------------------------------------ broken parts

    // The remembered edge against the tiles in view now (a tile out of view is "unchecked": the AI does not know).
    public static BreachScan BreachesNow(World server, Entity player, out BaseShape shape)
    {
        var s = S;
        shape = ShapeNow(server);
        if (shape == null) return null;
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var doors = AIDoorFacts.InWindowLastByTile(server, s.seed, Window + 2);
        EdgeNow Now((int x, int y) u)
        {
            var v = new int2(u.x, u.y);
            if (!AIVision.CanSee(me, (float2)v)) return EdgeNow.Unseen;
            if (doors.ContainsKey(v)) return EdgeNow.OwnDoor;   // a door stands there (open or shut): not broken
            if (tr.HasWall(v, out _, out _, out _) || tr.Top(v).tileType == TileType.fence || blocked.Contains(v) && !AIObjects.StuckOnly(player, v)) return EdgeNow.Closed;   // 재실측: a stuck mark is no object
            if (tr.Walkable(v) || AIFarm.IsWater(tr, v) && !AIPlaceSpot.IsLava(tr, v)) return EdgeNow.Open;
            return EdgeNow.Closed;
        }
        var walls = new List<(int x, int y)>(); foreach (var t in s.walls.Keys) walls.Add((t.x, t.y));
        var ds = new List<(int x, int y)>(); foreach (var t in s.doors.Keys) ds.Add((t.x, t.y));
        int seenV = s.edgeSeen.Version;
        var scan = AIBaseGuard.Breaches(shape, walls, ds, Now, s.edgeSeen);   // 검수 F2: the last looks (a hole seen open stays a breach)
        if (s.edgeSeen.Version != seenV) AIMemory.MarkDirty();   // 재검수 R3: kept in memory.json with the edge memory
        string log = string.Join(", ", scan.list.ConvertAll(b => b.Text));
        float now = Time.realtimeSinceStartup;
        if (log != s.breachLog || now - s.breachLogAt > 30f)
        {
            s.breachLog = log; s.breachLogAt = now;
            AIPlayerSpikeMod.Log($"EDGE breaches: {scan.list.Count} ({log}); edge {scan.edgeTiles}, unchecked {scan.unseen}, gap tiles not built {scan.notBuilt}");
        }
        return scan;
    }

    // A trigger for the action model (단계 3): the broken parts in view grew since the last look (checked once a second
    // while a plan is set and a goal runs). The reason names them; null when nothing new.
    public static string NewBreach(World server, Entity player, float now)
    {
        var s = S;
        if (!s.planSet || now - s.breachCheckAt < 1f) return null;
        s.breachCheckAt = now;
        var scan = BreachesNow(server, player, out _);
        return AIBaseGuard.BreachTrigger(s.breachWatch, s.planVersion, scan?.list);   // review S4: a new plan counts again
    }

    // live-run fix 7: the breach reason still waiting is added to the trigger of the call being built (once)
    public static string WithPendingBreach(string trigger) => AIBaseGuard.TakePending(S.breachWatch, trigger, S.planVersion);   // review S5: only this plan's

    // ------------------------------------------------------------------ an own door passed is closed after (fix 3)

    // every frame (AIBody.Tick): standing in an own door (it is open then) owes it a close; checked shut every 0.25 s
    public static void TrackDoor(World server, Entity player, float now)
    {
        var s = S;
        // 내문-판정 결함 1: the look at my doors (with a plan or without); 32-검수 L4: its own try, the owed-door check runs anyway
        if (s.myDoors.Count > 0 && (now - s.myDoorLookAt >= 0.5f || now < s.myDoorLookAt))
        {
            s.myDoorLookAt = now;
            try { LookMyDoors(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: my door look failed", e); }
        }
        // 32-검수 L3 (HEAD decision: a door passed is closed after): every own door — edge doors and my doors, with a plan or
        // without (IsWalkDoorTile)
        if (!s.planSet && s.myDoors.Count == 0) { s.owe.door = null; return; }
        float3 pos = AIBody.Pos(server, player);
        int2 t = AIBody.TileOf(pos);
        // 33-재검수 N3: the tile the AI stood on before it came into the door (the side it came from)
        if (IsWalkDoorTile(t) && math.all(StandTile(server, pos) == t)) AIBaseGuard.NoteDoorTile(s.owe, (t.x, t.y), true, s.offDoorSet ? (s.offDoor.x, s.offDoor.y) : ((int, int)?)null);
        else if (!IsWalkDoorTile(t)) { s.offDoor = t; s.offDoorSet = true; }
        if (s.owe.door.HasValue && !s.owe.tail && now - s.oweCheckAt >= 0.25f)
        {
            s.oweCheckAt = now;
            var d = s.owe.door.Value;
            var dv = DoorAt(server, new int2(d.x, d.y), pos);
            AIBaseGuard.NoteDoorState(s.owe, dv != null, dv != null && AIDoorPass.IsOpen(dv.variation));
        }
    }

    // at a skill's end: start closing the owed door from the AI's side (true: the skill's end waits for it)
    public static bool TailStart(World server, Entity player, AISkill sk, bool down)
    {
        var s = S;
        // (a door pass that ends by itself closes its own door; one stopped by the program in the middle does not: review F2)
        if (!s.owe.door.HasValue || sk is DoorPassSkill && sk.Status != SkillStatus.Cancelled) return false;   // 32-검수 L3: my doors too (no plan needed)
        float3 pos = AIBody.Pos(server, player);
        var d = s.owe.door.Value;
        var dv = DoorAt(server, new int2(d.x, d.y), pos);
        if (dv == null) { s.owe.door = null; return false; }
        if (dv.kind == DoorKind.Auto || !AIBaseGuard.TailNeeded(s.owe, AIDoorPass.IsOpen(dv.variation), math.distance(pos.xz, new float2(d.x, d.y)), down)) return false;
        // review S1: in the door tile itself it closes from the inside stand (outside only when the inside cannot be stood on)
        bool ew = AIDoorPass.PassageEastWest(dv.variation);
        int2 ax = ew ? new int2(1, 0) : new int2(0, 1), dt = new int2(d.x, d.y);
        var trT = new AITileReader(server);
        int inSign; bool inStand, outStand; string sideWhy;
        if (s.planSet && s.doors.ContainsKey(dt))
        {
            // an edge door of the plan: the inside (unchanged)
            inSign = IsInside(dt + ax) ? 1 : IsInside(dt - ax) ? -1 : 0;
            inStand = inSign != 0 && trT.Walkable(dt + ax * inSign); outStand = inSign != 0 && trT.Walkable(dt - ax * inSign);
            sideWhy = "inside";
        }
        else
        {
            // 33-재검수 N3: my door / no plan — the side the AI was going to (away from the tile it came in from), else the
            // side it can stand on (walkable, no object in view on it)
            HashSet<int2> blockedT;
            try { blockedT = AIObjects.BlockedInView(server, player, out _); } catch { blockedT = new HashSet<int2>(); }
            bool plus = StandOk(trT, dt + ax, blockedT, null), minus = StandOk(trT, dt - ax, blockedT, null);
            inSign = AIBaseGuard.PassSide((d.x, d.y), s.owe.from, ew, plus, minus);
            inStand = inSign > 0 ? plus : inSign < 0 && minus; outStand = inSign > 0 ? minus : inSign < 0 && plus;
            sideWhy = s.owe.from.HasValue ? $"going side (came from ({s.owe.from.Value.x},{s.owe.from.Value.y}))" : "a side to stand on";
        }
        AIBaseGuard.StartTail(s.owe, ew, pos.x, pos.z, inSign, inStand, outStand);
        s.oweUse = 0;
        AIPlayerSpikeMod.Log($"EDGE door owed: '{sk.Name}' ended next to own door ({d.x},{d.y}) left open; closing it from ({pos.x:F2},{pos.z:F2}) before the end is reported (side in the door tile: {sideWhy}, {inSign}; stand {inStand}/{outStand})");
        return true;
    }

    // one frame of the closing tail; done: the fact for the skill's end
    public static BodyInput TailTick(World server, Entity player, float now, out bool done, out string fact)
    {
        var s = S; done = false; fact = "";
        float3 pos = AIBody.Pos(server, player);
        var d = s.owe.door.Value;
        var dv = DoorAt(server, new int2(d.x, d.y), pos);
        if (dv == null) { done = true; fact = $"지난 문 ({d.x},{d.y})이 없어짐"; s.owe.tail = false; s.owe.door = null; return BodyInput.Idle; }
        bool pressDone = s.oweUse == 0 || AIBody.UseDone >= s.oweUse;
        if (s.owe.st != null) AIDoorPass.PressDropped(s.owe.st, AIBody.UseDroppedWhy(s.oweUse), now);   // 합치기 전 수정 C2
        var o = AIBaseGuard.TailStep(s.owe, AIDoorPass.IsOpen(dv.variation), pressDone, pos.x, pos.z, now, out done);
        if (!string.IsNullOrEmpty(o.observation) && s.owe.st != null && s.owe.st.reportedPhase != s.owe.st.phase)
        {
            s.owe.st.reportedPhase = s.owe.st.phase;
            AIBrain.ObserveExecution($"지난 문 ({d.x},{d.y}) 닫기: {o.observation}");
        }
        if (o.press) s.oweUse = AIBody.NextUseId;
        if (done)
        {
            // 실측결함수정3 N5: a close that failed says what stood in the door tile then (the door read open at that
            // moment is the fact; t=1566.1 it was open, the next tail closed it at t=1567.7)
            if (s.owe.st != null && s.owe.st.phase == DoorPhase.Failed && EnemyBodyIn(server, new int2(d.x, d.y), AIDoorPass.BodyBlock) is string who)
                s.owe.fact += $" (그때 문 칸에 {who}의 몸이 있음)";
            fact = s.owe.fact; AIPlayerSpikeMod.Log($"EDGE door owed: {fact}"); return BodyInput.Idle;
        }
        return new BodyInput { move = new float2(o.mx, o.my), face = new float2(o.fx, o.fy), slot = -1, usePress = s.oweUse != 0 && AIBody.UseDone < s.oweUse ? s.oweUse : 0, nudge = o.nudge };
    }

    // an enemy (alive or dying) whose body centre is within r of the tile centre: its id; null when none
    private static string EnemyBodyIn(World server, int2 t, float r)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<ObjectDataCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
                if (math.distance(em.GetComponentData<LocalTransform>(e).Position.xz, (float2)t) < r) return em.GetComponentData<ObjectDataCD>(e).objectID.ToString();
        return null;
    }

    // Own doors near that stand open: close (live-run fix 3: also with no goal, at the next decision — AIBrain offers
    // these then too; an owed door the AI went through says so)
    public static List<AICandidate> DoorCloseCandidates(World server, AIObservation o)
    {
        var res = new List<AICandidate>();
        var s = S;
        if (!s.planSet && s.myDoors.Count == 0) return res;
        float3 me = o.pos;
        bool meInside = s.inside.Contains(StandTile(server, me));
        // 제작대-놓기 §6 (c): my doors (placed off the plan) get the close option too
        var all = new List<int2>(s.doors.Keys); foreach (var t in s.myDoors) if (!all.Contains(t)) all.Add(t);
        foreach (var t in all)
        {
            if (math.distance(me.xz, (float2)t) > 8f) continue;
            var dv = DoorAt(server, t, me);
            if (dv == null || dv.kind == DoorKind.Auto || !AIDoorPass.IsOpen(dv.variation)) continue;
            // 실측결함수정3 N2: the door the running action is passing — its close is that action's last step (the
            // "continue" option carries the door's state); a separate close here was chosen and broke the attack off
            if (AIBody.DoorStepOn(t)) continue;
            string owed = OwedDoor(t) ? (s.owe.tail ? AIBaseGuard.TailNote(s.owe) : string.IsNullOrEmpty(s.owe.fact) ? " AI가 지난 뒤 아직 안 닫힘." : $" 마지막 닫기: {s.owe.fact}.") : "";   // recheck N1: closing it now
            res.Add(new AICandidate
            {
                kind = "doorclose", key = $"doorclose:{t.x},{t.y}", tile = t, progress = false,
                desc = $"문 닫기 ({t.x},{t.y}) — {(s.doors.ContainsKey(t) ? "" : "내가 놓은 문(둘레 밖), ")}문이 열려 있음(열린 문은 적도 지나감).{owed} 문 앞까지 가서 지금 쪽({(!s.planSet ? "지금 선 쪽" : meInside ? "안쪽" : "바깥쪽")})에서 문을 보고 E로 닫기. {dv.dist:F1}칸.",
            });
        }
        return res;
    }

    // Live-run fix 2: the straight step a skill takes while its walk has no direction yet (attack, pick-up: "approach
    // directly") — not into a shut door or a tile that cannot be walked: pressing into the door put the AI "on the door
    // tile" and every walk out failed. The step then waits for the walk's own way (the same way as the walk).
    public static bool DirectStepOk(World server, float3 me, float2 dir)
    {
        if (math.lengthsq(dir) < 1e-6f) return false;
        float2 d = math.normalize(dir);
        int2 ahead = AIBody.TileOf(new float3(me.x + d.x * 0.7f, me.y, me.z + d.y * 0.7f));
        if (math.all(ahead == AIBody.TileOf(me))) return true;
        var doors = AIDoorFacts.InWindowLastByTile(server, ahead, 0);
        if (doors.TryGetValue(ahead, out var dv) && !AIDoorPass.IsOpen(dv.variation)) return false;
        return new AITileReader(server).Walkable(ahead);
    }

    public static void TailAbort() { var s = S; s.owe.tail = false; }
    // recheck N1: the chosen close is the door the tail is closing now (the choice continues it)
    public static bool TailContinues(string key) => AIBaseGuard.TailContinues(S.owe, key);
    // recheck N4: a breach reason waits for this plan's next call
    public static bool BreachPending { get { var w = S.breachWatch; return !string.IsNullOrEmpty(w.pending) && w.plan == S.planVersion; } }
    public static bool OwedDoor(int2 t) { var o = S.owe; return o.door.HasValue && o.door.Value.x == t.x && o.door.Value.y == t.y; }

    // an inside walkable neighbour (not the spawn pad) to place t from
    private static (int x, int y)? InsideStandOf(AITileReader tr, (int x, int y) t)
    {
        foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
        {
            int2 u = new int2(t.x, t.y) + d;
            if (IsInside(u) && tr.Walkable(u) && !AIPlaceGrid.SpawnPad(u.x, u.y)) return (u.x, u.y);
        }
        return null;
    }

    // user decision A: a walkable neighbour off the inside (not the spawn pad) to close t from outside
    internal static (int x, int y)? OutsideStandOf(AITileReader tr, (int x, int y) t)
    {
        foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
        {
            int2 u = new int2(t.x, t.y) + d;
            if (!IsInside(u) && tr.Walkable(u) && !AIPlaceGrid.SpawnPad(u.x, u.y) && !S.doors.ContainsKey(u)) return (u.x, u.y);
        }
        return null;
    }

    // user decision A: the own door the AI comes in by after closing from outside (the plan's door when hung, else any
    // own door on the edge); except = a tile about to be closed (not a way in). Review S8: a remembered door that is not
    // there now is no door to come in by (its open tile is one of the open places, OpenEdgeNow).
    internal static (int x, int y)? EdgeDoorNow(World server, int2? except = null)
    {
        var s = S;
        bool There(int2 d) => server == null || DoorAt(server, d, float3.zero) != null;
        if (s.door.HasValue && s.doors.ContainsKey(s.door.Value) && !(except.HasValue && math.all(except.Value == s.door.Value)) && There(s.door.Value)) return (s.door.Value.x, s.door.Value.y);
        foreach (var d in s.doors.Keys) if (IsEdgeTile(d) && !(except.HasValue && math.all(except.Value == d)) && There(d)) return (d.x, d.y);
        return null;
    }

    // Review F3: the edge tiles open now, for the outside options' facts (AIBaseGuard.OpenAfter): the plan's gap tiles not
    // built (the door spot without its door among them) and the broken parts in view that open a way
    internal static List<(int x, int y)> OpenEdgeNow(World server, Entity player, BreachScan scan)
    {
        var s = S;
        var tr = new AITileReader(server);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var gapOpen = new List<(int x, int y)>();
        foreach (var g in s.gaps) foreach (var t in g) if (!tr.HasWall(t, out _, out _, out _) && !s.doors.ContainsKey(t) && !s.walls.ContainsKey(t) && !blocked.Contains(t)) gapOpen.Add((t.x, t.y));
        return AIBaseGuard.OpenAfter(gapOpen, scan ?? BreachesNow(server, player, out _), null);
    }

    public static List<EdgeBatch> RepairBatches(World server, Entity player, out BreachScan scan)
    {
        scan = BreachesNow(server, player, out _);
        if (scan == null) return new List<EdgeBatch>();
        var tr = new AITileReader(server);
        return AIBaseGuard.RepairBatches(scan, t => InsideStandOf(tr, t));
    }

    // Review G2 for a repair: t is the last open tile of its broken opening (the others walled / a door / an object)
    public static bool IsLastOpenOf(World server, Entity player, List<int2> group, int2 t)
    {
        var tr = new AITileReader(server);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var tiles = group.ConvertAll(u => (u.x, u.y));
        return AIBaseEdgeCore.IsLastOpen(tiles, (t.x, t.y), u => { var v = new int2(u.x, u.y); return !tr.HasWall(v, out _, out _, out _) && !S.doors.ContainsKey(v) && !blocked.Contains(v); });
    }

    // a placed object (or a door) whose position is on tile t
    public static Entity PlacedAt(World server, int2 t, out ObjectID id)
    {
        id = ObjectID.None;
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (AIWorldFacts.IsExcludedPlacedEntity(em, e)) continue;
                if (!math.all(AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position) == t)) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                if (!AIWorldFacts.IsPlacedObjectOrDoor(em, e, od.objectID)) continue;
                id = od.objectID;
                return e;
            }
        return Entity.Null;
    }

    // every placed object (or door) on the given tiles, one query (the options look at many gap tiles)
    private static Dictionary<int2, (Entity e, ObjectID id)> PlacedOn(World server, HashSet<int2> tiles)
    {
        var res = new Dictionary<int2, (Entity, ObjectID)>();
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                if (!tiles.Contains(t) || res.ContainsKey(t)) continue;
                if (AIWorldFacts.IsExcludedPlacedEntity(em, e)) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                if (!AIWorldFacts.IsPlacedObjectOrDoor(em, e, od.objectID)) continue;
                res[t] = (e, od.objectID);
            }
        return res;
    }

    // the edge memory forgets a placed thing that was picked up (PickupSkill)
    public static void ForgetPlaced(int2 t)
    {
        var s = S;
        bool w = s.walls.Remove(t), d = s.doors.Remove(t), l = s.lights.Remove(t);
        if (!(w || d || l)) return;
        AIPlayerSpikeMod.Log($"EDGE memory: ({t.x},{t.y}) picked up, forgotten (walls {s.walls.Count}, doors {s.doors.Count}, lights {s.lights.Count})");
        AIMemory.MarkDirty();
    }

    private static string NearestEnemyNote(AIObservation o, int2 at)
    {
        SeenEnemy best = null; float bd = 1e9f;
        foreach (var e in o.enemies) { float d = math.distance(e.pos.xz, (float2)at); if (d < bd) { bd = d; best = e; } }
        if (best == null) return "보이는 적 없음.";
        int2 et = AIBody.TileOf(best.pos);
        return $"이 곳에서 가장 가까운 보이는 적 {best.id} {bd:F1}칸({(IsInside(et) ? "기지 안쪽" : "기지 바깥")}).";
    }

    // 단계 2 live run: the game takes a door only between two walls (E+W or N+S; a fence counts as a wall here). Review
    // F5: one reading for the door spot (EdgeGrid.wall, AIBaseEdgeCore.DoorSpotOf / DoorSides) and for placing the door:
    // a wall tile or a fence; an object is not a wall until measured.
    internal static bool DoorWall(AITileReader tr, int2 u) => tr.HasWall(u, out _, out _, out _) || tr.Layers(u).Exists(l => l.tileType == TileType.fence);
    private static bool SidesReady(AITileReader tr, int2 dt) => AIBaseEdgeCore.SidesReady(u => DoorWall(tr, new int2(u.x, u.y)), (dt.x, dt.y));

    // what the door spot dt still needs on its sides (review F3/F5): the tiles in the fill batches, those that cannot take a block
    private static string DoorSidesText(World server, Entity player, int2 dt)
    {
        var s = S;
        var gap = new EdgeGap();
        foreach (var g in s.gaps) if (g.Exists(t => math.all(t == dt))) foreach (var t in g) if (!math.all(t == dt)) gap.tiles.Add((t.x, t.y));
        gap.tiles.Add((dt.x, dt.y));
        var need = AIBaseEdgeCore.DoorSides(BuildGrid(server, player, s.seed), gap, (dt.x, dt.y));
        // live-run fix 5: a side tile of the hung door (natural or placed) is the repair's, not a fill batch's
        return need.Need > 0 ? need.Text(dt.x, dt.y, u => RepairCovers(new int2(u.x, u.y), server) || IsHungDoorSide(new int2(u.x, u.y), server)) :$"문 자리 ({dt.x},{dt.y}): 양옆 벽 대기(계획의 틈 칸이 아직 다 막히지 않음)";
    }

    private static string BlockKinds(List<(ObjectID id, int n, bool water)> blocks) => string.Join(", ", blocks.ConvertAll(b => $"{b.id} {b.n}"));

    // ------------------------------------------------------------------ options (plan set and a goal running; G6: no progress)

    // 실측결함수정3 N6: whether the placing finds a stand for t from where the AI is now — the same pick as the skill
    // (AIPlaceSpot.ForTile: side stands the walk reaches without crossing t, own doors passed); note = why not
    private static bool StandReachable(World server, Entity player, AITileReader tr, int2 t, int2 prefer, bool standOnly, out string note, ObjectID item = ObjectID.None)
    {
        note = "";
        if (item == ObjectID.None) item = BlockFor(server, player, AIFarm.IsWater(tr, t));
        if (item == ObjectID.None) return true;   // nothing to place: the materials facts say so (not this check)
        return AIPlaceSpot.ForTile(server, player, item, t, false, null, prefer, out _, out note, standOnly);
    }

    // blocked (optional): what cannot be done now and why, for the input's blocked-target notes (review F3)
    public static List<AICandidate> GuardCandidates(World server, Entity player, AIObservation o, string curKey, List<string> blocked = null)
    {
        var res = new List<AICandidate>();
        var s = S;
        if (!s.planSet) return res;
        int2 meT = StandTile(server, o.pos);   // live-run fix 2
        bool meInside = s.inside.Contains(meT);
        var tr = new AITileReader(server);
        var blocks = WallBlocks(server, player);
        int have = 0; foreach (var b in blocks) have += b.n;
        ObjectID doorItem = ObjectID.None; int haveDoor = 0;
        foreach (var kv in AIBody.InventoryCounts(server, player)) if (kv.Value > 0 && IsDoorItem(server, kv.Key)) { doorItem = kv.Key; haveDoor = kv.Value; break; }
        var batches = RepairBatches(server, player, out var scan);
        if (scan == null) return res;
        int gapLeft = 0; foreach (var b in Batches(server, player)) gapLeft += b.tiles.Count;
        string mat = AIBaseGuard.MaterialFacts(have, BlockKinds(blocks), scan, haveDoor, gapLeft);
        bool progress = AIBaseGuard.CountsAsGoalProgress("edgerepair");
        List<(int x, int y)> openNow = null;   // review F3: the edge's open tiles now (the outside options' facts), once
        // repair batches (no wall block in the bag: nothing can be placed — not offered; the materials option says so)
        bool first = true;
        if (have == 0 && batches.Count > 0)
        {
            AIPlayerSpikeMod.Log($"EDGE repair: {batches.Count} batches not offered (no wall block in the bag)");
            blocked?.Add(AIBaseGuard.RepairBlockedNote(scan, have, batches.Count) + ". " + mat);   // review F3
            batches.Clear();
        }
        foreach (var b in batches)
        {
            var f = b.tiles[0]; var last = b.tiles[b.tiles.Count - 1]; var ls = b.stands[b.stands.Count - 1];
            // 실측결함수정3 N6: the first tile's stand by the same pick as the placing (AIPlaceSpot.ForTile) — a stand the
            // walk cannot reach is no option (it failed in 0.0 s "설 칸이 없음"); what stops it goes to the blocked notes
            if (!StandReachable(server, player, tr, new int2(f.x, f.y), new int2(b.stands[0].x, b.stands[0].y), b.closesGap && b.tiles.Count == 1 && b.group.Count > 0, out string noStand))
            {
                AIPlayerSpikeMod.Log($"EDGE repair: ({f.x},{f.y}) not offered: {noStand}");
                blocked?.Add($"기지 둘레 보수 ({f.x},{f.y}): {noStand} — 지금 설 칸으로 갈 수 없음");
                continue;
            }
            var texts = new List<string>();
            foreach (var t in b.tiles) { var br = scan.list.Find(x => x.tile == t); if (br != null) texts.Add(br.Text); }
            string closing = b.group.Count == 0 && b.doorSides.Count > 0 ? " 문 자리 옆 벽: 둘레를 닫는 칸은 아니지만, 문은 양옆이 벽이어야 놓임(규칙 door-between-walls; 옆 벽이 없을 때 이미 달린 문이 어떻게 되는지는 모름)."
                : b.closesGap ? $" 마지막 칸 ({last.x},{last.y})은 안쪽 ({ls.x},{ls.y})에서 놓아 이 구멍을 닫음." : " 이 묶음 뒤에도 같은 구멍이 남음.";
            res.Add(new AICandidate
            {
                kind = "edgerepair", key = $"edgerepair:{f.x},{f.y}", tiles = b.tiles.ConvertAll(t => new int2(t.x, t.y)), group = b.group.ConvertAll(t => new int2(t.x, t.y)), edgeGap = b.gap, progress = progress,
                desc = $"기지 둘레 보수 — 부서진 곳 {b.tiles.Count}칸({string.Join(", ", texts)}): 블록 {b.tiles.Count}개를 ({f.x},{f.y})부터 차례로 놓기. 가진 블록 {have}개{(have < b.tiles.Count ? "(모자라면 놓은 데까지 하고 멈춤)" : "")}.{closing} 지금 AI는 {(meInside ? "안쪽" : "바깥쪽")}. {NearestEnemyNote(o, new int2(f.x, f.y))}{(first ? " " + mat : "")}",
            });
            // user decision A: the same batch closing its opening from outside (when the last tile has an outside stand)
            var os = b.closesGap && b.group.Count > 0 ? OutsideStandOf(tr, last) : null;
            if (os.HasValue && openNow == null) openNow = OpenEdgeNow(server, player, scan);
            if (os.HasValue)
                res.Add(new AICandidate
                {
                    kind = "edgerepair", key = $"edgerepair:{f.x},{f.y}:out", tiles = b.tiles.ConvertAll(t => new int2(t.x, t.y)), group = b.group.ConvertAll(t => new int2(t.x, t.y)), edgeGap = b.gap, progress = progress,
                    desc = $"기지 둘레 보수(바깥에서 닫기) — 부서진 곳 {b.tiles.Count}칸({string.Join(", ", texts)}): 블록 {b.tiles.Count}개를 ({f.x},{f.y})부터 차례로 놓기. 가진 블록 {have}개. 마지막 칸 ({last.x},{last.y})은 바깥 ({os.Value.x},{os.Value.y})에서 놓아 이 구멍을 닫음 — {AIBaseGuard.OutsideCloseText(EdgeDoorNow(server), AIBaseGuard.OpenAfter(openNow, null, b.group), os.Value)}. 지금 AI는 {(meInside ? "안쪽" : "바깥쪽")}. {NearestEnemyNote(o, new int2(f.x, f.y))}",
                });
            first = false;
        }
        // a gone door: put a door back (only between two walls: the game refuses it otherwise — physically impossible)
        // (review F3: its outside option names the open places that stay)
        foreach (var br in scan.list)
        {
            if (br.kind != BreachKind.DoorGone) continue;
            int2 dt = new int2(br.tile.x, br.tile.y);
            var st = InsideStandOf(tr, br.tile);
            var ost = OutsideStandOf(tr, br.tile);   // user decision A: from outside too

            bool sides = SidesReady(tr, dt);
            if (doorItem == ObjectID.None || !st.HasValue && !ost.HasValue || !sides)
            {
                AIPlayerSpikeMod.Log($"EDGE door back: ({dt.x},{dt.y}) {(doorItem == ObjectID.None ? "no door in the bag" : !st.HasValue ? "no inside stand" : "its side walls are not both there")}");
                blocked?.Add(AIBaseGuard.DoorBackBlockedNote(br.tile, doorItem == ObjectID.None, !st.HasValue && !ost.HasValue, sides ? "" : DoorSidesText(server, player, dt)));   // review F3
                continue;
            }
            // 실측결함수정3 N6: each side's stand by the same pick as the placing (stand only, as the option says)
            string noIn = "", noOut = "";
            if (st.HasValue && !StandReachable(server, player, tr, dt, new int2(st.Value.x, st.Value.y), true, out noIn, doorItem)) st = null;
            if (ost.HasValue && !StandReachable(server, player, tr, dt, new int2(ost.Value.x, ost.Value.y), true, out noOut, doorItem)) ost = null;
            if (!st.HasValue && !ost.HasValue)
            {
                AIPlayerSpikeMod.Log($"EDGE door back: ({dt.x},{dt.y}) no stand reached ({noIn}{(noOut != "" ? "; " + noOut : "")})");
                blocked?.Add($"기지 문 다시 달기 ({dt.x},{dt.y}): {(noIn != "" ? noIn : noOut)} — 지금 설 칸으로 갈 수 없음");
                continue;
            }
            if (st.HasValue) res.Add(new AICandidate
            {
                kind = "place", edgeRole = EdgeCandidateRole.DoorRepair, key = $"place:{doorItem}@{dt.x},{dt.y}", itemId = doorItem, tile = dt, placeAt = true, placeStand = new int2(st.Value.x, st.Value.y), placeStandOnly = true, progress = AIBaseGuard.CountsAsGoalProgress("place"),
                desc = $"기지 문 다시 달기 — {br.Text}(부서졌거나 치워짐). 가방의 {doorItem}를 안쪽 ({st.Value.x},{st.Value.y})에서 놓기(가진 문 {haveDoor}개, 양옆 벽 있음). 지금 이 칸은 열려 있어 적도 지나감. {NearestEnemyNote(o, dt)}",
            });
            // user decision A: from outside too (the door it hangs is the way in)
            if (ost.HasValue && openNow == null) openNow = OpenEdgeNow(server, player, scan);
            if (ost.HasValue)
                res.Add(new AICandidate
                {
                    kind = "place", edgeRole = EdgeCandidateRole.DoorRepair, key = $"place:{doorItem}@{dt.x},{dt.y}:out", itemId = doorItem, tile = dt, placeAt = true, placeStand = new int2(ost.Value.x, ost.Value.y), placeStandOnly = true, progress = AIBaseGuard.CountsAsGoalProgress("place"),
                    desc = $"기지 문 다시 달기(바깥에서) — {br.Text}(부서졌거나 치워짐). 가방의 {doorItem}를 바깥 ({ost.Value.x},{ost.Value.y})에서 놓기(가진 문 {haveDoor}개, 양옆 벽 있음) — {AIBaseGuard.OutsideCloseText((dt.x, dt.y), AIBaseGuard.OpenAfter(openNow, null, new[] { br.tile }), ost.Value)}. 지금 이 칸은 열려 있어 적도 지나감. {NearestEnemyNote(o, dt)}",
                });
        }
        // pick up and place again: an own door off the plan's door tile; a placed object on a gap tile or the door tile
        string holdWhat = "";
        bool canHold = AIBody.CanHold(server, player, true, out holdWhat, out _);
        if (canHold)
        {
            var look = new HashSet<int2>(s.doors.Keys);
            foreach (var g in s.gaps) foreach (var t in g) look.Add(t);
            if (s.door.HasValue) look.Add(s.door.Value);
            var placed = PlacedOn(server, look);
            var targets = new List<(int2 t, int2? to, string why)>();
            foreach (var d in s.doors.Keys)
            {
                if (s.door.HasValue && math.all(d == s.door.Value)) continue;
                if (!placed.ContainsKey(d)) continue;
                int2? to = null;
                if (s.door.HasValue && !placed.ContainsKey(s.door.Value) && !tr.HasWall(s.door.Value, out _, out _, out _) && SidesReady(tr, s.door.Value)) to = s.door.Value;   // a door goes in only between two walls
                targets.Add((d, to, $"계획의 문 자리{(s.door.HasValue ? $" ({s.door.Value.x},{s.door.Value.y})" : "")}가 아닌 곳의 자기 문"));
            }
            foreach (var t in look)
            {
                if (s.doors.ContainsKey(t) || !placed.TryGetValue(t, out var pe) || IsDoorItem(server, pe.id)) continue;
                targets.Add((t, null, s.door.HasValue && math.all(t == s.door.Value) ? $"문 자리에 놓인 {pe.id}(문을 달 수 없음)" : $"둘레 틈 칸에 놓인 {pe.id}(벽 블록을 놓을 수 없고 적도 지나감)"));
            }
            foreach (var (t, to0, why) in targets)
            {
                ObjectID pid = placed[t].id;
                int2? to = to0; int2? stand = null; bool standOnly = false;
                if (to.HasValue) { var st = InsideStandOf(tr, (to.Value.x, to.Value.y)); if (st.HasValue) { stand = new int2(st.Value.x, st.Value.y); standOnly = true; } else to = null; }
                res.Add(new AICandidate
                {
                    kind = "edgepickup", key = $"edgepickup:{t.x},{t.y}", tile = t, dest = to.HasValue ? new float2(to.Value.x, to.Value.y) : new float2(float.NaN, float.NaN), pickupStand = stand ?? default, pickupStandOnly = standOnly, itemId = pid, progress = AIBaseGuard.CountsAsGoalProgress("edgepickup"),
                    desc = $"{(to.HasValue ? "옮기기" : "치우기")} — ({t.x},{t.y})의 {pid}: {why}. {AIBaseGuard.PickupToolNote(holdWhat.Contains("부서짐") ? $"맨손(빈 손 칸을 만들 수 없으면 {holdWhat})" : holdWhat, holdWhat != "맨손" && !holdWhat.Contains("부서짐"))}{(to.HasValue ? $", 그다음 ({to.Value.x},{to.Value.y})에 안쪽 ({stand.Value.x},{stand.Value.y})에서 다시 놓기" : "")}. 치운 동안 그 칸은 열림. 같은 휘두름에 적이 맞으면 물체는 안 맞음. {NearestEnemyNote(o, t)}",
                });
            }
        }
        // repair materials (review F4/S8): the nearest visible walls (not on the edge) that give a wall block, reached by an
        // open walk or through an own door (in a base closed all round every wall reached openly is an edge wall), up to 3
        var m = ReachNow(server, player);
        var wallInfo = new Dictionary<(int x, int y), (ObjectID obj, int hp)>();
        foreach (var w in o.wallTiles)
        {
            if (IsEdgeTile(w.p) || w.ore) continue;
            if (!AIWallData.TryGet(server, w.tileset, out int hp, out _, out ObjectID obj)) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(obj, 0);
            if (info == null || info.tileType != TileType.wall) continue;
            wallInfo[(w.p.x, w.p.y)] = (obj, hp);
        }
        var spots = AIBaseGuard.MaterialSpots(m, wallInfo.Keys);
        for (int i = 0; i < spots.Count; i++)
        {
            var sp = spots[i]; var wi = wallInfo[sp.tile]; int2 p = new int2(sp.tile.x, sp.tile.y);
            res.Add(new AICandidate
            {
                kind = "edgemat", key = $"edgemat:{p.x},{p.y}", tile = p, progress = AIBaseGuard.CountsAsGoalProgress("edgemat"),
                desc = $"보수 재료 모으기 — 벽 {wi.obj} ({p.x},{p.y}) 캐기({AIBaseGuard.MatWayText(sp)}, 벽 체력 {wi.hp}) → {wi.obj} 1개(벽으로 놓는 블록). 기지 둘레 칸이 아님({(IsInside(p) ? "기지 안쪽 벽" : "기지 바깥 벽")}). {NearestEnemyNote(o, p)}{(i == 0 ? " " + mat : "")}",
            });
        }
        // review F3: broken parts but no materials option (the material facts ride on the repair note or option)
        if (spots.Count == 0 && scan.list.Count > 0) blocked?.Add($"보수 재료 모으기: 보이는 벽 중 걸어서(열린 길·자기 문) 닿고, 둘레 칸이 아니고, 벽 블록이 나오는 벽이 없음(광석 벽은 뺌; 가진 벽 블록 {have}개)");
        // the running one is the "continue" option (a materials mine runs as mine:x,y)
        res.RemoveAll(c => c.key == curKey || c.kind == "edgemat" && curKey == $"mine:{c.tile.x},{c.tile.y}");
        return res;
    }

    // ------------------------------------------------------------------ back into the base (the retreat point)

    // The retreat point and its way: with a base, a walkable inside tile (not the spawn pad); without, the walkable tile
    // just off the spawn pad nearest the AI (the game's path search does not reach pad tiles, 단계 1 M4e).
    public static int2 RetreatPoint(World server, Entity player, out string label, out EnemyReach how)
    {
        var tr = new AITileReader(server);
        var m = ReachNow(server, player);
        int2 me = StandTile(server, AIBody.Pos(server, player));   // live-run fix 2
        how = new EnemyReach { kind = ReachKind.None, steps = -1 };
        var shape = ShapeNow(server);
        if (shape != null)
        {
            var t = AIBaseGuard.InsideTarget(shape, u => tr.Walkable(new int2(u.x, u.y)) && !AIPlaceGrid.SpawnPad(u.x, u.y), (S.seed.x, S.seed.y), m, IsInside(me), out how);
            if (t.HasValue) { label = IsInside(me) ? "기지 안쪽(코어 쪽)" : "기지 안쪽"; return new int2(t.Value.x, t.Value.y); }
        }
        label = "코어 스폰 받침 옆";
        // (0,-2), just south of the old point, when it is walkable (a stable key between calls); else the ring tile nearest the AI
        int2 best = new int2(0, -2); float bd = tr.Walkable(best) ? -1f : float.MaxValue;
        for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++)
            {
                if (math.max(math.abs(x), math.abs(y)) != 2) continue;
                int2 t = new int2(x, y);
                if (!tr.Walkable(t)) continue;
                float d = math.distance((float2)t, (float2)me);
                if (d < bd) { bd = d; best = t; }
            }
        if (m.g.In(best.x, best.y)) how = AIBaseGuard.ReachOf(m, (best.x, best.y));
        return best;
    }

    // ------------------------------------------------------------------ test commands (/ai edge …, behind testcmds.on)

    public static bool GuardCommand(World server, Entity player, string sub, string[] parts, float now)
    {
        int Arg(int i, int def) => parts.Length > i && int.TryParse(parts[i], out int v) ? v : def;
        switch (sub)
        {
            case "breaks":
            {
                var bs = RepairBatches(server, player, out var scan);
                if (scan == null) { AIPlayerSpikeMod.Reply("[시험용] 둘레 계획이 없음"); return true; }
                AIPlayerSpikeMod.Reply($"[시험용] 부서진 곳 {scan.list.Count}: {string.Join(", ", scan.list.ConvertAll(b => b.Text))}; 둘레 {scan.edgeTiles}칸, 확인 못 함 {scan.unseen}칸, 아직 안 막은 틈 칸 {scan.notBuilt}; 보수 묶음 {bs.Count}: {string.Join(" / ", bs.ConvertAll(b => $"{b.tiles.Count}칸({b.tiles[0].x},{b.tiles[0].y}~){(b.closesGap ? " 닫음" : "")}"))}");
                AIPlayerSpikeMod.Log("EDGE repair batches: " + string.Join(" | ", bs.ConvertAll(b => $"opening {b.gap} tiles [{string.Join(" ", b.tiles.ConvertAll(t => $"({t.x},{t.y})"))}] stands [{string.Join(" ", b.stands.ConvertAll(t => $"({t.x},{t.y})"))}] closes={b.closesGap}")));
                return true;
            }
            case "repair":
            {
                var bs = RepairBatches(server, player, out _);
                int k = Arg(3, 1);
                if (k < 1 || k > bs.Count) { AIPlayerSpikeMod.Reply($"[시험용] 보수 묶음 {k}번이 없음(남은 {bs.Count})"); return true; }
                var b = bs[k - 1];
                AIBody.StartSkill(server, player, new EdgeFillSkill(b.gap, b.tiles.ConvertAll(t => new int2(t.x, t.y)), b.group.ConvertAll(t => new int2(t.x, t.y)), parts.Length > 4 && parts[4] == "out"), now);
                return true;
            }
            case "pickup":
            {
                int2 t = new int2(Arg(3, 0), Arg(4, 0));
                int2? to = parts.Length >= 7 ? new int2(Arg(5, 0), Arg(6, 0)) : (int2?)null;
                int2? stand = null;
                if (to.HasValue) { var st = InsideStandOf(new AITileReader(server), (to.Value.x, to.Value.y)); if (st.HasValue) stand = new int2(st.Value.x, st.Value.y); }
                AIBody.StartSkill(server, player, new PickupSkill(t, to, stand, stand.HasValue && IsInside(stand.Value)), now);
                return true;
            }
            case "reach":
            {
                var o = AIObservation.Take(server, player, false);
                if (o.enemies.Count == 0) { AIPlayerSpikeMod.Reply("[시험용] 보이는 적 없음"); return true; }
                foreach (var e in o.enemies)
                {
                    string f = KillFacts(server, player, e, out var r);
                    bool th = ObserveThreat(server, player, e.entity, e.id, e.pos, e.dist, AIBrain.Settings().combatRange);
                    string line = $"{e.id} ({e.pos.x:F0},{e.pos.z:F0}) {e.dist:F1}칸 위협={(th ? "예" : "아니오")}: {f}";
                    AIPlayerSpikeMod.Reply("[시험용] " + line); AIPlayerSpikeMod.Log("EDGE reach: " + line);
                }
                return true;
            }
            case "guard":
            {
                var o = AIObservation.Take(server, player, true);
                var bl = new List<string>();
                var cs = GuardCandidates(server, player, o, "", bl);
                int2 rp = RetreatPoint(server, player, out string label, out var how);
                AIPlayerSpikeMod.Reply($"[시험용] 지키기 후보 {cs.Count}개; 물러날 곳 {label} ({rp.x},{rp.y}): {AIBaseGuard.ReachText(how)}");
                foreach (var c in cs) { AIPlayerSpikeMod.Reply("[시험용] " + c.desc); AIPlayerSpikeMod.Log($"EDGE guard option {c.kind} {c.key}: {c.desc}"); }
                foreach (var b in bl) { AIPlayerSpikeMod.Reply("[시험용] 막힘: " + b); AIPlayerSpikeMod.Log($"EDGE guard blocked: {b}"); }
                return true;
            }
        }
        return false;
    }
}
