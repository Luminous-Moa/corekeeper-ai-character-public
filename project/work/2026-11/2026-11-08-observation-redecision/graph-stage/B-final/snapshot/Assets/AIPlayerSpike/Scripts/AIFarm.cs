// AIFarm — the AI's own field near base: dig plots with a hoe, plant seeds, water them, harvest ripe crops (먹거리 자급
// 조각 2; report work/2026-11/2026-11-04-먹거리-자급/2-농사.md, session 코어키퍼-AI-캐릭터).
//
// Game rules (decompile C:\Dev\_ref\corekeeper-decomp):
// - Aim: with ClientInput.wasAiming the placement spot of a held tool/placeable is the aimed point
//   (PlacementHandler.FindPlaceablePositionFromMouseOrJoystick: mouseOrJoystickWorldPoint clamped to a box of
//   (size + 1.5) around the player, rounded to a tile), else a tile in the facing direction. The mod aims at the tile.
// - Hoe (HoeSlot.UpdateEquipment / Dig, second interact held, cooldown 0.4 s): ground of a hoeable tileset
//   (EquipmentSlotConstantCD.hoeableGroundTilesets) becomes dugUpGround (HOE_GROUND); a ripe plant (PlantCD + GrowingCD
//   at the last stage, or a wild Mushroom) is harvested: destroyed with its drop + PlayerController.OnHarvest
//   (HARVEST_PLANTS); dug-up / watered ground with nothing ripe on it is FLATTENED (undone). So the hoe is only used on
//   ground or a ripe crop, with one short press per target (FarmSkill), never held.
// - Seed: a placeable item (PlaceObjectSlot); where it may be placed comes from the seed prefab's data (dug-up ground;
//   checked in play, test plan F4).
// - Water can (WaterCanSlot.PlaceItem, cooldown 0.35 s): aimed at water (not lava, tileset 3) it refills to the prefab's
//   FullnessCD.maxFullness; aimed at dug-up ground while it holds water, the tile becomes wateredGround and the can loses 5.
// - Growth (PlantsGrowingSystem): a crop's stage timer runs only while its tile is wateredGround (re-checked by a timer
//   entity); each stage takes the prefab's stage time (ObjectPropertiesCD); at the last stage it is ripe
//   (HasFinishedGrowingCD) and the watered layer is removed — water again after replanting.
// Own plots only (user rule, as own chests): a character remembers and uses only the plots it dug itself (per world,
// memory.json). Crops on other dug-up ground (the host's or another AI's) are never harvested or broken for food
// (ForeignCrop, also used by the food-source options).
using System;
using System.Collections.Generic;
using System.Text;
using Pug.Properties;
using PlayerEquipment;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIFarm
{
    public const int MaxPlots = 6;            // plots a character keeps (one small field)
    public const int WaterPerUse = 5;         // WaterCanSlot.WATER_CONSUMPTION_PER_USE
    public const string Rule = "씨앗은 괭이로 일군 땅(판 땅)에 심고, 물 준 땅에서만 자람(다 자라면 물기가 사라져 다시 심으면 다시 물을 줘야 함). 괭이는 빈 땅을 일구고 다 자란 작물을 거둠(판 땅을 다시 괭이질하면 평평해져서 쓰지 않음). 물뿌리개는 물(용암 아님) 옆에서 채우고 한 번에 5씩 씀";

    // ---- per-character state (multi-AI): AIAgent.Cur's Slots[13]
    public sealed class Plot { public int2 tile; public long since; public int goneSeen; }
    private sealed class State
    {
        public readonly Dictionary<int2, Plot> plots = new Dictionary<int2, Plot>();
        public readonly Dictionary<int2, long> water = new Dictionary<int2, long>();   // known water tiles (not lava), last seen
        public readonly Dictionary<int2, float> bad = new Dictionary<int2, float>();    // tiles a hoe did not dig (this session)
        public readonly HashSet<int2> growthLogged = new HashSet<int2>();
        public float lastWaterScan = -999f;
        public int harvested;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Farm);
    private static State S => StateOf(AIAgent.Cur);

    public static int PlotCount => S.plots.Count;
    public static bool IsOwnPlot(int2 t) => S.plots.ContainsKey(t);
    public static IEnumerable<int2> OwnPlotTiles => S.plots.Keys;     // for AIPlaceSpot (keep the field reachable)
    public static IEnumerable<int2> KnownWaterTiles => S.water.Keys;

    // ------------------------------------------------------------------ memory (memory.json via AIMemory)

    public static void ClearMemory() { var s = S; s.plots.Clear(); s.water.Clear(); s.bad.Clear(); s.growthLogged.Clear(); }

    public static void Import(AIMemory.Data d)
    {
        var s = S;
        for (int i = 0; d.farmXY != null && 2 * i + 1 < d.farmXY.Length; i++)
        {
            var t = new int2(d.farmXY[2 * i], d.farmXY[2 * i + 1]);
            s.plots[t] = new Plot { tile = t, since = d.farmSince != null && i < d.farmSince.Length ? d.farmSince[i] : 0 };
        }
        for (int i = 0; d.waterXY != null && 2 * i + 1 < d.waterXY.Length; i++)
            s.water[new int2(d.waterXY[2 * i], d.waterXY[2 * i + 1])] = d.waterSeen != null && i < d.waterSeen.Length ? d.waterSeen[i] : 0;
    }

    public static void Export(AIMemory.Data d)
    {
        var s = S;
        var xy = new List<int>(); var since = new List<long>();
        foreach (var p in s.plots.Values) { xy.Add(p.tile.x); xy.Add(p.tile.y); since.Add(p.since); }
        d.farmXY = xy.ToArray(); d.farmSince = since.ToArray();
        var wxy = new List<int>(); var wsn = new List<long>();
        foreach (var kv in s.water) { wxy.Add(kv.Key.x); wxy.Add(kv.Key.y); wsn.Add(kv.Value); }
        d.waterXY = wxy.ToArray(); d.waterSeen = wsn.ToArray();
    }

    public static void NotePlot(int2 t)
    {
        if (S.plots.ContainsKey(t)) return;
        S.plots[t] = new Plot { tile = t, since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        AIMemory.MarkDirty();
        AIPlayerSpikeMod.Log($"FARM: own plot dug at ({t.x},{t.y}), {math.distance((float2)t, (float2)AIBase.BaseTile):F0} tiles from base; plots {S.plots.Count}");
    }

    public static void NoteBadTile(int2 t, string why)
    {
        S.bad[t] = Time.realtimeSinceStartup;
        AIPlayerSpikeMod.Log($"FARM: tile ({t.x},{t.y}) not used for a plot for now: {why}");
    }

    public static void NoteHarvest(int n) { S.harvested += n; }

    // Called from AIMemory.Observe (about once per second): an own plot whose tile is in view without dug-up / watered
    // ground on two observations in a row is forgotten (flattened or built over). Water tiles in view are remembered
    // (every 5 s), and a remembered one seen without water is forgotten.
    public static void Observe(World server, Entity player, float3 pos, long unix)
    {
        var s = S;
        var tr = new AITileReader(server);
        foreach (var t in new List<int2>(s.plots.Keys))
        {
            if (!AIVision.CanSee(pos, t)) continue;
            var p = s.plots[t];
            if (IsDug(tr, t)) { p.goneSeen = 0; continue; }
            if (++p.goneSeen >= 2)
            {
                s.plots.Remove(t);
                AIMemory.MarkDirty();
                AIPlayerSpikeMod.Log($"FARM: own plot ({t.x},{t.y}) seen without dug-up ground (flattened or built over); forgotten");
            }
        }
        float now = Time.realtimeSinceStartup;
        if (now - s.lastWaterScan < 5f) return;
        s.lastWaterScan = now;
        float2 half = AIVision.HalfExtents();
        int2 me = AIBody.TileOf(pos);
        int hx = (int)math.floor(half.x), hz = (int)math.floor(half.y);
        bool changed = false;
        for (int x = me.x - hx; x <= me.x + hx; x++)
            for (int z = me.y - hz; z <= me.y + hz; z++)
            {
                var t = new int2(x, z);
                if (!AIVision.CanSee(pos, t)) continue;
                bool w = IsWater(tr, t);
                if (w && HasStand(tr, t))
                {
                    if (!s.water.ContainsKey(t)) changed = true;
                    s.water[t] = unix;
                }
                else if (s.water.ContainsKey(t)) { s.water.Remove(t); changed = true; }
            }
        if (changed) AIMemory.MarkDirty();
    }

    // ------------------------------------------------------------------ tiles

    public static bool IsDug(AITileReader tr, int2 t)
    {
        foreach (var l in tr.Layers(t)) if (l.tileType == TileType.dugUpGround || l.tileType == TileType.wateredGround) return true;
        return false;
    }
    public static bool IsWatered(AITileReader tr, int2 t)
    {
        foreach (var l in tr.Layers(t)) if (l.tileType == TileType.wateredGround) return true;
        return false;
    }
    public static bool IsWater(AITileReader tr, int2 t)
    {
        var top = tr.Top(t);
        return top.tileType == TileType.water && top.tileset != 3;
    }
    private static bool HasStand(AITileReader tr, int2 t)
    {
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) if ((x != 0 || y != 0) && tr.Walkable(t + new int2(x, y))) return true;
        return false;
    }

    // A walkable tile next to t (8 neighbours, not t), known to the AI, nearest to `me`; false if none.
    public static bool StandFor(World server, Entity player, AITileReader tr, float3 me, int2 t, out int2 stand)
    {
        stand = t; float best = float.MaxValue; bool found = false;
        // not a tile covered by an object (a pot or chest next to the field: harvest walked at the pot's tile for 40 s,
        // ai2 2026-11-04 F6)
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var shut = AIEdge.ShutDoorsNear(server, t, 1);   // 검수 S2: the one stand rule (no shut door either)
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                var n = t + new int2(x, y);
                if (!AIEdge.StandOk(tr, n, blocked, shut) || !(AIVision.CanSee(me, n) || AIMemory.SeenTile(n))) continue;
                if (S.plots.ContainsKey(n) && (x != 0 && y != 0)) continue;   // prefer not to stand on a diagonal plot (crops)
                float d = math.distance(me.xz, (float2)n) + (x != 0 && y != 0 ? 0.3f : 0f);
                if (d < best) { best = d; stand = n; found = true; }
            }
        return found;
    }

    private static readonly List<int> _hoeable = new List<int>();
    private static bool _hoeableRead;
    private static bool Hoeable(World server, Entity player, int tileset)
    {
        if (!_hoeableRead)
        {
            try
            {
                var em = server.EntityManager;
                if (em.HasComponent<EquipmentSlotConstantCD>(player))
                {
                    var c = em.GetComponentData<EquipmentSlotConstantCD>(player);
                    if (c.hoeableGroundTilesets.IsCreated)
                    {
                        ref var arr = ref c.hoeableGroundTilesets.Value;
                        for (int i = 0; i < arr.Length; i++) _hoeable.Add((int)arr[i]);
                        _hoeableRead = true;
                        AIPlayerSpikeMod.Log($"FARM: hoeable ground tilesets (game data) [{string.Join(", ", _hoeable.ConvertAll(x => ((Tileset)x).ToString()))}]");
                    }
                }
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("farm: hoeable tilesets read failed", e); _hoeableRead = true; }
        }
        return _hoeable.Count == 0 || _hoeable.Contains(tileset);   // unknown list: let the hoe try (a failed tile is noted)
    }

    // ------------------------------------------------------------------ crops

    // later: seconds of the objects it turns into after its last stage (a seed becomes a plant: PlantsGrowingSystem,
    // property -1534320058 = the object to grow into; that object starts at stage 0 on the same watered tile), so
    // "ripe in" counts the whole chain (a seed's own 120 s plus the plant's 2 x 240 s, ai2 2026-11-04 F5).
    public struct Crop { public Entity e; public ObjectID id; public bool ripe; public int stage, stages; public float stageTime, timer, later; public ObjectID harvest, next; public int n; }

    private static readonly Dictionary<ObjectID, (float secs, ObjectID next)> _growData = new Dictionary<ObjectID, (float, ObjectID)>();

    // Whole growing time of an object kind from its prefab (stages x stage time) and what it turns into; cached.
    private static (float secs, ObjectID next) GrowData(World server, ObjectID id)
    {
        if (_growData.TryGetValue(id, out var d)) return d;
        d = (0f, ObjectID.None);
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(id, bank, 0);
                    if (prefab != Entity.Null && em.HasComponent<ObjectPropertiesCD>(prefab))
                    {
                        var pr = em.GetComponentData<ObjectPropertiesCD>(prefab);
                        int st = 0; float t = 0f;
                        try { st = pr.Get<int>(1963487001); } catch { }
                        try { t = pr.Get<float>(-965595447); } catch { }
                        ObjectID nx = ObjectID.None;
                        try { if (!pr.TryGet<ObjectID>(-1534320058, out nx)) nx = ObjectID.None; } catch { nx = ObjectID.None; }
                        d = (math.max(0, st) * math.max(0f, t), nx);
                    }
                }
            }
        }
        catch { }
        _growData[id] = d;
        return d;
    }

    private static float LaterSeconds(World server, ObjectID next)
    {
        float s = 0f;
        for (int i = 0; i < 4 && next != ObjectID.None; i++)
        {
            var d = GrowData(server, next);
            s += d.secs;
            next = d.next;
        }
        return s;
    }

    // Plants (GrowingCD or PlantCD) in view by tile.
    public static Dictionary<int2, Crop> CropsInView(World server, float3 me)
    {
        var res = new Dictionary<int2, Crop>();
        var em = server.EntityManager;
        foreach (var ct in new[] { ComponentType.ReadOnly<GrowingCD>(), ComponentType.ReadOnly<PlantCD>() })
            using (var q = em.CreateEntityQuery(ct, ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<ObjectDataCD>()))
            using (var ents = q.ToEntityArray(Allocator.Temp))
                foreach (var e in ents)
                {
                    float3 p = em.GetComponentData<LocalTransform>(e).Position;
                    if (!AIVision.CanSee(me, p.xz)) continue;
                    if (em.HasComponent<EntityDestroyedCD>(e) && em.IsComponentEnabled<EntityDestroyedCD>(e)) continue;
                    if (em.HasComponent<HealthCD>(e) && em.GetComponentData<HealthCD>(e).health <= 0) continue;
                    int2 t = AIBody.TileOf(p);
                    if (res.ContainsKey(t)) continue;
                    res[t] = Read(server, e);
                }
        return res;
    }

    public static Crop Read(World server, Entity e)
    {
        var em = server.EntityManager;
        var c = new Crop { e = e, id = em.GetComponentData<ObjectDataCD>(e).objectID };
        bool props = em.HasComponent<ObjectPropertiesCD>(e);
        ObjectPropertiesCD pr = props ? em.GetComponentData<ObjectPropertiesCD>(e) : default;
        if (em.HasComponent<GrowingCD>(e))
        {
            var g = em.GetComponentData<GrowingCD>(e);
            c.stage = g.currentStage;
            if (props)
            {
                try { c.stages = pr.Get<int>(1963487001); } catch { }
                try { c.stageTime = pr.Get<float>(-965595447); } catch { }
                try { c.ripe = g.HasFinishedGrowing(pr); } catch { }
                try { if (pr.TryGet<ObjectID>(-1534320058, out var nx)) c.next = nx; } catch { }
            }
        }
        if (c.next != ObjectID.None) c.later = LaterSeconds(server, c.next);   // a seed: add the plant it turns into
        if (em.HasComponent<HasFinishedGrowingCD>(e)) c.ripe = true;
        if (em.HasComponent<PlantCD>(e)) { var pc = em.GetComponentData<PlantCD>(e); c.harvest = pc.objectToDropWhenHarvested; c.n = math.max(1, pc.numberOfPlantsToDrop); }
        if (em.HasComponent<GrowTimerRefCD>(e))
        {
            var r = em.GetComponentData<GrowTimerRefCD>(e);
            if (r.GrowTimerEntity != Entity.Null && em.Exists(r.GrowTimerEntity) && em.HasComponent<GrowTimerCD>(r.GrowTimerEntity))
            {
                var gt = em.GetComponentData<GrowTimerCD>(r.GrowTimerEntity);
                c.timer = gt.Value;
                if (c.stageTime <= 0f) c.stageTime = gt.StageTime;
            }
        }
        return c;
    }

    // Seconds until ripe while watered; -1 = unknown (not watered, no stage data).
    public static float SecondsLeft(Crop c, bool watered)
    {
        if (c.ripe) return 0f;
        if (!watered || c.stages <= 0 || c.stageTime <= 0f) return -1f;
        return math.max(0, c.stages - c.stage - 1) * c.stageTime + math.max(0f, c.stageTime - c.timer) + c.later;
    }

    // A plant on dug-up / watered ground that is not one of this character's plots: someone else's crop (the host's or
    // another AI's). Never harvested or broken for food (own plots only). Wild plants stand on natural ground.
    public static bool ForeignCrop(World server, Entity e)
    {
        var em = server.EntityManager;
        if (!em.HasComponent<LocalTransform>(e)) return false;
        if (!em.HasComponent<PlantCD>(e) && !em.HasComponent<GrowingCD>(e)) return false;
        int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
        if (S.plots.ContainsKey(t)) return false;
        return IsDug(new AITileReader(server), t);
    }

    // ------------------------------------------------------------------ items

    // A seed: a placeable item whose prefab grows (GrowingCD); harvest = the prefab's PlantCD drop (None = not known).
    public static bool IsSeed(World server, ObjectID id, out ObjectID harvest) => SeedConfirmed(server, id, 0, out harvest);
    public static bool IsSeed(World server, ObjectID id) => IsSeed(server, id, out _);

    // A seed worth planting for food: its harvest fills hunger, or its harvest is not in the data (try it).
    public static bool IsFoodSeed(World server, ObjectID id) => IsSeed(server, id, out var h) && (h == ObjectID.None || AIFood.HungerAdd(server, h, 0) > 0);

    // Confirmed seeds in the bag (hotbar included, cursor slot not): one entry per kind, most first.
    public static List<(ObjectID id, int count, ObjectID harvest)> SeedsInBag(World server, Entity player)
    {
        var d = new Dictionary<ObjectID, int>();
        var em = server.EntityManager;
        if (em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            int end = em.HasComponent<CraftingCD>(player) ? math.min(buf.Length, em.GetComponentData<CraftingCD>(player).outputSlotIndex) : buf.Length;
            if (end <= 0) end = buf.Length;
            for (int i = 0; i < end; i++)
            {
                var id = buf[i].objectID;
                if (id == ObjectID.None || !IsSeed(server, id)) continue;
                d.TryGetValue(id, out int c); d[id] = c + math.max(1, buf[i].amount);
            }
        }
        var res = new List<(ObjectID, int, ObjectID)>();
        foreach (var kv in d) { IsSeed(server, kv.Key, out var h); res.Add((kv.Key, kv.Value, h)); }
        res.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return res;
    }

    // Confirmed seeds in own chests (contents as of when last opened).
    public static int SeedsInChests(World server)
    {
        int n = 0;
        foreach (var c in AIBase.OwnChests()) if (c.openedUnix != 0) foreach (var kv in c.items) if (kv.Value > 0 && IsSeed(server, kv.Key)) n += kv.Value;
        return n;
    }

    // A working hoe in the bag (ObjectType.Hoe, not broken); None if there is none.
    public static ObjectID Hoe(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return ObjectID.None;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || info.objectType != ObjectType.Hoe) continue;
            if (!PlayerController.ItemIsBroken(buf[i].objectData)) return buf[i].objectID;
        }
        return ObjectID.None;
    }

    // The water can in the bag: id, water in it (ObjectDataCD.amount) and its capacity (prefab FullnessCD).
    public static bool WaterCan(World server, Entity player, out ObjectID id, out int water, out int max, out int slot)
    {
        id = ObjectID.None; water = 0; max = 0; slot = -1;
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || info.objectType != ObjectType.WaterCan) continue;
            id = buf[i].objectID; water = buf[i].amount; slot = i;
            max = CanCapacity(server, id);
            return true;
        }
        return false;
    }

    private static readonly Dictionary<ObjectID, int> _cap = new Dictionary<ObjectID, int>();
    private static int CanCapacity(World server, ObjectID id)
    {
        if (_cap.TryGetValue(id, out int c)) return c;
        c = 0;
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(id, bank, 0);
                if (prefab != Entity.Null && em.HasComponent<FullnessCD>(prefab)) c = em.GetComponentData<FullnessCD>(prefab).maxFullness;
            }
        }
        catch { }
        _cap[id] = c;
        AIPlayerSpikeMod.Log($"FARM: {id} holds {c} water (FullnessCD), {WaterPerUse} per use");
        return c;
    }

    // ------------------------------------------------------------------ snapshot

    public class Snap
    {
        public int plots, inView;
        public readonly List<int2> empty = new List<int2>(), dry = new List<int2>(), ripe = new List<int2>(), growing = new List<int2>();
        public Dictionary<int2, Crop> crops;
        public List<(ObjectID id, int count, ObjectID harvest)> seeds;
        public int seedsBag, seedsChest;
        public ObjectID hoe = ObjectID.None, can = ObjectID.None;
        public int water, waterMax;
        public float nextRipe = -1f;          // seconds, of the watered growing crops in view (-1 = none / unknown)
        public int Want => math.max(0, seedsBag + seedsChest - empty.Count);   // new plots worth digging
        public bool SeedsAvail => seedsBag + seedsChest > 0;
    }

    public static Snap Take(World server, Entity player, float3 me)
    {
        var s = new Snap();
        var tr = new AITileReader(server);
        s.crops = CropsInView(server, me);
        foreach (var p in S.plots.Values)
        {
            s.plots++;
            if (!AIVision.CanSee(me, p.tile)) continue;
            s.inView++;
            bool watered = IsWatered(tr, p.tile);
            if (!s.crops.TryGetValue(p.tile, out var c)) { s.empty.Add(p.tile); continue; }
            if (c.ripe) { s.ripe.Add(p.tile); continue; }
            s.growing.Add(p.tile);
            if (!watered) s.dry.Add(p.tile);
            float left = SecondsLeft(c, watered);
            if (left >= 0f && (s.nextRipe < 0f || left < s.nextRipe)) s.nextRipe = left;
            if (S.growthLogged.Add(p.tile)) AIPlayerSpikeMod.Log($"FARM: crop {c.id} on own plot ({p.tile.x},{p.tile.y}): stage {c.stage}/{c.stages}, stage time {c.stageTime:F0}s, timer {c.timer:F0}s, watered {watered}, harvest {c.harvest} x{c.n}{(c.next != ObjectID.None ? $", then {c.next} (+{c.later:F0}s)" : "")}; ripe in {(left < 0 ? "?" : left.ToString("F0") + "s")}");
        }
        s.seeds = SeedsInBag(server, player);
        foreach (var x in s.seeds) s.seedsBag += x.count;
        s.seedsChest = SeedsInChests(server);
        s.hoe = Hoe(server, player);
        if (WaterCan(server, player, out var cid, out int w, out int mx, out _)) { s.can = cid; s.water = w; s.waterMax = mx; }
        return s;
    }

    public static string SnapText(Snap s) =>
        $"내 밭 {s.plots}칸(화면에 {s.inView}): 빈 칸 {s.empty.Count}, 자라는 중 {s.growing.Count}(그중 물 안 준 칸 {s.dry.Count}), 다 자람 {s.ripe.Count}" +
        $"{(s.nextRipe >= 0f ? $", 다음 다 자람 약 {s.nextRipe:F0}초 뒤" : "")}. 씨앗 가방 {(s.seeds.Count == 0 ? "0" : string.Join(", ", s.seeds.ConvertAll(x => $"{x.id} {x.count}")))}, 내 상자 {s.seedsChest}. " +
        $"괭이 {(s.hoe == ObjectID.None ? "없음" : s.hoe.ToString())}, 물뿌리개 {(s.can == ObjectID.None ? "없음" : $"{s.can} 물 {s.water}/{s.waterMax}")}, 아는 물 {S.water.Count}곳. 이번 접속 거둔 수 {S.harvested}";

    // ------------------------------------------------------------------ preparation (AIBrain.WantedItems)

    // Tools the farm goal needs that the bag lacks: the preparation path makes them (WoodHoe: wood at a workbench;
    // WaterCan: copper bars at a workbench).
    public static List<(ObjectID id, string why)> ToolTargets(World server, Entity player)
    {
        var res = new List<(ObjectID, string)>();
        if (AIBrain.Goal.kind != GoalKind.Farm) return res;
        var s = Take(server, player, AIBody.Pos(server, player));
        string mode = AIBrain.Goal.mode;
        if (s.hoe == ObjectID.None && (mode == "hoe" || mode == "harvest" || string.IsNullOrEmpty(mode) && (s.Want > 0 || s.ripe.Count > 0))) res.Add((ObjectID.WoodHoe, mode == "harvest" || s.ripe.Count > 0 ? "다 자란 작물을 거둘 괭이가 없음" : "선택한 땅을 일굴 괭이가 없음"));
        if (s.can == ObjectID.None && (mode == "water" || mode == "fill" || string.IsNullOrEmpty(mode) && (s.dry.Count > 0 || s.SeedsAvail))) res.Add((ObjectID.WaterCan, "선택한 밭 작업의 물뿌리개가 없음(물 준 땅에서만 자람)"));
        return res;
    }

    // ------------------------------------------------------------------ goal

    // Farm: done when nothing can be done on the field now (all own plots in view; nothing ripe; no empty plot while
    // seeds are at hand; no dry crop; no new plot worth digging) and something is growing or was harvested.
    public static bool GoalDone(World server, Entity player, out string text)
    {
        text = "";
        if (!string.IsNullOrEmpty(AIBrain.Goal.mode)) return false; // explicit work is reported by AILife; finish is an AI choice
        if (AIBody.Current is FarmSkill && AIBody.Current.Status == SkillStatus.Running) return false;
        var s = Take(server, player, AIBody.Pos(server, player));
        if (s.plots == 0 || s.inView < s.plots) return false;
        if (s.ripe.Count > 0 || s.dry.Count > 0 || (s.empty.Count > 0 && s.SeedsAvail) || s.Want > 0) return false;
        if (s.growing.Count == 0 && S.harvested == 0) return false;   // nothing planted and no seeds: not done (stall says why)
        text = $"밭 일 끝 — 자라는 중 {s.growing.Count}칸{(s.nextRipe >= 0f ? $"(다음 다 자람 약 {s.nextRipe:F0}초 뒤)" : "")}, 빈 칸 {s.empty.Count}{(s.empty.Count > 0 ? "(씨앗 없음)" : "")}, 이번 접속 거둔 수 {S.harvested}";
        return true;
    }

    public static string StallNote(World server, Entity player)
    {
        var s = Take(server, player, AIBody.Pos(server, player));
        string why = !s.SeedsAvail && s.growing.Count == 0 && s.ripe.Count == 0 ? "씨앗이 없음(가방·내 상자 모두), 거둘 것·자라는 것도 없음"
            : s.dry.Count > 0 && s.can != ObjectID.None && s.water < WaterPerUse && S.water.Count == 0 ? "물뿌리개에 물이 없고 아는 물이 없음"
            : "할 수 있는 밭 일이 없음";
        return $"{why} — {SnapText(s)}, 기지 {AIBase.BaseText}, 준비: {AIBrain.PrepNote}";
    }

    // ------------------------------------------------------------------ candidates (added with the base options)

    public sealed class Selection
    {
        public string Mode, Key;
        public int SourceSlot, Offered, Requested;
        public ContainedObjectsBuffer Item;
        public readonly List<int> SourceSlots = new List<int>();
        public readonly List<ContainedObjectsBuffer> SourceItems = new List<ContainedObjectsBuffer>();
        public readonly List<int2> Tiles = new List<int2>();
        public readonly List<TileStamp> Before = new List<TileStamp>();
    }
    public struct TileStamp
    {
        public int2 Tile;
        public bool Dug, Watered, Own, HasCrop, Ripe;
        public Entity CropEntity;
        public ObjectID CropId, HarvestId;
    }
    private struct SeedSlot { public int slot; public ContainedObjectsBuffer item; }
    private static string ItemKey(ContainedObjectsBuffer x) => x.objectID + (x.variation == 0 ? "" : "#v" + x.variation);
    private static bool ItemFilter(string value, ObjectID id, int variation = 0) => string.IsNullOrEmpty(value) || value == "all" || value == id.ToString() || value == id + "#v" + variation;
    private static List<SeedSlot> SeedSlots(World w, Entity p)
    {
        var result = new List<SeedSlot>();
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
            if (AIInventory.Read(w, p, i, out var x) && x.amount > 0 && SeedConfirmed(w, x.objectID, x.variation, out _)) result.Add(new SeedSlot { slot = i, item = x });
        return result;
    }
    // Require an actual growable placeable and crop/drop chain; food value is not seed classification.
    private static bool SeedConfirmed(World w, ObjectID id, int variation, out ObjectID harvest)
    {
        harvest = ObjectID.None;
        var info = PugDatabase.GetObjectInfo(id, variation);
        if (info == null || info.objectType != ObjectType.PlaceablePrefab) return false;
        var em = w.EntityManager;
        try
        {
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() != 1) return false;
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                Entity current = PugDatabase.GetPrimaryPrefabEntity(id, bank, variation);
                if (current == Entity.Null || !em.HasComponent<GrowingCD>(current)) return false;
                for (int depth = 0; depth < 5 && current != Entity.Null; depth++)
                {
                    if (em.HasComponent<PlantCD>(current)) { harvest = em.GetComponentData<PlantCD>(current).objectToDropWhenHarvested; return harvest != ObjectID.None; }
                    if (!em.HasComponent<ObjectPropertiesCD>(current)) return false;
                    var properties = em.GetComponentData<ObjectPropertiesCD>(current);
                    if (!properties.TryGet<ObjectID>(-1534320058, out var next) || next == ObjectID.None) return false;
                    current = PugDatabase.GetPrimaryPrefabEntity(next, bank, 0);
                }
            }
        }
        catch { }
        return false;
    }
    private static TileStamp ReadTile(World w, int2 tile, Dictionary<int2, Crop> crops)
    {
        var tr = new AITileReader(w); var stamp = new TileStamp { Tile = tile, Dug = IsDug(tr, tile), Watered = IsWatered(tr, tile), Own = IsOwnPlot(tile) };
        if (crops.TryGetValue(tile, out var crop)) { stamp.HasCrop = true; stamp.CropEntity = crop.e; stamp.CropId = crop.id; stamp.HarvestId = crop.harvest; stamp.Ripe = crop.ripe; }
        return stamp;
    }
    private static bool SameTile(TileStamp a, TileStamp b) => a.Dug == b.Dug && a.Watered == b.Watered && a.Own == b.Own && a.HasCrop == b.HasCrop && a.CropEntity == b.CropEntity && a.CropId == b.CropId && a.Ripe == b.Ripe;
    private static bool Coordinates(string text, string prefix, int n, out int[] values)
    {
        values = null; if (text == null || !text.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var parts = text.Substring(prefix.Length).Split(','); if (parts.Length != n) return false;
        values = new int[n]; for (int i = 0; i < n; i++) if (!int.TryParse(parts[i], out values[i])) return false;
        return true;
    }
    private static bool InWhere(int2 tile, string where)
    {
        if (string.IsNullOrEmpty(where) || where == "farm" || where == "tiles") return true;
        if (Coordinates(where, "tile:", 2, out var xy)) return tile.x == xy[0] && tile.y == xy[1];
        if (Coordinates(where, "rect:", 4, out var r)) return tile.x >= math.min(r[0], r[2]) && tile.x <= math.max(r[0], r[2]) && tile.y >= math.min(r[1], r[3]) && tile.y <= math.max(r[1], r[3]);
        return where.StartsWith("region:", StringComparison.Ordinal) && (";" + where.Substring(7) + ";").Contains(";" + tile.x + "," + tile.y + ";");
    }
    private static List<int2> Eligible(World w, Entity p, AIObservation o, LifeSelection request, Snap snap)
    {
        var tiles = new List<int2>();
        switch (request.Mode)
        {
            case "plant": tiles.AddRange(snap.empty); break;
            case "water": tiles.AddRange(snap.dry); break;
            case "harvest": tiles.AddRange(snap.ripe); break;
            case "hoe":
                bool explicitRange = request.MatchesGoal && request.Goal is AIGoal explicitGoal && !string.IsNullOrEmpty(explicitGoal.where);
                int count = request.RequestedCount > 0 || explicitRange || !string.IsNullOrEmpty(request.Where) && request.Where != "farm" ? int.MaxValue : snap.Want;
                tiles.AddRange(NewPlotTiles(w, p, o.pos, count)); break;
            case "fill": foreach (var t in S.water.Keys) if (AIVision.CanSee(o.pos, t) && IsWater(new AITileReader(w), t)) tiles.Add(t); break;
        }
        string upperWhere = request.MatchesGoal && request.Goal is AIGoal goal ? goal.where : null;
        var ground = new AITileReader(w);
        tiles.RemoveAll(t => !InWhere(t, request.Where) || !InWhere(t, upperWhere) || (request.Mode == "plant" || request.Mode == "water" || request.Mode == "harvest") && !IsDug(ground, t) || (request.Mode == "water" || request.Mode == "harvest") && (!snap.crops.TryGetValue(t, out var crop) || !ItemFilter(request.Item, crop.id)));
        return tiles;
    }
    private static List<List<int2>> Regions(List<int2> tiles)
    {
        var remaining = new HashSet<int2>(tiles); var groups = new List<List<int2>>();
        foreach (var anchor in tiles)
        {
            if (!remaining.Remove(anchor)) continue;
            var group = new List<int2> { anchor };
            for (int i = 0; i < group.Count; i++) foreach (var direction in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
            { var neighbour = group[i] + direction; if (remaining.Remove(neighbour)) group.Add(neighbour); }
            group.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)); groups.Add(group);
        }
        return groups;
    }
    public static List<AILife.Entry> LifeEntries(World w, Entity p, AIObservation o, string curKey, LifeSelection request)
    {
        var result = new List<AILife.Entry>();
        if (string.IsNullOrEmpty(request.Mode))
        {
            foreach (string mode in new[] { "hoe", "plant", "water", "fill", "harvest" }) result.Add(new AILife.Entry { Op = "mode", Id = mode, Text = mode == "hoe" ? "기지 근처 땅 일구기" : mode == "plant" ? "씨앗 심기" : mode == "water" ? "내 작물 물주기" : mode == "fill" ? "물뿌리개 물채우기" : "내 다 자란 작물 수확" });
            return result;
        }
        var snap = Take(w, p, o.pos);
        if (request.Mode == "plant" && string.IsNullOrEmpty(request.Item))
        {
            var seen = new HashSet<string>();
            foreach (var seed in SeedSlots(w, p)) { string key = ItemKey(seed.item); if (seen.Add(key)) result.Add(new AILife.Entry { Op = "item", Id = key, Text = $"{key} — 확인된 종자, 가방 슬롯 {seed.slot}, {seed.item.amount}개" }); }
            if (result.Count == 0) result.Add(new AILife.Entry { Text = "실제 설치·작물 연결이 확인된 가방 종자 없음. 미확인 종자는 실행하지 않음." });
            return result;
        }
        if ((request.Mode == "water" || request.Mode == "harvest") && string.IsNullOrEmpty(request.Item))
        {
            result.Add(new AILife.Entry { Op = "item", Id = "all", Text = "작물 종류 전체" }); var seen = new HashSet<string>();
            foreach (var crop in snap.crops.Values) if (seen.Add(crop.id.ToString())) result.Add(new AILife.Entry { Op = "item", Id = crop.id.ToString(), Text = crop.id.ToString() });
            return result;
        }
        if (string.IsNullOrEmpty(request.Where) || request.Where == "tiles")
        {
            var eligible = Eligible(w, p, o, request, snap);
            if (request.Where == "tiles") foreach (var tile in eligible) result.Add(new AILife.Entry { Op = "where", Id = $"tile:{tile.x},{tile.y}", Text = $"특정 칸 ({tile.x},{tile.y}) — {request.Mode}" });
            else
            {
                result.Add(new AILife.Entry { Op = "where", Id = "farm", Text = $"관측한 자기 영역 전체 — {request.Mode} 가능한 {eligible.Count}칸" });
                foreach (var group in Regions(eligible)) result.Add(new AILife.Entry { Op = "where", Id = "region:" + string.Join(";", group.ConvertAll(t => $"{t.x},{t.y}")), Text = $"연결구역 ({group[0].x},{group[0].y}) — {group.Count}칸, {request.Mode} 가능" });
                result.Add(new AILife.Entry { Op = "where", Id = "tiles", Text = "특정 칸 목록 보기" });
                foreach (var tile in S.plots.Keys) if (!AIVision.CanSee(o.pos, tile)) result.Add(new AILife.Entry { Op = "where", Id = $"tile:{tile.x},{tile.y}", Text = $"기억한 자기밭 ({tile.x},{tile.y}), 현재 화면 밖·상태 모름; 가서 확인" });
                if (request.Mode == "fill") foreach (var tile in S.water.Keys) if (!AIVision.CanSee(o.pos, tile)) result.Add(new AILife.Entry { Op = "where", Id = $"tile:{tile.x},{tile.y}", Text = $"기억한 물 ({tile.x},{tile.y}), 마지막 관측 {AIMemory.Ago(S.water[tile])}; 가서 확인" });
            }
            return result;
        }
        var exact = ExactCandidate(w, p, o, curKey, request);
        if (exact != null) result.Add(new AILife.Entry { Id = exact.key, Text = exact.desc, Candidate = exact });
        else result.Add(new AILife.Entry { Text = $"선택 {request.Mode}/{request.Item}/{request.Where}에 현재 적합한 자기 칸·종자·도구·물이 없음. 다른 대상으로 대체하지 않음." });
        return result;
    }
    public static AICandidate ExactCandidate(World w, Entity p, AIObservation o, string curKey, LifeSelection request)
    {
        if (AILife.RequestSatisfied(request)) return null;
        if (request == null || string.IsNullOrEmpty(request.Mode) || request.Mode == "plant" && string.IsNullOrEmpty(request.Item)) return null;
        if (Coordinates(request.Where, "tile:", 2, out var xy))
        {
            var tile = new int2(xy[0], xy[1]);
            if (!AIVision.CanSee(o.pos, tile) && (IsOwnPlot(tile) || request.Mode == "fill" && S.water.ContainsKey(tile)))
            {
                if (GoOption(w, p, o.pos, tile, request.Mode == "fill" ? "기억한 물" : "기억한 자기밭", "화면 밖·현재 상태 모름, 도착 후 재관측", curKey, out var go)) return go;
                return null;
            }
        }
        var snap = Take(w, p, o.pos); var targets = Eligible(w, p, o, request, snap);
        if (targets.Count == 0)
        {
            if (snap.plots > 0 && snap.inView == 0 && GoOption(w, p, o.pos, FieldCentre(), "내 밭", "화면 밖·현재 상태 모름, 도착 후 재관측", curKey, out var go)) return go;
            return null;
        }
        int source = -1; ContainedObjectsBuffer selected = default;
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
        {
            if (!AIInventory.Read(w, p, i, out var x) || x.objectID == ObjectID.None) continue;
            var info = PugDatabase.GetObjectInfo(x.objectID, x.variation); if (info == null) continue;
            bool usable = request.Mode == "plant" ? ItemFilter(request.Item, x.objectID, x.variation) && x.amount > 0 && SeedConfirmed(w, x.objectID, x.variation, out _) : request.Mode == "hoe" || request.Mode == "harvest" ? info.objectType == ObjectType.Hoe && !PlayerController.ItemIsBroken(x.objectData) : info.objectType == ObjectType.WaterCan && !PlayerController.ItemIsBroken(x.objectData);
            if (usable && (source < 0 || request.Mode == "plant" && x.amount > selected.amount)) { source = i; selected = x; }
        }
        if (source < 0) return null;
        var sources = new List<int>(); var sourceItems = new List<ContainedObjectsBuffer>();
        sources.Add(source); sourceItems.Add(selected);
        int seedCount = selected.amount;
        if (request.Mode == "plant")
            for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
                if (i != source && AIInventory.Read(w, p, i, out var x) && x.objectID == selected.objectID && x.variation == selected.variation && x.amount > 0)
                { sources.Add(i); sourceItems.Add(x); seedCount += x.amount; }
        int available = targets.Count;
        if (request.Mode == "plant") available = math.min(available, seedCount);
        if (request.Mode == "water") available = math.min(available, selected.amount / WaterPerUse);
        if (request.Mode == "fill") available = CanCapacity(w, selected.objectID) > selected.amount ? math.min(1, available) : 0;
        int requested = request.RequestedCount > 0 ? request.RequestedCount : math.min(6, available);
        int offered = math.min(6, math.min(requested, available)); if (offered <= 0) return null;
        var payload = new Selection { Mode = request.Mode, SourceSlot = source, Item = selected, Offered = offered, Requested = requested };
        payload.SourceSlots.AddRange(sources); payload.SourceItems.AddRange(sourceItems);
        payload.Tiles.AddRange(Nearest(targets, o.pos, offered));
        foreach (var tile in payload.Tiles) payload.Before.Add(ReadTile(w, tile, snap.crops));
        payload.Key = $"farm:{request.Mode}:{selected.objectID}#v{selected.variation}:s{source}:a{selected.auxDataIndex}:v{selected.variationUpdateCount}:n{offered}:" + string.Join(";", payload.Tiles.ConvertAll(t => $"{t.x},{t.y}"));
        for (int i = 0; i < sources.Count; i++) payload.Key += $":src{sources[i]}={sourceItems[i].auxDataIndex},{sourceItems[i].amount},{sourceItems[i].variationUpdateCount}";
        var binding = request.Copy(); binding.OfferedCount = offered;
        return new AICandidate { kind = "farm", key = payload.Key, actionMode = request.Mode, tiles = new List<int2>(payload.Tiles), tile = payload.Tiles[0], itemId = selected.objectID, itemVariation = selected.variation, itemCount = offered, progress = binding.MatchesGoal, farm = payload, life = binding,
            desc = $"{request.Mode} — {selected.objectID}(변형 {selected.variation}, 선택 슬롯 {source})로 [{string.Join(" ", payload.Tiles.ConvertAll(t => $"({t.x},{t.y})"))}]. 요청 {requested}칸 / 이번 시도 {offered}칸 / 현재 가능 {available}칸 / 잔량 {math.max(0, requested - offered)}칸 / 현재 부족 {math.max(0, requested - available)}칸. 작업 확인과 실제 가방 확보는 따로 관측." };
    }
    public static string Stale(World w, Entity p, AICandidate candidate) => SelectionStale(w, p, candidate?.farm);
    public static string SelectionStale(World w, Entity p, Selection selected)
    {
        if (selected == null) return "농사 명시 선택 payload 없음";
        if (!AIInventory.Read(w, p, selected.SourceSlot, out var item) || !AIInventory.Stamp(item).Exact(AIInventory.Stamp(selected.Item))) return "선택한 종자/도구 슬롯 상태가 바뀜";
        for (int i = 0; i < selected.SourceSlots.Count; i++)
            if (!AIInventory.Read(w, p, selected.SourceSlots[i], out var sourceItem) || !AIInventory.Stamp(sourceItem).Exact(AIInventory.Stamp(selected.SourceItems[i]))) return "선택한 종자 묶음의 슬롯 상태가 바뀜";
        if (selected.Mode != "plant" && PlayerController.ItemIsBroken(item.objectData)) return "선택한 농사 도구가 부서짐";
        float3 me = AIBody.Pos(w, p); var crops = CropsInView(w, me);
        foreach (var before in selected.Before)
        {
            if (!AIVision.CanSee(me, before.Tile)) return $"선택한 칸 ({before.Tile.x},{before.Tile.y})이 현재 안 보임";
            if (!SameTile(before, ReadTile(w, before.Tile, crops))) return $"선택한 칸 ({before.Tile.x},{before.Tile.y})의 소유/작물/땅 상태가 바뀜";
            if (selected.Mode == "fill" && !IsWater(new AITileReader(w), before.Tile)) return "선택한 물이 사라짐";
            if (selected.Mode == "hoe" && !NewPlotTiles(w, p, me, int.MaxValue).Contains(before.Tile)) return "선택한 땅 현재 조건이 바뀜";
        }
        return null;
    }
    public static string TileStale(World w, Entity p, Selection selection, int index)
    {
        if (selection == null || index < 0 || index >= selection.Before.Count) return null;
        var before = selection.Before[index]; var me = AIBody.Pos(w, p);
        if (!AIVision.CanSee(me, before.Tile)) return "선택한 작업 칸이 화면 밖으로 바뀜";
        return SameTile(before, ReadTile(w, before.Tile, CropsInView(w, me))) ? null : "선택한 칸의 소유/작물/땅이 작업 전에 바뀜";
    }
    public static AISkill Make(AICandidate candidate) => new FarmSkill(candidate.farm) { Key = candidate.key };
    public static List<AICandidate> Candidates(World w, Entity p, AIObservation o, string curKey)
    {
        var result = new List<AICandidate>(); if (AIBrain.Goal.kind != GoalKind.Farm) return result;
        var request = AILife.ForGoal(AIBrain.Goal);
        if (!string.IsNullOrEmpty(request.Mode)) { var c = ExactCandidate(w, p, o, curKey, request); if (c != null) result.Add(c); return result; }
        if (!string.IsNullOrEmpty(request.Item)) return result; // require explicit mode, never infer it from a seed
        foreach (string mode in new[] { "harvest", "plant", "water", "fill", "hoe" })
        {
            var q = request.Copy(); q.Mode = mode; q.Where = "farm";
            if (mode == "plant") foreach (var seed in SeedSlots(w, p)) { q.Item = ItemKey(seed.item); var c = ExactCandidate(w, p, o, curKey, q); if (c != null && !result.Exists(x => x.key == c.key)) result.Add(c); }
            else { var c = ExactCandidate(w, p, o, curKey, q); if (c != null) result.Add(c); }
        }
        var snap = Take(w, p, o.pos);
        if (snap.seedsBag == 0 && snap.seedsChest > 0 && (snap.empty.Count > 0 || snap.Want > 0))
            result.AddRange(AIBase.ItemTakeCandidates(w, p, o, curKey, id => IsSeed(w, id), 10, "내 밭에 심을 씨앗이 가방에 없음", true));
        if (!snap.SeedsAvail && (snap.empty.Count > 0 || true)) WildSeedOptions(w, p, o.pos, curKey, result);
        return result;
    }
    private static void Log(List<AICandidate> res, Snap s) => AIPlayerSpikeMod.Log($"FARM: {SnapText(s)}; options {res.Count}{(res.Count > 0 ? ": " + string.Join(" | ", res.ConvertAll(c => c.key)) : "")}");

    private static AICandidate Opt(string act, List<int2> tiles, ObjectID item, string desc) =>
        new AICandidate { kind = "farm", key = $"farm:{act}:{tiles[0].x},{tiles[0].y}", actionMode = act, tiles = tiles, tile = tiles[0], itemId = item, progress = true, desc = desc };

    private static List<int2> Nearest(List<int2> l, float3 me, int n)
    {
        var c = new List<int2>(l);
        c.Sort((a, b) => math.distance(me.xz, (float2)a).CompareTo(math.distance(me.xz, (float2)b)));
        if (c.Count > n) c.RemoveRange(n, c.Count - n);
        // visit in a short chain: nearest first, then each next nearest to the last
        var res = new List<int2>();
        float2 at = me.xz;
        while (c.Count > 0)
        {
            int bi = 0; float bd = float.MaxValue;
            for (int i = 0; i < c.Count; i++) { float d = math.distance(at, (float2)c[i]); if (d < bd) { bd = d; bi = i; } }
            res.Add(c[bi]); at = (float2)c[bi]; c.RemoveAt(bi);
        }
        return res;
    }

    private static int2 FieldCentre()
    {
        float2 sum = float2.zero; int n = 0;
        foreach (var p in S.plots.Values) { sum += (float2)p.tile; n++; }
        if (n == 0) return AIBase.BaseTile;
        float2 c = sum / n; int2 best = default; float bd = float.MaxValue;
        foreach (var p in S.plots.Values) { float d = math.distance(c, (float2)p.tile); if (d < bd) { bd = d; best = p.tile; } }
        return best;
    }

    // "walk to X" (a TunnelSkill along known tiles, as the base / chest options): to a known walkable tile next to `at`.
    private static bool GoOption(World server, Entity player, float3 me, int2 at, string what, string why, string curKey, out AICandidate c)
    {
        c = null;
        var tr = new AITileReader(server);
        var stands = new List<int2>();
        // not a tile under an object (seen now, or a remembered station/pot): the walk to "기지(밭 자리)" (-88,58) failed
        // twice with a workbench standing there (ai2 2026-11-04 F9)
        HashSet<int2> blocked;
        // a copy: BlockedInView returns the set cached for this frame and character (other callers read it)
        try { blocked = new HashSet<int2>(AIObjects.BlockedInView(server, player, out _)); } catch { blocked = new HashSet<int2>(); }
        foreach (var rs in AIMemory.RememberedStations(me)) blocked.Add(rs.tile);
        for (int r = 1; r <= 3 && stands.Count == 0; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (math.max(math.abs(x), math.abs(y)) != r) continue;
                    var t = at + new int2(x, y);
                    if ((AIVision.CanSee(me, t) || AIMemory.SeenTile(t)) && tr.Walkable(t) && !blocked.Contains(t)) stands.Add(t);
                }
        stands.Sort((a, b) => math.distance(me.xz, (float2)a).CompareTo(math.distance(me.xz, (float2)b)));
        var plan = AIApproach.FirstImmediate(server, player, stands, out int2 st, out string w);
        if (plan == null) { AIPlayerSpikeMod.Log($"FARM: no route to {what} ({at.x},{at.y}): {w}"); return false; }
        string k = $"dig:{st.x},{st.y}";
        if (k == curKey) return false;
        float d = math.distance(me.xz, (float2)at);
        c = AIApproach.WalkCandidate(st, what, true,
            $"{what}로 가기 — ({at.x},{at.y}) 여기서 {d:F0}칸. 까닭: {why}. 아는 길 {plan.path.Count}칸{(plan.walls.Count > 0 ? $", 부술 벽 {plan.walls.Count}개({plan.WallSummary()})" : ", 부술 벽 0개")}, 예상 약 {plan.EstimatedSeconds:F0}초. ({st.x},{st.y})에 서면 끝(밭 일은 따로).");
        return true;
    }

    // Fill the can at the known water nearest to the field: in view -> fill option; out of view -> walk there.
    private static void FillOption(World server, Entity player, float3 me, Snap s, string curKey, List<AICandidate> res)
    {
        if (curKey.StartsWith("farm:fill:")) return;
        if (S.water.Count == 0) { AIPlayerSpikeMod.Log("FARM: the water can needs water and no water tile is known"); return; }
        int2 field = s.plots > 0 ? FieldCentre() : AIBase.BaseTile;
        int2 best = default; float bd = float.MaxValue;
        foreach (var w in S.water.Keys) { float d = math.distance((float2)w, (float2)field) + 0.5f * math.distance((float2)w, me.xz); if (d < bd) { bd = d; best = w; } }
        float dm = math.distance(me.xz, (float2)best);
        if (AIVision.CanSee(me, best))
            res.Add(Opt("fill", new List<int2> { best }, s.can, $"물 채우기 — 물뿌리개 {s.can}(물 {s.water}/{s.waterMax})를 물 ({best.x},{best.y}) {dm:F0}칸에서 채우기(물 옆에 서서 한 번 누름). 까닭: 물 안 준 칸 {s.dry.Count}개에 줄 물이 모자람."));
        else if (GoOption(server, player, me, best, "물가", $"물뿌리개 물 {s.water}/{s.waterMax}, 기억한 물 ({best.x},{best.y})", curKey, out var go)) res.Add(go);
    }

    // Ground tiles to dig: in view, 3-9 tiles from base, top tile ground of a hoeable tileset, walkable, no object on it,
    // not a recently failed tile; next to the existing plots first (one compact field), else about 4 tiles from base.
    private static List<int2> NewPlotTiles(World server, Entity player, float3 me, int n)
    {
        var res = new List<int2>();
        var tr = new AITileReader(server);
        var blocked = AIObjects.BlockedInView(server, player, out _);
        int2 b = AIBody.TileOf(me);
        var half = AIVision.HalfExtents();
        int rx = (int)math.ceil(half.x), ry = (int)math.ceil(half.y);
        float now = Time.realtimeSinceStartup;
        var cand = new List<int2>();
        for (int x = -rx; x <= rx; x++)
            for (int y = -ry; y <= ry; y++)
            {
                var t = b + new int2(x, y);
                float db = math.distance((float2)t, (float2)b);
                if (!AIVision.CanSee(me, t) || S.plots.ContainsKey(t) || blocked.Contains(t)) continue;
                // Prior failed presses are observations, not a tile ban.
                var top = tr.Top(t);
                if (top.tileType != TileType.ground || !Hoeable(server, player, top.tileset)) continue;
                if (!HasStand(tr, t)) continue;
                cand.Add(t);
            }
        var field = new List<int2>(S.plots.Keys);
        for (int k = 0; k < n && cand.Count > 0; k++)
        {
            int bi = -1; float bs = float.MaxValue;
            for (int i = 0; i < cand.Count; i++)
            {
                var t = cand[i];
                float score;
                if (field.Count > 0)
                {
                    float near = float.MaxValue;
                    foreach (var f in field) near = math.min(near, math.distance((float2)t, (float2)f));
                    score = near * 10f + math.distance((float2)t, (float2)b);
                }
                else score = math.distance(me.xz, (float2)t);
                if (score < bs) { bs = score; bi = i; }
            }
            res.Add(cand[bi]); field.Add(cand[bi]); cand.RemoveAt(bi);
        }
        return res;
    }

    // With no seed anywhere: break ripe wild plants in view (natural ground, not someone's crop); they give their fruit
    // and may give seeds (learned drops tell after the first one).
    private static void WildSeedOptions(World server, Entity player, float3 me, string curKey, List<AICandidate> res)
    {
        var em = server.EntityManager;
        var l = new List<(float d, Crop c, float3 p)>();
        foreach (var kv in CropsInView(server, me))
        {
            var c = kv.Value;
            if (!c.ripe || S.plots.ContainsKey(kv.Key) || ForeignCrop(server, c.e)) continue;
            // The model can retry a previously undamaged target.
            float3 p = em.GetComponentData<LocalTransform>(c.e).Position;
            l.Add((math.distance(me.xz, p.xz), c, p));
        }
        l.Sort((a, b) => a.d.CompareTo(b.d));
        for (int i = 0; i < l.Count; i++)
        {
            var x = l[i];
            string k = "break:" + x.c.e.Index;
            if (k == curKey) continue;
            string learned = AIDrops.Known((int)x.c.id) ? AIDrops.Text((int)x.c.id) : "아직 부숴 본 적 없음(씨앗이 나올 수도 있음)";
            res.Add(new AICandidate { kind = "break", key = k, entity = x.c.e, tile = AIBody.TileOf(x.p), progress = true,
                desc = $"씨앗 얻으려 부수기 — 들에 난 다 자란 {x.c.id} ({x.p.x:F0},{x.p.z:F0}) {x.d:F1}칸. 부수면 {x.c.harvest} {x.c.n}개쯤; 나온 적 있는 것: {learned}. 까닭: 내 밭에 심을 씨앗이 가방·내 상자에 없음." });
        }
    }

    // ------------------------------------------------------------------ reports

    // One line for the action-selection input (farm goal only).
    public static string PromptLine(World server, Entity player, AIObservation o)
    {
        if (AIBrain.Goal.kind != GoalKind.Farm) return "";
        return $"밭: {SnapText(Take(server, player, o.pos))}. 기지 {AIBase.BaseText}. 규칙: {Rule}.";
    }

    // Upper-LLM report fragment (no leading comma).
    public static string ReportFields(World server, Entity player, AIObservation o, Func<string, string> esc)
    {
        var s = Take(server, player, o.pos);
        return $"\"farm\":{{\"plots\":{s.plots},\"in_view\":{s.inView},\"empty\":{s.empty.Count},\"growing\":{s.growing.Count},\"dry\":{s.dry.Count},\"ripe\":{s.ripe.Count},\"next_ripe_s\":{(s.nextRipe < 0f ? -1 : (int)math.round(s.nextRipe))}," +
               $"\"seeds\":\"{esc(s.seeds.Count == 0 ? "없음" : string.Join(", ", s.seeds.ConvertAll(x => $"{x.id} {x.count}")))}\",\"seeds_in_chests\":{s.seedsChest},\"hoe\":\"{s.hoe}\",\"water_can\":\"{(s.can == ObjectID.None ? "None" : $"{s.can} {s.water}/{s.waterMax}")}\",\"water_known\":{S.water.Count},\"note\":\"{esc(Rule)}\"}}";
    }

    // Whether a farm goal makes sense now (the upper LLM's answer check).
    public static string CanFarm(World server, Entity player)
    {
        var s = Take(server, player, AIBody.Pos(server, player));
        if (s.ripe.Count > 0 || s.dry.Count > 0 || s.SeedsAvail) return null;
        if (s.plots > 0 && s.inView < s.plots) return null;   // the field is out of view: its state is not known
        return "씨앗이 없고(가방·내 상자) 거둘 것·물 줄 것도 없음(farm 참고)";
    }

    // ------------------------------------------------------------------ /ai farm (test)

    public static void Command(World server, Entity player, string[] parts)
    {
        string a = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        float3 me = AIBody.Pos(server, player);
        if (a == "잊기" || a == "forget")
        {
            int n = S.plots.Count; S.plots.Clear(); S.growthLogged.Clear(); AIMemory.MarkDirty(); AIMemory.Save(true);
            AIPlayerSpikeMod.Log($"FARM: {n} plots forgotten (/ai farm forget, test)");
            AIPlayerSpikeMod.Reply($"{AIAgent.Cur.Name} 밭 기억 {n}칸을 지웠습니다.");
            return;
        }
        var s = Take(server, player, me);
        var tr = new AITileReader(server);
        var lines = new List<string>();
        foreach (var p in S.plots.Values)
        {
            if (!AIVision.CanSee(me, p.tile)) { lines.Add($"({p.tile.x},{p.tile.y}) 화면 밖"); continue; }
            bool wtr = IsWatered(tr, p.tile);
            if (!s.crops.TryGetValue(p.tile, out var c)) { lines.Add($"({p.tile.x},{p.tile.y}) 빈 칸{(wtr ? "(물 줌)" : "")}"); continue; }
            float left = SecondsLeft(c, wtr);
            lines.Add($"({p.tile.x},{p.tile.y}) {c.id} {(c.ripe ? "다 자람" : $"단계 {c.stage}/{c.stages} 단계시간 {c.stageTime:F0}s 타이머 {c.timer:F0}s {(wtr ? "물 줌" : "마름")}{(c.next != ObjectID.None ? $", 다음 {c.next} {c.later:F0}s" : "")}{(left >= 0 ? $" 다 자랄 때까지 약 {left:F0}s 남음" : "")}")}");
        }
        string txt = $"{AIAgent.Cur.Name} {SnapText(s)} · 아는 물 [{string.Join(" ", new List<int2>(S.water.Keys).ConvertAll(w => $"({w.x},{w.y})"))}] · {string.Join(" / ", lines)}";
        AIPlayerSpikeMod.Log("FARM: " + txt);
        AIPlayerSpikeMod.Reply(txt);
    }
}

