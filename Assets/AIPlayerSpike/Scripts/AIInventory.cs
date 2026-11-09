// Concrete inventory choices. The AI chooses a source, destination and operation; the game processes normal UI
// requests. Results are observed from both slots, never inferred from queue acceptance.
using System;
using System.Collections.Generic;
using System.Text;
using Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class AIInventoryMove
{
    public string mode, key, label;
    public Entity fromEntity, toEntity;
    public int from = -1, to = -1, count = 1;
    public ContainedObjectsBuffer beforeFrom, beforeTo;
    public AIGoal goal;
    public string destination = "";
    public Entity chest, recipient;
    public string recipientName = "";
    public MaintenanceRequest maintenance; // exact normal forge transfer; never a chest permission override
    public bool swap;        // 2026-11-08: the whole slot is exchanged with Create.Swap (also across player/chest)
    public int preset = -1;  // mode "preset": the equipment preset index to switch to
}

public static class AIInventory
{
    private sealed class Hand
    {
        public int slot;
        public ContainedObjectsBuffer item;
    }
    private sealed class State
    {
        public AIGoal goal;
        public bool done;
        public string result = "";
        public Hand mining, weapon;
        public int moved;
        public Entity openChest;
        public readonly HashSet<string> areaRepWalks = new HashSet<string>();   // area representative walks of this character's current build (merge review L-6)
    }
    private static readonly Dictionary<AIAgent, State> States = new Dictionary<AIAgent, State>();
    private static State S { get { var a = AIAgent.Cur; if (!States.TryGetValue(a, out var s)) States[a] = s = new State(); return s; } }
    public static void Reset() { States.Remove(AIAgent.Cur); AIInventoryMenu.Reset("캐릭터/월드의 가방 선택 초기화"); }

    public static bool OwnsGoal(AIGoal g) => g != null && (g.kind == GoalKind.Equip || g.kind == GoalKind.Unequip || g.kind == GoalKind.Hold || g.kind == GoalKind.Take || g.kind == GoalKind.Drop || g.kind == GoalKind.Give || g.kind == GoalKind.Store && (g.item != ObjectID.None || g.count > 0 || g.keep >= 0 || g.slot >= 0 || !string.IsNullOrEmpty(g.where)));
    public static void GoalSet(World world, Entity player, AIGoal g) { S.goal = g; S.done = false; S.result = ""; S.moved = 0; AIInventoryMenu.Reset("새 목표로 이전 가방 선택을 닫음"); }
    public static bool GoalDone(World world, Entity player, out string note)
    {
        note = S.result;
        if (!ReferenceEquals(S.goal, AIGoals.Goal)) return false;
        var g = S.goal;
        if (g != null && IsBaseWhere(g.where) && (g.kind == GoalKind.Store || g.kind == GoalKind.Take) && !AIBase.ArrivedAtBase(world, player, out _)) { note = "명시한 기지 기준점에 아직 도착하지 않음"; return false; }
        if (g.kind == GoalKind.Store && g.count <= 0 && StoreRemaining(world, player, g) == 0) { note = $"보관할 잔량 없음(실제 이동 {S.moved}, 남길 수량 {math.max(0, g.keep)})"; return true; }
        return S.done;
    }
    public static string StallNote(World world, Entity player) => Gate(world, player) ?? (IsBaseWhere(AIGoals.Goal?.where) ? AIBase.ArrivedAtBase(world, player, out _) ? "기지 기준점 도착 후 현재 관측·조작 가능한 상자/슬롯이 없음" : "명시한 기지 기준점으로 먼저 이동해야 함" : "요청과 일치하는 현재 가방 물품·유효 목적 슬롯이 없음");

    internal static bool Read(World world, Entity entity, int slot, out ContainedObjectsBuffer value)
    {
        value = default;
        if (world == null || !world.IsCreated) return false;
        var em = world.EntityManager;
        if (!em.Exists(entity) || !em.HasBuffer<ContainedObjectsBuffer>(entity)) return false;
        var b = em.GetBuffer<ContainedObjectsBuffer>(entity, true);
        if (slot < 0 || slot >= b.Length) return false;
        value = b[slot]; return true;
    }
    internal static int MainStart(World world, Entity player)
    {
        var em = world.EntityManager;
        if (em.HasBuffer<InventoryBuffer>(player)) { var b = em.GetBuffer<InventoryBuffer>(player, true); if (b.Length > 0) return b[0].startIndex; }
        return 0;
    }
    internal static int MainEnd(World world, Entity player)
    {
        var em = world.EntityManager;
        return em.HasBuffer<ContainedObjectsBuffer>(player) ? AIBody.MainEnd(em, player, em.GetBuffer<ContainedObjectsBuffer>(player, true).Length) : 0;
    }
    internal static bool IsMain(World world, Entity player, int slot) => slot >= MainStart(world, player) && slot < MainEnd(world, player);
    internal static ObjectInfo Info(ContainedObjectsBuffer item) => item.objectID == ObjectID.None ? null : PugDatabase.GetObjectInfo(item.objectID, item.variation);
    internal static int Quantity(ContainedObjectsBuffer item) { if (item.objectID == ObjectID.None) return 0; var info = Info(item); return info != null && !info.isStackable ? 1 : item.amount; }
    internal static bool Identity(ContainedObjectsBuffer a, ContainedObjectsBuffer b) => a.objectID == b.objectID && a.variation == b.variation && a.auxDataIndex == b.auxDataIndex;
    internal static InventoryStamp Stamp(ContainedObjectsBuffer item) => new InventoryStamp((int)item.objectID, item.variation, item.auxDataIndex, item.amount, item.variationUpdateCount, Info(item)?.isStackable == true);
    internal static string Text(ContainedObjectsBuffer x) => x.objectID == ObjectID.None ? "빈칸" : $"{x.objectID}(변형 {x.variation}, 상태 {x.amount}, 개수 {Quantity(x)})";

