using System;
using System.IO;
using System.Text.Json;

public static class MaintenanceRulesTests
{
    public static void Run()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "Tools", "ai-bridge", "test", "fixtures", "maintenance-contract.json")));
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            string raw = row.GetProperty("goal").GetRawText();
            var answer = AIPlanProtocol.Read("{\"answer\":{\"goal\":" + raw + ",\"plan\":[" + raw + "]}}").Answer;
            var step = answer.Plan[0];
            Program.Check(step.Mode == answer.GoalMode && step.Item == answer.GoalItem && step.Slot == answer.GoalSlot && step.Count == answer.GoalCount && step.Where == AILifeGoalForm.Text(answer.GoalWhere),
                "maintain goal/plan parser preserves selectors: " + row.GetProperty("name").GetString());
            bool valid = AILifeGoalForm.Check(step) == null;
            Program.Check(valid == row.GetProperty("valid").GetBoolean(), "maintain production form: " + row.GetProperty("name").GetString());
            if (valid) Program.Check(AILifeGoalForm.CountFor(step.Do, step.Count) == 1, "maintain omission or explicit one normalizes to one request");
        }
        Program.Check((int)GoalKind.Give == 17 && (int)GoalKind.Maintenance == 18 && (int)GoalFeature.Inventory == 5 && (int)GoalFeature.Maintenance == 6, "maintenance appends goal/feature enum without changing existing numbers");
        Program.Check(AIGoalKinds.TryWire("maintain", out var kind) && kind == GoalKind.Maintenance && AIGoalKinds.Of(kind).PlannerMaxCount == 1 && AIGoalKinds.Of(kind).ItemMode == GoalItemMode.Optional && AIGoalKinds.Of(kind).Feature == GoalFeature.Maintenance, "maintain vocabulary has optional item and single request feature");
        var cursor = new LifeCursor("a", 1);
        cursor.Apply(cursor.Token("open"), "a", 1);
        Program.Check(cursor.Apply(cursor.Token("branch", "maintenance"), "a", 1) == null && cursor.Branch == "maintenance", "maintenance uses existing navigation token path");
        cursor.Apply(cursor.Token("mode", "repair"), "a", 1); cursor.Apply(cursor.Token("slot", "12"), "a", 1); cursor.Apply(cursor.Token("where", "station:SalvageAndRepairStation@1,2"), "a", 1);
        cursor.Apply(cursor.Token("back"), "a", 1);
        Program.Check(cursor.Where == "" && cursor.Slot == 12 && cursor.Mode == "repair", "maintenance back removes facility before exact equipment slot");
        Program.Check(AIMaintenanceRules.RepairConfirmed(true, 5, 100, 100) == 1 && AIMaintenanceRules.RepairConfirmed(true, 100, 100, 100) == 0 && AIMaintenanceRules.RepairConfirmed(false, 5, 100, 100) == 0 && AIMaintenanceRules.RepairConfirmed(true, 5, 99, 100) == 0, "repair only confirms original damaged identity becoming exact expected durability");
        Program.Check(AIMaintenanceRules.UpgradeConfirmed(true, 7, 7, true) == 1 && AIMaintenanceRules.UpgradeConfirmed(true, 7, 7, false) == 0 && AIMaintenanceRules.UpgradeConfirmed(true, 8, 7, true) == 0 && AIMaintenanceRules.UpgradeConfirmed(false, 7, 7, true) == 0, "upgrade requires exact next level and actual main receipt");
        for (int bits = 0; bits < 16; bits++)
        {
            bool newer = (bits & 1) != 0, applied = (bits & 2) != 0, queued = (bits & 4) != 0, observed = (bits & 8) != 0;
            Program.Check((AIMaintenanceRules.RecoveryReady(newer, applied, queued, observed) == null) == (bits == 11), "recovery requires all tick, applied, queue drain and exact pending observations: " + bits);
        }
        Program.Check(AIMaintenanceRules.RecoveryCompletesUpgrade(true, true, 3, 3) && !AIMaintenanceRules.RecoveryCompletesUpgrade(false, true, 3, 3) && !AIMaintenanceRules.RecoveryCompletesUpgrade(true, false, 3, 3) && !AIMaintenanceRules.RecoveryCompletesUpgrade(true, true, 3, 4), "only already-upgraded pending with the same original goal reference/revision completes upgrade");
    }
}
