// AIBase — the AI's base and chests: where home is, which chests are there and what is in them, storing the bag into a
// chest, taking crafting materials out of one. Report: work/2026-11/2026-11-04-생활-기지/1-기지-상자.md (session
// 코어키퍼-AI-캐릭터).
//
// How the game moves items between a player and a chest (decompile C:\Dev\_ref\corekeeper-decomp):
// - A chest is an entity with an inventory (InventoryBuffer + ContainedObjectsBuffer) and InventoryAutoTransferEnabledCD
//   (the marker the game's "nearby chests" search looks for: InventoryUtility.GetNearbyChestsEntityWithDistanceSorted).
// - With a chest open, a client's shift-click is InventoryHandler.MoveAllToOrDrop (InventoryHandler.cs:330-339):
//   UIInputAction.InventoryChange with Create.MoveOrDropItem(from, slot, to, hint -1, to.start + to.size, pos). The server
//   (TriggerUIActionsSystem -> InventoryChangeBuffer -> InventoryUpdateSystem.ProcessInventoryChange :796-880) checks only
//   that both sides are live inventories, then InventoryUtility.MoveOrDrop: moves, or drops at pos when it cannot.
//   Distance is not checked by the server; the client can only do it while the chest UI is open (next to the chest).
// - The mod queues the same UIActionBuffer entry for the AI (AICraft.QueueInventoryChange), after walking next to the
//   chest, one slot per ~0.1 s like clicks, and only into a chest that has room (no drops).
// - Crafting already takes materials from chests within 10 tiles of the station (InventoryUpdateSystem :425-431,
//   InventoryUtility.GetNearbyChestsForCraftingByDistance -> 10 f / 20 chests, :3796-3799; the client UI counts the same
//   chests, CraftingHandler.GetNearbyChests :453-478). The mod's own check still counts only the bag, so a material that
//   is in a chest is fetched with "take" (as a player who opens the chest and takes it).
//
// Base: a tile the host sets (/ai base 여기 = the host's tile), else the core (0,0), where every player respawns. "At
// base" = within BaseRadius (the game's own nearby-chest radius). Per character, kept in memory.json (AIMemory).
// What is inside a chest is known only from when this character last had it open (store / take), like a person.
// Own chests only (user decision 2026-11-04): a character remembers and uses only the chests it placed itself
// (PlaceSkill marks the new chest). Chests of the host or of other AI characters are never stored into or taken from,
// even at base. An own chest that is gone from its tile (broken, picked up) is forgotten.
using System;
using System.Collections.Generic;
using System.Text;
using Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIBase
{
    public const float BaseRadius = 10f;    // the game's nearby-chest radius (crafting, quick stack)
    public const float HomeArrive = 4f;     // "back at base" for the home goal
    public const float ChestReach = 2.0f;   // stand this close to use a chest (as AICraft.StationRange)

    // ---- per-character state (multi-AI): AIAgent.Cur's Slots[12]
    public sealed class ChestMem
    {
        public ObjectID id; public int2 tile; public long seenUnix; public bool placed; public int goneSeen;
        public long openedUnix;   // 0 = never opened: contents unknown
        public int slots, free;
        public Dictionary<ObjectID, int> items = new Dictionary<ObjectID, int>();
    }
    private sealed class State
    {
        public bool baseSet; public int2 baseTile; public string baseBy = ""; public long baseUnix;
        public readonly Dictionary<int2, ChestMem> chests = new Dictionary<int2, ChestMem>();
        public readonly List<(ObjectID id, int n)> missing = new List<(ObjectID, int)>();
        public ObjectID chestItem = ObjectID.None; public bool chestItemLooked;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Base);
    private static State S => StateOf(AIAgent.Cur);

    public static int2 BaseTile => S.baseSet ? S.baseTile : int2.zero;
    public static string BaseText => S.baseSet ? $"({S.baseTile.x},{S.baseTile.y}), {S.baseBy}가 정함" : "(0,0) 코어(정하지 않아 기본값)";
    public static float BaseDist(float3 pos) => math.distance(pos.xz, (float2)BaseTile);
    public static bool AtBase(float3 pos) => BaseDist(pos) <= BaseRadius;

    // ------------------------------------------------------------------ memory (memory.json via AIMemory)

    public static void ClearMemory() { var s = S; s.baseSet = false; s.baseTile = int2.zero; s.baseBy = ""; s.chests.Clear(); }

    public static void Import(AIMemory.Data d)
    {
        var s = S;
        if (d.baseXY != null && d.baseXY.Length == 2 && d.baseSet != 0) { s.baseSet = true; s.baseTile = new int2(d.baseXY[0], d.baseXY[1]); s.baseBy = d.baseBy ?? ""; s.baseUnix = d.baseSeen; }
        int n = d.chestId == null ? 0 : d.chestId.Length;
        var list = new List<ChestMem>();   // file order = the index used by chestItems
        for (int i = 0; i < n; i++)
        {
            if (d.chestXY == null || 2 * i + 1 >= d.chestXY.Length) break;
            var c = new ChestMem { id = (ObjectID)d.chestId[i], tile = new int2(d.chestXY[2 * i], d.chestXY[2 * i + 1]) };
            if (d.chestSeen != null && i < d.chestSeen.Length) c.seenUnix = d.chestSeen[i];
            if (d.chestPlaced != null && i < d.chestPlaced.Length) c.placed = d.chestPlaced[i] != 0;
            if (d.chestOpened != null && i < d.chestOpened.Length) c.openedUnix = d.chestOpened[i];
            if (d.chestSlots != null && 2 * i + 1 < d.chestSlots.Length) { c.slots = d.chestSlots[2 * i]; c.free = d.chestSlots[2 * i + 1]; }
            list.Add(c);   // keeps the file index for chestItems
            if (c.placed) s.chests[c.tile] = c;   // own chests only (a chest only seen is not this character's)
        }
        // contents: flat [chest index, item, amount]
        for (int i = 0; d.chestItems != null && i + 2 < d.chestItems.Length; i += 3)
        {
            int ci = d.chestItems[i];
            if (ci < 0 || ci >= list.Count) continue;
            list[ci].items[(ObjectID)d.chestItems[i + 1]] = d.chestItems[i + 2];
        }
    }

    public static void Export(AIMemory.Data d)
    {
        var s = S;
        d.baseSet = s.baseSet ? 1 : 0; d.baseXY = new[] { s.baseTile.x, s.baseTile.y }; d.baseBy = s.baseBy; d.baseSeen = s.baseUnix;
        var id = new List<int>(); var xy = new List<int>(); var sn = new List<long>(); var pl = new List<int>(); var op = new List<long>(); var sl = new List<int>(); var it = new List<int>();
        int k = 0;
        foreach (var c in s.chests.Values)
        {
            id.Add((int)c.id); xy.Add(c.tile.x); xy.Add(c.tile.y); sn.Add(c.seenUnix); pl.Add(c.placed ? 1 : 0); op.Add(c.openedUnix); sl.Add(c.slots); sl.Add(c.free);
            foreach (var kv in c.items) { it.Add(k); it.Add((int)kv.Key); it.Add(kv.Value); }
            k++;
        }
        d.chestId = id.ToArray(); d.chestXY = xy.ToArray(); d.chestSeen = sn.ToArray(); d.chestPlaced = pl.ToArray(); d.chestOpened = op.ToArray(); d.chestSlots = sl.ToArray(); d.chestItems = it.ToArray();
    }

    // Called from AIMemory.Observe (about once per second): an own chest whose tile is in view is "seen" while the same
    // kind of chest stands there; when its tile is in view without it (or with another kind) on two observations in a
    // row it is forgotten (as stations). Other chests are not remembered (own chests only).
    public static void Observe(World server, Entity player, float3 pos, long unix)
    {
        var s = S;
        if (s.chests.Count == 0) return;
        var inView = new Dictionary<int2, ChestView>();
        foreach (var c in ChestsInView(server, player)) inView[AIBody.TileOf(c.pos)] = c;
        foreach (var t in new List<int2>(s.chests.Keys))
        {
            var m = s.chests[t];
            if (inView.TryGetValue(t, out var v) && v.id == m.id)
            {
                if (unix - m.seenUnix >= 30) AIMemory.MarkDirty();
                m.seenUnix = unix; m.goneSeen = 0;
                continue;
            }
            if (!AIVision.CanSee(pos, t)) continue;
            if (++m.goneSeen >= 2) Forget(t, "seen gone from its tile (broken or picked up)");
        }
    }

    public static void Forget(int2 t, string why)
    {
        if (!S.chests.TryGetValue(t, out var m)) return;
        S.chests.Remove(t);
        AIMemory.MarkDirty();
        AIPlayerSpikeMod.Log($"BASE: own chest {m.id} at ({t.x},{t.y}) {why}; forgotten");
    }

    // An own chest (placed by this character) at this chest's tile, of the same kind; null for any other chest.
    public static ChestMem Own(ChestView v) => v != null && S.chests.TryGetValue(AIBody.TileOf(v.pos), out var m) && m.id == v.id ? m : null;

    public static float BaseDistTile(int2 t) => math.distance((float2)t, (float2)BaseTile);

    // The AI had the chest open (standing next to it in StoreSkill / TakeSkill), or just placed it (placed = true, the
    // only way a chest becomes this character's): what is inside now. Null for a chest that is not its own.
    public static ChestMem NoteOpened(World server, Entity chest, bool placed = false)
    {
        var em = server.EntityManager;
        if (!em.Exists(chest)) return null;
        var v = View(server, chest, float3.zero);
        if (v == null) return null;
        var t = AIBody.TileOf(v.pos);
        if (!S.chests.TryGetValue(t, out var m) || m.id != v.id)
        {
            if (!placed) return null;
            m = new ChestMem { id = v.id, tile = t };
            S.chests[t] = m;
            AIPlayerSpikeMod.Log($"BASE: own chest {v.id} placed at ({t.x},{t.y}), {BaseDistTile(t):F0} tiles from base; remembered as {AIAgent.Cur.Name}'s");
        }
        m.placed = true;
        m.openedUnix = m.seenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        m.slots = v.slots; m.free = v.free; m.items = v.items;
        AIMemory.MarkDirty();
        return m;
    }

    // ------------------------------------------------------------------ chests in view

    public class ChestView { public Entity entity; public ObjectID id; public float3 pos; public float dist; public int start, slots, free; public Dictionary<ObjectID, int> items = new Dictionary<ObjectID, int>(); }

    private static ChestView View(World server, Entity e, float3 me)
    {
        var em = server.EntityManager;
        if (!em.HasComponent<InventoryAutoTransferEnabledCD>(e) || !em.HasBuffer<ContainedObjectsBuffer>(e) || !em.HasBuffer<InventoryBuffer>(e) || !em.HasComponent<LocalTransform>(e)) return null;
        if (em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e)) return null;
        if (em.HasBuffer<CanCraftObjectsBuffer>(e)) return null;   // a crafting station with an inventory (e.g. a cooking pot, AICook) is not a chest
        if (em.HasComponent<EntityDestroyedCD>(e) && em.IsComponentEnabled<EntityDestroyedCD>(e)) return null;
        var v = new ChestView { entity = e, id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None };
        v.pos = em.GetComponentData<LocalTransform>(e).Position;
        v.dist = math.distance(me.xz, v.pos.xz);
        var inv = em.GetBuffer<InventoryBuffer>(e, true);
        var buf = em.GetBuffer<ContainedObjectsBuffer>(e, true);
        if (inv.Length == 0) return null;
        v.start = inv[0].startIndex; v.slots = inv[0].size;
        if (inv[0].cantAddObjectsToInventory) return null;
        for (int i = v.start; i < v.start + v.slots && i < buf.Length; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None) { v.free++; continue; }
            ObjectInfo info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            int n = info != null && !info.isStackable ? 1 : buf[i].amount;
            v.items.TryGetValue(id, out int c); v.items[id] = c + n;
        }
        return v;
    }

    public static List<ChestView> ChestsInView(World server, Entity player)
    {
        var res = new List<ChestView>();
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<InventoryAutoTransferEnabledCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var v = View(server, e, me);
                if (v != null) res.Add(v);
            }
        }
        res.Sort((a, b) => a.dist.CompareTo(b.dist));
        return res;
    }

    // ------------------------------------------------------------------ items

    // Whether the item's prefab has a component or buffer type (game data, not a hard-coded ID list).
    private static bool PrefabHas(World server, ObjectID id, ComponentType t)
    {
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(id, bank, 0);
                if (prefab == Entity.Null) return false;
                return em.HasComponent(prefab, t);
            }
        }
        catch { return false; }
    }

    public static bool IsChestItem(World server, ObjectID id) => id != ObjectID.None && PrefabHas(server, id, ComponentType.ReadOnly<InventoryAutoTransferEnabledCD>()) && !IsStationItem(server, id);
    private static bool IsStationItem(World server, ObjectID id) => PrefabHas(server, id, ComponentType.ReadOnly<CanCraftObjectsBuffer>());
    // bombs (SmallBomb, Bomb, ...) are placed to explode: kept in the bag like weapons (2026-11-04 실기: SmallBomb was stored)
    private static bool IsExplosiveItem(World server, ObjectID id) => PrefabHas(server, id, ComponentType.ReadOnly<IsExplosiveCD>());

    // The chest the AI can make: a hand recipe or a basic-workbench recipe whose prefab is a chest (fewest materials).
    // Found from game data at run time (not a hard-coded ID).
    public static ObjectID ChestItem(World server, Entity player)
    {
        var s = S;
        if (s.chestItemLooked) return s.chestItem;
        s.chestItemLooked = true;
        var cands = new List<ObjectID>(AICraft.HandRecipes(server, player));
        foreach (var r in AICraft.PrefabRecipes(server, ObjectID.WoodenWorkBench)) if (!cands.Contains(r)) cands.Add(r);
        int best = int.MaxValue;
        foreach (var id in cands)
        {
            if (!IsChestItem(server, id)) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(id);
            int cost = 0; if (info?.requiredObjectsToCraft != null) foreach (var r in info.requiredObjectsToCraft) cost += r.amount;
            if (cost < best) { best = cost; s.chestItem = id; }
        }
        AIPlayerSpikeMod.Log($"BASE: chest the AI can make = {s.chestItem} (materials {(s.chestItem == ObjectID.None ? "-" : AICraft.MaterialsText(AICraft.Materials(server, player, s.chestItem, 1)))}), from {cands.Count} hand/workbench recipes");
        return s.chestItem;
    }

    // Kinds that go into a chest: materials (ores, bars, wood, parts, valuables) and placeable blocks; not tools, weapons,
    // armour, food or healing items, crafting stations, chests, torches or bombs (what the AI uses while out).
    // PlayerGrave (묘비석) is the decoration a player gets back from a grave: an ordinary placeable, stored.
    public static bool StorableKind(World server, ObjectID id, int variation)
    {
        ObjectInfo info = PugDatabase.GetObjectInfo(id, variation);
        if (info == null) return false;
        switch (info.objectType)
        {
            case ObjectType.NonUsable: case ObjectType.Valuable: case ObjectType.UniqueCraftingComponent: break;
            case ObjectType.PlaceablePrefab:
                if (id.ToString().Contains("Torch") || IsStationItem(server, id) || IsChestItem(server, id) || IsExplosiveItem(server, id)) return false;
                if (AIFarm.IsSeed(server, id)) return false;   // seeds stay in the bag for the AI's own field (먹거리 자급 2)
                break;
            default: return false;
        }
        return string.IsNullOrEmpty(AIItems.HealInfo(server, id, variation));
    }

    // Bag slots (hotbar included, the cursor slot not) holding storable items; filter None = every storable kind.
    public static List<(int slot, ObjectID id, int amount)> Storable(World server, Entity player, ObjectID filter)
    {
        var res = new List<(int, ObjectID, int)>();
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return res;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = AIBody.MainEnd(em, player, buf.Length);
        for (int i = 0; i < end; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None || (filter != ObjectID.None && id != filter)) continue;
            // food stays in the bag unless the store goal names it (보관 Mushroom: put that food away)
            if (filter == ObjectID.None && !StorableKind(server, id, buf[i].variation)) continue;
            var info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            if (info == null) continue;
            res.Add((i, id, info.isStackable ? buf[i].amount : 1));
        }
        return res;
    }

    public static string StorableText(List<(int slot, ObjectID id, int amount)> l)
    {
        var d = new Dictionary<ObjectID, int>();
        foreach (var x in l) { d.TryGetValue(x.id, out int c); d[x.id] = c + x.amount; }
        var parts = new List<string>(); foreach (var kv in d) parts.Add($"{kv.Key} {kv.Value}");
        return parts.Count == 0 ? "없음" : string.Join(", ", parts);
    }

    private static bool MayStackStored(World server, Entity player, ChestMem memory, (int slot, ObjectID id, int amount) item)
    {
        if (!memory.items.ContainsKey(item.id)) return false;
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var bag = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return item.slot >= 0 && item.slot < bag.Length && PugDatabase.GetObjectInfo(item.id, bag[item.slot].variation)?.isStackable == true;
    }

    // In-view, previously opened chests use the same actual slot limits as the selected movement. Historical,
    // off-screen contents remain a reason to go and check, not a claim that the current chest has room.
    private static bool MayStoreKnownChest(World server, Entity player, ChestMem memory,
        List<(int slot, ObjectID id, int amount)> bag, List<ChestView> inView)
    {
        var visible = inView.Find(c => c.id == memory.id && math.all(AIBody.TileOf(c.pos) == memory.tile));
        if (visible == null || memory.openedUnix == 0)
            return memory.openedUnix == 0 || memory.free > 0 || bag.Exists(x => MayStackStored(server, player, memory, x));
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var actual = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return bag.Exists(x => x.slot >= 0 && x.slot < actual.Length && actual[x.slot].objectID == x.id &&
            AIInventory.HasStoreRoom(server, visible.entity, actual[x.slot]));
    }

    private static string ItemsText(Dictionary<ObjectID, int> d, int max = 8)
    {
        var parts = new List<string>(); foreach (var kv in d) if (parts.Count < max) parts.Add($"{kv.Key} {kv.Value}");
        return parts.Count == 0 ? "비어 있음" : string.Join(", ", parts) + (d.Count > max ? " 외" : "");
    }

    public static string ChestText(ChestMem c, float3 pos) =>
        $"{c.id} ({c.tile.x},{c.tile.y}) {math.distance(pos.xz, (float2)c.tile):F0}칸{(c.placed ? ", " + AIAgent.Cur.Name + "가 놓음" : "")}: " +
        (c.openedUnix == 0 ? "안을 아직 못 봄" : $"{AIMemory.Ago(c.openedUnix)}에 열었을 때 빈 칸 {c.free}/{c.slots}, 든 것 {ItemsText(c.items)}");

    public static List<ChestMem> OwnChests() => new List<ChestMem>(S.chests.Values);

    public static List<ChestMem> BaseChests()
    {
        var l = new List<ChestMem>();
        foreach (var c in S.chests.Values) if (c.placed && BaseDistTile(c.tile) <= BaseRadius) l.Add(c);   // own chests at base
        return l;
    }

    // ------------------------------------------------------------------ crafting materials (from AIBrain.WantedItems)

    public static void ResetMissing() => S.missing.Clear();
    public static void NoteMissing(ObjectID id, int n)
    {
        if (n <= 0) return;
        var m = S.missing;
        int i = m.FindIndex(x => x.id == id);
        if (i < 0) m.Add((id, n)); else m[i] = (id, math.max(m[i].n, n));
    }

    // For the preparation path: the chest to make when the store goal has nowhere to put things (no base chest that
    // may have room, none in the bag). None otherwise.
    public static ObjectID ChestTarget(World server, Entity player)
    {
        if (AIBrain.Goal.kind != GoalKind.Store) return ObjectID.None;
        if (Storable(server, player, AIBrain.Goal.item).Count == 0) return ObjectID.None;
        if (BaseChests().Exists(c => c.openedUnix == 0 || c.free > 0)) return ObjectID.None;
        ObjectID ci = ChestItem(server, player);
        if (ci == ObjectID.None) return ObjectID.None;
        AIBody.InventoryCounts(server, player).TryGetValue(ci, out int have);
        return have > 0 ? ObjectID.None : ci;
    }

    // ------------------------------------------------------------------ goals

    // Store: done when the bag has nothing (of the goal's kind) left to store. Home: done within HomeArrive of base.
    public static bool GoalDone(World server, Entity player, out string text)
    {
        text = "";
        var g = AIBrain.Goal;
        if (g.kind == GoalKind.Store)
        {
            if (Storable(server, player, g.item).Count > 0) return false;
            if (AIBody.Current is StoreSkill && AIBody.Current.Status == SkillStatus.Running) return false;   // let it note what the chest holds now
            text = "가방의 넣을 것을 모두 넣음";
            return true;
        }
        if (g.kind == GoalKind.Home)
        {
            float3 me = AIBody.Pos(server, player);
            float d = BaseDist(me);
            // 검수 F1: standing on the base stand the walk aimed at (out to ring 6 now) is home too, not a walk again
            float sd = d > HomeArrive && BaseStand(server, player, out int2 hs) ? math.distance(me.xz, (float2)hs) : float.MaxValue;
            if (!AIPlaces.ArrivedAt(d, HomeArrive, sd)) return false;
            text = $"기지 {BaseText} 도착({d:F1}칸)";
            return true;
        }
        return false;
    }

    public static string StallNote(World server, Entity player)
    {
        var pos = AIBody.Pos(server, player);
        var bc = BaseChests();
        return $"기지 {BaseText} {BaseDist(pos):F0}칸, 기지 상자 {bc.Count}개{(bc.Count > 0 ? $"({string.Join("; ", bc.ConvertAll(c => ChestText(c, pos)))})" : "")}, 만들 수 있는 상자 {ChestItem(server, player)}, 준비: {AIBrain.PrepNote}";
    }

    // Nearest walkable tile the AI knows (seen now or before) within 3 tiles of the base tile (the core itself blocks
    // its tiles).
    // 제작대-놓기 §2.2 (실게임3 원인 B, "no known walkable tile within 3 of base" 96~98칸 밖): up to 6 tiles, then a walkable
    // tile within 3 the AI has not seen (the walk plan then says what it does not know: "본 적 없는 칸" etc.).
    internal static bool BaseStand(World server, Entity player, out int2 stand) => StandNear(server, player, BaseTile, out stand);

    internal static bool StandNear(World server, Entity player, int2 centre, out int2 stand)
    {
        if (StandNear(server, player, centre, 6, true, out stand)) return true;
        if (StandNear(server, player, centre, 3, false, out stand)) { AIPlaceGoal.LogChanged("basestand", $"BASE: no known walkable tile within 6 of ({centre.x},{centre.y}); a walkable tile not seen yet ({stand.x},{stand.y}) is the stand"); return true; }   // 검수 S5: once per change
        return false;
    }

    private static bool StandNear(World server, Entity player, int2 centre, int maxR, bool knownOnly, out int2 stand)
    {
        stand = centre;
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        float best = float.MaxValue; bool found = false;
        for (int r = 0; r <= maxR && !found; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (math.max(math.abs(x), math.abs(y)) != r) continue;
                    var t = centre + new int2(x, y);
                    if (knownOnly && !(AIVision.CanSee(me, t) || AIMemory.SeenTile(t)) || !tr.Walkable(t)) continue;
                    float d = math.distance(me.xz, (float2)t);
                    if (d < best) { best = d; stand = t; found = true; }
                }
        return found;
    }

    // ------------------------------------------------------------------ candidates (added to the craft list in BuildCandidates)

    // Free time (no goal, upper LLM unavailable; 2026-11-04 막힘-멈춤-정리): going home to put the bag away, offered
    // only when there is something to store and somewhere to put it (an own base chest that may have room, or a chest
    // in the bag to place) — the store goal's options (go home / store / place), described as free-time work.
    public static List<AICandidate> FreeStoreCandidates(World server, Entity player, AIObservation o, string curKey)
    {
        var res = new List<AICandidate>();
        var bag = Storable(server, player, ObjectID.None);
        if (bag.Count == 0) return res;
        ObjectID ci = ChestItem(server, player);
        AIBody.InventoryCounts(server, player).TryGetValue(ci, out int haveChest);
        if (!BaseChests().Exists(m => m.openedUnix == 0 || m.free > 0) && !(ci != ObjectID.None && haveChest > 0)) return res;
        res = Candidates(server, player, o, curKey, new AIGoal { kind = GoalKind.Store });
        foreach (var c in res) { c.progress = true; c.desc = $"자유 시간: 가방 정리 — 가방에 넣을 것 {StorableText(bag)}. {c.desc}"; }
        return res;
    }

    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey, AIGoal asGoal = null)
    {
        var res = new List<AICandidate>();
        var g = asGoal ?? AIBrain.Goal;
        float3 pos = o.pos;
        float bd = BaseDist(pos);
        var inView = ChestsInView(server, player);

        // go home (home goal; store goal while away from base)
        if ((g.kind == GoalKind.Home && bd > HomeArrive) || (g.kind == GoalKind.Store && bd > BaseRadius))
        {
            if (BaseStand(server, player, out int2 st))
            {
                string k = $"dig:{st.x},{st.y}";
                if (k != curKey)
                {
                    var plan = AIBrain.PlanC(server, player, st, out string why, true);   // 검수 H1: the detour check (the home walk offers the edge way)
                    if (plan != null)
                    {
                        var edgeH = AIPlaceGoal.DigEdge(plan);   // 실게임 D1 추가: the home walk stops on edge walls a replan adds
                        var homeEdgeOk = edgeH.ConvertAll(q => new int2(q.t.x, q.t.y));
                        var homeWalk = AIApproach.WalkCandidate(st, "기지", true,
                            $"기지로 가기 — 기지 {BaseText}, 여기서 {bd:F0}칸. 아는 길 {plan.path.Count}칸{(plan.walls.Count > 0 ? $", 부술 벽 {plan.walls.Count}개({plan.WallSummary()})" : ", 부술 벽 0개")}{(AIPlaceGoal.DoorsOn(plan) is string dr ? ", " + dr : "")}, 예상 약 {plan.EstimatedSeconds:F0}초.{AIPlaceGoal.EdgeText(plan, edgeH)}{AIPlaceGoal.DetourNote(plan)} 기지 옆 ({st.x},{st.y})에 서면 끝.{(g.kind == GoalKind.Store ? " 넣기는 기지에서 따로." : "")}");
                        homeWalk.digEdgeOk = homeEdgeOk;
                        res.Add(homeWalk);   // 제작대-놓기 실게임 D1: the door / edge facts of the walk
                        var altH = AIPlaceGoal.DetourOption(plan, st, "기지", $"기지로 가기(기지 {BaseText}, 여기서 {bd:F0}칸)", $"기지 옆 ({st.x},{st.y})에 서면 끝.");   // 우회 사실
                        if (altH != null && altH.key != curKey) res.Add(altH);
                    }
                    else AIPlaceGoal.LogChanged("baseroute", $"BASE: no route to base stand ({st.x},{st.y}): {why}");
                }
            }
            else AIPlaceGoal.LogChanged("basestand", $"BASE: no walkable tile within 6 (known) or 3 (any) of base {BaseText}");
        }

        // store into a base chest in view (that may have room)
        if (g.kind == GoalKind.Store && bd <= BaseRadius)
        {
            var bag = Storable(server, player, g.item);
            if (bag.Count > 0)
            {
                int n = 0;
                foreach (var c in inView)
                {
                    if (BaseDistTile(AIBody.TileOf(c.pos)) > BaseRadius) continue;
                    var m = Own(c);
                    if (m == null) continue;   // not this character's chest (host's or another AI's): never used
                    if (!MayStoreKnownChest(server, player, m, bag, inView)) continue;
                    string k = "store:" + c.entity.Index;
                    if (k == curKey) continue;
                    n++;
                    res.Add(new AICandidate { kind = "store", key = k, entity = c.entity, tile = AIBody.TileOf(c.pos), itemId = g.item, progress = true,
                        desc = $"상자에 넣기 — {AIAgent.Cur.Name}가 놓은 기지 상자 {c.id} ({c.pos.x:F0},{c.pos.z:F0}) {c.dist:F1}칸. {(m.openedUnix == 0 ? "안은 아직 못 봄." : $"{AIMemory.Ago(m.openedUnix)}에 열었을 때 빈 칸 {m.free}/{m.slots}, 든 것 {ItemsText(m.items)}.")} 가방에서 넣을 것: {StorableText(bag)}({(g.item != ObjectID.None ? "이 품목만" : "도구·무기·음식·제작대·횃불은 가방에 둠")}). 옆으로 가서 열고 한 칸씩 넣음, 자리가 없으면 거기서 멈춤." });
                }
                // a base chest the AI remembers (that may have room) but cannot see from here: walk next to it
                if (n == 0)
                    foreach (var m in BaseChests())
                    {
                        if (!MayStoreKnownChest(server, player, m, bag, inView)) continue;
                        if (inView.Exists(x => math.all(AIBody.TileOf(x.pos) == m.tile))) continue;
                        var stands = AICraft.StandTilesFor(server, player, new AIMemory.Station { id = m.id, tile = m.tile });
                        var plan = AIApproach.FirstCached(server, player, stands, out int2 st, out string why);
                        if (plan == null) { AIPlayerSpikeMod.Log($"BASE: base chest ({m.tile.x},{m.tile.y}): no route ({why})"); continue; }
                        string k = $"dig:{st.x},{st.y}";
                        if (k == curKey) { n++; break; }
                        res.Add(AIApproach.WalkCandidate(st, $"기지 상자 {m.id}", true,
                            $"기지 상자로 가기 — {ChestText(m, pos)}(지금은 화면 밖). 아는 길 {plan.path.Count}칸, 예상 약 {plan.EstimatedSeconds:F0}초. 옆 칸 ({st.x},{st.y})에 서면 끝(넣기는 따로)."));
                        n++;
                        break;
                    }
                // no chest with room at base: place one from the bag (making one is the preparation path's step)
                ObjectID ci = ChestItem(server, player);
                AIBody.InventoryCounts(server, player).TryGetValue(ci, out int haveChest);
                if (n == 0 && ci != ObjectID.None && haveChest > 0 && !BaseChests().Exists(m2 => MayStoreKnownChest(server, player, m2, bag, inView)))
                    AIPlaceSpot.AddUnique(res, AIPlaceSpot.Candidates(server, player, ci, "상자 놓기",
                        $"까닭: 기지 {BaseText}에 {AIAgent.Cur.Name}가 놓은 자리 있는 상자가 없음(다른 플레이어의 상자는 쓰지 않음). 기지에서 {bd:F0}칸. 놓인 상자가 보이는지로 확인.", true, out _));   // no spot: the reason goes to the blocked targets (AIPlaceSpot.TakeNoSpot)
            }
        }

        // take crafting materials the preparation path is missing out of an own chest the AI knows holds them
        foreach (var miss in asGoal != null ? new List<(ObjectID id, int n)>() : S.missing)
        {
            foreach (var c in S.chests.Values)
            {
                if (c.openedUnix == 0 || !c.items.TryGetValue(miss.id, out int has) || has <= 0) continue;
                if (TakeOrGo(server, player, c, miss.id, math.min(has, miss.n), has, inView, pos, curKey, $"만들 것의 재료가 {miss.n}개 모자람", true, res)) break;
            }
        }
        if (res.Count > 0) AIPlayerSpikeMod.Log($"BASE: candidates {res.Count}: {string.Join(" | ", res.ConvertAll(c => c.key))}");
        return res;
    }

    // One option for getting `take` of `id` out of own chest c: "take" when it is in view, else "go to the remembered
    // chest". False when neither could be offered (no route, already the running action).
    private static bool TakeOrGo(World server, Entity player, ChestMem c, ObjectID id, int take, int has, List<ChestView> inView, float3 pos, string curKey, string why, bool progress, List<AICandidate> res)
    {
        var v = inView.Find(x => math.all(AIBody.TileOf(x.pos) == c.tile) && x.id == c.id);
        if (v != null)
        {
            string k = $"take:{v.entity.Index}:{id}";
            if (k == curKey) return false;
            res.Add(new AICandidate { kind = "take", key = k, entity = v.entity, tile = c.tile, itemId = id, itemCount = take, progress = progress,
                desc = $"상자에서 꺼내기 — {id} {take}개, {AIAgent.Cur.Name}가 놓은 상자 {c.id} ({c.tile.x},{c.tile.y}) {v.dist:F1}칸({AIMemory.Ago(c.openedUnix)}에 열었을 때 {has}개). 까닭: {why}. 옆으로 가서 열고 꺼냄." });
            return true;
        }
        var stands = AICraft.StandTilesFor(server, player, new AIMemory.Station { id = c.id, tile = c.tile });
        var plan = AIApproach.FirstCached(server, player, stands, out int2 st, out string w);
        if (plan == null) { AIPlayerSpikeMod.Log($"BASE: own chest ({c.tile.x},{c.tile.y}) with {id}: no route ({w})"); return false; }
        string kd = $"dig:{st.x},{st.y}";
        if (kd == curKey || res.Exists(x => x.key == kd)) return false;
        res.Add(AIApproach.WalkCandidate(st, $"기억한 상자 {c.id}", progress,
            $"기억한 상자로 가기 — {ChestText(c, pos)}(지금은 화면 밖이라 지금 상태는 모름). 거기서 {id} {take}개를 꺼낼 수 있음(까닭: {why}). 아는 길 {plan.path.Count}칸{(plan.walls.Count > 0 ? $", 부술 벽 {plan.walls.Count}개({plan.WallSummary()})" : "")}, 예상 약 {plan.EstimatedSeconds:F0}초. 옆 칸 ({st.x},{st.y})에 서면 끝(꺼내기는 따로)."));
        return true;
    }

    // Food out of an own chest (AIFood, hungry with no hunger food in the bag): the food kind with the most hunger per
    // item in a chest that was seen holding it; up to 5 of it.
    public static List<AICandidate> FoodTakeCandidates(World server, Entity player, AIObservation o, string curKey, string why)
    {
        var res = new List<AICandidate>();
        if (S.chests.Count == 0) return res;
        List<ChestView> inView = null;
        var best = new List<(ChestMem c, ObjectID id, int has, int hunger)>();
        foreach (var c in S.chests.Values)
        {
            if (c.openedUnix == 0) continue;
            foreach (var kv in c.items)
            {
                int h = kv.Value > 0 ? AIFood.HungerAdd(server, kv.Key, 0) : 0;
                if (h > 0) best.Add((c, kv.Key, kv.Value, h));
            }
        }
        if (best.Count == 0) return res;
        best.Sort((a, b) => b.hunger != a.hunger ? b.hunger.CompareTo(a.hunger) : math.distance(o.pos.xz, (float2)a.c.tile).CompareTo(math.distance(o.pos.xz, (float2)b.c.tile)));
        inView = ChestsInView(server, player);
        foreach (var b in best)
            if (TakeOrGo(server, player, b.c, b.id, math.min(5, b.has), b.has, inView, o.pos, curKey, $"{why} {b.id}는 하나에 배고픔 +{b.hunger}", false, res)) break;
        return res;
    }

    // Items of a kind (`want`) out of an own chest seen holding them (먹거리 자급 2: seeds for the field): the kind with
    // the most in one chest, up to `max`; one option ("take" in view, else "go to the remembered chest").
    public static List<AICandidate> ItemTakeCandidates(World server, Entity player, AIObservation o, string curKey, Predicate<ObjectID> want, int max, string why, bool progress)
    {
        var res = new List<AICandidate>();
        var best = new List<(ChestMem c, ObjectID id, int has)>();
        foreach (var c in S.chests.Values)
        {
            if (c.openedUnix == 0) continue;
            foreach (var kv in c.items) if (kv.Value > 0 && want(kv.Key)) best.Add((c, kv.Key, kv.Value));
        }
        if (best.Count == 0) return res;
        best.Sort((a, b) => b.has != a.has ? b.has.CompareTo(a.has) : math.distance(o.pos.xz, (float2)a.c.tile).CompareTo(math.distance(o.pos.xz, (float2)b.c.tile)));
        var inView = ChestsInView(server, player);
        foreach (var b in best)
            if (TakeOrGo(server, player, b.c, b.id, math.min(max, b.has), b.has, inView, o.pos, curKey, why, progress, res)) break;
        return res;
    }

    // One line for the action-selection input (only when base or chests matter for this decision).
    public static string PromptLine(World server, Entity player, AIObservation o)
    {
        var g = AIBrain.Goal;
        if (g.kind != GoalKind.Store && g.kind != GoalKind.Home && S.missing.Count == 0) return "";
        var bc = BaseChests();
        var sb = new StringBuilder();
        sb.Append($"기지: {BaseText}, 여기서 {BaseDist(o.pos):F0}칸(기지 = 그 둘레 {BaseRadius:F0}칸). 기지 상자 {bc.Count}개");
        if (bc.Count > 0) sb.Append($"({string.Join("; ", bc.ConvertAll(c => ChestText(c, o.pos)))})");
        if (g.kind == GoalKind.Store) sb.Append($". 가방에서 넣을 것: {StorableText(Storable(server, player, g.item))}");
        sb.Append('.');
        return sb.ToString();
    }

    // Upper-LLM report fragment (no leading comma): base, known chests, what the bag would store.
    public static string ReportFields(World server, Entity player, float3 pos, Func<string, string> esc)
    {
        var chests = new List<string>();
        var all = new List<ChestMem>(S.chests.Values);
        all.Sort((a, b) => math.distance(pos.xz, (float2)a.tile).CompareTo(math.distance(pos.xz, (float2)b.tile)));
        foreach (var c in all) if (chests.Count < 4) chests.Add($"\"{esc((BaseDistTile(c.tile) <= BaseRadius ? "기지 " : "") + ChestText(c, pos))}\"");
        var st = Storable(server, player, ObjectID.None);
        return $"\"base\":{{\"where\":\"{esc(BaseText)}\",\"distance\":{(int)math.round(BaseDist(pos))}}},\"chests\":[{string.Join(",", chests)}],\"bag_storable\":\"{esc(StorableText(st))}\"";
    }

    public static bool CanStore(World server, Entity player) => Storable(server, player, ObjectID.None).Count > 0;

    // ------------------------------------------------------------------ /ai base

    public static void Command(World server, Entity player, string[] parts)
    {
        string a = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        float3 me = AIBody.Pos(server, player);
        if (a == "여기" || a == "here")
        {
            float3 h = AIBody.HostPosition(server, player);
            S.baseSet = true; S.baseTile = AIBody.TileOf(h); S.baseBy = "호스트"; S.baseUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            AIMemory.MarkDirty(); AIMemory.Save(true);
            AIPlayerSpikeMod.Log($"BASE: set by the host to ({S.baseTile.x},{S.baseTile.y}) (the host's tile)");
            AIExperience.BaseSet(server, player, S.baseTile, S.baseBy);   // experience memory: base_set event
            AIPlayerSpikeMod.Reply($"{AIAgent.Cur.Name} 기지: {BaseText}");
            return;
        }
        if (a == "코어" || a == "core" || a == "해제")
        {
            S.baseSet = false; S.baseBy = ""; AIMemory.MarkDirty(); AIMemory.Save(true);
            AIPlayerSpikeMod.Log("BASE: reset to the core (0,0)");
            AIPlayerSpikeMod.Reply($"{AIAgent.Cur.Name} 기지: {BaseText}");
            return;
        }
        var bc = BaseChests();
        string txt = $"{AIAgent.Cur.Name} 기지 {BaseText}, 여기서 {BaseDist(me):F0}칸 · 기지 상자 {bc.Count}개{(bc.Count > 0 ? ": " + string.Join(" / ", bc.ConvertAll(c => ChestText(c, me))) : "")} · 가방에서 넣을 것 {StorableText(Storable(server, player, ObjectID.None))}";
        AIPlayerSpikeMod.Log("BASE: " + txt);
        AIPlayerSpikeMod.Reply(txt + " · 바꾸기: /ai base 여기 | 코어");
    }
}