// Farm work on a list of tiles, one kind per action (FarmSkill): hoe new plots, plant a seed, water, fill the can (one
// water tile), harvest ripe crops. For each tile: walk next to it when not within reach, hold the tool / seed in a
// hotbar slot, aim at the tile (ClientInput.wasAiming + mouseOrJoystickWorldPoint) and press the secondary use briefly
// (one game action per press: hoe cooldown 0.4 s, can 0.35 s), then watch the tile / bag for the result (up to 4
// presses). A held hoe would flatten the plot it just dug, so it is never held.
public class FarmSkill : AISkill, ILifeResult
{
    private readonly string _act;
    private readonly List<int2> _tiles;
    private readonly ObjectID _item;
    private readonly AINavigator _nav = new AINavigator();
    private readonly AIFarm.Selection _selection;
    private int _boundSlot = -1;
    private readonly List<int> _sourceSlots = new List<int>();
    private int _sourceIndex, _sourceUsed;
    private Dictionary<string, int> _mainBefore;
    public int ConfirmedWork => _done;
    public int RequestedWork => _selection?.Offered ?? _tiles.Count;
    public int ConfirmedReceived { get; private set; }
    public string ResultStage => _act == "harvest" ? "harvested_tiles_and_main_inventory" : "changed_tiles";
    private bool _navOn;
    private int _i, _slot = -1, _done, _presses;
    private float _tileAt, _pressAt = -1f;
    private Dictionary<ObjectID, int> _invBefore;
    private readonly List<string> _fails = new List<string>();
    private const float Reach = 1.2f, Press = 0.12f, Wait = 0.7f;

