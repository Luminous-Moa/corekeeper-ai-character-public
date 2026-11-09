using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

internal static class Program
{
    private static int checks;
    private static readonly Entity Player = new Entity { Index=1, Version=1 };
    private static AIGatherTarget Target(string key,int x,string kind="mine") => new AIGatherTarget(kind,key,kind+":copper",key,new int2(x,0),new Entity{Index=x+2,Version=1},ObjectID.CopperOre,0,0,false,0,true);
    private static void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
    private static AIGatherSkill Start(FakeWorld world, params AIGatherTarget[] targets)
    { var skill=new AIGatherSkill(new AIGatherSelection(Player,"selected",targets),world); Check(skill.Start(new World(),Player,0),"start"); return skill; }
    private static void Run(AIGatherSkill s, float until=40)
    { for(float t=0;t<until && s.Status==SkillStatus.Running;t+=1) s.Tick(new World(),Player,t); }
    public static int Main()
    {
        ExplicitLimits();
        ActiveOrderLimits();
        DiscoveryWatch();
        ProductionCandidates();
        var targets=new List<AIGatherTarget>{Target("a",0),Target("b",1),Target("c",4),Target("d",1,"break")};
        var groups=AIGatheringRules.Components(targets,t=>t.Family,t=>t.Tile.x,t=>t.Tile.y);
        Check(groups.Count==3 && groups[0].Count==2,"only same family touching cells join");
        targets.Clear(); for(int i=0;i<301;i++)targets.Add(Target(i.ToString(),i));
        Check(AIGatheringRules.Components(targets,t=>t.Family,t=>t.Tile.x,t=>t.Tile.y)[0].Count==301,"no 30-target cap");
        var selection=new AIGatherSelection(Player,"fixed",targets);
        targets.Clear(); var exposed=selection.Targets; exposed[0]=Target("replacement",999);
        Check(selection.Count==301 && selection.Targets[0].Key=="0","selection is copied and immutable to caller");
        var world=new FakeWorld(); world.Alive.Add("a"); world.Alive.Add("b");
        var s=Start(world,Target("a",0),Target("b",1));
        world.Alive.Add("newly-visible"); Run(s);
        Check(s.Status==SkillStatus.Done && s.CompletedTargets==2,"selected targets complete");
        Check(world.Trace.Count==4 && world.Trace[0]=="act:a" && world.Trace[1]=="recover:a" && world.Trace[2]=="act:b" && world.Trace[3]=="recover:b","actual sequence approach/action then recovery without reselect");
        Check(world.Alive.Contains("newly-visible") && s.Received[ObjectID.CopperOre]==4,"new target excluded and actual item counts independent of target count");
        Check(world.Cleanups==4,"child resources disposed once");
        world=new FakeWorld(); world.Alive.Add("a");world.Alive.Add("b");
        s=Start(world,Target("a",0),Target("b",1));
        s.Tick(new World(),Player,0);s.Tick(new World(),Player,1);
        var rest=s.RemainingSelection; s.Cleanup(new World()); int prior=world.Trace.Count;
        s.Tick(new World(),Player,20);
        Check(world.Trace.Count==prior,"interrupted group never starts another child");
        Check(rest.Count==2,"interrupted active target retained in snapshot");
        var resumed=new AIGatherSkill(rest,world); resumed.Start(new World(),Player,20);Run(resumed);
        Check(resumed.Status==SkillStatus.Done && world.Trace.Contains("act:b"),"resume safely skips already gone target and executes remaining snapshot");
        world=new FakeWorld();world.Alive.Add("b");
        s=Start(world,Target("gone",0),Target("b",1));Run(s);
        Check(s.CompletedTargets==1 && s.Reason.Contains("변경 1"),"missing targets are not counted as successful harvest");
        world=new FakeWorld{Fail="a"};world.Alive.Add("a");world.Alive.Add("b");
        s=Start(world,Target("a",0),Target("b",1));Run(s);
        Check(s.Status==SkillStatus.Failed && !world.Trace.Contains("act:b"),"failed action returns evidence without selecting a different target");
        world=new FakeWorld{Give=0};world.Alive.Add("a");
        s=Start(world,Target("a",0));Run(s);
        Check(s.CompletedTargets==1 && s.Received.Count==0,"destroyed target does not imply received inventory");
        world=new FakeWorld{LeavePickup=true};world.Alive.Add("a");
        s=Start(world,Target("a",0,"pickup"));Run(s);
        Check(s.Status==SkillStatus.Failed && s.CompletedTargets==0,"pickup that remains on ground is not claimed collected");
        var previous=new Dictionary<ObjectID,int>{{ObjectID.CopperOre,10}};
        var received=new Dictionary<ObjectID,long>();
        AIGatheringRules.Observe(previous,new Dictionary<ObjectID,int>{{ObjectID.CopperOre,12}},received);
        AIGatheringRules.Observe(previous,new Dictionary<ObjectID,int>{{ObjectID.CopperOre,1}},received);
        AIGatheringRules.Observe(previous,new Dictionary<ObjectID,int>{{ObjectID.CopperOre,4}},received);
        Check(received[ObjectID.CopperOre]==5,"observed gain survives spending and accumulates next pickup");
        Console.WriteLine($"PASS {checks} production gathering sequence checks");return 0;
    }
    private static void ExplicitLimits()
    {
        AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.CopperOre,count=10,maxCount=10};
        AIBody.Bag.Clear();AIBody.Bag[ObjectID.CopperOre]=9;
        var world=new FakeWorld{Give=1};world.Bag[ObjectID.CopperOre]=9;
        var targets=new List<AIGatherTarget>();
        for(int i=0;i<5;i++)
        { string key="drop"+i;world.Alive.Add(key);targets.Add(new AIGatherTarget("pickup",key,"copper",key,new int2(i,0),default,ObjectID.CopperOre,0,0,false,0,true,1)); }
        var skill=Start(world,targets.ToArray());Run(skill,20);
        Check(skill.Status==SkillStatus.Running&&skill.CompletedTargets==1&&world.Trace.Count==1,"explicit 9/10 ceiling rechecked before every target in a five-target selection");
        Check(skill.ExecutionObservations.Exists(s=>s.Contains("명시 확보 상한"))&&skill.RemainingSelection.Count==4,"ceiling pauses with evidence and preserves original remaining selection without completing goal");
        AITileReader.Walls[new int2(0,0)]=new AITileReader.Wall{tileset=1,ore=true,oreTileset=4};
        var native=Start(world,Target("unknown-native",0));world.Alive.Add("unknown-native");Run(native,5);
        Check(!world.Trace.Contains("act:unknown-native")&&native.ExecutionObservations.Exists(s=>s.Contains("최대 산출량")),"unknown native yield pauses rather than silently overshooting a user ceiling");
        var oversized=new AIGatherTarget("pickup","big","copper","big",new int2(0,0),default,ObjectID.CopperOre,0,0,false,0,true,2);
        world.Alive.Add("big");var large=Start(world,oversized);Run(large,5);
        Check(!world.Trace.Contains("act:big")&&large.ExecutionObservations.Exists(s=>s.Contains("부분 회수")),"indivisible stack larger than remaining allowance is reported without executing");
        AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.CopperOre,count=10};
        world=new FakeWorld{Give=1};world.Bag[ObjectID.CopperOre]=9;foreach(var t in targets)world.Alive.Add(t.Key);
        skill=Start(world,targets.ToArray());Run(skill);
        Check(skill.Status==SkillStatus.Done&&skill.CompletedTargets==5&&world.Bag[ObjectID.CopperOre]==14,"ordinary goal count remains a target and never becomes an execution ceiling");
        Check(AIGatheringRules.LimitForm("gather",200,-1)==null&&AIGatheringRules.LimitForm("gather",10,9)!=null,"no default limit and contradictory explicit ceiling rejected");
        AIGoals.Goal=new AIGoal();AIBody.Bag.Clear();
    }
    private static void DiscoveryWatch()
    {
        var world=new FakeWorld();world.Alive.Add("a");world.Alive.Add("b");
        var skill=Start(world,Target("a",0),Target("b",1));
        var seen=new AIObservation();seen.wallTiles.Add((new int2(7,2),1,true,4));
        seen.items.Add((ObjectID.CopperOre,1,new float3{x=0,y=0,z=0},0));
        skill.ObserveDiscoveries(seen);skill.ObserveDiscoveries(seen);
        Check(skill.ExecutionObservations.Count==1&&skill.ExecutionObservations[0].Contains("새로 보이는 광맥"),"new visible resource wakes judgement once; selected-target recovery drops do not create another discovery");
        seen.rootTiles.Add((new int2(9,2),3));skill.ObserveDiscoveries(seen);skill.ObserveDiscoveries(seen);
        Check(skill.ExecutionObservations.Count==2&&skill.ExecutionObservations[1].Contains("새로 보이는 뿌리"),"visible root discovery wakes judgement once through the same fact path");
        Check(skill.Status==SkillStatus.Running&&skill.Selection.Count==2,"new discovery retains running body and immutable original target selection");
        Run(skill);Check(world.Trace.Count==4,"discovery does not add work or interrupt original selected patch");
    }
    private static void ActiveOrderLimits()
    {
        AIGoals.Goal=new AIGoal{kind=GoalKind.Home};
        AIExpedition.Limits=new[]{new ExpeditionLimit{OrderId=17,Item="CopperOre",Maximum=10,Acquired=9},new ExpeditionLimit{OrderId=18,Item="Wood",Maximum=1,Acquired=1}};
        var world=new FakeWorld{Give=1};world.Bag[ObjectID.CopperOre]=9;
        var targets=new List<AIGatherTarget>();
        for(int i=0;i<5;i++){string key="carried"+i;world.Alive.Add(key);targets.Add(new AIGatherTarget("pickup",key,"copper",key,new int2(i,0),default,ObjectID.CopperOre,0,0,false,0,false,1));}
        var skill=Start(world,targets.ToArray());Run(skill);
        Check(skill.CompletedTargets==1&&skill.Status==SkillStatus.Running,"active original-order ceiling applies per-target during Home stage independent of current GoalKind and progress");
        Check(AIGathering.LimitNote(new World(),Player).Contains("원부탁#17")&&AIGathering.LimitNote(new World(),Player).Contains("원부탁#18"),"reports identify all active owner/item conditions");
        var wood=new AIGatherTarget("pickup","wood","wood","wood",default,default,ObjectID.Wood,0,0,false,0,false,1);
        Check(AIGathering.LimitTarget(new World(),Player,wood)!=null,"second active item condition also applies without first-condition substitution");
        var mushroom=new AIGatherTarget("pickup","mushroom","mushroom","mushroom",default,default,ObjectID.Mushroom,0,0,false,0,true,1);
        Check(AIGathering.LimitTarget(new World(),Player,mushroom)==null,"known different pickup item is not bound by unrelated active item ceiling despite progress flag");
        AIExpedition.Limits=Array.Empty<ExpeditionLimit>();
        Check(AIGathering.LimitTarget(new World(),Player,wood)==null,"owner service releasing conditions removes guard without reading previous goal ceiling");
        AIExpedition.Limits=null;AIGoals.Goal=new AIGoal();AIBody.Bag.Clear();
    }
    private static void ProductionCandidates()
    {
        AITileReader.Walls.Clear(); AIBody.Bag.Clear(); AIVision.Hidden.Clear();
        var world=new World(); var choices=new List<AICandidate>();
        for(int i=0;i<3;i++)
        {
            var tile=new int2(i,0);AITileReader.Walls[tile]=new AITileReader.Wall{tileset=2,ore=true,oreTileset=4};
            choices.Add(new AICandidate{kind="mine",key="mine:"+i+",0",tile=tile,desc="observed copper",progress=true});
        }
        AIVision.Hidden.Add(new int2(2,0));
        AIGathering.AddCandidates(world,Player,new AIObservation(),choices);
        var candidate=choices.Find(c=>c.kind=="gather_group");
        Check(choices.Count==4&&candidate.gather.Count==2,"production producer preserves individuals and groups only visible eligible inputs");
        Check(candidate.itemId==ObjectID.CopperOre && choices[0].itemId==ObjectID.CopperOre,"ore patch classified by observed ore instead of wall drop");
        Check(AIGathering.Stale(world,Player,candidate)==null,"production exact-target stale gate accepts observed targets");
        AITileReader.Walls[new int2(3,0)]=new AITileReader.Wall{tileset=2,ore=true,oreTileset=4};
        var skill=(AIGatherSkill)AIGathering.Make(candidate);skill.Start(world,Player,0);
        skill.Tick(world,Player,0);
        Check(skill.Status==SkillStatus.Done,"no extra recovery delay when existing harvest already collected drops");
        for(int t=0;t<20&&skill.Status==SkillStatus.Running;t++)skill.Tick(world,Player,t);
        Check(skill.Status==SkillStatus.Done&&skill.CompletedTargets==2&&skill.Received[ObjectID.CopperOre]==4,"production candidate to Make to actual runtime adapter to sequence receipt");
        Check(AITileReader.Walls.ContainsKey(new int2(2,0))&&AITileReader.Walls.ContainsKey(new int2(3,0)),"hidden/newly exposed walls never added");
        Check(AIGathering.Stale(world,Player,candidate)!=null,"consumed snapshot cannot be reused");
        AIVision.Hidden.Clear();
        var old=Target("entity",1,"break");var replacement=new Entity{Index=old.Entity.Index,Version=old.Entity.Version+1};
        world.EntityManager.Set(replacement,new HealthCD{health=10});
        Check(new AIGatherRuntime().Check(world,Player,old)!=null,"entity version replacement rejected");
        var tileTarget=Target("wall",1);
        AITileReader.Walls[tileTarget.Tile]=new AITileReader.Wall{tileset=99};
        Check(new AIGatherRuntime().Check(world,Player,tileTarget)!=null,"changed wall source rejected");
        var drop=new Entity{Index=90,Version=1};
        world.EntityManager.Set(drop,new PickUpItemCD{state=PickUpItemState.OnGround});
        world.EntityManager.Set(drop,new Unity.Transforms.LocalTransform{Position=new float3{x=1,z=0}});
        world.EntityManager.Buffer(drop,new ContainedObjectsBuffer{objectID=ObjectID.CopperOre});
        Check(new AIGatherRuntime().Recovery(world,Player,tileTarget)!=null,"visible remaining drop triggers recovery approach");
        world.EntityManager.Set(drop,new PickUpItemCD{state=PickUpItemState.HasBeenPickedUp});
        Check(new AIGatherRuntime().Recovery(world,Player,tileTarget)==null,"already collected drop does not add a recovery wait");
        world.EntityManager.Set(drop,new PickUpItemCD{state=PickUpItemState.OnGround});
        world.EntityManager.Buffer(drop,new ContainedObjectsBuffer{objectID=ObjectID.CopperOre,amount=1});
        AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.CopperOre,count=10,maxCount=10};AIBody.Bag[ObjectID.CopperOre]=9;
        var single=new AICandidate{kind="pickup",tile=new int2(1,0),itemId=ObjectID.CopperOre,progress=true};
        Check(AIGathering.LimitReason(world,Player,single)==null,"production direct pickup gate accepts currently observed one-item stack within explicit remaining one");
        world.EntityManager.Buffer(drop,new ContainedObjectsBuffer{objectID=ObjectID.CopperOre,amount=2});
        Check(AIGathering.LimitReason(world,Player,single)!=null,"delayed direct pickup reobserves increased indivisible stack and rejects possible excess");
        Check(AIGathering.LimitReason(world,Player,new AICandidate{kind="mine",tile=tileTarget.Tile,itemId=ObjectID.None,progress=false})!=null,"production individual mine reads real target despite false progress and absent item metadata");
        Check(AIGathering.LimitReason(world,Player,new AICandidate{kind="edgemat",tile=tileTarget.Tile})!=null,"edgemat cannot bypass actual wall destruction ceiling");
        AITunnel.Next=new TunnelPlan();AITunnel.Next.walls.Add((tileTarget.Tile,ObjectID.None,1));
        Check(AIGathering.LimitReason(world,Player,new AICandidate{kind="dig",tile=new int2(20,0)})!=null,"false-progress dig tests actual route wall instead of only final destination metadata");
        AITunnel.Next.walls.Clear();
        Check(AIGathering.LimitReason(world,Player,new AICandidate{kind="dig",tile=new int2(20,0)})==null,"confirmed non-destructive pure walk remains available under explicit ceiling");
        Check(AIGathering.LimitReason(world,Player,new AICandidate{kind="dig",progress=true})==null,"progress metadata does not turn confirmed pure walking into resource production");
        AIGoals.Goal.maxCount=-1;
        Check(AIGathering.LimitReason(world,Player,single)==null,"same direct pickup allowed for ordinary target without maximum");
        AIGoals.Goal=new AIGoal();AIBody.Bag.Clear();
    }
    private sealed class FakeWorld : IAIGatherRuntime
    {
        public readonly HashSet<string> Alive=new HashSet<string>(); public readonly List<string> Trace=new List<string>();
        public readonly Dictionary<ObjectID,int> Bag=new Dictionary<ObjectID,int>();
        public string Fail; public int Give=2,Cleanups; public bool LeavePickup;
        public string Check(World w,Entity p,AIGatherTarget t)=>Alive.Contains(t.Key)?null:"gone";
        public AISkill Begin(AIGatherTarget t)=>new Child(this,t,false);
        public AISkill Recovery(World w,Entity p,AIGatherTarget t)=>t.Kind=="pickup"?null:new Child(this,t,true);
        public Dictionary<ObjectID,int> Counts(World w,Entity p)=>new Dictionary<ObjectID,int>(Bag);
        public float3 Position(World w,Entity p)=>default;
        private sealed class Child : AISkill
        {
            private readonly FakeWorld w;private readonly AIGatherTarget t;private readonly bool recovery;private int ticks;
            public Child(FakeWorld world,AIGatherTarget target,bool recover){w=world;t=target;recovery=recover;Name="fake physics";}
            public override bool Start(World world,Entity p,float now){w.Trace.Add((recovery?"recover:":"act:")+t.Key);return true;}
            public override BodyInput Tick(World world,Entity p,float now)
            {
                if(++ticks<2)return BodyInput.Idle;
                if(w.Fail==t.Key)return Finish(SkillStatus.Failed,"blocked");
                if(!recovery){if(!w.LeavePickup)w.Alive.Remove(t.Key);w.Bag.TryGetValue(t.Item,out int have);w.Bag[t.Item]=have+w.Give;}
                return Finish(SkillStatus.Done,"observed");
            }
            public override void Cleanup(World world){w.Cleanups++;}
        }
    }
}