// AIFood — eating for hunger outside combat (생활-기지 piece 2 A; report: work/2026-11/2026-11-04-생활-기지/
// 2-먹고살기-조사.md, 3-먹기-합치기.md). Game rules (decompile): hunger 0-100 drops only with distance moved, 1 per 20
// tiles (HungerAndRunningSystem.cs:158-170); below 25 max health and attack drop (about -25% at 0, :178-205); 75+ gives
// "well fed" (+health / +attack, :206-237); respawn sets 70 (PlayerController.cs:6996). Eating adds the item's
// HungerAddition from GivesConditionsWhenConsumedBuffer (EatableSlotConsumeResultEvaluationSystem.cs:319).
// The combat eat option (AIBrain, healing items while health is not full) is unchanged; this adds "eat for hunger"
// options while no enemy is within combat range, and fetching food from an own chest when the bag has none. The model
// chooses (Decisions); EatSkill is the same skill the combat option uses.
public static class AIFood
{
    public const int EatBelow = 75;    // offer eating for hunger below this (75+ is already "well fed")
    public const int TakeBelow = 50;   // fetch food from an own chest below this when the bag has no hunger food
    public const string Rule = "배고픔은 0~100이고 움직일 때만 줄어듦(약 20칸에 1). 25 미만이면 최대 체력·공격력이 줄고(0이면 약 25% 감소), 75 이상이면 배부름(체력·공격력 조금 늘어남). 쓰러졌다 일어나면 70";

