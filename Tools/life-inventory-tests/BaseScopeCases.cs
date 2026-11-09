using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
static partial class Program {
static void BaseScopeChecks() {
 var(w,p,b)=Setup();var(chest,box)=Chest(w);b[0]=Item(ObjectID.Wood,0,10);
 w.EntityManager.Put(p,new LocalTransform{Position=new(100,0,0)});
 w.EntityManager.Put(chest,new LocalTransform{Position=new(101,0,0)});AIBase.Memories[0].tile=new(101,0);
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=3,where="base"});
 var choices=AIInventory.Candidates(w,p,Observe(w,p),"");
 Check(choices.Count==1&&choices[0].key=="fixture:base"&&choices[0].inventory==null,"explicit base at 100 tiles offers anchor movement before adjacent remote own chest");
 Check(!AIInventory.BaseChestContext(w,p,new(101,0)),"remote adjacent own chest is not explicit base context before anchor arrival");
 var selection=new LifeSelection{Branch="inventory",Mode="store",Where="base",Goal=AIGoals.Goal,MatchesGoal=true};
 Check(AIInventory.LifeEntries(w,p,Observe(w,p),"",selection).Single().Candidate?.key=="fixture:base","explicit base life selection also routes to anchor first");
 AIInventory.Opened(chest);
 var menu=AIInventory.MenuMoves(w,p,p,0,"store",chest,Entity.Null,"",3).First();
 Check(menu.goal==null&&AIInventory.MenuGoal(w,p,menu)==null,"manual remote chest transfer cannot bind explicit base goal progress");
 var skill=Start(w,p,menu);b[0]=Item(ObjectID.Wood,0,7);box[0]=Item(ObjectID.Wood,0,3);skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done&&AIInventory.MovedForGoal(AIGoals.Goal)==0&&!AIInventory.GoalDone(w,p,out _),"actual remote transfer receipt remains separate and does not fulfill explicit base");
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=3});
 choices=AIInventory.Candidates(w,p,Observe(w,p),"");var transfer=choices.First(x=>x.inventory?.mode=="store").inventory;
 Check(ReferenceEquals(transfer.goal,AIGoals.Goal),"omitted where retains remote owned chest as goal destination without base radius");
 skill=Start(w,p,transfer);b[0]=Item(ObjectID.Wood,0,4);box[0]=Item(ObjectID.Wood,0,6);skill.Tick(w,p,2);
 Check(skill.Status==SkillStatus.Done&&AIInventory.GoalDone(w,p,out _),"omitted where remote owned transfer completes through actual inventory observer");
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,where="base"});b[0]=default;
 Check(!AIInventory.GoalDone(w,p,out _),"empty all-store request cannot finish explicit base before reaching anchor");
 w.EntityManager.Put(p,new LocalTransform());b[0]=Item(ObjectID.Wood,0,10);
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=3,where="base"});
 choices=AIInventory.Candidates(w,p,Observe(w,p),"");
 Check(choices.Count==0&&!AIInventory.BaseChestContext(w,p,new(101,0)),"anchor arrival does not expose an unreachable remembered chest 101 tiles away as base target");
 selection=new(){Branch="inventory",Mode="store",Where="base",Goal=AIGoals.Goal,MatchesGoal=true};
 var entries=AIInventory.LifeEntries(w,p,Observe(w,p),"",selection);
 Check(entries.Count==1&&entries[0].Id=="inventory:base:no-chest"&&entries[0].Candidate==null,"arrived base without observed interactable own chest reports a fact instead of remote completion");
 w.EntityManager.Put(chest,new LocalTransform{Position=new(1,0,0)});AIBase.Memories[0].tile=new(1,0);
 Check(AIInventory.BaseChestContext(w,p,new(1,0)),"anchor arrival accepts observed interactable own chest by physical reach");
 entries=AIInventory.LifeEntries(w,p,Observe(w,p),"",selection);
 Check(entries.Single().Op=="where"&&entries[0].Id=="chest:1,0","explicit base life selection narrows to actual nearby owned chest");
 AIInventory.Opened(chest);choices=AIInventory.Candidates(w,p,Observe(w,p),"");transfer=choices.First(x=>x.inventory?.mode=="store").inventory;
 Check(ReferenceEquals(AIInventory.MenuGoal(w,p,transfer),AIGoals.Goal),"near-anchor actual chest transfer binds explicit base progress");
 skill=Start(w,p,transfer);b[0]=Item(ObjectID.Wood,0,7);box[0]=Item(ObjectID.Wood,0,9);skill.Tick(w,p,3);
 Check(skill.Status==SkillStatus.Done&&AIInventory.GoalDone(w,p,out _),"observed near-anchor exact transfer completes explicit base goal");
 AIBase.Memories.Clear();
 Check(!AIInventory.BaseChestContext(w,p,new(1,0)),"base helper preserves ownership rather than accepting unowned nearby chest");
 Check(AIInventory.CheckGoal(w,p,GoalKind.Take,ObjectID.Wood,3,"base",-1,-1)==null,"explicit base take may first travel and inspect without remembered owned chest");
 // The actual AIBase blocked-anchor predicate is tested by the separate production base/place adapter.
 // This boundary exposes its successful BaseStand result at (3,0); Inventory must reuse it everywhere.
 (w,p,b)=Setup();(chest,box)=Chest(w);AIBase.TestBlockedBaseStand=true;
 w.EntityManager.Put(p,new LocalTransform{Position=new(3,0,0)});w.EntityManager.Put(chest,new LocalTransform{Position=new(4,0,0)});AIBase.Memories[0].tile=new(4,0);b[0]=Item(ObjectID.Wood,0,10);
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=3,where="base"});
 Check(!AIBase.AtBase(AIBody.Pos(w,p))&&AIInventory.BaseChestContext(w,p,new(4,0)),"blocked anchor stand arrival is accepted even though literal anchor center is not reached");
 choices=AIInventory.Candidates(w,p,Observe(w,p),"");
 Check(choices.Count==1&&choices[0].inventory?.mode=="inspect","blocked anchor stand arrival proceeds to chest inspection instead of repeating Home");
 selection=new(){Branch="inventory",Mode="store",Where="base",Goal=AIGoals.Goal,MatchesGoal=true};
 Check(AIInventory.LifeEntries(w,p,Observe(w,p),"",selection).Single().Id=="chest:4,0","blocked anchor stand life selection shares arrival context");
 Start(w,p,choices[0].inventory);transfer=AIInventory.Candidates(w,p,Observe(w,p),"").First(x=>x.inventory?.mode=="store").inventory;
 Check(AIInventory.Stale(w,p,transfer)==null&&ReferenceEquals(AIInventory.MenuGoal(w,p,transfer),AIGoals.Goal),"blocked anchor stand exact transfer remains valid and binds current goal");
 skill=Start(w,p,transfer);b[0]=Item(ObjectID.Wood,0,7);box[0]=Item(ObjectID.Wood,0,3);skill.Tick(w,p,4);
 Check(skill.Status==SkillStatus.Done&&AIInventory.GoalDone(w,p,out _),"blocked anchor stand observed receipt completes without Home loop");
 AIBase.TestBlockedBaseStand=false;
 Console.WriteLine("BASE SCOPE: actual AIInventory producer, menu binding, InventorySkill and receipt; fixture world, base arrival/mover and visibility boundary; no game consumer");
}
}