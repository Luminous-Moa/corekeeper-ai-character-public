using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// One fixture is read here and by the JS bridge. Parsing is not world validation.
public static class GoalContractTests
{
    public static void Run()
    {
        var kinds = AIGoalKinds.All;
        Program.Check(kinds.Count == Enum.GetValues(typeof(GoalKind)).Length
            && kinds.Select(x => x.Kind).Distinct().Count() == kinds.Count
            && kinds.All(x => Enum.IsDefined(typeof(GoalKind), x.Kind)), "every internal goal kind is registered once");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "Tools", "ai-bridge", "test", "fixtures", "goal-contract.json")));
        var root = doc.RootElement;
        var wire = root.GetProperty("wireNames").EnumerateArray().Select(x => x.GetString()).ToArray();
        Program.Check(kinds.Where(x => x.WireName != null).Select(x => x.WireName).SequenceEqual(wire), "C# external goal vocabulary matches the shared contract");
        Program.Check(AIGoalKinds.Of(GoalKind.None).WireName == null && AIGoalKinds.Of(GoalKind.Free).WireName == null
            && !AIGoalKinds.TryWire("free", out _) && !AIGoalKinds.TryWire("Gather", out _), "internal kinds and case changes are not wire goals");
        foreach (var row in root.GetProperty("hostWords").EnumerateObject())
        {
            var kind = (GoalKind)Enum.Parse(typeof(GoalKind), row.Name);
            var words = row.Value.EnumerateArray().Select(x => x.GetString()).ToArray();
            Program.Check(AIGoalKinds.Of(kind).HostWords.SequenceEqual(words)
                && words.All(w => AIGoalKinds.IsHostWord(kind, w)), "host aliases stay compatible: " + kind);
        }
        Program.Check(!AIGoalKinds.IsHostWord(GoalKind.Gather, "gather")
            && !AIGoalKinds.IsHostWord(GoalKind.None, "none"), "gather item fallback and no-goal commands stay separate");
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            if (c.GetProperty("bridgeError").ValueKind != JsonValueKind.Null) continue;
            var goal = c.GetProperty("goal");
            var answer = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"set\",\"goal\":" + goal.GetRawText() + "}}").Answer;
            string d = goal.GetProperty("do").GetString();
            bool known = AIGoalKinds.TryWire(d, out var kind);
            int count = goal.TryGetProperty("count", out var countJ) && countJ.ValueKind != JsonValueKind.Null ? countJ.GetInt32() : -1;
            string item = goal.TryGetProperty("item", out var itemJ) ? itemJ.ToString() : null;
            string where = goal.TryGetProperty("where", out var whereJ) ? whereJ.ToString() : null;
            Program.Check(known && answer != null && answer.HasGoal && answer.GoalDo == d
                && answer.GoalCount == count && answer.GoalItem == item && answer.GoalWhere == where,
                c.GetProperty("name").GetString() + ": the mod reader retains the bridge goal without range normalization");
            if (c.TryGetProperty("max", out var max))
                Program.Check(AIGoalKinds.Of(kind).PlannerMaxCount == max.GetInt32(), c.GetProperty("name").GetString() + ": planner bound comes from the contract");
        }
        foreach (var c in root.GetProperty("experience").EnumerateArray())
        {
            var kind = (GoalKind)c.GetProperty("kind").GetInt32();
            Program.Check(AIGoalKinds.ExperienceKey(kind, c.GetProperty("item").GetString(), c.GetProperty("hasItem").GetBoolean())
                == c.GetProperty("key").GetString(), "experience key compatibility: " + c.GetProperty("key").GetString());
        }
        // Preserve distinct boundary semantics in the production callers, not a new test-only validator.
        string planner = File.ReadAllText(Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIPlanner.cs"));
        string form = planner.Substring(planner.IndexOf("private static string StepForm"));
        form = form.Substring(0, form.IndexOf("// One goal against the world now"));
        string place = form.Substring(form.IndexOf("if (d == AIGoalKinds.Of(GoalKind.Place).WireName)"));
        Program.Check(place.Contains("s.Count > AIGoalKinds.Of(GoalKind.Place).PlannerMaxCount") && !place.Contains("s.Count <"),
            "later place step keeps the upper bound only; world-start validation stays separate");
        Program.Check(form.Contains("s.Count < 1 || s.Count > hi") && planner.Contains("AIPlaceGoal.CheckPlace(server, player, item, count, where)"),
            "inventory step lower bound and actual place-start validation are preserved");
    }
}
