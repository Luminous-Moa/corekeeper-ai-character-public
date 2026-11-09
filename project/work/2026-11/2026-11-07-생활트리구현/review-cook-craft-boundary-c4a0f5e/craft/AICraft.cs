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

// A request-time recipe, not a later lookup by item. All quantities are in game units.
public sealed class AICraftSelection
{
    public Entity Station;
    public ObjectID Item;
    public CanCraftObjectsBuffer Recipe;
    public int RecipeIndex, BaseOutput, Calls, CraftAmount, ExpectedOutput;
    public string Where, Key;
    public readonly List<(ObjectID id, int need)> Costs = new List<(ObjectID, int)>();
    public readonly Dictionary<int, ContainedObjectsBuffer> Materials = new Dictionary<int, ContainedObjectsBuffer>();
}

public static class AICraft
{
    public const float StationRange = 2.0f;

    public static bool ParseItem(string text, out ObjectID item)
    {
        string id = (text ?? "").Split('#')[0];
        return Enum.TryParse(id, true, out item) && item != ObjectID.None;
    }
    private static string StationId(Station station) => AIPlaces.ObjectId("station", station.id.ToString(), AIBody.TileOf(station.pos).x, AIBody.TileOf(station.pos).y);
    private static bool Manual(World world, Entity entity) => world.EntityManager.HasComponent<CraftingCD>(entity) && !CraftingCD.IsProcessAutoCrafter(world.EntityManager.GetComponentData<CraftingCD>(entity).craftingType);