    public FarmSkill(string act, List<int2> tiles, ObjectID item)
    {
        _act = act; _tiles = tiles == null ? new List<int2>() : new List<int2>(tiles); _item = item;
        Name = act == "hoe" ? "밭 일구기" : act == "plant" ? "씨앗 심기" : act == "water" ? "물 주기" : act == "fill" ? "물 채우기" : act == "harvest" ? "거두기" : "밭 일";
        Key = _tiles.Count > 0 ? $"farm:{act}:{_tiles[0].x},{_tiles[0].y}" : $"farm:{act}";
    }

    public FarmSkill(AIFarm.Selection selection) : this(selection?.Mode ?? "", selection?.Tiles, selection == null ? ObjectID.None : selection.Item.objectID)
    {
        _selection = selection; _boundSlot = selection?.SourceSlot ?? -1; Key = selection?.Key ?? Key;
        if (selection != null) _sourceSlots.AddRange(selection.SourceSlots);
    }
    private static Dictionary<string, int> MainCounts(World w, Entity p)
    {
        var counts = new Dictionary<string, int>();
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
            if (AIInventory.Read(w, p, i, out var item) && item.objectID != ObjectID.None)
            { string key = item.objectID + "#v" + item.variation; counts.TryGetValue(key, out int n); counts[key] = n + AIInventory.Quantity(item); }
        return counts;
    }
    private void Received(World w, Entity p)
    {
        if (_act != "harvest" || _mainBefore == null) return;
        int n = 0;
        foreach (var pair in MainCounts(w, p))
        {
            bool produce = _selection == null;
            if (_selection != null) foreach (var tile in _selection.Before) if (tile.HarvestId != ObjectID.None && pair.Key.StartsWith(tile.HarvestId + "#v", StringComparison.Ordinal)) { produce = true; break; }
            if (!produce) continue;
            _mainBefore.TryGetValue(pair.Key, out int before); n += math.max(0, pair.Value - before);
        }
        ConfirmedReceived = n;
    }
    public override bool Start(World server, Entity player, float now)
    {
        if (_tiles.Count == 0) { Status = SkillStatus.Failed; Reason = "대상 칸이 없음"; return false; }
        if (_act == "harvest") foreach (var tile in _tiles)
        {
            string limit = AIGathering.HarvestLimit(server, player, tile, "FarmSkill.Start harvest");
            if (limit != null) { ObserveOnce("explicit-max", limit); Status = SkillStatus.Failed; Reason = limit; return false; }
        }
        if (_selection != null)
        {
            string stale = AIFarm.SelectionStale(server, player, _selection);
            if (stale != null) { Status = SkillStatus.Failed; Reason = stale; return false; }
        }
        _mainBefore = MainCounts(server, player);
        ObjectID hold = HeldItem(server, player);
        if (hold == ObjectID.None) { Status = SkillStatus.Failed; Reason = _act == "plant" ? $"가방에 {_item}이(가) 없음" : _act == "hoe" || _act == "harvest" ? "괭이가 없음" : "물뿌리개가 없음"; return false; }
        _invBefore = AIBody.InventoryCounts(server, player);
        Facts.Add($"{_act} tiles [{string.Join(" ", _tiles.ConvertAll(t => $"({t.x},{t.y})"))}] with {hold}");
        _tileAt = now;
        return true;
    }