    internal static string Gate(World world, Entity player)
    {
        if (world == null || !world.IsCreated) return "게임 월드 없음";
        var em = world.EntityManager;
        if (!em.Exists(player) || !em.HasBuffer<ContainedObjectsBuffer>(player) || !em.HasBuffer<InventoryBuffer>(player)) return "가방을 관측할 수 없음";
        if (!em.HasBuffer<UIActionBuffer>(player)) return "UIActionBuffer 없음";
        if (em.HasComponent<HealthCD>(player) && em.GetComponentData<HealthCD>(player).health <= 0) return "쓰러진 상태";
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldInfoCD>()))
            if (q.CalculateEntityCount() == 1 && q.GetSingleton<WorldInfoCD>().guestMode && em.HasComponent<PlayerGhost>(player) && em.GetComponentData<PlayerGhost>(player).adminPrivileges <= 0) return "게스트 권한으로 가방 변경 불가";
        if (em.HasComponent<CraftingCD>(player))
        {
            int c = em.GetComponentData<CraftingCD>(player).outputSlotIndex;
            if (Read(world, player, c, out var cursor) && cursor.objectID != ObjectID.None) return "커서에 물품이 있음";
        }
        return null;
    }

    private static EquipmentCD Equipment(World world, Entity player)
    {
        var em = world.EntityManager;
        if (em.HasBuffer<EquipmentPresetsBuffer>(player) && em.HasComponent<ActiveEquipmentPresetCD>(player))
        {
            var p = em.GetBuffer<EquipmentPresetsBuffer>(player, true); int n = em.GetComponentData<ActiveEquipmentPresetCD>(player).Value;
            if (n >= 0 && n < p.Length) return p[n].equipment;
        }
        return em.GetComponentData<EquipmentCD>(player);
    }
    internal static List<(string name, int slot, ObjectType type)> GearSlots(World world, Entity player)
    {
        var l = new List<(string, int, ObjectType)>(); var em = world.EntityManager;
        if (!em.Exists(player) || !em.HasComponent<EquipmentCD>(player)) return l;
        var e = Equipment(world, player);
        l.Add(("head", e.helmSlotIndex, ObjectType.Helm)); l.Add(("chest", e.breastSlotIndex, ObjectType.BreastArmor));
        l.Add(("legs", e.pantsSlotIndex, ObjectType.PantsArmor)); l.Add(("necklace", e.necklaceSlotIndex, ObjectType.Necklace));
        l.Add(("ring1", e.ring1SlotIndex, ObjectType.Ring)); l.Add(("ring2", e.ring2SlotIndex, ObjectType.Ring));
        l.Add(("offhand", e.offHandIndex, ObjectType.Offhand)); l.Add(("bag", e.bagIndex, ObjectType.Bag)); l.Add(("lantern", e.lanternIndex, ObjectType.Lantern));
        for (int i = 0; i < 4; i++) l.Add(("pouch" + (i + 1), e.GetPouchSlotIndex(i), ObjectType.Pouch));
        // 2026-11-08 빈 선택지 B5·B6: the pet slot (PetOwnerCD.SlotIndex) and the three vanity slots (VanitySlotsCD) are
        // player slots like the gear above. Vanity is never an implicit equip target (WantsSlot), only when named.
        if (em.HasComponent<PetOwnerCD>(player)) l.Add(("pet", em.GetComponentData<PetOwnerCD>(player).SlotIndex, ObjectType.Pet));
        if (em.HasComponent<VanitySlotsCD>(player))
        {
            var v = em.GetComponentData<VanitySlotsCD>(player);
            l.Add(("vanity_head", v.helmVanitySlotIndex, ObjectType.Helm)); l.Add(("vanity_chest", v.breastVanitySlotIndex, ObjectType.BreastArmor)); l.Add(("vanity_legs", v.pantsVanitySlotIndex, ObjectType.PantsArmor));
        }
        return l;
    }
    internal static bool IsGear(World world, Entity player, int slot) => GearSlots(world, player).Exists(x => x.slot == slot);
    // 2026-11-08 빈 선택지 B1: each worn pouch owns a separate inventory (InventoryBuffer whose extraInventorySizeSlot is
    // the pouch gear slot, InventoryConverter). Its size follows the pouch item (extraSize); an empty pouch slot gives 0.
    internal static List<(int pouch, int start, int end, int provider)> Pouches(World world, Entity player)
    {
        var l = new List<(int, int, int, int)>(); var em = world.EntityManager;
        if (!em.Exists(player) || !em.HasComponent<EquipmentCD>(player) || !em.HasBuffer<InventoryBuffer>(player) || !em.HasBuffer<ContainedObjectsBuffer>(player)) return l;
        var e = Equipment(world, player); var inventories = em.GetBuffer<InventoryBuffer>(player, true); int length = em.GetBuffer<ContainedObjectsBuffer>(player, true).Length;
        for (int p = 0; p < 4; p++)
        {
            int provider = e.GetPouchSlotIndex(p);
            for (int j = 1; j < inventories.Length; j++)
                if (inventories[j].extraInventorySizeSlot == provider)
                {
                    if (inventories[j].size > 0) l.Add((p, inventories[j].startIndex, math.min(length, inventories[j].startIndex + inventories[j].size), provider));
                    break;
                }
        }
        return l;
    }
    internal static bool IsPouch(World world, Entity player, int slot) => Pouches(world, player).Exists(x => slot >= x.start && slot < x.end);
    // A carried storage slot: the main inventory (hotbar + bag) or an active pouch inventory.
    internal static bool IsCarried(World world, Entity player, int slot) => IsMain(world, player, slot) || IsPouch(world, player, slot);
    internal static List<int> CarriedSlots(World world, Entity player)
    {
        var l = new List<int>();
        for (int i = MainStart(world, player); i < MainEnd(world, player); i++) l.Add(i);
        foreach (var pouch in Pouches(world, player)) for (int i = pouch.start; i < pouch.end; i++) l.Add(i);
        return l;
    }
    // End index of the inventory that holds `slot` in `entity` (MoveAmount's endIndex); main uses MainEnd as before.
    internal static int InventoryEnd(World world, Entity entity, int slot)
    {
        var em = world.EntityManager; if (!em.HasBuffer<InventoryBuffer>(entity)) return -1;
        var inventories = em.GetBuffer<InventoryBuffer>(entity, true);
        for (int j = 0; j < inventories.Length; j++)
            if (slot >= inventories[j].startIndex && slot < inventories[j].startIndex + inventories[j].size)
                return j == 0 && em.HasComponent<EquipmentCD>(entity) ? MainEnd(world, entity) : inventories[j].startIndex + inventories[j].size;
        return -1;
    }
    internal static string SlotName(World world, Entity player, int slot)
    {
        if (IsMain(world, player, slot)) return slot < 10 ? "핫바 " + slot : "가방 " + slot;
        foreach (var pouch in Pouches(world, player)) if (slot >= pouch.start && slot < pouch.end) return $"파우치{pouch.pouch + 1} 칸 {slot}";
        foreach (var gear in GearSlots(world, player)) if (gear.slot == slot) return "착용 " + gear.name;
        return "슬롯 " + slot;
    }
    private static string Canonical(string s)
    {
        switch ((s ?? "").Trim().ToLowerInvariant())
        {
            case "helm": case "머리": return "head";
            case "breast": case "상의": case "흉갑": return "chest";
            case "pants": case "하의": return "legs";
            case "목걸이": return "necklace";
            case "반지1": return "ring1"; case "반지2": return "ring2";
            case "보조손": return "offhand"; case "가방": return "bag"; case "랜턴": return "lantern";
            case "펫": case "반려동물": return "pet";
            case "꾸미기 머리": case "vanity_helm": return "vanity_head"; case "꾸미기 상의": case "vanity_breast": return "vanity_chest"; case "꾸미기 하의": case "vanity_pants": return "vanity_legs";
            default: return (s ?? "").Trim().ToLowerInvariant();
        }
    }
    private static bool WantsSlot(string where, string actual) => (string.IsNullOrEmpty(where) || where == "null") ? !actual.StartsWith("vanity") : Canonical(where) == actual;
    private static AIInventoryMove SwapMove(World world, Entity player, string mode, int source, int destination, string dest, AIGoal goal)
    {
        if (source == destination || !Read(world, player, source, out var a) || !Read(world, player, destination, out var b)) return null;
        return new AIInventoryMove { mode = mode, key = $"inventory:{mode}:{source}:{destination}", label = $"{mode} {Text(a)} 슬롯 {source} → {dest}({destination}), 이전 {Text(b)} → 슬롯 {source}", fromEntity = player, toEntity = player, from = source, to = destination, beforeFrom = a, beforeTo = b, goal = goal, destination = dest };
    }
    private static AICandidate Candidate(AIInventoryMove m, bool progress) => new AICandidate { kind = "inventory", actionMode = m.mode, key = m.key, desc = m.label + (m.mode == "inspect" ? ". 내용 관측 후 다음 행동은 다시 고름." : m.mode == "drop" ? ". 출발 감소와 실제 바닥 생성을 확인함." : m.mode == "give" ? ". 바닥 물품의 수령 대상과 실제 가방 증가를 확인함." : ". 실제 양 슬롯/장비 상태 변화를 확인함."), inventory = m, itemId = m.beforeFrom.objectID, itemVariation = m.beforeFrom.variation, itemCount = m.count, progress = progress };
    public static List<AICandidate> Candidates(World world, Entity player, AIObservation observation, string currentKey)
    {
        var result = new List<AICandidate>(); if (Gate(world, player) != null) return result;
        var g = AIGoals.Goal; bool own = OwnsGoal(g); var gear = GearSlots(world, player);
        if (own && ReferenceEquals(S.goal, g) && S.done) return result;
        int start = MainStart(world, player), end = MainEnd(world, player);
        if (!own || g.kind == GoalKind.Equip || g.kind == GoalKind.Hold)
        {
            for (int i = start; i < end; i++)
            {
                if (!Read(world, player, i, out var a) || a.objectID == ObjectID.None) continue;
                if (own && ((g.item != ObjectID.None && g.item != a.objectID) || (g.slot >= 0 && g.slot != i))) continue;
                var info = Info(a); if (info == null) continue;
                if ((!own || g.kind == GoalKind.Equip)) foreach (var target in gear)
                {
                    if (target.type != info.objectType || (own && !WantsSlot(g.where, target.name))) continue;
                    var m = SwapMove(world, player, "equip", i, target.slot, target.name, own ? g : null);
                    if (m == null || m.key == currentKey || Stale(world, player, m) != null) continue;
                    result.Add(Candidate(m, own));
                }
                if (!own || g.kind == GoalKind.Hold)
                {
                    // The destination is part of the offered choice. Empty slots first; occupied hotbar slots remain
                    // explicit swap choices so a full hotbar never silently discards or auto-selects an item.
                    var targets = new List<int>();
                    if (own && !string.IsNullOrEmpty(g.where) && g.where.StartsWith("hotbar:") && int.TryParse(g.where.Substring(7), out int explicitHotbar) && explicitHotbar >= 0 && explicitHotbar < math.min(10, end)) targets.Add(explicitHotbar);
                    else if (i < 10) targets.Add(i);
                    else
                    {
                        for (int h = start; h < math.min(10, end); h++) if (Read(world, player, h, out var v) && v.objectID == ObjectID.None) targets.Add(h);
                        if (targets.Count == 0) for (int h = start; h < math.min(10, end); h++) targets.Add(h);
                    }
                    foreach (int h in targets)
                    {
                        string destination = "hotbar:" + h;
                        if (own && !WantsSlot(g.where, destination) && g.where != "hand") continue;
                        AIInventoryMove m;
                        if (i == h) m = new AIInventoryMove { mode = "hold", key = $"inventory:hold:{i}:{h}", label = $"손에 들기 {Text(a)} 핫바 {h}", fromEntity = player, toEntity = player, from = i, to = h, beforeFrom = a, beforeTo = a, goal = own ? g : null, destination = destination };
                        else m = SwapMove(world, player, "hold", i, h, destination, own ? g : null);
                        if (m == null || m.key == currentKey || Stale(world, player, m) != null || (!own && IsChosen(world, player, m))) continue;
                        result.Add(Candidate(m, own));
                    }
                }
            }
        }
        if (!own || g.kind == GoalKind.Unequip)
        {
            foreach (var target in gear)
            {
                if (own && (!WantsSlot(g.where, target.name) || g.slot >= 0 && g.slot != target.slot) || !Read(world, player, target.slot, out var a) || a.objectID == ObjectID.None || own && g.item != ObjectID.None && a.objectID != g.item) continue;
                for (int i = start; i < end; i++)
                {
                    if (!Read(world, player, i, out var b) || b.objectID != ObjectID.None) continue;
                    var m = SwapMove(world, player, "unequip", target.slot, i, "bag:" + i, own ? g : null);
                    if (m != null && m.key != currentKey && Stale(world, player, m) == null) result.Add(Candidate(m, own));
                }
            }
        }
        if (own && (g.kind == GoalKind.Store || g.kind == GoalKind.Take))
        {
            if (g.slot >= 0 && !string.IsNullOrEmpty(g.where) && g.where != "base")
            {
                var selected = AILife.ForGoal(g); selected.RequestedCount = g.count > 0 ? math.max(0, g.count - MovedForGoal(g)) : 0;
                // Review-4 F1: an area where (area: / base:area:) names the chest by the open chest inside that area.
                if (!(selected.Where ?? "").StartsWith("chest:", StringComparison.Ordinal) && S.openChest != Entity.Null)
                {
                    var openView = AIBase.ChestsInView(world, player).Find(c => c.entity == S.openChest);
                    var openMemory = openView == null ? null : AIBase.OwnChests().Find(c => math.all(c.tile == AIBody.TileOf(openView.pos)) && c.id == openView.id);
                    if (openMemory != null && ChestWhere(world, player, g.where, openMemory)) selected.Where = $"chest:{openMemory.tile.x},{openMemory.tile.y}";
                }
                var exact = ExactCandidate(world, player, observation, currentKey, selected);
                if (exact != null) result.Add(exact);
                else foreach (var candidate in CandidatesFor(world, player, observation, currentKey, g, g)) if (candidate.inventory?.mode == "inspect" || candidate.kind == "dig") result.Add(candidate);
            }
            else result.AddRange(CandidatesFor(world, player, observation, currentKey, g, g));
        }
        if (own && (g.kind == GoalKind.Drop || g.kind == GoalKind.Give)) result.AddRange(DropCandidates(world, player, observation, currentKey, g));
        return result;
    }
    // A query is not a goal assignment. Only a real current progressGoal may consume its receipts.
    public static List<AICandidate> CandidatesFor(World world, Entity player, AIObservation observation, string currentKey, AIGoal requestGoal, AIGoal progressGoal)
    {
        if (requestGoal == null || (requestGoal.kind != GoalKind.Store && requestGoal.kind != GoalKind.Take) || Gate(world, player) != null) return new List<AICandidate>();
        AIGoal bound = ReferenceEquals(progressGoal, AIGoals.Goal) && ReferenceEquals(progressGoal, S.goal) ? progressGoal : null;
        var result = ChestCandidates(world, player, observation, currentKey, requestGoal, bound);
        if (bound != null)
            foreach (var candidate in result)
            {
                var binding = AILife.ForGoal(bound);
                binding.Mode = requestGoal.kind == GoalKind.Store ? "store" : "take"; binding.Where = requestGoal.where; binding.Slot = candidate.inventory?.from ?? -1;
                if (candidate.inventory != null && candidate.inventory.beforeFrom.objectID != ObjectID.None) binding.Item = candidate.inventory.beforeFrom.objectID + "#v" + candidate.inventory.beforeFrom.variation;
                binding.RequestedCount = requestGoal.count > 0 ? math.max(0, requestGoal.count - (ReferenceEquals(requestGoal, bound) ? MovedForGoal(bound) : 0)) : 0; binding.OfferedCount = candidate.itemCount;
                candidate.life = binding;
            }
        return result;
    }
    public static int MovedForGoal(AIGoal goal) => ReferenceEquals(goal, AIGoals.Goal) && ReferenceEquals(goal, S.goal) ? S.moved : 0;
    private static AIGoal LifeQuery(LifeSelection selection)
    {
        AICraft.ParseItem(selection.Item, out ObjectID item);
        return new AIGoal { kind = selection.Mode == "store" ? GoalKind.Store : GoalKind.Take, item = item, itemName = item == ObjectID.None ? "" : item.ToString(), where = selection.Where ?? "", slot = selection.Slot, count = selection.RequestedCount,
            keep = selection.MatchesGoal && selection.Goal is AIGoal original ? original.keep : -1 };
    }
    public static List<AILife.Entry> LifeEntries(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        var result = new List<AILife.Entry>();
        if (string.IsNullOrEmpty(selection.Mode))
        {
            result.Add(new AILife.Entry { Op = "mode", Id = "store", Text = "보관 — 자기 가방의 정확한 슬롯에서 상자로" });
            result.Add(new AILife.Entry { Op = "mode", Id = "take", Text = "꺼내기 — 상자의 정확한 슬롯에서 main 가방으로" }); return result;
        }
        if (selection.Mode != "store" && selection.Mode != "take") return result;
        // Review-2 N7: an area chosen under "base" stays a base area ("base:area:x,y,s"), so its list keeps the base filter.
        bool baseArea = selection.Where != null && selection.Where.StartsWith("base:", StringComparison.Ordinal) && AIChestAreas.IsArea(selection.Where.Substring(5));
        if (selection.Where == "base" || baseArea)
        {
            if (!AIBase.ArrivedAtBase(world, player, out _))
            {
                var toBase = selection.Copy(); toBase.Where = "base";
                foreach (var candidate in CandidatesFor(world, player, observation, currentKey, LifeQuery(toBase), selection.MatchesGoal ? selection.Goal as AIGoal : null))
                { candidate.life = selection.Copy(); result.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate }); }
            }
            else
            {
                string inArea = baseArea ? selection.Where.Substring(5) : null;
                var atBase = AIChestAreas.Within(AIBase.OwnChests().FindAll(memory => BaseChestContext(world, player, memory.tile)), AIInventory.ChestTile, inArea);
                var baseAreas = AIChestAreas.Split(atBase, AIInventory.ChestTile, inArea);
                if (baseAreas != null) { foreach (var area in baseAreas) result.Add(new AILife.Entry { Op = "where", Id = "base:" + area.id, Text = "기지 범위 " + AIChestAreas.Text(area, AIInventory.ChestTile, observation.pos.xz) }); return result; }
                foreach (var memory in atBase)
                        result.Add(new AILife.Entry { Op = "where", Id = $"chest:{memory.tile.x},{memory.tile.y}", Text = $"기지 도착 후 현재 조작 가능한 상자 chest:{memory.tile.x},{memory.tile.y}" });
                if (result.Count == 0) result.Add(new AILife.Entry { Id = "inventory:base:no-chest", Text = "기지 기준점에 도착했으나 현재 관측·조작 가능한 상자가 없음" });
            }
            return result;
        }
        if (string.IsNullOrEmpty(selection.Where) || AIChestAreas.IsArea(selection.Where))
        {
            var visible = AIBase.ChestsInView(world, player);
            var within = AIChestAreas.Within(AIBase.OwnChests(), AIInventory.ChestTile, selection.Where);
            var areas = AIChestAreas.Split(within, AIInventory.ChestTile, selection.Where);
            if (areas != null)
            {
                foreach (var area in areas) result.Add(new AILife.Entry { Op = "where", Id = area.id, Text = AIChestAreas.Text(area, AIInventory.ChestTile, observation.pos.xz) });
                return result;
            }
            foreach (var memory in within)
            {
                string id = $"chest:{memory.tile.x},{memory.tile.y}";
                var chest = visible.Find(c => math.all(AIBody.TileOf(c.pos) == memory.tile) && c.id == memory.id && AIBase.Own(c) != null);
                result.Add(new AILife.Entry { Op = "where", Id = id, Text = $"{id} {memory.id} — {(chest == null ? "화면 밖·현재 내용 모름, " + AIBase.ChestText(memory, observation.pos) : S.openChest == chest.entity ? "열어 현재 내용 관측 가능" : "먼저 다가가 열어 확인")}" });
            }
            return result;
        }
        var query = LifeQuery(selection);
        var selectedMemory = AIBase.OwnChests().Find(c => selection.Where == $"chest:{c.tile.x},{c.tile.y}");
        if (selectedMemory == null) return result;
        var selected = AIBase.ChestsInView(world, player).Find(c => math.all(AIBody.TileOf(c.pos) == selectedMemory.tile) && c.id == selectedMemory.id && AIBase.Own(c) != null);
        if (selected == null || S.openChest != selected.entity || selected.dist > AIBase.ChestReach)
        {
            foreach (var candidate in CandidatesFor(world, player, observation, currentKey, query, selection.MatchesGoal ? selection.Goal as AIGoal : null))
                if (candidate.inventory?.mode == "inspect" || candidate.kind == "dig") { candidate.life = selection.Copy(); result.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate }); }
            return result;
        }
        if (selection.Slot < 0)
        {
            bool store = selection.Mode == "store"; Entity source = store ? player : selected.entity;
            var buffers = world.EntityManager.GetBuffer<InventoryBuffer>(selected.entity, true); if (buffers.Length == 0) return result;
            int start = store ? MainStart(world, player) : buffers[0].startIndex;
            int end = store ? MainEnd(world, player) : start + buffers[0].size;
            // Read one source axis only; destination search is deferred until a single slot was chosen.
            for (int slot = start; slot < end; slot++)
            {
                if (!Read(world, source, slot, out var item) || item.objectID == ObjectID.None) continue;
                var actual = selection.Copy(); actual.Item = item.objectID + "#v" + item.variation; actual.Slot = slot; actual = AILife.Resolve(actual);
                string quantity = actual.MatchesGoal ? $"현재 목표 잔량 {(actual.RequestedCount > 0 ? actual.RequestedCount.ToString() : "미지정 유지관리")}" : "원래 단계 밖의 별도 행동; 목표 진척에 합산하지 않음, 1개";
                result.Add(new AILife.Entry { Op = "slot", Id = slot.ToString(System.Globalization.CultureInfo.InvariantCulture), Text = $"출발 슬롯 {slot}: {Text(item)} — {quantity}. ID/variation/상태/슬롯을 합치지 않음" });
            }
            return result;
        }
        var exact = ExactCandidate(world, player, observation, currentKey, selection);
        if (exact != null) result.Add(new AILife.Entry { Id = exact.key, Text = exact.desc, Candidate = exact });
        else result.Add(new AILife.Entry { Id = "inventory:unavailable", Text = "선택 슬롯 이동 불가: 수량·남길 양·정확한 목적 슬롯 공간/제한을 현재 충족하지 못함" });
        return result;
    }
    public static AICandidate ExactCandidate(World world, Entity player, AIObservation observation, string currentKey, LifeSelection selection)
    {
        if ((selection.Mode != "store" && selection.Mode != "take") || string.IsNullOrEmpty(selection.Where) || selection.Slot < 0) return null;
        var memory = AIBase.OwnChests().Find(c => selection.Where == $"chest:{c.tile.x},{c.tile.y}");
        if (memory == null) return null;
        var chest = AIBase.ChestsInView(world, player).Find(c => math.all(AIBody.TileOf(c.pos) == memory.tile) && c.id == memory.id && AIBase.Own(c) != null);
        // Unknown/offscreen chest contents never resolve an item identity; only inspect may be offered there.
        if (chest == null || S.openChest != chest.entity || chest.dist > AIBase.ChestReach) return null;
        bool store = selection.Mode == "store";
        Entity source = store ? player : chest.entity;
        var inventory = world.EntityManager.GetBuffer<InventoryBuffer>(chest.entity, true); if (inventory.Length == 0) return null;
        int start = store ? MainStart(world, player) : inventory[0].startIndex;
        int end = store ? MainEnd(world, player) : start + inventory[0].size;
        if (selection.Slot < start || selection.Slot >= end || !Read(world, source, selection.Slot, out var sourceItem) || sourceItem.objectID == ObjectID.None) return null;
        var resolved = selection.Copy(); resolved.Item = sourceItem.objectID + "#v" + sourceItem.variation; resolved = AILife.Resolve(resolved);
        if (AILife.RequestSatisfied(resolved)) return null;
        var result = CandidatesFor(world, player, observation, currentKey, LifeQuery(resolved), resolved.MatchesGoal ? resolved.Goal as AIGoal : null);
        var candidate = result.Find(c => c.inventory != null && c.inventory.mode == selection.Mode && c.inventory.from == selection.Slot);
        if (candidate != null) { candidate.life = resolved.Copy(); candidate.life.OfferedCount = candidate.itemCount; }
        return candidate;
    }
    private static bool IsChosen(World world, Entity player, AIInventoryMove m)
    {
        if (m.mode != "hold" || m.from != m.to) return false;
        var info = Info(m.beforeFrom); var h = info != null && AIHandCapabilities.IsMiningTool(info.objectType.ToString()) ? S.mining : S.weapon;
        return h != null && h.slot == m.from && Identity(h.item, m.beforeFrom);
    }
    internal static int2 ChestTile(AIBase.ChestMem chest) => chest.tile;   // AIChestAreas key (review I3)
    internal static int Available(World world, Entity entity, ObjectID filter, int sourceSlot = -1)
    {
        int n = 0;
        for (int i = MainStart(world, entity); i < MainEnd(world, entity); i++)
            if ((sourceSlot < 0 || sourceSlot == i) && Read(world, entity, i, out var item) && item.objectID != ObjectID.None && (filter == ObjectID.None || filter == item.objectID)) n += Quantity(item);
        return n;
    }
    private static int StoreRemaining(World world, Entity player, AIGoal goal)
    {
        int eligible = 0;
        for (int i = MainStart(world, player); i < MainEnd(world, player); i++)
        {
            if ((goal.slot >= 0 && goal.slot != i) || !Read(world, player, i, out var item) || item.objectID == ObjectID.None || (goal.item != ObjectID.None && item.objectID != goal.item)) continue;
            if (goal.item == ObjectID.None && goal.slot < 0 && !AIBase.StorableKind(world, item.objectID, item.variation)) continue;
            eligible += Quantity(item);
        }
        ObjectID filter = goal.item;
        if (filter == ObjectID.None && goal.slot >= 0 && Read(world, player, goal.slot, out var selected)) filter = selected.objectID;
        int total = filter != ObjectID.None ? Available(world, player, filter) : eligible;
        return AIInventoryRules.MoveCount(total, eligible, 0, goal.keep);
    }
    internal static int MatchingCount(World world, Entity entity, ContainedObjectsBuffer expected)
    {
        if (world == null || !world.IsCreated) return -1;
        var em = world.EntityManager;
        if (!em.Exists(entity) || !em.HasBuffer<ContainedObjectsBuffer>(entity)) return -1;
        int n = 0;
        // Include equipment and cursor slots. Wearing or using received gear can change its
        // durability without changing which item was received.
        foreach (var item in em.GetBuffer<ContainedObjectsBuffer>(entity, true))
            if (Identity(item, expected)) n += Quantity(item);
        return n;
    }
    internal static bool BaseChestContext(World world, Entity player, int2 tile)
    {
        if (world == null || !world.IsCreated || !AIBase.ArrivedAtBase(world, player, out _)) return false;
        return AIBase.ChestsInView(world, player).Exists(c => math.all(AIBody.TileOf(c.pos) == tile) && c.dist <= AIBase.ChestReach && AIBase.Own(c) != null);
    }
    // "base", or an area under base ("base:area:x,y,s", the life tree's base area step): the base context applies.
    internal static bool IsBaseWhere(string where) => where == "base" || where != null && where.StartsWith("base:", StringComparison.Ordinal) && AIChestAreas.IsArea(where.Substring(5));
    private static bool InArea(string area, AIBase.ChestMem chest) => AIChestAreas.Within(new List<AIBase.ChestMem> { chest }, ChestTile, area).Count == 1;
    private static bool ChestWhere(World world, Entity player, string where, AIBase.ChestMem chest)
    {
        if (string.IsNullOrEmpty(where) || where == "null") return true;
        if (where == "base") return BaseChestContext(world, player, chest.tile);
        if (IsBaseWhere(where)) return BaseChestContext(world, player, chest.tile) && InArea(where.Substring(5), chest);
        if (AIChestAreas.IsArea(where)) return InArea(where, chest);   // an area id from chest_ids (review-2 N2, review-3 R1)
        return where == $"chest:{chest.tile.x},{chest.tile.y}";
    }
    // A store/take where the upper plan or a host order may name: base, a remembered chest's chest:x,y, or an area id
    // (area:x,y,s / base:area:x,y,s) holding at least one remembered chest (review-3 R1).
    private static bool KnownChestWhere(string where)
    {
        if (string.IsNullOrEmpty(where) || where == "base") return true;
        string area = AIChestAreas.IsArea(where) ? where : IsBaseWhere(where) ? where.Substring(5) : null;
        if (area != null) return AIBase.OwnChests().Exists(c => InArea(area, c));
        return AIBase.OwnChests().Exists(c => where == $"chest:{c.tile.x},{c.tile.y}");
    }
    internal static bool OwnChest(World world, Entity chest)
    {
        var em = world.EntityManager;
        if (!em.Exists(chest) || !em.HasComponent<InventoryAutoTransferEnabledCD>(chest) || !em.HasComponent<ObjectDataCD>(chest) || !em.HasComponent<LocalTransform>(chest) || !em.HasBuffer<ContainedObjectsBuffer>(chest) || !em.HasBuffer<InventoryBuffer>(chest)) return false;
        return AIBase.Own(new AIBase.ChestView { entity = chest, id = em.GetComponentData<ObjectDataCD>(chest).objectID, pos = em.GetComponentData<LocalTransform>(chest).Position }) != null;
    }
    internal static void Opened(Entity chest) { S.openChest = chest; }
    // Walk keys listed as an area's representative in this decision (review-5 G4).
    internal static HashSet<string> AreaRepWalks => S.areaRepWalks;   // per character (merge review L-6)
    public static bool IsAreaRepresentativeWalk(string key) => key != null && AreaRepWalks.Contains(key);
    internal static void CloseChest() { S.openChest = Entity.Null; }
    public static bool ChestIsOpen => S.openChest != Entity.Null;   // read-only: the chest open mode's real state
    // The chest the AI has open, still there, in view and within reach; otherwise the open state is released (the chest
    // was destroyed, the AI moved away or it is out of view) and `released` says why (상자 열림 모드가 갇히지 않게).
    internal static Entity ValidOpenChest(World world, Entity player, out string released)
    {
        released = null; var chest = S.openChest; if (chest == Entity.Null) return Entity.Null;
        var em = world.EntityManager;
        if (!em.Exists(chest) || !em.HasComponent<LocalTransform>(chest)) released = "연 상자가 없어짐";
        else
        {
            var view = AIBase.ChestsInView(world, player).Find(c => c.entity == chest);
            if (view == null) released = "연 상자가 보이지 않음";
            else if (view.dist > AIBase.ChestReach) released = $"연 상자에서 멀어짐({view.dist:F1}칸)";
        }
        if (released == null) return chest;
        S.openChest = Entity.Null; return Entity.Null;
    }
    internal static int Destination(World world, Entity entity, ContainedObjectsBuffer item, bool mainOnly, out int room)
    {
        room = 0; var em = world.EntityManager;
        if (!em.Exists(entity) || !em.HasBuffer<InventoryBuffer>(entity) || !em.HasBuffer<ContainedObjectsBuffer>(entity)) return -1;
        var inventories = em.GetBuffer<InventoryBuffer>(entity, true); int start = mainOnly ? MainStart(world, entity) : inventories.Length > 0 ? inventories[0].startIndex : 0;
        int end = mainOnly ? MainEnd(world, entity) : inventories.Length > 0 ? inventories[0].startIndex + inventories[0].size : 0;
        for (int i = start; i < end; i++)
        {
            if (!Read(world, entity, i, out var existing) || SlotAllows(world, entity, i, item) != null) continue;
            int inventory = -1; for (int j = 0; j < inventories.Length; j++) if (i >= inventories[j].startIndex && i < inventories[j].startIndex + inventories[j].size) { inventory = j; break; }
            if (inventory < 0 || inventories[inventory].objectsGetLockedInPlace) continue;
            bool one = inventories[inventory].canOnlyContainOneItemPerSlot;
            var info = Info(item);
            if (existing.objectID == ObjectID.None) { room = info?.isStackable == true ? (one ? 1 : 9999) : 1; return i; }
            if (!one && info?.isStackable == true && Identity(existing, item) && existing.amount < 9999) { room = 9999 - existing.amount; return i; }
        }
        return -1;
    }
    internal static bool HasStoreRoom(World world, Entity chest, ContainedObjectsBuffer item) =>
        item.objectID != ObjectID.None && Destination(world, chest, item, false, out int room) >= 0 && room > 0;

    // Menu data is made from actual slots, not the goal-filtered flat choices above. Each valid destination remains
    // a separate target; the first-fit helper is retained only for the legacy preparation path.
    internal static bool MenuChestOpened(World world, Entity player, Entity chest)
    {
        if (S.openChest != chest || !OwnChest(world, chest)) return false;
        return math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<LocalTransform>(chest).Position.xz) <= AIBase.ChestReach;
    }
    internal static AICandidate MenuChestAccess(World world, Entity player, AIObservation observation, AIBase.ChestMem memory)
    {
        var view = AIBase.ChestsInView(world, player).Find(c => c.id == memory.id && math.all(AIBody.TileOf(c.pos) == memory.tile) && AIBase.Own(c) != null);
        if (view == null)
        {
            var stands = AICraft.StandTilesFor(world, player, new AIMemory.Station { id = memory.id, tile = memory.tile });
            var path = AIApproach.FirstCached(world, player, stands, out int2 stand, out _);
            return path == null ? null : AIApproach.WalkCandidate(stand, $"상자 {memory.id}", false,
                $"기억한 상자 chest:{memory.tile.x},{memory.tile.y}로 가서 확인. 현재 내용은 모름. {AIBase.ChestText(memory, observation.pos)}. 도착 후 열어보기는 별도 선택.");
        }
        if (MenuChestOpened(world, player, view.entity)) return null;
        return Candidate(new AIInventoryMove { mode = "inspect", key = "inventory:inspect:" + view.entity.Index,
            label = $"상자 chest:{memory.tile.x},{memory.tile.y}에 접근해 열어 살펴보기. 내용을 본 뒤 물품을 고름",
            chest = view.entity, fromEntity = player, toEntity = view.entity }, false);
    }
    internal static Entity MenuVisibleChest(World world, Entity player, AIBase.ChestMem memory)
    {
        var view = AIBase.ChestsInView(world, player).Find(c => c.id == memory.id && math.all(AIBody.TileOf(c.pos) == memory.tile) && AIBase.Own(c) != null);
        return view?.entity ?? Entity.Null;
    }
    internal static AICandidate MenuCandidate(AIInventoryMove move) => Candidate(move, move.goal != null);
    private static string MenuKey(AIInventoryMove m) =>
        $"inventory:{m.mode}:{m.fromEntity.Index}:{m.from}:{m.toEntity.Index}:{m.to}:{m.count}:{(int)m.beforeFrom.objectID}:{m.beforeFrom.variation}:{m.beforeFrom.auxDataIndex}:{m.beforeFrom.amount}:{m.beforeFrom.variationUpdateCount}";
    internal static AIGoal MenuGoal(World world, Entity player, AIInventoryMove move)
    {
        var g = AIGoals.Goal;
        if (!OwnsGoal(g) || !ReferenceEquals(S.goal, g) || S.done || g.slot >= 0 && g.slot != move.from || g.item != ObjectID.None && g.item != move.beforeFrom.objectID) return null;
        bool mode = g.kind == GoalKind.Equip && move.mode == "equip" || g.kind == GoalKind.Unequip && move.mode == "unequip" ||
            g.kind == GoalKind.Hold && move.mode == "hold" || g.kind == GoalKind.Store && move.mode == "store" ||
            g.kind == GoalKind.Take && move.mode == "take" || g.kind == GoalKind.Drop && move.mode == "drop" || g.kind == GoalKind.Give && move.mode == "give";
        if (!mode) return null;
        if (move.mode == "equip" && !WantsSlot(g.where, move.destination)) return null;
        if (move.mode == "unequip")
        {
            var slot = GearSlots(world, player).Find(x => x.slot == move.from);
            if (!WantsSlot(g.where, slot.name)) return null;
        }
        if (move.mode == "hold" && g.where != "hand" && !WantsSlot(g.where, move.destination)) return null;
        if (move.mode == "store" || move.mode == "take")
        {
            var memory = AIBase.OwnChests().Find(x => MenuVisibleChest(world, player, x) == move.chest);
            if (memory == null || !ChestWhere(world, player, g.where, memory)) return null;
        }
        if (move.mode == "give" && Recipient(world, player, g.where)?.entity != move.recipient) return null;
        return g;
    }
    internal static int MenuRequestedCount(AIGoal goal) => goal != null && goal.count > 0 ? math.max(0, goal.count - S.moved) : 0;
    internal static int MenuMaximum(World world, Entity player, AIInventoryMove move)
    {
        int maximum = Quantity(move.beforeFrom);
        if (move.swap || move.mode == "equip" || move.mode == "unequip" || move.mode == "hold" || move.mode == "use") return 1;
        if (move.mode == "store" || move.mode == "take" || move.mode == "move")
        {
            var em = world.EntityManager;
            if (!em.HasBuffer<InventoryBuffer>(move.toEntity)) return 0;
            int inventory = -1; var inventories = em.GetBuffer<InventoryBuffer>(move.toEntity, true);
            for (int i = 0; i < inventories.Length; i++) if (move.to >= inventories[i].startIndex && move.to < inventories[i].startIndex + inventories[i].size) { inventory = i; break; }
            if (inventory < 0) return 0;
            if (inventories[inventory].objectsGetLockedInPlace) return 0;
            bool stack = Info(move.beforeFrom)?.isStackable == true;
            int room = move.beforeTo.objectID == ObjectID.None ? stack && !inventories[inventory].canOnlyContainOneItemPerSlot ? 9999 : 1 :
                stack && !inventories[inventory].canOnlyContainOneItemPerSlot && Identity(move.beforeFrom, move.beforeTo) ? math.max(0, 9999 - move.beforeTo.amount) : 0;
            maximum = math.min(maximum, room);
        }
        var goal = MenuGoal(world, player, move);
        if (move.mode == "store" && goal != null && goal.keep >= 0) maximum = math.min(maximum, math.max(0, Available(world, player, move.beforeFrom.objectID) - goal.keep));
        return maximum;
    }
    // 2026-11-08 빈 선택지 B4: items whose own game use is the held secondary (eat / cast) or an instrument note.
    internal static bool UseKind(ObjectInfo info) => info != null && (info.objectType == ObjectType.Eatable || info.objectType == ObjectType.CastingItem || info.objectType == ObjectType.Instrument);
    private static AIInventoryMove CrossSwap(World world, Entity from, Entity to, int fromSlot, int toSlot, string mode, string destination, string label, Entity chest)
    {
        if (!Read(world, from, fromSlot, out var a) || !Read(world, to, toSlot, out var b)) return null;
        return new AIInventoryMove { mode = mode, swap = true, label = label + $", 이전 {Text(b)} → 출발 칸", fromEntity = from, toEntity = to, from = fromSlot, to = toSlot, beforeFrom = a, beforeTo = b, destination = destination, chest = chest };
    }
    private static List<int> HotbarTargets(World world, Entity player) { var l = new List<int>(); for (int i = MainStart(world, player); i < math.min(10, MainEnd(world, player)); i++) l.Add(i); return l; }
    internal static List<AIInventoryMove> MenuMoves(World world, Entity player, Entity source, int sourceSlot, string mode, Entity chest, Entity recipient, string recipientName, int count)
    {
        var result = new List<AIInventoryMove>();
        if (!Read(world, source, sourceSlot, out var item) || item.objectID == ObjectID.None || count <= 0) return result;
        var info = Info(item); bool known = info != null;
        void Add(AIInventoryMove move)
        {
            if (move == null) return;
            move.goal = MenuGoal(world, player, move);
            if (count > MenuMaximum(world, player, move)) return;
            move.count = count; move.key = MenuKey(move);
            if (Stale(world, player, move) == null) result.Add(move);
        }
        bool fromPlayer = source == player, fromChest = !fromPlayer && source == chest && MenuChestOpened(world, player, chest);
        bool carried = fromPlayer && IsCarried(world, player, sourceSlot), gear = fromPlayer && IsGear(world, player, sourceSlot);
        string from = fromPlayer ? SlotName(world, player, sourceSlot) : $"상자 칸 {sourceSlot}";
        switch (mode)
        {
            case "equip":
                if (!known) break;
                foreach (var slot in GearSlots(world, player)) if (slot.type == info.objectType && slot.slot != sourceSlot)
                {
                    if (carried) Add(SwapMove(world, player, mode, sourceSlot, slot.slot, slot.name, null));
                    else if (fromChest) Add(CrossSwap(world, chest, player, sourceSlot, slot.slot, mode, slot.name, $"상자에서 바로 착용 {Text(item)} {from} → 착용 {slot.name}({slot.slot})", chest));
                }
                break;
            case "unequip":
                // 2026-11-08 B7: any empty carried slot (main or pouch) that accepts the item; storing/dropping/giving
                // a worn item directly is the store/drop/give operation on the worn slot.
                if (gear) foreach (int i in CarriedSlots(world, player)) if (Read(world, player, i, out var empty) && empty.objectID == ObjectID.None)
                    Add(SwapMove(world, player, mode, sourceSlot, i, SlotName(world, player, i), null));
                break;
            case "hold": case "use":
                if (!known || mode == "use" && (!UseKind(info) || !carried)) break;
                foreach (int h in HotbarTargets(world, player))
                {
                    string label = mode == "use" ? "사용(" + (info.objectType == ObjectType.Instrument ? "연주" : info.objectType == ObjectType.Eatable ? "먹기" : "쓰기") + ")" : "손에 들기";
                    if (carried && h == sourceSlot) Add(new AIInventoryMove { mode = mode, label = $"{label} {Text(item)} 핫바 {h}", fromEntity = player, toEntity = player, from = h, to = h, beforeFrom = item, beforeTo = item, destination = "hotbar:" + h });
                    else if (carried) { var m = SwapMove(world, player, mode, sourceSlot, h, "hotbar:" + h, null); if (m != null) m.label = $"{label} — " + m.label; Add(m); }
                    else if (fromChest && mode == "hold") Add(CrossSwap(world, chest, player, sourceSlot, h, mode, "hotbar:" + h, $"상자에서 바로 손에 들기 {Text(item)} {from} → 핫바 {h}", chest));
                }
                break;
            case "move":
                // 2026-11-08 B8 (and B1 pouch): within carried slots. Empty or same stack → MoveAmount with a chosen
                // amount (split / merge); another item → whole-slot swap. An item without DB info only moves whole.
                if (!carried) break;
                foreach (int i in CarriedSlots(world, player))
                {
                    if (i == sourceSlot || !Read(world, player, i, out var target)) continue;
                    string to = SlotName(world, player, i);
                    bool merge = known && target.objectID != ObjectID.None && info.isStackable && Identity(item, target);
                    if (known && (target.objectID == ObjectID.None || merge))
                        Add(new AIInventoryMove { mode = mode, label = $"옮기기 {Text(item)} {count}개, {from} → {to}{(merge ? "(같은 물품에 합치기)" : "(빈 칸)")}", fromEntity = player, toEntity = player, from = sourceSlot, to = i, beforeFrom = item, beforeTo = target, count = count, destination = to });
                    else if (count == 1 && (target.objectID != ObjectID.None || !known))
                    {
                        var m = SwapMove(world, player, mode, sourceSlot, i, to, null);
                        if (m != null) { m.swap = true; m.label = $"자리 바꾸기 {Text(item)} {from} ↔ {to} {Text(target)}"; }
                        Add(m);
                    }
                }
                break;
            case "store": case "take":
            {
                if (!MenuChestOpened(world, player, chest)) break;
                if (mode == "store" && !(carried || gear) || mode == "take" && !fromChest) break;
                Entity target = mode == "store" ? chest : player;
                var em = world.EntityManager; if (!em.HasBuffer<InventoryBuffer>(target)) break;
                var inv = em.GetBuffer<InventoryBuffer>(target, true);
                var targets = new List<int>();
                if (target == player) targets = CarriedSlots(world, player);
                else if (inv.Length > 0) for (int i = inv[0].startIndex; i < inv[0].startIndex + inv[0].size; i++) targets.Add(i);
                bool whole = gear || !known; // worn items and items without DB info move as a whole slot into an empty slot
                foreach (int i in targets) if (Read(world, target, i, out var beforeTo))
                {
                    string to = target == player ? SlotName(world, player, i) : $"상자 칸 {i}";
                    if (whole)
                    {
                        if (beforeTo.objectID != ObjectID.None || count != 1) continue;
                        Add(CrossSwap(world, source, target, sourceSlot, i, mode, to, $"{(mode == "store" ? "보관" : "꺼내기")} {Text(item)} 칸 전체, {from} → {to}", chest));
                    }
                    else Add(new AIInventoryMove { mode = mode, label = $"{(mode == "store" ? "보관" : "꺼내기")} {Text(item)} {count}개, {from} → {to}",
                        fromEntity = source, toEntity = target, from = sourceSlot, to = i, beforeFrom = item, beforeTo = beforeTo, chest = chest, count = count, destination = to });
                }
                break;
            }
            case "drop": case "give":
                if (!known || !(carried || gear) || gear && count != 1) break;
                Add(new AIInventoryMove { mode = mode, label = $"{Text(item)} {count}개 ({from}) {(mode == "drop" ? "현재 위치에 내려놓기" : recipientName + "에게 접근해 자연 드롭·수령 확인")}",
                    fromEntity = player, toEntity = player, from = sourceSlot, to = sourceSlot, beforeFrom = item, beforeTo = item, count = count, recipient = recipient, recipientName = recipientName ?? "" });
                break;
        }
        return result;
    }
    private static List<AICandidate> ChestCandidates(World world, Entity player, AIObservation observation, string key, AIGoal g, AIGoal progressGoal)
    {
        bool progress = progressGoal != null && ReferenceEquals(progressGoal, S.goal) && ReferenceEquals(progressGoal, AIGoals.Goal);
        var result = new List<AICandidate>(); if (progress && S.done) return result;
        if (IsBaseWhere(g.where) && !AIBase.ArrivedAtBase(world, player, out _))
        {
            // Explicit base is a destination, not an alias for any remembered owned chest.
            result.AddRange(AIBase.Candidates(world, player, observation, key, new AIGoal { kind = GoalKind.Home }));
            return result;
        }
        var visible = AIBase.ChestsInView(world, player); var memories = AIBase.OwnChests();
        if (S.openChest != Entity.Null && (!world.EntityManager.Exists(S.openChest) || math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<LocalTransform>(S.openChest).Position.xz) > AIBase.ChestReach)) S.openChest = Entity.Null;
        // Review-4 F2: the shared rule (AIChestAreas.Step). Up to 40 chests in the goal's scope each is a choice as before;
        // above that one per area of the life tree's own split of that scope (the open chest first, then the nearest),
        // and its route is not searched here (only when chosen). Every chest stays reachable through the life tree.
        // Review-5 G4: an area representative's walk is listed without its own route search here; the decision assembly
        // prepares every walk's route anyway (AIBrain PreparePlan) and drops a representative that has none (DropUnroutedRepresentatives).
        var scope = memories.FindAll(memory => ChestWhere(world, player, g.where, memory));
        string parent = AIChestAreas.IsArea(g.where) ? g.where : IsBaseWhere(g.where) && g.where != "base" ? g.where.Substring(5) : null;
        float3 here = observation.pos;
        Entity openChest = S.openChest;
        // Review-5 G1: the area's chest is chosen by what is remembered of it — a take prefers a chest seen holding the item,
        // a store one with a free slot or a stack to join; unopened next; a chest seen useless for this goal last — then the
        // open chest (only when not known useless), then distance. So an opened full / empty chest hands its small area to
        // the next chest at the next decision. This only orders the one shown per area; nothing is removed.
        int Use(AIBase.ChestMem m)
        {
            if (m.openedUnix == 0) return 1;
            if (g.kind == GoalKind.Take) return g.item != ObjectID.None && m.items.TryGetValue(g.item, out int n) && n > 0 ? 0 : 2;
            return m.free > 0 || g.item != ObjectID.None && m.items.ContainsKey(g.item) ? 0 : 2;
        }
        var step = AIChestAreas.Step(scope, ChestTile, parent, m =>
        {
            int use = Use(m);
            bool open = openChest != Entity.Null && visible.Exists(c => c.entity == openChest && math.all(AIBody.TileOf(c.pos) == m.tile));
            return open && use < 2 ? -1f : use * 100000f + math.distance(here.xz, (float2)m.tile);
        });
        foreach (var (memory, area) in step)
        {
            string areaNote = area == null ? "" : $" {area.id} 구역(상자 {area.items.Count}개)의 대표(기억 내용상 이 일에 쓸모 있어 보이는 것 먼저, 그다음 가까운 것); 그 구역의 다른 상자는 생활 보관/꺼내기에서 구역을 골라 고름";
            var chest = visible.Find(c => math.all(AIBody.TileOf(c.pos) == memory.tile) && c.id == memory.id && AIBase.Own(c) != null);
            if (chest == null)
            {
                var stands = AICraft.StandTilesFor(world, player, new AIMemory.Station { id = memory.id, tile = memory.tile });
                int2 stand;
                if (area == null)
                {
                    var route = AIApproach.FirstCached(world, player, stands, out stand, out _);
                    if (route == null) continue;
                }
                else
                {
                    if (stands.Count == 0) continue;
                    stand = stands[0]; foreach (var t in stands) if (math.distance(here.xz, (float2)t) < math.distance(here.xz, (float2)stand)) stand = t;
                }
                var walk = AIApproach.WalkCandidate(stand, $"상자 {memory.id}", true, $"기억한 상자 chest:{memory.tile.x},{memory.tile.y}로 가서 확인 — 지금 내용은 모름. {AIBase.ChestText(memory, observation.pos)}.{areaNote}{(area != null ? "." : "")} 옆에 도착한 뒤 열기와 물품 이동은 다음 선택.");
                if (walk.key != key && !result.Exists(r => r.key == walk.key)) { result.Add(walk); if (area != null) AreaRepWalks.Add(walk.key); }
                continue;
            }
            if (S.openChest != chest.entity || chest.dist > AIBase.ChestReach)
            {
                var inspect = new AIInventoryMove { mode = "inspect", key = "inventory:inspect:" + chest.entity.Index + "." + chest.entity.Version, label = $"상자 chest:{memory.tile.x},{memory.tile.y}에 다가가 열어 살펴보기(현재 내용은 다음 판단에 관측){areaNote}", chest = chest.entity, fromEntity = player, toEntity = chest.entity, goal = progressGoal };
                if (inspect.key != key) result.Add(Candidate(inspect, true));
                continue;
            }
            var em = world.EntityManager; var ci = em.GetBuffer<InventoryBuffer>(chest.entity, true); if (ci.Length == 0) continue;
            bool storing = g.kind == GoalKind.Store;
            Entity source = storing ? player : chest.entity, target = storing ? chest.entity : player;
            int start = storing ? MainStart(world, player) : ci[0].startIndex;
            int end = storing ? MainEnd(world, player) : ci[0].startIndex + ci[0].size;
            for (int i = start; i < end; i++)
            {
                if ((g.slot >= 0 && g.slot != i) || !Read(world, source, i, out var item) || item.objectID == ObjectID.None || (g.item != ObjectID.None && item.objectID != g.item)) continue;
                if (storing && g.item == ObjectID.None && g.slot < 0 && !AIBase.StorableKind(world, item.objectID, item.variation)) continue;
                int remaining = g.count > 0 ? g.count - (progress && ReferenceEquals(g, progressGoal) ? S.moved : 0) : 0; if (g.count > 0 && remaining <= 0) continue;
                int total = storing ? Available(world, player, item.objectID) : Quantity(item);
                int count = AIInventoryRules.MoveCount(total, Quantity(item), remaining, storing ? g.keep : 0);
                int dest = Destination(world, target, item, !storing, out int room); count = math.min(count, room);
                if (dest < 0 || count <= 0 || !Read(world, target, dest, out var beforeTo)) continue;
                var move = new AIInventoryMove { mode = storing ? "store" : "take", key = $"inventory:{(storing ? "store" : "take")}:{chest.entity.Index}.{chest.entity.Version}:{i}:{item.objectID}:v{item.variation}:{item.auxDataIndex}:{item.variationUpdateCount}:{dest}:{count}", label = $"{(storing ? "보관" : "꺼내기")} {Text(item)} {count}개, 출발 슬롯 {i} → 목적 슬롯 {dest}, 상자 chest:{memory.tile.x},{memory.tile.y}{(storing && g.keep >= 0 ? $", 가방에 {g.keep}개 남김" : "")}", fromEntity = source, toEntity = target, from = i, to = dest, beforeFrom = item, beforeTo = beforeTo, count = count, goal = progressGoal, chest = chest.entity };
                if (move.key != key && Stale(world, player, move) == null) result.Add(Candidate(move, progress));
            }
        }
        return result;
    }
    internal static bool RecipientVisible(World world, Entity player, string name) => Recipient(world, player, name) != null;
    private static SeenPlayer Recipient(World world, Entity player, string name)
    {
        return AITeammates.Visible(world, player).Find(p => (p.maxHealth <= 0 || p.health > 0) && ((name == "host" || name == "호스트") && !p.isAi || string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)));
    }
    private static List<AICandidate> DropCandidates(World world, Entity player, AIObservation observation, string key, AIGoal goal)
    {
        var result = new List<AICandidate>(); if (ReferenceEquals(S.goal, goal) && S.done) return result;
        var recipient = goal.kind == GoalKind.Give ? Recipient(world, player, goal.where) : null;
        if (goal.kind == GoalKind.Give && recipient == null) return result;
        for (int i = MainStart(world, player); i < MainEnd(world, player); i++)
        {
            if ((goal.slot >= 0 && goal.slot != i) || !Read(world, player, i, out var item) || item.objectID == ObjectID.None || item.objectID != goal.item) continue;
            int count = math.min(9999, math.min(Quantity(item), goal.count - S.moved)); if (count <= 0) continue;
            var move = new AIInventoryMove { mode = goal.kind == GoalKind.Give ? "give" : "drop", key = $"inventory:{(goal.kind == GoalKind.Give ? "give" : "drop")}:{i}:{count}:{recipient?.entity.Index ?? 0}", label = $"{Text(item)} {count}개를 {(recipient == null ? "현재 위치 바닥에 내려놓기" : recipient.name + "에게 가까이 가서 바닥을 통해 건네기(실제 수령 확인 필요)")}", fromEntity = player, toEntity = player, from = i, to = i, beforeFrom = item, beforeTo = item, count = count, goal = goal, recipient = recipient?.entity ?? Entity.Null, recipientName = recipient?.name ?? "" };
            if (move.key != key && Stale(world, player, move) == null) result.Add(Candidate(move, true));
        }
        return result;
    }
    public static string CheckGoal(World world, Entity player, GoalKind kind, ObjectID item, int count, string where, int keep, int slot)
    {
        if (count < 0 && (kind == GoalKind.Take || kind == GoalKind.Drop || kind == GoalKind.Give)) count = 1;
        string gate = Gate(world, player); if (gate != null) return gate;
        if (kind == GoalKind.Store || kind == GoalKind.Take)
        {
            if (kind == GoalKind.Take && (item == ObjectID.None || count <= 0)) return "꺼낼 물품과 양수 개수 필요";
            if (kind == GoalKind.Store && item == ObjectID.None && slot < 0 && (count > 0 || keep >= 0)) return "보관 개수/남길 개수를 적용할 품목 지정 필요";
            if (!KnownChestWhere(where)) return "알고 있는 상자 식별자가 아님(chest:x,y, 또는 기억한 상자가 든 구역 area:x,y,크기)";
            if (kind == GoalKind.Take && !IsBaseWhere(where) && !AIBase.OwnChests().Exists(c => ChestWhere(world, player, where, c))) return "지정 범위에 알고 있는 상자가 없음";
            if (kind == GoalKind.Store && slot >= 0 && (!IsMain(world, player, slot) || !Read(world, player, slot, out var selected) || selected.objectID == ObjectID.None || item != ObjectID.None && item != selected.objectID)) return "보관할 출발 가방 슬롯/물품이 없음";
            return null;
        }
        if (kind == GoalKind.Drop || kind == GoalKind.Give)
        {
            if (item == ObjectID.None || count <= 0) return "내려놓거나 전달할 물품과 양수 개수 필요";
            if (Available(world, player, item, slot) <= 0) return "지정 물품/출발 슬롯이 가방에 없음";
            if (kind == GoalKind.Give && Recipient(world, player, where) == null) return "지정 수령자가 현재 보이지 않거나 쓰러져 있음";
            return null;
        }
        if (kind != GoalKind.Equip && kind != GoalKind.Unequip && kind != GoalKind.Hold) return "지원하지 않는 가방 동작";
        if (kind != GoalKind.Unequip && item == ObjectID.None) return "물품 지정 필요";
        if (kind == GoalKind.Unequip && item == ObjectID.None && string.IsNullOrEmpty(where) && slot < 0) return "해제할 물품 또는 착용 슬롯 지정 필요";
        if (slot >= 0 && !Read(world, player, slot, out _)) return "출발 슬롯 범위 밖";
        if (kind == GoalKind.Hold && !string.IsNullOrEmpty(where) && where != "hand" && !(where.StartsWith("hotbar:") && int.TryParse(where.Substring(7), out int h) && h >= 0 && h < 10)) return "손 목적지는 hand 또는 hotbar:0..9";
        if (kind != GoalKind.Hold && !string.IsNullOrEmpty(where) && !GearSlots(world, player).Exists(x => x.name == Canonical(where))) return "현재 활성 장비 슬롯 식별자가 아님";
        bool found = false;
        if (kind == GoalKind.Unequip)
        {
            foreach (var s in GearSlots(world, player)) if (WantsSlot(where, s.name) && (slot < 0 || slot == s.slot) && Read(world, player, s.slot, out var a) && a.objectID != ObjectID.None && (item == ObjectID.None || item == a.objectID)) found = true;
        }
        else
        {
            for (int i = MainStart(world, player); i < MainEnd(world, player); i++) if ((slot < 0 || slot == i) && Read(world, player, i, out var a) && a.objectID == item) found = true;
        }
        return found ? null : "지정한 물품이 현재 요청한 가방/장비 슬롯에 없음";
    }

    public static string Stale(World world, Entity player, AIInventoryMove m)
    {
        if (m == null) return "물품 선택 없음";
        if (m.goal != null && !ReferenceEquals(m.goal, AIGoals.Goal)) return "이 물품 선택의 원래 목표가 이미 바뀜";
        if (IsBaseWhere(m.goal?.where) && (m.mode == "inspect" || m.mode == "store" || m.mode == "take") && world.EntityManager.Exists(m.chest) && world.EntityManager.HasComponent<LocalTransform>(m.chest) && !BaseChestContext(world, player, AIBody.TileOf(world.EntityManager.GetComponentData<LocalTransform>(m.chest).Position))) return "명시한 기지에서 현재 관측·조작할 상자 선택이 아님";
        string gate = Gate(world, player); if (gate != null) return gate;
        string barrier = AIMaintenance.SlotBarrier(world, player, m); if (barrier != null) return barrier;
        if (m.mode == "inspect") return OwnChest(world, m.chest) ? null : "상자가 사라졌거나 형식이 바뀜";
        if (AIInventoryBulk.Owns(m.mode)) return AIInventoryBulk.Stale(world, player, m);
        bool maintenance = m.mode == "maintenance_put" || m.mode == "maintenance_take";
        bool chestSide = !maintenance && (m.fromEntity != player || m.toEntity != player);
        if (maintenance)
        {
            string pending = AIMaintenance.StaleMove(world, player, m); if (pending != null) return pending;
            var em = world.EntityManager;
            if (m.maintenance == null || m.goal != null || m.count != 1 || !em.Exists(m.chest) || !em.HasComponent<ObjectDataCD>(m.chest) || em.GetComponentData<ObjectDataCD>(m.chest).objectID != ObjectID.UpgradeForge) return "정확한 자기 정비 투입/회수 대상이 아님";
            if (m.mode == "maintenance_put" && (m.fromEntity != player || m.toEntity != m.chest || (!IsCarried(world, player, m.from) && !IsGear(world, player, m.from)))) return "현재 자기 가방·파우치/활성 장비에서 선택 대장간으로만 투입 가능";
            if (m.mode == "maintenance_take" && (m.fromEntity != m.chest || m.toEntity != player || !IsMain(world, player, m.to))) return "자기 pending 대장간에서 현재 main 가방으로만 회수 가능";
        }
        if (chestSide)
        {
            // 2026-11-08: every chest is public (AIBase.Usable/Own); the chest must still be opened and within reach.
            if (!OwnChest(world, m.chest)) return "상자가 사라졌거나 형식이 바뀜";
            if (math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<LocalTransform>(m.chest).Position.xz) > AIBase.ChestReach) return "상자를 조작할 거리 밖";
            if (S.openChest != m.chest) return "먼저 상자를 열어 살펴봐야 함";
            bool toChest = m.fromEntity == player && m.toEntity == m.chest, fromChest = m.fromEntity == m.chest && m.toEntity == player;
            bool ok = m.mode == "store" && toChest && (IsCarried(world, player, m.from) || m.swap && IsGear(world, player, m.from)) ||
                m.mode == "take" && fromChest && IsCarried(world, player, m.to) ||
                (m.mode == "equip" || m.mode == "hold") && m.swap && fromChest;
            if (!ok) return "허용된 가방/상자 이동과 선택이 다름";
        }
        bool amountMove = maintenance || !m.swap && (m.mode == "store" || m.mode == "take" || m.mode == "move");
        if (amountMove)
        {
            if (m.mode == "move" && (m.fromEntity != player || m.toEntity != player || !IsCarried(world, player, m.from) || !IsCarried(world, player, m.to) || m.from == m.to)) return "가방·파우치 안 옮기기의 출발/목적 칸이 바뀜";
            if (!Read(world, m.fromEntity, m.from, out var from) || !from.EqualsExact(m.beforeFrom)) return "이동의 출발 물품/상태가 바뀜";
            if (!CanRemove(world, m.fromEntity, m.from)) return "출발 인벤토리의 물품 꺼내기가 제한됨";
            if (!Read(world, m.toEntity, m.to, out var to) || !to.EqualsExact(m.beforeTo)) return "이동의 목적 슬롯/상태가 바뀜";
            if (m.count <= 0 || Quantity(from) < m.count || (to.objectID != ObjectID.None && (Info(from)?.isStackable != true || !Identity(from, to)))) return "선택한 수량/중첩 조건이 바뀜";
            string allows = SlotAllows(world, m.toEntity, m.to, from); if (allows != null) return allows;
            if (maintenance && to.objectID != ObjectID.None) return "정비 투입/회수의 선택 목적 슬롯이 더는 비어 있지 않음";
            if (m.mode == "maintenance_put") { string capacity = CapacityAfterRemoval(world, player, m.from); if (capacity != null) return capacity; }
            if (m.goal != null && m.mode == "store" && Available(world, player, from.objectID) - m.count < math.max(0, m.goal.keep)) return "남길 수량보다 가방 물품이 줄어듦";
            return null;
        }
        if (!chestSide && (m.fromEntity != player || m.toEntity != player)) return "아직 연결되지 않은 외부 인벤토리 동작";
        if (!Read(world, m.fromEntity, m.from, out var a) || !a.EqualsExact(m.beforeFrom)) return "출발 슬롯의 물품/상태가 바뀜";
        if (!Read(world, m.toEntity, m.to, out var b) || !b.EqualsExact(m.beforeTo)) return "목적 슬롯의 물품/상태가 바뀜";
        if (a.objectID == ObjectID.None) return "출발 슬롯이 비어 있음";
        if (m.mode == "drop" || m.mode == "give")
        {
            bool gearSource = IsGear(world, player, m.from);
            if (!(IsCarried(world, player, m.from) || gearSource) || m.count <= 0 || Quantity(a) < m.count || gearSource && m.count != 1) return "내려놓을 가방·파우치·착용 물품/수량이 바뀜";
            if (gearSource) { string capacity = CapacityAfterRemoval(world, player, m.from); if (capacity != null) return capacity; }
            if (m.mode == "give")
            {
                var recipient = Recipient(world, player, m.recipientName);
                if (recipient == null || recipient.entity != m.recipient) return "선택한 수령자가 현재 보이지 않거나 달라짐";
            }
            return null;
        }
        var slots = GearSlots(world, player);
        bool sourceOk = m.fromEntity == player ? IsCarried(world, player, m.from) : chestSide;
        if (m.mode == "equip")
        {
            var info = Info(a);
            if (!sourceOk || info == null || m.toEntity != player || !slots.Exists(x => x.slot == m.to && x.type == info.objectType && x.name == m.destination)) return "현재 장비 타입/활성 슬롯과 선택이 다름";
            if (PlayerController.ItemIsBroken(a.objectData)) return "고장 난 장비";
        }
        else if (m.mode == "unequip")
        {
            if (!slots.Exists(x => x.slot == m.from) || !IsCarried(world, player, m.to) || b.objectID != ObjectID.None) return "해제할 장비/빈 가방·파우치 칸이 바뀜";
        }
        else if (m.mode == "hold" || m.mode == "use")
        {
            var info = Info(a);
            if (!sourceOk || m.toEntity != player || m.to < 0 || m.to >= math.min(10, MainEnd(world, player)) || info == null || m.mode == "use" && !UseKind(info)) return "현재 손에 들/쓸 물품이나 핫바 슬롯을 확인할 수 없음";
        }
        else if (m.mode == "move")
        {
            if (!m.swap || !IsCarried(world, player, m.from) || !IsCarried(world, player, m.to)) return "가방·파우치 안 자리 바꾸기의 칸이 바뀜";
        }
        else if (m.mode == "store" || m.mode == "take")
        {
            if (!m.swap || b.objectID != ObjectID.None) return "칸 전체 보관/꺼내기의 빈 목적 칸이 바뀜";
        }
        else return "아직 연결되지 않은 가방 동작";
        if (m.from != m.to || m.fromEntity != m.toEntity)
        {
            if (!CanRemove(world, m.fromEntity, m.from) && !(m.fromEntity == player && IsGear(world, player, m.from))) return "출발 인벤토리의 물품 꺼내기가 제한됨";
            string valid = SlotAllows(world, m.toEntity, m.to, a); if (valid != null) return valid;
            valid = SlotAllows(world, m.fromEntity, m.from, b); if (valid != null) return valid;
            valid = CapacityAfterSwap(world, player, m); if (valid != null) return valid;
        }
        return null;
    }
    internal static bool CanRemove(World world, Entity entity, int slot)
    {
        var em = world.EntityManager;
        if (!em.HasBuffer<InventoryBuffer>(entity)) return false;
        foreach (var inventory in em.GetBuffer<InventoryBuffer>(entity, true))
            if (slot >= inventory.startIndex && slot < inventory.startIndex + inventory.size) return !inventory.objectsGetLockedInPlace;
        return false;
    }
    internal static bool ActiveSlotsMatch(World world, Entity player, AIInventoryMove move)
    {
        if (move.mode == "equip") return GearSlots(world, player).Exists(x => x.slot == move.to && x.name == move.destination);
        if (move.mode == "unequip") return GearSlots(world, player).Exists(x => x.slot == move.from);
        return true;
    }
    internal static string SlotAllows(World world, Entity entity, int slot, ContainedObjectsBuffer value)
    {
        if (value.objectID == ObjectID.None) return null;
        var em = world.EntityManager; if (!em.HasBuffer<InventoryBuffer>(entity)) return "인벤토리 설정 없음";
        var iv = em.GetBuffer<InventoryBuffer>(entity, true); int inventory = -1, local = -1;
        for (int i = 0; i < iv.Length; i++) if (slot >= iv[i].startIndex && slot < iv[i].startIndex + iv[i].size) { inventory = i; local = slot - iv[i].startIndex; break; }
        if (inventory < 0) return "현재 활성 인벤토리 범위 밖";
        if (iv[inventory].cantAddObjectsToInventory) return "목적 인벤토리 넣기 제한";
        if (!em.HasBuffer<InventorySlotRequirementBuffer>(entity)) return null;
        var requirements = em.GetBuffer<InventorySlotRequirementBuffer>(entity, true); Entity prefab = Prefab(world, value); ulong tags = prefab != Entity.Null && em.HasComponent<ObjectCategoryTagsCD>(prefab) ? em.GetComponentData<ObjectCategoryTagsCD>(prefab).tagsBitMask : 0;
        foreach (var r in requirements)
        {
            if (r.inventoryIndex != inventory || (!r.requirementAppliesToAllSlots && r.slotIndex != local)) continue;
            var info = Info(value);
            if (r.denyLegendaryRarity && info != null && info.rarity == Rarity.Legendary && (prefab == Entity.Null || !em.HasComponent<OverrideLegendaryForSlotRequirementsCD>(prefab))) return "목적 슬롯의 전설 등급 제한";
            if (r.acceptsObjectsWithTags != 0 && (tags & r.acceptsObjectsWithTags) == 0) return "목적 슬롯 태그 조건 불일치";
            if (r.acceptsObjectIds.Length > 0) { bool found = false; foreach (var id in r.acceptsObjectIds) if (id == value.objectID) found = true; if (!found) return "목적 슬롯 품목 조건 불일치"; }
            break;
        }
        return null;
    }
    internal static Entity Prefab(World world, ContainedObjectsBuffer value)
    {
        if (value.objectID == ObjectID.None) return Entity.Null;
        var em = world.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            return q.CalculateEntityCount() == 1 ? PugDatabase.GetPrimaryPrefabEntity(value.objectID, q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob, value.variation) : Entity.Null;
    }
    private static ExtraInventoryCD Extra(World world, ContainedObjectsBuffer item)
    {
        var em = world.EntityManager; Entity p = Prefab(world, item);
        if (p != Entity.Null && em.HasBuffer<LevelEntitiesBuffer>(p) && em.HasComponent<LevelCD>(p))
        {
            var levels = em.GetBuffer<LevelEntitiesBuffer>(p, true); int level = item.variation > 0 ? math.min(LevelScaling.GetMaxLevel(), item.variation) : em.GetComponentData<LevelCD>(p).level;
            if (level >= 0 && level < levels.Length) p = levels[level].entity;
            else return default;
        }
        else return default;
        return p != Entity.Null && em.HasComponent<ExtraInventoryCD>(p) ? em.GetComponentData<ExtraInventoryCD>(p) : default;
    }
    private static string CapacityAfterSwap(World world, Entity player, AIInventoryMove move)
    {
        // Only the player side of a swap changes the player capacity providers (bag/pouch gear slots).
        var replacements = new Dictionary<int, ContainedObjectsBuffer>();
        if (move.fromEntity == player) replacements[move.from] = move.beforeTo;
        if (move.toEntity == player) replacements[move.to] = move.beforeFrom;
        return CapacityAfterReplacement(world, player, replacements);
    }

    internal static string CapacityAfterRemoval(World world, Entity player, int sourceSlot)
        => CapacityAfterReplacement(world, player, new Dictionary<int, ContainedObjectsBuffer> { [sourceSlot] = default });

    internal static string CapacityAfterReplacement(World world, Entity player, IReadOnlyDictionary<int, ContainedObjectsBuffer> replacements)
    {
        var em = world.EntityManager; var inventories = em.GetBuffer<InventoryBuffer>(player, true);
        for (int i = 0; i < inventories.Length; i++)
        {
            var inventory = inventories[i]; int provider = inventory.extraInventorySizeSlot;
            if (provider < 0 || !replacements.TryGetValue(provider, out var replacement)) continue;
            var extra = Extra(world, replacement);
            int end = inventory.startIndex + inventory.sizeX * inventory.sizeY + extra.size;
            bool newTags = inventory.extraInventoryCategoryTagsMask != extra.categoryTagsMask;
            for (int s = inventory.startIndex; s < math.min(inventory.startIndex + inventory.maxSize, em.GetBuffer<ContainedObjectsBuffer>(player, true).Length); s++)
            {
                if (!Read(world, player, s, out var item)) continue;
                if (replacements.TryGetValue(s, out var replaced)) item = replaced;
                if (item.objectID == ObjectID.None) continue;
                if (s >= end) return "가방/파우치 축소로 물건이 떨어질 슬롯이 있음: " + s;
                if (newTags && extra.size < inventory.extraSize) return "가방/파우치 용량과 태그를 함께 줄이면 게임이 기존 슬롯 물품을 떨어뜨림: " + s;
                if (newTags && extra.categoryTagsMask != 0)
                {
                    Entity prefab = Prefab(world, item); ulong tags = prefab != Entity.Null && em.HasComponent<ObjectCategoryTagsCD>(prefab) ? em.GetComponentData<ObjectCategoryTagsCD>(prefab).tagsBitMask : 0;
                    if ((tags & extra.categoryTagsMask) == 0) return "가방/파우치 변경 후 태그가 맞지 않아 떨어질 물품이 있음: " + s;
                }
            }
        }
        return null;
    }
    internal static void Completed(World world, Entity player, AIInventoryMove move, string result, int moved = 1)
    {
        if (move.mode == "maintenance_put" || move.mode == "maintenance_take") return;
        if (move.mode == "hold" && Read(world, player, move.to, out var item))
        {
            var h = new Hand { slot = move.to, item = item }; var info = Info(item);
            if (info != null && AIHandCapabilities.IsMiningTool(info.objectType.ToString())) S.mining = h; else S.weapon = h;
        }
        if (move.goal != null && ReferenceEquals(move.goal, AIGoals.Goal) && ReferenceEquals(S.goal, move.goal))
        {
            bool transfer = move.mode == "store" || move.mode == "take" || move.mode == "drop" || move.mode == "give";
            if (transfer) S.moved += math.max(0, moved);
            S.done = !transfer || move.goal.count > 0 && S.moved >= move.goal.count || move.mode == "store" && move.goal.count <= 0 && StoreRemaining(world, player, move.goal) == 0;
            S.result = result + (transfer ? $" · 이 목표의 확인된 이동 {S.moved}/{(move.goal.count > 0 ? move.goal.count.ToString() : "대상 전량(남길 수량 제외)")}" : "");
        }
    }
    public static bool TryChosenHand(World world, Entity player, bool forMining, out int slot, out string what, out string why)
    {
        slot = -1; what = ""; why = ""; var h = forMining ? S.mining : S.weapon; if (h == null) return false;
        what = h.item.objectID.ToString();
        if (!Read(world, player, h.slot, out var item) || !IsMain(world, player, h.slot) || h.slot >= 10 || !Identity(item, h.item)) { why = "AI가 고른 손 장비의 슬롯/물품이 바뀜"; return true; }
        if (PlayerController.ItemIsBroken(item.objectData)) { why = "AI가 고른 손 장비가 고장 남"; return true; }
        var info = Info(item); if (info == null || (forMining ? !AIHandCapabilities.IsMiningTool(info.objectType.ToString()) : !AIHandCapabilities.IsWeapon(info.objectType.ToString()))) { why = "AI가 고른 손 물품은 현재 작업의 도구/무기가 아님(다른 물품으로 바꾸지 않음)"; return true; }
        slot = h.slot; return true;
    }
    public static string PromptLine(World world, Entity player)
    {
        var parts = new List<string>(); foreach (var s in GearSlots(world, player)) if (Read(world, player, s.slot, out var item) && item.objectID != ObjectID.None) parts.Add($"{s.name}[{s.slot}]={Text(item)}");
        string receipt = ReferenceEquals(S.goal, AIGoals.Goal) && S.goal?.kind == GoalKind.Give
            ? $". 현재 전달 목표 '{S.goal.Text}'의 확인된 수령 {S.moved}/{S.goal.count}, 완료 관측 {(S.done ? "있음" : "없음")}" : "";
        int presets = AIInventoryBulk.PresetCount(world, player); var pouches = Pouches(world, player); int pouchItems = 0, pouchSlots = 0;
        foreach (var pouch in pouches) for (int i = pouch.start; i < pouch.end; i++) { pouchSlots++; if (Read(world, player, i, out var x) && x.objectID != ObjectID.None) pouchItems++; }
        string extra = (presets > 1 ? $". 착용 프리셋 {AIInventoryBulk.ActivePreset(world, player) + 1}/{presets}" : "") + (pouches.Count > 0 ? $". 파우치 칸 {pouchSlots}개 중 물품 {pouchItems}칸" : "");
        return "착용: " + (parts.Count > 0 ? string.Join(", ", parts) : "없음") + extra + ". " + (Gate(world, player) ?? "가방 조작 가능") + receipt + (ReferenceEquals(S.goal, AIGoals.Goal) && !string.IsNullOrEmpty(S.result) ? ". 최근 확인: " + S.result : "");
    }
    private static string Esc(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    private static string ChestIdJson(string id, string desc) => "{\"id\":\"" + Esc(id) + "\",\"desc\":\"" + Esc(desc) + "\"}";
    public static string ReportJson(World world, Entity player)
    {
        var slots = new List<string>();
        for (int i = MainStart(world, player); i < MainEnd(world, player); i++) if (Read(world, player, i, out var a) && a.objectID != ObjectID.None) slots.Add($"{{\"slot\":{i},\"item\":\"{a.objectID}\",\"variation\":{a.variation},\"state\":{a.amount},\"count\":{Quantity(a)}}}");
        // Every remembered chest is shared (any owner); many chests are reported by area so the report does not grow
        // without bound (review I3/L12).
        var all = AIBase.OwnChests(); var areas = AIChestAreas.Split(all, AIInventory.ChestTile, null);
        // review-3 R1: {id, desc} — id is exactly what store/take where accepts (chest:x,y or an area id).
        var chests = areas == null ? all.ConvertAll(c => ChestIdJson($"chest:{c.tile.x},{c.tile.y}", c.id + (c.placed ? " 내가 놓음" : " 공용") + (c.openedUnix == 0 ? ", 안 열어 봄" : ""))) : areas.ConvertAll(a => ChestIdJson(a.id, $"구역 상자 {a.items.Count}개"));
        return "{\"bag_slots\":[" + string.Join(",", slots) + "],\"equipment\":\"" + Esc(PromptLine(world, player)) + "\",\"chest_ids\":[" + string.Join(",", chests) + "],\"operations\":[\"equip\",\"unequip\",\"hold\",\"use\",\"move\",\"store\",\"take\",\"drop\",\"give\",\"sort\",\"quickstack\",\"preset\"]}";
    }
}

