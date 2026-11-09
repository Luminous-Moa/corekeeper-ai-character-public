// Single-item maintenance through the normal game cost helpers and UI requests. No inventory data is written here.
using System;
using System.Collections.Generic;
using Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

public sealed class MaintenanceCost
{
    public ObjectID Item;
    public int Needed, Available;
}

public sealed class MaintenanceRequest
{
    public string Mode, Where, Key;
    public string Owner;
    public World World;
    public LifeSelection Selection;
    public Entity Player, Station;
    public int SourceSlot = -1, InputSlot = -1;
    public bool SourceGear;
    public int2 Tile;
    public ContainedObjectsBuffer BeforeItem, BeforeInput;
    public int BeforeAmount, ExpectedAmount, BeforeLevel, ExpectedLevel;
    public long PendingRun;
    public readonly List<MaintenanceCost> Costs = new List<MaintenanceCost>();
}

public static class AIMaintenance
{
    internal sealed class Pending
    {
        public long Run;
        public MaintenanceRequest Request;
        public ContainedObjectsBuffer Current, ReceivedItem;
        public bool PutConfirmed, Upgraded, TakeConfirmed;
        public NetworkTick PostedTick;
        public bool Posted;
        public string PostedKind = "";
        public CraftActionData Craft;
        public InventoryChangeData Change;
        public AIInventoryMove PutMove, TakeMove;
        public string Observation = "";
        public readonly Dictionary<ObjectID, int> MaterialsBefore = new Dictionary<ObjectID, int>();
    }
    private sealed class State
    {
        public World World;
        public Entity Player;
        public long NextRun;
        public Pending Pending;
        public string LastResult = "";
    }
    private static readonly Dictionary<AIAgent, State> States = new Dictionary<AIAgent, State>();
    private static State Sync(World world, Entity player)
    {
        var owner = AIAgent.Cur;
        if (!States.TryGetValue(owner, out var s) || !ReferenceEquals(s.World, world) || s.Player != player)
            States[owner] = s = new State { World = world, Player = player };
        return s;
    }
    public static void Reset() { States.Remove(AIAgent.Cur); }
    private static ObjectID Facility(string mode) => mode == "repair" ? ObjectID.SalvageAndRepairStation : ObjectID.UpgradeForge;
    private static string Where(ObjectID id, int2 tile) => $"station:{id}@{tile.x},{tile.y}";
    private static bool ItemMatches(string selector, ObjectID item) => string.IsNullOrEmpty(selector) || selector.Split('#')[0] == item.ToString();
    private static bool SameObject(ContainedObjectsBuffer a, ContainedObjectsBuffer b) => a.objectID == b.objectID && a.auxDataIndex == b.auxDataIndex && a.objectID != ObjectID.None;
    private static bool ActiveSlot(World world, Entity player, int slot, out bool gear)
    {
        gear = AIInventory.GearSlots(world, player).Exists(x => x.slot == slot);
        return gear || AIInventory.IsMain(world, player, slot);
    }
    private static Dictionary<ObjectID, int> Main(World world, Entity player)
    {
        var result = new Dictionary<ObjectID, int>();
        for (int slot = AIInventory.MainStart(world, player); slot < AIInventory.MainEnd(world, player); slot++)
            if (AIInventory.Read(world, player, slot, out var item) && item.objectID != ObjectID.None)
            { result.TryGetValue(item.objectID, out int n); result[item.objectID] = n + AIInventory.Quantity(item); }
        return result;
    }
    private static string MaterialChanges(World world, Entity player, Pending pending)
    {
        var now = Main(world, player); var facts = new List<string>();
        foreach (var cost in pending.Request.Costs)
        {
            pending.MaterialsBefore.TryGetValue(cost.Item, out int before); now.TryGetValue(cost.Item, out int after);
            facts.Add($"{cost.Item} main {before}→{after}, 실제 감소 {math.max(0, before - after)}, 요청 비용 {cost.Needed}");
        }
        return string.Join(" / ", facts);
    }
    private static void BeforeMaterials(World world, Entity player, Pending pending)
    {
        pending.MaterialsBefore.Clear();
        var all = Main(world, player);
        foreach (var cost in pending.Request.Costs) { all.TryGetValue(cost.Item, out int amount); pending.MaterialsBefore[cost.Item] = amount; }
    }
    private static bool Station(World world, Entity player, Entity station, ObjectID id, out int2 tile, out int input, out string why)
    {
        tile = default; input = -1; why = "정비 시설 미확인";
        var em = world.EntityManager;
        if (!em.Exists(station) || !em.HasComponent<ObjectDataCD>(station) || em.GetComponentData<ObjectDataCD>(station).objectID != id || !em.HasComponent<LocalTransform>(station)) return false;
        tile = AIBody.TileOf(em.GetComponentData<LocalTransform>(station).Position);
        if (!AIVision.CanSee(AIBody.Pos(world, player), tile)) { why = "선택 정비 시설이 현재 관측 밖"; return false; }
        if (!em.HasComponent<CraftingCD>(station)) { why = "선택 시설의 runtime CraftingCD 없음"; return false; }
        if (id == ObjectID.UpgradeForge)
        {
            if (!em.HasBuffer<InventoryBuffer>(station) || !em.HasBuffer<ContainedObjectsBuffer>(station)) { why = "대장간 입력 인벤토리 미확인"; return false; }
            var inventories = em.GetBuffer<InventoryBuffer>(station, true);
            if (inventories.Length == 0 || inventories[0].size <= 0) { why = "대장간 local0 입력 없음"; return false; }
            input = inventories[0].startIndex;
            if (!AIInventory.Read(world, station, input, out _)) return false;
        }
        why = null; return true;
    }
    private static List<(Entity entity, ObjectID id, int2 tile)> Stations(World world, Entity player, string mode)
    {
        var result = new List<(Entity, ObjectID, int2)>(); var em = world.EntityManager; ObjectID wanted = Facility(mode);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var entities = q.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
                if (em.GetComponentData<ObjectDataCD>(entity).objectID == wanted && Station(world, player, entity, wanted, out var tile, out _, out _)) result.Add((entity, wanted, tile));
        return result;
    }
    // The public game helper supplies costs; this method only copies its result and compares main inventory quantities.
#pragma warning disable CS0618 // The approved runtime entry is the game's public PugQuerySystem singleton/lookup API.
    public static bool ReadCosts(World world, Entity player, Entity station, string mode, ContainedObjectsBuffer item, MaintenanceRequest request, out string why)
    {
        if (mode != "repair" && mode != "upgrade") { why = "이 정비 mode에는 새 비용/강화 요청이 없음"; return false; }
        why = "게임 비용 lookup/DB 미확인";
        try
        {
            var query = world.GetExistingSystemManaged<PugQuerySystem>();
            if (query == null || !query.TryGetSingleton<PugDatabase.DatabaseBankCD>(out var database)) return false;
            var levels = query.GetBufferLookup<LevelEntitiesBuffer>(true);
            var durability = query.GetComponentLookup<DurabilityCD>(true);
            var level = query.GetComponentLookup<LevelCD>(true);
            var contained = query.GetBufferLookup<ContainedObjectsBuffer>(true);
            var inventoriesLookup = query.GetBufferLookup<InventoryBuffer>(true);
            Entity primary = PugDatabase.GetPrimaryPrefabEntity(item.objectID, database.databaseBankBlob, item.variation);
            if (primary == Entity.Null) return false;
            using (var inventories = new NativeList<Entity>(Allocator.Temp))
            {
                inventories.Add(player);
                if (mode == "repair")
                {
                    if (!durability.TryGetComponent(primary, out var d) || !InventoryUtility.CanBeRepaired(primary, item, levels, durability, false)) { why = "장비가 일반 수리 대상이 아니거나 이미 최대 내구도"; return false; }
                    request.BeforeAmount = item.amount; request.ExpectedAmount = d.maxDurability;
                    if (item.amount >= d.maxDurability) { why = "이미 최대 내구도"; return false; }
                    using (var costs = InventoryUtility.GetCraftingMaterialInfosForRecipe(database, contained, inventoriesLookup,
                        query.GetComponentLookup<AnvilCD>(true), query.GetComponentLookup<ObjectDataCD>(true), query.GetBufferLookup<SummarizedConditionsBuffer>(true),
                        durability, query.GetComponentLookup<PrioritizedRepairMaterialCD>(true), level,
                        new ObjectWithAmount { objectID = item.objectID, amount = item.amount }, default, inventories, 1, true, false, station, player, Allocator.Temp))
                    { if (!costs.IsCreated) return false; CopyCosts(world, player, request, costs); }
                }
                else
                {
                    if (!InventoryUtility.CanBeUpgraded(levels, query.GetComponentLookup<ObjectCategoryTagsCD>(true), primary) || !level.TryGetComponent(primary, out var l)) { why = "장비의 레벨 업그레이드 규칙 미확인"; return false; }
                    request.BeforeLevel = item.variation > 0 ? item.variation : l.level; request.ExpectedLevel = request.BeforeLevel + 1;
                    if (request.BeforeLevel >= LevelScaling.GetMaxLevel()) { why = "이미 최대 레벨"; return false; }
                    if (!query.TryGetSingleton<UpgradeCostsTableCD>(out var table)) { why = "업그레이드 비용표 미확인"; return false; }
                    using (var costs = InventoryUtility.GetCraftingMaterialInfosForUpgrade(contained, inventoriesLookup, database, table, request.ExpectedLevel, inventories, 1, Allocator.Temp))
                    { if (!costs.IsCreated) return false; CopyCosts(world, player, request, costs); }
                }
            }
            why = null; return true;
        }
        catch (Exception e) { why = "게임 비용 미확인: " + e.GetType().Name; return false; }
    }
    private static void CopyCosts(World world, Entity player, MaintenanceRequest request, NativeList<PugDatabase.MaterialInfoData> data)
    {
        request.Costs.Clear(); var main = Main(world, player); var needs = new Dictionary<ObjectID, int>();
        for (int i = 0; i < data.Length; i++) { needs.TryGetValue(data[i].objectID, out int n); needs[data[i].objectID] = n + data[i].amountNeeded; }
        var ids = new List<ObjectID>(needs.Keys); ids.Sort((a, b) => ((int)a).CompareTo((int)b));
        foreach (var id in ids) { main.TryGetValue(id, out int amount); request.Costs.Add(new MaintenanceCost { Item = id, Needed = needs[id], Available = amount }); }
    }
    private static bool CostsEqual(MaintenanceRequest a, MaintenanceRequest b)
    {
        if (a.Costs.Count != b.Costs.Count || a.ExpectedAmount != b.ExpectedAmount || a.BeforeLevel != b.BeforeLevel || a.ExpectedLevel != b.ExpectedLevel) return false;
        for (int i = 0; i < a.Costs.Count; i++) if (a.Costs[i].Item != b.Costs[i].Item || a.Costs[i].Needed != b.Costs[i].Needed) return false;
        return true;
    }
    private static string Missing(MaintenanceRequest request)
    {
        var missing = new List<string>();
        foreach (var cost in request.Costs) if (cost.Available < cost.Needed) missing.Add($"{cost.Item} {cost.Needed - cost.Available}개 부족(main {cost.Available}/{cost.Needed})");
        return missing.Count == 0 ? null : string.Join(", ", missing);
    }
    private static string CostsText(MaintenanceRequest request) => request.Costs.Count == 0 ? "게임 helper 비용0" : string.Join(", ", request.Costs.ConvertAll(c => $"{c.Item} {c.Needed}(main {c.Available})"));
    private static string Identity(ContainedObjectsBuffer item) => $"{item.objectID}:v{item.variation}:a{item.auxDataIndex}:n{item.amount}:u{item.variationUpdateCount}";
    private static bool Eligible(World world, Entity player, int slot, string mode, out ContainedObjectsBuffer item, out bool gear)
    {
        item = default;
        if (!ActiveSlot(world, player, slot, out gear) || !AIInventory.Read(world, player, slot, out item) || item.objectID == ObjectID.None || AIInventory.Info(item)?.isStackable != false) return false;
        try
        {
            var q = world.GetExistingSystemManaged<PugQuerySystem>();
            if (q == null || !q.TryGetSingleton<PugDatabase.DatabaseBankCD>(out var bank)) return false;
            var primary = PugDatabase.GetPrimaryPrefabEntity(item.objectID, bank.databaseBankBlob, item.variation);
            return mode == "repair" ? InventoryUtility.CanBeRepaired(primary, item, q.GetBufferLookup<LevelEntitiesBuffer>(true), q.GetComponentLookup<DurabilityCD>(true), false)
                : InventoryUtility.CanBeUpgraded(q.GetBufferLookup<LevelEntitiesBuffer>(true), q.GetComponentLookup<ObjectCategoryTagsCD>(true), primary);
        }
        catch { return false; }
    }
#pragma warning restore CS0618
    public static string CheckGoal(World world, Entity player, ObjectID item, int slot, string mode, string where)
    {
        if (!AIMaintenanceRules.Mode(mode)) return "정비 mode는 repair/upgrade/recover만 가능";
        var state = Sync(world, player); ObservePending(world, player, state);
        if (mode == "recover") return state.Pending?.PutConfirmed == true && (item == ObjectID.None || item == state.Pending.Request.BeforeItem.objectID)
            && (slot < 0 || slot == state.Pending.Request.SourceSlot) && (string.IsNullOrEmpty(where) || where == state.Pending.Request.Where)
            ? null : "현재 캐릭터가 정상 투입을 확인한 해당 장비/시설의 pending 물품 없음";
        if (slot >= 0)
        {
            if (!ActiveSlot(world, player, slot, out _) || !AIInventory.Read(world, player, slot, out var selected) || selected.objectID == ObjectID.None || item != ObjectID.None && selected.objectID != item) return "지정한 현재 장비 슬롯/품목이 일치하지 않음";
        }
        else if (item != ObjectID.None)
        {
            bool found = false; var slots = SourceSlots(world, player);
            foreach (int source in slots) if (AIInventory.Read(world, player, source, out var selected) && selected.objectID == item) { found = true; break; }
            if (!found) return "지정한 장비 품목이 main/활성 장비 슬롯에 없음";
        }
        if (!string.IsNullOrEmpty(where))
        {
            string prefix = "station:" + Facility(mode) + "@";
            if (!where.StartsWith(prefix, StringComparison.Ordinal)) return "정비 작업과 지정 시설의 종류가 다름";
            bool known = Stations(world, player, mode).Exists(x => Where(x.id, x.tile) == where);
            foreach (var memory in AIMemory.RememberedStations(AIBody.Pos(world, player))) if (Where(memory.id, memory.tile) == where) known = true;
            if (!known) return "지정 정비 시설은 현재 관측/기존 기억에 없음";
        }
        return null;
    }
    private static List<int> SourceSlots(World world, Entity player)
    {
        var slots = new List<int>();
        for (int i = AIInventory.MainStart(world, player); i < AIInventory.MainEnd(world, player); i++) slots.Add(i);
        foreach (var gear in AIInventory.GearSlots(world, player)) if (!slots.Contains(gear.slot)) slots.Add(gear.slot);
        slots.Sort(); return slots;
    }
    public static List<AILife.Entry> LifeEntries(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        var s = Sync(world, player); ObservePending(world, player, s); var result = new List<AILife.Entry>();
        if (string.IsNullOrEmpty(selection.Mode))
        {
            result.Add(new AILife.Entry { Op = "mode", Id = "repair", Text = "일반 수리 — 장비 하나의 실제 내구도/비용 확인(내구 강화 아님)" });
            result.Add(new AILife.Entry { Op = "mode", Id = "upgrade", Text = "레벨 업그레이드 — 정확 장비 하나를 대장간에 넣어 한 단계 올리고 main 가방으로 회수" });
            if (s.Pending?.PutConfirmed == true) result.Add(new AILife.Entry { Op = "mode", Id = "recover", Text = "자기 pending 회수 — 게시 요청 처리 확인 뒤 정확 투입물만 회수" });
            if (s.Pending != null) result.Add(new AILife.Entry { Text = PendingText(s.Pending) });
            return result;
        }
        if (!AIMaintenanceRules.Mode(selection.Mode)) { result.Add(new AILife.Entry { Text = "알 수 없는 정비 mode; repair/upgrade/recover만 지원" }); return result; }
        if (selection.Slot < 0)
        {
            if (selection.Mode == "recover")
            {
                if (s.Pending?.PutConfirmed == true) result.Add(new AILife.Entry { Op = "slot", Id = s.Pending.Request.SourceSlot.ToString(), Text = $"자기 pending: 원래 장비 슬롯 {s.Pending.Request.SourceSlot}, {AIInventory.Text(s.Pending.Current)}. {PendingText(s.Pending)}" });
                else result.Add(new AILife.Entry { Text = "자기 투입을 확인한 pending 없음; 기존 대장간 물품을 채택하지 않음" });
                return result;
            }
            foreach (int slot in SourceSlots(world, player))
                if (Eligible(world, player, slot, selection.Mode, out var item, out bool gear))
                {
                    var resolved = selection.Copy(); resolved.Item = item.objectID.ToString(); resolved.Slot = slot; resolved = AILife.Resolve(resolved);
                    result.Add(new AILife.Entry { Op = "slot", Id = slot.ToString(), Text = $"{(gear ? "활성 착용" : "main 가방")} 슬롯 {slot}: {AIInventory.Text(item)} — {(resolved.MatchesGoal ? "현재 목표의 정확 장비 선택" : "원래 단계 밖의 별도 행동1회; 목표 진척 제외")}. 시설 선택 후 실제 효과/비용 확인" });
                }
            if (result.Count == 0) result.Add(new AILife.Entry { Text = "현재 main/활성 장비 슬롯에서 해당 정비 규칙이 확인된 장비 없음" });
            return result;
        }
        if (string.IsNullOrEmpty(selection.Where))
        {
            if (selection.Mode == "recover")
            {
                if (s.Pending?.PutConfirmed == true) result.Add(new AILife.Entry { Op = "where", Id = s.Pending.Request.Where, Text = s.Pending.Request.Where + " — 자신의 정확 pending 위치; " + PendingText(s.Pending) });
                return result;
            }
            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var station in Stations(world, player, selection.Mode))
            {
                string id = Where(station.id, station.tile); visible.Add(id);
                result.Add(new AILife.Entry { Op = "where", Id = id, Text = $"{id} — 현재 관측, 거리 {math.distance(observation.pos.xz, (float2)station.tile):F1}칸" });
            }
            foreach (var memory in AIMemory.RememberedStations(observation.pos))
            {
                string id = Where(memory.id, memory.tile);
                if (memory.id != Facility(selection.Mode) || visible.Contains(id)) continue;
                result.Add(new AILife.Entry { Op = "where", Id = id, Text = $"{id} — 화면 밖, {AIMemory.Ago(memory.seenUnix)} 마지막 관측; 현재 입력/비용 상태 모름, 가서 확인" });
            }
            if (result.Count == 0) result.Add(new AILife.Entry { Text = "해당 수리대/대장간이 현재 관측 또는 기존 제작대 기억에 없음" });
            return result;
        }
        var candidate = ExactCandidate(world, player, observation, currentKey, selection);
        if (candidate != null) result.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate });
        else result.Add(new AILife.Entry { Text = s.LastResult == "" ? "선택 정비 조건 미확인; 현재 장비/시설/재료/자기 pending을 다시 확인" : s.LastResult });
        return result;
    }
    public static AICandidate ExactCandidate(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        var s = Sync(world, player); ObservePending(world, player, s);
        if (selection == null || !AIMaintenanceRules.Mode(selection.Mode)) return null;
        if (s.Pending != null)
        {
            if (selection.Mode == "recover" || selection.Mode == "upgrade" && s.Pending.Upgraded && SameOriginalGoal(s.Pending)) return Recovery(world, player, selection, s);
            s.LastResult = PendingText(s.Pending) + "; 새 정비/새 장비 투입은 기존 요청 확인·명시 회수 후 선택"; return null;
        }
        if (selection.Mode == "recover" || selection.Slot < 0 || string.IsNullOrEmpty(selection.Where)) return null;
        if (!Eligible(world, player, selection.Slot, selection.Mode, out var item, out bool gear)) { s.LastResult = "정확한 현재 main/활성 장비 슬롯의 대상/정비 규칙이 없음"; return null; }
        var chosen = selection.Copy(); chosen.Item = item.objectID.ToString(); chosen = AILife.Resolve(chosen);
        if (AILife.RequestSatisfied(chosen)) { s.LastResult = "현재 정비 요청량1의 실제 결과가 이미 확인됨"; return null; }
        if (observation == null) observation = AIObservation.Take(world, player, false);
        var stations = Stations(world, player, chosen.Mode); var at = stations.Find(x => Where(x.id, x.tile) == chosen.Where);
        if (at.entity == Entity.Null)
        {
            foreach (var memory in AIMemory.RememberedStations(observation.pos))
            {
                if (memory.id != Facility(chosen.Mode) || Where(memory.id, memory.tile) != chosen.Where || AIVision.CanSee(observation.pos, memory.tile)) continue;
                var route = AIApproach.FirstCached(world, player, AICraft.StandTilesFor(world, player, memory), out int2 stand, out _);
                if (route == null) return null;
                var move = AIApproach.WalkCandidate(stand, "기억한 정비 시설", false, $"{chosen.Where}로 가서 확인 — {AIMemory.Ago(memory.seenUnix)} 마지막 관측; 현재 입력·비용 상태 모름. 도착 뒤 정비를 다시 선택"); move.life = chosen; return move;
            }
            s.LastResult = "선택 시설의 현재 모습 또는 기존 기억 없음"; return null;
        }
        if (!Station(world, player, at.entity, Facility(chosen.Mode), out var tile, out int input, out string why)) { s.LastResult = why; return null; }
        var request = new MaintenanceRequest { Mode = chosen.Mode, Selection = chosen.Copy(), World = world, Owner = AIAgent.Cur.Id, Player = player, SourceSlot = chosen.Slot, SourceGear = gear, BeforeItem = item, Station = at.entity, Tile = tile, Where = chosen.Where, InputSlot = input };
        if (!ReadCosts(world, player, at.entity, chosen.Mode, item, request, out why)) { s.LastResult = why; return null; }
        why = Missing(request); if (why != null) { s.LastResult = $"{chosen.Where} 장비 {AIInventory.Text(item)}: 실제 비용 {CostsText(request)}; {why}. 자기 상자 꺼내기/준비 또는 request_plan 필요"; return null; }
        if (chosen.Mode == "upgrade")
        {
            if (!AIInventory.Read(world, at.entity, input, out request.BeforeInput) || request.BeforeInput.objectID != ObjectID.None) { s.LastResult = "대장간 local0에 기존 물품이 있음; 옮기거나 자기 pending으로 채택하지 않음"; return null; }
            if (AIInventory.Destination(world, player, item, true, out int room) < 0 || room < 1) { s.LastResult = "업그레이드 전 main 가방 회수 공간 없음"; return null; }
            if (!AIInventory.CanRemove(world, player, chosen.Slot)) { s.LastResult = "선택 장비 출발 슬롯 꺼내기 제한"; return null; }
            why = AIInventory.CapacityAfterRemoval(world, player, chosen.Slot); if (why != null) { s.LastResult = why; return null; }
        }
        string stage = chosen.Mode == "repair" ? "max" + request.ExpectedAmount : $"level{request.BeforeLevel}>{request.ExpectedLevel}";
        request.Key = $"maintain:{chosen.Mode}:{player.Index}.{player.Version}:s{chosen.Slot}:{Identity(item)}:{at.entity.Index}.{at.entity.Version}:i{input}:{stage}:one";
        chosen.OfferedCount = 1;
        string effect = chosen.Mode == "repair" ? $"내구도 {request.BeforeAmount}→{request.ExpectedAmount}" : $"레벨 {request.BeforeLevel}→{request.ExpectedLevel}, 대장간 투입 뒤 main 회수까지";
        return new AICandidate { kind = "maintain", key = request.Key, desc = $"장비 정비 {chosen.Mode} — 슬롯 {chosen.Slot} {AIInventory.Text(item)}, {chosen.Where}, {effect}. 실제 비용 {CostsText(request)}. 거리 {math.distance(observation.pos.xz, (float2)tile):F1}칸. 요청1회; 정상 요청 처리와 장비/재료/가방 결과를 구별해 확인", entity = at.entity, tile = tile, itemId = item.objectID, itemVariation = item.variation, itemCount = 1, life = chosen, maintenance = request, progress = chosen.MatchesGoal };
    }
    private static bool SameOriginalGoal(Pending p) => p.Request.Selection?.Goal != null && ReferenceEquals(p.Request.Selection.Goal, AIGoals.Goal) && p.Request.Selection.GoalRevision == AIGoals.GoalRevision;
    private static AICandidate Recovery(World world, Entity player, LifeSelection selection, State s)
    {
        var p = s.Pending;
        if (p == null || !p.PutConfirmed || selection.Slot >= 0 && selection.Slot != p.Request.SourceSlot || !string.IsNullOrEmpty(selection.Where) && selection.Where != p.Request.Where || !ItemMatches(selection.Item, p.Request.BeforeItem.objectID)) { s.LastResult = "선택 조건에 맞는 자기 투입 pending 없음"; return null; }
        if (!Station(world, player, p.Request.Station, ObjectID.UpgradeForge, out _, out _, out string why)) { s.LastResult = why; return null; }
        if (!p.TakeConfirmed && (why = QueueReady(world, player, p, true)) != null) { s.LastResult = why; return null; }
        var chosen = selection.Copy(); chosen.Item = p.Request.BeforeItem.objectID.ToString(); chosen.Slot = p.Request.SourceSlot; chosen.Where = p.Request.Where; chosen.Mode = "recover";
        bool finishes = AIMaintenanceRules.RecoveryCompletesUpgrade(p.Upgraded, SameOriginalGoal(p), p.Request.Selection.GoalRevision, AIGoals.GoalRevision);
        if (finishes) chosen = p.Request.Selection.Copy();
        chosen = AILife.Resolve(chosen); chosen.OfferedCount = 1;
        if (AILife.RequestSatisfied(chosen)) return null;
        var request = new MaintenanceRequest { Mode = "recover", Selection = chosen.Copy(), World = world, Owner = AIAgent.Cur.Id, Player = player, SourceSlot = p.Request.SourceSlot, SourceGear = p.Request.SourceGear, BeforeItem = p.Request.BeforeItem, BeforeInput = p.Current, Station = p.Request.Station, Tile = p.Request.Tile, Where = p.Request.Where, InputSlot = p.Request.InputSlot, BeforeLevel = p.Request.BeforeLevel, ExpectedLevel = p.Request.ExpectedLevel, PendingRun = p.Run };
        request.Key = $"maintain:recover:{player.Index}.{player.Version}:p{p.Run}:{Identity(p.Current)}:{request.Station.Index}.{request.Station.Version}:i{request.InputSlot}:level{request.BeforeLevel}>{request.ExpectedLevel}:one";
        return new AICandidate { kind = "maintain", key = request.Key, entity = request.Station, tile = request.Tile, itemId = p.Current.objectID, itemVariation = p.Current.variation, itemCount = 1, life = chosen, maintenance = request, progress = chosen.MatchesGoal,
            desc = $"자기 pending 회수 — {p.Request.Where} local0(절대 {p.Request.InputSlot}), {AIInventory.Text(p.Current)}. {(finishes ? "이미 강화된 정확 물품을 받아 현재 업그레이드 단계 마무리; 강화 재요청 없음" : p.Upgraded ? "확인된 업그레이드 물품 회수; 이전/새 upgrade 목표 진척에는 합산하지 않음" : "강화되지 않은 원본 회수; upgrade 성공 아님")}. {PendingText(p)}" };
    }
    public static string Stale(World world, Entity player, AICandidate candidate) => candidate.maintenance == null ? "정비 요청 payload 없음" : Stale(world, player, candidate.maintenance);
    internal static string Stale(World world, Entity player, MaintenanceRequest request)
    {
        if (request == null || request.Player != player || request.Owner != AIAgent.Cur.Id || !ReferenceEquals(request.World, world)) return "정비 요청 월드/캐릭터/플레이어 신원 변경";
        if (!AIMaintenanceRules.Mode(request.Mode)) return "정비 mode가 유효하지 않음";
        var s = Sync(world, player); ObservePending(world, player, s);
        string common = AILife.ExecuteStale(new AICandidate { life = request.Selection }); if (common != null) return common;
        if (request.Mode == "recover")
        {
            if (s.Pending == null || s.Pending.Run != request.PendingRun || !s.Pending.PutConfirmed) return "정확한 자기 pending이 바뀜";
            if (!Station(world, player, request.Station, ObjectID.UpgradeForge, out var tile, out int input, out string why) || math.any(tile != request.Tile) || input != request.InputSlot) return why ?? "선택 대장간 위치/입력 변경";
            return s.Pending.TakeConfirmed ? null : QueueReady(world, player, s.Pending, true);
        }
        if (s.Pending != null) return "기존 자기 정비 요청/투입물의 처리가 남아 있음";
        if (!ActiveSlot(world, player, request.SourceSlot, out bool gear) || gear != request.SourceGear || !AIInventory.Read(world, player, request.SourceSlot, out var item) || !item.EqualsExact(request.BeforeItem)) return "선택 장비의 슬롯/활성 프리셋/variation/aux/상태가 바뀜";
        if (!Station(world, player, request.Station, Facility(request.Mode), out var t, out int at, out string reason) || math.any(t != request.Tile) || at != request.InputSlot) return reason ?? "선택 정비 시설 위치/입력 변경";
        string gate = AIInventory.Gate(world, player); if (gate != null) return gate;
        var costs = new MaintenanceRequest();
        if (!ReadCosts(world, player, request.Station, request.Mode, item, costs, out reason)) return reason;
        if (!CostsEqual(request, costs)) return "선택 뒤 실제 정비 비용/효과가 바뀜";
        reason = Missing(costs); if (reason != null) return reason;
        if (request.Mode == "upgrade")
        {
            if (!AIInventory.Read(world, request.Station, request.InputSlot, out var input) || !input.EqualsExact(request.BeforeInput) || input.objectID != ObjectID.None) return "선택 대장간 입력이 바뀜; 기존 물품을 대체하지 않음";
            if (AIInventory.Destination(world, player, item, true, out int room) < 0 || room < 1) return "main 회수 공간이 사라짐";
            if (!AIInventory.CanRemove(world, player, request.SourceSlot)) return "출발 슬롯 꺼내기 제한";
            return AIInventory.CapacityAfterRemoval(world, player, request.SourceSlot);
        }
        return null;
    }
    public static AISkill Make(AICandidate candidate) => new MaintenanceSkill(candidate.maintenance) { Key = candidate.key };
    private static string PendingText(Pending p) => $"정비 run{p.Run}, {p.Request.Mode}, {p.Request.Where}, 원래 슬롯 {p.Request.SourceSlot}, 투입 {(p.PutConfirmed ? "양쪽 확인" : "미확인")}, 레벨 변화 {(p.Upgraded ? "확인" : "미확인")}, main 회수 {(p.TakeConfirmed ? "확인" : "미확인")}, 게시 {p.PostedKind}/{(p.Posted ? p.PostedTick.ToString() : "없음")}. {p.Observation}";
    public static string Report(World world, Entity player)
    {
        var s = Sync(world, player); ObservePending(world, player, s);
        return "장비 정비: " + (s.Pending == null ? "자기 pending 없음. " : PendingText(s.Pending) + " ") + s.LastResult + ". 일반 수리/단일 레벨 업그레이드만 지원; 임시 pending은 월드 재입장 뒤 복원되지 않음";
    }
    public static string StallNote(World world, Entity player) => Report(world, player);
    public static bool GoalDone(World world, Entity player, out string note)
    {
        note = Report(world, player);
        if (AIGoals.Goal.kind != GoalKind.Maintenance) return false;
        var selected = AILife.ForGoal(AIGoals.Goal);
        bool done = AILife.RequestSatisfied(selected);
        if (done) note = "정확 장비 정비 작업1의 실제 결과 확인. " + note;
        return done;
    }
    private static bool SameChange(InventoryChangeData a, InventoryChangeData b) => a.inventoryAction == b.inventoryAction && a.inventory1 == b.inventory1 && a.entityOrInventory2 == b.entityOrInventory2 && a.index1 == b.index1 && a.index2 == b.index2;
    private static bool SameCraft(CraftActionData a, CraftActionData b) => a.craftAction == b.craftAction && a.targetInventoryEntity == b.targetInventoryEntity && a.int0 == b.int0 && a.craftingEntity == b.craftingEntity;
    private static string QueueReady(World world, Entity player, Pending p, bool exactInput)
    {
        if (!p.Posted) return exactInput && !ObserveExactInput(world, p) ? "정확한 자기 pending 입력을 현재 확인하지 못함" : null;
        var em = world.EntityManager;
        try
        {
            em.CompleteDependencyBeforeRO<UIActionsCD>();
            if (!em.HasComponent<UIActionsCD>(player)) return "요청 처리 미확인; UIActionsCD 없음, 회수 대기";
            NetworkTick current;
            using (var time = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>()))
            { if (time.CalculateEntityCount() != 1) return "요청 처리 미확인; server tick 없음, 회수 대기"; current = time.GetSingleton<NetworkTime>().ServerTick; }
            var applied = em.GetComponentData<UIActionsCD>(player).lastActionTick;
            bool pending = false, read = false;
            if (p.PostedKind == "craft")
            {
                em.CompleteDependencyBeforeRO<CraftBuffer>();
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CraftBuffer>()))
                using (var entities = q.ToEntityArray(Allocator.Temp))
                    foreach (var entity in entities) { read = true; foreach (var entry in em.GetBuffer<CraftBuffer>(entity, true)) if (entry.playerEntity == player && SameCraft(entry.craftActionData, p.Craft)) pending = true; }
            }
            else
            {
                em.CompleteDependencyBeforeRO<InventoryChangeBuffer>();
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<InventoryChangeBuffer>()))
                using (var entities = q.ToEntityArray(Allocator.Temp))
                    foreach (var entity in entities) { read = true; foreach (var entry in em.GetBuffer<InventoryChangeBuffer>(entity, true)) if (entry.playerEntity == player && SameChange(entry.inventoryChangeData, p.Change)) pending = true; }
            }
            bool input = !exactInput || ObserveExactInput(world, p);
            if (!read) return "요청 처리 미확인; 소비 버퍼를 읽을 수 없음, 회수 대기";
            return AIMaintenanceRules.RecoveryReady(current.IsValid && current.IsNewerThan(p.PostedTick), applied.IsValid && !p.PostedTick.IsNewerThan(applied), pending, input);
        }
        catch (Exception e) { return "요청 처리 미확인; " + e.GetType().Name + ", 회수 대기"; }
    }
    private static bool ObserveExactInput(World world, Pending p) => p.PutConfirmed
        && Station(world, p.Request.Player, p.Request.Station, ObjectID.UpgradeForge, out var tile, out int input, out _)
        && math.all(tile == p.Request.Tile) && input == p.Request.InputSlot
        && AIInventory.Read(world, p.Request.Station, p.Request.InputSlot, out var current) && current.EqualsExact(p.Current);
    private static int TransferAmount(World world, AIInventoryMove move)
    {
        if (move == null || !AIInventory.Read(world, move.fromEntity, move.from, out var from) || !AIInventory.Read(world, move.toEntity, move.to, out var to)) return 0;
        int beforeTo = AIInventory.Identity(move.beforeTo, move.beforeFrom) ? AIInventory.Quantity(move.beforeTo) : 0;
        int afterTo = AIInventory.Identity(to, move.beforeFrom) ? AIInventory.Quantity(to) : 0;
        return AIInventoryRules.ObservedTransfer(AIInventory.Stamp(move.beforeFrom), AIInventory.Stamp(from), beforeTo, afterTo, 1);
    }
    private static void ObservePending(World world, Entity player, State s)
    {
        var p = s.Pending; if (p == null) return;
        if (p.Request.Mode == "repair")
        {
            if (!p.Posted || QueueReady(world, player, p, false) != null) return;
            // A repair targets this player's own slot. Once processing is proven, no station inventory is read.
            bool stationVisible = Station(world, player, p.Request.Station, ObjectID.SalvageAndRepairStation, out _, out _, out string stationWhy);
            bool repaired = ConfirmRepair(world, player, p.Request, new PendingHandle(p), out string fact);
            s.LastResult = (repaired ? "게시한 일반 수리의 실제 내구도 변화 확인" : "게시한 일반 수리 요청 소비 확인; target 변화 미확인") + ". " + fact
                + (stationVisible ? "" : ". 시설 현재 관측: " + stationWhy + "; 시설 buffer는 읽지 않음");
            // The original skill records its own ILifeResult, if still running. Observation never records a second run.
            s.Pending = null;
            return;
        }
        if (!Station(world, player, p.Request.Station, ObjectID.UpgradeForge, out var observedTile, out int observedInput, out string observationWhy)
            || math.any(observedTile != p.Request.Tile) || observedInput != p.Request.InputSlot)
        { p.Observation = (observationWhy ?? "선택 시설 위치/입력 변경") + "; 마지막 pending 관측만 보존, 현재 내용 모름"; return; }
        if (!p.PutConfirmed && p.PutMove != null && p.Posted && p.PostedKind == "put" && QueueReady(world, player, p, false) == null && TransferAmount(world, p.PutMove) == 1)
        { p.PutConfirmed = true; if (AIInventory.Read(world, p.Request.Station, p.Request.InputSlot, out var put)) p.Current = put; p.Observation = "정상 투입의 출발 감소/대장간 증가1 확인"; }
        if (!p.PutConfirmed) return;
        if (p.PostedKind == "craft" && AIInventory.Read(world, p.Request.Station, p.Request.InputSlot, out var item) && SameObject(item, p.Request.BeforeItem))
        {
            if (p.Upgraded)
            {
                if (!item.EqualsExact(p.Current)) p.Observation = "이미 확인한 업그레이드 물품의 정확 variation/내구도/상태 stamp가 바뀜; pending 재채택/회수 차단";
            }
            else if (item.variation == p.Request.ExpectedLevel) { p.Current = item; p.Upgraded = true; p.Observation = $"레벨 {p.Request.BeforeLevel}→{item.variation} 변화 확인, 가방 미회수. {MaterialChanges(world, player, p)}"; }
            else if (item.variation == p.Request.BeforeItem.variation) p.Current = item;
            else p.Observation = "예상과 다른 레벨 변화; 임의 재강화/회수 대상 대체 없음";
        }
        if (!p.TakeConfirmed && p.TakeMove != null && p.PostedKind == "take" && QueueReady(world, player, p, false) == null && TransferAmount(world, p.TakeMove) == 1
            && AIInventory.Read(world, p.TakeMove.toEntity, p.TakeMove.to, out var received) && NormalMoveReceipt(p.TakeMove, received) && p.TakeMove.beforeFrom.EqualsExact(p.Current))
        { p.ReceivedItem = received; p.TakeConfirmed = true; p.Observation = "자기 pending 출발 감소와 main 가방 정확 snapshot 수령1 확인"; }
    }
    public static string StaleMove(World world, Entity player, AIInventoryMove move)
    {
        var s = Sync(world, player); var p = s.Pending;
        if (move.maintenance == null || p == null || move.maintenance.PendingRun != p.Run || move.chest != p.Request.Station) return "정비 이동의 자기 pending/시설 binding 불일치";
        if (!Station(world, player, move.chest, ObjectID.UpgradeForge, out var tile, out int input, out string why) || math.any(tile != p.Request.Tile) || input != p.Request.InputSlot) return why ?? "대장간 위치/입력 변경";
        if (move.mode == "maintenance_put")
        {
            if (!ReferenceEquals(move, p.PutMove) || p.PutConfirmed) return "새 물품 투입을 허용할 pending 상태가 아님";
            if (!ActiveSlot(world, player, move.from, out bool gear) || gear != p.Request.SourceGear) return "투입 장비의 활성 프리셋/슬롯 변경";
            return null;
        }
        if (!p.PutConfirmed || !ReferenceEquals(move, p.TakeMove) || p.TakeConfirmed) return "회수할 정확 자기 pending 상태가 아님";
        return QueueReady(world, player, p, true);
    }
    public static string SlotBarrier(World world, Entity player, AIInventoryMove move)
    {
        var s = Sync(world, player); var p = s.Pending; if (p == null || !p.Posted) return null;
        if (ReferenceEquals(move, p.PutMove) || ReferenceEquals(move, p.TakeMove)) return null;
        if (QueueReady(world, player, p, false) == null) return null;
        bool source = move.fromEntity == player && move.from == p.Request.SourceSlot || move.toEntity == player && move.to == p.Request.SourceSlot;
        bool forge = move.fromEntity == p.Request.Station && move.from == p.Request.InputSlot || move.toEntity == p.Request.Station && move.to == p.Request.InputSlot;
        bool transfer = p.PostedKind != "craft" && (Touches(move, p.Change.inventory1, p.Change.index1) || Touches(move, p.Change.entityOrInventory2, p.Change.index2));
        return source || forge || transfer ? "자기 정비 요청 처리 미확인; 해당 정확 슬롯 교환/이동 대기" : null;
    }
    private static bool Touches(AIInventoryMove move, Entity inventory, int slot) => move.fromEntity == inventory && move.from == slot || move.toEntity == inventory && move.to == slot;
    public static void MovePosted(World world, Entity player, AIInventoryMove move, NetworkTick tick, InventoryChangeData data)
    {
        var s = Sync(world, player); var p = s.Pending;
        if (p == null || move.maintenance == null || move.maintenance.PendingRun != p.Run) return;
        p.Posted = true; p.PostedTick = tick; p.PostedKind = move.mode == "maintenance_put" ? "put" : "take"; p.Change = data; p.Observation = "정상 이동 요청 게시; 처리/양쪽 이동 미확인";
    }
    public static string MoveReceiptReady(World world, Entity player, AIInventoryMove move)
    {
        var s = Sync(world, player); var p = s.Pending;
        if (p == null || move.maintenance?.PendingRun != p.Run) return "정비 이동 pending이 바뀜";
        if (move.mode == "maintenance_take" && (!move.beforeFrom.EqualsExact(p.Current)
            || !AIInventory.Read(world, move.toEntity, move.to, out var received) || !NormalMoveReceipt(move, received))) return "회수한 main 물품의 정상 이동 variation/상태 전이 미확인";
        return QueueReady(world, player, p, false);
    }
    public static string MoveObservationReady(World world, Entity player, AIInventoryMove move)
    {
        var p = Sync(world, player).Pending;
        if (p == null || move.maintenance?.PendingRun != p.Run) return "정비 이동의 자기 pending 변경";
        if (!Station(world, player, move.chest, ObjectID.UpgradeForge, out var tile, out int input, out string why) || math.any(tile != p.Request.Tile) || input != p.Request.InputSlot) return why ?? "정비 시설 위치/입력 변경";
        return math.distance(AIBody.Pos(world, player).xz, (float2)tile) > AICraft.StationRange ? "정비 물품 이동 중 시설 거리 이탈" : null;
    }
    public static void MoveObserved(World world, Entity player, AIInventoryMove move, int confirmed)
    {
        var s = Sync(world, player); var p = s.Pending;
        if (confirmed != 1 || p == null || move.maintenance?.PendingRun != p.Run) return;
        if (move.mode == "maintenance_put") { p.PutConfirmed = true; if (AIInventory.Read(world, p.Request.Station, p.Request.InputSlot, out var item)) p.Current = item; }
        else if (AIInventory.Read(world, move.toEntity, move.to, out var received) && NormalMoveReceipt(move, received) && move.beforeFrom.EqualsExact(p.Current)) { p.ReceivedItem = received; p.TakeConfirmed = true; }
        p.Observation = move.mode == "maintenance_put" ? "양쪽 변화로 자기 투입1 확인" : "양쪽 변화로 자기 pending main 회수1 확인";
    }
    internal static PendingHandle Begin(World world, Entity player, MaintenanceRequest request)
    {
        var s = Sync(world, player);
        if (request.Mode == "recover") return s.Pending == null || s.Pending.Run != request.PendingRun ? null : new PendingHandle(s.Pending);
        if (s.Pending != null) return null;
        var p = new Pending { Run = ++s.NextRun, Request = request, Current = request.BeforeItem };
        request.PendingRun = p.Run; s.Pending = p; return new PendingHandle(p);
    }
    internal sealed class PendingHandle
    {
        internal readonly Pending Value;
        internal PendingHandle(Pending p) { Value = p; }
    }
    internal static AIInventoryMove Put(World world, Entity player, MaintenanceRequest request, PendingHandle handle)
    {
        var p = handle.Value;
        var move = new AIInventoryMove { mode = "maintenance_put", key = request.Key + ":put", label = "정확 장비 대장간 local0 투입1", fromEntity = player, toEntity = request.Station, from = request.SourceSlot, to = request.InputSlot, count = 1, beforeFrom = request.BeforeItem, beforeTo = request.BeforeInput, chest = request.Station, goal = null, maintenance = request };
        p.PutMove = move; return move;
    }
    internal static AIInventoryMove Take(World world, Entity player, MaintenanceRequest request, PendingHandle handle, out string why)
    {
        var p = handle.Value; why = QueueReady(world, player, p, true); if (why != null) return null;
        int slot = AIInventory.Destination(world, player, p.Current, true, out int room);
        if (slot < 0 || room < 1 || !AIInventory.Read(world, player, slot, out var before)) { why = "정확 main 가방 회수 공간 없음; forge 잔류"; return null; }
        var move = new AIInventoryMove { mode = "maintenance_take", key = request.Key + ":take", label = "자기 pending main 가방 회수1", fromEntity = request.Station, toEntity = player, from = request.InputSlot, to = slot, count = 1, beforeFrom = p.Current, beforeTo = before, chest = request.Station, goal = null, maintenance = request };
        p.TakeMove = move; return move;
    }
    internal static bool Post(World world, Entity player, MaintenanceRequest request, PendingHandle handle, out string why)
    {
        var p = handle.Value;
        if (!Station(world, player, request.Station, Facility(request.Mode), out var tile, out int input, out why) || math.any(tile != request.Tile) || input != request.InputSlot) { why = why ?? "정비 시설 위치/입력 변경"; return false; }
        if (math.distance(AIBody.Pos(world, player).xz, (float2)request.Tile) > AICraft.StationRange) { why = "정비 요청 직전 시설 거리 이탈"; return false; }
        string gate = AIInventory.Gate(world, player); if (gate != null) { why = gate; return false; }
        if (p.Posted && QueueReady(world, player, p, request.Mode != "repair") != null) { why = "직전 자기 요청 처리 미확인; 새 정비 요청 없음"; return false; }
        var actual = new MaintenanceRequest(); var item = request.BeforeItem;
        if (request.Mode == "upgrade")
        {
            if (!p.PutConfirmed || !ObserveExactInput(world, p)) { why = "자기 투입/정확 forge 입력 미확인"; return false; }
            item = p.Current;
        }
        else if (!ActiveSlot(world, player, request.SourceSlot, out bool gear) || gear != request.SourceGear || !AIInventory.Read(world, player, request.SourceSlot, out item) || !item.EqualsExact(request.BeforeItem))
        { why = "게시 직전 선택 장비 슬롯/프리셋/정확 상태 변경"; return false; }
        if (!ReadCosts(world, player, request.Station, request.Mode, item, actual, out why) || !CostsEqual(request, actual)) { why = why ?? "게시 직전 실제 비용/효과 변경"; return false; }
        why = Missing(actual); if (why != null) return false;
        var data = request.Mode == "repair" ? Create.RepairOrReinforce(player, request.SourceSlot, item.objectID, 1, request.Station, player, false, false)
            : Create.Upgrade(request.Station, player, request.Station, request.InputSlot, item.objectID, 1, false);
        BeforeMaterials(world, player, p);
        if (!AICraft.QueueCraftAction(world, player, data, out var tick, out why)) return false;
        p.Craft = data; p.Posted = true; p.PostedTick = tick; p.PostedKind = "craft"; p.Observation = "정상 정비 요청 게시; 소비/장비 변화 미확인";
        return true;
    }
    internal static string Processed(World world, Entity player, PendingHandle handle, bool input) => QueueReady(world, player, handle.Value, input);
    internal static bool Upgraded(World world, Entity player, PendingHandle handle) { var s = Sync(world, player); ObservePending(world, player, s); return handle.Value.Upgraded; }
    internal static bool Received(PendingHandle handle) => handle.Value.TakeConfirmed;
    internal static bool ReceivedIdentity(PendingHandle handle, out int observedLevel)
    {
        var p = handle.Value; observedLevel = p.ReceivedItem.variation;
        return p.TakeConfirmed && p.TakeMove != null && NormalMoveReceipt(p.TakeMove, p.ReceivedItem) && p.TakeMove.beforeFrom.EqualsExact(p.Current)
            && SameObject(p.ReceivedItem, p.Request.BeforeItem);
    }
    // InventoryUtility.TryMove keeps the destination's variationUpdateCount, copying ID/variation/aux and adding
    // the complete non-stackable object's amount. This is the normal game transition, not source struct copying.
    private static bool NormalMoveReceipt(AIInventoryMove move, ContainedObjectsBuffer observed)
    {
        if (move.beforeTo.objectID != ObjectID.None || AIInventory.Info(move.beforeFrom)?.isStackable != false) return false;
        var expected = move.beforeTo; var data = expected.objectData;
        data.objectID = move.beforeFrom.objectID; data.variation = move.beforeFrom.variation; data.amount += move.beforeFrom.amount;
        expected.objectData = data; expected.auxDataIndex = move.beforeFrom.auxDataIndex;
        return observed.EqualsExact(expected);
    }
    internal static string Changes(World world, Entity player, PendingHandle handle) => MaterialChanges(world, player, handle.Value);
    internal static string LiveTarget(World world, Entity player, MaintenanceRequest request, PendingHandle handle)
    {
        if (!ReferenceEquals(request.World, world) || request.Player != player || request.Owner != AIAgent.Cur.Id) return "정비 실행 중 월드/캐릭터/플레이어 신원 변경";
        if (!Station(world, player, request.Station, Facility(request.Mode), out var tile, out int input, out string why) || math.any(tile != request.Tile) || input != request.InputSlot) return why ?? "정비 시설 위치/입력 변경";
        if (math.distance(AIBody.Pos(world, player).xz, (float2)tile) > AICraft.StationRange) return "정비 관측 중 시설 거리 이탈";
        var p = handle?.Value;
        if (request.Mode == "repair")
        {
            if (!ActiveSlot(world, player, request.SourceSlot, out bool gear) || gear != request.SourceGear || !AIInventory.Read(world, player, request.SourceSlot, out var target) || !AIInventory.Identity(target, request.BeforeItem)) return "게시한 수리 대상 슬롯/활성 프리셋/물품 신원 변경";
        }
        else if (p?.PutConfirmed == true && p.PostedKind != "take")
        {
            if (!AIInventory.Read(world, request.Station, request.InputSlot, out var target) || !SameObject(target, request.BeforeItem)) return "자기 pending 물품 신원 변경; 다른 물건으로 대체하지 않음";
            if (p.Upgraded && !target.EqualsExact(p.Current)) return "확인된 업그레이드 물품의 정확 snapshot이 바뀜; 재채택/회수 없음";
            if (target.variation != request.BeforeItem.variation && target.variation != request.ExpectedLevel) return "자기 pending에 예상과 다른 레벨; 재요청/대체 실행 없음";
        }
        return null;
    }
    internal static bool ConfirmRepair(World world, Entity player, MaintenanceRequest request, PendingHandle handle, out string fact)
    {
        var p = handle.Value;
        if (!AIInventory.Read(world, player, request.SourceSlot, out var item)) { fact = "수리 대상 슬롯 미관측"; return false; }
        bool same = AIInventory.Identity(item, request.BeforeItem);
        int confirmed = AIMaintenanceRules.RepairConfirmed(same, request.BeforeItem.amount, item.amount, request.ExpectedAmount);
        fact = $"장비 {AIInventory.Text(request.BeforeItem)}→{AIInventory.Text(item)}, 내구도 변화 {(confirmed == 1 ? "확인" : "미확인")}. {MaterialChanges(world, player, p)}";
        return confirmed == 1;
    }
    internal static void Complete(World world, Entity player, PendingHandle handle, string result)
    {
        var s = Sync(world, player); s.LastResult = result;
        if (ReferenceEquals(s.Pending, handle.Value)) s.Pending = null;
    }
    internal static void Retain(World world, Entity player, PendingHandle handle, string result)
    {
        var s = Sync(world, player); s.LastResult = result; if (handle != null) handle.Value.Observation = result;
    }
    internal static void CleanupIntent(World world, Entity player, PendingHandle handle)
    {
        if (handle == null || !States.TryGetValue(AIAgent.Cur, out var s) || !ReferenceEquals(s.World, world) || s.Player != player) return;
        var p = handle.Value;
        if (ReferenceEquals(s.Pending, p) && !p.Posted && !p.PutConfirmed)
        { s.Pending = null; s.LastResult = "게시/로컬 큐 등록·실제 투입이 없던 정비 intent 정리; 물품/목표 변경 없음"; }
    }
}