    private ObjectID HeldItem(World server, Entity player)
    {
        if (_selection != null)
        {
            if (_act == "plant" && _sourceSlots.Count > 0)
            {
                while (_sourceIndex < _sourceSlots.Count)
                {
                    var before = _selection.SourceItems[_sourceIndex];
                    _boundSlot = _sourceSlots[_sourceIndex];
                    if (!AIInventory.Read(server, player, _boundSlot, out var seed)) return ObjectID.None;
                    int remaining = before.amount - _sourceUsed;
                    if (remaining == 0)
                    {
                        if (seed.objectID != ObjectID.None && (!AIInventory.Identity(seed, before) || seed.amount != 0)) return ObjectID.None;
                        _sourceIndex++; _sourceUsed = 0; continue;
                    }
                    return AIInventory.Identity(seed, before) && seed.amount == remaining ? seed.objectID : ObjectID.None;
                }
                return ObjectID.None;
            }
            if (!AIInventory.Read(server, player, _boundSlot, out var item) || !AIInventory.Identity(item, _selection.Item)) return ObjectID.None;
            if (_act == "plant") return item.amount > 0 ? item.objectID : ObjectID.None;
            return !PlayerController.ItemIsBroken(item.objectData) ? item.objectID : ObjectID.None;
        }
        if (_act == "plant") { AIBody.InventoryCounts(server, player).TryGetValue(_item, out int c); return c > 0 ? _item : ObjectID.None; }
        if (_act == "hoe" || _act == "harvest") return AIFarm.Hoe(server, player);
        return AIFarm.WaterCan(server, player, out var id, out _, out _, out _) ? id : ObjectID.None;
    }

