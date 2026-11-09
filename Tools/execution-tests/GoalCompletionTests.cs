using System;
using System.Collections.Generic;

public static class GoalCompletionTests
{
#if !BASELINE
    public static void Run(Action<bool, string> check)
    {
        var world = Harness.World;
        var player = Harness.Player;
        Harness.Reset();
        AIPlanner.Orders.Add(new HostOrder { Id = 20, Text = "장비를 만들어 전달해줘", State = "따름", Source = "completion-plan" }, null);
        AIPlanner.Cur.Plan.Set("completion-plan", new List<PlanStep>
        {
            new PlanStep("gather", "CopperOre", 5),
            new PlanStep("craft", "CopperBreastArmor", 1)
        });
        AIGoals.SetGoal(world, player, GoalKind.Gather, ObjectID.CopperOre, "구리", 5, "completion-plan");
        var goal = AIGoals.Goal;
        var selected = new ProbeSkill("mine:1,2");
        AIBody.StartSkill(world, player, selected, 0);
        int revision = AIBrain.Revision;
        AIBrain.ApplyChosen(world, player, 1, "finish_goal");
        AIPlanner.TickNextStep(world, player, 2);
        check(ReferenceEquals(AIGoals.Goal, goal) && AIBrain.Revision == revision && AIPlanner.Cur.Plan.Index == 0,
            "production finish of observed 0/5 keeps the same goal and plan step");
        check(AIBody.Current == selected && selected.Status == SkillStatus.Running && AIBody.Stops == 0,
            "unconfirmed finish does not stop the current selected body");
        check(AIPlanner.Orders.Latest.Open && AIPlanner.EndReports == 0 && AIBrain._needCall,
            "unconfirmed finish preserves original instruction and schedules local judgment");
        check(AIGoals.Observation.Contains("0/5") && AIBrain.RetainedExecutionReason.Contains("0/5"),
            "unconfirmed finish exposes actual shortfall to the next judgment");
        AIBody.Bag[ObjectID.CopperOre] = 3;
        AIGoals.CheckDone(world, player, 1.6f);
        check(AIGoals.Observation.Contains("3/5") && !AIGoals.Observation.Contains("0/5"),
            "unfinished completion review refreshes its quantity when observation changes");
        AIBody.Bag[ObjectID.CopperOre] = 5;
        AIBrain.ApplyChosen(world, player, 2.1f, "finish_goal");
        AIPlanner.TickNextStep(world, player, 2.2f);
        check(AIGoals.Goal.kind == GoalKind.Craft && AIPlanner.Cur.Plan.Index == 1 && AIPlanner.Orders.Latest.Open,
            "current acquisition permits explicit completion and next plan step while request remains open");
        check(!AIGoals.Observation.Contains("0/5"), "next goal clears rejected completion facts");

        Harness.Reset();
        AIGoals.SetGoal(world, player, GoalKind.Craft, ObjectID.CopperBreastArmor, "구리 흉갑", 1, "craft-source");
        AIBody.Bag[ObjectID.CopperBreastArmor] = 1;
        AIGoals.CheckDone(world, player, 1);
        check(AIGoals.CompletionObserved, "craft initially observes its current 1/1 output");
        AIBody.Bag[ObjectID.CopperBreastArmor] = 0;
        AIBrain.ApplyChosen(world, player, 1.1f, "finish_goal");
        check(AIGoals.Goal.kind == GoalKind.Craft && !AIGoals.CompletionObserved && AIGoals.Observation.Contains("0/1"),
            "finish rereads lost craft output inside the half-second observation throttle");

        Harness.Reset();
        AIGoals.SetGoal(world, player, GoalKind.Give, ObjectID.CopperBreastArmor, "구리 흉갑", 1, "give-source");
        selected = new ProbeSkill("inventory:give");
        AIBody.StartSkill(world, player, selected, 0);
        AIGoalFeatures.Guidance = "목표와 가방 메뉴의 관계: 현재 분류 '도구'에는 이 품목 없음";
        AIBrain.ApplyChosen(world, player, 1, "finish_goal");
        check(AIGoals.Goal.kind == GoalKind.Give && AIBody.Current == selected && AIPlanner.EndReports == 0,
            "give without an observed receipt stays on the same step after finish choice");
        check(AIGoals.Observation.Contains("'도구'에는 이 품목 없음") && AIBrain.RetainedExecutionReason.Contains("'도구'에는 이 품목 없음"),
            "rejected give completion carries the item/menu relation into the next judgment");
        check(AIBrain.RetainedExecutionReason.Contains("(거절 시점)"), "the execution observation labels the relation as of the rejection");
        AIGoalFeatures.Guidance = "목표와 가방 메뉴의 관계: 현재 분류 '착용 장비'에 이 품목 있음";
        AIGoals.CheckDone(world, player, 1.6f);
        check(AIGoals.Observation.Contains("'착용 장비'에 이 품목 있음") && !AIGoals.Observation.Contains("'도구'에는 이 품목 없음"),
            "검수 F3: after the menu moves, the goal observation shows the current relation, not the rejection-time one");
        AIGoalFeatures.Guidance = "";
        AIInventory.Observed = true;
        AIBrain.ApplyChosen(world, player, 1.1f, "finish_goal");
        check(!AIGoals.Goal.IsReal && AIBody.Current == null && AIPlanner.EndReports == 1,
            "give with current receipt completes on explicit choice without another delivery action");

        Harness.Reset();
        AIGoals.SetGoal(world, player, GoalKind.Home, ObjectID.None, "", 1, "home-source");
        AIGoalFeatures.Done = true;
        AIGoals.CheckDone(world, player, 1);
        AIGoalFeatures.Done = false;
        AIBrain.ApplyChosen(world, player, 1.1f, "finish_goal");
        check(AIGoals.Goal.kind == GoalKind.Home && !AIGoals.CompletionObserved,
            "feature completion rereads current arrival instead of relying on cached success");
        AIBrain.ApplyChosen(world, player, 2, "request_plan");
        check(AIPlanner.Replans == 1 && AIGoals.Goal.kind == GoalKind.Home,
            "failed completion leaves the existing explicit replan control usable");

        Harness.Reset();
        AIGoals.SetGoal(world, player, GoalKind.Craft, ObjectID.CopperBreastArmor, "구리 흉갑", 1, "lost-output");
        AIBody.Bag[ObjectID.CopperBreastArmor] = 1;
        AIGoals.CheckDone(world, player, 1);
        int completeRevision = AIBrain.RetainedExecutionRevision;
        AIBrain.AcknowledgeObservationForTest(completeRevision);
        AIBrain.SetObservationTransportForTest(true, false);
        AIBody.Bag[ObjectID.CopperBreastArmor] = 0;
        AIGoals.CheckDone(world, player, 1.6f);
        check(!AIGoals.CompletionObserved && AIBrain._needCall && AIBrain.RetainedExecutionRevision == completeRevision + 1 &&
            AIBrain.ExecutionObservation.Contains("0/1") && AIBrain.ExecutionObservation.Contains("현재 목표"),
            "losing an observed completion while transport is pending publishes a new current-goal fact");
        AIBrain.AcknowledgeObservationForTest(completeRevision);
        check(AIBrain.HasUnaskedObservationForTest,
            "old request acknowledgement cannot consume the newer lost-completion observation");

        foreach (var kind in new[] { GoalKind.Explore, GoalKind.Follow })
        {
            Harness.Reset();
            AIGoals.SetGoal(world, player, kind, ObjectID.None, "", 1, "judged-end");
            AIBrain.ApplyChosen(world, player, 1, "finish_goal");
            check(!AIGoals.Goal.IsReal && AIPlanner.EndReports == 1,
                "model retains explicit endpoint choice for non-quantified " + kind);
        }
    }
#endif
}
