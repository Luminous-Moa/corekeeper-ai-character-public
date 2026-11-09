// AIEdge.Reach — extracted from the existing implementation; behavior and state ownership are unchanged.
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
    public const int ReachRange = 24;   // the walk grid around the AI: (2*24+1)^2 tiles (enemies in view are well inside)

    // ------------------------------------------------------------------ reach

    // 실측결함수정3 N4/N6: the own door tiles as the AI knows them now — in view, where an own door stands; out of view, the
    // remembered doors except one whose tile was seen open at the last look (it fell). A remembered door that fell in
    // view is ground (a stand, a way) — it was still read as a door (the place grid's "no stand" / door step).
    public static HashSet<int2> OwnDoorTilesNow(World server, float3 me, int2 c, int r)
    {
        var s = S; var res = new HashSet<int2>();
        if (s.doors.Count == 0) return res;
        var doors = AIDoorFacts.InWindowLastByTile(server, c, r);
        foreach (var t in s.doors.Keys)
        {
            if (math.abs(t.x - c.x) > r || math.abs(t.y - c.y) > r) continue;
            if (AIBaseGuard.OwnDoorStands(AIVision.CanSee(me, (float2)t), doors.ContainsKey(t), s.edgeSeen.Last(t.x, t.y))) res.Add(t);
        }
        return res;
    }

    // The walk grid around the AI: known = in view or seen before (the AI's memory); own closed doors (edge doors and my
    // doors) are doors (the AI opens them, enemies do not); fences and water are low (fliers cross them); objects in view block.
    private static GuardGrid GuardGridAround(World server, Entity player, int2 c)
    {
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var doors = AIDoorFacts.InWindowLastByTile(server, c, ReachRange);
        // 검수 F3: large objects seen before are the reach map's memory layer (AIBaseGuard.Reach), not read into the tiles here
        int n = 2 * ReachRange + 1;
        var g = GuardGrid.Make(c.x - ReachRange, c.y - ReachRange, n, GuardCell.Unknown);
        g.view = new bool[n * n];   // 재실측 N-b: out of view, the remembered base edge is what memory says (AIBaseGuard.Reach)
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int2 t = new int2(g.loX + x, g.loY + y);
                int i = x * n + y;
                bool inView = AIVision.CanSee(me, (float2)t);
                g.view[i] = inView;
                if (!(inView || AIMemory.SeenTile(t))) continue;   // unknown
                // 재검수 R4: someone else's closed door is a door; 실측결함수정3 N4: out of view an own door is the last look
                // (one that fell unseen is still the door; a door seen gone is open: the memory layer, AIBaseGuard.Reach).
                // 내문-판정 결함 1·2: own = edge doors and my doors alike (IsWalkDoorTile) — a closed my door is the AI's
                // door, not someone else's, and one that fell out of view is still the door
                bool doorThere = doors.TryGetValue(t, out var dv);
                var dc = AIBaseGuard.DoorCell(inView, doorThere, doorThere && AIDoorPass.IsOpen(dv.variation), IsWalkDoorTile(t), S.edgeSeen.Last(t.x, t.y));   // 32-검수 L1: with the last look
                if (dc.HasValue) { g.cell[i] = dc.Value; continue; }
                var top = tr.Top(t);
                bool low = top.tileType == TileType.fence || top.tileType == TileType.water && !AIPlaceSpot.IsLava(tr, t);
                bool ob = blocked.Contains(t);
                g.cell[i] = AIBaseGuard.CellOf(!low && tr.Walkable(t), ob, ob && AIObjects.StuckOnly(player, t), low);   // 검수 F9: a stuck mark is no wall
            }
        return g;
    }

    // the reach map from the AI's tile (kept 0.4 s while the AI stays on the same tile); live-run fix 1: with a plan the
    // remembered base (its unseen tiles, no window-border way out of it); fix 2: from the tile the AI stands on
    public static ReachMap ReachNow(World server, Entity player)
    {
        var s = S;
        float now = Time.realtimeSinceStartup;
        int2 me = StandTile(server, AIBody.Pos(server, player));
        if (s.reach != null && math.all(s.reachAt == me) && now - s.reachTime < 0.4f && now >= s.reachTime) return s.reach;
        var g = GuardGridAround(server, player, me);
        // 재검수 R5: the last looks of the edge tiles in view are noted in one place, the breach scan; the reach map reads them
        if (s.planSet) { try { BreachesNow(server, player, out _); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: breach scan for the reach map failed", e); } }
        s.reach = AIBaseGuard.Reach(g, (me.x, me.y), MemoryNow(), AIObjects.BigSeen(player)); s.reachAt = me; s.reachTime = now;
        return s.reach;
    }

    // the remembered base for the reach map (null without a plan)
    private static BaseMemory MemoryNow()
    {
        var s = S;
        var shape = ShapeCached();
        if (shape == null) return null;
        var w = new List<(int x, int y)>(); foreach (var t in s.walls.Keys) w.Add((t.x, t.y));
        var d = new List<(int x, int y)>(); foreach (var t in s.doors.Keys) d.Add((t.x, t.y));
        return new BaseMemory(shape, w, d, s.edgeSeen);   // 검수 F2: with the last look of each edge tile
    }

    // shut doors (any owner) within r tiles (Chebyshev) of c: no tile to stand on (재실측 결함 2, N-c)
    public static HashSet<int2> ShutDoorsNear(World server, int2 c, int r)
    {
        var res = new HashSet<int2>();
        foreach (var kv in AIDoorFacts.InWindowLastByTile(server, c, r)) if (!AIDoorPass.IsOpen(kv.Value.variation)) res.Add(kv.Key);
        return res;
    }

    // Live-run fix 2 (사실 2): the tile the AI stands on — a shut door is no tile to stand on (pressed against one, the
    // rounded position is the door tile), so the nearest tile it can stand on (AIBaseGuard.StandTile). 재실측 결함 2:
    // the one "where the AI stands" of every skill and walk plan (AINavigator plans from it through
    // AIBaseGuard.PlanFrom); an enemy pressed against a shut door is read the same way (its side of the door)
    // 검수 S2: the one rule for a tile to stand on next to a target (mine, harvest, root): walkable, no shut door (anyone's;
    // 재실측 N-c) and no object on it (BlockedInView; a pot next to a field, an object against a wall). Each skill keeps
    // which neighbours it looks at and how it orders them.
    public static bool StandOk(AITileReader tr, int2 n, HashSet<int2> blocked, HashSet<int2> shut)
        => tr.Walkable(n) && (shut == null || !shut.Contains(n)) && (blocked == null || !blocked.Contains(n));

    // 검수 F8: the one reading of an enemy's tile — the facts (KillFacts, Threat: the touch rule), the attack option's
    // tile and the attack's walk target (an enemy pressed on the far side of a shut door is on its side, not on the door
    // tile: a walk to the door tile "arrived" from inside and stood there for 45 s)
    public static int2 EnemyTile(World server, float3 pos) => StandTile(server, pos);

    public static int2 StandTile(World server, float3 pos)
    {
        int2 t = AIBody.TileOf(pos);
        var doors = AIDoorFacts.InWindowLastByTile(server, t, 1);
        bool Shut(int2 u) => doors.TryGetValue(u, out var dv) && !AIDoorPass.IsOpen(dv.variation);
        if (!Shut(t)) return t;
        var tr = new AITileReader(server);
        var r = AIBaseGuard.StandTile(pos.x, pos.z, u => { var v = new int2(u.x, u.y); return !Shut(v) && tr.Walkable(v); });
        return new int2(r.x, r.y);
    }

    // review F1: how the enemy gets at the AI besides walking — flier by name (estimate), ranged / jump attack by its
    // components (the enemy data the knowledge dump reads, AIKnowledge)
    public static EnemyMove MoveOf(World server, Entity e, ObjectID id)
    {
        var m = AIBaseGuard.IsFlier(id.ToString()) ? EnemyMove.Fly : EnemyMove.Walk;
        var em = server.EntityManager;
        if (e != Entity.Null && em.Exists(e))
        {
            if (em.HasComponent<RangeAttackStateCD>(e)) m |= EnemyMove.Ranged;
            if (em.HasComponent<JumpAttackStateCD>(e)) m |= EnemyMove.Jump;
        }
        return m;
    }

    public static EnemyReach ReachOf(World server, Entity player, int2 t, ObjectID id, Entity e = default)
    {
        var m = ReachNow(server, player);
        if (!m.g.In(t.x, t.y)) return new EnemyReach { kind = ReachKind.Unseen, steps = -1, comes = true, walks = true, flyReach = true };   // outside the grid: unknown
        var r = AIBaseGuard.ReachOf(m, (t.x, t.y), MoveOf(server, e, id));
        r.tileBreak = TileBreakOf(server, e, id);   // 실측결함수정3 N1: the game data first
        r.breaker = AIBaseGuard.IsBreaker(id.ToString(), r.tileBreak);   // 재실측 N-g: hits across a wall / door within HitReach
        return r;
    }

    // 실측결함수정3 N1: an enemy kind's tile damage from the game data, read once per kind (the entity seen, else the kind's
    // prefab): the same components and property keys the game's systems read (AIBaseGuard.TileBreak). Logged once per
    // kind so a live run can check it against what it sees.
    private static readonly Dictionary<ObjectID, TileBreak> _tileBreak = new Dictionary<ObjectID, TileBreak>();
    public static TileBreak TileBreakOf(World server, Entity e, ObjectID id)
    {
        if (_tileBreak.TryGetValue(id, out var tb)) return tb;
        tb = default;
        try
        {
            var em = server.EntityManager;
            Entity src = e != Entity.Null && em.Exists(e) ? e : Entity.Null;
            if (src == Entity.Null)
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
                    if (q.CalculateEntityCount() == 1) src = PugDatabase.GetPrimaryPrefabEntity(id, q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob, 0);
            if (src != Entity.Null && em.HasComponent<ObjectPropertiesCD>(src))
            {
                var pr = em.GetComponentData<ObjectPropertiesCD>(src);
                tb.known = true;
                // a channel the kind does not have is 0 (the game does not run it); a key not read stays null (모름)
                tb.chase = em.HasComponent<DamageObjectStateCD>(src);
                if (!tb.chase) { tb.bump = 0; tb.obj = 0; }
                else
                {
                    if (pr.TryGet<int>(-302465456, out int bump)) tb.bump = bump;
                    if (pr.TryGet<int>(-636725815, out int obj)) tb.obj = obj;   // 합치기 전 수정 C1: what a door (an object) is hit with
                }
                if (!pr.Has(-2072102458)) tb.melee = 0; else if (pr.TryGet<int>(-1854640576, out int melee)) tb.melee = melee;
                if (!(em.HasComponent<ChargeAttackStateCD>(src) && pr.Has(2130508658))) tb.charge = 0; else if (pr.TryGet<int>(1689738466, out int charge)) tb.charge = charge;
            }
        }
        catch (Exception ex) { AIPlayerSpikeMod.Log($"EDGE tile damage {id}: read failed ({ex.GetType().Name})"); tb = default; }
        _tileBreak[id] = tb;
        AIPlayerSpikeMod.Log($"EDGE tile damage {id}: {tb.Log} (game data; breaker={AIBaseGuard.IsBreaker(id.ToString(), tb)})");
        return tb;
    }

    // An enemy that matters for the fight: within range and able to get at the AI (walk, or fly / shoot / jump over
    // fences and water), within hit reach, or any enemy in range while the AI was hurt within 1.5 s (review F1).
    // Remembered for the close-fight set of the same build (CannotCome).
    public static bool? AssessThreatWithinRange(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range)
    {
        if (dist > range) return null;
        var r = ReachOf(server, player, EnemyTile(server, pos), id, e);   // 재실측: an enemy pressed against a shut door is on its side
        bool hurt = AIVitals.HurtRecently(AIBaseGuard.HurtSeconds, Time.realtimeSinceStartup);   // recheck R1: not a lower max (hunger)
        bool t = AIBaseGuard.IsThreat(r, dist, range, hurt);
        return t;
    }

    public static bool ObserveThreat(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range)
    {
        var assessment = AssessThreatWithinRange(server, player, e, id, pos, dist, range);
        if (!assessment.HasValue) return false;
        S.threatHistory.Record(e, assessment.Value);
        return assessment.Value;
    }

    public static bool ObserveAnyThreat(World server, Entity player, List<SeenEnemy> enemies, float range)
    {
        bool any = false;
        foreach (var e in enemies) if (ObserveThreat(server, player, e.entity, e.id, e.pos, e.dist, range)) any = true;
        return any;
    }

    public static bool LastAssessedCannotCome(Entity e) => S.threatHistory.LastAssessedCannotCome(e);

    // Source-compatible entry points; internal callers name whether they observe or read history.
    public static bool Threat(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range) => ObserveThreat(server, player, e, id, pos, dist, range);
    public static bool AnyThreat(World server, Entity player, List<SeenEnemy> enemies, float range) => ObserveAnyThreat(server, player, enemies, range);
    public static bool CannotCome(Entity e) => LastAssessedCannotCome(e);
}