    // The target needs nothing (already dug / planted / watered / harvested / can full).
    private bool Satisfied(World server, Entity player, int2 t)
    {
        var tr = new AITileReader(server);
        switch (_act)
        {
            case "hoe": return AIFarm.IsDug(tr, t);
            case "plant": return AIFarm.CropsInView(server, AIBody.Pos(server, player)).ContainsKey(t);
            case "water": return AIFarm.IsWatered(tr, t);
            case "fill":
                if (_selection != null) return AIInventory.Read(server, player, _boundSlot, out var selected) && AIInventory.Identity(selected, _selection.Item) && selected.amount > _selection.Item.amount;
                return AIFarm.WaterCan(server, player, out _, out int w, out int mx, out _) && mx > 0 && w >= mx;
            case "harvest":
                var cr = AIFarm.CropsInView(server, AIBody.Pos(server, player));
                return !cr.TryGetValue(t, out var c) || !c.ripe;
        }
        return true;
    }

    // Result of the last press on this target (for the summary and memory).
    private void Note(World server, Entity player, int2 t)
    {
        if (_act == "hoe") AIFarm.NotePlot(t);
        if (_act == "harvest") AIFarm.NoteHarvest(1);
        if (_act == "plant" && _selection != null) _sourceUsed++;
        _done++;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        Received(server, player);
        if (_i >= _tiles.Count) return Finish(_done > 0 ? SkillStatus.Done : SkillStatus.Failed, Summary(server, player));
        int2 t = _tiles[_i];
        if (_act == "harvest")
        {
            string limit = AIGathering.HarvestLimit(server, player, t, "FarmSkill.Tick harvest 입력 전");
            if (limit != null) { ObserveOnce("explicit-max", limit); return Finish(SkillStatus.Failed, Summary(server, player, limit)); }
        }
        // a press is out: wait for the result before anything else
        if (_pressAt >= 0f)
        {
            if (Satisfied(server, player, t))
            {
                Note(server, player, t);
                AIPlayerSpikeMod.Log($"SKILL {Name}: ({t.x},{t.y}) done after {_presses} press(es), {now - _tileAt:F1}s");
                return NextTile(now);
            }
            if (now - _pressAt < Press) return Aimed(me, t, true);
            if (now - _pressAt < Wait) return Aimed(me, t, false);
            if (_presses >= 4)
                ObserveOnce("farm-no-change:" + t, $"({t.x},{t.y}) {_presses}번 눌렀으나 상태 변화 확인 안 됨");
            _pressAt = -1f;   // press again
        }
        if (_presses == 0 && _selection != null)
        {
            string stale = AIFarm.TileStale(server, player, _selection, _i);
            if (stale != null) return Finish(_done > 0 ? SkillStatus.Done : SkillStatus.Failed, Summary(server, player, stale));
        }
        if (_presses == 0 && Satisfied(server, player, t)) { AIPlayerSpikeMod.Log($"SKILL {Name}: ({t.x},{t.y}) needs nothing (already)"); return NextTile(now); }
        // within reach of the tile (the game's aim box is 1.25 tiles around the player for a 1x1 tool)
        float2 off = math.abs((float2)t - me.xz);
        if (math.cmax(off) > Reach)
        {
            if (!_navOn)
            {
                if (!AIFarm.StandFor(server, player, new AITileReader(server), me, t, out int2 stand)) { _fails.Add($"({t.x},{t.y}) 옆에 설 칸 없음"); return NextTile(now); }
                _nav.Start(server, player, stand, 0.45f, now);
                _navOn = true;
            }
            var st = _nav.Tick(server, player, now, out float2 move);
            if (st == NavStatus.Failed) { _nav.Dispose(server); _navOn = false; _fails.Add($"({t.x},{t.y})로 가지 못함: {_nav.FailReason}"); return NextTile(now); }
            if (st == NavStatus.Moving) return new BodyInput { move = move, slot = -1 };
            _nav.Dispose(server); _navOn = false;
            me = AIBody.Pos(server, player);
            if (math.cmax(math.abs((float2)t - me.xz)) > Reach + 0.3f) { _fails.Add($"({t.x},{t.y}) 옆에 닿지 못함"); return NextTile(now); }
        }
        else if (_navOn) { _nav.Dispose(server); _navOn = false; }
        // tool / seed in a hotbar slot (re-checked per tile: a swap may have moved it)
        ObjectID hold = HeldItem(server, player);
        if (hold == ObjectID.None) return Finish(_done > 0 ? SkillStatus.Done : SkillStatus.Failed, Summary(server, player, _act == "plant" ? "씨앗이 떨어짐" : "도구가 없어짐"));
        string swap = "";
        int slot = _selection == null ? AICraft.HotbarSlotFor(server, player, hold, out swap) : SelectedHotbar(server, player, out swap);
        if (!string.IsNullOrEmpty(swap)) AIPlayerSpikeMod.Log($"SKILL {Name}: {swap}");
        if (slot < 0) return Finish(SkillStatus.Failed, $"{hold}을(를) 손 칸에 놓을 수 없음");
        if (slot != _slot) { _slot = slot; _tileAt = now; return new BodyInput { slot = _slot, face = AIBody.DirTo(me, (float2)t) }; }   // select first
        if (now - _tileAt < 0.25f) return Aimed(me, t, false);
        _presses++;
        _pressAt = now;
        return Aimed(me, t, true);
    }

