// Offline checks of the mod's Unity-free core (refactor plan 2-계획.md). Skeleton from S0; each later step adds its
// own group (S1 PlanProtocolTests, S2 OutcomeTests, S4 InboxTests, S2b StallTests, S6b AssemblerReplayTests, B1 ReportEscTests,
// C2 CombatS2Tests, E2 EdgeTests, G3 GuardTests, CW ContinueWarningTests).
// dotnet run -c Release --project Tools/core-tests [-- <group>]
using System;
using System.Collections.Generic;

public static class Program
{
    static int _fail, _ok;
    internal static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {what}");
        if (ok) _ok++; else _fail++;
    }

    public static int Main(string[] args)
    {
        string only = args.Length > 0 ? args[0] : null;
        var groups = new List<(string name, Action run)>
        {
            ("S0", S0Tests.Run),
            ("S1", PlanProtocolTests.Run),
            ("GATHER-LIMIT", ExplicitGatherLimitTests.Run),
            ("R1", GoalContractTests.Run),
            ("INV-GOAL", InventoryGoalTests.Run),
            ("INV-FLOW", InventoryFlowTests.Run),
            ("INV-HAND", HeldChoiceTests.Run),
            ("LIFE", LifeTreeTests.Run),
            ("MAINT", MaintenanceRulesTests.Run),
            ("AR", ApproachSearchTests.Run),
            ("TUNNEL-JOB", TunnelSearchJobTests.Run),
            ("TUNNEL-SHARED", SharedTunnelSearchTests.Run),
            ("TH", ThreatHistoryTests.Run),
            ("S2", OutcomeTests.Run),
            ("S4", InboxTests.Run),
            ("S2b", StallTests.Run),
            ("S6b", AssemblerReplayTests.Run),
            ("B1", ReportEscTests.Run),
            ("C2", CombatS2Tests.Run),
            ("P1", PlaceTests.Run),
            ("E2", EdgeTests.Run),
            ("G3", GuardTests.Run),
            ("NL", NlChatTests.Run),
            ("G3f", FixMergeReviewTests.Run),
            ("G3r", RemeasureTests.Run),
            ("G3rr", DoorReachReviewTests.Run),
            ("G3f3", FixRound3Tests.Run),
            ("G3m", MergeFixTests.Run),
            ("PG", PlaceGoalTests.Run),
            ("G3d", DoorSettleTests.Run),
            ("G3md", MyDoorTests.Run),
            ("FADE", ChatFadeTests.Run),
            ("CW", ContinueWarningTests.Run),
            ("CHOICE", ChoiceFactsTests.Run),
            ("UI", UiPointerTests.Run),
            ("UI-LAYOUT", UiLayoutTests.Run),
            ("CHAT-SCREEN", ChatScreenTests.Run),
        };
        foreach (var g in groups)
        {
            if (only != null && only != g.name) continue;
            Console.WriteLine($"== {g.name}");
            g.run();
        }
        Console.WriteLine(_fail == 0 ? $"ALL CHECKS OK ({_ok})" : $"FAILED {_fail} of {_ok + _fail}");
        return _fail;
    }
}

// S0: the project links Core/ and AIExpCore.cs, and ExpJson behaves as the plan's S1 assumes (null, escapes, nesting).
public static class S0Tests
{
    public static void Run()
    {
        Program.Check(typeof(AIPlanProtocol) != null, "Core/ is linked (AIPlanProtocol)");
        var d = ExpJson.Parse("{\"ok\":true,\"error\":null,\"answer\":{\"say\":\"그는 \\\"이동\\\"이라고\n말함\",\"memo\":{\"why\":\"x\"},\"why\":\"y\"}}") as Dictionary<string, object>;
        Program.Check(d != null && d.ContainsKey("error") && d["error"] == null, "ExpJson: JSON null is a present key with a null value");
        var a = ExpJson.GetO(d, "answer");
        Program.Check(ExpJson.GetS(a, "say") == "그는 \"이동\"이라고\n말함", "ExpJson: escaped quote and newline are decoded");
        Program.Check(ExpJson.GetS(a, "why") == "y", "ExpJson: a nested key of the same name does not shadow the top-level one");
        Program.Check(ExpJson.Parse("{\"a\":1") == null && ExpJson.Parse("") == null && ExpJson.Parse("<html>") == null, "ExpJson: cut, empty and non-JSON bodies give null");
        Program.Check(ExpJson.Parse("{\"n\":3}") is Dictionary<string, object> n && n["n"] is double, "ExpJson: numbers are double");
    }
}
