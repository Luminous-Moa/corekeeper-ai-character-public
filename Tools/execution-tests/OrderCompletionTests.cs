using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

public static class OrderCompletionTests
{
#if !BASELINE
    private static HostOrder Begin(bool hasNext = true, GoalKind kind = GoalKind.Gather)
    {
        Harness.Reset();
        var order = new HostOrder { Id = 1, Text = "장비 세트를 전달해줘", State = "따름", Source = "old-plan" };
        AIPlanner.Orders.Add(order, null);
        var steps = new List<PlanStep> { new PlanStep(kind.ToString().ToLowerInvariant(), "CopperOre", 5) };
        if (hasNext) steps.Add(new PlanStep("give", "CopperBreastArmor", 1));
        AIPlanner.Cur.Plan.Set("old-plan", steps);
        AIGoals.SetGoal(Harness.World, Harness.Player, kind, ObjectID.CopperOre, "구리", 5, "old-plan");
        AIBody.StartSkill(Harness.World, Harness.Player, new ProbeSkill("old-action"), 0);
        AIPlanner.OrderReportForTest(1);
        return order;
    }

    private static void Apply(PlanAnswerJ answer, int cut = 0, StaleAction act = StaleAction.Apply)
        => AIPlanner.ApplyOrderBoundaryForTest(Harness.World, Harness.Player, Time.realtimeSinceStartup,
            answer, cut, "지정 수령자가 현재 보이지 않아 새 전달 계획을 받지 않음", act);

