// AIBaseProbe — TEST ONLY measurements for 코어-안전지대 단계 1 (behind AIPlayers/testcmds.on, like the combat stage 0
// probe). Changes no behaviour. Player.log lines start with "BASE-PROBE".
//   /ai baseprobe immune [r]     the core's protected zone: tiles with TileType.immune within r of the core (0,0) (default
//                                16): count, bounds, farthest, a map; and every ImmunityZoneCD entity (where, radius, rect)
//   /ai baseprobe tile x z      the tile's layers and the entities within 2 tiles with their components (단계 2: what
//                                makes the spawn pad refuse placing; a door's interaction parts)
//   /ai baseprobe self           what the spawn rules see of the AI: its ObjectDataCD id (the respawn system counts
//                                ObjectID.Player within 6 tiles via physics hits, SpawnEnvironmentObjectsSystem), PlayerGhost
//                                (the 200-tile periodic rule queries PlayerGhost), a physics collider; the host for comparison
//   /ai baseprobe door on|off    doors and fence gates within 10 tiles of the AI, 4x a second: variation (the game's path
//                                search treats 1 and 3 as open, PathFindSystem.GetBlockedPositionsFromColliderHits), collider
//                                response, whether the AI's own blocked tiles (AIObjects.BlockedInView) hold its tile, and the
//                                AI's distance; logged on every change
//   /ai baseprobe hits on|off [r] walls, fences, thin walls (tile entities) and doors, gates, torches (objects) within r
//                                (default 12) of the AI: health drops and disappearances with the nearest enemy at that moment
// Watchers stop by themselves after 10 minutes.
using System;
using System.Collections.Generic;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIBaseProbe
{
    private const float WatchMax = 600f;

    private sealed class Watch
    {
        public bool door, hits;
        public float doorUntil, hitsUntil, next;
        public int r = 12;
        public readonly Dictionary<Entity, string> doorState = new Dictionary<Entity, string>();
        public readonly Dictionary<Entity, (string what, int2 t, int hp)> hp = new Dictionary<Entity, (string, int2, int)>();
    }
    private static readonly Dictionary<Entity, Watch> _w = new Dictionary<Entity, Watch>();

    private static void Log(string s) => AIPlayerSpikeMod.Log("BASE-PROBE " + s);

    public static void Command(World server, Entity player, string[] parts)
    {
        string sub = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        float now = Time.realtimeSinceStartup;
        if (!_w.TryGetValue(player, out var w)) _w[player] = w = new Watch();
        switch (sub)
        {
            case "immune":
                Immune(server, parts.Length >= 4 && int.TryParse(parts[3], out int r) ? math.clamp(r, 2, 40) : 16);
                break;
            case "tile":
                // 단계 2: what is on a tile (the spawn pad refusal, a door's interaction parts)
                if (parts.Length >= 5 && int.TryParse(parts[3], out int tx) && int.TryParse(parts[4], out int tz)) Tile(server, new int2(tx, tz));
                else AIPlayerSpikeMod.Reply("사용법: /ai baseprobe tile x z");
                break;
            case "self":
                Self(server, player);
                break;
            case "door":
            {
                bool on = parts.Length < 4 || parts[3] != "off";
                w.door = on; w.doorUntil = now + WatchMax; w.doorState.Clear();
                Log($"door watch {(on ? "ON (10 min)" : "OFF")}");
                AIPlayerSpikeMod.Reply($"[시험용] 문 관찰 {(on ? "켬(10분)" : "끔")}");
                break;
            }
            case "hits":
            {
                bool on = parts.Length < 4 || parts[3] != "off";
                w.hits = on; w.hitsUntil = now + WatchMax; w.hp.Clear();
                if (parts.Length >= 5 && int.TryParse(parts[4], out int hr)) w.r = math.clamp(hr, 3, 30);
                Log($"hits watch {(on ? $"ON (10 min, r={w.r})" : "OFF")}");
                AIPlayerSpikeMod.Reply($"[시험용] 벽·울타리·문 피해 관찰 {(on ? $"켬(10분, {w.r}칸)" : "끔")}");
                break;
            }
            default:
                AIPlayerSpikeMod.Reply("사용법: /ai baseprobe immune [r] | self | door on|off | hits on|off [r]");
                break;
        }
    }

    private static void Immune(World server, int r)
    {
        var tr = new AITileReader(server);
        int count = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        float far = 0f;
        var rows = new List<string>();
        for (int y = r; y >= -r; y--)
        {
            var sb = new StringBuilder();
            for (int x = -r; x <= r; x++)
            {
                bool im = AIPlaceSpot.IsImmune(tr, new int2(x, y));
                if (im)
                {
                    count++; minX = math.min(minX, x); minY = math.min(minY, y); maxX = math.max(maxX, x); maxY = math.max(maxY, y);
                    far = math.max(far, math.length(new float2(x, y)));
                }
                sb.Append(x == 0 && y == 0 ? (im ? '@' : 'o') : im ? '#' : tr.HasWall(new int2(x, y), out _, out _, out _) ? '+' : '.');
            }
            rows.Add(sb.ToString());
        }
        var zones = new List<string>();
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ImmunityZoneCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                var z = em.GetComponentData<ImmunityZoneCD>(e);
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                string id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID.ToString() : "?";
                zones.Add($"{id} ({p.x:F1},{p.z:F1}) radius={z.radius} offset=({z.offset.x},{z.offset.y}) rect={(z.useRectangularBounds ? $"{z.rectangularWidth}x{z.rectangularHeight}" : "no")} remove={z.removeImmunityZone}");
            }
        string bounds = count == 0 ? "none" : $"x {minX}..{maxX}, y {minY}..{maxY}, farthest {far:F2} from (0,0)";
        Log($"immune r={r}: {count} tiles, {bounds}; zones [{string.Join(" | ", zones)}]");
        Log($"immune map (# immune, @ core tile immune, + wall, . other; top row y={r}, left x={-r}):\n" + string.Join("\n", rows));
        AIPlayerSpikeMod.Reply($"[시험용] 코어 보호 구역: {count}칸, {bounds}; 보호 구역 물체 {zones.Count}개");
    }

    // the tile's layers and every entity within 2 tiles (id, position, prefab size, variation, component names)
    private static void Tile(World server, int2 t)
    {
        var tr = new AITileReader(server);
        var layers = new List<string>();
        foreach (var l in tr.Layers(t)) layers.Add($"{l.tileType}/{l.tileset}");
        var em = server.EntityManager;
        var ents = new List<string>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>()))
        using (var arr = q.ToEntityArray(Allocator.Temp))
            foreach (var e in arr)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (math.distance(p.xz, (float2)t) > 2.0f) continue;
                if (em.HasComponent<TileCD>(e)) continue;
                string id = em.HasComponent<ObjectDataCD>(e) ? $"{em.GetComponentData<ObjectDataCD>(e).objectID} var={em.GetComponentData<ObjectDataCD>(e).variation}" : "no ObjectDataCD";
                string size = "";
                if (em.HasComponent<ObjectDataCD>(e))
                {
                    ObjectInfo info = PugDatabase.GetObjectInfo(em.GetComponentData<ObjectDataCD>(e).objectID, 0);
                    if (info != null) size = $" size={info.prefabTileSize.x}x{info.prefabTileSize.y} corner=({info.prefabCornerOffset.x},{info.prefabCornerOffset.y}) toggle->{info.variationToToggleTo}";
                }
                var names = new List<string>();
                using (var types = em.GetComponentTypes(e, Allocator.Temp))
                    foreach (var ct in types) { string nm = ct.GetManagedType()?.Name ?? ct.ToString(); if (!nm.StartsWith("Ghost") && !nm.StartsWith("Linked")) names.Add(nm); }
                names.Sort();
                ents.Add($"{id} ({p.x:F2},{p.z:F2}){size} [{string.Join(",", names)}]");
            }
        Log($"tile ({t.x},{t.y}): layers [{string.Join(" ", layers)}] walkable={tr.Walkable(t)} spawnPad={AIPlaceGrid.SpawnPad(t.x, t.y)}; entities within 2: {ents.Count}\n  " + string.Join("\n  ", ents));
        AIPlayerSpikeMod.Reply($"[시험용] 칸 ({t.x},{t.y}): 층 {string.Join(" ", layers)}, 둘레 2칸 물체 {ents.Count}개(로그에 구성 요소)");
    }

    private static void Self(World server, Entity player)
    {
        var em = server.EntityManager;
        string Desc(Entity e)
        {
            if (e == Entity.Null || !em.Exists(e)) return "none";
            string id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID.ToString() : "no ObjectDataCD";
            bool ghost = em.HasComponent<PlayerGhost>(e);
            string col = "no collider";
            if (em.HasComponent<Unity.Physics.PhysicsCollider>(e))
            {
                var pc = em.GetComponentData<Unity.Physics.PhysicsCollider>(e);
                col = pc.IsValid ? $"collider {pc.Value.Value.GetCollisionResponse()}" : "collider invalid";
            }
            return $"id={id} PlayerGhost={ghost} {col}";
        }
        Entity host = Entity.Null;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents) if (e != player && !AIPlayerSpikeMod.IsAiPlayer(e)) { host = e; break; }
        string ai = Desc(player), h = Desc(host);
        Log($"self: ai {ai}; host {h}; spawn rules: periodic (200 tiles) queries PlayerGhost+LocalTransform; respawn (6 tiles) counts physics hits with ObjectDataCD id Player");
        AIPlayerSpikeMod.Reply($"[시험용] AI: {ai} / 호스트: {h}");
    }

    // once per character tick (AIBrain.Tick); does nothing unless a watcher is on and the test switch exists
    public static void Frame(World server, Entity player, float now)
    {
        if (!_w.TryGetValue(player, out var w) || (!w.door && !w.hits)) return;
        if (!AITestSwitches.Test) { w.door = w.hits = false; Log("watchers OFF: test switch gone"); return; }
        if (w.door && now > w.doorUntil) { w.door = false; Log("door watch OFF (10 min)"); }
        if (w.hits && now > w.hitsUntil) { w.hits = false; Log("hits watch OFF (10 min)"); }
        if (now < w.next) return;
        w.next = now + 0.25f;
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        if (w.door) DoorTick(server, player, em, me, w);
        if (w.hits) HitsTick(server, player, em, me, w);
    }

    private static bool IsDoorLike(EntityManager em, Entity e) => AIDoorFacts.IsDoorLike(em, e);

    private static void DoorTick(World server, Entity player, EntityManager em, float3 me, Watch w)
    {
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (!IsDoorLike(em, e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                float d = math.distance(me.xz, p.xz);
                if (d > 10f) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                string col = "no collider";
                if (em.HasComponent<Unity.Physics.PhysicsCollider>(e))
                {
                    var pc = em.GetComponentData<Unity.Physics.PhysicsCollider>(e);
                    col = pc.IsValid ? pc.Value.Value.GetCollisionResponse().ToString() : "invalid";
                }
                int2 t = AIBody.TileOf(p);
                string state = $"variation={od.variation} ({(od.variation == 1 || od.variation == 3 ? "open" : "closed")}) collider={col} aiBlocked={blocked.Contains(t)}";
                if (w.doorState.TryGetValue(e, out var old) && old == state) continue;
                w.doorState[e] = state;
                Log($"door {od.objectID} ({t.x},{t.y}) {state}; ai ({me.x:F2},{me.z:F2}) dist {d:F2}");
            }
    }

    private static void HitsTick(World server, Entity player, EntityManager em, float3 me, Watch w)
    {
        var seen = new HashSet<Entity>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (math.distance(me.xz, p.xz) > w.r) continue;
                string what = null;
                if (em.HasComponent<TileCD>(e))
                {
                    var tc = em.GetComponentData<TileCD>(e);
                    if (tc.tileType == TileType.wall || tc.tileType == TileType.fence || tc.tileType == TileType.thinWall) what = $"tile {tc.tileType} tileset={tc.tileset}";
                }
                else if (em.HasComponent<ObjectDataCD>(e) && !em.HasComponent<EnemyCD>(e) && !em.HasComponent<PlayerGhost>(e))
                {
                    var id = em.GetComponentData<ObjectDataCD>(e).objectID;
                    if (IsDoorLike(em, e) || id.ToString().Contains("Torch") || id.ToString().Contains("Fence") || id.ToString().Contains("Door")) what = $"object {id}";
                }
                if (what == null) continue;
                seen.Add(e);
                int hp = em.GetComponentData<HealthCD>(e).health;
                int2 t = AIBody.TileOf(p);
                if (w.hp.TryGetValue(e, out var old) && hp < old.hp) Log($"hit {what} ({t.x},{t.y}) hp {old.hp}->{hp}; {NearestEnemy(em, p)}");
                w.hp[e] = (what, t, hp);
            }
        var gone = new List<Entity>();
        foreach (var kv in w.hp) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var e in gone)
        {
            var old = w.hp[e];
            w.hp.Remove(e);
            if (math.distance(me.xz, (float2)old.t) > w.r - 1.5f) continue;   // left the watched circle, not broken
            bool exists = em.Exists(e);
            Log($"gone {old.what} ({old.t.x},{old.t.y}) last hp {old.hp}, entity {(exists ? "still exists" : "destroyed")}; {NearestEnemy(em, new float3(old.t.x, 0f, old.t.y))}");
        }
    }

    private static string NearestEnemy(EntityManager em, float3 at)
    {
        string best = "no enemy"; float bd = float.MaxValue;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                float d = math.distance(at.xz, p.xz);
                if (d < bd) { bd = d; best = $"nearest enemy {(em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID.ToString() : "?")} at {d:F1} ({p.x:F1},{p.z:F1})"; }
            }
        return best;
    }
}
