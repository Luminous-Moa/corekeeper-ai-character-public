using System;
using UnityEngine;

public static class ObservationReviewTests
{
#if !BASELINE
    private static ProbeSkill StartPending()
    {
        Harness.Reset();
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Give, ObjectID.CopperBreastArmor, "구리 흉갑", 1, "give-plan");
        var skill = new ProbeSkill("inventory:give") { Run = 17 };
        AIBody.StartSkill(Harness.World, Harness.Player, skill, 0);
        skill.BindObservationGoal();
        Time.realtimeSinceStartup = 1;
        skill.BeginObservationTick();
        skill.Observe("receipt:pending", "수령 사실 미확인");
        return skill;
    }

    private static void ObserveAndReview(ProbeSkill skill, float now)
    {
        Time.realtimeSinceStartup = now;
        AIBrain.ReviewPendingForTest(now);
        skill.BeginObservationTick();
        skill.Observe("receipt:pending", "수령 사실 미확인");
    }

    public static void Run(Action<bool, string> check)
    {
        var skill = StartPending();
        int firstRevision = AIBrain.RetainedExecutionRevision;
        check(AIBrain.ExecutionObservation.Contains("현재 목표") && AIBrain.ExecutionObservation.Contains("실행 17"),
            "actual first pending observation records the owning goal and body run");
        AIBrain.AcknowledgeObservationForTest(firstRevision);
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 1.1f, "continue", skill.Key);
        ObserveAndReview(skill, 5.9f);
        check(!AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision,
            "same pending condition remains quiet before the five-second review interval");
        ObserveAndReview(skill, 6f);
        check(AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision + 1 && AIBrain.ExecutionObservation.Contains("6.0초"),
            "same pending condition wakes local judgment after quiet continue without an external event");
        check(AIBody.Current == skill && skill.Status == SkillStatus.Running && skill.Facts.Count == 1,
            "review preserves selected execution and does not duplicate its historical fact list");
        AIBrain.AcknowledgeObservationForTest(AIBrain.RetainedExecutionRevision);
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 6.1f, "continue", skill.Key);
        ObserveAndReview(skill, 11f);
        check(AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision + 2,
            "a second continue remains eligible for another quiet review rather than sleeping indefinitely");

        skill = StartPending();
        firstRevision = AIBrain.RetainedExecutionRevision;
        AIBrain.AcknowledgeObservationForTest(firstRevision);
        AIBrain.SetObservationTransportForTest(true, false);
        ObserveAndReview(skill, 6f);
        check(!AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision,
            "unchanged observation does not enqueue a duplicate review while a request is in flight");
        Time.realtimeSinceStartup = 6.1f;
        skill.Observe("receipt:source-changed", "출발 슬롯 변화가 추가로 확인됨");
        check(AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision + 1,
            "a genuinely new fact still updates revision while the request is in flight");
        AIBrain.AcknowledgeObservationForTest(firstRevision);
        check(AIBrain.HasUnaskedObservationForTest,
            "acknowledging the sent revision leaves a newer in-flight fact unconsumed");
        AIBrain.SetObservationTransportForTest(false, false);
        AIBrain.AcknowledgeObservationForTest(AIBrain.RetainedExecutionRevision);
        AIBrain.SetObservationTransportForTest(false, true);
        int beforePreparing = AIBrain.RetainedExecutionRevision;
        ObserveAndReview(skill, 12f);
        check(!AIBrain._needCall && AIBrain.RetainedExecutionRevision == beforePreparing,
            "unchanged pending facts do not cancel or restart an existing preparation");
        AIBrain.SetObservationTransportForTest(false, false);
        ObserveAndReview(skill, 12.1f);
        check(AIBrain._needCall && AIBrain.RetainedExecutionRevision == beforePreparing + 1,
            "pending review resumes once the preparation or transport boundary becomes available");

        skill = StartPending();
        firstRevision = AIBrain.RetainedExecutionRevision;
        AIBrain.AcknowledgeObservationForTest(firstRevision);
        skill.BeginObservationTick();
        AIBrain.ReviewPendingForTest(7f);
        check(!AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision,
            "a historical condition no longer observed by the skill does not generate reviews");
        skill.Status = SkillStatus.Done;
        ObserveAndReview(skill, 8f);
        check(!AIBrain._needCall && AIBrain.RetainedExecutionRevision == firstRevision,
            "an ended execution cannot generate another pending review");

        foreach (float frameSeconds in new[] { 0.05f, 2f })
        {
            skill = StartPending();
            AIBrain.AcknowledgeObservationForTest(AIBrain.RetainedExecutionRevision);
            int reviews = 0;
            float lastReview = 1f;
            bool correctlySpaced = true;
            for (float now = 1f + frameSeconds; now < 22f; now += frameSeconds)
            {
                // Production updates Brain first, then clears the body tick marker,
                // then Tick records whether this condition still occurs.
                Time.realtimeSinceStartup = now;
                AIBrain.ReviewPendingForTest(now);
                if (AIBrain._needCall)
                {
                    correctlySpaced &= now - lastReview >= 4.999f && now - lastReview <= 5f + frameSeconds + 0.001f;
                    lastReview = now;
                    reviews++;
                    AIBrain.AcknowledgeObservationForTest(AIBrain.RetainedExecutionRevision);
                    AIBrain.ApplyChosen(Harness.World, Harness.Player, now, "continue", skill.Key);
                }
                skill.BeginObservationTick();
                skill.Observe("receipt:pending", "수령 사실 미확인");
            }
            check(reviews >= 3 && correctlySpaced && AIBody.Current == skill,
                "Brain-before-body order sustains bounded quiet reviews at " + frameSeconds + " second frames");
        }

        skill = StartPending();
        int oldGoalRevision = skill.ObservationGoalRevision;
        AIBrain.ObserveExecution("목표 조건 관측 요청 수량 확보 1/1");
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Gather, ObjectID.CopperOre, "구리", 5, "next-plan");
        check(AIBrain.ExecutionObservation == "없음" && !AIBrain.HasUnaskedObservationForTest,
            "new goal clears prior 1/1 event and its unconsumed wake authority");
        check(AIBody.Current == skill && skill.ObservationGoalRevision == oldGoalRevision,
            "goal replacement preserves the selected body and its original observation owner");
        AIBrain.AcknowledgeObservationForTest(AIBrain.RetainedExecutionRevision);
        ObserveAndReview(skill, 6f);
        check(AIBrain.ExecutionObservation.Contains("이전 목표") && AIBrain.ExecutionObservation.Contains("실행 17") &&
            !AIBrain.ExecutionObservation.Contains("1/1"),
            "continuing old body reports its pending condition as prior-goal evidence after goal replacement");

        Harness.Reset();
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Gather, ObjectID.CopperOre, "구리", 5, "incomplete-plan");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 10f, "finish_goal");
        check(AIBrain._needCall && AIBrain.ObservationRetryAtForTest < 10.001f && AIGoals.Goal.IsReal,
            "unconfirmed completion requires a judgment without the former five-second hold (2026-11-08)");
        AIBrain.ExhaustRate(15f);
        check(AIBrain.BudgetAllows(15f) && AIBrain._needCall && AIGoals.Goal.IsReal,
            "a full former per-minute window no longer blocks the pending correction (caps removed 2026-11-08)");
        AIBrain.ResetBudget();
        check(AIBrain.BudgetAllows(15f), "pending correction becomes budget eligible when allowance is available");
        AIBrain.ExhaustCost();
        check(!AIBrain.BudgetAllows(16f) && AIBrain._needCall && AIGoals.Goal.IsReal,
            "monthly cost cap also preserves the pending correction and original goal");
    }
#endif
}

#if !BASELINE
// Only transport acknowledgement and a fake clock are test boundaries. The review,
// observation, goal ownership, explicit choice and budget methods are production code.
public static partial class AIBrain
{
    public static void AcknowledgeObservationForTest(int sentRevision)
    {
        S.AskedExecutionRevision = sentRevision;
        _needCall = false;
    }
    public static void ReviewPendingForTest(float now) => RefreshPendingExecution(now);
    public static bool HasUnaskedObservationForTest => S.ExecutionRevision != S.AskedExecutionRevision;
    public static float ObservationRetryAtForTest => _retryAt;
    public static void SetObservationTransportForTest(bool request, bool preparing)
    {
        _req = request ? new Request() : null;
        S.Preparation = preparing ? new PreparationProbe() : null;
    }
}
#endif