public sealed class InventorySkill : AISkill, ILifeResult
{
    public int ConfirmedWork { get; private set; }
    public int ConfirmedReceived { get; private set; }
    public int RequestedWork => _move?.count ?? 0;
    public string ResultStage { get; private set; } = "미실행";
    private readonly AIInventoryMove _move;
    private readonly InventorySwapProgress _exchange;
    private bool _sent, _swapped;
    private float _sentAt;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn;
    private HashSet<Entity> _floorBefore;
    private Entity _floor;
    private ContainedObjectsBuffer _floorInitial;
    private bool _sawRecipient;
    private bool _pickupReceipt;
    private int _recipientBefore = -1;
    private float3 _dropPosition;
    private InventoryDeliveryObservationSystem _deliveryObserver;
    private InventoryDeliverySourceObservationSystem _sourceObserver;
    private Entity _sender;
    private string _deliveryObservation = "";
    private bool _ambiguousFloor;
    private bool _otherRecipient;
    public InventorySkill(AIInventoryMove move) { _move = move; _exchange = move == null ? null : new InventorySwapProgress(AIInventory.Stamp(move.beforeFrom), AIInventory.Stamp(move.beforeTo)); Name = "가방 조작"; Key = move?.key ?? "inventory:missing"; }
    public override bool Start(World world, Entity player, float now)
    {
        string stale = AIInventory.Stale(world, player, _move);
        if (stale != null) { Status = SkillStatus.Failed; Reason = stale; return false; }
        if (_move.mode == "inspect" || AmountMove || _move.mode == "drop" || _move.mode == "give" || AIInventoryBulk.Owns(_move.mode)) { Facts.Add(_move.label); return true; }
        if (!AIInventory.Read(world, _move.fromEntity, _move.from, out var from) || !AIInventory.Read(world, _move.toEntity, _move.to, out var to) || !_exchange.MayRequest(AIInventory.Stamp(from), AIInventory.Stamp(to))) { Status = SkillStatus.Failed; Reason = "교환할 슬롯 상태가 이미 바뀜"; return false; }
        Facts.Add(_move.label); return true;
    }
    // MoveAmount family: chest store/take, carried move/split/merge and the maintenance transfers. Swap moves are whole slots.
    private bool AmountMove => _move.mode == "maintenance_put" || _move.mode == "maintenance_take" || !_move.swap && (_move.mode == "store" || _move.mode == "take" || _move.mode == "move");
    public override BodyInput Tick(World world, Entity player, float now)
    {
        if (_move.mode == "inspect") return InspectTick(world, player, now);
        if (AIInventoryBulk.Owns(_move.mode)) return BulkTick(world, player, now);
        if (AmountMove) return TransferTick(world, player, now);
        if (_move.mode == "drop" || _move.mode == "give") return DropTick(world, player, now);
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            _sent = true; _sentAt = now;
            if (_move.from != _move.to || _move.fromEntity != _move.toEntity)
            {
                if (!AICraft.QueueInventoryChange(world, player, Create.Swap(_move.fromEntity, _move.toEntity, _move.from, _move.to))) return Finish(SkillStatus.Failed, "교환 요청 큐에 넣지 못함");
                Facts.Add($"Swap queued {_move.fromEntity}:{_move.from}↔{_move.toEntity}:{_move.to}; completion awaits both slots");
            }
            _exchange.RequestedOnce();
        }
        if (!_swapped && AIInventory.Read(world, _move.fromEntity, _move.from, out var a) && AIInventory.Read(world, _move.toEntity, _move.to, out var b) && _exchange.Observe(AIInventory.Stamp(a), AIInventory.Stamp(b))) _swapped = true;
        if (_swapped)
        {
            if (!AIInventory.ActiveSlotsMatch(world, player, _move)) return Finish(SkillStatus.Failed, "양 슬롯 교환은 관측했지만 활성 장비 프리셋이 바뀌어 현재 착용 완료로 처리하지 않음");
            if (_move.mode == "use") return UseTick(world, player, now);
            if (_move.mode == "store" || _move.mode == "take")
            {
                ConfirmedWork = 1; ConfirmedReceived = _move.mode == "take" ? 1 : 0; ResultStage = _move.mode == "take" ? "가방·파우치 수령 확인(칸 전체)" : "상자 보관 확인(칸 전체)";
                AIBase.NoteOpened(world, _move.chest); AIInventory.Opened(_move.chest);
                string whole = $"{_move.mode} 칸 전체 교환 관측: {_move.label}"; AIInventory.Completed(world, player, _move, whole, 1); return Finish(SkillStatus.Done, whole);
            }
            if (_move.mode == "hold")
            {
                if (!AIInventory.Read(world, player, _move.to, out var item) || !AIInventory.Identity(item, _move.beforeFrom)) return Finish(SkillStatus.Failed, "들기로 한 물품이 바뀜");
                var em = world.EntityManager;
                if (em.HasComponent<EquippedObjectCD>(player))
                {
                    var held = em.GetComponentData<EquippedObjectCD>(player);
                    if (held.equippedSlotIndex == _move.to && AIInventory.Identity(held.containedObject, item))
                    {
                        string result = $"AI 선택 손 장비 관측: {_move.to} {AIInventory.Text(item)}"; AIInventory.Completed(world, player, _move, result); return Finish(SkillStatus.Done, result);
                    }
                }
                if (now - _sentAt > 3f) ObserveOnce("inventory-hand-pending", "핫바 이동 후 실제 손 장비를 아직 확인하지 못함(재요청 없이 관측하며 AI가 다음 행동 선택)");
                return new BodyInput { slot = _move.to };
            }
            string done = $"양 슬롯 교환 관측: {_move.label}"; AIInventory.Completed(world, player, _move, done); return Finish(SkillStatus.Done, done);
        }
        if (now - _sentAt > 3f) ObserveOnce("inventory-swap-pending", "교환 요청 후 양 슬롯 변화를 아직 확인하지 못함(재요청 없이 AI 재판단)");
        return BodyInput.Idle;
    }
    // 2026-11-08 빈 선택지 B4: the chosen item is in the chosen hotbar slot; hold it and use it like a player
    // (secondary held for eat / cast, one primary press for an instrument note). Success = the item count went down.
    private float _useStart = -1f;
    private int _useBefore = -1;
    private BodyInput UseTick(World world, Entity player, float now)
    {
        if (_useStart < 0f) { _useStart = now; _useBefore = AIInventory.MatchingCount(world, player, _move.beforeFrom); Facts.Add($"use start count {_useBefore}"); }
        bool instrument = AIInventory.Info(_move.beforeFrom)?.objectType == ObjectType.Instrument;
        int count = AIInventory.MatchingCount(world, player, _move.beforeFrom);
        if (!instrument && _useBefore >= 0 && count >= 0 && count < _useBefore)
        {
            string used = $"{_move.beforeFrom.objectID} 사용 확인: 같은 물품 수량 {_useBefore}→{count}";
            ConfirmedWork = _useBefore - count; ResultStage = "사용(소비) 확인"; AIInventory.Completed(world, player, _move, used); return Finish(SkillStatus.Done, used);
        }
        if (instrument && now - _useStart > 0.9f)
        {
            // Review L1: the note itself is not observable here, so nothing is counted as done work and no completion is
            // recorded; the skill only ends after delivering the press, and says the result is unconfirmed.
            string played = $"{_move.beforeFrom.objectID} 연주 입력(주 사용 1회)을 보냄 — 소리·연주 결과는 관측하지 못해 확인된 결과 없음";
            ConfirmedWork = 0; ResultStage = "연주 입력 전송; 결과 미확인"; Facts.Add(played); return Finish(SkillStatus.Done, played);
        }
        if (now - _useStart > 8f) return Finish(SkillStatus.Failed, "사용 입력 8초 동안 수량 감소를 관측하지 못함 — 재사용 대기, 소모되지 않는 물품, 지금 쓸 수 없는 상태일 수 있음");
        if (now - _useStart > 4f) ObserveOnce("inventory-use-pending", "사용 입력 후 수량 감소를 아직 확인하지 못함(입력 유지, AI 재판단 가능)");
        bool press = now - _useStart > 0.4f;
        return new BodyInput { slot = _move.to, secondary = press && !instrument, primary = press && instrument && now - _useStart < 0.6f };
    }

    // 2026-11-08 빈 선택지 B2·B3: one game request over a whole inventory (sort / quick stack) or a preset switch.
    private Dictionary<Entity, ContainedObjectsBuffer[]> _bulkBefore;
    private BodyInput BulkTick(World world, Entity player, float now)
    {
        if (_move.mode == "preset")
        {
            if (!_sent) { string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale); _sent = true; _sentAt = now; ResultStage = "프리셋 전환 입력 중"; }
            int active = AIInventoryBulk.ActivePreset(world, player);
            if (active == _move.preset)
            {
                string done = $"착용 프리셋 {active + 1} 활성 관측 — {AIInventoryBulk.PresetText(world, player, active)}";
                ResultStage = "프리셋 전환 확인"; AIInventory.Completed(world, player, _move, done); return Finish(SkillStatus.Done, done);
            }
            if (now - _sentAt > 8f) return Finish(SkillStatus.Failed, $"프리셋 {_move.preset + 1} 전환 입력 8초 동안 활성 프리셋 변화 없음(현재 {active + 1})");
            if (now - _sentAt > 3f) ObserveOnce("inventory-preset-pending", "프리셋 전환 입력 후 활성 프리셋 변화를 아직 확인하지 못함");
            return new BodyInput { slot = -1, presetAsk = _move.preset + 1 };
        }
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            _bulkBefore = AIInventoryBulk.Snapshot(world, player, _move);
            if (!AIInventoryBulk.Queue(world, player, _move)) return Finish(SkillStatus.Failed, "정리 요청 큐에 넣지 못함");
            _sent = true; _sentAt = now; ResultStage = "정상 정리 요청; 칸 변화 미확인";
            Facts.Add($"{_move.mode} queued once; completion awaits observed slot changes");
        }
        string changed = AIInventoryBulk.Diff(world, player, _move, _bulkBefore, out int moved);
        if (changed != null)
        {
            ConfirmedWork = moved; ResultStage = "칸 변화 확인";
            if (_move.chest != Entity.Null) { AIBase.NoteOpened(world, _move.chest); }
            AIInventory.Completed(world, player, _move, changed, moved); return Finish(SkillStatus.Done, changed);
        }
        if (now - _sentAt > 8f) return Finish(SkillStatus.Failed, "정리 요청 후 8초 동안 정리 결과를 확인하지 못함 — 이미 정렬됐거나 옮길 같은 물품·빈 자리가 없었을 수 있음, 또는 줍기 등 다른 칸 변화가 섞여 정렬만의 결과를 가려내지 못함(재요청 없음)");
        if (now - _sentAt > 3f) ObserveOnce("inventory-bulk-pending", "정리 요청 후 칸 변화를 아직 확인하지 못함(재요청 없이 AI 재판단)");
        return BodyInput.Idle;
    }
    private BodyInput? Navigate(World world, Entity player, Entity target, float reach, float now)
    {
        var em = world.EntityManager;
        if (!em.Exists(target) || !em.HasComponent<LocalTransform>(target)) return Finish(SkillStatus.Failed, "다가갈 대상이 사라짐");
        var position = em.GetComponentData<LocalTransform>(target).Position;
        if (math.distance(AIBody.Pos(world, player).xz, position.xz) <= reach)
        {
            if (_navOn) { _nav.Dispose(world); _navOn = false; }
            return null;
        }
        if (!_navOn) { _nav.Start(world, player, AIBody.TileOf(position), math.max(0.2f, reach - 0.2f), now, target); _navOn = true; }
        var state = _nav.Tick(world, player, now, out float2 move);
        if (state == NavStatus.Failed) return Finish(SkillStatus.Failed, "대상에게 가지 못함: " + _nav.FailReason);
        if (state == NavStatus.Arrived) { _nav.Dispose(world); _navOn = false; }
        return new BodyInput { move = move, slot = -1 };
    }
    private BodyInput InspectTick(World world, Entity player, float now)
    {
        if (!AIInventory.OwnChest(world, _move.chest)) return Finish(SkillStatus.Failed, "살펴볼 상자가 사라지거나 소유가 바뀜");
        var nav = Navigate(world, player, _move.chest, AIBase.ChestReach, now); if (nav.HasValue) return nav.Value;
        var chest = AIBase.NoteOpened(world, _move.chest);
        if (chest == null) return Finish(SkillStatus.Failed, "상자로 열어 볼 수 없음");
        AIInventory.Opened(_move.chest);
        return Finish(SkillStatus.Done, $"상자를 열어 현재 내용 관측: {AIBase.ChestText(chest, AIBody.Pos(world, player))}. 물품 이동은 다음 선택");
    }
    private static int UnitsLike(ContainedObjectsBuffer item, ContainedObjectsBuffer expected)
    {
        if (!AIInventory.Identity(item, expected)) return 0;
        if (AIInventory.Info(expected)?.isStackable != true && item.amount != expected.amount) return 0;
        return AIInventory.Quantity(item);
    }
    private BodyInput TransferTick(World world, Entity player, float now)
    {
        if (_move.mode == "maintenance_put" || _move.mode == "maintenance_take")
        {
            string observation = AIMaintenance.MoveObservationReady(world, player, _move);
            if (observation != null) return Finish(SkillStatus.Failed, observation);
        }
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            var em = world.EntityManager; var inventory = em.GetBuffer<InventoryBuffer>(_move.toEntity, true);
            if (inventory.Length == 0) return Finish(SkillStatus.Failed, "목적 인벤토리 설정 없음");
            int end = _move.toEntity == player ? AIInventory.InventoryEnd(world, player, _move.to) : inventory[0].startIndex + inventory[0].size;
            var change = Create.MoveAmount(_move.fromEntity, _move.from, _move.toEntity, _move.to, end, _move.count, false);
            if (_move.mode == "maintenance_put" || _move.mode == "maintenance_take")
            {
                if (!AICraft.QueueInventoryChange(world, player, change, out var postedTick, out string why)) return Finish(SkillStatus.Failed, why);
                AIMaintenance.MovePosted(world, player, _move, postedTick, change);
            }
            else if (!AICraft.QueueInventoryChange(world, player, change)) return Finish(SkillStatus.Failed, "물품 이동 요청 큐에 넣지 못함");
            _sent = true; _sentAt = now;
            Facts.Add($"MoveAmount queued {_move.count}; no drop/destroy path; awaits actual source and destination change");
            ResultStage = "정상 이동 요청; 양쪽 변화 미확인";
        }
        if (AIInventory.Read(world, _move.fromEntity, _move.from, out var from) && AIInventory.Read(world, _move.toEntity, _move.to, out var to))
        {
            int actual = AIInventoryRules.ObservedTransfer(AIInventory.Stamp(_move.beforeFrom), AIInventory.Stamp(from), UnitsLike(_move.beforeTo, _move.beforeFrom), UnitsLike(to, _move.beforeFrom), _move.count);
            if (actual > 0)
            {
                bool maintenance = _move.mode == "maintenance_put" || _move.mode == "maintenance_take";
                if (maintenance)
                {
                    string pending = AIMaintenance.MoveReceiptReady(world, player, _move);
                    if (pending != null) { ObserveOnce("maintenance-transfer-processing", "양쪽 이동 변화 확인; " + pending); ResultStage = "양쪽 이동 변화 확인; 요청 처리 미확인"; return BodyInput.Idle; }
                    AIMaintenance.MoveObserved(world, player, _move, actual);
                }
                ConfirmedWork = actual; ConfirmedReceived = _move.mode == "take" || _move.mode == "maintenance_take" ? actual : 0;
                ResultStage = _move.mode == "take" || _move.mode == "maintenance_take" ? "가방·파우치 수령 확인" : maintenance ? "자기 정비 대장간 투입 확인" : _move.mode == "move" ? "가방·파우치 안 이동 확인" : "상자 보관 확인";
                if (!maintenance && _move.mode != "move") { AIBase.NoteOpened(world, _move.chest); AIInventory.Opened(_move.chest); }
                string result = $"{_move.mode} 실제 양쪽 슬롯 변화로 {actual}/{_move.count}개 이동 확인: {_move.from}→{_move.to}";
                AIInventory.Completed(world, player, _move, result, actual); return Finish(SkillStatus.Done, result);
            }
        }
        if (now - _sentAt > 3f) ObserveOnce("inventory-transfer-pending", "이동 요청 후 출발 감소와 목적 증가를 같은 수량으로 아직 확인하지 못함(재요청 없이 AI 재판단)");
        return BodyInput.Idle;
    }
    private Dictionary<Entity, ContainedObjectsBuffer> Floor(World world, float3 position)
    {
        var result = new Dictionary<Entity, ContainedObjectsBuffer>(); var em = world.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PickUpItemCD>(), ComponentType.ReadOnly<ContainedObjectsBuffer>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var entities = q.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
            {
                if (math.distance(em.GetComponentData<LocalTransform>(entity).Position.xz, position.xz) > 2.5f || !AIInventory.Read(world, entity, 0, out var item) || item.objectID == ObjectID.None) continue;
                result[entity] = item;
            }
        return result;
    }
    private BodyInput DropTick(World world, Entity player, float now)
    {
        var em = world.EntityManager;
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            if (_move.mode == "give")
            {
                if (now - StartTime > 30f) ObserveOnce("inventory-recipient-approach", "수령자 접근 30초 경과, 아직 내려놓지 않음. 접근 가능 여부를 관측하며 AI 재판단");
                var navigation = Navigate(world, player, _move.recipient, 1.0f, now); if (navigation.HasValue) return navigation.Value;
                var targetPosition = em.GetComponentData<LocalTransform>(_move.recipient).Position;
                if (!AIVision.CanSee(AIBody.Pos(world, player), targetPosition.xz)) return Finish(SkillStatus.Failed, "수령자가 현재 보이지 않아 내려놓지 않음");
                _recipientBefore = AIInventory.MatchingCount(world, _move.recipient, _move.beforeFrom);
                if (_recipientBefore < 0) return Finish(SkillStatus.Failed, "수령자 가방의 전달 전 수량을 관측할 수 없어 내려놓지 않음");
            }
            _dropPosition = AIBody.Pos(world, player);
            _floorBefore = new HashSet<Entity>();
            // Exclude every pre-existing drop identity, including an old sender drop that
            // could move into this area while the queued request is being processed.
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PickUpItemCD>()))
            using (var existing = query.ToEntityArray(Allocator.Temp))
                foreach (var entity in existing) _floorBefore.Add(entity);
            if (_move.mode == "give")
            {
                // Observe inside the server inventory group, before its consumer removes the
                // transient pickup entity. A render-frame Tick can run after that entity is gone.
                _sender = player;
                _sourceObserver = world.GetOrCreateSystemManaged<InventoryDeliverySourceObservationSystem>();
                var simulation = world.GetOrCreateSystemManaged<Unity.NetCode.PredictedSimulationSystemGroup>();
                simulation.AddSystemToUpdateList(_sourceObserver);
                simulation.SortSystems();
                _sourceObserver.Watch(this);
                _deliveryObserver = world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>();
                var group = world.GetOrCreateSystemManaged<InventorySystemGroup>();
                group.AddSystemToUpdateList(_deliveryObserver);
                group.SortSystems();
                _deliveryObserver.Watch(this);
            }
            if (!AICraft.QueueInventoryChange(world, player, Create.DropItem(player, _move.from, _move.count, _dropPosition, player))) return Finish(SkillStatus.Failed, "내려놓기 요청 큐에 넣지 못함");
            _sent = true; _sentAt = now;
            Facts.Add($"DropItem queued at self position, dropper pickup blocked; source {_move.from}, units {_move.count}; recipient inventory never written");
        }
        int removed = -1, floorRemoved = -1;
        int recipientNow = _move.mode == "give" ? AIInventory.MatchingCount(world, _move.recipient, _move.beforeFrom) : -1;
        int gained = _recipientBefore >= 0 && recipientNow >= 0 ? recipientNow - _recipientBefore : -1;
        if (AIInventory.Read(world, player, _move.from, out var from))
        {
            int left = from.objectID == ObjectID.None ? 0 : AIInventory.Identity(from, _move.beforeFrom) ? AIInventory.Quantity(from) : -1;
            if (left >= 0) removed = AIInventory.Quantity(_move.beforeFrom) - left;
        }
        CaptureDelivery(world, _move.mode == "drop");
        if (_ambiguousFloor) return Finish(SkillStatus.Failed, "동시에 같은 바닥 물품이 여러 개 생겨 이번 내려놓기에 귀속하지 못함(재전송 없음)");
        if (_otherRecipient) return Finish(SkillStatus.Failed, "내려놓은 물건을 지정 수령자 이외의 플레이어가 가져감(재전송 없음)");
        if (_floor != Entity.Null && removed == _move.count)
        {
            if (_move.mode == "drop")
            {
                string result = $"출발 슬롯 {_move.from} 감소와 새 바닥 물품 {_floor} {_move.count}개 생성 확인";
                AIInventory.Completed(world, player, _move, result, _move.count); return Finish(SkillStatus.Done, result);
            }
            if (!em.Exists(_floor)) floorRemoved = AIInventory.Quantity(_floorInitial);
            else if (AIInventory.Read(world, _floor, 0, out var remainingFloor))
            {
                if (remainingFloor.objectID == ObjectID.None) floorRemoved = AIInventory.Quantity(_floorInitial);
                else if (AIInventory.Identity(remainingFloor, _floorInitial) && (AIInventory.Info(_floorInitial)?.isStackable == true || remainingFloor.amount == _floorInitial.amount)) floorRemoved = AIInventory.Quantity(_floorInitial) - AIInventory.Quantity(remainingFloor);
            }
            int received = AIInventoryRules.ObservedDelivery(removed, _move.count, _floor != Entity.Null, _pickupReceipt, floorRemoved, gained);
            if (received > 0)
            {
                string result = $"전달 run{Run}, 목표 {_move.goal?.Text ?? "개별 선택"}, 출발 {_sender} 슬롯 {_move.from} 감소 {removed}/{_move.count}, 바닥 {_floor} 지정 pickup {_pickupReceipt}·소비 {floorRemoved}, {_move.recipientName}({_move.recipient}) 전체 가방·착용 {_recipientBefore}→{recipientNow}, {received}/{_move.count}개 수령 확인";
                ConfirmedWork = ConfirmedReceived = received;
                ResultStage = "지정 수령자 실제 수령 확인";
                AIInventory.Completed(world, player, _move, result, received); return Finish(SkillStatus.Done, result);
            }
        }
        string receiver = recipientNow < 0 ? "미관측" : $"전 {_recipientBefore}, 현재 {recipientNow}, 증가 {gained}";
        _deliveryObservation = $"내려놓기 요청 1회 전송, 출발 감소 {(removed < 0 ? "미관측" : removed.ToString())}/{_move.count}, 바닥 {_floor} 감소 {(floorRemoved < 0 ? "미관측" : floorRemoved.ToString())}, 수령자 전체 가방·착용 {receiver}, 지정 대상 관측 {_sawRecipient}, 지정 pickup 요청 {_pickupReceipt}. 수령 귀속 미확인, 재전송 없음";
        ResultStage = _deliveryObservation;
        if (now - _sentAt > 6f) ObserveOnce($"inventory-drop-pending:{removed}:{floorRemoved}:{gained}:{recipientNow}", _deliveryObservation + ". 이미 수령했을 가능성을 확인하고 다음 행동을 판단");
        return BodyInput.Idle;
    }
    // Called both from the ordinary skill tick and from the server inventory consumer boundary.
    // This method records facts only. Completion and model notifications stay in the owning AI scope.
    internal void CaptureDelivery(World world, bool sourcePhase = false)
    {
        if (!_sent || Status != SkillStatus.Running || _floorBefore == null) return;
        var em = world.EntityManager;
        if (_floor == Entity.Null && sourcePhase)
        {
            Entity match = Entity.Null;
            int matches = 0;
            foreach (var floor in Floor(world, _dropPosition))
                if (!_floorBefore.Contains(floor.Key) && AIInventory.Identity(floor.Value, _move.beforeFrom) &&
                    AIInventory.Quantity(floor.Value) == _move.count &&
                    (AIInventory.Info(_move.beforeFrom)?.isStackable == true || floor.Value.amount == _move.beforeFrom.amount))
                {
                    if (_move.mode == "give")
                    {
                        // DropItem marks its dropper before auto-pickup changes targetEntity.
                        // A same-looking object dropped by somebody else is never ours.
                        var source = em.GetComponentData<PickUpItemCD>(floor.Key);
                        if (source.targetEntity != _sender || source.state != PickUpItemState.BlockPickupUntilReEnterStart) continue;
                        if (!AIInventory.Read(world, _sender, _move.from, out var current)) continue;
                        int left = current.objectID == ObjectID.None ? 0 : AIInventory.Identity(current, _move.beforeFrom) ? AIInventory.Quantity(current) : -1;
                        if (left < 0 || AIInventory.Quantity(_move.beforeFrom) - left != _move.count) continue;
                    }
                    match = floor.Key; matches++;
                }
            if (matches > 1) { _ambiguousFloor = true; return; }
            if (matches == 1 && AIInventory.Read(world, match, 0, out var initial))
            { _floor = match; _floorInitial = initial; Facts.Add("new floor entity observed " + match); }
        }
        if (_move.mode != "give" || _floor == Entity.Null || !em.Exists(_floor) || !em.HasComponent<PickUpItemCD>(_floor)) return;
        var pickup = em.GetComponentData<PickUpItemCD>(_floor);
        bool pulling = pickup.state == PickUpItemState.IsBeingPickedUp || pickup.state == PickUpItemState.ForcePickUp || pickup.state == PickUpItemState.HasBeenPickedUp;
        if (pickup.targetEntity == _move.recipient && pulling) _sawRecipient = true;
        if (pickup.targetEntity == _move.recipient && pickup.state == PickUpItemState.HasBeenPickedUp) _pickupReceipt = true;
        if (pickup.targetEntity != Entity.Null && pickup.targetEntity != _move.recipient && pulling) _otherRecipient = true;
    }
    internal bool DeliveryWatchActive(World world) => _sent && Status == SkillStatus.Running && world.EntityManager.Exists(_sender);
    public override void Cleanup(World world)
    {
        _sourceObserver?.Forget(this);
        _sourceObserver = null;
        _deliveryObserver?.Forget(this);
        _deliveryObserver = null;
        _nav.Dispose(world);
    }
    public override string Progress(World world, Entity player) => _sent
        ? (_move.mode == "give" || _move.mode == "drop" ? _deliveryObservation : "정상 인벤토리 요청의 실제 양 슬롯/손 상태를 관측 중")
        : _move?.label ?? "선택 없음";
}