    private static readonly Dictionary<long, int> _hunger = new Dictionary<long, int>();

    // HungerAddition of eating one (0 = not an eatable that fills hunger). Uncooked value (cooked dishes use
    // conditionDataWhenCooked; not handled yet).
    public static int HungerAdd(World server, ObjectID id, int variation)
    {
        if (id == ObjectID.None) return 0;
        long key = ((long)id << 32) | (uint)variation;   // a cooked dish's variation is 32 bits (its two ingredients)
        if (_hunger.TryGetValue(key, out int c)) return c;
        int res = 0;
        try
        {
            // cooked food: what eating gives comes from the ingredients' "when cooked" values (AICook.Effects)
            if (AICook.TryCookedHunger(server, id, variation, out int cooked))
            {
                _hunger[key] = cooked;
                if (cooked > 0) AIPlayerSpikeMod.Log($"FOOD: cooked {id} (variation {variation}: {CookedFoodCD.GetPrimaryIngredientFromVariation(variation)}+{CookedFoodCD.GetSecondaryIngredientFromVariation(variation)}) fills hunger +{cooked}");
                return cooked;
            }
            ObjectInfo info = PugDatabase.GetObjectInfo(id, variation);
            if (info != null && info.objectType == ObjectType.Eatable)
            {
                var em = server.EntityManager;
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
                {
                    if (q.CalculateEntityCount() == 1)
                    {
                        var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                        Entity prefab = PugDatabase.GetPrimaryPrefabEntity(id, bank, variation);
                        if (prefab != Entity.Null && em.HasBuffer<GivesConditionsWhenConsumedBuffer>(prefab))
                        {
                            var b = em.GetBuffer<GivesConditionsWhenConsumedBuffer>(prefab, true);
                            for (int i = 0; i < b.Length; i++)
                            {
                                var cd = b[i].conditionDataContainer.conditionData;
                                if (cd.conditionID == ConditionID.HungerAddition) res += cd.value;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"hunger value for {id} failed", e); }
        _hunger[key] = res;
        if (res > 0) AIPlayerSpikeMod.Log($"FOOD: {id} (variation {variation}) fills hunger +{res}{(string.IsNullOrEmpty(AIItems.HealInfo(server, id, variation)) ? "" : ", " + AIItems.HealInfo(server, id, variation))}");
        return res;
    }

    // Hunger food in the bag (hotbar included, cursor slot not): one entry per kind, most hunger per item first.
    public static List<(ObjectID id, int variation, int count, int hunger, string heal)> InBag(World server, Entity player)
    {
        var res = new List<(ObjectID, int, int, int, string)>();
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return res;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = em.HasComponent<CraftingCD>(player) ? math.min(buf.Length, em.GetComponentData<CraftingCD>(player).outputSlotIndex) : buf.Length;
        if (end <= 0) end = buf.Length;
        var seen = new Dictionary<long, int>();
        for (int i = 0; i < end; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None) continue;
            int h = HungerAdd(server, id, buf[i].variation);
            if (h <= 0) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            int n = info != null && !info.isStackable ? 1 : buf[i].amount;
            long key = ((long)id << 16) | (uint)buf[i].variation;
            if (seen.TryGetValue(key, out int idx)) { var e = res[idx]; res[idx] = (e.Item1, e.Item2, e.Item3 + n, e.Item4, e.Item5); }
            else { seen[key] = res.Count; res.Add((id, buf[i].variation, n, h, AIItems.HealInfo(server, id, buf[i].variation))); }
        }
        res.Sort((a, b) => b.Item4.CompareTo(a.Item4));
        return res;
    }

    public static string Meaning(int h) =>
        h < 0 ? "알 수 없음" : h < 25 ? "불이익 구간(최대 체력·공격력 감소 중)" : h >= 75 ? "배부름" : "보통(효과 없음)";

    // Options while no enemy is within combat range and hunger is below EatBelow (called from AIBrain.BuildCandidates;
    // `combat` = the combat list, whose healing "eat" option for the same item gets the hunger facts instead of a copy).
    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey, List<AICandidate> combat)
    {
        var res = new List<AICandidate>();
        int h = o.hunger;
        if (h < 0 || h >= EatBelow) return res;
        var food = InBag(server, player);
        foreach (var f in food)
        {
            string k = $"eat:{f.id}";
            if (k == curKey) continue;
            // No enemy within combat range here: the hunger facts lead. The healing "eat" option for the same item (offered
            // while health is not full, e.g. when hunger below 25 lowers max health) gets this text instead of a copy, so
            // it does not read as a healing choice with the hunger fact at the end (2026-11-04 실기 F2: at hunger 20 the
            // model kept choosing "wait" 0.78 over that option 0.04).
            string d = $"먹기(배고픔) — {f.id} 하나에 배고픔 +{f.hunger}{(string.IsNullOrEmpty(f.heal) ? "" : ", " + f.heal)}, 가방에 {f.count}개. 지금 배고픔 {h}/100: {Meaning(h)}. {Rule}. 먹는 데 약 0.5~1초, 그동안 하던 일은 멈춤(목표 일은 다시 골라야 이어짐).";
            var dup = combat.Find(c => c.key == k);
            if (dup != null) { dup.desc = d; continue; }
            res.Add(new AICandidate { kind = "eat", key = k, itemId = f.id, itemVariation = f.variation, desc = d });
        }
        if (food.Count == 0 && h < TakeBelow)
        {
            try { res.AddRange(AIBase.FoodTakeCandidates(server, player, o, curKey, $"배고픔 {h}/100({Meaning(h)})인데 가방에 배고픔을 채울 음식이 없음.")); }
            catch (Exception e) { AIPlayerSpikeMod.LogError("food: chest options failed", e); }
        }
        if (res.Count > 0) AIPlayerSpikeMod.Log($"FOOD: hunger {h}: options {string.Join(" | ", res.ConvertAll(c => c.key))}");
        return res;
    }

    // Hungry with nothing to eat in the bag (먹거리 자급 1): visible food sources (ripe plants, food growing on the ground,
    // food lying on the ground) are offered by AIBrain.BuildCandidates next to the own-chest options above.
    public static bool NeedsFood(World server, Entity player, AIObservation o) =>
        o.hunger >= 0 && o.hunger < TakeBelow && InBag(server, player).Count == 0;

    // Upper-LLM report fragment (no leading comma): what hunger means now, the hunger food in the bag, and where food
    // can come from (what is on screen now, and what breaking things gave before).
    public static string ReportFields(World server, Entity player, AIObservation o, Func<string, string> esc)
    {
        var food = InBag(server, player);
        string bag = food.Count == 0 ? "없음" : string.Join(", ", food.ConvertAll(f => $"{f.id} {f.count}(하나에 +{f.hunger})"));
        var near = AIFoodSource.Visible(server, player, o.pos, 5);
        string nearTxt = near.Count == 0 ? "화면에 없음" : string.Join(", ", near.ConvertAll(s => $"{s.how} ({s.pos.x:F0},{s.pos.z:F0}) {s.dist:F0}칸"));
        foreach (var it in o.items) { int hh = HungerAdd(server, it.id, 0); if (hh > 0) nearTxt += $", 떨어진 {it.id} x{it.amount} {it.dist:F0}칸(+{hh})"; }
        string learned = AIFoodSource.LearnedText(server);
        return $"\"hunger_note\":\"{esc($"지금 {o.hunger}: {Meaning(o.hunger)}. {Rule}. 배고프면 먹기는 행동 판단기가 한다(가방에 없으면 {AIAgent.Cur.Name}가 놓은 상자에서 꺼내 오거나, 화면의 다 자란 식물·땅에 난 음식을 부숴 얻음)")}\",\"food\":\"{esc(bag)}\",\"food_sources\":\"{esc($"지금 화면: {nearTxt}. 부숴 본 것 중 음식이 나온 것: {(learned.Length == 0 ? "없음" : learned)}")}\"";
    }
}

// What food a visible object gives when broken, as a player sees it (먹거리 자급 1, 2026-11-04). Game code
// (decompile C:\Dev\_ref\corekeeper-decomp): a plant (PlantCD) that has finished growing (HasFinishedGrowingCD, added by
// PlantsGrowingSystem.OnHasFinishedGrowing) drops PlantCD.objectToDropWhenHarvested x numberOfPlantsToDrop when it is
// destroyed (DropLootSystem.DropHarvestedPlantsLootJob); an unripe one gives no harvest (and takes limited damage per hit,
// DamageReductionCD.maxDamagePerHit until ripe). Food growing on the ground (e.g. a wild Mushroom object, spawned in the
// Slime and Larva biomes) is the food item itself and drops itself unless DontDropSelfCD is on (game rule; checked in
// play, test plan E2). Wild ripe plants seen on screen are what a player sees (fruit on the bush).
public static class AIFoodSource
{
    public struct Info { public bool plant, ripe; public ObjectID harvest, food; public int n, hunger; public string how; }

    public static Info Of(World server, Entity e, ObjectID id)
    {
        var em = server.EntityManager;
        var r = new Info();
        if (em.HasComponent<PlantCD>(e))
        {
            r.plant = true;
            var pc = em.GetComponentData<PlantCD>(e);
            r.harvest = pc.objectToDropWhenHarvested;
            r.ripe = em.HasComponent<HasFinishedGrowingCD>(e);
            int h = r.harvest == ObjectID.None ? 0 : AIFood.HungerAdd(server, r.harvest, 0);
            if (r.ripe && h > 0)
            {
                r.food = r.harvest; r.n = math.max(1, pc.numberOfPlantsToDrop); r.hunger = h;
                r.how = $"다 자란 {id}(열매가 보임) — 부수면 {r.harvest} {r.n}개(하나에 배고픔 +{h})";
            }
            else r.how = r.ripe ? $"다 자란 {id}(부수면 {r.harvest})" : $"덜 자란 {id}(아직 열매 없음)";
            return r;
        }
        int hs = AIFood.HungerAdd(server, id, 0);
        if (hs > 0 && !(em.HasComponent<DontDropSelfCD>(e) && em.IsComponentEnabled<DontDropSelfCD>(e)))
        {
            r.food = id; r.n = 1; r.hunger = hs;
            r.how = $"땅에 난 {id} — 부수면 {id}가 떨어짐(배고픔 +{hs})";
        }
        return r;
    }

    // Food sources on this character's screen, nearest first (not creatures, players, tiles or dropped items).
    public static List<(Entity e, ObjectID id, float3 pos, float dist, Info info, string how)> Visible(World server, Entity player, float3 me, int max)
    {
        var res = new List<(Entity, ObjectID, float3, float, Info, string)>();
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<PickUpItemCD>(e)) continue;
                if (em.GetComponentData<HealthCD>(e).health <= 0) continue;
                var id = em.GetComponentData<ObjectDataCD>(e).objectID;
                var info = Of(server, e, id);
                if (info.food == ObjectID.None) continue;
                if (info.plant && AIFarm.ForeignCrop(server, e)) continue;   // someone's crop on dug-up ground: not this character's (own plots only)
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                res.Add((e, id, p, math.distance(me.xz, p.xz), info, info.how));
            }
        }
        res.Sort((a, b) => a.Item4.CompareTo(b.Item4));
        if (res.Count > max) res.RemoveRange(max, res.Count - max);
        return res;
    }

    // "HeartBerryPlant → HeartBerry, Mushroom → Mushroom" from what breaking things gave before (AIDrops, per world).
    public static string LearnedText(World server)
    {
        var parts = new List<string>();
        foreach (var s in AIDrops.Seen())
        {
            if (AIFood.HungerAdd(server, s.item, 0) <= 0) continue;
            string src = s.source == AIDrops.RootSource ? "bigRoot" : ((ObjectID)s.source).ToString();
            parts.Add($"{src} → {s.item}");
        }
        return string.Join(", ", parts);
    }

    // Food items the upper LLM may name as a gather goal now: what visible sources give, what broke into food before,
    // and food lying on the ground in view.
    public static List<ObjectID> GatherableFood(World server, Entity player, AIObservation o)
    {
        var l = new List<ObjectID>();
        foreach (var s in Visible(server, player, o.pos, 8)) if (!l.Contains(s.info.food)) l.Add(s.info.food);
        foreach (var s in AIDrops.Seen()) if (AIFood.HungerAdd(server, s.item, 0) > 0 && !l.Contains(s.item)) l.Add(s.item);
        foreach (var it in o.items) if (AIFood.HungerAdd(server, it.id, 0) > 0 && !l.Contains(it.id)) l.Add(it.id);
        return l;
    }
}