    // Same hotbar preparation as AICraft, bound to the observed source instead of another item of the same id.
    private int SelectedHotbar(World w, Entity p, out string swap)
    {
        swap = "";
        if (_boundSlot >= 0 && _boundSlot < 10) return _boundSlot;
        var buffer = w.EntityManager.GetBuffer<ContainedObjectsBuffer>(p);
        int hot = -1;
        for (int i = 0; i < 10 && i < buffer.Length; i++) if (buffer[i].objectID == ObjectID.None) { hot = i; break; }
        if (hot < 0) for (int i = math.min(9, buffer.Length - 1); i >= 0; i--)
        {
            var info = PugDatabase.GetObjectInfo(buffer[i].objectID, buffer[i].variation);
            if (info == null || info.objectType != ObjectType.MiningPick && info.objectType != ObjectType.MeleeWeapon) { hot = i; break; }
        }
        if (hot < 0) return -1;
        var held = buffer[hot]; buffer[hot] = buffer[_boundSlot]; buffer[_boundSlot] = held;
        for (int i = 0; i < _sourceSlots.Count; i++)
            if (i != _sourceIndex && _sourceSlots[i] == hot) _sourceSlots[i] = _boundSlot;
        if (_sourceIndex < _sourceSlots.Count) _sourceSlots[_sourceIndex] = hot;
        swap = $"선택한 종자/도구 슬롯 {_boundSlot} → 손 슬롯 {hot}"; _boundSlot = hot;
        return hot;
    }
    private BodyInput NextTile(float now)
    {
        _i++; _presses = 0; _pressAt = -1f; _tileAt = now;
        return new BodyInput { slot = _slot };   // release between targets
    }