public sealed class MaintenanceSkill : AISkill, ILifeResult
{
    private readonly MaintenanceRequest _request;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn;
    private string _phase = "created";
    private AIMaintenance.PendingHandle _pending;
    private InventorySkill _move;
    public int RequestedWork => 1;
    public int ConfirmedWork { get; private set; }
    public int ConfirmedReceived { get; private set; }
    public string ResultStage { get; private set; } = "미실행";
    public MaintenanceSkill(MaintenanceRequest request) { _request = request; Name = request?.Mode == "repair" ? "장비 일반 수리" : request?.Mode == "recover" ? "자기 정비 물품 회수" : "장비 단일 레벨 업그레이드"; Key = request?.Key ?? "maintain:missing"; }
    public override bool Start(World world, Entity player, float now)
    {
        string why = AIMaintenance.Stale(world, player, new AICandidate { maintenance = _request });
        if (why != null) { Status = SkillStatus.Failed; Reason = why; return false; }
        if (math.distance(AIBody.Pos(world, player).xz, (float2)_request.Tile) > AICraft.StationRange)
        { _nav.Start(world, player, _request.Tile, AICraft.StationRange - 0.2f, now, _request.Station); _navOn = true; _phase = "approach"; }
        else _phase = "revalidate";
        return true;
    }
    public override BodyInput Tick(World world, Entity player, float now)
    {
        if (_navOn)
        {
            var status = _nav.Tick(world, player, now, out float2 move);
            if (status == NavStatus.Failed) return Finish(SkillStatus.Failed, "정비 시설 접근 실패: " + _nav.FailReason);
            if (status != NavStatus.Arrived) return new BodyInput { move = move, slot = -1 };
            _nav.Dispose(world); _navOn = false; _phase = "revalidate";
        }
        if (_phase == "revalidate")
        {
            string stale = AIMaintenance.Stale(world, player, new AICandidate { maintenance = _request });
            if (stale != null) return Finish(SkillStatus.Failed, stale);
            if (math.distance(AIBody.Pos(world, player).xz, (float2)_request.Tile) > AICraft.StationRange) return Finish(SkillStatus.Failed, "정비 요청 직전 시설 거리 이탈");
            _pending = AIMaintenance.Begin(world, player, _request);
            if (_pending == null) return Finish(SkillStatus.Failed, "다른 자기 정비 intent/pending이 이미 있음");
            if (_request.Mode == "recover") _phase = "take";
            else if (_request.Mode == "upgrade")
            {
                _move = new InventorySkill(AIMaintenance.Put(world, player, _request, _pending));
                if (!_move.Start(world, player, now)) return Finish(SkillStatus.Failed, _move.Reason);
                _phase = "put";
            }
            else _phase = "queue";
        }
        if (_pending != null)
        {
            string live = AIMaintenance.LiveTarget(world, player, _request, _pending);
            if (live != null) { AIMaintenance.Retain(world, player, _pending, live); ResultStage = live; return Finish(SkillStatus.Failed, live); }
        }
        if (_phase == "put")
        {
            var input = _move.Tick(world, player, now);
            if (_move.Status == SkillStatus.Failed) { AIMaintenance.Retain(world, player, _pending, _move.Reason); return Finish(SkillStatus.Failed, _move.Reason); }
            if (_move.Status != SkillStatus.Done) { ResultStage = "정상 투입 요청/양쪽 이동·게시 처리 미확인"; return input; }
            Facts.AddRange(_move.Facts); _move.Cleanup(world); _move = null; _phase = "queue";
        }
        if (_phase == "queue")
        {
            if (!AIMaintenance.Post(world, player, _request, _pending, out string why))
            { ResultStage = why; AIMaintenance.Retain(world, player, _pending, why); return Finish(SkillStatus.Failed, why); }
            _phase = "observe"; ResultStage = "정상 정비 요청1회 게시; 처리/장비/재료 변화 확인 중";
            return BodyInput.Idle;
        }
        if (_phase == "observe")
        {
            bool upgrade = _request.Mode == "upgrade";
            bool changed = upgrade && AIMaintenance.Upgraded(world, player, _pending);
            string waiting = AIMaintenance.Processed(world, player, _pending, upgrade);
            if (waiting != null) { ResultStage = waiting; ObserveOnce("maintenance-consumer-pending", waiting); return BodyInput.Idle; }
            if (!upgrade)
            {
                bool repaired = AIMaintenance.ConfirmRepair(world, player, _request, _pending, out string fact);
                ConfirmedWork = repaired ? 1 : 0; ResultStage = repaired ? "실제 내구도 변화 확인; 재료 결과는 별도" : "요청 처리 확인, 수리 상태 변화 미확인; 재요청 없음";
                Facts.Add(fact); AIMaintenance.Complete(world, player, _pending, ResultStage + ". " + fact);
                return Finish(SkillStatus.Done, ResultStage + ". " + fact);
            }
            if (!changed)
            {
                ResultStage = "업그레이드 요청 처리 확인, 예상 레벨 변화 없음; work0, 명시 recover 필요";
                string fact = AIMaintenance.Changes(world, player, _pending); Facts.Add(fact); AIMaintenance.Retain(world, player, _pending, ResultStage + ". " + fact);
                return Finish(SkillStatus.Done, ResultStage);
            }
            ResultStage = "레벨 변화 확인, 가방 미회수"; _phase = "take";
        }
        if (_phase == "take")
        {
            if (AIMaintenance.Received(_pending)) return Received(world, player);
            var move = AIMaintenance.Take(world, player, _request, _pending, out string why);
            if (move == null) { ResultStage = why; AIMaintenance.Retain(world, player, _pending, why); ObserveOnce("maintenance-take-blocked", why); return BodyInput.Idle; }
            _move = new InventorySkill(move);
            if (!_move.Start(world, player, now)) { ResultStage = _move.Reason; AIMaintenance.Retain(world, player, _pending, ResultStage); return Finish(SkillStatus.Failed, ResultStage); }
            _phase = "receive";
        }
        if (_phase == "receive")
        {
            var input = _move.Tick(world, player, now);
            if (_move.Status == SkillStatus.Failed) { AIMaintenance.Retain(world, player, _pending, _move.Reason); return Finish(SkillStatus.Failed, _move.Reason); }
            if (_move.Status != SkillStatus.Done) { ResultStage = "레벨/원본 상태와 별개로 main 가방 회수 미확인"; return input; }
            Facts.AddRange(_move.Facts); _move.Cleanup(world); _move = null; return Received(world, player);
        }
        return BodyInput.Idle;
    }
    private BodyInput Received(World world, Entity player)
    {
        bool upgraded = AIMaintenance.Upgraded(world, player, _pending);
        bool recoveringUpgrade = _request.Selection?.Mode == "upgrade";
        bool exactReceived = AIMaintenance.ReceivedIdentity(_pending, out int observedLevel);
        if (!exactReceived)
        {
            ResultStage = "회수된 main 물품의 정확 신원/variation/상태가 pending snapshot과 다름; 작업/확보 미확인";
            AIMaintenance.Retain(world, player, _pending, ResultStage); return Finish(SkillStatus.Failed, ResultStage);
        }
        ConfirmedReceived = 1;
        ConfirmedWork = _request.Mode == "upgrade" || recoveringUpgrade ? AIMaintenanceRules.UpgradeConfirmed(upgraded && exactReceived, observedLevel, _request.ExpectedLevel, true) : 1;
        ResultStage = upgraded ? "정확한 레벨 변화와 main 가방 회수1 확인" : "미강화 원본 main 회수1 확인; upgrade 성공 아님";
        string fact = AIMaintenance.Changes(world, player, _pending); Facts.Add(fact); AIMaintenance.Complete(world, player, _pending, ResultStage + ". " + fact);
        return Finish(SkillStatus.Done, ResultStage + ". " + fact);
    }
    public override string Progress(World world, Entity player) => _phase + ": " + ResultStage;
    public override void Cleanup(World world) { _nav.Dispose(world); _move?.Cleanup(world); if (_request != null) AIMaintenance.CleanupIntent(world, _request.Player, _pending); }
}
