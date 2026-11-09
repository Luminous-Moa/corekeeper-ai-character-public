// Inventory selection is a menu, not a body skill. Only a confirmed leaf carries a normal game operation.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

internal sealed class AIInventoryMenuFrame
{
    internal string step = "categories", category = "", mode = "", digits = "", recipientName = "", sourceRole = "";
    internal bool fromChest, selected, hasChest;
    internal Entity source, chest, recipient;
    internal ObjectID chestId;
    internal int2 chestTile;
    internal int slot = -1, count;
    internal ContainedObjectsBuffer before;
    internal AIInventoryMove template;
    internal AIInventoryMenuFrame Copy() => (AIInventoryMenuFrame)MemberwiseClone();
}

public sealed class AIInventoryMenuChoice
{
    public readonly int revision;
    public readonly string node, action;
    public readonly bool IsLeaf;
    internal readonly AIInventoryMenuFrame next;
    internal readonly bool replace;
    internal AIInventoryMenuChoice(int revision, string node, string action, AIInventoryMenuFrame next, bool replace = false, bool leaf = false)
    { this.revision = revision; this.node = node; this.action = action; this.next = next?.Copy(); this.replace = replace; IsLeaf = leaf; }
}

public static class AIInventoryMenu
{
    private sealed class State
    {
        internal int revision;
        internal World world;
        internal Entity player;
        internal AIGoal goal;
        internal string notice = "";
        internal readonly List<AIInventoryMenuFrame> path = new List<AIInventoryMenuFrame>();
        internal AIInventoryMenuFrame Current => path.Count == 0 ? null : path[path.Count - 1];
    }
    private sealed class Item
    {
        internal Entity source;
        internal int slot;
        internal string location, category;
        internal ContainedObjectsBuffer value;
    }
    private static readonly Dictionary<AIAgent, State> States = new Dictionary<AIAgent, State>();
    private static int _revision;
    private static int NextRevision() => unchecked(++_revision);
    private static State S
    {
        get
        {
            var who = AIAgent.Cur;
            if (!States.TryGetValue(who, out var state)) States[who] = state = new State { revision = NextRevision() };
            return state;
        }
    }
    public static bool Active => S.Current != null;
    public static void Reset(string reason = "가방 선택 초기화")
    {
        States[AIAgent.Cur] = new State { revision = NextRevision(), notice = reason ?? "" };
    }
    private static void Ensure(World world, Entity player)
    {
        var s = S;
        if (ReferenceEquals(s.world, world) && s.player == player && ReferenceEquals(s.goal, AIGoals.Goal)) return;
        s.path.Clear(); s.revision = NextRevision(); s.world = world; s.player = player; s.goal = AIGoals.Goal;
        s.notice = "목표/캐릭터/월드 변경으로 이전 가방 선택을 닫음. 몸 동작과 부탁은 메뉴가 변경하지 않음";
    }
    private static string Node => S.Current?.step ?? "root";
    private static void Replace(AIInventoryMenuFrame frame, string notice)
    {
        var s = S;
        if (s.path.Count == 0) s.path.Add(frame.Copy()); else s.path[s.path.Count - 1] = frame.Copy();
        s.revision = NextRevision(); s.notice = notice ?? "";
    }
    private static AIBase.ChestMem Memory(AIInventoryMenuFrame frame) => frame != null && frame.hasChest ?
        AIBase.OwnChests().Find(c => c.id == frame.chestId && math.all(c.tile == frame.chestTile)) : null;
    private static bool Readable(World world, Entity player) => world != null && world.IsCreated && world.EntityManager.Exists(player) &&
        world.EntityManager.HasBuffer<ContainedObjectsBuffer>(player) && world.EntityManager.HasBuffer<InventoryBuffer>(player);

