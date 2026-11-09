// Run: powershell -File Tools/inventory-menu-tests/run.ps1
// Actual AIInventoryMenu + AIInventory over the life-inventory fake world (no game, no model).
using Unity.Entities;
using Unity.Transforms;
static class Program
{
    static int count, failed;
    static void Check(bool ok, string message) { Console.WriteLine((ok ? "PASS " : "FAIL ") + message); count++; if (!ok) failed++; }
    static ContainedObjectsBuffer Item(ObjectID id, int state = 100) => new() { objectData = new() { objectID = id, amount = state } };
    static (World, Entity, DynamicBuffer<ContainedObjectsBuffer>) Setup()
    {
        AIAgent.Cur = new(); AIInventory.Reset(); AIInventoryMenu.Reset(); AIBase.Memories.Clear(); AITeammates.Seen.Clear();
        var w = new World(); var p = new Entity { Index = 1 };
        var b = new DynamicBuffer<ContainedObjectsBuffer>(); for (int i = 0; i < 70; i++) b.Add(default); w.EntityManager.PutBuffer(p, b);
        var iv = new DynamicBuffer<InventoryBuffer>();
        iv.Add(new() { startIndex = 0, size = 40, sizeX = 10, sizeY = 4, maxSize = 40, extraInventorySizeSlot = -1 });
        iv.Add(new() { startIndex = 40, size = 30, sizeX = 1, sizeY = 30, maxSize = 30, extraInventorySizeSlot = -1 });
        w.EntityManager.PutBuffer(p, iv); w.EntityManager.PutBuffer(p, new DynamicBuffer<UIActionBuffer>()); w.EntityManager.Put(p, new LocalTransform());
        w.EntityManager.Put(p, new EquipmentCD { helmSlotIndex = 40, breastSlotIndex = 41, pantsSlotIndex = 42, necklaceSlotIndex = 43, ring1SlotIndex = 44, ring2SlotIndex = 45, offHandIndex = 46, bagIndex = 47, lanternIndex = 48, pouch1Index = 49 });
        return (w, p, b);
    }
    static AICandidate Find(List<AICandidate> list, string action) => list.Find(c => c.inventoryMenu != null && c.inventoryMenu.action == action);
    static int Main()
    {
        var (w, p, b) = Setup();
        b[12] = Item(ObjectID.CopperBreastArmor); b[3] = Item(ObjectID.MiningPick);
        var goal = new AIGoal { kind = GoalKind.Give, item = ObjectID.CopperBreastArmor, count = 1, where = "Luminous" };
        AIGoals.Goal = goal; AIInventory.GoalSet(w, p, goal);
        var o = new AIObservation { pos = AIBody.Pos(w, p) };
        var enter = AIInventoryMenu.Candidates(w, p, o, "");
        AIInventoryMenu.ApplyNavigation(w, p, enter[0], out _);
        var categories = AIInventoryMenu.Candidates(w, p, o, "");
        var wear = categories.Find(c => c.desc.StartsWith("분류 착용 장비")); var tools = categories.Find(c => c.desc.StartsWith("분류 도구"));
        Check(wear != null && wear.desc.Contains("목표 품목 1칸") && tools != null && !tools.desc.Contains("목표 품목"),
            "category choice marks the class holding the goal item, other classes unmarked");
        Check(categories.FindAll(c => c.desc.StartsWith("분류 ")).Count == 2, "marking keeps every category choice (none removed or merged)");
        AIInventoryMenu.ApplyNavigation(w, p, tools, out _);
        string relation = AIInventoryMenu.GoalRelation(w, p, goal);
        Check(relation.Contains("가방 12('착용 장비')") && relation.Contains("'도구'에 없음")
            && relation.Contains("수령자 Luminous(안 보임) 수령 0/1") && relation.Contains("보이는 동료에게 전달"),
            "rejected-completion facts name the item's slot and class, the open class lacking it, the recipient and receipt");
        AIInventoryMenu.ApplyNavigation(w, p, Find(AIInventoryMenu.Candidates(w, p, o, ""), "back"), out _);
        AIInventoryMenu.ApplyNavigation(w, p, AIInventoryMenu.Candidates(w, p, o, "").Find(c => c.desc.StartsWith("분류 착용 장비")), out _);
        var items = AIInventoryMenu.Candidates(w, p, o, "");
        Check(items.Exists(c => c.desc.Contains("가방 12") && c.desc.Contains("목표 품목")), "item choice marks the goal item");
        Check(AIInventoryMenu.GoalRelation(w, p, goal).Contains("'착용 장비'에 있음"), "relation reflects the class now open");
        AIGoals.Goal = new AIGoal { kind = GoalKind.Other };
        AIInventoryMenu.Reset(); AIInventoryMenu.ApplyNavigation(w, p, AIInventoryMenu.Candidates(w, p, o, "")[0], out _);
        Check(!AIInventoryMenu.Candidates(w, p, o, "").Exists(c => c.desc.Contains("목표 품목")), "no inventory goal: no marks");
        Console.WriteLine($"INVENTORY MENU CHECKS {count}, FAILED {failed}");
        return failed;
    }
}
