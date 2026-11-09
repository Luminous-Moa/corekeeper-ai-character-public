using System;
using System.IO;
using System.Text.Json;

public static class LifeTreeTests
{
    private sealed class Result : ILifeResult
    { public int ConfirmedWork { get; set; } public int ConfirmedReceived { get; set; } public int RequestedWork { get; set; } public string ResultStage => "work"; }
    public static void Run()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "Tools", "ai-bridge", "test", "fixtures", "life-contract.json")));
        foreach (var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            string json = row.GetProperty("goal").GetRawText();
            var a = AIPlanProtocol.Read("{\"answer\":{\"goal\":" + json + ",\"plan\":[" + json + "]}}").Answer;
            var s = a.Plan[0];
            Program.Check(s.Do == a.GoalDo && s.Item == a.GoalItem && s.Count == a.GoalCount && s.Where == AILifeGoalForm.Text(a.GoalWhere) && s.Mode == a.GoalMode && s.Second == a.GoalSecond,
                row.GetProperty("name").GetString() + ": goal and plan selectors retain identical input");
            Program.Check((AILifeGoalForm.Check(s) == null) == row.GetProperty("coreValid").GetBoolean(), row.GetProperty("name").GetString() + ": production life form");
        }
        var old = new PlanStep("craft", "Wood", 2);
        Program.Check(old.Mode == null && old.Second == null && old.Text == "craft Wood 2", "optional selector arguments preserve old callers and text");
        Program.Check(new PlanStep("farm", null, 12, "farm", mode: "harvest").Text.Contains("count=12"), "plan display preserves quantity when item filter is unspecified");
        Program.Check(AILifeGoalForm.CountFor("farm", -1) == 0 && AILifeGoalForm.CountFor("cook", -1) == 0 && AILifeGoalForm.CountFor("craft", 2) == 2, "only unspecified farm/cook count becomes maintenance zero");
        foreach (string invalid in new[] { "-1", "0", "2147483648", "2.5", "\"6\"", "true", "{}" })
        {
            var step = AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"farm\",\"mode\":\"plant\",\"count\":" + invalid + "}]}}").Answer.Plan[0];
            Program.Check(AILifeGoalForm.Check(step) != null, "production parser rejects explicit invalid life count: " + invalid);
        }
        var invalidSecond = AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"cook\",\"mode\":\"put\",\"second\":{}}]}}").Answer.Plan[0];
        Program.Check(AILifeGoalForm.Check(invalidSecond) != null, "invalid selector type is not silently treated as omission");
        Program.Check(AILifeGoalForm.TryFarmBounds("rect:4,8,-2,3", out int x1, out int y1, out int x2, out int y2) && x1 == -2 && y1 == 3 && x2 == 4 && y2 == 8,
            "rect endpoints normalize without enumerating its area");
        Program.Check(AILifeGoalForm.InFarmWhere("tile:-1,2", -1, 2) && !AILifeGoalForm.InFarmWhere("tile:-1,2", -1, 3), "tile where filters exactly one observed tile");
        Program.Check(!AILifeGoalForm.TryFarmBounds("rect:0,0,2147483648,1", out _, out _, out _, out _) && !AILifeGoalForm.TryFarmBounds("tile:1.2,3", out _, out _, out _, out _), "coordinates require int range and integer syntax");
        var c = new LifeCursor("a", 3);
        var open = c.Token("open"); Program.Check(c.Apply(open, "a", 3) == null && c.Open && c.Revision == 1, "open changes only cursor revision");
        Program.Check(c.Apply(open, "a", 3) != null && c.Revision == 1, "late navigation leaves cursor unchanged");
        Program.Check(c.Apply(c.Token("branch", "farm"), "b", 3) != null && c.Branch == "", "different character token rejected");
        Program.Check(c.Apply(c.Token("branch", "farm"), "a", 4) != null && c.Branch == "", "different goal revision rejected");
        Program.Check(c.Apply(c.Token("branch", "trade"), "a", 3) != null && c.Branch == "", "unsupported branch rejected");
        c.Apply(c.Token("branch", "farm"), "a", 3); c.Apply(c.Token("mode", "plant"), "a", 3); c.Apply(c.Token("item", "A"), "a", 3); c.Apply(c.Token("where", "farm"), "a", 3);
        int rev = c.Revision;
        Program.Check(c.Apply(c.Token("page", "2"), "a", 3) == null && c.Page == 2 && c.Revision == rev + 1, "page has its own revision");
        c.Apply(c.Token("back"), "a", 3);
        Program.Check(c.Where == "" && c.Item == "A" && c.Page == 0, "back removes only last chosen selection and page");
        c.Apply(c.Token("root"), "a", 3);
        Program.Check(c.Open && c.Branch == "" && c.Mode == "" && c.Item == "", "root clears selected values and remains open");
        Program.Check(c.Apply(c.Token("back"), "a", 3) != null && c.Apply(c.Token("page", "-1"), "a", 3) != null && c.Apply(c.Token("dance"), "a", 3) != null, "invalid back/page/op leave state unchanged");
        Program.Check(c.Apply(c.Token("item", "missing"), "a", 3, (op, value) => false) != null, "value not offered by current list is rejected");
        c.Apply(c.Token("branch", "inventory"), "a", 3); c.Apply(c.Token("where", "chest:1,2"), "a", 3); c.Apply(c.Token("item", "Wood"), "a", 3); c.Apply(c.Token("slot", "13"), "a", 3); c.Apply(c.Token("back"), "a", 3);
        Program.Check(c.Slot == -1 && c.Item == "Wood" && c.Where == "chest:1,2", "storage reverse order removes slot before item before chest");
        c.Apply(c.Token("close"), "a", 3);
        Program.Check(!c.Open && c.Branch == "" && c.Where == "" && c.Page == 0, "close clears cursor without any execution operation");
        c.Reset("b", 4); Program.Check(c.Owner == "b" && c.GoalRevision == 4 && !c.Open, "owner/goal reset cannot reuse old cursor");
        var g = new object(); var progress = new LifeProgress(); progress.Reset(g, 3);
        var selection = new LifeSelection { Branch = "farm", Mode = "plant", Item = "A", Where = "rect:0,0,2,2", RequestedCount = 6, OfferedCount = 4, Goal = g, GoalRevision = 3, MatchesGoal = true };
        var result = new Result { RequestedWork = 4, ConfirmedWork = 4 };
        Program.Check(progress.Record(1, selection, result) && progress.Work(selection) == 4 && progress.Remaining(selection, 6) == 2, "four confirmed tiles of six leave two; offered amount is not assumed success");
        Program.Check(!progress.Record(1, selection, result) && progress.Work(selection) == 4, "same run cannot count twice");
        Program.Check(progress.Remaining(selection, 12) == 8, "six-tile execution batch does not cap twelve-tile request");
        var other = selection.Copy(); other.Item = "B"; other.MatchesGoal = false; other.Goal = null;
        Program.Check(!progress.Record(2, other, result) && progress.Work(selection) == 4, "separate action does not advance original goal");
        var take = selection.Copy(); take.Branch = "cook"; take.Mode = "take";
        progress.Record(3, take, new Result { ConfirmedWork = 4, ConfirmedReceived = 2 });
        Program.Check(progress.Remaining(take, 6) == 4, "cook take measures main-inventory receipt instead of work");
        var craft = take.Copy(); craft.Branch = "craft";
        progress.Record(4, craft, new Result { ConfirmedWork = 2, ConfirmedReceived = 4 });
        Program.Check(progress.Remaining(craft, 6) == 2, "craft count is produced items rather than number of crafts");
        progress.Reset(new object(), 4); Program.Check(!progress.Record(5, selection, result) && progress.Work(selection) == 0, "new goal clears counters and refuses old run binding");
    }
}