// Walks next to a chest, "opens" it (reads what is inside, as a player sees the chest UI) and moves storable bag slots
// into it one per ~0.1 s, the way a client shift-clicks (InventoryHandler.MoveAllToOrDrop -> Create.MoveOrDropItem).
// Only while the chest has room (an empty slot, or a stack of the same kind), so nothing is dropped. Done when every
// storable slot was moved (or the chest is full after at least one); judged by the bag change.
public class StoreSkill : AISkill
{
    private readonly Entity _chest;
    private readonly ObjectID _filter;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _opened;
    private float _lastSend = -1f, _doneSendingAt = -1f;
    private int _sent;
    private string _stopWhy = "";
    private Dictionary<ObjectID, int> _before;
    private int2 _tile;
    public StoreSkill(Entity chest, ObjectID filter) { Name = "상자에 넣기"; _chest = chest; _filter = filter; Key = "store:" + chest.Index; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_chest) || !em.HasBuffer<ContainedObjectsBuffer>(_chest)) { Status = SkillStatus.Failed; Reason = "상자가 없음"; return false; }
        if (AIBase.Storable(server, player, _filter).Count == 0) { Status = SkillStatus.Failed; Reason = "가방에 넣을 것이 없음"; return false; }
        _before = AIBody.InventoryCounts(server, player);
        float3 cp = em.GetComponentData<LocalTransform>(_chest).Position;
        _tile = AIBody.TileOf(cp);
        Facts.Add($"chest at ({cp.x:F1},{cp.z:F1}); storable {AIBase.StorableText(AIBase.Storable(server, player, _filter))}");
        if (math.distance(AIBody.Pos(server, player).xz, cp.xz) > AIBase.ChestReach)
        {
            _nav.Start(server, player, AIBody.TileOf(cp), AIBase.ChestReach - 0.4f, now, _chest);
            _navOn = true;
        }
        return true;
    }

    private static bool HasRoom(World server, Entity chest, ObjectID id, int variation)
    {
        var em = server.EntityManager;
        var inv = em.GetBuffer<InventoryBuffer>(chest, true);
        var buf = em.GetBuffer<ContainedObjectsBuffer>(chest, true);
        if (inv.Length == 0) return false;
        ObjectInfo info = PugDatabase.GetObjectInfo(id, variation);
        for (int i = inv[0].startIndex; i < inv[0].startIndex + inv[0].size && i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) return true;
            if (info != null && info.isStackable && buf[i].objectID == id && buf[i].variation == variation) return true;
        }
        return false;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_chest) || !em.HasBuffer<ContainedObjectsBuffer>(_chest)) { AIBase.Forget(_tile, "gone while storing"); return Finish(_sent > 0 ? SkillStatus.Done : SkillStatus.Failed, Summary(server, player, "상자가 사라짐")); }
        float3 cp = em.GetComponentData<LocalTransform>(_chest).Position;
        if (_navOn)
        {
            if (math.distance(AIBody.Pos(server, player).xz, cp.xz) <= AIBase.ChestReach) { _nav.Dispose(server); _navOn = false; }
            else
            {
                var st = _nav.Tick(server, player, now, out float2 move);
                if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "상자로 가지 못함: " + _nav.FailReason);
                if (st == NavStatus.Arrived) { _nav.Dispose(server); _navOn = false; }
                else return new BodyInput { move = move, slot = -1 };
            }
        }
        if (!_opened)
        {
            _opened = true;
            var m = AIBase.NoteOpened(server, _chest);
            if (m == null) return Finish(SkillStatus.Failed, "이 캐릭터가 놓은 상자가 아님(다른 플레이어의 상자는 쓰지 않음)");
            AIPlayerSpikeMod.Log($"SKILL 상자에 넣기: opened chest {AIBase.ChestText(m, AIBody.Pos(server, player))}");
        }
        if (_doneSendingAt < 0f)
        {
            if (now - _lastSend < 0.1f) return new BodyInput { face = AIBody.DirTo(AIBody.Pos(server, player), cp.xz), slot = -1 };
            var bag = AIBase.Storable(server, player, _filter);
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            int pick = -1;
            foreach (var x in bag) if (HasRoom(server, _chest, x.id, buf[x.slot].variation)) { pick = x.slot; break; }
            if (pick < 0)
            {
                _stopWhy = bag.Count == 0 ? "넣을 것을 모두 넣음" : $"상자에 자리가 없음(남은 것 {AIBase.StorableText(bag)})";
                _doneSendingAt = now;
            }
            else
            {
                var inv = em.GetBuffer<InventoryBuffer>(_chest, true);
                int end = inv[0].startIndex + inv[0].size;
                float3 me = AIBody.Pos(server, player);
                if (!AICraft.QueueInventoryChange(server, player, Create.MoveOrDropItem(player, pick, _chest, -1, end, me)))
                    return Finish(SkillStatus.Failed, "옮기기 요청을 넣지 못함(UIActionBuffer 없음)");
                AIPlayerSpikeMod.Log($"SKILL 상자에 넣기: move slot {pick} {buf[pick].objectID} x{buf[pick].amount} into the chest (client MoveOrDropItem, like a shift-click)");
                _sent++; _lastSend = now;
                if (_sent > 60) ObserveOnce("store-count", "60칸 넘게 옮김. 계속 정리할지 직접 선택");
            }
            return new BodyInput { face = AIBody.DirTo(AIBody.Pos(server, player), cp.xz), slot = -1 };
        }
        // the last move takes a tick or two to land
        if (now - _doneSendingAt < 0.5f) return BodyInput.Idle;
        AIBase.NoteOpened(server, _chest);
        if (_sent == 0) return Finish(SkillStatus.Failed, Summary(server, player, _stopWhy));
        return Finish(SkillStatus.Done, Summary(server, player, _stopWhy));
    }

    private string Summary(World server, Entity player, string why) => $"{why} · 가방 {AIBody.InventoryDiff(_before ?? new Dictionary<ObjectID, int>(), AIBody.InventoryCounts(server, player))}";

    public override string Progress(World server, Entity player) => _navOn ? "상자로 가는 중" : $"{_sent}칸 넣음";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}

