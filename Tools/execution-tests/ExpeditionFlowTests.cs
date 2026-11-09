using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class ExpeditionFlowTests
{
#if !BASELINE
    public static void Run(Action<bool, string> check)
    {
        Harness.Reset(); var w = Harness.World; var p = Harness.Player;
        AIPlanner.Orders.Add(new HostOrder { Id = 1, Text = "구리 갑옷 만들어줘", State = "따름", Source = "llm:gemini#1" }, null);
        AIPlanner.Cur.Plan.Set("llm:gemini#1", new List<PlanStep> { new PlanStep("gather", "CopperOre", 200), new PlanStep("home", null, 1), new PlanStep("store", "CopperOre", 200), new PlanStep("craft", "CopperBreastArmor", 1) });
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",200,"llm:gemini#1");
        var original = AIGoals.Goal;
        AIBody.Bag[ObjectID.CopperOre] = 0; AIBody.Bag[ObjectID.Wood] = 0;
        var mine = new AICandidate { kind = "gather_group", key = "ore-patch", itemId = ObjectID.CopperOre, progress = true, desc = "보이는 구리 묶음", gather = new AIGatherSelection() };
        var wood = new AICandidate { kind = "mine", key = "wood-patch", itemId = ObjectID.Wood, desc = "길가 나무" };
        var o = new AIObservation();
        var choices = AIExpedition.Compose(w,p,o,new List<AICandidate> { mine, wood });
        var open = choices.Find(c => c.expeditionNav?.Op == "open");
        AIBrain.ApplyCandidate(mine,0); var mainSkill = AIBody.Current;
        AIBrain.ApplyCandidate(open,1);
        check(AIExpedition.Active && AIBody.Current == mainSkill && ReferenceEquals(original,AIGoals.Goal), "expedition actual Apply opens tree while retaining chosen body/goal");
        check(AIActions.Stale(w,p,open) != null, "old tree response invalidated by cursor revision");
        choices = AIExpedition.Compose(w,p,o,new List<AICandidate> { mine, wood });
        AIBrain.ApplyCandidate(choices.Find(c => c.expeditionNav?.Value == "gather"),2);
        choices = AIExpedition.Compose(w,p,o,new List<AICandidate> { mine, wood });
        check(choices.Exists(c => c.expeditionNav?.Value == "CopperOre") && choices.Exists(c => c.expeditionNav?.Value == "Wood"), "tree exposes observed goal and side resource without quantity cap");
        AIBrain.ApplyCandidate(choices.Find(c => c.expeditionNav?.Op == "close"),3);
        AIBody.Bag[ObjectID.CopperOre] = 120; AIExpedition.Observe(w,p);
        AIBrain.ApplyCandidate(wood,4); Time.realtimeSinceStartup = 6;
        AIBody.Bag[ObjectID.Wood] = 2; AIExpedition.Observe(w,p);
        choices = AIExpedition.Compose(w,p,o,new List<AICandidate> { mine, wood });
        var resume = choices.Find(c => c.kind == "expedition_resume");
        check(resume?.resume == mine && ReferenceEquals(original,AIGoals.Goal), "side work retains original fixed candidate and goal for resume");
        AIBrain.ApplyCandidate(resume,7);
        check(AIBody.Current.Key == mine.key && AIExpedition.Gathered(original,120) == 120, "actual Apply resumes main work with preserved accumulated receipt");
        AIBody.Bag[ObjectID.CopperOre] = 220; AIExpedition.Observe(w,p); AIGoals.CheckDone(w,p,8);
        check(AIGoals.CompletionObserved && AIBody.Current.Status == SkillStatus.Running, "200+ actual acquisition is observed without automatic finish or return");
        var returnWalk = new AICandidate { kind = "retreat", key = "return", tile = new int2(40,0), desc = "기지로 돌아가기" };
        AIExpedition.Compose(w,p,o,new List<AICandidate> { mine, wood }); AIBrain.ApplyCandidate(returnWalk,9);
        check(AIPlanner.Replans == 1 && AIBody.Current.Key == "return", "leaving unreviewed opportunity reports while selected movement continues");
        AIBrain.ApplyCandidate(wood,10); AIBrain.ApplyCandidate(returnWalk,11);
        check(AIPlanner.Replans == 1 && !AIPlanner.Pending, "identical leaving opportunity does not enqueue duplicate replan");
        AIExpedition.Sent(1); AIExpedition.Accepted(1);
        AIBrain.ApplyCandidate(wood,12); AIBrain.ApplyCandidate(returnWalk,13);
        check(AIPlanner.Replans == 1 && !AIPlanner.Pending, "accepted report marks reported opportunity facts reviewed");
        var fresh = new AICandidate {kind="pickup",key="new-opportunity",itemId=ObjectID.TinOre,itemCount=2,desc="새 주석 2개"};
        AIExpedition.Compose(w,p,o,new List<AICandidate>{fresh}); AIBrain.ApplyCandidate(returnWalk,13.5f);
        check(AIPlanner.Pending && AIPlanner.Replans==1 && AIBody.Current.Key=="return", "new observed opportunity queues followup while first report and body remain in flight");
        AIPlanner.CompleteTransport(); AIPlanner.TickPending(w,p,14);
        using (var sent=System.Text.Json.JsonDocument.Parse(AIPlanner.LastRequestBody))
        {
            var report=sent.RootElement.GetProperty("report");
            check(AIPlanner.Replans==2 && !AIPlanner.Pending && report.GetProperty("events").GetArrayLength()>0
                && report.GetProperty("events")[0].GetString().Contains("TinOre@")
                && report.GetProperty("expedition_observation").GetString().Contains("새 주석 2개"),
                "production AddEvent pending dispatch Report serialization retain the new fact and expedition context in offline HTTP payload");
        }
        AIBody.Bag[ObjectID.CopperOre] = 0; AIExpedition.Observe(w,p);
        check(AIExpedition.Gathered(original,0) == 220, "deposit does not erase accumulated gathering progress");
        check(AIExpedition.Report().Contains("home") && AIExpedition.Report().Contains("CopperBreastArmor") && AIPlanner.Orders.Latest.Open, "report carries return storage crafting plan while original request remains open");
        AIBrain.ApplyCandidate(new AICandidate { kind = "finish_goal" },14);
        check(!AIGoals.Goal.IsReal && AIPlanner.Orders.Latest.Open, "actual explicit step completion does not complete original request");
        AIPlanner.TickNextStep(w,p,15);
        check(AIGoals.Goal.kind == GoalKind.Home && AIPlanner.Cur.Plan.Rest()[0].Do == "store", "production finish callback and next-tick scheduler consume home goal");
        AIGoalFeatures.Done = true;
        AIBrain.ApplyCandidate(new AICandidate { kind = "finish_goal" },16); AIPlanner.TickNextStep(w,p,17);
        check(AIGoals.Goal.kind == GoalKind.Store && AIPlanner.Orders.Latest.Open, "production home completion starts storage without completing original request");
        AIBrain.ApplyCandidate(new AICandidate { kind = "finish_goal" },18); AIPlanner.TickNextStep(w,p,19);
        check(AIGoals.Goal.kind == GoalKind.Craft && AIGoals.Goal.item == ObjectID.CopperBreastArmor, "production storage completion consumes the planned crafting goal");
        Harness.Reset();
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",50,"llm:gemini#2","tile:20,30");
        AIExpedition.Compose(w,p,o,new List<AICandidate>{mine}); AIBrain.ApplyCandidate(mine,0);
        AIBody.Bag[ObjectID.CopperOre] = 20; AIExpedition.Observe(w,p);
        AIPlanner.Cur.Plan.Set("llm:gemini#3",new List<PlanStep> {new PlanStep("home",null,1),new PlanStep("store","CopperOre",20),new PlanStep("gather","CopperOre",30,"tile:20,30")});
        AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"llm:gemini#3");
        var interrupted = AIExpedition.Report();
        check(interrupted.Contains("목표 부족 30") && interrupted.Contains("실제 누적 20") && interrupted.Contains("tile:20,30"), "early return retains original goal acquired shortage and work location");
        AIGoalFeatures.Done = true;
        AIBrain.ApplyCandidate(new AICandidate {kind="finish_goal"},2); AIPlanner.TickNextStep(w,p,3);
        AIBody.Bag[ObjectID.CopperOre] = 0; AIExpedition.Observe(w,p);
        AIBrain.ApplyCandidate(new AICandidate {kind="finish_goal"},4); AIPlanner.TickNextStep(w,p,5);
        check(AIGoals.Goal.kind == GoalKind.Gather && AIGoals.Goal.count == 30 && AIGoals.Goal.where == "tile:20,30" && AIExpedition.Report().Contains("실제 누적 20"), "upper planned remainder is consumed after return storage with prior acquisition and location preserved");
        Harness.Reset(); AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",10,"llm:gemini#2");
        AIExpedition.Compose(w,p,o,new List<AICandidate>());
        AIGoals.CheckAfterBuild(w,p,1,new GoalBuildFacts { GatherFacts = "관측한 광맥 고갈", BlockNote = "알려진 경로 없음" });
        var emptyCorridor = new List<AICandidate> { new AICandidate {kind="explore",progress=false,desc="새 지역 탐색 가능"} };
        AIExpedition.AfterBuild(w,p,emptyCorridor,false); AIExpedition.AfterBuild(w,p,emptyCorridor,false);
        check(AIPlanner.Replans == 1 && AIGoals.Goal.IsReal, "premise loss and no alternative reports once without cancelling goal");
        choices = AIExpedition.Compose(w,p,o,new List<AICandidate> { mine });
        var old = choices.Find(c => c.expeditionNav?.Op == "open");
        AIAgent.Cur = new AIAgent { Id = "other" };
        check(AIExpedition.Stale(old) != null, "tree tokens cannot cross character ownership");
        AIAgent.Cur.Id = "gemini"; AIPlayerSpikeMod.WorldId = "other-world";
        check(AIExpedition.Stale(old) != null, "tree tokens cannot cross worlds");
        Harness.Reset();
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",10,"llm:gemini#4");
        AIExpedition.Compose(w,p,o,new List<AICandidate>{mine}); AIBrain.ApplyCandidate(mine,0);
        AIGathering.LimitBlock = "명시 상한에 비해 다음 채집 최대 산출량 미확인";
        AIExpedition.EnforceExplicitLimit(w,p,1);
        check(AIBody.Current != null, "ordinary target quantity never activates explicit-limit body guard");
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",10,"llm:gemini#5",null,-1,-1,null,null,10);
        AIExpedition.EnforceExplicitLimit(w,p,2);
        check(AIBody.Current == null && AIGoals.Goal.IsReal && AIBrain.RetainedExecutionRevision > 0, "new explicit upper condition stops further gathering input and wakes local judgment without completing goal");
        choices = AIExpedition.Compose(w,p,o,new List<AICandidate>{mine});
        check(!choices.Exists(c=>c.kind=="gather_group") && AIActions.Stale(w,p,mine)!=null, "explicitly unexecutable collection is excluded and in-flight selection rechecked");
        Harness.Reset();
        var order=new HostOrder {Id=7,Text="구리는 최대10개만 가져와",State="따름",Source="old-source"};
        AIPlanner.Orders.Add(order,null);
        var boundedPlan=new List<PlanStep> {new PlanStep("home",null,1),new PlanStep("gather","CopperOre",10,null,-1,-1,null,null,10)};
        AIPlanner.ApplyPlanForTest(w,p,0,boundedPlan,new List<HostOrder>{order},"bounded-plan");
        check(AIGoals.Goal.kind==GoalKind.Home && boundedPlan[1].MaxOrder==7 && AIExpedition.ActiveLimits().Count==1,
            "production plan bind resolves single original owner and registers future gather condition during preceding home stage");
        AIGoalFeatures.Done = true;
        AIBrain.ApplyCandidate(new AICandidate{kind="finish_goal"},1); AIPlanner.TickNextStep(w,p,2);
        AIExpedition.Compose(w,p,o,new List<AICandidate>{mine}); AIBrain.ApplyCandidate(mine,3);
        AIBody.Bag[ObjectID.CopperOre]=5; AIExpedition.Observe(w,p);
        check(AIExpedition.ActiveLimits()[0].Acquired==5 && AIExpedition.ActiveLimits()[0].OrderId==7,"actual receipt is accumulated by original owner and item");
        AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"bounded-plan");
        AIBrain.ApplyCandidate(new AICandidate{kind="stop_action"},4);
        AIBody.Bag[ObjectID.CopperOre]=0; AIExpedition.Observe(w,p);
        check(AIExpedition.ActiveLimits().Count==1 && AIExpedition.ActiveLimits()[0].Acquired==5 && AIExpedition.Report().Contains("원부탁#7 CopperOre 최대 10 / 실제확보 5"),
            "home and deposit preserve the original user condition and observed acquisition in report");
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",20,"bounded-plan");
        check(AIExpedition.ActiveLimits()[0].Maximum==10,"new upper target without max field does not erase original user maximum");
        var oldGoal=AIGoals.Goal;
        AIPlanner.ApplyPlanForTest(w,p,5,new List<PlanStep>{new PlanStep("gather","CopperOre",11,null,-1,-1,null,null,11,7)},new List<HostOrder>{order},"illegal-change");
        check(ReferenceEquals(oldGoal,AIGoals.Goal) && AIPlanner.Pending && AIExpedition.ActiveLimits()[0].Maximum==10,
            "production accepted-plan path rejects silent original-condition change and queues report without replacing goal");
        var other=new HostOrder {Id=8,Text="다른 물건 작업",State="따름",Source="other-plan"}; AIPlanner.Orders.Add(other,null);
        var ambiguous=new List<PlanStep>{new PlanStep("gather","CopperOre",10,null,-1,-1,null,null,10)};
        AIPlanner.ApplyPlanForTest(w,p,6,ambiguous,new List<HostOrder>{order,other},"ambiguous-plan");
        check(ReferenceEquals(oldGoal,AIGoals.Goal) && ambiguous[0].MaxOrder<0,"multiple original requests require explicit condition owner and cannot become unlimited execution");
        check(AIExpedition.BindPlanLimits(new List<PlanStep>{new PlanStep("gather","CopperOre",10,null,-1,-1,null,null,10,7)},new List<HostOrder>{order,other})==null,
            "explicit max_order identifies the correct request among multiple carried requests");
        AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",10,"other-plan");
        check(AIExpedition.ActiveLimits().Count==0,"unrelated original request does not inherit another request maximum");
        AIExpedition.Compose(w,p,o,new List<AICandidate>{mine}); AIBrain.ApplyCandidate(mine,7);
        AIBody.Bag[ObjectID.CopperOre]=4; AIExpedition.Observe(w,p); AIBrain.ApplyCandidate(new AICandidate{kind="stop_action"},8);
        order.Source="resumed-plan"; AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"resumed-plan");
        check(AIExpedition.ActiveLimits()[0].Acquired==5,"replanning source change resumes original condition without counting separate request receipt");
        order.State="미룸"; order.Source=null;
        AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"other-plan");
        check(AIExpedition.ActiveLimits().Count==0,"put-off request condition is retained but not imposed on another request");
        order.State="따름";order.Source="resumed-again";
        AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"resumed-again");
        check(AIExpedition.ActiveLimits().Count==1 && AIExpedition.ActiveLimits()[0].Acquired==5,"resuming original put-off request restores its condition and progress");
        order.State="끝남";order.Source=null;
        check(AIExpedition.ActiveLimits().Count==0,"completed original request releases its quantity condition");
        BrowseTests(check);
    }

    private static void BrowseTests(Action<bool, string> check)
    {
        var recorded = new List<AICandidate>();
        var assembly = typeof(ExpeditionFlowTests).Assembly;
        string resource = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("expedition-258.json", StringComparison.Ordinal));
        using (var stream = assembly.GetManifestResourceStream(resource))
        using (var json = System.Text.Json.JsonDocument.Parse(stream))
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                var candidate = new AICandidate { kind = entry.GetProperty("kind").GetString(), key = entry.GetProperty("key").GetString(), desc = entry.GetProperty("key").GetString() };
                if (candidate.kind == "mine" || candidate.kind == "dig")
                {
                    var coordinates = candidate.key.Substring(candidate.key.IndexOf(':') + 1).Split(',');
                    candidate.tile = new int2(int.Parse(coordinates[0]), int.Parse(coordinates[1]));
                }
                recorded.Add(candidate);
            }
        check(recorded.Count == 258, "recorded live request fixture preserves all 258 original kind/key identities");
        foreach (int count in new[] { 208, 452, 7526 })
        {
            Harness.Reset(); var world = Harness.World; var player = Harness.Player; var observation = new AIObservation();
            AIGoals.SetGoal(world, player, GoalKind.Gather, ObjectID.CopperOre, "구리", 200, "browse-test");
            var leaves = count == 208 ? recorded.FindAll(c => c.kind == "mine" || c.kind == "dig") : new List<AICandidate>();
            if (count != 208)
                for (int i = 0; i < count; i++)
                    leaves.Add(new AICandidate { kind = "dig", key = "dig:" + i + "," + (-i), tile = new int2(i, -i), desc = "관측 광석 " + i });
            var direct = recorded.FindAll(c => c.kind != "mine" && c.kind != "dig" && c.kind != "expedition_nav" && c.kind != "inventory_menu");
            var known = new AICandidate { kind = "dig", key = "known-home", desc = "명시된 귀환 목적지", progress = true };
            var urgent = new AICandidate { kind = "dig", key = "urgent-enemy", desc = "위험 적까지의 경로" };
            var current = new AICandidate { kind = "continue", key = "continue-work", desc = "현재 작업 계속" };
            direct.Add(known); direct.Add(urgent); direct.Add(current);
            var chosen = new AICandidate { kind = "gather_group", key = "active-patch", desc = "현재 묶음", itemId = ObjectID.CopperOre, gather = new AIGatherSelection(), progress = true };
            AIBrain.ApplyCandidate(chosen, 0); var skill = AIBody.Current; var goal = AIGoals.Goal;
            bool withinLimit = true, directPreserved = true, onlySelected = true, noEarlyPreparation = true;
            int maximum = 0, examined = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
            List<AICandidate> Build()
            {
                var query = AIExpedition.BeginBuild();
                foreach (var leaf in leaves) query.Add(leaf.key, "gather", "CopperOre", leaf.tile.x, leaf.tile.y, leaf.desc, leaf);
                query.Seal(128);
                var actual = new List<AICandidate>(direct);
                foreach (var target in query.Targets)
                    if (query.ShouldPrepare(target.Key)) { actual.Add((AICandidate)target.Source); examined++; }
                if (!query.Open || query.Item == "") noEarlyPreparation &= actual.Count == direct.Count;
                var result = AIExpedition.Compose(world, player, observation, actual, query);
                result.Add(new AICandidate { kind = "inventory_menu", key = "test-inventory-entry", desc = "가방 열기" });
                for (int i = 0; i < result.Count; i++) result[i].value = "c" + (i + 1);
                using (var json = System.Text.Json.JsonDocument.Parse(AIBrain.RequestJson("현재 목록", result)))
                    withinLimit &= json.RootElement.GetProperty("questions")[0].GetProperty("choices").GetArrayLength() <= 255;
                maximum = Math.Max(maximum, result.Count);
                foreach (var entry in direct) directPreserved &= result.Contains(entry);
                foreach (var entry in result)
                    if (query.Contains(entry.key)) onlySelected &= query.ShouldPrepare(entry.key);
                return result;
            }
            void Choose(string op, string value = null)
            {
                var options = Build();
                var token = options.Find(c => c.expeditionNav?.Op == op && (value == null || c.expeditionNav.Value == value));
                if (token == null) throw new Exception("Missing browse navigation " + op + "/" + value);
                AIBrain.ApplyCandidate(token, 1);
            }
            void Visit()
            {
                var options = Build();
                foreach (var entry in options) if (leaves.Contains(entry)) seen.Add(entry.key);
                var ranges = options.FindAll(c => c.expeditionNav?.Op == "range");
                foreach (var range in ranges)
                {
                    Choose("range", range.expeditionNav.Value);
                    Visit();
                    Choose("back");
                }
            }
            var closed = Build();
            check(!closed.Exists(c => leaves.Contains(c)) && examined == 0, count + " catalog exposes closed navigation with zero unselected route preparations");
            Choose("open"); Choose("branch", "gather"); Choose("item", "CopperOre"); Visit();
            check(seen.Count == count && withinLimit && maximum <= 255, count + " targets all reachable through production Compose/navigation/BuildJson within 255 including controls");
            check(directPreserved && noEarlyPreparation && onlySelected, count + " direct groups/current/known destination/urgent choices retained and only selected leaves prepared");
            check(AIBody.Current == skill && ReferenceEquals(goal, AIGoals.Goal), count + " range traversal preserves running body and original goal");
            var old = Build().Find(c => c.expeditionNav?.Op == "close");
            Choose("root");
            check(AIActions.Stale(world, player, old) != null, count + " root invalidates earlier range/close navigation tokens");
            Choose("close");
            check(!AIExpedition.Active && !Build().Exists(c => leaves.Contains(c)), count + " close restores direct surface and browser entry");
        }
        Harness.Reset();
        var w = Harness.World; var p = Harness.Player; var o = new AIObservation();
        AIGoals.SetGoal(w, p, GoalKind.Gather, ObjectID.CopperOre, "구리", 10, "catalog-report");
        var pending = AIExpedition.BeginBuild();
        pending.Add("mine:12,13", "gather", "CopperOre", 12, 13, "보이는 구리 관측");
        pending.Seal(128);
        AIExpedition.Compose(w, p, o, new List<AICandidate>(), pending);
        check(AIExpedition.Report().Contains("보이는 구리 관측") && AIExpedition.Report().Contains("대상 선택 후 확인"), "unprepared catalog resources retain observation facts without claiming route feasibility");
        AIGoals.CheckAfterBuild(w, p, 0, new GoalBuildFacts { GatherFacts = "아직 경로 미평가", InventorySelectionPending = true });
        AIExpedition.AfterBuild(w, p, new List<AICandidate>(), false);
        check(AIPlanner.Replans == 0, "unprepared browser catalog is not reported as proven absence of goal alternatives");
        AIBrain.ApplyCandidate(new AICandidate { kind = "retreat", key = "leave-catalog", tile = new int2(40, 0), desc = "귀환" }, 1);
        check(AIPlanner.Replans == 1 && AIExpedition.Report().Contains("보이는 구리 관측"), "leaving still reports unreviewed observed resources whose routes were deferred");
        // 토큰 절감 ①: the action model's report counts resources already in the choices and lists only the others briefly
        Harness.Reset();
        AIGoals.SetGoal(w, p, GoalKind.Gather, ObjectID.CopperOre, "구리", 10, "prompt-report");
        var catalog = AIExpedition.BeginBuild();
        catalog.Add("mine:30,31", "gather", "CopperOre", 30, 31, "목록 안 구리");
        catalog.Seal(128);
        var veinGroup = new AICandidate { kind = "gather_group", key = "gather:mine:1,1|mine:1,2", tile = new int2(1, 1), itemId = ObjectID.CopperOre,
            resources = new[] { "CopperOre@1,1", "CopperOre@1,2" }, desc = "보이는 같은 종류 2대상 묶음 채집 — 긴 설명", gather = new AIGatherSelection() };
        AIExpedition.Compose(w, p, o, new List<AICandidate> { veinGroup }, catalog);
        string prompt = AIExpedition.PromptReport(), upper = AIExpedition.Report();
        check(prompt.Contains("선택지에 있음(선택지 참조): CopperOre 2곳") && prompt.Contains("선택지 밖(채집·이동 목록 안): CopperOre @30,31")
            && !prompt.Contains("긴 설명") && upper.Contains("긴 설명") && upper.Contains("목록 안 구리"),
            "action-model report refers to the choices and lists outside resources briefly; the upper report keeps the descriptions");
        // 재검수 R6: a choice the bag menu took off the surface is not described as being in the browse catalog
        Harness.Reset();
        AIGoals.SetGoal(w, p, GoalKind.Gather, ObjectID.CopperOre, "구리", 10, "prompt-report-menu");
        var visibleVein = new AICandidate { kind = "gather_group", key = "gather:mine:5,5|mine:5,6", tile = new int2(5, 5), itemId = ObjectID.CopperOre,
            resources = new[] { "CopperOre@5,5", "CopperOre@5,6" }, desc = "구리 묶음", gather = new AIGatherSelection() };
        var noCatalog = AIExpedition.BeginBuild(); noCatalog.Seal(128);   // production registers no group in the catalog
        var surface = AIExpedition.Compose(w, p, o, new List<AICandidate> { visibleVein }, noCatalog);
        surface.Remove(visibleVein);   // the open bag menu narrows the same list in place
        string menuPrompt = AIExpedition.PromptReport();
        check(menuPrompt.Contains("지금 선택지에 없음(가방·생활 메뉴 중 등): CopperOre @5,5 @5,6") && !menuPrompt.Contains("목록 안): CopperOre @5,5"),
            "a resource hidden by the bag menu is reported as not in the choices now, not as in the catalog (재검수 R6)");
        var cursor = new ExpeditionCursor { Open = true, Branch = "gather" };
        ExpeditionQuery ItemCatalog()
        {
            var query = new ExpeditionQuery(cursor);
            for (int i = 0; i < 260; i++) query.Add("item:" + i, "gather", "kind-" + i.ToString("D3"), 0, 0, "대상 " + i);
            query.Seal(200); return query;
        }
        var split = ItemCatalog().Entries[0];
        cursor.Apply(new ExpeditionNav { Op = "range", Lower = split.Lower, Upper = split.Upper });
        var grouped = ItemCatalog(); var selectedItem = grouped.Entries[0];
        cursor.Apply(new ExpeditionNav { Op = "item", Value = selectedItem.Value });
        cursor.Apply(new ExpeditionNav { Op = "back" });
        check(cursor.Ranges.Count == 1 && ItemCatalog().Entries.Count == 130, "back from an item restores its parent classification range instead of losing that navigation level");
        Harness.Reset();
        AIGoals.SetGoal(w, p, GoalKind.Home, ObjectID.None, "귀환", 1, "collision-test");
        var collision = AIExpedition.BeginBuild();
        collision.Add("dig:1,2", "travel", "dig", 1, 2, "관측한 세계 대상");
        collision.Seal(128);
        var enemyRoute = new AICandidate { kind = "dig", key = "dig:1,2", entity = new Unity.Entities.Entity { Index = 42 }, desc = "긴급 적 경로" };
        var lifeRoute = new AICandidate { kind = "dig", key = "dig:1,2", life = new LifeSelection(), desc = "지정한 생활 시설" };
        var homeRoute = new AICandidate { kind = "dig", key = "dig:1,2", progress = true, desc = "명시한 귀환 목적지" };
        var worldRoute = new AICandidate { kind = "dig", key = "dig:1,2", desc = "아직 고르지 않은 세계 대상" };
        var collisions = AIExpedition.Compose(w, p, o, new List<AICandidate> { enemyRoute, lifeRoute, homeRoute, worldRoute }, collision);
        check(collisions.Contains(enemyRoute) && collisions.Contains(lifeRoute) && collisions.Contains(homeRoute) && !collisions.Contains(worldRoute),
            "same-key catalog collision preserves entity danger, life-bound execution and explicit home route independently of world leaf folding");
        LeaveReportRelevance(check);
        TravelPacing(check);
        Harness.Reset();
    }

    // 2026-11-08 항목6: while a corridor run moves, relevant sightings call at once; a common kind is offered after its
    // interval and only when seen again; not chosen doubles the interval, chosen restores it. No-goal idle calls only for issues.
    private static void TravelPacing(Action<bool, string> check)
    {
        var pacer = new TravelResourcePacer(); pacer.Sync(1); pacer.Learn("WallDirtBlock"); pacer.Learn("Wood");
        Func<string, string> why = item => item == "CopperOre" ? "계획 품목" : item == "TinOre" ? "광석" : null;
        var dirt = new[] { ("WallDirtBlock", 0, 1) };
        check(pacer.Observe(0, dirt, why).Count == 0 && !pacer.Offerable("WallDirtBlock", 1) && pacer.Observe(29, dirt, why).Count == 0,
            "a common kind is neither a call nor a choice before its first 30 s interval");
        check(pacer.Observe(31, new (string, int, int)[0], why).Count == 0 && pacer.Observe(35, dirt, why).Count == 1 && pacer.Offerable("WallDirtBlock", 35),
            "after the interval the kind calls only when it is seen again, and is then offered");
        check(pacer.Observe(36, dirt, why).Count == 0, "a due kind does not call again before it is answered");
        pacer.Sent(new[] { "WallDirtBlock" }); pacer.Answered(36, "Wood");
        check(pacer.IntervalOf("WallDirtBlock") == 60 && !pacer.Offerable("WallDirtBlock", 37) && pacer.Observe(95, dirt, why).Count == 0 && pacer.Observe(97, dirt, why).Count == 1,
            "offered but not chosen doubles the kind's interval (30 -> 60 s)");
        pacer.Sent(new[] { "WallDirtBlock" }); pacer.Answered(97, "WallDirtBlock");
        check(pacer.IntervalOf("WallDirtBlock") == TravelResourcePacer.FirstInterval, "choosing the kind restores the first interval");
        for (int i = 0; i < 10; i++) { pacer.Observe(1000 + i * 1000, dirt, why); pacer.Sent(new[] { "WallDirtBlock" }); pacer.Answered(1000 + i * 1000, null); }
        check(pacer.IntervalOf("WallDirtBlock") == TravelResourcePacer.MaxInterval, "the interval stops growing at its cap");
        var vein = pacer.Observe(5000, new[] { ("CopperOre", 10, 10), ("CopperOre", 11, 10) }, why);
        check(vein.Count == 1 && vein[0].Contains("CopperOre(계획 품목)"), "a plan item calls at once, one call for one vein");
        check(pacer.Observe(5001, new[] { ("CopperOre", 13, 10) }, why).Count == 0 && pacer.Observe(5002, new[] { ("CopperOre", 40, 10), ("TinOre", 41, 12) }, why).Count == 2,
            "the vein's next tiles do not call again; a separate vein and a valuable kind do");
        check(pacer.Observe(5003, new[] { ("PlayerGrave", 1, 1) }, why).Count == 0, "an object that was never a gather choice never calls");
        pacer.Sync(2); check(pacer.IntervalOf("WallDirtBlock") == TravelResourcePacer.FirstInterval, "a new goal/plan revision restarts every kind");

        // Compose while moving: due/relevant kinds are choices, a common kind in its interval is not.
        Harness.Reset(); var w = Harness.World; var p = Harness.Player; var o = new AIObservation();
        AIGoals.SetGoal(w, p, GoalKind.Explore, ObjectID.None, "탐험", 1, "pacing-test");
        UnityEngine.Time.realtimeSinceStartup = 100;
        var dirtChoice = new AICandidate { kind = "gather_group", key = "gather:dirt", itemId = ObjectID.WallDirtBlock, desc = "흙벽 묶음", gather = new AIGatherSelection() };
        var oreChoice = new AICandidate { kind = "gather_group", key = "gather:ore", itemId = ObjectID.CopperOre, desc = "구리 광석 묶음", gather = new AIGatherSelection() };
        var turn = new AICandidate { kind = "travel_turn", key = "travel:turn:1:j:(0,1)", desc = "갈림길" };
        var moving = AIExpedition.Compose(w, p, o, new List<AICandidate> { dirtChoice, oreChoice, turn }, null, true);
        check(!moving.Contains(dirtChoice) && moving.Contains(oreChoice) && moving.Contains(turn), "while moving, a common kind in its interval is held back; ore and the branch stay");
        var unknown = new AICandidate { kind = "gather_group", key = "gather:unknown", itemId = ObjectID.None, desc = "품목 모름", gather = new AIGatherSelection() };
        check(AIExpedition.Compose(w, p, o, new List<AICandidate> { unknown, turn }, null, true).Contains(unknown), "검수 F2b: a choice without a known item is never held back while moving");
        var halted = AIExpedition.Compose(w, p, o, new List<AICandidate> { dirtChoice, oreChoice, turn }, null, false);
        check(halted.Contains(dirtChoice), "at a halt or outside travel every resource choice is offered as before");
        UnityEngine.Time.realtimeSinceStartup = 140;
        var due = AIExpedition.TravelSightings(140, new[] { ("WallDirtBlock", 1, 1) });
        moving = AIExpedition.Compose(w, p, o, new List<AICandidate> { dirtChoice, turn }, null, true);
        check(due.Count == 1 && moving.Contains(dirtChoice), "a due common kind seen again calls and is offered");

        IdleGate(check);

        // Offline count (one 120 s straight-corridor walk with dirt always in view, never chosen, one plan-ore vein at 40 s,
        // one valuable vein at 100 s): calls caused by sightings.
        var sim = new TravelResourcePacer(); sim.Sync(1); sim.Learn("WallDirtBlock"); int calls = 0;
        for (float t = 0; t <= 120; t += 0.5f)
        {
            var seen = new List<(string, int, int)> { ("WallDirtBlock", (int)t, 1) };
            if (t >= 40 && t < 45) seen.Add(("CopperOre", 40 + (int)(t - 40), 2));
            if (t >= 100 && t < 102) seen.Add(("TinOre", 100, 2));
            var r = sim.Observe(t, seen, why); if (r.Count > 0) { calls++; sim.Sent(new[] { "WallDirtBlock" }); sim.Answered(t + 0.3f, null); }
        }
        Harness.Trace.Add("TRAVEL PACING SIM: sighting calls in 120 s = " + calls);
        Console.WriteLine("TRAVEL PACING SIM: sighting calls in 120 s = " + calls + " (dirt at 30 s and 90 s, ore vein at 40 s, tin at 100 s)");
        check(calls == 4, "120 s corridor: 2 common-kind calls (30 s, then 60 s later) + 1 per relevant vein");
    }

    // 검수 B1 (2026-11-08): the no-goal idle gate decides by flags, not by the reason sentence. Free time was removed.
    private static void IdleGate(Action<bool, string> check)
    {
        bool Hold(bool running = false, bool inCombat = false, bool emergency = false, bool urgent = false, bool issue = false, bool tree = false, bool menu = false)
            => AIRunGate.IdleHold(running, true, inCombat, emergency, urgent, issue, tree, menu);
        check(AIRunGate.IsIdleIssue("자기 방어") && AIRunGate.IsIdleIssue("쓰러졌다가") && AIRunGate.IsIdleIssue("긴급")
            && !AIRunGate.IsIdleIssue("적 사라짐") && !AIRunGate.IsIdleIssue("위협이 보이지 않음") && !AIRunGate.IsIdleIssue("자유 시간 시작") && !AIRunGate.IsIdleIssue("할 일 없음"),
            "issue trigger list: back up and emergency kept; enemy gone, threat gone and free time removed (user decisions)");
        check(Hold() && !AIRunGate.IdleHold(false, false, false, false, false, false, false, false),
            "idle with no goal and no issue holds the call; with a goal the ordinary rules apply");

        // Scenario 1 (review): an issue call fails (network) and is retried with the reason "재시도(...)".
        Harness.Reset();
        AIBrain.MarkIssue("자기 방어");
        // CallFailed rewrites only the reason; the flag is cleared only when an answer is taken (HandleResponse).
        check(AIBrain._issuePending && !Hold(issue: AIBrain._issuePending), "scenario 1: a failed issue call is retried, not dropped by the gate");

        // Scenario 2 (review): idle with no goal, the model enters the bag menu.
        Harness.Reset(); AIInventoryMenu.Reset();
        AIBrain.ApplyMenu(Harness.World, Harness.Player, 1, "enter");
        check(!Hold(tree: AIBrain._treeStepNext, menu: AIInventoryMenu.Active), "scenario 2: the next bag-menu step is asked (tree step / open menu pass the gate)");
        AIInventoryMenu.Reset();

        // Scenario 3 (review): back up after being down while an unasked execution observation overwrites the reason.
        Harness.Reset();
        AIBrain.MarkIssue("쓰러졌다가");
        AIBrain.ObserveExecution("통로 탐색: 기술 끝");   // the reason sentence is now the execution observation
        check(!Hold(issue: AIBrain._issuePending), "scenario 3: the back-up issue survives the reason being overwritten");
        AIBrain.ResetIdleEngagement();
        check(!AIBrain._issuePending && Hold(), "a new goal clears the issue flag; afterwards the idle gate holds again");
        Harness.Reset();
    }

    // 2026-11-08 이동·전달 개선 1/4: same-exploration choices are not leaving; only plan items (with recipe
    // ingredients) and valuable kinds are reported automatically, each opportunity once until reviewed.
    private static void LeaveReportRelevance(Action<bool, string> check)
    {
        Harness.Reset(); AIPlanner.ResetTransport();
        var w = Harness.World; var p = Harness.Player; var o = new AIObservation();
        AIReportRelevance.Info = item => item == "CopperBreastArmor" ? new AIReportRelevance.ItemFacts { Type = "BreastArmor", Rarity = 1, Requires = new[] { "CopperBar" } }
            : item == "CopperBar" ? new AIReportRelevance.ItemFacts { Type = "NonUsable", Requires = new[] { "CopperOre" } }
            : item == "Wood" || item == "WallDirtBlock" ? new AIReportRelevance.ItemFacts { Type = "NonUsable" } : null;
        try
        {
            AIGoals.SetGoal(w, p, GoalKind.Explore, ObjectID.None, "탐험", 1, "relevance-test");
            AIPlanner.Cur.Plan.Set("relevance-test", new List<PlanStep> { new PlanStep("explore", null, 1), new PlanStep("craft", "CopperBreastArmor", 1) });
            check(AIReportRelevance.PlanItems(new[] { "CopperBreastArmor" }).SetEquals(new[] { "CopperBreastArmor", "CopperBar", "CopperOre" }), "plan items include recipe ingredients down to ore");
            var dirt = new AICandidate { kind = "mine", key = "mine:1,1", tile = new int2(1, 1), itemId = ObjectID.WallDirtBlock, desc = "흙벽" };
            var wood = new AICandidate { kind = "root", key = "root:3,3", tile = new int2(3, 3), itemId = ObjectID.Wood, desc = "나무뿌리" };
            var ore = new AICandidate { kind = "mine", key = "mine:2,2", tile = new int2(2, 2), itemId = ObjectID.CopperOre, desc = "구리 광석 벽" };
            var tin = new AICandidate { kind = "pickup", key = "pickup:4,4", tile = new int2(4, 4), itemId = ObjectID.TinOre, itemCount = 2, desc = "주석 광석" };
            AIExpedition.Compose(w, p, o, new List<AICandidate> { dirt, wood, ore });
            foreach (var inRun in new[] {
                new AICandidate { kind = "travel_turn", key = "travel:turn:1:j:(0,1)", tile = new int2(0, 1) },
                new AICandidate { kind = "continue", key = "travel:explore:0,0" },
                new AICandidate { kind = "explore", key = "travel:restart:1:(0,1)", tile = new int2(0, 1) },
                new AICandidate { kind = "explore", key = "travel:reverse:1", tile = new int2(-1, 0) } })
                AIExpedition.Choosing(w, p, inRun);
            check(AIPlanner.Replans == 0, "same-exploration turn, continue, restart and reverse are not reported as leaving the site");
            var depart = new AICandidate { kind = "explore", key = "travel:explore:0,0:(1,0)", tile = new int2(1, 0) };
            AIExpedition.Choosing(w, p, depart);
            string Events()
            {
                using (var sent = System.Text.Json.JsonDocument.Parse(AIPlanner.LastRequestBody))
                {
                    var all = new List<string>();
                    foreach (var e in sent.RootElement.GetProperty("report").GetProperty("events").EnumerateArray()) all.Add(e.GetString());
                    return string.Join(" / ", all);
                }
            }
            string first = AIPlanner.Replans == 1 ? Events() : "";
            check(first.Contains("CopperOre@2,2") && first.Contains("CopperOre=계획 품목") && !first.Contains("WallDirtBlock") && !first.Contains("Wood"),
                "a new departure reports the plan-material ore only, not common dirt or an unrelated root");
            AIPlanner.CompleteTransport();
            AIExpedition.Compose(w, p, o, new List<AICandidate> { dirt, wood, ore, tin });
            AIExpedition.Choosing(w, p, depart);
            AIPlanner.TickPending(w, p, 5);
            string second = AIPlanner.Replans == 2 ? Events() : "";
            check(second.Contains("TinOre@4,4") && second.Contains("TinOre=광석") && !second.Contains("CopperOre@2,2"),
                "a changed opportunity set reports only the newly seen valuable item, not the already reported ore again");
            // 검수 F4: one fact per resource — a group over a reported tile, a changed pile count, the same tile as a member
            AIPlanner.CompleteTransport();
            var group = new AICandidate { kind = "gather_group", key = "gather:mine:2,2|mine:2,3", tile = new int2(2, 2), itemId = ObjectID.CopperOre,
                resources = new[] { "CopperOre@2,2", "CopperOre@2,3" }, desc = "구리 묶음", gather = new AIGatherSelection() };
            var tinMore = new AICandidate { kind = "pickup", key = "pickup:4,4", tile = new int2(4, 4), itemId = ObjectID.TinOre, itemCount = 5, desc = "주석 광석" };
            AIExpedition.Compose(w, p, o, new List<AICandidate> { ore, group, tinMore });
            AIExpedition.Choosing(w, p, depart);
            AIPlanner.TickPending(w, p, 6);
            string third = AIPlanner.Replans == 3 ? Events() : "";
            check(third.Contains("CopperOre@2,3") && !third.Contains("CopperOre@2,2") && !third.Contains("TinOre@4,4"),
                "a group over an already reported tile and a changed pile count report only the new tile");
            // user decision: the upper LLM can remove a kind from the valuable set (kept until changed)
            AIPlanner.CompleteTransport();
            check(AIExpedition.SetValuable(new Dictionary<string, bool> { ["TinOre"] = false, ["NoSuchItem"] = true }) == "TinOre-", "valuable-kind change applies known items only");
            var tinFar = new AICandidate { kind = "pickup", key = "pickup:9,9", tile = new int2(9, 9), itemId = ObjectID.TinOre, itemCount = 1, desc = "주석 광석" };
            AIExpedition.Compose(w, p, o, new List<AICandidate> { tinFar });
            int before = AIPlanner.Replans; AIExpedition.Choosing(w, p, depart); AIPlanner.TickPending(w, p, 7);
            check(AIPlanner.Replans == before, "a kind the upper LLM removed is no longer reported");
            AIExpedition.SetValuable(new Dictionary<string, bool> { ["TinOre"] = true });
            // the change is kept on disk per character/world and read back after a reconnect (new expedition state)
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "valuable-" + Guid.NewGuid().ToString("N") + ".json");
            AIExpedition.ValuablePath = () => file;
            try
            {
                AIExpedition.SetValuable(new Dictionary<string, bool> { ["CopperOre"] = false });
                AIPlayerSpikeMod.WorldId = "other"; AIExpedition.Observe(w, p); AIPlayerSpikeMod.WorldId = "test"; AIExpedition.Observe(w, p);
                bool kept = AIExpedition.SetValuable(new Dictionary<string, bool>()) == "" && System.IO.File.ReadAllText(file).Contains("\"CopperOre\":false");
                var after = new AICandidate { kind = "mine", key = "mine:50,50", tile = new int2(50, 50), itemId = ObjectID.CopperOre, desc = "구리" };
                AIGoals.SetGoal(w, p, GoalKind.Explore, ObjectID.None, "탐험", 1, "reload-test"); AIPlanner.Cur.Plan.Set("reload-test", new List<PlanStep> { new PlanStep("explore", null, 1) });
                AIPlanner.CompleteTransport(); int r0 = AIPlanner.Replans;
                AIExpedition.Compose(w, p, o, new List<AICandidate> { after }); AIExpedition.Choosing(w, p, depart); AIPlanner.TickPending(w, p, 8);
                check(kept && AIPlanner.Replans == r0, "a removed kind stays removed after a reconnect (read back from the character's file)");
                System.IO.File.WriteAllText(file, "{broken");
                AIPlayerSpikeMod.WorldId = "other"; AIExpedition.Observe(w, p); AIPlayerSpikeMod.WorldId = "test"; AIExpedition.Observe(w, p);
                AIExpedition.Compose(w, p, o, new List<AICandidate> { after }); AIExpedition.Choosing(w, p, depart); AIPlanner.TickPending(w, p, 9);
                check(AIPlanner.Replans == r0 + 1, "a broken file falls back to the default kinds (copper ore valuable again)");
            }
            finally { AIExpedition.ValuablePath = () => null; try { System.IO.File.Delete(file); } catch { } }
        }
        finally { AIReportRelevance.Info = _ => null; AIPlanner.ResetTransport(); }
    }
#endif
}
