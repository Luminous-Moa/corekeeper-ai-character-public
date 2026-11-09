using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

public static class BrainCases
{
    private sealed class StructuredResult : ProbeSkill,ILifeResult
    {
        public int ConfirmedWork {get;set;}public int ConfirmedReceived {get;set;}public int RequestedWork {get;set;}public string ResultStage=>"adapter observed result boundary";
    }
    private static void RecordResult(FarmFixture f,LifeSelection selection,int run,int work,int received)
    {
        var skill=new StructuredResult{Run=run,RequestedWork=Math.Max(work,received),ConfirmedWork=work,ConfirmedReceived=received,Status=SkillStatus.Done};
        AILife.Starting(new AICandidate{life=selection},skill);AIBrain.OnSkillEnded(skill,Time.realtimeSinceStartup);
    }
    private static void SharedUnspecifiedCount()
    {
        AIBrain.ResetAdapter();var f=FarmFixture.Create(6,4,6);
        var step=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"farm\",\"mode\":\"plant\",\"where\":\"rect:3,0,8,0\",\"count\":6}]}}").Answer.Plan[0];
        AIPlanner.SetStep(f.World,f.Player,step);
        Program.Check(AIGoals.Goal.item==ObjectID.None&&AIGoals.Goal.count==6&&AIGoals.Goal.mode=="plant","unspecified plant item survives actual Protocol/SetGoalFrom/Goal as a shared six-tile request");
        AIBody.StartSkill(f.World,f.Player,new ProbeSkill(),Time.realtimeSinceStartup);AIBrain.SeedPending(AIBody.Current);
        Nav(f,"open");Nav(f,"branch","farm");Nav(f,"mode","plant");Nav(f,"item","SeedA");Nav(f,"where","farm");
        var page=Page(f);var a=page.First(c=>c.kind=="farm");AIBrain.Response(f.World,f.Player,page,a);var first=AIBody.Current;f.Drive(first);AIBrain.OnSkillEnded(first,Time.realtimeSinceStartup);
        Program.Check(((ILifeResult)first).ConfirmedWork==4&&f.ActualInputs==4,"four SeedA plant inputs confirm four of shared request");
        Nav(f,"root");Nav(f,"branch","farm");Nav(f,"mode","plant");Nav(f,"item","SeedB");Nav(f,"where","farm");
        page=Page(f);var b=page.First(c=>c.kind=="farm");
        Program.Check(b.life.MatchesGoal&&b.life.RequestedCount==2&&b.itemCount==2,"SeedB candidate receives shared remaining two after SeedA four, rather than a new six quota");
        AIBrain.Response(f.World,f.Player,page,b);var second=AIBody.Current;f.Drive(second);AIBrain.OnSkillEnded(second,Time.realtimeSinceStartup);
        var qa=AILife.ForGoal(AIGoals.Goal);qa.Item="SeedA";var qb=AILife.ForGoal(AIGoals.Goal);qb.Item="SeedB";
        Program.Check(((ILifeResult)second).ConfirmedWork==2&&f.ActualInputs==6&&AILife.RequestSatisfied(qa)&&AILife.RequestSatisfied(qb),"SeedB two completes shared count and both narrowed seed selections are satisfied");
        Program.Check(AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",qa)==null&&AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",qb)==null&&!Page(f).Exists(c=>c.kind=="farm"),"satisfied shared count prevents either seed's direct or tree execution leaf");

        var cook=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"cook\",\"mode\":\"put\",\"item\":\"FoodA\",\"count\":6}]}}").Answer.Plan[0];
        AIPlanner.SetStep(f.World,f.Player,cook);var pairA=AILife.ForGoal(AIGoals.Goal);pairA.Second="FoodB";RecordResult(f,pairA,101,4,0);
        var pairB=AILife.ForGoal(AIGoals.Goal);pairB.Second="FoodA";
        Program.Check(pairB.RequestedCount==2&&AILife.Remaining(pairB)==2,"actual AILife cook put shares count across unspecified second ingredient at structured result boundary");
        RecordResult(f,pairB,102,2,0);Program.Check(AILife.RequestSatisfied(pairA)&&AILife.RequestSatisfied(pairB),"different second ingredient results fulfill one six-pair cook put goal");
        var take=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"cook\",\"mode\":\"take\",\"count\":6}]}}").Answer.Plan[0];
        AIPlanner.SetStep(f.World,f.Player,take);var dishA=AILife.ForGoal(AIGoals.Goal);dishA.Item="FoodA";RecordResult(f,dishA,103,0,4);
        var dishB=AILife.ForGoal(AIGoals.Goal);dishB.Item="FoodB";Program.Check(dishB.RequestedCount==2,"cook take with unspecified output shares confirmed receipt count across dish types");
        RecordResult(f,dishB,104,0,2);Program.Check(AILife.RequestSatisfied(dishA)&&AILife.RequestSatisfied(dishB),"two output types fulfill one six-dish receipt goal without separate quotas");
        var normalized=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"cook\",\"mode\":\"put\",\"item\":\"SeedA\",\"second\":\"seedb\",\"count\":1}]}}").Answer.Plan[0];
        AIPlanner.SetStep(f.World,f.Player,normalized);Program.Check(AIGoals.Goal.second=="SeedB","actual Protocol -> SetGoalFrom -> AIGoals normalizes case-insensitive second item identifier");
    }
    private static void ExplicitGatherCeiling()
    {
        AIBrain.ResetAdapter();var f=FarmFixture.Create(6,4,6);
        var step=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"gather\",\"item\":\"SeedA\",\"where\":\"tile:3,4\",\"count\":9,\"max_count\":10,\"max_order\":17}]}}").Answer.Plan[0];
        AIPlanner.SetStep(f.World,f.Player,step);
        Program.Check(AIGoals.Goal.kind==GoalKind.Gather&&AIGoals.Goal.count==9&&AIGoals.Goal.maxCount==10&&AIGoals.Goal.maxOrder==17&&AIGoals.Goal.where=="tile:3,4","actual Protocol -> Planner.SetGoalFrom -> Goals keeps explicit ceiling, owner identity, target and destination");
        AIPlanner.SetStep(f.World,f.Player,new PlanStep("gather","SeedA",1000));
        Program.Check(AIGoals.Goal.count==1000&&AIGoals.Goal.maxCount==-1,"ordinary gather target creates no default upper bound through production goal setter");
    }
    static List<AICandidate> Page(FarmFixture f,bool combat=false,AICandidate direct=null)
    {
        var old=new List<AICandidate> {
            new AICandidate{kind="continue",key="probe",value="c1",desc="continue probe",run=AIBody.Current?.Run??0},
            new AICandidate{kind="eat",key="danger-eat",value="c2",desc="danger eating"},
            new AICandidate{kind="doorclose",key="door",value="c3",desc="close own door"} };
        for(int i=0;i<14;i++)old.Add(new AICandidate{kind="mine",key="old:"+i,value="c"+(i+4),desc="old candidate"});
        var list=AIBrain.AssembleLifePage(f.World,f.Player,f.Observation,AIBody.Current?.Key??"",combat,old,direct);
        AIBrain.AddCommonControls(list,false,AIBody.Current);return list;
    }
    static AICandidate Pick(List<AICandidate> page,string op,string id=null)=>page.First(c=>c.lifeNav?.Op==op&&(id==null||c.lifeNav.Value==id||c.lifeNav.Value.StartsWith(id,StringComparison.Ordinal)));
    static void Nav(FarmFixture f,string op,string id=null,bool prefetch=false)
    {
        var page=Page(f);var choice=Pick(page,op,id);var body=AIBody.Current;var goal=AIGoals.Goal;var held=AIBrain.HeldChoice;var queued=AIBrain.QueuedChoice;int starts=AIBody.Starts,stops=AIBody.Stops,applied=AIBrain.Applied,answers=AIBrain.StartedAnswers;
        AIBrain.Response(f.World,f.Player,page,choice,prefetch);
        Program.Check(ReferenceEquals(body,AIBody.Current)&&ReferenceEquals(goal,AIGoals.Goal)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice)&&starts==AIBody.Starts&&stops==AIBody.Stops&&applied==AIBrain.Applied&&answers==AIBrain.StartedAnswers,"production HandleResponse read-only "+op+" preserves body, pending choices, goal and execution counters"+(prefetch?" in prefetch":""));
    }
    static FarmFixture Setup(int plots=4,int request=6)
    {
        AIBrain.ResetAdapter();var f=FarmFixture.Create(plots,20,request);
        string json="{\"answer\":{\"plan\":[{\"do\":\"farm\",\"mode\":\"plant\",\"item\":\"SeedA\",\"where\":\"rect:3,0,"+(3+plots-1)+",0\",\"count\":"+request+"}]}}";
        var step=AIPlanProtocol.Read(json).Answer.Plan[0];AIPlanner.SetStep(f.World,f.Player,step);
        Program.Check(AIGoals.Goal.mode=="plant"&&AIGoals.Goal.item==ObjectID.SeedA&&AIGoals.Goal.count==request&&AIGoals.Goal.where==step.Where,"actual Protocol -> PlanStep -> extracted SetGoalFrom -> production AIGoals retains selectors and count");
        AIBody.StartSkill(f.World,f.Player,new ProbeSkill(),Time.realtimeSinceStartup);AIBrain.SeedPending(AIBody.Current);return f;
    }
    public static void Run()
    {
        var f=Setup();Nav(f,"open",prefetch:true);Nav(f,"branch","farm");Nav(f,"mode","plant");Nav(f,"item","SeedA");
        var area=Page(f);var region=area.First(c=>c.lifeNav?.Value.StartsWith("region:",StringComparison.Ordinal)==true);
        Program.Check(region.desc.Contains("4"),"rect-filtered region reports four actual own tiles");
        Nav(f,"where",region.lifeNav.Value);
        var leaf=Page(f);var execute=leaf.First(c=>c.kind=="farm");
        var direct=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));
        Program.Check(execute.key==direct.key&&execute.farm.Item.objectID==direct.farm.Item.objectID&&execute.tiles.SequenceEqual(direct.tiles),"direct and tree use the same ExactCandidate payload, quantity and identity");
        Program.Check(execute.itemCount==4&&execute.life.RequestedCount==6&&execute.desc.Contains("부족 2"),"tree leaf offers four of requested six with shortage two");
        Program.Check(leaf.Exists(c=>c.kind=="stop_action")&&leaf.Exists(c=>c.kind=="finish_goal")&&leaf.Exists(c=>c.kind=="request_plan")&&leaf.Exists(c=>c.kind=="continue")&&leaf.Exists(c=>c.kind=="eat")&&leaf.Exists(c=>c.kind=="doorclose"),"open page preserves continue, danger responses and all common controls beyond original candidate cut");
        AIBrain.Response(f.World,f.Player,leaf,execute);
        var skill=AIBody.Current;
        Program.Check(skill is FarmSkill&&skill.Key==execute.key&&skill.Status==SkillStatus.Running,"production response Apply -> AIActions.Make -> FarmSkill.Start starts bound action");
        f.Drive(skill);AIBrain.OnSkillEnded(skill,Time.realtimeSinceStartup);
        Program.Check(f.ActualInputs==4&&((ILifeResult)skill).ConfirmedWork==4&&AILife.ForGoal(AIGoals.Goal).RequestedCount==2,"four physical boundary changes become four confirmed work and remaining two");
        Program.Check(AILife.Report().Contains("확인된 작업량 4")&&AILife.Report().Contains("잔량 2")&&AIBrain.ExecutionObservation.Contains("잔량 2"),"actual OnSkillEnded delivers structured work to report and next decision observation");
        var after=Page(f);Program.Check(!after.Exists(c=>c.kind=="farm")&&AILife.Report().Contains("현재 적합한"),"fresh observation has no unmodified tiles and explains remaining shortage");
        AIBrain.OnSkillEnded(skill,Time.realtimeSinceStartup);Program.Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==2,"same end notification does not count receipt twice");

        f=Setup(12,12);Nav(f,"open");Nav(f,"branch","farm");Nav(f,"mode","plant");Nav(f,"item","SeedA");Nav(f,"where","tiles");
        var tiles=Page(f);Program.Check(tiles.Count(c=>c.lifeNav?.Op=="where")>=12&&!tiles.Exists(c=>c.lifeNav?.Op=="page"),"specific-tile depth exposes all12plus entries together without page truncation");
        int revision=AILife.Cursor.Revision;Page(f);Program.Check(AILife.Cursor.Revision==revision,"redrawing identical depth does not change cursor revision");
        var sixth=tiles.Where(c=>c.lifeNav?.Op=="where").Skip(5).First();Nav(f,"where",sixth.lifeNav.Value);Program.Check( Page(f).Exists(c=>c.kind=="farm"),"sixth tile selection directly reaches actual farm execution leaf");
        Nav(f,"back");Program.Check(AILife.Cursor.Where==""&&AILife.Cursor.Item.StartsWith("SeedA",StringComparison.Ordinal)&&AILife.Cursor.Page==0,"production back removes last selected scope and resets page only");
        Nav(f,"root");Program.Check(AILife.Cursor.Open&&AILife.Cursor.Branch==""&&AILife.Cursor.Page==0,"production root clears path and page");Nav(f,"close");Program.Check(!AILife.Cursor.Open,"production close clears cursor without stopping probe");

        f=Setup();Nav(f,"open");var root=Page(f);var old=Pick(root,"branch","farm");AIBrain.Response(f.World,f.Player,root,Pick(root,"branch","cook"));int cursor=AILife.Cursor.Revision;var probe=AIBody.Current;var held=AIBrain.HeldChoice;var queued=AIBrain.QueuedChoice;
        AIBrain.Response(f.World,f.Player,root,old,true);Program.Check(AILife.Cursor.Revision==cursor&&AILife.Cursor.Branch=="cook"&&ReferenceEquals(probe,AIBody.Current)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice),"late prefetch nav cannot overwrite newer path or pending body choices");
        var owner=AIAgent.Cur;AIAgent.Cur=new AIAgent{Id="b"};Program.Check(AILife.NavStale(old.lifeNav)!=null,"another character cannot consume old navigation token");AIAgent.Cur=owner;
        var oldGoal=AIGoals.Goal;int oldRevision=AIBrain.Revision;AIPlanner.SetStep(f.World,f.Player,new PlanStep("farm","SeedB",2,"farm",mode:"plant"));Program.Check(!AILife.Cursor.Open&&AILife.NavStale(old.lifeNav)!=null&&!ReferenceEquals(oldGoal,AIGoals.Goal),"actual goal replacement closes cursor and rejects old token");
        cursor=AILife.Cursor.Revision;probe=AIBody.Current;AIBrain.Response(f.World,f.Player,root,old,true,oldRevision);Program.Check(!AILife.Cursor.Open&&AILife.Cursor.Revision==cursor&&ReferenceEquals(probe,AIBody.Current),"production request revision check prevents old-goal navigation even during prefetch");

        f=Setup();Nav(f,"open");direct=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));var withDirect=Page(f,false,direct);
        Program.Check(withDirect.Count(c=>c.key==direct.key)==1&&withDirect.Exists(c=>c.lifeNav?.Op=="branch"&&c.lifeNav.Value=="farm")&&withDirect.Select(c=>c.value).Distinct().Count()==withDirect.Count,"direct bound execution survives separately from full tree page with unique stable values");
        Nav(f,"branch","farm");Nav(f,"mode","plant");Nav(f,"item","SeedB");Nav(f,"where","farm");leaf=Page(f);execute=leaf.First(c=>c.kind=="farm");
        Program.Check(!execute.life.MatchesGoal&&execute.life.Goal==null&&execute.itemCount==1&&execute.desc.Contains("원래 단계 밖"),"different seed is one explicit separate action with no original-goal binding");
        AIBrain.Response(f.World,f.Player,leaf,execute);skill=AIBody.Current;f.Drive(skill);AIBrain.OnSkillEnded(skill,Time.realtimeSinceStartup);Program.Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==6&&((ILifeResult)skill).ConfirmedWork==1,"confirmed work on alternate seed never advances original six-seed stage");

        f=Setup();Nav(f,"open");var before=Page(f);var farm=Pick(before,"branch","farm");cursor=AILife.Cursor.Revision;FarmWorld.Enemies.Add(new SeenEnemy{dist=1});var combat=Page(f,true);
        Program.Check(combat.Exists(c=>c.lifeNav!=null)&&AILife.Cursor.Revision==cursor,"combat retains cursor and all supported life navigation");
        AIBrain.Response(f.World,f.Player,before,farm);Program.Check(AILife.Cursor.Branch=="farm","read-only navigation answer entering combat executes after actual stale validation");FarmWorld.Enemies.Clear();Program.Check(AILife.Cursor.Branch=="farm","combat return redraws fresh life path");
        probe=AIBody.Current;held=AIBrain.HeldChoice;queued=AIBrain.QueuedChoice;var goal=AIGoals.Goal;int applied=AIBrain.Applied,started=AIBrain.StartedAnswers;
        foreach(long code in new[]{429L,500L,401L})AIBrain.Response(f.World,f.Player,Page(f),null,false,null,new AIDecisionsApi.Reply{Ok=false,Code=code,ErrorMessage="adapter API failure"});
        Program.Check(ReferenceEquals(probe,AIBody.Current)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice)&&ReferenceEquals(goal,AIGoals.Goal)&&applied==AIBrain.Applied&&started==AIBrain.StartedAnswers,"actual rate-limit/server/client failures preserve action, pending choices and goal");
        double oldUsd=AIBrain.Usage().usd;AIBrain.Usage().usd=AIBrain.Settings().monthlyCapUsd-0.001;
        Program.Check(AIBrain.CheckCost(),"production CostAllows permits new decisions just below monthly cap");
        AIBrain.Usage().usd=AIBrain.Settings().monthlyCapUsd;int costStarts=AIBody.Starts,costStops=AIBody.Stops;
        Program.Check(!AIBrain.CheckCost()&&AIBrain.RetryAt==Time.realtimeSinceStartup+30f,"production CostAllows suspends new decisions at monthly cap with retry delay");
        Program.Check(ReferenceEquals(probe,AIBody.Current)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice)&&ReferenceEquals(goal,AIGoals.Goal)&&applied==AIBrain.Applied&&started==AIBrain.StartedAnswers&&costStarts==AIBody.Starts&&costStops==AIBody.Stops,"monthly cap preserves selected probe, held/queued, goal and execution counters");
        AIBrain.Usage().usd=oldUsd;
        var stale=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(goal));var bag=f.World.EntityManager.GetBuffer<ContainedObjectsBuffer>(f.Player);bag[0]=FarmFixture.Item(ObjectID.SeedB,20);int starts=AIBody.Starts;AIBrain.ApplyChosen(f.World,f.Player,stale);
        Program.Check(starts==AIBody.Starts&&ReferenceEquals(probe,AIBody.Current),"production Apply revalidation discards replaced selected seed without substitution");
        AIGoals.ClearGoal(new GoalEnd("test stop","cancelled"));Program.Check(Page(f).Exists(c=>c.lifeNav!=null),"None goal retains supported life navigation");
        SharedUnspecifiedCount();
        ExplicitGatherCeiling();
    }
}