    // This is a new possession-menu grouping based on actual game ObjectType, not a claim about the creative UI.
    internal static string Category(ContainedObjectsBuffer value)
    {
        var info = AIInventory.Info(value);
        if (info == null) return "기타·분류 불명";
        switch (info.objectType.ToString())
        {
            case "Helm": case "BreastArmor": case "PantsArmor": case "Necklace": case "Ring": case "Offhand": case "Bag": case "Lantern": case "Pouch": return "착용 장비";
            case "MeleeWeapon": case "RangeWeapon": case "SummoningWeapon": case "ThrowingWeapon": case "BeamWeapon": return "무기";
            case "Shovel": case "Hoe": case "MiningPick": case "PaintTool": case "FishingRod": case "BugNet": case "Sledge": case "RoofingTool": case "DrillTool": case "WaterCan": case "Bucket": case "Seeder": return "도구";
            case "CastingItem": return "사용 아이템";
            case "Eatable": return "섭취물";
            case "PlaceablePrefab": case "TrainingDummy": return "설치물";
            case "Critter": case "Pet": case "Creature": return "생물·반려동물";
            case "Valuable": return "귀중품";
            case "UniqueCraftingComponent": return "특수 제작 부품";
            case "KeyItem": return "열쇠·중요 물품";
            case "Instrument": return "악기";
            case "NonUsable": return "재료·기타";
            default: return "기타·분류 불명";
        }
    }
    private static string ItemText(ContainedObjectsBuffer value)
    {
        var info = AIInventory.Info(value);
        string amount = info == null ? $"원시 상태 {value.amount}, 수량/상태 불명" : $"상태 {value.amount}, 개수 {AIInventory.Quantity(value)}";
        return $"{value.objectID} (변형 {value.variation}, {amount})";
    }
    private static string SourceRole(World world, Entity player, Entity source, int slot, bool fromChest, Entity chest)
    {
        if (fromChest)
        {
            var em = world.EntityManager;
            if (source != chest || !em.Exists(chest) || !em.HasBuffer<InventoryBuffer>(chest)) return "관측 상자 위치 아님";
            var inventories = em.GetBuffer<InventoryBuffer>(chest, true);
            return inventories.Length > 0 && slot >= inventories[0].startIndex && slot < inventories[0].startIndex + inventories[0].size ? "관측한 자기 상자" : "상자 인벤토리 범위 밖";
        }
        if (source != player) return "자기 보유 위치 아님";
        if (AIInventory.IsMain(world, player, slot)) return slot < 10 ? "핫바" : "주 가방";
        foreach (var gear in AIInventory.GearSlots(world, player)) if (gear.slot == slot) return "현재 착용 " + gear.name;
        return "현재 주 가방/착용 범위 밖";
    }
    private static List<Item> Items(World world, Entity player, AIInventoryMenuFrame frame)
    {
        var result = new List<Item>(); if (!Readable(world, player)) return result;
        var seen = new HashSet<int>();
        void Add(Entity source, int slot, string location)
        {
            if (!seen.Add(slot) || !AIInventory.Read(world, source, slot, out var value) || value.objectID == ObjectID.None) return;
            result.Add(new Item { source = source, slot = slot, location = location, value = value, category = Category(value) });
        }
        if (frame.fromChest)
        {
            if (!AIInventory.MenuChestOpened(world, player, frame.chest)) return result;
            var inv = world.EntityManager.GetBuffer<InventoryBuffer>(frame.chest, true); if (inv.Length == 0) return result;
            for (int i = inv[0].startIndex; i < inv[0].startIndex + inv[0].size; i++) Add(frame.chest, i, $"자기 상자 chest:{frame.chestTile.x},{frame.chestTile.y} 슬롯 {i}");
        }
        else
        {
            for (int i = AIInventory.MainStart(world, player); i < AIInventory.MainEnd(world, player); i++) Add(player, i, i < 10 ? "핫바 " + i : "가방 " + i);
            foreach (var gear in AIInventory.GearSlots(world, player)) Add(player, gear.slot, $"현재 착용 {gear.name} 슬롯 {gear.slot}");
        }
        return result;
    }
    private static AICandidate Navigation(string action, string label, AIInventoryMenuFrame next = null, bool replace = false)
    {
        var token = new AIInventoryMenuChoice(S.revision, Node, action, next, replace);
        return new AICandidate { kind = "inventory_menu", key = $"inventory-menu:{token.revision}:{token.node}:{action}:{label}", desc = label + " · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음", inventoryMenu = token };
    }
    private static AICandidate Leaf(AICandidate candidate, AIInventoryMenuFrame next = null)
    {
        candidate.inventoryMenu = new AIInventoryMenuChoice(S.revision, Node, "execute", next, true, true);
        candidate.desc = "실행 확인 — " + candidate.desc;
        return candidate;
    }
    private static void Common(List<AICandidate> result)
    {
        result.Add(Navigation("back", "이전 선택 단계로 돌아가기"));
        result.Add(Navigation("cancel", "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지"));
    }
    private static string ValidateFrame(World world, Entity player, AIInventoryMenuFrame frame)
    {
        if (frame == null) return null;
        if (frame.hasChest && Memory(frame) == null) return "선택한 자기 상자 기억/소유가 바뀜";
        if (frame.fromChest && frame.step != "chests" && frame.step != "access" && frame.step != "await" && !AIInventory.MenuChestOpened(world, player, frame.chest)) return "상자를 현재 열어 관측할 수 있는 조건이 바뀜";
        if (frame.selected)
        {
            string role = SourceRole(world, player, frame.source, frame.slot, frame.fromChest, frame.chest);
            if (role != frame.sourceRole) return $"선택한 물품의 위치 역할이 바뀜: 슬롯 {frame.slot}, {frame.sourceRole} → {role}";
        }
        if (frame.selected && (!AIInventory.Read(world, frame.source, frame.slot, out var current) || !current.EqualsExact(frame.before))) return "선택한 물품의 슬롯/변형/상태가 바뀜";
        if (frame.template != null)
        {
            string stale = AIInventory.Stale(world, player, frame.template);
            if (stale != null) return stale;
        }
        return null;
    }
    private static void Recover(World world, Entity player, string why)
    {
        var s = S; var current = s.Current;
        if (current == null) return;
        if (current.hasChest && Memory(current) != null && current.fromChest && !AIInventory.MenuChestOpened(world, player, current.chest))
        {
            var access = current.Copy(); access.step = "access"; access.selected = false; access.template = null;
            while (s.path.Count > 0 && s.Current.step != "chests") s.path.RemoveAt(s.path.Count - 1);
            s.path.Add(access); s.revision = NextRevision(); s.notice = why + ". 상자 접근/열어보기 단계로 돌아감"; return;
        }
        int items = s.path.FindLastIndex(f => f.step == "items");
        if (items >= 0) s.path.RemoveRange(items + 1, s.path.Count - items - 1);
        else { s.path.Clear(); s.path.Add(new AIInventoryMenuFrame { source = player }); }
        s.revision = NextRevision(); s.notice = why + ". 현재 실제 물품에서 다시 고름";
    }
    private static void RefreshAccess(World world, Entity player)
    {
        var current = S.Current;
        if (current == null || current.step != "access" && current.step != "await") return;
        var memory = Memory(current); if (memory == null) return;
        Entity chest = AIInventory.MenuVisibleChest(world, player, memory);
        if (chest == Entity.Null) return;
        if (AIInventory.MenuChestOpened(world, player, chest))
        {
            var next = current.Copy(); next.chest = chest; next.template = null;
            if (current.mode == "store") next.step = "destinations";
            else { next.step = "categories"; next.fromChest = true; next.source = chest; next.selected = false; next.category = ""; }
            Replace(next, "자기 상자를 실제로 열어 관측함. 현재 슬롯에서 다음 선택");
        }
        else if (current.step == "await" && math.distance(AIBody.Pos(world, player).xz, world.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(chest).Position.xz) <= AIBase.ChestReach)
        {
            var next = current.Copy(); next.step = "access"; next.chest = chest;
            Replace(next, "기억한 상자에 도착함. 내용은 아직 공개하지 않고 열어보기 선택을 기다림");
        }
    }
    public static List<AICandidate> Candidates(World world, Entity player, AIObservation observation, string currentKey)
    {
        Ensure(world, player); var result = new List<AICandidate>(); if (!Readable(world, player)) return result;
        if (!Active)
        {
            result.Add(Navigation("enter", "인벤토리에서 물품 분류와 가능한 조작 고르기", new AIInventoryMenuFrame { source = player }));
            return result;
        }
        string changed = ValidateFrame(world, player, S.Current);
        if (changed != null) Recover(world, player, changed);
        RefreshAccess(world, player);
        var frame = S.Current;
        switch (frame.step)
        {
            case "categories":
            {
                var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var item in Items(world, player, frame)) { groups.TryGetValue(item.category, out int n); groups[item.category] = n + 1; }
                foreach (var group in groups)
                {
                    var next = frame.Copy(); next.step = "items"; next.category = group.Key;
                    result.Add(Navigation("category:" + group.Key, $"분류 {group.Key} — 실제 보유 슬롯 {group.Value}개", next));
                }
                if (!frame.fromChest && AIBase.OwnChests().Count > 0)
                {
                    var next = new AIInventoryMenuFrame { step = "chests", source = player, mode = "take", fromChest = true };
                    result.Add(Navigation("own-chests", "자기 상자에서 꺼낼 물품 고르기 — 접근/열어보기 후 내용 확인", next));
                }
                break;
            }
            case "items":
                foreach (var item in Items(world, player, frame)) if (item.category == frame.category)
                {
                    var next = frame.Copy(); next.step = "operations"; next.source = item.source; next.slot = item.slot; next.before = item.value; next.selected = true; next.template = null;
                    next.sourceRole = SourceRole(world, player, item.source, item.slot, frame.fromChest, frame.chest);
                    result.Add(Navigation("item:" + item.slot, item.location + " · " + ItemText(item.value), next));
                }
                break;
            case "operations": AddOperations(world, player, frame, result); break;
            case "chests":
            {
                var memories = AIBase.OwnChests(); memories.Sort((a, b) => a.tile.x != b.tile.x ? a.tile.x.CompareTo(b.tile.x) : a.tile.y.CompareTo(b.tile.y));
                foreach (var memory in memories)
                {
                    var next = frame.Copy(); next.step = "access"; next.hasChest = true; next.chestId = memory.id; next.chestTile = memory.tile; next.chest = AIInventory.MenuVisibleChest(world, player, memory); next.template = null;
                    result.Add(Navigation("chest:" + memory.tile.x + ":" + memory.tile.y,
                        $"자기 상자 chest:{memory.tile.x},{memory.tile.y} · {memory.id} — 현재 내용은 실제 열어보기 뒤 확인", next));
                }
                break;
            }
            case "access":
            {
                var memory = Memory(frame); var candidate = memory == null ? null : AIInventory.MenuChestAccess(world, player, observation, memory);
                if (candidate != null && candidate.key != currentKey)
                {
                    var next = frame.Copy(); next.step = "await";
                    result.Add(Leaf(candidate, next));
                }
                break;
            }
            case "await": break; // current physical access/inspection runs; back/cancel never stop it
            case "recipients":
                foreach (var recipient in AITeammates.Visible(world, player)) if (recipient.entity != player && (recipient.maxHealth <= 0 || recipient.health > 0))
                {
                    var moves = AIInventory.MenuMoves(world, player, frame.source, frame.slot, "give", Entity.Null, recipient.entity, recipient.name, 1);
                    if (moves.Count == 0) continue;
                    var next = Arguments(frame, moves[0], world, player);
                    result.Add(Navigation("recipient:" + recipient.entity.Index, "수령 대상 " + recipient.name + " — 접근 후 바닥 물품과 실제 수령을 확인", next));
                }
                break;
            case "destinations":
                foreach (var move in AIInventory.MenuMoves(world, player, frame.source, frame.slot, frame.mode, frame.chest, frame.recipient, frame.recipientName, 1))
                {
                    var next = Arguments(frame, move, world, player);
                    result.Add(Navigation("destination:" + move.toEntity.Index + ":" + move.to, "목적 슬롯 선택 — " + move.label, next));
                }
                break;
            case "quantity": AddQuantities(world, player, frame, result); break;
            case "number": AddDigits(world, player, frame, result); break;
            case "confirm":
            {
                var move = FinalMove(world, player, frame);
                if (move != null && move.key != currentKey) result.Add(Leaf(AIInventory.MenuCandidate(move)));
                break;
            }
        }
        Common(result); return result;
    }
    private static AIInventoryMenuFrame Arguments(AIInventoryMenuFrame frame, AIInventoryMove move, World world, Entity player)
    {
        var next = frame.Copy(); next.template = move; next.recipient = move.recipient; next.recipientName = move.recipientName;
        int maximum = AIInventory.MenuMaximum(world, player, move);
        next.step = move.mode == "equip" || move.mode == "unequip" || move.mode == "hold" || maximum == 1 ? "confirm" : "quantity";
        next.count = next.step == "confirm" ? 1 : 0; return next;
    }
    private static void AddOperations(World world, Entity player, AIInventoryMenuFrame frame, List<AICandidate> result)
    {
        if (AIInventory.Info(frame.before) == null) return;
        var operations = frame.fromChest ? new[] { "take" } : new[] { "equip", "unequip", "hold", "store", "drop", "give" };
        foreach (string mode in operations)
        {
            var next = frame.Copy(); next.mode = mode; next.template = null;
            if (mode == "store")
            {
                if (!AIInventory.IsMain(world, player, frame.slot) || AIBase.OwnChests().Count == 0 || AIInventory.Gate(world, player) != null) continue;
                next.step = "chests"; next.hasChest = false; next.chest = Entity.Null;
            }
            else if (mode == "give")
            {
                bool possible = false;
                foreach (var recipient in AITeammates.Visible(world, player)) if (recipient.entity != player && (recipient.maxHealth <= 0 || recipient.health > 0) &&
                    AIInventory.MenuMoves(world, player, frame.source, frame.slot, mode, Entity.Null, recipient.entity, recipient.name, 1).Count > 0) possible = true;
                if (!possible) continue;
                next.step = "recipients";
            }
            else
            {
                var moves = AIInventory.MenuMoves(world, player, frame.source, frame.slot, mode, frame.chest, Entity.Null, "", 1);
                if (moves.Count == 0) continue;
                if (mode == "drop") next = Arguments(next, moves[0], world, player);
                else next.step = "destinations";
            }
            result.Add(Navigation("operation:" + mode, "조작 " + Operation(mode), next));
        }
    }
    private static string Operation(string mode)
    {
        switch (mode)
        {
            case "equip": return "착용·교체"; case "unequip": return "착용 해제"; case "hold": return "손에 들기";
            case "store": return "자기 상자에 보관"; case "take": return "가방으로 꺼내기"; case "drop": return "바닥에 내려놓기"; case "give": return "보이는 동료에게 전달";
            default: return mode;
        }
    }
    private static void AddQuantities(World world, Entity player, AIInventoryMenuFrame frame, List<AICandidate> result)
    {
        int maximum = AIInventory.MenuMaximum(world, player, frame.template); if (maximum <= 0) return;
        var amounts = new HashSet<int>();
        void Add(int count, string label)
        {
            if (count <= 0 || count > maximum || !amounts.Add(count)) return;
            var next = frame.Copy(); next.step = "confirm"; next.count = count;
            result.Add(Navigation("quantity:" + count, label, next));
        }
        Add(1, "수량 1개");
        int requested = AIInventory.MenuRequestedCount(AIInventory.MenuGoal(world, player, frame.template));
        if (requested > 0) Add(math.min(requested, maximum), $"현재 부탁 잔량 {requested}개 중 이 슬롯에서 가능한 {math.min(requested, maximum)}개");
        Add(maximum, $"이 물품/목적 슬롯에서 현재 가능한 전량 {maximum}개");
        var digits = frame.Copy(); digits.step = "number"; digits.digits = "";
        result.Add(Navigation("custom-quantity", $"다른 정확한 수량 입력 — 1~{maximum}, 각 자리 선택 후 확정", digits));
    }
    private static void AddDigits(World world, Entity player, AIInventoryMenuFrame frame, List<AICandidate> result)
    {
        int maximum = AIInventory.MenuMaximum(world, player, frame.template); if (maximum <= 0) return;
        for (int digit = 0; digit <= 9; digit++)
        {
            string text = frame.digits + digit;
            if (!int.TryParse(text, out int value) || value > maximum || text.Length > maximum.ToString().Length) continue;
            var next = frame.Copy(); next.digits = text;
            result.Add(Navigation("digit:" + digit, "수량 자리 " + digit + $" 추가 → {text} (유효 범위 1~{maximum})", next, true));
        }
        if (frame.digits.Length > 0)
        {
            var delete = frame.Copy(); delete.digits = delete.digits.Substring(0, delete.digits.Length - 1);
            result.Add(Navigation("erase-digit", "마지막 수량 자리 지우기", delete, true));
        }
        if (int.TryParse(frame.digits, out int quantity) && quantity > 0 && quantity <= maximum)
        {
            var next = frame.Copy(); next.step = "confirm"; next.count = quantity;
            result.Add(Navigation("number-confirm", $"정확한 수량 {quantity}개로 확정 — 아직 물품은 움직이지 않음", next));
        }
    }
    private static AIInventoryMove FinalMove(World world, Entity player, AIInventoryMenuFrame frame)
    {
        if (frame.template == null || frame.count <= 0) return null;
        var original = frame.template;
        var move = AIInventory.MenuMoves(world, player, frame.source, frame.slot, frame.mode, frame.chest, frame.recipient, frame.recipientName, frame.count)
            .Find(x => x.toEntity == original.toEntity && x.to == original.to && x.recipient == original.recipient && x.beforeFrom.EqualsExact(original.beforeFrom) && x.beforeTo.EqualsExact(original.beforeTo));
        return move;
    }
    public static string Stale(World world, Entity player, AICandidate candidate)
    {
        var token = candidate?.inventoryMenu; if (token == null) return null;
        Ensure(world, player);
        if (token.revision != S.revision || token.node != Node) return "이전 가방 메뉴 단계의 답(revision/node가 바뀜)";
        if (!token.IsLeaf && (token.action == "back" || token.action == "cancel")) return null;
        string changed = ValidateFrame(world, player, S.Current) ?? ValidateFrame(world, player, token.next);
        if (changed != null) { Recover(world, player, changed); return changed; }
        if (token.IsLeaf && candidate.inventory != null)
        {
            changed = AIInventory.Stale(world, player, candidate.inventory);
            if (changed != null) { Recover(world, player, changed); return changed; }
        }
        return null;
    }
    public static bool ApplyNavigation(World world, Entity player, AICandidate candidate, out string note)
    {
        note = ""; if (candidate?.kind != "inventory_menu") return false;
        string stale = Stale(world, player, candidate);
        if (stale != null) { note = stale; return true; }
        var token = candidate.inventoryMenu;
        if (token.action == "cancel") { S.path.Clear(); S.revision = NextRevision(); S.notice = note = "가방 선택만 전체 취소. 원래 부탁/목표/몸 동작 유지"; return true; }
        if (token.action == "back")
        {
            if (S.path.Count > 0) S.path.RemoveAt(S.path.Count - 1);
            S.revision = NextRevision(); S.notice = note = "이전 가방 선택 단계로 돌아감. 물품/목표/몸 동작 유지"; return true;
        }
        if (token.next == null) { note = "가방 메뉴의 다음 단계가 없음"; return true; }
        if (token.replace && S.path.Count > 0) S.path[S.path.Count - 1] = token.next.Copy(); else S.path.Add(token.next.Copy());
        S.revision = NextRevision(); S.notice = note = "가방 선택 단계: " + PathText(); return true;
    }
    public static void NoteLeafSelected(AICandidate candidate)
    {
        var token = candidate?.inventoryMenu;
        if (token == null || !token.IsLeaf || token.revision != S.revision || token.node != Node) return;
        if (token.next != null) Replace(token.next, "접근/열어보기의 실제 결과를 기다림. 선택 취소는 진행 몸 동작을 멈추지 않음");
        else { S.path.Clear(); S.revision = NextRevision(); S.notice = "최종 물품 조작을 선택함. 성공은 정상 게임 요청 후 실제 상태로 확인"; }
    }
    private static string PathText()
    {
        var parts = new List<string> { "인벤토리" };
        foreach (var frame in S.path)
        {
            if (frame.step == "items") parts.Add(frame.category);
            else if (frame.step == "operations") parts.Add($"슬롯 {frame.slot} {frame.before.objectID}");
            else if (frame.step == "chests") parts.Add("자기 상자");
            else if (frame.step == "destinations") parts.Add(Operation(frame.mode) + " 목적 슬롯");
            else if (frame.step == "recipients") parts.Add("전달 대상");
            else if (frame.step == "quantity" || frame.step == "number") parts.Add("수량" + (frame.digits.Length > 0 ? " " + frame.digits : ""));
            else if (frame.step == "confirm") parts.Add($"실행 확인 {frame.count}개");
            else if (frame.step == "access" || frame.step == "await") parts.Add($"상자 {frame.chestTile.x},{frame.chestTile.y} {(frame.step == "await" ? "관측 대기" : "접근/열기")}");
        }
        return string.Join(" → ", parts);
    }
    public static string PromptLine() => Active ? $"가방 선택 메뉴: {PathText()}. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. {S.notice}" :
        "가방 선택 메뉴: 닫힘. 인벤토리 진입에서 실제 보유 물품의 분류부터 선택. " + S.notice;
    public static string PromptLine(World world, Entity player) { Ensure(world, player); return PromptLine(); }
}