    private BodyInput Aimed(float3 me, int2 t, bool press) =>
        new BodyInput { slot = _slot, face = AIBody.DirTo(me, (float2)t), aim = true, hasPoint = true, point = (float2)t, secondary = press };

    private string Summary(World server, Entity player, string why = "")
    {
        string w = "";
        if (AIFarm.WaterCan(server, player, out var cid, out int water, out int mx, out _)) w = $", 물뿌리개 {water}/{mx}";
        Received(server, player);
        return $"{Name} 요청 {_selection?.Requested ?? _tiles.Count}칸 / 이번 시도 {_tiles.Count}칸 / 확인된 작업 {_done}칸 / main inventory 확보 {ConfirmedReceived}개 / 미충족 {math.max(0, (_selection?.Requested ?? _tiles.Count) - _done)}칸{(why != "" ? " — " + why : "")}{(_fails.Count > 0 ? " · 안 된 칸: " + string.Join("; ", _fails) : "")} · 가방 {AIBody.InventoryDiff(_invBefore ?? new Dictionary<ObjectID, int>(), AIBody.InventoryCounts(server, player))}{w}";
    }

    public override string Progress(World server, Entity player) => $"{_i}/{_tiles.Count}칸{(_navOn ? ", 다음 칸으로 가는 중" : "")}";
    public override float EtaSeconds(World server, Entity player) => (_tiles.Count - _i) * 1.2f;
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