// Walks next to a chest, opens it and takes `amount` of an item into the bag (Create.MoveOrDropAmount from the chest
// slot, as a client moving part of a stack). Done when the bag has that many more (or all the chest had).
public class TakeSkill : AISkill
{
    private readonly Entity _chest;
    private readonly ObjectID _item;
    private readonly int _amount;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _sent;
    private float _sentAt;
    private int _before, _expect;
    private Dictionary<ObjectID, int> _invBefore;
    private int2 _tile;
    public TakeSkill(Entity chest, ObjectID item, int amount) { Name = "상자에서 꺼내기"; _chest = chest; _item = item; _amount = math.max(1, amount); Key = $"take:{chest.Index}:{item}"; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_chest) || !em.HasBuffer<ContainedObjectsBuffer>(_chest)) { Status = SkillStatus.Failed; Reason = "상자가 없음"; return false; }
        _invBefore = AIBody.InventoryCounts(server, player);
        _invBefore.TryGetValue(_item, out _before);
        float3 cp = em.GetComponentData<LocalTransform>(_chest).Position;
        _tile = AIBody.TileOf(cp);
        Facts.Add($"take {_item} x{_amount} from chest at ({cp.x:F1},{cp.z:F1}); bag had {_before}");
        if (math.distance(AIBody.Pos(server, player).xz, cp.xz) > AIBase.ChestReach)
        {
            _nav.Start(server, player, AIBody.TileOf(cp), AIBase.ChestReach - 0.4f, now, _chest);
            _navOn = true;
        }
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_chest) || !em.HasBuffer<ContainedObjectsBuffer>(_chest)) { AIBase.Forget(_tile, "gone while taking"); return Finish(SkillStatus.Failed, "상자가 사라짐"); }
        float3 cp = em.GetComponentData<LocalTransform>(_chest).Position;
        if (_navOn)
        {
            if (math.distance(AIBody.Pos(server, player).xz, cp.xz) <= AIBase.ChestReach) { _nav.Dispose(server); _navOn = false; }
            else
            {
                var st = _nav.Tick(server, player, now, out float2 move);
                if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "상자로 가지 못함: " + _nav.FailReason);
                if (st == NavStatus.Arrived) { _nav.Dispose(server); _navOn = false; }
                else return new BodyInput { move = move, slot = -1 };
            }
        }
        if (!_sent)
        {
            var m = AIBase.NoteOpened(server, _chest);
            if (m == null) return Finish(SkillStatus.Failed, "이 캐릭터가 놓은 상자가 아님(다른 플레이어의 상자는 쓰지 않음)");
            m.items.TryGetValue(_item, out int has);
            if (has <= 0) return Finish(SkillStatus.Failed, $"열어 보니 상자에 {_item}이(가) 없음({AIBase.ChestText(m, AIBody.Pos(server, player))})");
            int out_ = em.HasComponent<CraftingCD>(player) ? em.GetComponentData<CraftingCD>(player).outputSlotIndex : 50;
            var inv = em.GetBuffer<InventoryBuffer>(_chest, true);
            var buf = em.GetBuffer<ContainedObjectsBuffer>(_chest, true);
            float3 me = AIBody.Pos(server, player);
            int left = _amount;
            for (int i = inv[0].startIndex; i < inv[0].startIndex + inv[0].size && i < buf.Length && left > 0; i++)
            {
                if (buf[i].objectID != _item) continue;
                ObjectInfo info = PugDatabase.GetObjectInfo(_item, buf[i].variation);
                int n = info != null && !info.isStackable ? 1 : math.min(left, buf[i].amount);
                if (!AICraft.QueueInventoryChange(server, player, Create.MoveOrDropAmount(_chest, i, player, -1, out_, n, me)))
                    return Finish(SkillStatus.Failed, "옮기기 요청을 넣지 못함(UIActionBuffer 없음)");
                AIPlayerSpikeMod.Log($"SKILL 상자에서 꺼내기: take {_item} x{n} from chest slot {i} (client MoveOrDropAmount)");
                left -= n;
            }
            _expect = _amount - left;
            _sent = true; _sentAt = now;
            return BodyInput.Idle;
        }
        var invNow = AIBody.InventoryCounts(server, player);
        invNow.TryGetValue(_item, out int c);
        if (c - _before >= _expect)
        {
            AIBase.NoteOpened(server, _chest);
            return Finish(SkillStatus.Done, $"상자에서 {_item} {c - _before}개 꺼냄 · 가방 {AIBody.InventoryDiff(_invBefore, invNow)}");
        }
        if (now - _sentAt > 2f)
        {
            AIBase.NoteOpened(server, _chest);
            ObserveOnce("take-pending", $"상자 이동 요청 결과 확인 중: {_item} {c - _before}/{_expect}개. 중복 요청 없이 계속 관측");
        }
        return BodyInput.Idle;
    }

    public override string Progress(World server, Entity player) => _navOn ? "상자로 가는 중" : "꺼내는 중";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
