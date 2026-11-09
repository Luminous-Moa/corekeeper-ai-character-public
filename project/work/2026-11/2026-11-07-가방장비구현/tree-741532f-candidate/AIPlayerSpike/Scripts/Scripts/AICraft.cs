// AICraft — crafting for the AI body. Report: work/2026-11/2026-11-04-기본-제작.md (session 코어키퍼-AI-캐릭터).
//
// How the game crafts (decompile): the client's crafting UI builds Create.Craft(objectID, amount, 0, player, station)
// (Inventory/Create.cs:350, InventoryHandler.cs:222) and queues it as UIInputAction.Craft; on the server
// TriggerUIActionsSystem (:207-218) adds it to the CraftBuffer singleton, InventoryUpdateSystem (:411-444) takes the
// player inventory + chests near the station and calls InventoryUtility.Craft (:1709), which checks materials only
// (CanCraft :1605). Range and "this station makes this item" are checked by the client UI, not by the server.
//
// The client sends it as UIInputActionDataRPC (SendClientInputSystem.cs:225); the server's
// ReceiveUIInputActionsRPCSystem (:136-147) appends it to the player's UIActionBuffer. The mod appends the same
// UIActionBuffer entry for the AI on the server (the thin client has no ghosts to name entities in an RPC)
// (player = AI, main inventory = AI, crafting entity = the station), after checking what the client UI would check:
// the AI stands next to the station (within StationRange tiles), the station's recipe list (CanCraftObjectsBuffer)
// has the item, the AI's bag has the materials (ObjectInfo.requiredObjectsToCraft). The result is judged by the
// actual inventory change.
using System;
using System.Collections.Generic;
using Inventory;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AICraft
{
    public const float StationRange = 2.0f;

    public class Station { public Entity entity; public ObjectID id; public float3 pos; public float dist; public List<ObjectID> recipes = new List<ObjectID>(); }

    // Crafting stations in view (entities with a recipe list), nearest first. The AI itself is included as "hand"
    // when the player entity has a recipe list.
    public static List<Station> StationsInView(World server, Entity player)
    {
        var res = new List<Station>();
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CanCraftObjectsBuffer>(), ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (em.HasComponent<PlayerGhost>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var s = new Station { entity = e, id = em.GetComponentData<ObjectDataCD>(e).objectID, pos = p, dist = math.distance(me.xz, p.xz) };
                var b = em.GetBuffer<CanCraftObjectsBuffer>(e, true);
                for (int i = 0; i < b.Length; i++) if (b[i].objectID != ObjectID.None) s.recipes.Add(b[i].objectID);
                res.Add(s);
            }
        }
        res.Sort((a, b) => a.dist.CompareTo(b.dist));
        return res;
    }

    // Recipes the player can make without a station (the player entity's own recipe list), empty if none.
    public static List<ObjectID> HandRecipes(World server, Entity player)
    {
        var res = new List<ObjectID>();
        var em = server.EntityManager;
        if (!em.HasBuffer<CanCraftObjectsBuffer>(player)) return res;
        var b = em.GetBuffer<CanCraftObjectsBuffer>(player, true);
        for (int i = 0; i < b.Length; i++) if (b[i].objectID != ObjectID.None) res.Add(b[i].objectID);
        return res;
    }

    // Materials for one unit (ObjectInfo.requiredObjectsToCraft), and what is missing given the bag.
    public static List<(ObjectID id, int need, int have)> Materials(World server, Entity player, ObjectID item, int count)
    {
        var res = new List<(ObjectID, int, int)>();
        ObjectInfo info = PugDatabase.GetObjectInfo(item);
        if (info == null || info.requiredObjectsToCraft == null) return res;
        var inv = AIBody.InventoryCounts(server, player);
        foreach (var r in info.requiredObjectsToCraft)
        {
            inv.TryGetValue(r.objectID, out int have);
            res.Add((r.objectID, r.amount * count, have));
        }
        return res;
    }

    public static string MaterialsText(List<(ObjectID id, int need, int have)> m)
    {
        var parts = new List<string>();
        foreach (var x in m) parts.Add($"{x.id} {x.have}/{x.need}");
        return parts.Count == 0 ? "재료 정보 없음" : string.Join(", ", parts);
    }

    public static bool HasMaterials(List<(ObjectID id, int need, int have)> m) => m.Count > 0 && m.TrueForAll(x => x.have >= x.need);

    // The server's craft request singleton (an entity with a CraftBuffer).
    private static bool CraftBufferEntity(World server, out Entity e)
    {
        e = Entity.Null;
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadWrite<CraftBuffer>()))
        {
            if (q.CalculateEntityCount() != 1) return false;
            e = q.GetSingletonEntity();
            return true;
        }
    }

    // Queues one craft action the way the server receives a client's: a real client sends UIInputActionDataRPC
    // {tick = server tick + 1, action = Craft, craftActionData = Create.Craft(...)} (SendClientInputSystem.cs:225,
    // InventoryHandler.cs:211-223) and the server's ReceiveUIInputActionsRPCSystem (:136-147) appends it to the
    // player's UIActionBuffer; TriggerUIActionsSystem (:199-222) then moves it into the CraftBuffer. The thin client
    // has no ghost entities to reference in an RPC, so the mod appends the same UIActionBuffer entry on the server.
    // Fallback (no UIActionBuffer): the CraftBuffer entry TriggerUIActionsSystem would add.
    public static bool QueueCraft(World server, Entity player, Entity station, ObjectID item, int amount, out string why)
    {
        why = "";
        var em = server.EntityManager;
        var data = Create.Craft(item, amount, 0, player, station);
        if (em.HasBuffer<UIActionBuffer>(player))
        {
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Unity.NetCode.NetworkTime>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    var tick = q.GetSingleton<Unity.NetCode.NetworkTime>().ServerTick;
                    tick.Add(1u);
                    em.GetBuffer<UIActionBuffer>(player).Add(new UIActionBuffer { tick = tick, actionData = new UIInputActionData { action = UIInputAction.Craft, craftActionData = data } });
                    why = "UIActionBuffer";
                    return true;
                }
            }
        }
        if (!CraftBufferEntity(server, out Entity cb)) { why = "서버 제작 요청 버퍼를 찾지 못함"; return false; }
        why = "CraftBuffer";
        var buf = em.GetBuffer<CraftBuffer>(cb);
        buf.Add(new CraftBuffer { playerEntity = player, craftActionData = data });
        return true;
    }

    // A client UI action (inventory change) queued like QueueCraft: UIActionBuffer entry at server tick + 1.
    public static bool QueueInventoryChange(World server, Entity player, InventoryChangeData change)
    {
        var em = server.EntityManager;
        if (!em.HasBuffer<UIActionBuffer>(player)) return false;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Unity.NetCode.NetworkTime>()))
        {
            if (q.CalculateEntityCount() != 1) return false;
            var tick = q.GetSingleton<Unity.NetCode.NetworkTime>().ServerTick;
            tick.Add(1u);
            em.GetBuffer<UIActionBuffer>(player).Add(new UIActionBuffer { tick = tick, actionData = new UIInputActionData { action = UIInputAction.InventoryChange, inventoryChangeData = change } });
            return true;
        }
    }

    // The crafting output slot of the player's inventory is the "item held on the cursor": a hand or station craft
    // puts the made item there (InventoryUtility.Craft :1715-1760 writes craftingCD.outputSlotIndex), and while
    // something is there the equipment cannot be used (EquipmentUpdateSystem.Execute: hasItemInMouse) and a different
    // item cannot be crafted (CanCraft :1645-1650). A player puts it into the bag; the AI does the same with the
    // client's MoveOrDropItem inventory change. Returns the item that was there (None if empty).
    public static ObjectID CursorItem(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!em.HasComponent<CraftingCD>(player) || !em.HasBuffer<ContainedObjectsBuffer>(player)) return ObjectID.None;
        int outIdx = em.GetComponentData<CraftingCD>(player).outputSlotIndex;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return outIdx < 0 || outIdx >= buf.Length ? ObjectID.None : buf[outIdx].objectID;
    }

    public static ObjectID StowCursorItem(World server, Entity player, out int amount)
    {
        amount = 0;
        var em = server.EntityManager;
        if (!em.HasComponent<CraftingCD>(player) || !em.HasBuffer<ContainedObjectsBuffer>(player)) return ObjectID.None;
        int outIdx = em.GetComponentData<CraftingCD>(player).outputSlotIndex;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        if (outIdx < 0 || outIdx >= buf.Length || buf[outIdx].objectID == ObjectID.None) return ObjectID.None;
        var id = buf[outIdx].objectID; amount = buf[outIdx].amount;
        float3 pos = em.GetComponentData<LocalTransform>(player).Position;
        if (!QueueInventoryChange(server, player, Create.MoveOrDropItem(player, outIdx, player, 0, outIdx, pos))) return ObjectID.None;
        return id;
    }

    // Recipe list of a station kind from its prefab (the same list the station's crafting UI shows); cached.
    private static readonly Dictionary<ObjectID, List<ObjectID>> _prefabRecipes = new Dictionary<ObjectID, List<ObjectID>>();
    public static List<ObjectID> PrefabRecipes(World server, ObjectID station)
    {
        if (_prefabRecipes.TryGetValue(station, out var l)) return l;
        l = new List<ObjectID>();
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(station, bank, 0);
                if (prefab != Entity.Null && em.HasBuffer<CanCraftObjectsBuffer>(prefab))
                {
                    var b = em.GetBuffer<CanCraftObjectsBuffer>(prefab, true);
                    for (int i = 0; i < b.Length; i++) if (b[i].objectID != ObjectID.None) l.Add(b[i].objectID);
                }
            }
        }
        catch { }
        _prefabRecipes[station] = l;
        return l;
    }

    // A remembered station (seen or placed by the AI, now out of view) that makes the item, nearest first.
    public static AIMemory.Station? RememberedStationFor(World server, Entity player, ObjectID item)
    {
        foreach (var s in AIMemory.RememberedStations(AIBody.Pos(server, player)))
            if (PrefabRecipes(server, s.id).Contains(item)) return s;
        return null;
    }

    // Tiles next to a station's footprint (from the game data's prefab size, as AIObjects) that are walkable and known
    // (seen now or before), nearest to the AI first: where to stand to use a remembered station.
    public static List<int2> StandTilesFor(World server, Entity player, AIMemory.Station s)
    {
        var res = new List<int2>();
        ObjectInfo info = PugDatabase.GetObjectInfo(s.id);
        int2 size = info != null ? new int2(math.max(1, info.prefabTileSize.x), math.max(1, info.prefabTileSize.y)) : new int2(1, 1);
        int2 corner = info != null ? new int2(info.prefabCornerOffset.x, info.prefabCornerOffset.y) : int2.zero;
        int2 b = s.tile + corner;
        if (info != null && info.centerIsAtEntityPosition) b = s.tile - size / 2;
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        for (int x = b.x - 1; x <= b.x + size.x; x++)
            for (int y = b.y - 1; y <= b.y + size.y; y++)
            {
                var t = new int2(x, y);
                bool inside = x >= b.x && x < b.x + size.x && y >= b.y && y < b.y + size.y;
                if (inside) continue;
                if (!(AIVision.CanSee(me, t) || AIMemory.SeenTile(t)) || !tr.Walkable(t)) continue;
                res.Add(t);
            }
        res.Sort((p, q) => math.distance(me.xz, (float2)p).CompareTo(math.distance(me.xz, (float2)q)));
        return res;
    }

    // A recipe the AI knows: offered by hand, by a station in view, or by a basic wooden workbench (the station the
    // AI can expect to find or build first). Used to check the upper LLM's craft goals.
    public static bool KnownRecipe(World server, Entity player, ObjectID item, out string where)
    {
        where = "";
        if (HandRecipes(server, player).Contains(item)) { where = "손"; return true; }
        var st = StationsInView(server, player).Find(s => s.recipes.Contains(item));
        if (st != null) { where = $"{st.id}"; return true; }
        var known = RememberedStationFor(server, player, item);
        if (known.HasValue) { where = $"{known.Value.id}(기억, 화면 밖 ({known.Value.tile.x},{known.Value.tile.y}))"; return true; }
        // a station the AI can build (hand recipe or basic-workbench recipe) makes it
        ObjectID sid = StationFor(server, player, item);
        if (sid != ObjectID.None) { where = $"{sid}(아직 없음)"; return true; }
        return false;
    }

    // 제작대-놓기 §2.5: when no station the AI knows makes the item (KnownRecipe false), the sentence with the way to it.
    // One step only: the station that makes it among the bag's stations, hand recipes, the wooden workbench's recipes and
    // the recipes of stations in view or remembered (prefab CanCraftObjectsBuffer); where that station is made.
    public static string CraftFact(World server, Entity player, ObjectID item)
    {
        try
        {
            var inv = AIBody.InventoryCounts(server, player);
            var hand = HandRecipes(server, player);
            var view = StationsInView(server, player);
            var remembered = AIMemory.RememberedStations(AIBody.Pos(server, player));
            var wb = PrefabRecipes(server, ObjectID.WoodenWorkBench);
            // where each known thing is made: hand, the workbench (as a recipe of it), a station in view, a remembered one
            var makeAt = new Dictionary<ObjectID, string>();
            foreach (var h in hand) if (!makeAt.ContainsKey(h)) makeAt[h] = "손";
            foreach (var s in view) foreach (var r in s.recipes) if (!makeAt.ContainsKey(r)) makeAt[r] = $"{s.id}(보임 ({s.pos.x:F0},{s.pos.z:F0}))";
            foreach (var s in remembered) foreach (var r in PrefabRecipes(server, s.id)) if (!makeAt.ContainsKey(r)) makeAt[r] = $"{s.id}(기억 {AIPlaces.ObjectId("station", s.id.ToString(), s.tile.x, s.tile.y)})";
            foreach (var r in wb) if (!makeAt.ContainsKey(r)) makeAt[r] = "WoodenWorkBench(나무 작업대, 아직 없음)";
            var cands = new List<ObjectID>();
            foreach (var kv in inv) if (kv.Value > 0 && PrefabRecipes(server, kv.Key).Count > 0) cands.Add(kv.Key);   // bag stations first
            foreach (var k in makeAt.Keys) if (!cands.Contains(k)) cands.Add(k);
            foreach (var st in cands)
            {
                if (st == item || !PrefabRecipes(server, st).Contains(item)) continue;
                inv.TryGetValue(st, out int nb);
                makeAt.TryGetValue(st, out string at);
                return AIPlaces.CraftFact(item.ToString(), st.ToString(), nb, at ?? "", MaterialsText(Materials(server, player, st, 1)));
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("craft: craft fact failed", e); }
        return AIPlaces.CraftFact(item.ToString(), "", 0, "", "");
    }

    // A placeable crafting station whose prefab recipe list (CanCraftObjectsBuffer) has the item: what the AI could
    // build and place when no station in view makes it. Looked for among things the AI can already make: hand recipes,
    // and the recipes of the basic workbench (WoodenWorkBench, itself a hand recipe) — e.g. Furnace for bars. The
    // caller chains them (bar -> Furnace -> WoodenWorkBench -> hand).
    public static ObjectID StationFor(World server, Entity player, ObjectID item)
    {
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                var known = new List<ObjectID>(HandRecipes(server, player));
                Entity wb = PugDatabase.GetPrimaryPrefabEntity(ObjectID.WoodenWorkBench, bank, 0);
                if (wb != Entity.Null && em.HasBuffer<CanCraftObjectsBuffer>(wb))
                {
                    var wbb = em.GetBuffer<CanCraftObjectsBuffer>(wb, true);
                    for (int i = 0; i < wbb.Length; i++) if (!known.Contains(wbb[i].objectID)) known.Add(wbb[i].objectID);
                }
                foreach (var st in known)
                {
                    if (st == item) continue;
                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(st, bank, 0);
                    if (prefab == Entity.Null || !em.HasBuffer<CanCraftObjectsBuffer>(prefab)) continue;
                    var b = em.GetBuffer<CanCraftObjectsBuffer>(prefab, true);
                    for (int i = 0; i < b.Length; i++) if (b[i].objectID == item) return st;
                }
            }
        }
        catch { }
        return ObjectID.None;
    }

    // Moves an item from the bag into the hotbar (an empty hotbar slot, else the last one holding neither pick nor
    // weapon), like the weapon swap in AIBody.WeaponSlotForAttack. Returns the hotbar slot, -1 if not possible.
    public static int HotbarSlotFor(World server, Entity player, ObjectID item, out string swap)
    {
        swap = "";
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return -1;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
        for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == item) return i;
        int bag = -1;
        for (int i = 10; i < buf.Length; i++) if (buf[i].objectID == item) { bag = i; break; }
        if (bag < 0) return -1;
        int hot = -1;
        for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == ObjectID.None) { hot = i; break; }
        if (hot < 0)
            for (int i = 9; i >= 0; i--)
            {
                ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
                if (info == null || (info.objectType != ObjectType.MiningPick && info.objectType != ObjectType.MeleeWeapon)) { hot = i; break; }
            }
        if (hot < 0) return -1;
        var a = buf[hot]; var b = buf[bag];
        buf[hot] = b; buf[bag] = a;
        swap = $"swapped {b.objectID} from bag slot {bag} into hotbar slot {hot}";
        return hot;
    }

    // TEST ONLY diagnosis: stations in view, hand recipes, a few recipes.
    public static void Diagnose(World server, Entity player)
    {
        var st = StationsInView(server, player);
        var parts = new List<string>();
        foreach (var s in st) parts.Add($"{s.id} ({s.pos.x:F0},{s.pos.z:F0}) d={s.dist:F1} recipes={s.recipes.Count}[{string.Join(",", s.recipes.GetRange(0, Math.Min(12, s.recipes.Count)))}]");
        var hand = HandRecipes(server, player);
        var rec = new List<string>();
        foreach (var id in new[] { ObjectID.WoodenWorkBench, ObjectID.WoodSword, ObjectID.WoodMiningPick, ObjectID.CopperMiningPick, ObjectID.CopperBar, ObjectID.Furnace, ObjectID.Torch, ObjectID.CopperSword })
            rec.Add($"{id}=[{MaterialsText(Materials(server, player, id, 1))}]");
        AIPlayerSpikeMod.Log($"TEST-ONLY craft: stations in view {st.Count}: {string.Join(" | ", parts)}; hand recipes {hand.Count}: [{string.Join(",", hand)}]; recipes: {string.Join("; ", rec)}");
        try
        {
            var em1 = server.EntityManager;
            using (var q = em1.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                foreach (var sid in new[] { ObjectID.WoodenWorkBench, ObjectID.Furnace })
                {
                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(sid, bank, 0);
                    var l = new List<string>();
                    if (prefab != Entity.Null && em1.HasBuffer<CanCraftObjectsBuffer>(prefab))
                    {
                        var b = em1.GetBuffer<CanCraftObjectsBuffer>(prefab, true);
                        for (int i = 0; i < b.Length; i++) if (b[i].objectID != ObjectID.None) l.Add($"{b[i].objectID}[{MaterialsText(Materials(server, player, b[i].objectID, 1))}]");
                    }
                    AIPlayerSpikeMod.Log($"TEST-ONLY craft: {sid} prefab recipes {l.Count}: {string.Join("; ", l)}");
                }
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.Log("TEST-ONLY craft: prefab recipes failed " + e.Message); }
        // destructible objects in view (what could give wood etc.)
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        var objs = new Dictionary<ObjectID, (int n, float d)>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<PickUpItemCD>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var id = em.GetComponentData<ObjectDataCD>(e).objectID;
                objs.TryGetValue(id, out var v);
                objs[id] = (v.n + 1, v.n == 0 ? math.distance(me.xz, p.xz) : math.min(v.d, math.distance(me.xz, p.xz)));
            }
        }
        var ol = new List<string>();
        foreach (var kv in objs) ol.Add($"{kv.Key} x{kv.Value.n} (nearest {kv.Value.d:F1})");
        // tile types in view (are there big roots, the wood source in walls?)
        var tr = new AITileReader(server);
        var tt = new Dictionary<string, int>();
        int2 c0 = AIBody.TileOf(me);
        for (int dx = -14; dx <= 14; dx++) for (int dy = -10; dy <= 10; dy++)
        {
            int2 t = c0 + new int2(dx, dy);
            if (!AIVision.CanSee(me, (float2)t)) continue;
            foreach (var l in tr.Layers(t)) { string k = $"{l.tileType}/{l.tileset}"; tt.TryGetValue(k, out int n); tt[k] = n + 1; }
        }
        var tl = new List<string>(); foreach (var kv in tt) tl.Add($"{kv.Key}x{kv.Value}");
        AIPlayerSpikeMod.Log($"TEST-ONLY craft tile types in view: {string.Join(", ", tl)}");
        AIPlayerSpikeMod.Log($"TEST-ONLY craft objects with health in view: {string.Join(", ", ol)}");        AIPlayerSpikeMod.Reply($"[시험용] 제작대 {st.Count}개, 손 제작 {hand.Count}개");
    }
}

