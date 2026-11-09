// AIObjects — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// --------------------------------------------------------------------------------------------- objects as obstacles

// Tiles covered by placed/natural objects that have a physics collider (what a player bumps into: the core, wires,
// plants, workbenches), from the object's tile footprint in the game data (prefabTileSize / prefabCornerOffset).
// Only objects in the AI's view (AIVision) are used. Excludes players, creatures, dropped items and tile entities.
public static class AIObjects
{
    // "Under the AI" (2026-11-04 막힘-멈춤-정리, U.tunnel-objects): an object counted as passable because the AI stands
    // on it must have the AI's centre well inside its footprint. The old test used the AI's rounded tile: an AI pressed
    // against a small object (its centre 0.05 inside the object's tile, e.g. (-62.00,67.55) against an object on
    // (-62,68)) "stood on" it, so the whole object was dropped from the blocked tiles, the tile A* planned straight
    // through it and the walk stuck three times on the same spot, over and over (memory test t=957-1226: (-63,55.55),
    // (-62,67.55), (-61,52.45), every SEND there said "1 under the AI"; the game's own path search said "no path"
    // because the AI's start was inside the object's tile).
    private const float UnderMargin = 0.35f;   // ~ the player body half width: closer to an edge = pressed against it
    // Tiles a walk got stuck on although the tile map and the object list said open (something not in either): kept
    // per character for StuckSeconds and counted as blocked like an object (AINavigator marks them).
    public const float StuckSeconds = 120f;
    private static readonly Dictionary<Entity, List<(int2 t, float until)>> _stuck = new Dictionary<Entity, List<(int2, float)>>();
    // one result per character per frame: BuildCandidates asked this once and every tunnel plan again (8-10 times a
    // build, each a query over every object with a collider)
    private static readonly Dictionary<Entity, (int frame, int2 me, HashSet<int2> set, string note)> _frame = new Dictionary<Entity, (int, int2, HashSet<int2>, string)>();

    public static void NoteStuck(Entity player, int2 t, float now, string why)
    {
        if (!_stuck.TryGetValue(player, out var l)) _stuck[player] = l = new List<(int2, float)>();
        l.RemoveAll(x => now > x.until || math.all(x.t == t));
        l.Add((t, now + StuckSeconds));
        _frame.Remove(player);
        try { AIBrain.ForgetPlans(); } catch { }
        AIPlayerSpikeMod.Log($"nav: tile ({t.x},{t.y}) counted as blocked for {StuckSeconds:F0}s ({why}; open on the tile map and in the object list)");
    }

