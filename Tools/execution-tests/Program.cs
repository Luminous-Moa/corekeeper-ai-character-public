using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

public static class Program
{
    private static int _checks, _failures;
    private static void Check(bool condition, string what)
    {
        _checks++;
        if (!condition) _failures++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + what);
    }
    private static ProbeSkill BeginGather()
    {
        Harness.Reset();
        AIPlanner.Orders.Add(new HostOrder { Id = 2, Text = "구리 갑옷 만들어줘", State = "따름", Source = "llm:gemini#2" }, null);
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Gather, ObjectID.CopperOre, "구리", 10, "llm:gemini#2");
        var selected = new ProbeSkill("dig:-11,24");
        AIBody.StartSkill(Harness.World, Harness.Player, selected, 0);
        return selected;
    }
    public static int Main()
    {
        var selected = BeginGather();
        AIBody.Bag[ObjectID.CopperOre] = 10;
        int revision = AIBrain.Revision;
        bool ended = AIGoals.CheckDone(Harness.World, Harness.Player, 56.3f);
#if BASELINE
        Check(ended, "baseline actual CheckDone consumes the 10/10 observation as goal completion");
        Check(AIBody.Current == null && selected.Status == SkillStatus.Cancelled, "baseline cancels selected next mining at 10/10");
        Check(AIPlanner.Orders.Latest.State == "끝남", "baseline marks copper-armour instruction done after ore step");
#else
        Check(!ended && AIGoals.CompletionObserved, "10/10 is an observation and leaves the update loop running");
        Check(AIGoals.Goal.kind == GoalKind.Gather && AIBrain.Revision == revision, "10/10 preserves goal and revision");
        Check(AIBody.Current == selected && selected.Status == SkillStatus.Running && AIBody.Stops == 0, "selected next mining survives the same 10/10 boundary");
        Check(selected.Tick(Harness.World, Harness.Player, 57).primary && selected.Ticks == 1, "selected probe skill can still produce input on its next Tick");
        Check(AIPlanner.Orders.Latest.State == "따름" && AIPlanner.EndReports == 0, "ore observation preserves original armour instruction without ending report");
        int observations = AIBrain.RetainedExecutionRevision;
        AIGoals.CheckDone(Harness.World, Harness.Player, 58);
        Check(AIBrain.RetainedExecutionRevision == observations, "unchanged 10/10 does not repeatedly wake the model");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 59, "request_plan");
        Check(AIPlanner.Replans == 1 && AIBody.Current == selected && AIGoals.Goal.IsReal, "actual Apply request_plan asks upper planner while body and goal continue");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 60, "stop_action");
        Check(AIBody.Current == null && selected.Status == SkillStatus.Cancelled && AIGoals.Goal.IsReal, "actual Apply stop_action stops only the body");
        Check(AIPlanner.Orders.Latest.Open, "explicit action stop preserves original instruction");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 61, "mine", "mine:-11,25");
        Check(AIBody.Current != null && AIBody.Current.Key == "mine:-11,25", "actual Apply accepts a new chosen body action after stop");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 62, "finish_goal");
        Check(!AIGoals.Goal.IsReal && AIBody.Current == null && AIPlanner.Pending, "actual Apply finish_goal clears current step and queues report behind pending upper response");
        Check(AIPlanner.Orders.Latest.State == "따름", "explicit ore-step completion leaves armour instruction open");
        AIPlanner.Orders.ApplyAnswer("done", null, "goal_done", 2, false, "갑옷 전달 완료", "upper chose done", null, null);
        Check(AIPlanner.Orders.Latest.State == "끝남", "actual host-order answer done closes original instruction");

        selected = BeginGather();
        int beforeBlocked = AIBrain.RetainedExecutionRevision;
        AIGoals.CheckAfterBuild(Harness.World, Harness.Player, 5, new GoalBuildFacts { GatherFacts = "no source", BlockNote = "route unavailable" });
        Check(AIGoals.Goal.IsReal && AIBody.Current == selected && AIBrain.RetainedExecutionRevision == beforeBlocked + 1, "no-progress build reports facts without cancelling a valid running choice");
        selected.Observe("progress", "6 seconds without damage");
        selected.Observe("progress", "6 seconds without damage");
        Check(selected.Status == SkillStatus.Running && selected.Facts.Count == 1 && AIBrain.RetainedExecutionRevision == beforeBlocked + 2, "actual AISkill ObserveOnce wakes once without finishing body");
        AIBrain._needReason = "API retry";
        Check(selected.ExecutionObservations.Count == 1 && selected.ExecutionObservations[0] == "6 seconds without damage", "actual skill retains execution conditions after transient retry reason changes");
        Check(AIBrain.RetainedExecutionReason.Contains("6 seconds without damage") && AIBrain.RetainedExecutionRevision == beforeBlocked + 2, "actual ObserveExecution retains fact and revision independently of transient call reason");
        AIGoals.HandleGoalCommand(Harness.World, Harness.Player, new[] { "/ai", "goal", "none" });
        Check(!AIGoals.Goal.IsReal && AIBody.Current == null, "direct host goal clear still stops execution");

        Harness.Reset();
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Home, ObjectID.None, "", 1, "llm:gemini#3");
        selected = new ProbeSkill("goto:base"); AIBody.StartSkill(Harness.World, Harness.Player, selected, 0);
        AIGoalFeatures.Done = true;
        AIGoals.CheckDone(Harness.World, Harness.Player, 1);
        Check(AIGoals.CompletionObserved && AIBody.Current == selected && AIGoals.Goal.kind == GoalKind.Home, "feature completion also reports arrival without automatic step finish");

        Harness.Reset();
        AIPlanner.Orders.Add(new HostOrder { Id = 3, Text = "갑옷 입고 보급해줘", State = "따름", Source = "llm:gemini#3" }, null);
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Equip, ObjectID.CopperBreastArmor, "구리 흉갑", 1, "llm:gemini#3", "chest", -1, 12);
        AIGoalFeatures.Done = true; AIInventory.Observed = true;
        AIGoals.CheckDone(Harness.World, Harness.Player, 2);
        Check(AIGoals.CompletionObserved && AIGoals.Goal.IsReal && AIGoals.Goal.note.Length > 0, "inventory receipt reaches common completion observation without automatically ending its step");
        AIGoals.CheckAfterBuild(Harness.World, Harness.Player, 3, new GoalBuildFacts { HasProgress = false });
        Check(!AIGoals.Observation.Contains("no current inventory choice") && AIPlanner.EndReports == 0, "completed inventory receipt is not reclassified as stalled when progress choices disappear");
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 4, "finish_goal");
        Check(!AIGoals.Goal.IsReal && AIPlanner.EndReports == 1, "common explicit finish_goal advances an observed equipment step");
        Check(AIPlanner.Orders.Latest.Open, "equipment step completion preserves the original equip-and-supply request");
        Harness.Reset(); AIBody.SlotObjects[12] = ObjectID.CopperBreastArmor;
        AIGoals.SetGoal(Harness.World, Harness.Player, GoalKind.Store, ObjectID.None, "", 1, "llm:gemini#4", null, -1, 12);
        Check(AIGoals.Goal.item == ObjectID.CopperBreastArmor && AIGoals.Goal.slot == 12, "slot-only store binds the same actual item for existing chest preparation and exact movement");

        selected = BeginGather();
        selected.Run = 41;
        var stop = new AICandidate { kind = "stop_action", run = 41 };
        Check(AIActions.Stale(Harness.World, Harness.Player, stop) == null, "actual stop stale check accepts the same observed running execution");
        selected.Run = 42;
        Check(AIActions.Stale(Harness.World, Harness.Player, stop) != null, "actual stop stale check rejects late stop of run 41 after run 42 began");
        stop.run = 0;
        Check(AIActions.Stale(Harness.World, Harness.Player, stop) != null, "stop observed while idle cannot cancel a newly started run");
        AIBody.Current = null;
        Check(AIActions.Stale(Harness.World, Harness.Player, stop) == null, "idle stop remains valid while still idle");

        selected = BeginGather();
        AIBrain.SeedQueuedOnly(selected);
        selected.Status = SkillStatus.Done;
        AIBrain.OnSkillEnded(selected, 10);
        Check(AIBrain.QueuedReady, "actual OnSkillEnded still schedules a chosen prefetch after ordinary completion");

        selected = BeginGather();
        AIBody.BeginTail();
        AIBrain.SeedPending(selected);
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 10, "stop_action");
        Check(AIBody.Current == null && !AIBody.TailRunning && AIEdge.TailAborts == 1, "actual Apply and Body.Stop cut an active closing tail for explicit AI stop");
        Check(AIBrain.LastEndedResult.Contains("문이 열린 채일 수 있음"), "actual EndSkill and OnSkillEnded report the cut tail and possibly open door");
        Check(!AIBrain.HasPendingChoices, "actual OnSkillEnded cannot reschedule old queued, held or next-hit choices after explicit stop");
        Check(AIGoals.Goal.IsReal && AIPlanner.Orders.Latest.Open && AIEdge.TailStartCalls == 0, "explicit AI stop preserves request and starts no replacement tail");

        selected = BeginGather();
        AIBody.BeginTail();
        AIBody.Bag[ObjectID.CopperOre] = 10;
        AIBrain.ApplyChosen(Harness.World, Harness.Player, 10, "finish_goal");
        Check(AIBody.Current == null && AIEdge.TailAborts == 1 && !AIGoals.Goal.IsReal && AIPlanner.Orders.Latest.Open, "explicit step completion also cuts tail while preserving original instruction");

        selected = BeginGather();
        AIBody.BeginTail();
        AIGoals.HandleGoalCommand(Harness.World, Harness.Player, new[] { "/ai", "goal", "none" });
        Check(AIBody.Current == null && AIEdge.TailAborts == 1, "direct host goal clear cuts an active tail immediately");

        selected = BeginGather();
        AIGoals.Goal = new AIGoal(); AIBrain._sd = true;
        AIBody.BeginTail(); AIBrain.SeedPending(selected);
        AIGoals.HandleGoalCommand(Harness.World, Harness.Player, new[] { "/ai", "goal", "none" });
        Check(AIBody.Current == null && !AIBrain.HasPendingChoices && !AIBrain._sd, "host clear without a real goal clears self-defence tail and old waiting choices");

        foreach (string menuAction in new[] { "enter", "back", "cancel" })
        {
            selected = BeginGather();
            var originalGoal = AIGoals.Goal;
            AIBrain.SeedPending(selected); AIBrain.SeedInFlight();
            AIBrain.ApplyMenu(Harness.World, Harness.Player, 10, menuAction);
            Check(AIBody.Current == selected && selected.Status == SkillStatus.Running && AIBody.Stops == 0,
                "actual Loop menu " + menuAction + " retains the selected body run without a stop or replacement");
            Check(ReferenceEquals(AIGoals.Goal, originalGoal) && AIPlanner.Orders.Latest.Open && AIPlanner.EndReports == 0,
                "actual Loop menu " + menuAction + " changes no goal, original instruction or completion");
            Check(!AIBrain.HasPendingChoices && !AIBrain.HasInFlight && AIBrain.AbortedRequests == 1,
                "actual Loop menu " + menuAction + " revokes superseded ordinary request/queued/held/next-hit authority");
            Check(selected.Tick(Harness.World, Harness.Player, 11).primary,
                "actual Loop menu " + menuAction + " leaves the old body able to produce its next selected input");
        }

        Harness.Reset();
        var observation = new AIObservation();
        var ordinary = new List<AICandidate>();
        for (int i = 0; i < 30; i++) ordinary.Add(new AICandidate { kind = "mine", key = "world-" + i, desc = "관측된 목표 " + i });
        ordinary.Add(new AICandidate { kind = "store", key = "legacy-store" });
        ordinary.Add(new AICandidate { kind = "take", key = "legacy-take" });
        ordinary.Add(new AICandidate { kind = "inventory", key = "legacy-flat" });
        AIInventoryMenu.Choices.Add(new AICandidate { kind = "inventory_menu", key = "inventory-entry", desc = "인벤토리 분류로 들어가기" });
        var surfaced = AIBrain.Surface(Harness.World, Harness.Player, observation, ordinary);
        string rootJson = AIBrain.RequestJson("메뉴 밖 실제 선택 입력", surfaced);
        using (var json = JsonDocument.Parse(rootJson))
        {
            var values = json.RootElement.GetProperty("questions")[0].GetProperty("choices");
            Check(surfaced.Count == 31 && values.GetArrayLength() == 31 && surfaced.FindAll(c => c.kind == "mine").Count == 30,
                "actual public surface and model JSON retain thirty eligible world choices beyond fourteen");
            Check(!surfaced.Exists(c => c.key.StartsWith("legacy")) && surfaced.FindAll(c => c.kind == "inventory_menu").Count == 1,
                "ordinary root publishes one inventory tree entry instead of legacy per-item store/take/inventory choices");
        }
        Console.WriteLine("MODEL ROOT PAYLOAD " + rootJson);

        AIInventoryMenu.Active = true; AIInventoryMenu.Choices.Clear();
        for (int i = 0; i < 20; i++) AIInventoryMenu.Choices.Add(new AICandidate { kind = "inventory_menu", key = "item-" + i, desc = "현재 분류 보유 물품 " + i });
        AIInventoryMenu.Choices.Add(new AICandidate { kind = "inventory_menu", key = "back", desc = "이전 선택으로" });
        AIInventoryMenu.Choices.Add(new AICandidate { kind = "inventory_menu", key = "cancel", desc = "가방 선택 전체 취소" });
        ordinary = new List<AICandidate> {
            new AICandidate { kind = "mine", key = "unrelated-world", desc = "다른 작업" }, new AICandidate { kind = "continue", key = "current-run", desc = "현재 동작 계속" },
            new AICandidate { kind = "stop_action", key = "body-stop", desc = "현재 몸 동작 중단" }, new AICandidate { kind = "finish_goal", key = "step-finish", desc = "단계 완료" },
            new AICandidate { kind = "request_plan", key = "replan", desc = "상위 재계획" }, new AICandidate { kind = "eat", key = "vitals", desc = "기존 생존/회복 먹기" }
        };
        surfaced = AIBrain.Surface(Harness.World, Harness.Player, observation, ordinary);
        string nodeJson = AIBrain.RequestJson("현재 분류에서 다음 선택", surfaced);
        using (var json = JsonDocument.Parse(nodeJson))
            Check(json.RootElement.GetProperty("questions")[0].GetProperty("choices").GetArrayLength() == 27 && !surfaced.Exists(c => c.key == "unrelated-world"),
                "active menu sends all twenty current children plus back/cancel and common/vital controls without a count cut");
        Check(surfaced.Exists(c => c.kind == "continue") && surfaced.Exists(c => c.kind == "stop_action") && surfaced.Exists(c => c.kind == "finish_goal") && surfaced.Exists(c => c.kind == "request_plan"),
            "active menu retains continue and explicit common controls");
        Console.WriteLine("MODEL NODE PAYLOAD " + nodeJson);

        var enemy = new Unity.Entities.Entity { Index = 9 }; observation.enemies.Add(new SeenEnemy { entity = enemy, dist = 1 });
        ordinary = new List<AICandidate> { new AICandidate { kind = "attack", key = "enemy", entity = enemy }, new AICandidate { kind = "dig", key = "enemy-route", entity = enemy }, new AICandidate { kind = "dig", key = "other-route", entity = new Unity.Entities.Entity { Index = 10 } } };
        surfaced = AIBrain.Surface(Harness.World, Harness.Player, observation, ordinary);
        Check(surfaced.Exists(c => c.key == "enemy") && surfaced.Exists(c => c.key == "enemy-route") && !surfaced.Exists(c => c.key == "other-route"),
            "active menu preserves the existing threat attack and enemy approach route without reopening unrelated world branches");

        selected = BeginGather(); AIBrain.ApplyMenu(Harness.World, Harness.Player, 5, "enter");
        Check(AIBrain._treeStepNext, "bag-menu navigation marks the next decision call as floor-free (2026-11-08)");
        AIBrain.ExhaustRate(6);
        Check(AIBrain.BudgetAllows(6) && AIInventoryMenu.Active && AIBody.Current == selected && selected.Status == SkillStatus.Running && AIGoals.Goal.IsReal,
            "no per-minute cap blocks the next menu decision; menu, body and goal are kept (caps removed 2026-11-08)");
        AIBrain.ExhaustCost();
        Check(!AIBrain.BudgetAllows(7) && AIInventoryMenu.Active && AIInventoryMenu.LeafCount == 0 && AIBody.Stops == 0 && AIPlanner.Orders.Latest.Open,
            "actual monthly cost gate blocks menu calls while preserving selected body and original request");
#endif
#if !BASELINE
        selected = BeginGather(); AIBrain.SeedPreparingPrefetch(selected);
        selected.Status = SkillStatus.Done; AIBrain.OnSkillEnded(selected, 10);
        Check(AIBrain.HasPreparingPrefetch && AIBrain.PreparingPrefetchCompleted,
            "production skill-ended callback preserves preparing prefetch and marks the original action completed");
        selected = BeginGather(); AIBrain.SeedPreparingPrefetch(selected);
        selected.Status = SkillStatus.Failed; AIBrain.OnSkillEnded(selected, 10);
        Check(!AIBrain.HasPreparingPrefetch, "production skill-ended callback cancels preparation after action failure");
        ExpeditionFlowTests.Run(Check);
        GoalCompletionTests.Run(Check);
        ObservationReviewTests.Run(Check);
        OrderCompletionTests.Run(Check);
#endif
        foreach (string line in Harness.Trace) Console.WriteLine("TRACE " + line);
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed");
        return _failures == 0 ? 0 : 1;
    }
}