// 2026-11-08 빈 선택지 B2 (일괄 보관·정리, 사용자 결정 "넣음") and B3 (착용 프리셋 전환·비활성 프리셋 보기).
// Each is one normal game request (Create.Sort / QuickStack / QuickStackToNearbyChests) or the preset input the game
// reads from ClientInput.equipmentPresetIndex (SelectedEquipmentChangeSystem). The AI chooses them in the bag tree.
public static class AIInventoryBulk
{
    internal static bool Owns(string mode) => mode == "quickstack_nearby" || mode == "quickstack_chest" || mode == "sort_bag" || mode == "sort_chest" || mode == "preset";
    // InventoryUtility.GetNearbyChestsForAutoStackingByDistance: radius 10, at most 20 chests (physics overlap; here the
    // chests in view within that radius approximate it for the prediction and the observed result).
    internal const float NearbyRadius = 10f;
    internal static List<Entity> NearbyChests(World world, Entity player)
    {
        var l = new List<Entity>();
        foreach (var c in AIBase.ChestsInView(world, player)) if (c.dist <= NearbyRadius && l.Count < 20) l.Add(c.entity);
        return l;
    }
    private static bool Holds(World world, Entity chest, ContainedObjectsBuffer item, bool stackableOnly)
    {
        var em = world.EntityManager; if (!em.HasBuffer<InventoryBuffer>(chest)) return false;
        if (stackableOnly && AIInventory.Info(item)?.isStackable != true) return false;
        var inventories = em.GetBuffer<InventoryBuffer>(chest, true); if (inventories.Length == 0) return false;
        for (int i = inventories[0].startIndex; i < inventories[0].startIndex + inventories[0].size; i++)
            if (AIInventory.Read(world, chest, i, out var x) && x.objectID == item.objectID && x.variation == item.variation) return true;
        return false;
    }
    // Carried items (hotbar, bag, pouches) of a kind the target chests already hold: what the game quick stack would move.
    internal static List<string> Matches(World world, Entity player, List<Entity> chests, bool stackableOnly)
    {
        var names = new List<string>(); var seen = new HashSet<string>();
        foreach (int i in AIInventory.CarriedSlots(world, player))
        {
            if (!AIInventory.Read(world, player, i, out var item) || item.objectID == ObjectID.None) continue;
            foreach (var chest in chests) if (Holds(world, chest, item, stackableOnly)) { if (seen.Add(item.objectID + "#" + item.variation)) names.Add($"{item.objectID}(칸 {i})"); break; }
        }
        return names;
    }
    private static int Items(World world, Entity entity, int start, int end)
    {
        int n = 0; for (int i = start; i < end; i++) if (AIInventory.Read(world, entity, i, out var x) && x.objectID != ObjectID.None) n++;
        return n;
    }
    internal static int ActivePreset(World world, Entity player)
    {
        var em = world.EntityManager;
        return em.Exists(player) && em.HasComponent<ActiveEquipmentPresetCD>(player) ? em.GetComponentData<ActiveEquipmentPresetCD>(player).Value : 0;
    }
    internal static int PresetCount(World world, Entity player)
    {
        var em = world.EntityManager;
        return em.Exists(player) && em.HasBuffer<EquipmentPresetsBuffer>(player) ? em.GetBuffer<EquipmentPresetsBuffer>(player, true).Length : 0;
    }
    // The worn items of one preset (also an inactive one, which the active gear list does not show).
    internal static string PresetText(World world, Entity player, int preset)
    {
        var em = world.EntityManager; if (!em.HasBuffer<EquipmentPresetsBuffer>(player)) return "프리셋 정보 없음";
        var presets = em.GetBuffer<EquipmentPresetsBuffer>(player, true); if (preset < 0 || preset >= presets.Length) return "프리셋 범위 밖";
        var e = presets[preset].equipment; var parts = new List<string>();
        void Add(string name, int slot) { if (AIInventory.Read(world, player, slot, out var item) && item.objectID != ObjectID.None) parts.Add($"{name}={AIInventory.Text(item)}"); }
        Add("head", e.helmSlotIndex); Add("chest", e.breastSlotIndex); Add("legs", e.pantsSlotIndex); Add("necklace", e.necklaceSlotIndex);
        Add("ring1", e.ring1SlotIndex); Add("ring2", e.ring2SlotIndex); Add("offhand", e.offHandIndex);
        return parts.Count == 0 ? "착용 장비 없음" : string.Join(", ", parts);
    }
    // kind: "bag" (sort / quick stack to nearby chests), "chest" (the opened chest), "presets".
    internal static List<AIInventoryMove> Moves(World world, Entity player, string kind, Entity chest)
    {
        var result = new List<AIInventoryMove>();
        void Add(AIInventoryMove m) { m.fromEntity = player; if (m.toEntity == Entity.Null) m.toEntity = player; m.key = $"inventory:{m.mode}:{m.chest.Index}.{m.chest.Version}:{m.preset}"; if (AIInventory.Stale(world, player, m) == null) result.Add(m); }
        if (kind == "bag")
        {
            Add(new AIInventoryMove { mode = "sort_bag", label = "가방 정렬 — 게임 정렬: 같은 물품을 먼저 합친 뒤 종류순으로 정렬. 정렬 순서는 핫바 0~9 칸을 건너뛰지만, 같은 물품 합치기로 핫바 스택이 가방 스택에 합쳐질 수 있음" });
            var nearby = NearbyChests(world, player); var names = Matches(world, player, nearby, false);
            Add(new AIInventoryMove { mode = "quickstack_nearby", label = $"근처 상자 {nearby.Count}개(10칸 안)에 같은 종류 빠른 쌓기 — 그 상자들에 이미 든 종류인 물품: {string.Join(", ", names)}. 핫바·파우치 칸도 포함" });
        }
        else if (kind == "chest")
        {
            var names = Matches(world, player, new List<Entity> { chest }, true);
            Add(new AIInventoryMove { mode = "quickstack_chest", toEntity = chest, chest = chest, label = $"열린 상자에 같은 종류 빠른 쌓기 — 상자에 이미 든, 쌓을 수 있는 물품: {string.Join(", ", names)}(가방·핫바·파우치에서)" });
            Add(new AIInventoryMove { mode = "sort_chest", toEntity = chest, chest = chest, label = "열린 상자 정렬 — 같은 물품을 합친 뒤 종류순으로 정렬" });
        }
        else if (kind == "presets")
        {
            int active = ActivePreset(world, player);
            for (int k = 0; k < PresetCount(world, player); k++) if (k != active)
                Add(new AIInventoryMove { mode = "preset", preset = k, label = $"착용 프리셋 {k + 1}로 전환(현재 {active + 1}) — 프리셋 {k + 1} 착용: {PresetText(world, player, k)}" });
        }
        return result;
    }
    internal static string Stale(World world, Entity player, AIInventoryMove m)
    {
        var em = world.EntityManager;
        if (m.fromEntity != player) return "정리 요청의 주체가 이 캐릭터가 아님";
        switch (m.mode)
        {
            case "sort_bag":
            {
                int start = AIInventory.MainStart(world, player), end = AIInventory.MainEnd(world, player);
                return Items(world, player, math.min(end, start + 10), end) >= 2 ? null : "정렬할 가방 물품(핫바 밖)이 2개 미만";
            }
            case "quickstack_nearby":
            {
                var nearby = NearbyChests(world, player);
                if (nearby.Count == 0) return "10칸 안에 보이는 상자가 없음";
                return Matches(world, player, nearby, false).Count > 0 ? null : "근처 상자에 이미 든 종류의 물품이 가방·파우치에 없음";
            }
            case "quickstack_chest": case "sort_chest":
            {
                if (!AIInventory.OwnChest(world, m.chest)) return "상자가 사라졌거나 형식이 바뀜";
                if (math.distance(AIBody.Pos(world, player).xz, em.GetComponentData<LocalTransform>(m.chest).Position.xz) > AIBase.ChestReach) return "상자를 조작할 거리 밖";
                if (!AIInventory.MenuChestOpened(world, player, m.chest)) return "먼저 상자를 열어 살펴봐야 함";
                var inventories = em.GetBuffer<InventoryBuffer>(m.chest, true); if (inventories.Length == 0) return "상자 인벤토리 설정 없음";
                if (m.mode == "sort_chest") return Items(world, m.chest, inventories[0].startIndex, inventories[0].startIndex + inventories[0].size) >= 2 ? null : "정렬할 상자 물품이 2개 미만";
                if (inventories[0].canOnlyContainOneItemPerSlot) return "한 칸에 하나만 드는 상자는 빠른 쌓기를 받지 않음";
                return Matches(world, player, new List<Entity> { m.chest }, true).Count > 0 ? null : "상자에 이미 든, 쌓을 수 있는 물품이 가방·파우치에 없음";
            }
            case "preset":
            {
                int count = PresetCount(world, player);
                if (count == 0 || !em.HasComponent<ActiveEquipmentPresetCD>(player)) return "착용 프리셋을 읽을 수 없음";
                if (m.preset < 0 || m.preset >= count) return "프리셋 번호 범위 밖";
                return m.preset == ActivePreset(world, player) ? "이미 활성인 프리셋" : null;
            }
        }
        return "아직 연결되지 않은 정리 동작";
    }
    internal static bool Queue(World world, Entity player, AIInventoryMove m)
    {
        switch (m.mode)
        {
            case "sort_bag": return AICraft.QueueInventoryChange(world, player, Create.Sort(player, true));
            case "sort_chest": return AICraft.QueueInventoryChange(world, player, Create.Sort(m.chest, false));
            case "quickstack_chest": return AICraft.QueueInventoryChange(world, player, Create.QuickStack(player, m.chest));
            default: return AICraft.QueueInventoryChange(world, player, Create.QuickStackToNearbyChests(player));
        }
    }
    private static List<Entity> Watched(World world, Entity player, AIInventoryMove m)
    {
        var l = new List<Entity> { player };
        if (m.mode == "quickstack_nearby") l.AddRange(NearbyChests(world, player));
        else if (m.chest != Entity.Null) l.Add(m.chest);
        return l;
    }
    internal static Dictionary<Entity, ContainedObjectsBuffer[]> Snapshot(World world, Entity player, AIInventoryMove m)
    {
        var result = new Dictionary<Entity, ContainedObjectsBuffer[]>(); var em = world.EntityManager;
        foreach (var e in Watched(world, player, m))
            if (em.Exists(e) && em.HasBuffer<ContainedObjectsBuffer>(e)) { var b = em.GetBuffer<ContainedObjectsBuffer>(e, true); var copy = new ContainedObjectsBuffer[b.Length]; for (int i = 0; i < b.Length; i++) copy[i] = b[i]; result[e] = copy; }
        return result;
    }
    private static int CarriedUnits(World world, Entity player, Func<int, ContainedObjectsBuffer> read)
    {
        int n = 0; foreach (int i in AIInventory.CarriedSlots(world, player)) n += AIInventory.Quantity(read(i));
        return n;
    }
    // The observed result: the slots that changed, and for a quick stack the carried units that left the player.
    // Review L2: a sort is only a reorder of the sorted inventory (its item totals unchanged); a quick stack is the carried
    // units that left the player. Any other slot change (an auto pickup, another player's chest use) is not the result.
    internal static string Diff(World world, Entity player, AIInventoryMove m, Dictionary<Entity, ContainedObjectsBuffer[]> before, out int moved)
    {
        moved = 0; if (before == null) return null; var em = world.EntityManager;
        Func<Entity, int, ContainedObjectsBuffer> nowAt = (e, i) => em.Exists(e) && em.HasBuffer<ContainedObjectsBuffer>(e) && i < em.GetBuffer<ContainedObjectsBuffer>(e, true).Length ? em.GetBuffer<ContainedObjectsBuffer>(e, true)[i] : default;
        if (m.mode == "sort_bag" || m.mode == "sort_chest")
        {
            Entity target = m.mode == "sort_bag" ? player : m.chest;
            if (!before.TryGetValue(target, out var was)) return null;
            var slots = new List<int>();
            if (m.mode == "sort_bag") slots.AddRange(AIInventory.CarriedSlots(world, player)); else for (int i = 0; i < was.Length; i++) slots.Add(i);
            int changed = 0; var totals = new Dictionary<(ObjectID, int), long>();
            foreach (int i in slots)
            {
                var a = i < was.Length ? was[i] : default; var b = nowAt(target, i);
                if (!b.EqualsExact(a)) changed++;
                if (a.objectID != ObjectID.None) { totals.TryGetValue((a.objectID, a.variation), out long n); totals[(a.objectID, a.variation)] = n + a.amount; }
                if (b.objectID != ObjectID.None) { totals.TryGetValue((b.objectID, b.variation), out long n); totals[(b.objectID, b.variation)] = n - b.amount; }
            }
            if (changed == 0) return null;
            foreach (var t in totals.Values) if (t != 0) return null;   // something else changed the contents: not the sort's result; keep observing
            return $"{(m.mode == "sort_bag" ? "가방 정렬" : "상자 정렬")} 실제 칸 순서 변화 {changed}칸 관측(물품 종류별 총량 그대로)";
        }
        if (before.TryGetValue(player, out var mine))
        {
            int was = CarriedUnits(world, player, i => i < mine.Length ? mine[i] : default);
            int left = CarriedUnits(world, player, i => AIInventory.Read(world, player, i, out var x) ? x : default);
            moved = math.max(0, was - left);
        }
        if (moved == 0) return null;
        long gained = 0;
        foreach (var kv in before)
        {
            if (kv.Key == player) continue;
            for (int i = 0; i < kv.Value.Length; i++) gained += AIInventory.Quantity(nowAt(kv.Key, i)) - AIInventory.Quantity(kv.Value[i]);
        }
        string what = m.mode == "quickstack_chest" ? "열린 상자 빠른 쌓기" : "근처 상자 빠른 쌓기";
        return $"{what}: 가방·파우치에서 빠진 수량 {moved}개, 지켜본 상자 쪽 증가 {math.max(0L, gained)}개"
            + (m.mode == "quickstack_nearby" ? "(게임은 물품을 떨어뜨려 상자로 끌어당김; 증가가 적으면 아직 끌려가는 중이거나 지켜보지 않은 상자로 갔을 수 있음)" : "");
    }
}