    // A player or creature body within ~1 tile of t (a walk stopped by a body is not a blocked tile).
    public static bool BodyNear(World server, Entity player, int2 t)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player || !(em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e))) continue;
                if (math.distance(em.GetComponentData<LocalTransform>(e).Position.xz, (float2)t) <= 1.2f) return true;
            }
        }
        return false;
    }

    public static bool StuckAt(Entity player, int2 t, float now) => _stuck.TryGetValue(player, out var l) && l.Exists(x => now <= x.until && math.all(x.t == t));

    // combat stage 2 review F2: what blocks a tile of the last BlockedInView of this character (the object id, for the
    // charge prediction sentence); null when it is not an object tile of that result.
    private static readonly Dictionary<Entity, Dictionary<int2, ObjectID>> _what = new Dictionary<Entity, Dictionary<int2, ObjectID>>();
    public static bool ObjectAt(Entity player, int2 t, out ObjectID id) { id = ObjectID.None; return _what.TryGetValue(player, out var m) && m.TryGetValue(t, out id); }
    // Tiles held only by critters (CritterCD: fireflies and the like, they move and fly; R3 live run: a YellowFirefly tile in
    // front of the brute made the charge prediction stop after 0.6 tiles). The charge prediction leaves them out; walking
    // still counts them as before.
    private static readonly Dictionary<Entity, HashSet<int2>> _critterOnly = new Dictionary<Entity, HashSet<int2>>();
    public static bool CritterOnly(Entity player, int2 t) => _critterOnly.TryGetValue(player, out var s) && s.Contains(t);
    // 재실측: tiles of the last result that are blocked only because a walk got stuck there (no object): the breach scan
    // does not read them as closed (a fallen own door was hidden behind one for its 120 s)
    private static readonly Dictionary<Entity, HashSet<int2>> _stuckOnly = new Dictionary<Entity, HashSet<int2>>();
    public static bool StuckOnly(Entity player, int2 t) => _stuckOnly.TryGetValue(player, out var s) && s.Contains(t);
    // 재실측 N-b: large objects seen before, per character (AIBaseGuard.BigObjectMemory): their tiles stay closed for the
    // reach map while out of view
    private static readonly Dictionary<Entity, BigObjectMemory> _bigSeen = new Dictionary<Entity, BigObjectMemory>();
    public static BigObjectMemory BigSeen(Entity player) { if (!_bigSeen.TryGetValue(player, out var m)) _bigSeen[player] = m = new BigObjectMemory(); return m; }
    private static long EntKey(Entity e) => ((long)e.Index << 32) | (uint)e.Version;
    // 검수 F5: another world (AIMemory.Ensure): the large objects, stuck marks and per-frame results of the last one go
    // (the keys are entities, which a new world numbers again)
    public static void ClearMemory() { _bigSeen.Clear(); _stuck.Clear(); _stuckOnly.Clear(); _frame.Clear(); _what.Clear(); _critterOnly.Clear(); }

    // 코어-안전지대 단계 2 live run: tiles a placed object occupies although it has no blocking collider (a torch): the
    // game refuses placing there and its path search does not walk onto them. For placing only (walking keeps
    // BlockedInView); with the blocked tiles.
    public static HashSet<int2> OccupiedInView(World server, Entity player)
    {
        var res = new HashSet<int2>(BlockedInView(server, player, out _));
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<PickUpItemCD>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<CritterCD>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                ObjectInfo info = PugDatabase.GetObjectInfo(od.objectID, 0);
                if (info == null || info.objectType != ObjectType.PlaceablePrefab) continue;
                int2 t = AIBody.TileOf(p);
                int2 size = new int2(math.max(1, info.prefabTileSize.x), math.max(1, info.prefabTileSize.y));
                for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) res.Add(t + new int2(info.prefabCornerOffset.x + x, info.prefabCornerOffset.y + y));
            }
        return res;
    }

    public static HashSet<int2> BlockedInView(World server, Entity player, out string note)
    {
        int frame = Time.frameCount;
        int2 meTile = AIBody.TileOf(AIBody.Pos(server, player));
        if (_frame.TryGetValue(player, out var fc) && fc.frame == frame && math.all(fc.me == meTile)) { note = fc.note; return fc.set; }
        var res = Compute(server, player, out note);
        _frame[player] = (frame, meTile, res, note);
        return res;
    }

    private static HashSet<int2> Compute(World server, Entity player, out string note)
    {
        var res = new HashSet<int2>();
        if (!_what.TryGetValue(player, out var what)) _what[player] = what = new Dictionary<int2, ObjectID>();
        what.Clear();
        if (!_critterOnly.TryGetValue(player, out var critters)) _critterOnly[player] = critters = new HashSet<int2>();
        critters.Clear(); var solid = new HashSet<int2>();
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        int n = 0, triggers = 0, underMe = 0, pressed = 0;
        var big = new List<string>(); var pressedLog = new List<string>();
        var bigMem = BigSeen(player); var seenIds = new HashSet<long>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<Unity.Physics.PhysicsCollider>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<PickUpItemCD>(e) || em.HasComponent<TileCD>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                seenIds.Add(EntKey(e));
                var od = em.GetComponentData<ObjectDataCD>(e);
                ObjectInfo info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                int2 size = info != null ? new int2(math.max(1, info.prefabTileSize.x), math.max(1, info.prefabTileSize.y)) : new int2(1, 1);
                int2 corner = info != null ? new int2(info.prefabCornerOffset.x, info.prefabCornerOffset.y) : int2.zero;
                int2 baseT = AIBody.TileOf(p) + corner;
                if (info != null && info.centerIsAtEntityPosition) baseT = AIBody.TileOf(p) - size / 2;
                // trigger-only colliders (pads, pickups) do not stop a player
                try
                {
                    var pc = em.GetComponentData<Unity.Physics.PhysicsCollider>(e);
                    if (pc.IsValid && pc.Value.Value.GetCollisionResponse() != Unity.Physics.CollisionResponsePolicy.Collide && pc.Value.Value.GetCollisionResponse() != Unity.Physics.CollisionResponsePolicy.CollideRaiseCollisionEvents) { triggers++; continue; }
                }
                catch { }
                // the AI stands on it (centre well inside the footprint), so it is passable for the AI (e.g. the core
                // spawn pad); only pressed against it (rounded tile inside, centre near an edge) it still blocks
                int2 meT = AIBody.TileOf(me);
                bool inTile = math.all(meT >= baseT) && math.all(meT < baseT + size);
                if (inTile)
                {
                    float2 lo = (float2)baseT - 0.5f, hi = (float2)(baseT + size) - 0.5f;
                    float margin = math.cmin(math.min(me.xz - lo, hi - me.xz));
                    if (margin >= UnderMargin) { underMe++; bigMem.Drop(EntKey(e)); continue; }   // 검수 F4: passable for the AI, not remembered closed
                    pressed++;
                    if (pressedLog.Count < 2) pressedLog.Add($"{od.objectID} at ({baseT.x},{baseT.y}) {size.x}x{size.y}, AI centre {margin:F2} inside its edge");
                }
                bool critter = em.HasComponent<CritterCD>(e);
                for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) { var tt = baseT + new int2(x, y); res.Add(tt); if (critter) { if (!solid.Contains(tt)) { critters.Add(tt); if (!what.ContainsKey(tt)) what[tt] = od.objectID; } } else { solid.Add(tt); critters.Remove(tt); what[tt] = od.objectID; } }
                n++;
                if (size.x * size.y > 1 && big.Count < 4) big.Add($"{od.objectID} {size.x}x{size.y} at ({baseT.x},{baseT.y})");
                if (size.x * size.y > 1 && !critter)
                {
                    var ft = new List<(int x, int y)>();
                    for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) ft.Add((baseT.x + x, baseT.y + y));
                    bigMem.Seen(EntKey(e), od.objectID.ToString(), p.x, p.z, ft);
                }
            }
        }
        bigMem.Forget(seenIds, (x, y) => AIVision.CanSee(me, new float2(x, y)));
        int stuck = 0;
        if (!_stuckOnly.TryGetValue(player, out var stuckOnly)) _stuckOnly[player] = stuckOnly = new HashSet<int2>();
        stuckOnly.Clear();
        if (_stuck.TryGetValue(player, out var sl))
        {
            float now = Time.realtimeSinceStartup;
            sl.RemoveAll(x => now > x.until);
            foreach (var s in sl) { critters.Remove(s.t); if (res.Add(s.t)) { stuck++; stuckOnly.Add(s.t); } }
        }
        note = $"objects {n} (+{triggers} trigger-only, {underMe} under the AI{(pressed > 0 ? $", {pressed} pressed against: {string.Join("; ", pressedLog)}" : "")}), blocked tiles {res.Count}{(stuck > 0 ? $" (+{stuck} where a walk got stuck)" : "")}{(big.Count > 0 ? ", large: " + string.Join("; ", big) : "")}";
        return res;
    }
}