// Craft one recipe (count units): at a station (walk next to it first) or by hand (station = the AI itself).
// Done when the item count in the bag rises by `count`; failed if the recipe is not offered there, materials are
// short, or the bag did not change within 3 s after the request.
public class CraftSkill : AISkill
{
    private readonly ObjectID _item;
    private readonly int _count;
    private readonly Entity _station;      // Entity.Null = by hand
    private readonly string _where;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _sent;
    private float _sentAt, _stowAt = -1f, _preStowAt = -1f;
    private int _before;
    private Dictionary<ObjectID, int> _invBefore;
    public CraftSkill(ObjectID item, int count, Entity station, string where) { Name = "만들기"; _item = item; _count = math.max(1, count); _station = station; _where = where; Key = $"craft:{item}"; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        bool hand = _station == Entity.Null;
        var recipes = hand ? AICraft.HandRecipes(server, player) : new List<ObjectID>();
        if (!hand)
        {
            if (!em.Exists(_station) || !em.HasBuffer<CanCraftObjectsBuffer>(_station)) { Status = SkillStatus.Failed; Reason = "제작대가 없음"; return false; }
            var b = em.GetBuffer<CanCraftObjectsBuffer>(_station, true);
            for (int i = 0; i < b.Length; i++) recipes.Add(b[i].objectID);
        }
        if (!recipes.Contains(_item)) { Status = SkillStatus.Failed; Reason = $"{_where}에서 {_item}을(를) 만들 수 없음"; return false; }
        var m = AICraft.Materials(server, player, _item, _count);
        if (!AICraft.HasMaterials(m)) { Status = SkillStatus.Failed; Reason = $"재료 부족: {AICraft.MaterialsText(m)}"; return false; }
        _invBefore = AIBody.InventoryCounts(server, player);
        _invBefore.TryGetValue(_item, out _before);
        Facts.Add($"item={_item} x{_count} at {_where}; materials {AICraft.MaterialsText(m)}; had {_before}");
        if (!hand)
        {
            float3 sp = em.GetComponentData<LocalTransform>(_station).Position;
            if (math.distance(AIBody.Pos(server, player).xz, sp.xz) > AICraft.StationRange)
            {
                // walk up to the station like to a breakable object: the station's tile is blocked by it, so the
                // navigator stops within reach of it (a fixed side tile failed with "no path" in ai2)
                _nav.Start(server, player, AIBody.TileOf(sp), AICraft.StationRange - 0.4f, now, _station);
                _navOn = true;
            }
        }
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (_navOn)
        {
            float3 sp = em.GetComponentData<LocalTransform>(_station).Position;
            if (math.distance(AIBody.Pos(server, player).xz, sp.xz) <= AICraft.StationRange) { _nav.Dispose(server); _navOn = false; }
            else
            {
                var st = _nav.Tick(server, player, now, out float2 move);
                if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "제작대로 가지 못함: " + _nav.FailReason);
                if (st == NavStatus.Arrived) { _nav.Dispose(server); _navOn = false; }
                else return new BodyInput { move = move, slot = -1 };
            }
        }
        if (!_sent && AICraft.CursorItem(server, player) != ObjectID.None)
        {
            // something already on the cursor would block the craft (CanCraft): put it away first
            if (_preStowAt < 0f) { _preStowAt = now; AICraft.StowCursorItem(server, player, out _); }
            if (now - _preStowAt < 2f) return BodyInput.Idle;
            return Finish(SkillStatus.Failed, $"커서 칸의 {AICraft.CursorItem(server, player)}을(를) 가방에 넣지 못해 만들 수 없음");
        }
        if (!_sent)
        {
            if (!AICraft.QueueCraft(server, player, _station == Entity.Null ? player : _station, _item, _count, out string why)) return Finish(SkillStatus.Failed, why);
            _sent = true; _sentAt = now;
            AIPlayerSpikeMod.Log($"SKILL 만들기: craft request {_item} x{_count} at {_where} (via {why}, like a client's UIInputAction.Craft)");
            return BodyInput.Idle;
        }
        var inv = AIBody.InventoryCounts(server, player);
        inv.TryGetValue(_item, out int now_);
        if (now_ - _before >= _count)
        {
            // the made item sits on the cursor (crafting output slot); put it into the bag before finishing
            if (_stowAt < 0f) { _stowAt = now; var s = AICraft.StowCursorItem(server, player, out int a); if (s != ObjectID.None) AIPlayerSpikeMod.Log($"SKILL 만들기: made item on the cursor {s} x{a}; moving it into the bag"); return BodyInput.Idle; }
            if (AICraft.CursorItem(server, player) != ObjectID.None && now - _stowAt < 2f) return BodyInput.Idle;
            return Finish(SkillStatus.Done, $"{_item} {_count}개 만듦 · 가방 {AIBody.InventoryDiff(_invBefore, inv)}{(AICraft.CursorItem(server, player) != ObjectID.None ? " (커서 칸에 남음)" : "")}");
        }
        if (now - _sentAt > 3f) ObserveOnce("pending-1", $"3초 동안 가방이 늘지 않음({_item} {_before}→{now_})");
        return BodyInput.Idle;
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }
}