    public static void Run(Action<bool, string> check)
    {
        var original = Begin();
        var unrelated = new HostOrder { Id = 2, Text = "다른 완료된 부탁", State = "따름", Source = "other-plan" };
        var latest = new HostOrder { Id = 3, Text = "가방에 있는 세트를 줘", State = "받음" };
        AIPlanner.Orders.Add(unrelated, null); AIPlanner.Orders.Add(latest, null);
        AIPlanner.OrderReportForTest(3);
        var goal = AIGoals.Goal; var body = AIBody.Current;
        var answer = new PlanAnswerJ { decision = "set", order = "follow", orders = new Dictionary<int, string> { { 1, "done" }, { 2, "done" } }, why = "장비 전달로 전환" };
        Apply(answer);
        check(original.Open && original.Source == "old-plan" && AIPlanner.Orders.Find(1) == original && latest.State == "받음",
            "refused replacement keeps the carried original order open while its new follow remains unapplied");
        check(unrelated.State == "끝남" && AIPlanner.Orders.Find(2) == null && answer.orders[1] == "done" && answer.order == "follow",
            "unrelated done is applied and the received answer remains unchanged");
        check(ReferenceEquals(AIGoals.Goal, goal) && AIBody.Current == body && AIPlanner.Cur.Plan.Source == "old-plan",
            "completion conflict changes neither the current goal nor body nor unfinished plan");
        check(AIPlanner.Replans == 1,
            "unfinished carried-order contradiction sends a correction even while an old plan runs");
        using (var json = JsonDocument.Parse(AIPlanner.LastRequestBody))
        {
            var report = json.RootElement.GetProperty("report");
            check(report.GetProperty("last_answer_rejected").GetString().Contains("완료 선언을 보류") &&
                report.GetProperty("host_orders_open")[0].GetProperty("id").GetInt32() == 1,
                "actual followup JSON retains rejection facts and the still-open original obligation");
        }
        Apply(answer); AIPlanner.CompleteTransport(); Apply(answer);
        AIPlanner.ApplyOrderBoundaryForTest(Harness.World, Harness.Player, 3, answer, 0, "다른 잘못된 새 목표도 실행할 수 없음");
        check(AIPlanner.Replans == 1 && !AIPlanner.Pending,
            "same incomplete obligation does not loop even when rejected replacement details change");

        original = Begin();
        AIPlanner.Orders.Add(new HostOrder { Id = 2, Text = "같은 계획의 다른 부탁", State = "따름", Source = "old-plan" }, null);
        AIPlanner.Orders.Add(new HostOrder { Id = 3, Text = "새 부탁", State = "받음" }, null);
        AIPlanner.OrderReportForTest(3);
        var firstClaim = new PlanAnswerJ { decision = "set", orders = new Dictionary<int, string> { { 1, "done" } } };
        var secondClaim = new PlanAnswerJ { decision = "set", orders = new Dictionary<int, string> { { 2, "done" } } };
        Apply(firstClaim); AIPlanner.CompleteTransport();
        Apply(secondClaim); AIPlanner.CompleteTransport();
        Apply(firstClaim);
        check(AIPlanner.Replans == 2 && !AIPlanner.Pending && AIPlanner.Orders.Find(1).Open && AIPlanner.Orders.Find(2).Open,
            "alternating carried-order claims A B A report each unchanged conflict only once in the goal scope");

        original = Begin();
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.Open && AIPlanner.Replans == 1,
            "latest order done receives the same carried-plan protection as earlier orders");
        original = Begin(); AIPlanner.OrderReportForTest(0);
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.Open && AIPlanner.Replans == 0,
            "an answer predating the latest order keeps the existing report-sequence guard");

        original = Begin();
        AIBody.Bag[ObjectID.CopperOre] = 5;
        AIGoals.CheckDone(Harness.World, Harness.Player, 1);
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.Open && AIGoals.CompletionObserved,
            "completed current step does not imply completion of a plan with remaining delivery");
        original = Begin(false, GoalKind.Craft);
        AIBody.Bag[ObjectID.CopperOre] = 5;
        AIGoals.CheckDone(Harness.World, Harness.Player, 1);
        AIBody.Bag[ObjectID.CopperOre] = 0;
        Time.realtimeSinceStartup = 1.1f;
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.Open && !AIGoals.CompletionObserved,
            "last-step completion is reobserved before deciding whether its done contradicts facts");
        original = Begin(false); AIBody.Bag[ObjectID.CopperOre] = 5;
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.State == "끝남" && AIPlanner.Replans == 0,
            "currently observed last-step completion retains upper done authority despite an unrelated rejected replacement");

        foreach (var state in new[] { "defer", "drop", "answered" })
        {
            original = Begin();
            Apply(new PlanAnswerJ { decision = "set", order = state });
            check(original.State == AIPlanRules.OrderState(state) && AIPlanner.Replans == 0,
                "refused replacement preserves explicit upper " + state + " authority");
        }
        original = Begin(); Apply(new PlanAnswerJ { decision = "set", order = "done" }, -1);
        check(original.State == "끝남", "completion protection is confined to a wholly refused replacement");
        original = Begin(false, GoalKind.Explore); Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.State == "끝남", "non-measured final goal retains upper completion judgment");
        original = Begin(); AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Gather, ObjectID.CopperOre, "구리", 5, "other-goal");
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.State == "끝남", "a goal outside the carried plan is not treated as its completion evidence");
        original = Begin(); AIGoals.ClearGoal(new GoalEnd("fixture goal cleared", "cancelled"));
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(original.State == "끝남", "a plan without a current owned goal does not extend the completion guard");

        original = Begin();
        AIPlanner.RequestReplan(Harness.World, Harness.Player, "existing request");
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(AIPlanner.Replans == 1 && AIPlanner.Pending && original.Open,
            "completion contradiction joins the existing in-flight report queue");
        AIPlanner.CompleteTransport(); AIPlanner.TickPending(Harness.World, Harness.Player, 2);
        check(AIPlanner.Replans == 2 && !AIPlanner.Pending,
            "queued contradiction dispatches once after the prior request finishes");
        original = Begin();
        AIPlanner.HoldOrderRetryForTest(20f); Time.realtimeSinceStartup = 10f;
        Apply(new PlanAnswerJ { decision = "set", order = "done" });
        check(AIPlanner.Pending && AIPlanner.Replans == 0 && original.Open,
            "completion correction respects an existing upper unavailable retry window");
        Time.realtimeSinceStartup = 20f; AIPlanner.TickPending(Harness.World, Harness.Player, 20f);
        check(AIPlanner.Replans == 1, "completion correction dispatches when the existing retry window permits");

        Harness.Reset();
        original = new HostOrder { Id = 1, Text = "세 부위를 각각 전달", State = "따름", Source = "three-gives" };
        AIPlanner.Orders.Add(original, null);
        AIPlanner.Cur.Plan.Set("three-gives", new List<PlanStep> {
            new PlanStep("give", "CopperBreastArmor", 1), new PlanStep("give", "Wood", 1), new PlanStep("give", "TinOre", 1) });
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Give, ObjectID.CopperBreastArmor, "흉갑", 1, "three-gives");
        for (int i = 0; i < 3; i++)
        {
            AIInventory.Observed = true;
            AIBrain.ApplyChosen(Harness.World, Harness.Player, i + 1, "finish_goal");
            AIPlanner.TickNextStep(Harness.World, Harness.Player, i + 1.1f);
        }
        check(!AIPlanner.Cur.Plan.Active && !AIGoals.Goal.IsReal && original.Open && AIPlanner.EndReports == 1,
            "three observed receipt steps reach plan_done while original completion still belongs to the upper model");
        AIPlanner.OrderReportForTest(1);
        Apply(new PlanAnswerJ { decision = "keep", order = "done" }, -1);
        check(original.State == "끝남", "normal upper done closes the request after all observed deliveries and plan_done");
    }
#endif
}

#if !BASELINE
public static partial class AIPlanner
{
    public static void OrderReportForTest(int sequence)
    {
        S._reqOrderSeq = sequence; S.HostOrderSeq = sequence; _reqTrigger = "host_said";
    }
    public static void HoldOrderRetryForTest(float until) { S._down = "fixture unavailable"; _retryAt = until; }
}
#endif
