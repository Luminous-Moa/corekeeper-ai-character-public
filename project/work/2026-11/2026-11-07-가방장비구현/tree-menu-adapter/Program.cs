using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Text.Json;

static class Program
{
    static int checks, failed, selections;
    static World world; static Entity player; static DynamicBuffer<ContainedObjectsBuffer> bag;
    static readonly List<object> payloads = new();
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); checks++; if (!ok) failed++; }
    static ContainedObjectsBuffer Item(ObjectID id, int state = 100, int variation = 0, int aux = 0) => new() { objectData = new() { objectID = id, amount = state, variation = variation }, auxDataIndex = aux };
    static void Setup(AIGoal goal = null)
    {
        AIAgent.Cur = new(); world = new(); player = new() { Index = 1 }; bag = new();
        for (int i = 0; i < 90; i++) bag.Add(default);
        world.EntityManager.PutBuffer(player, bag);
        var inventories = new DynamicBuffer<InventoryBuffer>();
        inventories.Add(new() { startIndex = 0, size = 40, sizeX = 10, sizeY = 4, maxSize = 40, extraInventorySizeSlot = -1 });
        inventories.Add(new() { startIndex = 40, size = 50, sizeX = 1, sizeY = 50, maxSize = 50, extraInventorySizeSlot = -1 });
        world.EntityManager.PutBuffer(player, inventories); world.EntityManager.PutBuffer(player, new DynamicBuffer<UIActionBuffer>());
        world.EntityManager.Put(player, new LocalTransform());
        world.EntityManager.Put(player, new EquipmentCD { helmSlotIndex = 40, breastSlotIndex = 41, pantsSlotIndex = 42, necklaceSlotIndex = 43, ring1SlotIndex = 44, ring2SlotIndex = 45, offHandIndex = 46, bagIndex = 47, lanternIndex = 48, pouch1Index = 49 });
        AIBase.Memories.Clear(); AITeammates.Seen.Clear(); AICraft.Requests.Clear(); PugDatabase.Unknown.Clear(); PugDatabase.Overrides.Clear();
        AIGoals.Goal = goal ?? new() { kind = GoalKind.Other }; AIInventory.Reset(); AIInventory.GoalSet(world, player, AIGoals.Goal);
    }
    static List<AICandidate> Menu(string reason = "")
    {
        var cs = AIInventoryMenu.Candidates(world, player, new() { pos = AIBody.Pos(world, player) }, "");
        for (int i = 0; i < cs.Count; i++) cs[i].value = "c" + (i + 1);
        payloads.Add(new { sample = reason, input = AIInventoryMenu.PromptLine(), inventoryRequests = AICraft.Requests.Count,
            choices = cs.Select(c => new { value = c.value, c.kind, c.desc, action = c.inventoryMenu?.action, leaf = c.inventoryMenu?.IsLeaf, c.itemId, c.itemVariation, c.itemCount }).ToArray() });
        return cs;
    }
    static AICandidate Choose(string action)
    {
        var c = Menu(action).Find(x => x.inventoryMenu?.action == action);
        if (c == null) throw new Exception("missing choice " + action + " in " + AIInventoryMenu.PromptLine());
        string stale = AIInventoryMenu.Stale(world, player, c); Check(stale == null, "fresh navigation/leaf token " + action);
        if (c.kind == "inventory_menu")
        {
            int before = AICraft.Requests.Count; var goal = AIGoals.Goal;
            Check(AIInventoryMenu.ApplyNavigation(world, player, c, out _), "navigation dispatch " + action);
            Check(AICraft.Requests.Count == before && ReferenceEquals(goal, AIGoals.Goal), "navigation keeps request queue and original goal " + action);
        }
        selections++; return c;
    }
    static void Enter(string category, int slot) { Choose("enter"); Choose("category:" + category); Choose("item:" + slot); }
    static (Entity, DynamicBuffer<ContainedObjectsBuffer>) Chest(int slots = 8)
    {
        Entity chest = new() { Index = 2 }; var contents = new DynamicBuffer<ContainedObjectsBuffer>(); for (int i = 0; i < slots; i++) contents.Add(default);
        var inv = new DynamicBuffer<InventoryBuffer>(); inv.Add(new() { size = slots, sizeX = slots, sizeY = 1, maxSize = slots, extraInventorySizeSlot = -1 });
        world.EntityManager.PutBuffer(chest, contents); world.EntityManager.PutBuffer(chest, inv);
        world.EntityManager.Put(chest, new LocalTransform { Position = new(1, 0, 0) }); world.EntityManager.Put(chest, new ObjectDataCD { objectID = ObjectID.Chest }); world.EntityManager.Put(chest, new InventoryAutoTransferEnabledCD());
        AIBase.Memories.Add(new() { id = ObjectID.Chest, tile = new(1, 0), slots = slots, free = slots }); return (chest, contents);
    }
    static InventorySkill Execute(AICandidate c)
    {
        Check(c.inventoryMenu?.IsLeaf == true && c.inventory != null, "only confirmed physical leaf reaches inventory skill");
        AIInventoryMenu.NoteLeafSelected(c); var skill = new InventorySkill(c.inventory); Check(skill.Start(world, player, 0), "physical leaf checks current world"); skill.Tick(world, player, 0); return skill;
    }
    static void CancelHere(string where)
    {
        var before = bag.Select(x => (x.objectID, x.amount, x.variation, x.auxDataIndex)).ToArray(); var goal = AIGoals.Goal; int requests = AICraft.Requests.Count;
        Choose("cancel"); Check(!AIInventoryMenu.Active && ReferenceEquals(goal, AIGoals.Goal) && requests == AICraft.Requests.Count && before.SequenceEqual(bag.Select(x => (x.objectID, x.amount, x.variation, x.auxDataIndex))), "whole selection cancel has no physical/goal effect at " + where);
    }
    static int Main()
    {
        Setup(new() { kind = GoalKind.Equip, item = ObjectID.CopperHelm, slot = 12 });
        for (int i = 0; i < 25; i++) bag[i] = Item(ObjectID.Ring, 100 - i, i, i + 1);
        bag[30] = Item(ObjectID.CopperSword); bag[40] = Item(ObjectID.CopperHelm, 20);
        var root = Menu("root"); Check(root.Count == 1 && root[0].inventoryMenu.action == "enter", "ordinary root exposes one inventory entry, no flat cross-product");
        Choose("enter"); var cats = Menu("all possessed categories"); Check(cats.Exists(c => c.inventoryMenu.action == "category:무기") && cats.Exists(c => c.inventoryMenu.action == "category:착용 장비"), "current armor goal never filters other owned weapon category");
        Choose("category:착용 장비"); var items = Menu("more than fourteen exact items"); Check(items.Count(c => c.inventoryMenu.action.StartsWith("item:")) == 26, "all 25 ring instances plus currently worn helm remain accessible beyond 8/14");
        Check(items.Exists(c => c.inventoryMenu.next?.before.auxDataIndex == 25 && c.desc.Contains("변형 24") && !c.desc.Contains("aux")) && items.Exists(c => c.desc.Contains("현재 착용 head")), "actual state/variation/worn location are visible while opaque aux remains only in exact selection token");
        Choose("item:0"); Choose("back"); Choose("item:24"); Check(AICraft.Requests.Count == 0, "enter category item back different item never queues movement"); CancelHere("operations");

        Setup(); bag[0] = Item(ObjectID.CopperSword, 0); PugDatabase.Unknown.Add((ObjectID)777); bag[1] = Item((ObjectID)777, 44, 7, 9);
        PugDatabase.Overrides[(ObjectID)778] = new() { objectType = ObjectType.BeamWeapon }; bag[2] = Item((ObjectID)778);
        PugDatabase.Overrides[(ObjectID)779] = new() { objectType = ObjectType.Eatable, isStackable = true }; bag[3] = Item((ObjectID)779, 4);
        Choose("enter"); Check(Menu().Exists(c => c.inventoryMenu.action == "category:섭취물"), "eatable is a broad ingestion category, not a claim all are human food"); Choose("category:무기");
        Check(Menu().Count(c => c.inventoryMenu.action.StartsWith("item:")) == 2, "broken amount-zero equipment and BeamWeapon remain in possession category"); Choose("back"); Choose("category:기타·분류 불명");
        Check(Menu().Find(c => c.inventoryMenu.action == "item:1").desc.Contains("수량/상태 불명"), "unknown metadata retains raw possessed instance with quantity uncertainty"); CancelHere("unknown item list");

        Setup(new() { kind = GoalKind.Hold, item = ObjectID.CopperSword, slot = 12, where = "hotbar:5", count = 1 }); bag[12] = Item(ObjectID.CopperSword, 300, 2); bag[5] = Item(ObjectID.Wood, 20);
        Enter("무기", 12); Choose("operation:hold"); var destinations = Menu("all physical hotbar destinations"); Check(destinations.Count(c => c.inventoryMenu.action.StartsWith("destination:")) == 10, "all ten valid hotbar slots are selectable, including occupied target");
        Choose("destination:1:5"); var hold = Choose("execute"); Check(hold.inventory.goal == AIGoals.Goal && hold.inventory.to == 5 && AICraft.Requests.Count == 0, "matching final leaf binds same goal but still sends no request before body applies");
        var skill = Execute(hold); Check(AICraft.Requests.Count == 1 && !AIInventoryMenu.Active, "confirmed hold creates exactly one normal request and closes menu");
        (bag[12], bag[5]) = (bag[5], bag[12]); world.EntityManager.Put(player, new EquippedObjectCD { equippedSlotIndex = 5, containedObject = bag[5] }); skill.Tick(world, player, 1);
        Check(skill.Status == SkillStatus.Done && AIInventory.GoalDone(world, player, out _), "chosen exact hold leaf observes both slots and actual hand before goal receipt");

        Setup(new() { kind = GoalKind.Equip, item = ObjectID.CopperHelm, count = 1 }); bag[12] = Item(ObjectID.CopperSword);
        Enter("무기", 12); Choose("operation:drop"); var drop = Choose("execute"); Check(drop.inventory.goal == null, "unrelated valid preparation/drop leaf cannot complete current armor goal");
        skill = Execute(drop); bag[12] = default; Entity floor = new() { Index = 200 }; var fb = new DynamicBuffer<ContainedObjectsBuffer>(); fb.Add(Item(ObjectID.CopperSword));
        world.EntityManager.PutBuffer(floor, fb); world.EntityManager.Put(floor, new LocalTransform()); world.EntityManager.Put(floor, new PickUpItemCD()); skill.Tick(world, player, 1);
        Check(skill.Status == SkillStatus.Done && !AIInventory.GoalDone(world, player, out _), "successful unrelated physical action leaves original goal uncompleted");

        Setup(new() { kind = GoalKind.Store, item = ObjectID.Wood, count = 23 }); bag[0] = Item(ObjectID.Wood, 30); var (chest, box) = Chest(36);
        Enter("재료·기타", 0); Choose("operation:store"); Choose("chest:1:0"); var inspect = Choose("execute"); Check(inspect.inventory.mode == "inspect" && AICraft.Requests.Count == 0, "store chooses access/inspect before any content-based target"); skill = Execute(inspect); Check(skill.Status == SkillStatus.Done, "normal inspection returns actual nearby own chest observation");
        var allSlots = Menu("all thirty-six destination slots"); Check(allSlots.Count(c => c.inventoryMenu.action.StartsWith("destination:")) == 36, "all valid chest destinations exceed fourteen without truncation"); Choose("destination:2:0");
        var quantities = Menu("short quantity paths"); Check(quantities.Exists(c => c.inventoryMenu.action == "quantity:23") && quantities.Exists(c => c.inventoryMenu.action == "quantity:30"), "one, actual request and all quantities avoid unnecessary digit calls");
        Choose("custom-quantity"); Choose("digit:2"); Choose("digit:3"); Choose("number-confirm"); var transfer = Choose("execute"); Check(transfer.inventory.count == 23, "direct digits reach any valid non-preset integer through current children only");
        skill = Execute(transfer); bag[0] = Item(ObjectID.Wood, 7); box[0] = Item(ObjectID.Wood, 23); skill.Tick(world, player, 1); Check(skill.Status == SkillStatus.Done && AIInventory.GoalDone(world, player, out _), "tree selection reaches normal transfer and matching actual two-side observation");

        Setup(new() { kind = GoalKind.Other }); bag[0] = Item(ObjectID.Wood, 10); (chest, box) = Chest(); box[0] = Item(ObjectID.Wood, 50);
        Choose("enter"); Choose("own-chests"); var chestChoices = Menu("unopened chest list"); Check(chestChoices.All(c => !c.desc.Contains("50")), "unopened own chest list never discloses its current contents"); Choose("chest:1:0"); inspect = Choose("execute"); skill = Execute(inspect);
        Choose("category:재료·기타"); var chestItems = Menu("observed chest items"); Check(chestItems.Exists(c => c.desc.Contains("상자") && c.desc.Contains("50")), "only after normal inspection does chest source reveal actual slots"); Choose("back"); Choose("back"); Check(Menu().Exists(c => c.inventoryMenu.action == "chest:1:0"), "back from observed chest categories returns to chest selection, not an automatic reopen loop"); CancelHere("chest selection");

        Setup(); bag[12] = Item(ObjectID.CopperSword); Enter("무기", 12); var oldOperation = Menu().Find(c => c.inventoryMenu.action == "operation:hold"); bag[12] = Item(ObjectID.CopperSword, 99);
        Check(AIInventoryMenu.Stale(world, player, oldOperation) != null && AICraft.Requests.Count == 0, "source state changed during menu never reinterprets old selection"); Check(Menu().Exists(c => c.inventoryMenu.action == "item:12"), "changed source returns to current exact item choices");
        var oldItem = Menu().Find(c => c.inventoryMenu.action == "item:12"); Choose("back"); Check(AIInventoryMenu.Stale(world, player, oldItem) != null, "back invalidates previously returned item tokens"); CancelHere("categories after stale recovery");
        Setup(); bag[0] = Item(ObjectID.Wood, 10); Choose("enter"); var oldCategory = Menu()[0]; var nextGoal = new AIGoal { kind = GoalKind.Other }; AIGoals.Goal = nextGoal;
        Check(AIInventoryMenu.Stale(world, player, oldCategory) != null && !AIInventoryMenu.Active && ReferenceEquals(AIGoals.Goal, nextGoal), "new goal invalidates menu without modifying the new goal or executing anything");

        Setup(); bag[40] = Item(ObjectID.CopperHelm, 80); bag[60] = Item(ObjectID.CopperHelm, 70, 2); Enter("착용 장비", 40);
        var oldUnequip = Menu("wearing role before preset switch").Find(c => c.inventoryMenu.action == "operation:unequip");
        var originalEquipment = world.EntityManager.GetComponentData<EquipmentCD>(player); var otherEquipment = originalEquipment; otherEquipment.helmSlotIndex = 60;
        var rolePresets = new DynamicBuffer<EquipmentPresetsBuffer>(); rolePresets.Add(new() { equipment = originalEquipment }); rolePresets.Add(new() { equipment = otherEquipment });
        world.EntityManager.PutBuffer(player, rolePresets); world.EntityManager.Put(player, new ActiveEquipmentPresetCD { Value = 1 });
        string roleChanged = AIInventoryMenu.Stale(world, player, oldUnequip);
        Check(roleChanged != null && roleChanged.Contains("위치 역할") && roleChanged.Contains("head"), "old worn-item token is stale when selected head role moves from slot forty to sixty");
        var afterRoleChange = Menu("selected role changed, recovered item parent");
        Check(afterRoleChange.Exists(c => c.inventoryMenu.action == "item:60") && !afterRoleChange.Exists(c => c.inventoryMenu.action == "item:40") && AIInventoryMenu.PromptLine().Contains("위치 역할"), "role change reports why and returns to actual supported item choices instead of silent back-cancel only");
        Check(AICraft.Requests.Count == 0, "role recovery creates no physical request");

        Setup(); bag[12] = Item(ObjectID.CopperSword); Enter("무기", 12); var validBagHold = Menu("bag item before unrelated preset switch").Find(c => c.inventoryMenu.action == "operation:hold");
        originalEquipment = world.EntityManager.GetComponentData<EquipmentCD>(player); otherEquipment = originalEquipment; otherEquipment.helmSlotIndex = 60;
        rolePresets = new DynamicBuffer<EquipmentPresetsBuffer>(); rolePresets.Add(new() { equipment = originalEquipment }); rolePresets.Add(new() { equipment = otherEquipment });
        world.EntityManager.PutBuffer(player, rolePresets); world.EntityManager.Put(player, new ActiveEquipmentPresetCD { Value = 1 });
        Check(AIInventoryMenu.Stale(world, player, validBagHold) == null && Menu().Exists(c => c.inventoryMenu.action == "operation:hold"), "unrelated head preset change preserves selected main-bag weapon menu");

        foreach (string stage in new[] { "categories", "items", "operations", "destinations", "confirm", "quantity", "number", "chests", "access", "recipients" })
        {
            Setup(); bag[0] = Item(ObjectID.Wood, 30); bag[12] = Item(ObjectID.CopperSword); (chest, box) = Chest();
            Entity receiver = new() { Index = 3 }; world.EntityManager.Put(receiver, new LocalTransform { Position = new(0.5f, 0, 0) }); var rb = new DynamicBuffer<ContainedObjectsBuffer>(); rb.Add(default); world.EntityManager.PutBuffer(receiver, rb); AITeammates.Seen.Add(new() { entity = receiver, name = "Friend" });
            Choose("enter");
            if (stage != "categories") Choose("category:" + (stage == "destinations" || stage == "confirm" ? "무기" : "재료·기타"));
            if (stage != "categories" && stage != "items") Choose("item:" + (stage == "destinations" || stage == "confirm" ? 12 : 0));
            if (stage == "destinations" || stage == "confirm") Choose("operation:hold");
            if (stage == "confirm") Choose("destination:1:0");
            if (stage == "quantity" || stage == "number") Choose("operation:drop");
            if (stage == "number") Choose("custom-quantity");
            if (stage == "chests" || stage == "access") Choose("operation:store");
            if (stage == "access") Choose("chest:1:0");
            if (stage == "recipients") Choose("operation:give");
            CancelHere(stage);
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "payloads.json"), JsonSerializer.Serialize(payloads, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"TREE MENU CHECKS {checks}, FAILED {failed}; controlled selection count {selections}; actual menu/inventory source, fake world/queue and manually supplied outcomes; no model or game consumer");
        return failed;
    }
}
