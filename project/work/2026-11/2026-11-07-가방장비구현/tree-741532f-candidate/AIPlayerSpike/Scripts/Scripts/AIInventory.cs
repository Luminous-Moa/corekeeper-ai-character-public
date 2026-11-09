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
        if (g.kind == GoalKind.Store && g.count <= 0 && StoreRemaining(world, player, g) == 0) { note = $"보관할 잔량 없음(실제 이동 {S.moved}, 남길 수량 {math.max(0, g.keep)})"; return true; }
        return S.done;
    }
    public static string StallNote(World world, Entity player) => Gate(world, player) ?? "요청과 일치하는 현재 가방 물품·유효 목적 슬롯이 없음";

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
        return l;
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
            default: return (s ?? "").Trim().ToLowerInvariant();
        }
    }
    private static bool WantsSlot(string where, string actual) => string.IsNullOrEmpty(where) || where == "null" || Canonical(where) == actual;
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
                var info = Info(a); if (info == null || PlayerController.ItemIsBroken(a.objectData)) continue;
                if ((!own || g.kind == GoalKind.Equip)) foreach (var target in gear)
                {
                    if (target.type != info.objectType || (own && !WantsSlot(g.where, target.name))) continue;
                    var m = SwapMove(world, player, "equip", i, target.slot, target.name, own ? g : null);
                    if (m == null || m.key == currentKey || Stale(world, player, m) != null) continue;
                    result.Add(Candidate(m, own));
                }
                if ((!own || g.kind == GoalKind.Hold) && (info.objectType == ObjectType.MiningPick || info.objectType == ObjectType.MeleeWeapon))
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
        if (own && (g.kind == GoalKind.Store || g.kind == GoalKind.Take)) result.AddRange(ChestCandidates(world, player, observation, currentKey, g));
        if (own && (g.kind == GoalKind.Drop || g.kind == GoalKind.Give)) result.AddRange(DropCandidates(world, player, observation, currentKey, g));
        return result;
    }
    private static bool IsChosen(World world, Entity player, AIInventoryMove m)
    {
        if (m.mode != "hold" || m.from != m.to) return false;
        var info = Info(m.beforeFrom); var h = info?.objectType == ObjectType.MiningPick ? S.mining : S.weapon;
        return h != null && h.slot == m.from && Identity(h.item, m.beforeFrom);
    }
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
        var em = world.EntityManager; if (!em.Exists(entity) || !em.HasBuffer<ContainedObjectsBuffer>(entity)) return 0;
        int n = 0; var b = em.GetBuffer<ContainedObjectsBuffer>(entity, true);
        foreach (var item in b) if (Identity(item, expected) && (Info(expected)?.isStackable == true || item.amount == expected.amount)) n += Quantity(item);
        return n;
    }
    private static bool ChestWhere(string where, AIBase.ChestMem chest)
    {
        if (string.IsNullOrEmpty(where) || where == "null" || where == "base") return AIBase.BaseDistTile(chest.tile) <= AIBase.BaseRadius;
        return where == $"chest:{chest.tile.x},{chest.tile.y}";
    }
    internal static bool OwnChest(World world, Entity chest)
    {
        var em = world.EntityManager;
        if (!em.Exists(chest) || !em.HasComponent<InventoryAutoTransferEnabledCD>(chest) || !em.HasComponent<ObjectDataCD>(chest) || !em.HasComponent<LocalTransform>(chest) || !em.HasBuffer<ContainedObjectsBuffer>(chest) || !em.HasBuffer<InventoryBuffer>(chest)) return false;
        return AIBase.Own(new AIBase.ChestView { entity = chest, id = em.GetComponentData<ObjectDataCD>(chest).objectID, pos = em.GetComponentData<LocalTransform>(chest).Position }) != null;
    }
    internal static void Opened(Entity chest) { S.openChest = chest; }
    private static int Destination(World world, Entity entity, ContainedObjectsBuffer item, bool mainOnly, out int room)
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
            return path == null ? null : AIApproach.WalkCandidate(stand, $"자기 상자 {memory.id}", false,
                $"기억한 자기 상자 chest:{memory.tile.x},{memory.tile.y}로 가서 확인. 현재 내용은 모름. {AIBase.ChestText(memory, observation.pos)}. 도착 후 열어보기는 별도 선택.");
        }
        if (MenuChestOpened(world, player, view.entity)) return null;
        return Candidate(new AIInventoryMove { mode = "inspect", key = "inventory:inspect:" + view.entity.Index,
            label = $"자기 상자 chest:{memory.tile.x},{memory.tile.y}에 접근해 열어 살펴보기. 내용을 본 뒤 물품을 고름",
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
            if (memory == null || !ChestWhere(g.where, memory)) return null;
        }
        if (move.mode == "give" && Recipient(world, player, g.where)?.entity != move.recipient) return null;
        return g;
    }
    internal static int MenuRequestedCount(AIGoal goal) => goal != null && goal.count > 0 ? math.max(0, goal.count - S.moved) : 0;
    internal static int MenuMaximum(World world, Entity player, AIInventoryMove move)
    {
        int maximum = Quantity(move.beforeFrom);
        if (move.mode == "equip" || move.mode == "unequip" || move.mode == "hold") return 1;
        if (move.mode == "store" || move.mode == "take")
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
    internal static List<AIInventoryMove> MenuMoves(World world, Entity player, Entity source, int sourceSlot, string mode, Entity chest, Entity recipient, string recipientName, int count)
    {
        var result = new List<AIInventoryMove>();
        if (!Read(world, source, sourceSlot, out var item) || item.objectID == ObjectID.None || Info(item) == null || count <= 0) return result;
        void Add(AIInventoryMove move)
        {
            if (move == null) return;
            move.goal = MenuGoal(world, player, move);
            if (count > MenuMaximum(world, player, move)) return;
            move.count = count; move.key = MenuKey(move);
            if (Stale(world, player, move) == null) result.Add(move);
        }
        if (mode == "equip" && source == player)
        {
            foreach (var slot in GearSlots(world, player)) if (slot.type == Info(item).objectType)
                Add(SwapMove(world, player, mode, sourceSlot, slot.slot, slot.name, null));
        }
        else if (mode == "unequip" && source == player)
        {
            for (int i = MainStart(world, player); i < MainEnd(world, player); i++) if (Read(world, player, i, out var empty) && empty.objectID == ObjectID.None)
                Add(SwapMove(world, player, mode, sourceSlot, i, "bag:" + i, null));
        }
        else if (mode == "hold" && source == player)
        {
            for (int i = MainStart(world, player); i < math.min(10, MainEnd(world, player)); i++)
            {
                if (i != sourceSlot) Add(SwapMove(world, player, mode, sourceSlot, i, "hotbar:" + i, null));
                else Add(new AIInventoryMove { mode = mode, label = $"손에 들기 {Text(item)} 핫바 {i}", fromEntity = player, toEntity = player,
                    from = i, to = i, beforeFrom = item, beforeTo = item, destination = "hotbar:" + i });
            }
        }
        else if (mode == "store" || mode == "take")
        {
            if (!MenuChestOpened(world, player, chest)) return result;
            Entity target = mode == "store" ? chest : player;
            var em = world.EntityManager; if (!em.HasBuffer<InventoryBuffer>(target)) return result;
            var inv = em.GetBuffer<InventoryBuffer>(target, true);
            int start = target == player ? MainStart(world, player) : inv.Length > 0 ? inv[0].startIndex : 0;
            int end = target == player ? MainEnd(world, player) : inv.Length > 0 ? inv[0].startIndex + inv[0].size : 0;
            for (int i = start; i < end; i++) if (Read(world, target, i, out var beforeTo))
                Add(new AIInventoryMove { mode = mode, label = $"{(mode == "store" ? "보관" : "꺼내기")} {Text(item)} {count}개, 출발 슬롯 {sourceSlot} → 목적 슬롯 {i}",
                    fromEntity = source, toEntity = target, from = sourceSlot, to = i, beforeFrom = item, beforeTo = beforeTo, chest = chest, count = count });
        }
        else if ((mode == "drop" || mode == "give") && source == player)
            Add(new AIInventoryMove { mode = mode, label = $"{Text(item)} {count}개 {(mode == "drop" ? "현재 위치에 내려놓기" : recipientName + "에게 접근해 자연 드롭·수령 확인")}",
                fromEntity = player, toEntity = player, from = sourceSlot, to = sourceSlot, beforeFrom = item, beforeTo = item, count = count, recipient = recipient, recipientName = recipientName ?? "" });
        return result;
    }
    private static List<AICandidate> ChestCandidates(World world, Entity player, AIObservation observation, string key, AIGoal g)
    {
        var result = new List<AICandidate>(); if (ReferenceEquals(S.goal, g) && S.done) return result;
        var visible = AIBase.ChestsInView(world, player); var memories = AIBase.OwnChests();
        if (S.openChest != Entity.Null && (!world.EntityManager.Exists(S.openChest) || math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<LocalTransform>(S.openChest).Position.xz) > AIBase.ChestReach)) S.openChest = Entity.Null;
        foreach (var memory in memories)
        {
            if (!ChestWhere(g.where, memory)) continue;
            var chest = visible.Find(c => math.all(AIBody.TileOf(c.pos) == memory.tile) && c.id == memory.id && AIBase.Own(c) != null);
            if (chest == null)
            {
                var stands = AICraft.StandTilesFor(world, player, new AIMemory.Station { id = memory.id, tile = memory.tile });
                var route = AIApproach.FirstCached(world, player, stands, out int2 stand, out _);
                if (route == null) continue;
                var walk = AIApproach.WalkCandidate(stand, $"자기 상자 {memory.id}", true, $"기억한 자기 상자 chest:{memory.tile.x},{memory.tile.y}로 가서 확인 — 지금 내용은 모름. {AIBase.ChestText(memory, observation.pos)}. 옆에 도착한 뒤 열기와 물품 이동은 다음 선택.");
                if (walk.key != key) result.Add(walk);
                continue;
            }
            if (S.openChest != chest.entity || chest.dist > AIBase.ChestReach)
            {
                var inspect = new AIInventoryMove { mode = "inspect", key = "inventory:inspect:" + chest.entity.Index, label = $"자기 상자 chest:{memory.tile.x},{memory.tile.y}에 다가가 열어 살펴보기(현재 내용은 다음 판단에 관측)", chest = chest.entity, fromEntity = player, toEntity = chest.entity, goal = g };
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
                int remaining = g.count > 0 ? g.count - S.moved : 0; if (g.count > 0 && remaining <= 0) continue;
                int total = storing ? Available(world, player, item.objectID) : Quantity(item);
                int count = AIInventoryRules.MoveCount(total, Quantity(item), remaining, storing ? g.keep : 0);
                int dest = Destination(world, target, item, !storing, out int room); count = math.min(count, room);
                if (dest < 0 || count <= 0 || !Read(world, target, dest, out var beforeTo)) continue;
                var move = new AIInventoryMove { mode = storing ? "store" : "take", key = $"inventory:{(storing ? "store" : "take")}:{chest.entity.Index}:{i}:{dest}:{count}", label = $"{(storing ? "보관" : "꺼내기")} {Text(item)} {count}개, 출발 슬롯 {i} → 목적 슬롯 {dest}, 자기 상자 chest:{memory.tile.x},{memory.tile.y}{(storing && g.keep >= 0 ? $", 가방에 {g.keep}개 남김" : "")}", fromEntity = source, toEntity = target, from = i, to = dest, beforeFrom = item, beforeTo = beforeTo, count = count, goal = g, chest = chest.entity };
                if (move.key != key && Stale(world, player, move) == null) result.Add(Candidate(move, true));
            }
        }
        return result;
    }
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
            if (!string.IsNullOrEmpty(where) && where != "base" && !AIBase.OwnChests().Exists(c => where == $"chest:{c.tile.x},{c.tile.y}")) return "알고 있는 자기 상자 식별자가 아님(chest:x,y)";
            if (kind == GoalKind.Take && !AIBase.OwnChests().Exists(c => ChestWhere(where, c))) return "지정 범위에 알고 있는 자기 상자가 없음";
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
        string gate = Gate(world, player); if (gate != null) return gate;
        if (m.mode == "inspect") return OwnChest(world, m.chest) ? null : "자기 상자가 사라졌거나 소유/형식이 바뀜";
        if (m.mode == "store" || m.mode == "take")
        {
            if (!OwnChest(world, m.chest)) return "자기 상자가 사라졌거나 소유/형식이 바뀜";
            if (math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<LocalTransform>(m.chest).Position.xz) > AIBase.ChestReach) return "상자를 조작할 거리 밖";
            if (S.openChest != m.chest) return "먼저 자기 상자를 열어 살펴봐야 함";
            if (m.mode == "store" && (m.fromEntity != player || m.toEntity != m.chest || !IsMain(world, player, m.from)) || m.mode == "take" && (m.fromEntity != m.chest || m.toEntity != player || !IsMain(world, player, m.to))) return "허용된 자기 가방/상자 이동과 선택이 다름";
            if (!Read(world, m.fromEntity, m.from, out var from) || !from.EqualsExact(m.beforeFrom)) return "상자 이동의 출발 물품/상태가 바뀜";
            if (!CanRemove(world, m.fromEntity, m.from)) return "출발 인벤토리의 물품 꺼내기가 제한됨";
            if (!Read(world, m.toEntity, m.to, out var to) || !to.EqualsExact(m.beforeTo)) return "상자 이동의 목적 슬롯/상태가 바뀜";
            if (m.count <= 0 || Quantity(from) < m.count || (to.objectID != ObjectID.None && (Info(from)?.isStackable != true || !Identity(from, to)))) return "선택한 수량/중첩 조건이 바뀜";
            string allows = SlotAllows(world, m.toEntity, m.to, from); if (allows != null) return allows;
            if (m.goal != null && m.mode == "store" && Available(world, player, from.objectID) - m.count < math.max(0, m.goal.keep)) return "남길 수량보다 가방 물품이 줄어듦";
            return null;
        }
        if (m.fromEntity != player || m.toEntity != player) return "아직 연결되지 않은 외부 인벤토리 동작";
        if (!Read(world, m.fromEntity, m.from, out var a) || !a.EqualsExact(m.beforeFrom)) return "출발 슬롯의 물품/상태가 바뀜";
        if (!Read(world, m.toEntity, m.to, out var b) || !b.EqualsExact(m.beforeTo)) return "목적 슬롯의 물품/상태가 바뀜";
        if (a.objectID == ObjectID.None) return "출발 슬롯이 비어 있음";
        if (m.mode == "drop" || m.mode == "give")
        {
            if (!IsMain(world, player, m.from) || m.count <= 0 || Quantity(a) < m.count) return "내려놓을 가방 물품/수량이 바뀜";
            if (m.mode == "give")
            {
                var recipient = Recipient(world, player, m.recipientName);
                if (recipient == null || recipient.entity != m.recipient) return "선택한 수령자가 현재 보이지 않거나 달라짐";
            }
            return null;
        }
        var slots = GearSlots(world, player);
        if (m.mode == "equip")
        {
            var info = Info(a);
            if (!IsMain(world, player, m.from) || info == null || !slots.Exists(x => x.slot == m.to && x.type == info.objectType && x.name == m.destination)) return "현재 장비 타입/활성 슬롯과 선택이 다름";
            if (PlayerController.ItemIsBroken(a.objectData)) return "고장 난 장비";
        }
        else if (m.mode == "unequip")
        {
            if (!slots.Exists(x => x.slot == m.from) || !IsMain(world, player, m.to) || b.objectID != ObjectID.None) return "해제할 장비/빈 가방 슬롯이 바뀜";
        }
        else if (m.mode == "hold")
        {
            var info = Info(a);
            if (!IsMain(world, player, m.from) || m.to < 0 || m.to >= math.min(10, MainEnd(world, player)) || info == null || (info.objectType != ObjectType.MiningPick && info.objectType != ObjectType.MeleeWeapon)) return "현재 손에 들 수 있는 도구/근접무기가 아님";
            if (PlayerController.ItemIsBroken(a.objectData)) return "선택 장비가 고장 남";
        }
        else return "아직 연결되지 않은 가방 동작";
        if (m.from != m.to)
        {
            string valid = SlotAllows(world, player, m.to, a); if (valid != null) return valid;
            valid = SlotAllows(world, player, m.from, b); if (valid != null) return valid;
            valid = CapacityAfterSwap(world, player, m); if (valid != null) return valid;
        }
        return null;
    }
    private static bool CanRemove(World world, Entity entity, int slot)
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
    private static Entity Prefab(World world, ContainedObjectsBuffer value)
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
        var em = world.EntityManager; var inventories = em.GetBuffer<InventoryBuffer>(player, true);
        for (int i = 0; i < inventories.Length; i++)
        {
            var inventory = inventories[i]; int provider = inventory.extraInventorySizeSlot;
            if (provider < 0 || (provider != move.from && provider != move.to)) continue;
            var extra = Extra(world, provider == move.from ? move.beforeTo : move.beforeFrom);
            int end = inventory.startIndex + inventory.sizeX * inventory.sizeY + extra.size;
            bool newTags = inventory.extraInventoryCategoryTagsMask != extra.categoryTagsMask;
            for (int s = inventory.startIndex; s < math.min(inventory.startIndex + inventory.maxSize, em.GetBuffer<ContainedObjectsBuffer>(player, true).Length); s++)
            {
                if (!Read(world, player, s, out var item)) continue;
                if (s == move.from) item = move.beforeTo; else if (s == move.to) item = move.beforeFrom;
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
        if (move.mode == "hold" && Read(world, player, move.to, out var item))
        {
            var h = new Hand { slot = move.to, item = item }; var info = Info(item);
            if (info?.objectType == ObjectType.MiningPick) S.mining = h; else if (info?.objectType == ObjectType.MeleeWeapon) S.weapon = h;
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
        var info = Info(item); if (info == null || info.objectType != (forMining ? ObjectType.MiningPick : ObjectType.MeleeWeapon)) { why = "AI가 고른 손 장비가 현재 작업에 맞지 않음"; return true; }
        slot = h.slot; return true;
    }
    public static string PromptLine(World world, Entity player)
    {
        var parts = new List<string>(); foreach (var s in GearSlots(world, player)) if (Read(world, player, s.slot, out var item) && item.objectID != ObjectID.None) parts.Add($"{s.name}[{s.slot}]={Text(item)}");
        return "착용: " + (parts.Count > 0 ? string.Join(", ", parts) : "없음") + ". " + (Gate(world, player) ?? "가방 조작 가능") + (ReferenceEquals(S.goal, AIGoals.Goal) && !string.IsNullOrEmpty(S.result) ? ". 최근 확인: " + S.result : "");
    }
    private static string Esc(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    public static string ReportJson(World world, Entity player)
    {
        var slots = new List<string>();
        for (int i = MainStart(world, player); i < MainEnd(world, player); i++) if (Read(world, player, i, out var a) && a.objectID != ObjectID.None) slots.Add($"{{\"slot\":{i},\"item\":\"{a.objectID}\",\"variation\":{a.variation},\"state\":{a.amount},\"count\":{Quantity(a)}}}");
        var chests = AIBase.OwnChests().ConvertAll(c => $"\"chest:{c.tile.x},{c.tile.y}\"");
        return "{\"bag_slots\":[" + string.Join(",", slots) + "],\"equipment\":\"" + Esc(PromptLine(world, player)) + "\",\"own_chests\":[" + string.Join(",", chests) + "],\"operations\":[\"equip\",\"unequip\",\"hold\",\"store\",\"take\",\"drop\",\"give\"]}";
    }
}

public sealed class InventorySkill : AISkill
{
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
    private int _recipientBefore;
    private float3 _dropPosition;
    public InventorySkill(AIInventoryMove move) { _move = move; _exchange = move == null ? null : new InventorySwapProgress(AIInventory.Stamp(move.beforeFrom), AIInventory.Stamp(move.beforeTo)); Name = "가방 조작"; Key = move?.key ?? "inventory:missing"; }
    public override bool Start(World world, Entity player, float now)
    {
        string stale = AIInventory.Stale(world, player, _move);
        if (stale != null) { Status = SkillStatus.Failed; Reason = stale; return false; }
        if (_move.mode == "inspect" || _move.mode == "store" || _move.mode == "take" || _move.mode == "drop" || _move.mode == "give") { Facts.Add(_move.label); return true; }
        if (!AIInventory.Read(world, player, _move.from, out var from) || !AIInventory.Read(world, player, _move.to, out var to) || !_exchange.MayRequest(AIInventory.Stamp(from), AIInventory.Stamp(to))) { Status = SkillStatus.Failed; Reason = "교환할 슬롯 상태가 이미 바뀜"; return false; }
        Facts.Add(_move.label); return true;
    }
    public override BodyInput Tick(World world, Entity player, float now)
    {
        if (_move.mode == "inspect") return InspectTick(world, player, now);
        if (_move.mode == "store" || _move.mode == "take") return TransferTick(world, player, now);
        if (_move.mode == "drop" || _move.mode == "give") return DropTick(world, player, now);
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            _sent = true; _sentAt = now;
            if (_move.from != _move.to)
            {
                if (!AICraft.QueueInventoryChange(world, player, Create.Swap(player, player, _move.from, _move.to))) return Finish(SkillStatus.Failed, "교환 요청 큐에 넣지 못함");
                Facts.Add($"Swap queued {_move.from}↔{_move.to}; completion awaits both slots");
            }
            _exchange.RequestedOnce();
        }
        if (!_swapped && AIInventory.Read(world, player, _move.from, out var a) && AIInventory.Read(world, player, _move.to, out var b) && _exchange.Observe(AIInventory.Stamp(a), AIInventory.Stamp(b))) _swapped = true;
        if (_swapped)
        {
            if (!AIInventory.ActiveSlotsMatch(world, player, _move)) return Finish(SkillStatus.Failed, "양 슬롯 교환은 관측했지만 활성 장비 프리셋이 바뀌어 현재 착용 완료로 처리하지 않음");
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
        if (!AIInventory.OwnChest(world, _move.chest)) return Finish(SkillStatus.Failed, "살펴볼 자기 상자가 사라지거나 소유가 바뀜");
        var nav = Navigate(world, player, _move.chest, AIBase.ChestReach, now); if (nav.HasValue) return nav.Value;
        var chest = AIBase.NoteOpened(world, _move.chest);
        if (chest == null) return Finish(SkillStatus.Failed, "자기 상자로 열어 볼 수 없음");
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
        if (!_sent)
        {
            string stale = AIInventory.Stale(world, player, _move); if (stale != null) return Finish(SkillStatus.Failed, stale);
            var em = world.EntityManager; var inventory = em.GetBuffer<InventoryBuffer>(_move.toEntity, true);
            if (inventory.Length == 0) return Finish(SkillStatus.Failed, "목적 인벤토리 설정 없음");
            int end = _move.toEntity == player ? AIInventory.MainEnd(world, player) : inventory[0].startIndex + inventory[0].size;
            if (!AICraft.QueueInventoryChange(world, player, Create.MoveAmount(_move.fromEntity, _move.from, _move.toEntity, _move.to, end, _move.count, false))) return Finish(SkillStatus.Failed, "물품 이동 요청 큐에 넣지 못함");
            _sent = true; _sentAt = now;
            Facts.Add($"MoveAmount queued {_move.count}; no drop/destroy path; awaits actual source and destination change");
        }
        if (AIInventory.Read(world, _move.fromEntity, _move.from, out var from) && AIInventory.Read(world, _move.toEntity, _move.to, out var to))
        {
            int actual = AIInventoryRules.ObservedTransfer(AIInventory.Stamp(_move.beforeFrom), AIInventory.Stamp(from), UnitsLike(_move.beforeTo, _move.beforeFrom), UnitsLike(to, _move.beforeFrom), _move.count);
            if (actual > 0)
            {
                AIBase.NoteOpened(world, _move.chest); AIInventory.Opened(_move.chest);
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
            }
            _dropPosition = AIBody.Pos(world, player); _floorBefore = new HashSet<Entity>(Floor(world, _dropPosition).Keys);
            if (!AICraft.QueueInventoryChange(world, player, Create.DropItem(player, _move.from, _move.count, _dropPosition, player))) return Finish(SkillStatus.Failed, "내려놓기 요청 큐에 넣지 못함");
            _sent = true; _sentAt = now;
            Facts.Add($"DropItem queued at self position, dropper pickup blocked; source {_move.from}, units {_move.count}; recipient inventory never written");
        }
        int removed = 0, floorRemoved = -1, gained = 0;
        if (AIInventory.Read(world, player, _move.from, out var from))
        {
            int left = from.objectID == ObjectID.None ? 0 : AIInventory.Identity(from, _move.beforeFrom) ? AIInventory.Quantity(from) : -1;
            if (left >= 0) removed = AIInventory.Quantity(_move.beforeFrom) - left;
        }
        if (_floor == Entity.Null)
        {
            Entity match = Entity.Null; int matches = 0;
            foreach (var floor in Floor(world, _dropPosition))
                if (!_floorBefore.Contains(floor.Key) && AIInventory.Identity(floor.Value, _move.beforeFrom) && AIInventory.Quantity(floor.Value) == _move.count && (AIInventory.Info(_move.beforeFrom)?.isStackable == true || floor.Value.amount == _move.beforeFrom.amount)) { match = floor.Key; matches++; }
            if (matches > 1) return Finish(SkillStatus.Failed, "동시에 같은 바닥 물품이 여러 개 생겨 이번 내려놓기에 귀속하지 못함(재전송 없음)");
            if (matches == 1 && AIInventory.Read(world, match, 0, out var initial)) { _floor = match; _floorInitial = initial; Facts.Add("new floor entity observed " + match); }
        }
        if (_floor != Entity.Null && removed == _move.count)
        {
            if (_move.mode == "drop")
            {
                string result = $"출발 슬롯 {_move.from} 감소와 새 바닥 물품 {_floor} {_move.count}개 생성 확인";
                AIInventory.Completed(world, player, _move, result, _move.count); return Finish(SkillStatus.Done, result);
            }
            if (em.Exists(_floor) && em.HasComponent<PickUpItemCD>(_floor))
            {
                var pickup = em.GetComponentData<PickUpItemCD>(_floor);
                if (pickup.targetEntity == _move.recipient && (pickup.state == PickUpItemState.IsBeingPickedUp || pickup.state == PickUpItemState.ForcePickUp || pickup.state == PickUpItemState.HasBeenPickedUp)) _sawRecipient = true;
                if (pickup.targetEntity == _move.recipient && pickup.state == PickUpItemState.HasBeenPickedUp) _pickupReceipt = true;
                else if (pickup.targetEntity != Entity.Null && pickup.targetEntity != _move.recipient && (pickup.state == PickUpItemState.IsBeingPickedUp || pickup.state == PickUpItemState.ForcePickUp || pickup.state == PickUpItemState.HasBeenPickedUp)) return Finish(SkillStatus.Failed, "내려놓은 물건을 지정 수령자 이외의 플레이어가 가져감(재전송 없음)");
            }
            if (!em.Exists(_floor)) floorRemoved = AIInventory.Quantity(_floorInitial);
            else if (AIInventory.Read(world, _floor, 0, out var remainingFloor))
            {
                if (remainingFloor.objectID == ObjectID.None) floorRemoved = AIInventory.Quantity(_floorInitial);
                else if (AIInventory.Identity(remainingFloor, _floorInitial) && (AIInventory.Info(_floorInitial)?.isStackable == true || remainingFloor.amount == _floorInitial.amount)) floorRemoved = AIInventory.Quantity(_floorInitial) - AIInventory.Quantity(remainingFloor);
            }
            gained = AIInventory.MatchingCount(world, _move.recipient, _move.beforeFrom) - _recipientBefore;
            int received = AIInventoryRules.ObservedDelivery(removed, _move.count, _floor != Entity.Null, _pickupReceipt, floorRemoved, gained);
            if (received > 0)
            {
                string result = $"바닥 물품 {_floor}의 지정 수령 요청 관측 + 실제 바닥 감소 {floorRemoved}개 = {_move.recipientName} 가방 증가 {gained}개, {received}/{_move.count}개 수령 확인";
                AIInventory.Completed(world, player, _move, result, received); return Finish(SkillStatus.Done, result);
            }
        }
        if (now - _sentAt > 6f) ObserveOnce($"inventory-drop-pending:{removed}:{floorRemoved}:{gained}", $"내려놓기 후 지정 수령 미확인: 출발 감소 {removed}/{_move.count}, 바닥 {_floor} 실제 감소 {floorRemoved}, 수령자 가방 증가 {gained}, 대상 관측 {_sawRecipient}, 지정 pickup 요청 관측 {_pickupReceipt}(재전송 없이 AI 재판단)");
        return BodyInput.Idle;
    }
    public override void Cleanup(World world) { _nav.Dispose(world); }
    public override string Progress(World world, Entity player) => _sent ? "정상 인벤토리 요청의 실제 양 슬롯/손 상태를 관측 중" : _move?.label ?? "선택 없음";
}