// The game queues pickup transfers in PickUpItemSystem, then consumes them inside this group.
// Register only in the selected sender's server world. No inventory or pickup state is written.
[DisableAutoCreation]
[UpdateInGroup(typeof(InventorySystemGroup))]
[UpdateBefore(typeof(InventoryUpdateSystem))]
public sealed partial class InventoryDeliveryObservationSystem : SystemBase
{
    private readonly List<InventorySkill> _watching = new List<InventorySkill>();
    internal void Watch(InventorySkill skill) { if (!_watching.Contains(skill)) _watching.Add(skill); }
    internal void Forget(InventorySkill skill) { _watching.Remove(skill); }
    protected override void OnUpdate()
    {
        if (_watching.Count == 0) return;
        EntityManager.CompleteAllTrackedJobs();
        for (int i = _watching.Count - 1; i >= 0; i--)
        {
            var skill = _watching[i];
            if (!skill.DeliveryWatchActive(World)) { _watching.RemoveAt(i); continue; }
            skill.CaptureDelivery(World);
        }
    }
}


// DropItem's temporary block target identifies the sender. Observe it before auto-pickup
// replaces that target with the receiver, then follow that exact entity in the consumer observer.
[DisableAutoCreation]
[UpdateInGroup(typeof(Unity.NetCode.PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(PickUpItemSystem))]
public sealed partial class InventoryDeliverySourceObservationSystem : SystemBase
{
    private readonly List<InventorySkill> _watching = new List<InventorySkill>();
    internal void Watch(InventorySkill skill) { if (!_watching.Contains(skill)) _watching.Add(skill); }
    internal void Forget(InventorySkill skill) { _watching.Remove(skill); }
    protected override void OnUpdate()
    {
        if (_watching.Count == 0) return;
        EntityManager.CompleteAllTrackedJobs();
        for (int i = _watching.Count - 1; i >= 0; i--)
        {
            var skill = _watching[i];
            if (!skill.DeliveryWatchActive(World)) { _watching.RemoveAt(i); continue; }
            skill.CaptureDelivery(World, true);
        }
    }
}