    public static List<AILife.Entry> LifeEntries(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        var result = new List<AILife.Entry>();
        var stations = StationsInView(world, player);
        if (string.IsNullOrEmpty(selection.Item))
        {
            var known = new HashSet<ObjectID>(HandRecipes(world, player));
            foreach (var station in stations) if (Manual(world, station.entity)) foreach (var item in station.recipes) known.Add(item);
            foreach (var station in AIMemory.RememberedStations(observation.pos)) foreach (var item in PrefabRecipes(world, station.id)) known.Add(item);
            foreach (var item in known) result.Add(new AILife.Entry { Op = "item", Id = item.ToString(), Text = $"만들 품목 {item} — 알려진 손/제작대 레시피. 비용·실행 조건은 제작대 선택 후 확인" });
            return result;
        }
        if (!ParseItem(selection.Item, out ObjectID wanted)) return result;
        if (string.IsNullOrEmpty(selection.Where))
        {
            if (HandRecipes(world, player).Contains(wanted)) result.Add(new AILife.Entry { Op = "where", Id = "hand", Text = $"손에서 {wanted} 만들기" });
            var inView = new HashSet<string>();
            foreach (var station in stations)
            {
                string id = StationId(station); inView.Add(id);
                if (!station.recipes.Contains(wanted)) continue;
                if (!Manual(world, station.entity)) { result.Add(new AILife.Entry { Id = id, Text = $"{id}: 자동 가공 시설은 일반 제작 실행 범위 밖" }); continue; }
                result.Add(new AILife.Entry { Op = "where", Id = id, Text = $"{id} — 현재 관측, 거리 {station.dist:F1}칸" });
            }
            foreach (var station in AIMemory.RememberedStations(observation.pos))
            {
                string id = AIPlaces.ObjectId("station", station.id.ToString(), station.tile.x, station.tile.y);
                if (inView.Contains(id) || !PrefabRecipes(world, station.id).Contains(wanted)) continue;
                result.Add(new AILife.Entry { Op = "where", Id = id, Text = $"{id} — 화면 밖, {AIMemory.Ago(station.seenUnix)}에 마지막 관측; 현재 레시피/상태 모름. 가서 확인" });
            }
            return result;
        }
        var candidate = ExactCandidate(world, player, observation, currentKey, selection);
        if (candidate != null) result.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate });
        else result.Add(new AILife.Entry { Id = "craft:unavailable", Text = "선택한 제작대의 실제 레시피·비용·재료·출력 공간이 확인되지 않거나 부족함" });
        return result;
    }

    public static AICandidate ExactCandidate(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        if (AILife.RequestSatisfied(selection)) return null;
        if (!ParseItem(selection.Item, out ObjectID item) || string.IsNullOrEmpty(selection.Where)) return null;
        if (observation == null) observation = AIObservation.Take(world, player, false);
        Entity at = Entity.Null; string label = selection.Where;
        if (selection.Where != "hand")
        {
            var station = StationsInView(world, player).Find(s => StationId(s) == selection.Where);
            if (station == null)
            {
                foreach (var memory in AIMemory.RememberedStations(observation.pos))
                {
                    if (AIPlaces.ObjectId("station", memory.id.ToString(), memory.tile.x, memory.tile.y) != selection.Where || !PrefabRecipes(world, memory.id).Contains(item)) continue;
                    var route = AIApproach.FirstCached(world, player, StandTilesFor(world, player, memory), out int2 stand, out _);
                    if (route == null) return null;
                    var walk = AIApproach.WalkCandidate(stand, $"기억한 제작대 {memory.id}", false, $"{selection.Where}로 가서 확인 — {AIMemory.Ago(memory.seenUnix)}에 마지막 관측, 현재 레시피/재료/출력 상태 모름. 도착 뒤 다시 선택(제작 아님)");
                    walk.life = selection.Copy(); return walk;
                }
                return null;
            }
            at = station.entity;
        }
        int requested = selection.RequestedCount > 0 ? selection.RequestedCount : 1;
        if (selection.MatchesGoal && selection.Goal is AIGoal goal && ReferenceEquals(goal, AIGoals.Goal) && goal.kind == GoalKind.Craft && goal.count > 0)
        {
            int mainRemaining = math.max(0, goal.count - math.max(0, MainCount(world, player, item) - goal.startCount));
            if (mainRemaining == 0) return null;
            requested = math.min(requested, mainRemaining);
        }
        if (!ReadRecipe(world, player, at, item, requested, out var recipe, out _)) return null;
        string key = recipe.Key;
        var binding = selection.Copy(); binding.RequestedCount = requested; binding.OfferedCount = recipe.ExpectedOutput;
        return new AICandidate { kind = "craft", key = key, itemId = item, itemCount = recipe.CraftAmount, entity = at, targetLabel = label, craft = recipe, life = binding, progress = selection.MatchesGoal,
            desc = $"만들기 — {item}, {label}. 요청 품목 {requested}개 / 1회 기본 출력 {recipe.BaseOutput}개 / 요청 {recipe.Calls}회(게임 amount={recipe.CraftAmount}), 예상 {recipe.ExpectedOutput}개·초과 {math.max(0, recipe.ExpectedOutput - requested)}개. 실제 비용 {string.Join(", ", recipe.Costs.ConvertAll(x => x.id + " " + x.need))}. 생산과 main 가방 수령은 따로 확인" };
    }

    private static bool SameRecipe(CanCraftObjectsBuffer a, CanCraftObjectsBuffer b) => a.objectID == b.objectID && a.amount == b.amount && a.entityAmountToConsume == b.entityAmountToConsume && a.allowCraftingNone == b.allowCraftingNone && a.craftingTimeOverride == b.craftingTimeOverride;
    public static bool ReadRecipe(World world, Entity player, Entity selectedStation, ObjectID item, int requested, out AICraftSelection result, out string why)
    {
        result = null; why = "실제 제작 규칙 미확인"; var em = world.EntityManager;
        Entity station = selectedStation == Entity.Null ? player : selectedStation;
        if (!em.Exists(station) || !Manual(world, station) || !em.HasBuffer<CanCraftObjectsBuffer>(station)) { why = "제작대/손 레시피가 없거나 자동 가공 시설임"; return false; }
        if (station != player && (!em.HasComponent<LocalTransform>(station) || !AIVision.CanSee(AIBody.Pos(world, player), em.GetComponentData<LocalTransform>(station).Position.xz))) { why = "선택 제작대가 현재 관측되지 않음"; return false; }
        var recipes = em.GetBuffer<CanCraftObjectsBuffer>(station, true); int index = -1;
        for (int i = 0; i < recipes.Length; i++) if (recipes[i].objectID == item) { index = i; break; }
        if (index < 0 || recipes[index].amount <= 0 || recipes[index].entityAmountToConsume != 0) { why = "선택 레시피의 소비/출력 규칙 미확인"; return false; }
        try
        {
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (query.CalculateEntityCount() != 1) return false;
                var bank = query.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                ref var info = ref PugDatabase.GetEntityObjectInfo(item, bank);
                if (info.CraftingSettings.canOnlyUseAnyMaterialsWithTag != ObjectCategoryTag.None || info.initialAmount <= 0) { why = "대체 재료 또는 출력 규칙 미확인"; return false; }
                int baseOutput = info.isStackable ? checked(info.initialAmount * recipes[index].amount) : 1;
                int calls = (math.max(1, requested) + baseOutput - 1) / baseOutput;
                if (!info.isStackable) calls = 1; // a non-stackable cursor can receive only one object per request
                var snap = new AICraftSelection { Station = selectedStation, Item = item, Recipe = recipes[index], RecipeIndex = index, BaseOutput = baseOutput, Calls = calls, CraftAmount = checked(recipes[index].amount * calls), ExpectedOutput = info.isStackable ? checked(baseOutput * calls) : 1 };
                float multiplier = 1f;
                if (em.HasComponent<AnvilCD>(station) || em.HasComponent<ObjectDataCD>(station) && em.GetComponentData<ObjectDataCD>(station).objectID == ObjectID.SalvageAndRepairStation)
                    multiplier -= EntityUtility.GetConditionValue(ConditionID.ReducedAnvilEquipmentCosts, player, world) / 100f;
                ref var costs = ref PugDatabase.GetRequiredObjectsToCraft(item, bank);
                if (costs.Length == 0) { why = "소비 정보 없음"; return false; }
                var shortages = new List<string>();
                for (int i = 0; i < costs.Length; i++)
                {
                    int need = checked((int)math.round(costs[i].amount * multiplier) * snap.CraftAmount);
                    if (need < 0) { why = "실제 소비량 규칙 미확인"; return false; }
                    int have = AIInventory.Available(world, player, costs[i].objectID);
                    if (have < need) shortages.Add($"{costs[i].objectID} {have}/{need}, {need - have}개 부족");
                    snap.Costs.Add((costs[i].objectID, need));
                    for (int slot = AIInventory.MainStart(world, player); slot < AIInventory.MainEnd(world, player); slot++) if (AIInventory.Read(world, player, slot, out var material) && material.objectID == costs[i].objectID) snap.Materials[slot] = material;
                }
                // Metadata is a current game-rule observation even if the request cannot execute yet.
                // Prep consumes these exact costs; it must not multiply item count as though it were craft calls.
                result = snap;
                if (shortages.Count > 0) { why = "실제 재료 부족: " + string.Join(", ", shortages); return false; }
                if (!em.HasComponent<CraftingCD>(player) || !AIInventory.Read(world, player, em.GetComponentData<CraftingCD>(player).outputSlotIndex, out var cursor)) { why = "커서 출력 칸 미확인"; return false; }
                if (cursor.objectID != ObjectID.None || snap.ExpectedOutput > 9999) { why = "커서 출력 공간 부족"; return false; }
                snap.Where = selectedStation == Entity.Null ? "hand" : AIPlaces.ObjectId("station", em.GetComponentData<ObjectDataCD>(station).objectID.ToString(), AIBody.TileOf(em.GetComponentData<LocalTransform>(station).Position).x, AIBody.TileOf(em.GetComponentData<LocalTransform>(station).Position).y);
                snap.Key = $"craft:{item}:v0:{station.Index}.{station.Version}:r{index}:{snap.CraftAmount}:{snap.BaseOutput}";
                result = snap; why = null; return true;
            }
        }
        catch (Exception e) { why = "레시피 규칙 미확인: " + e.Message; return false; }
    }
    public static string Stale(World world, Entity player, AICandidate candidate) => Stale(world, player, candidate.craft);
    public static string Stale(World world, Entity player, AICraftSelection selected)
    {
        if (selected == null) return "요청 시점 제작 레시피 없음";
        if (!ReadRecipe(world, player, selected.Station, selected.Item, selected.ExpectedOutput, out var current, out string why)) return why;
        if (current.RecipeIndex != selected.RecipeIndex || !SameRecipe(current.Recipe, selected.Recipe) || current.BaseOutput != selected.BaseOutput || current.CraftAmount != selected.CraftAmount || current.Costs.Count != selected.Costs.Count) return "선택 레시피의 출력/소비 조건이 바뀜";
        for (int i = 0; i < current.Costs.Count; i++) if (current.Costs[i] != selected.Costs[i]) return "제작 비용이 바뀜";
        foreach (var material in selected.Materials) if (!AIInventory.Read(world, player, material.Key, out var now) || !now.EqualsExact(material.Value)) return "선택 재료 슬롯의 물품/수량/상태가 바뀜";
        return null;
    }
    public static AISkill Make(AICandidate candidate) => new CraftSkill(candidate.craft) { Key = candidate.key };
    public static int MainCount(World world, Entity player, ObjectID item)
    {
        int count = 0;
        for (int i = AIInventory.MainStart(world, player); i < AIInventory.MainEnd(world, player); i++) if (AIInventory.Read(world, player, i, out var value) && value.objectID == item && value.variation == 0) count += AIInventory.Quantity(value);
        return count;
    }

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
public class CraftSkill : AISkill, ILifeResult
{
    private AICraftSelection _recipe;
    private readonly ObjectID _item;
    private readonly int _requested;
    private readonly Entity _station;
    private readonly string _where;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _sent;
    private float _sentAt, _stowAt = -1f;
    private int _before;
    private readonly Dictionary<ObjectID, int> _materialBefore = new Dictionary<ObjectID, int>();
    public int ConfirmedWork { get; private set; }
    public int ConfirmedReceived { get; private set; }
    public int RequestedWork => _recipe?.Calls ?? _requested;
    public string ResultStage { get; private set; } = "미실행";
    public CraftSkill(AICraftSelection recipe)
    {
        _recipe = recipe; _item = recipe?.Item ?? ObjectID.None; _station = recipe?.Station ?? Entity.Null;
        _requested = recipe?.ExpectedOutput ?? 1; _where = recipe?.Where ?? "";
        Name = "만들기"; Key = recipe?.Key ?? "craft:missing";
    }
    // Compatibility for test commands; production candidates carry the frozen recipe overload above.
    public CraftSkill(ObjectID item, int count, Entity station, string where)
    { Name = "만들기"; _item = item; _requested = math.max(1, count); _station = station; _where = where; Key = $"craft:{item}"; }
    public override bool Start(World server, Entity player, float now)
    {
        if (_recipe == null && !AICraft.ReadRecipe(server, player, _station, _item, _requested, out _recipe, out string missing)) { Status = SkillStatus.Failed; Reason = missing; return false; }
        string stale = AICraft.Stale(server, player, _recipe);
        if (stale != null) { Status = SkillStatus.Failed; Reason = stale; return false; }
        _before = AICraft.MainCount(server, player, _item);
        foreach (var cost in _recipe.Costs) _materialBefore[cost.id] = AIInventory.Available(server, player, cost.id);
        Facts.Add($"requested {_recipe.Calls} craft calls; game amount {_recipe.CraftAmount}, base output {_recipe.BaseOutput}; main bag before {_before}; station {_recipe.Station}");
        ResultStage = "실행 전 검증";
        if (_station != Entity.Null)
        {
            float3 position = server.EntityManager.GetComponentData<LocalTransform>(_station).Position;
            if (math.distance(AIBody.Pos(server, player).xz, position.xz) > AICraft.StationRange)
            { _nav.Start(server, player, AIBody.TileOf(position), AICraft.StationRange - 0.4f, now, _station); _navOn = true; }
        }
        return true;
    }
    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!_sent)
        {
            string stale = AICraft.Stale(server, player, _recipe);
            if (stale != null) return Finish(SkillStatus.Failed, stale);
            if (_navOn)
            {
                float3 position = em.GetComponentData<LocalTransform>(_station).Position;
                if (math.distance(AIBody.Pos(server, player).xz, position.xz) <= AICraft.StationRange) { _nav.Dispose(server); _navOn = false; }
                else
                {
                    var status = _nav.Tick(server, player, now, out float2 move);
                    if (status == NavStatus.Failed) return Finish(SkillStatus.Failed, "제작대로 가지 못함: " + _nav.FailReason);
                    if (status == NavStatus.Arrived) { _nav.Dispose(server); _navOn = false; }
                    else return new BodyInput { move = move, slot = -1 };
                }
            }
            if (!AICraft.QueueCraft(server, player, _station == Entity.Null ? player : _station, _item, _recipe.CraftAmount, out string why)) return Finish(SkillStatus.Failed, why);
            _sent = true; _sentAt = now; ResultStage = "제작 요청 접수; 결과 미확인";
            Facts.Add($"normal craft request via {why}; {_item}, amount {_recipe.CraftAmount}; no receipt yet");
            return BodyInput.Idle;
        }
        int main = AICraft.MainCount(server, player, _item);
        int received = math.max(0, main - _before);
        ObjectID cursor = AICraft.CursorItem(server, player);
        int cursorAmount = 0;
        bool matchingCursor = cursor == ObjectID.None;
        if (cursor == _item && em.HasComponent<CraftingCD>(player) && AIInventory.Read(server, player, em.GetComponentData<CraftingCD>(player).outputSlotIndex, out var output) && output.variation == 0)
        { cursorAmount = AIInventory.Quantity(output); matchingCursor = true; }
        if (!matchingCursor)
        {
            ResultStage = "선택 레시피와 다른 커서 출력; 결과 미확인";
            ObserveOnce("different-output", $"선택 레시피의 {_item}(variation0)과 다른 커서 출력 {cursor}. 다른 물품을 요청 결과로 세거나 옮기지 않음");
            return BodyInput.Idle;
        }
        int produced = received + cursorAmount;
        bool consumed = true;
        foreach (var cost in _recipe.Costs) if (_materialBefore[cost.id] - AIInventory.Available(server, player, cost.id) < cost.need) consumed = false;
        if (produced > 0 && consumed)
        {
            ConfirmedReceived = received;
            ConfirmedWork = math.min(_recipe.Calls, produced / math.max(1, _recipe.BaseOutput));
            ResultStage = cursor != ObjectID.None ? "생산 확인; 커서 잔류·가방 수령 대기" : "main 가방 수령 확인";
            if (cursor != ObjectID.None)
            {
                if (_stowAt < 0f) { _stowAt = now; AICraft.StowCursorItem(server, player, out _); Facts.Add($"produced {produced}; cursor {cursor} {cursorAmount}; normal stow request; main received {ConfirmedReceived}"); }
                ObserveOnce("cursor:" + cursorAmount + ":" + ConfirmedReceived, $"커서 {cursor} {cursorAmount}개 잔류, main 가방 확보 {ConfirmedReceived}개. 커서 잔류를 가방 수령/완료로 세지 않음");
                return BodyInput.Idle;
            }
            if (ConfirmedReceived > 0) return Finish(SkillStatus.Done, $"{_item} 실제 main 가방 확보 {ConfirmedReceived}개 / 기본 예상 {_recipe.ExpectedOutput}개, 제작 작업 {ConfirmedWork}/{_recipe.Calls}회; 추가 출력은 실제 확보에 포함");
        }
        if (produced > 0 && !consumed) ObserveOnce("cost-unconfirmed", $"선택 출력 {_item} 증가 {produced}개 관측, 자기 요청의 실제 재료 감소 미확인. 제작 작업/수령 완료로 확정하지 않음");
        if (now - _sentAt > 3f) ObserveOnce("pending", $"제작 요청 후 main 가방 확보 {ConfirmedReceived}개, 커서 {cursor} {cursorAmount}개. 결과 미확인; 시간만으로 강제 취소하지 않음");
        return BodyInput.Idle;
    }
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
