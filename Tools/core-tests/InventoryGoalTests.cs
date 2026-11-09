using System;

public static class InventoryGoalTests
{
    public static void Run()
    {
        var answer = AIPlanProtocol.Read("{\"answer\":{\"decision\":\"set\",\"goal\":{\"do\":\"store\",\"item\":\"Wood\",\"keep\":10,\"slot\":12,\"where\":\"chest:2,3\"},\"plan\":[{\"do\":\"take\",\"item\":\"Wood\",\"count\":20,\"slot\":2},{\"do\":\"equip\",\"item\":\"CopperHelm\",\"where\":\"head\",\"slot\":12}]}}").Answer;
        Program.Check(answer.GoalCount == -1 && answer.GoalKeep == 10 && answer.GoalSlot == 12 && answer.GoalWhere == "chest:2,3", "store reserve, exact source and destination survive upper reply parsing without turning an absent count into one");
        Program.Check(answer.Plan.Count == 2 && answer.Plan[0].Count == 20 && answer.Plan[0].Slot == 2 && answer.Plan[1].Slot == 12 && answer.Plan[1].Where == "head", "inventory plan keeps each action's own slots and destination");
        var queue = new AIPlanQueue();
        queue.Set("test", answer.Plan);
        Program.Check(queue.Current.Slot == 2 && queue.Current.Keep == -1, "queued take keeps its original source and no invented reserve");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("store", "Wood", -1, "chest:2,3", 10)) == null, "reserve-only store is a valid future step");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("equip", "CopperHelm", 1, "head", -1, 12)) == null, "exact equipment selection is valid");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("unequip", null, -1, "head")) == null, "unequip can identify a gear slot without guessing an item");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("unequip", null, -1)) != null, "unequip cannot choose an unspecified item and slot");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("give", "Wood", 2)) != null && AIInventoryGoalForm.Check(new PlanStep("give", "Wood", 2, "Gemini")) == null, "delivery requires its recipient before world validation");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("take", "Wood", 0)) != null && AIInventoryGoalForm.Check(new PlanStep("equip", "CopperHelm", 2)) != null, "a zero transfer and multiple equipment action are rejected");
        Program.Check(AIInventoryGoalForm.Check(new PlanStep("hold", "CopperSword", 1, null, 3)) != null, "reserve is not silently repurposed as a hand-choice instruction");
        var legacy = AIPlanProtocol.Read("{\"answer\":{\"decision\":\"set\",\"goal\":{\"do\":\"store\"}}}").Answer;
        Program.Check(legacy.GoalKeep == -1 && legacy.GoalSlot == -1 && legacy.GoalCount == -1, "legacy generic store carries no new selection or count constraint");
        Program.Check(AIInventoryGoalForm.CountFor(GoalKind.Take, -1) == 1 && AIInventoryGoalForm.CountFor(GoalKind.Give, -1) == 1, "an omitted transfer count becomes one before both world validation and goal creation");
        Program.Check(AIInventoryGoalForm.CountFor(GoalKind.Store, -1) == 0 && AIInventoryGoalForm.CountFor(GoalKind.Take, 0) == 0, "an omitted store keeps all-item meaning while explicit invalid zero transfers are not repaired");
    }
}
