using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
class Program {
 static int checks,failed;
 static void Check(bool pass,string text){checks++;if(!pass)failed++;Console.WriteLine((pass?"PASS ":"FAIL ")+text);}
 static ContainedObjectsBuffer Item(ObjectID id,int amount,int variation=0)=>new(){objectData=new(){objectID=id,amount=amount,variation=variation}};
 static (World,Entity,DynamicBuffer<ContainedObjectsBuffer>) Setup(){
  AIAgent.Cur=new();AIInventory.Reset();AIBase.Memories.Clear();
  var world=new World();var player=new Entity{Index=1,Version=2};var bag=new DynamicBuffer<ContainedObjectsBuffer>();for(int i=0;i<60;i++)bag.Add(default);
  bag[0]=Item(ObjectID.Wood,20);world.EntityManager.PutBuffer(player,bag);
  var inventories=new DynamicBuffer<InventoryBuffer>();inventories.Add(new(){startIndex=0,size=40});world.EntityManager.PutBuffer(player,inventories);
  world.EntityManager.Put(player,new LocalTransform());world.EntityManager.Put(player,new CraftingCD{outputSlotIndex=50});world.EntityManager.PutBuffer(player,new DynamicBuffer<CanCraftObjectsBuffer>());
  world.EntityManager.PutBuffer(player,new DynamicBuffer<UIActionBuffer>());world.EntityManager.Put(new(){Index=90},new Unity.NetCode.NetworkTime());world.EntityManager.Put(new(){Index=91},new PugDatabase.DatabaseBankCD{databaseBankBlob=new object()});
  PugDatabase.Data[(int)ObjectID.Torch]=new(){isStackable=true,initialAmount=2,requiredObjectsToCraft=new(){Values=new[]{new ObjectWithAmount{objectID=ObjectID.Wood,amount=1}}}};
  for(int i=2;i<=3;i++){var station=new Entity{Index=i,Version=7};world.EntityManager.Put(station,new ObjectDataCD{objectID=ObjectID.WoodenWorkBench});world.EntityManager.Put(station,new LocalTransform{Position=new(i-1,0,0)});world.EntityManager.Put(station,new CraftingCD());var recipes=new DynamicBuffer<CanCraftObjectsBuffer>();recipes.Add(new(){objectID=ObjectID.Torch,amount=1});world.EntityManager.PutBuffer(station,recipes);}
  return(world,player,bag);
 }
 static int Main(){
  var(world,player,bag)=Setup();var observation=AIObservation.Take(world,player,false);
  var selection=new LifeSelection{Branch="craft",Item="Torch",RequestedCount=3};
  var places=AICraft.LifeEntries(world,player,observation,"",selection);
  Check(places.Count(x=>x.Op=="where")==2,"known item expands both currently observed manual stations");
  selection.Where="station:WoodenWorkBench@2,0";
  var direct=AICraft.ExactCandidate(world,player,observation,"",selection);
  var leaf=AICraft.LifeEntries(world,player,observation,"",selection).Single().Candidate;
  Check(direct!=null&&leaf.key==direct.key&&direct.entity.Index==3&&direct.craft.Station.Version==7,"tree and direct entrance freeze the same second station and entity version");
  Check(direct.craft.BaseOutput==2&&direct.craft.Calls==2&&direct.itemCount==2&&direct.craft.ExpectedOutput==4,"output-two recipe distinguishes request-three items, ceil two craft calls and expected surplus one");
  var skill=(CraftSkill)AICraft.Make(direct);Check(skill.Key==direct.key&&skill.Start(world,player,0),"production CraftSkill accepts frozen recipe and preserves candidate key");
  skill.Tick(world,player,0);var queue=world.EntityManager.GetBuffer<UIActionBuffer>(player);
  Check(queue.Length==1&&queue[0].actionData.action==UIInputAction.Craft&&queue[0].actionData.craftActionData.craftingEntity.Index==3&&queue[0].actionData.craftActionData.amount==2,"normal UI Craft request carries exact station and craft amount");
  bag[0]=Item(ObjectID.Wood,18);bag[50]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.Status==SkillStatus.Running&&skill.ConfirmedReceived==0&&skill.ConfirmedWork==2&&queue.Length==2,"produced cursor output records work while normal stow request does not imply bag receipt");
  skill.Tick(world,player,12);Check(skill.Status==SkillStatus.Running&&skill.ConfirmedReceived==0&&queue.Length==2,"cursor residual stays pending without false Done, repeated request or elapsed-time cancellation");
  bag[1]=Item(ObjectID.Torch,5);bag[50]=default;skill.Tick(world,player,13);
  Check(skill.Status==SkillStatus.Done&&skill.ConfirmedReceived==5,"actual main inventory receipt includes observed dynamic bonus and cursor clear");
  (world,player,bag)=Setup();direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);var material=bag[0];material.auxDataIndex=44;bag[0]=material;
  Check(AICraft.Stale(world,player,direct)!=null&&!AICraft.Make(direct).Start(world,player,0),"material identity replaced after selection is stale in both action and skill entrance");
  (world,player,bag)=Setup();direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);var recipes=world.EntityManager.GetBuffer<CanCraftObjectsBuffer>(direct.entity);recipes[0]=new(){objectID=ObjectID.Torch,amount=2};
  Check(AICraft.Stale(world,player,direct)!=null,"recipe amount changed after selection cannot silently execute another batch");
  world.EntityManager.Put(direct.entity,new CraftingCD{craftingType=CraftingType.ProcessResources});selection.Where="";
  Check(AICraft.LifeEntries(world,player,AIObservation.Take(world,player,false),"",selection).All(x=>x.Op!="where"||x.Id!="station:WoodenWorkBench@2,0"),"automatic process station is excluded from manual craft execution leaves");
  (world,player,bag)=Setup();selection.Where="station:WoodenWorkBench@2,0";direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,0);skill.Tick(world,player,0);bag[0]=Item(ObjectID.Wood,18);bag[50]=Item(ObjectID.Torch,4,3);skill.Tick(world,player,1);
  Check(skill.Status==SkillStatus.Running&&skill.ConfirmedReceived==0&&world.EntityManager.GetBuffer<UIActionBuffer>(player).Length==1,"unexpected output variation is not stowed or counted as selected recipe output");
  (world,player,bag)=Setup();direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,0);skill.Tick(world,player,0);bag[1]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.Status==SkillStatus.Running&&skill.ConfirmedWork==0&&skill.ConfirmedReceived==0,"coincidental output gain without own requested material consumption does not establish craft receipt");
  (world,player,bag)=Setup();var goal=new AIGoal{kind=GoalKind.Craft,item=ObjectID.Torch,count=3,startCount=0};AIGoals.Goal=goal;selection.Goal=goal;selection.MatchesGoal=true;selection.RequestedCount=3;bag[1]=Item(ObjectID.Torch,2);direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct.craft.Calls==1&&direct.life.RequestedCount==1,"current main inventory acquisition narrows remaining craft quantity even without a craft progress receipt");
  bag[1]=Item(ObjectID.Torch,3);Check(AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection)==null,"current main inventory fulfillment prevents another exact craft request");
  (world,player,bag)=Setup();bag[0]=Item(ObjectID.Wood,2);goal=new AIGoal{kind=GoalKind.Craft,item=ObjectID.Torch,count=3,startCount=0,where="station:WoodenWorkBench@2,0"};AIGoals.Goal=goal;
  var prep=AIBrain.TestPrep(world,player,out var wanted);var prepared=prep.Single(x=>x.kind=="craft");selection=AILife.ForGoal(goal);var treeLeaf=AICraft.LifeEntries(world,player,AIObservation.Take(world,player,false),"",selection).Single().Candidate;
  Check(prepared.key==treeLeaf.key&&prepared.craft.Calls==2&&prepared.craft.Costs.Single().need==2,"production Prep offers the same direct/tree output-two recipe for goal three with actual Wood two cost");
  Check(!wanted.Contains(ObjectID.Wood)&&!AIBase.Missing.ContainsKey(ObjectID.Wood),"production Prep does not apply the legacy three-item materials gate to a two-call recipe");
  bag[0]=Item(ObjectID.Wood,1);prep=AIBrain.TestPrep(world,player,out wanted);
  Check(!prep.Any(x=>x.kind=="craft")&&AIBase.Missing[ObjectID.Wood]==1&&AIBrain.PrepNote.Contains("1개 더"),"production Prep reports actual Wood shortage one for two craft calls rather than legacy shortage two");
  bool canCraft=AICraft.ReadRecipe(world,player,new Entity{Index=3,Version=7},ObjectID.Torch,3,out var unavailableRecipe,out string shortage);
  Check(!canCraft&&unavailableRecipe.Costs.Single().need==2&&shortage.Contains("1개 부족"),"non-executable recipe retains the same runtime output/cost metadata for accurate preparation facts");
  LargeQuantityChecks();
  ChestAndMemoryChecks();
  PlannedTakeChecks();
  Console.WriteLine($"CRAFT CHECKS {checks}, FAILED {failed}; production AICraft/CraftSkill with fake ECS/DB/UI queue and manually supplied state, no actual game consumer");return failed;
 }
 // 2026-11-08 gaps: every chest within 10 tiles of the station supplies materials (shared chests, host's included), and a
 // remembered auto-process station is not offered as a craft place.
 static Entity Chest(World world,int index,float x,ObjectID id,int amount){
  var chest=new Entity{Index=index,Version=1};world.EntityManager.Put(chest,new InventoryAutoTransferEnabledCD());world.EntityManager.Put(chest,new ObjectDataCD{objectID=ObjectID.Chest});world.EntityManager.Put(chest,new LocalTransform{Position=new(x,0,0)});
  var inv=new DynamicBuffer<InventoryBuffer>();inv.Add(new(){startIndex=0,size=4});world.EntityManager.PutBuffer(chest,inv);var buf=new DynamicBuffer<ContainedObjectsBuffer>();for(int i=0;i<4;i++)buf.Add(default);buf[1]=Item(id,amount);world.EntityManager.PutBuffer(chest,buf);return chest;
 }
 static void ChestAndMemoryChecks(){
  var(world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};bag[0]=Item(ObjectID.Wood,1);
  var selection=new LifeSelection{Branch="craft",Item="Torch",RequestedCount=3,Where="station:WoodenWorkBench@2,0"};
  Check(AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection)==null,"bag one Wood alone cannot pay the two-call recipe");
  var far=Chest(world,70,30,ObjectID.Wood,9);
  Check(AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection)==null,"a chest beyond 10 tiles of the station does not supply materials");
  var near=Chest(world,71,5,ObjectID.Wood,5);
  var direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.craft.Calls==2&&direct.desc.Contains("근처 상자")&&direct.desc.Contains("Wood 1"),"a near chest of any owner covers the shortfall and the choice says one Wood comes from it");
  Check(AICraft.CraftChests(world,player,direct.craft.Station).Count==1&&AICraft.MaterialCount(world,player,direct.craft.Station,ObjectID.Wood)==6,"craft chest set and material count follow the server's nearby-chest rule");
  var skill=(CraftSkill)AICraft.Make(direct);Check(skill.Start(world,player,0),"chest-supplied craft starts");skill.Tick(world,player,0);
  var chestBuf=world.EntityManager.GetBuffer<ContainedObjectsBuffer>(near);bag[0]=default;chestBuf[1]=Item(ObjectID.Wood,4);bag[50]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.ConfirmedWork==2,"bag plus chest consumption corroborates the produced output");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};bag[0]=Item(ObjectID.Wood,1);near=Chest(world,71,5,ObjectID.Wood,5);
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);chestBuf=world.EntityManager.GetBuffer<ContainedObjectsBuffer>(near);chestBuf[1]=Item(ObjectID.Wood,2);
  Check(AICraft.Stale(world,player,direct)!=null,"a changed chest material slot makes the selected craft stale");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};
  var furnacePrefab=new Entity{Index=1000+(int)ObjectID.Furnace};world.EntityManager.Put(furnacePrefab,new CraftingCD{craftingType=CraftingType.ProcessResources});var fr=new DynamicBuffer<CanCraftObjectsBuffer>();fr.Add(new(){objectID=ObjectID.CopperBar,amount=1});world.EntityManager.PutBuffer(furnacePrefab,fr);
  AIMemory.Remembered.Clear();AIMemory.Remembered.Add(new AIMemory.Station{id=ObjectID.Furnace,tile=new int2(30,0)});
  var items=AICraft.LifeEntries(world,player,AIObservation.Take(world,player,false),"",new LifeSelection{Branch="craft"});
  Check(items.All(x=>x.Id!="CopperBar"),"a remembered furnace's auto-process output is not listed as a craft item");
  var places=AICraft.LifeEntries(world,player,AIObservation.Take(world,player,false),"",new LifeSelection{Branch="craft",Item="CopperBar"});
  Check(places.All(x=>x.Op!="where"),"a remembered furnace is not offered as a craft place");
  AIMemory.Remembered.Clear();
 }
 // Review I1/L13: only the slots the server's Craft will take from are bound (player buffer first, then the nearest
 // chests); an unrelated chest change or an inflow into a planned slot neither refuses nor stalls the craft.
 static void PlannedTakeChecks(){
  var selection=new LifeSelection{Branch="craft",Item="Torch",RequestedCount=3,Where="station:WoodenWorkBench@2,0"};
  var(world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};var near=Chest(world,71,5,ObjectID.Wood,5);
  var direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.craft.ChestMaterials.Count==0&&!direct.desc.Contains("근처 상자"),"a bag-only craft binds no chest slot even with the same material in a near chest");
  var chestBuf=world.EntityManager.GetBuffer<ContainedObjectsBuffer>(near);chestBuf[1]=Item(ObjectID.Wood,9);chestBuf[2]=Item(ObjectID.Wood,3);
  Check(AICraft.Stale(world,player,direct)==null,"a conveyor-style change in a near chest does not make a bag-only craft stale");
  var skill=(CraftSkill)AICraft.Make(direct);Check(skill.Start(world,player,0),"bag-only craft starts after the chest changed");skill.Tick(world,player,0);
  bag[0]=Item(ObjectID.Wood,18);chestBuf[1]=Item(ObjectID.Wood,12);bag[50]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.ConfirmedWork==2,"bag-only consumption is confirmed while the near chest keeps filling");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};bag[0]=Item(ObjectID.Wood,1);near=Chest(world,71,5,ObjectID.Wood,5);var other=Chest(world,72,6,ObjectID.Wood,4);
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.craft.ChestMaterials.Count==1&&direct.craft.ChestMaterials.Keys.Single().chest.Index==71,"only the nearest chest slot the craft takes from is bound");
  world.EntityManager.GetBuffer<ContainedObjectsBuffer>(other)[1]=Item(ObjectID.Wood,1);
  Check(AICraft.Stale(world,player,direct)==null,"a change in a farther chest the craft does not take from is not stale");
  skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,0);skill.Tick(world,player,0);
  chestBuf=world.EntityManager.GetBuffer<ContainedObjectsBuffer>(near);bag[0]=default;chestBuf[1]=Item(ObjectID.Wood,7);bag[50]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.ConfirmedWork==2,"an inflow into the planned chest slot during the craft is settled by the cursor output instead of stalling");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};bag[0]=Item(ObjectID.Wood,1);near=Chest(world,71,5,ObjectID.Wood,10);
  var four=new LifeSelection{Branch="craft",Item="Torch",RequestedCount=7,Where="station:WoodenWorkBench@2,0"};
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",four);
  Check(direct!=null&&direct.craft.Calls==4&&direct.craft.ChestMaterials.Count==1,"fixture: four calls take one Wood from the bag and three from the near chest");
  skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,0);skill.Tick(world,player,0);
  chestBuf=world.EntityManager.GetBuffer<ContainedObjectsBuffer>(near);bag[0]=default;chestBuf[1]=Item(ObjectID.Wood,8);bag[50]=Item(ObjectID.Torch,8);skill.Tick(world,player,1);
  Check(skill.ConfirmedWork==4,"review-2 N3: a small inflow (chest 10→8 for a take of 3: the slot went down, by less than planned) is settled by the cursor output");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};bag[0]=Item(ObjectID.Wood,1);bag[45]=Item(ObjectID.Wood,1);
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.craft.ChestMaterials.Count==0&&direct.craft.Materials.ContainsKey(45),"a material outside the main bag (a pouch slot) counts as the server consumes the player's whole buffer");
  (world,player,bag)=Setup();AIGoals.Goal=new AIGoal{kind=GoalKind.None};direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,0);skill.Tick(world,player,0);bag[1]=Item(ObjectID.Torch,4);skill.Tick(world,player,1);
  Check(skill.ConfirmedWork==0,"a main gain without cursor output and without a planned decrease still does not confirm");
 }
 static void LargeQuantityChecks(){
  var(world,player,bag)=Setup();bag[0]=Item(ObjectID.Wood,9999);bag[2]=Item(ObjectID.Wood,9999);
  var station=new Entity{Index=3,Version=7};
  Check(AICraft.ReadRecipe(world,player,station,ObjectID.Torch,int.MaxValue,out var huge,out _)&&huge.RequestedOutput==int.MaxValue&&huge.ExpectedOutput==9998&&huge.Calls==4999,"int.Max total with output-two recipe is safely rounded using a normal cursor batch, not overflowed or rejected");
  var goal=new AIGoal{kind=GoalKind.Craft,item=ObjectID.Torch,count=20000,startCount=0,where="station:WoodenWorkBench@2,0"};AIGoals.Goal=goal;
  var selection=new LifeSelection{Branch="craft",Item="Torch",Where=goal.where,Goal=goal,MatchesGoal=true,RequestedCount=20000};
  var direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.life.RequestedCount==20000&&direct.life.OfferedCount==9998,"goal 20000 survives candidate binding while only offered request is capped by normal cursor space");
  var skill=(CraftSkill)AICraft.Make(direct);Check(skill.Start(world,player,0),"large goal starts a normal production CraftSkill request");skill.Tick(world,player,0);
  var queue=world.EntityManager.GetBuffer<UIActionBuffer>(player);
  Check(queue.Length==1&&queue[0].actionData.craftActionData.amount==4999,"large goal emits one correct normal recipe request, not 20000 unsafe calls");
  bag[0]=Item(ObjectID.Wood,5000);bag[50]=Item(ObjectID.Torch,9998);skill.Tick(world,player,1);skill.Tick(world,player,10);
  Check(skill.Status==SkillStatus.Running&&skill.ConfirmedReceived==0&&queue.Length==2,"large cursor batch pending emits one stow request and no duplicate craft or false total completion");
  bag[4]=Item(ObjectID.Torch,9998);bag[50]=default;skill.Tick(world,player,11);
  Check(skill.Status==SkillStatus.Done&&skill.ConfirmedReceived==9998&&ReferenceEquals(AIGoals.Goal,goal)&&goal.count==20000,"confirmed first-batch receipt preserves total goal and goal identity");
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(direct!=null&&direct.life.RequestedCount==10002&&direct.craft.ExpectedOutput==9998,"actual receipt yields the second safe batch with true remaining total 10002");
  skill=(CraftSkill)AICraft.Make(direct);Check(skill.Start(world,player,12),"second production batch revalidates changed material slots");skill.Tick(world,player,12);
  bag[0]=Item(ObjectID.Wood,1);bag[50]=Item(ObjectID.Torch,9998);skill.Tick(world,player,13);bag[5]=Item(ObjectID.Torch,9998);bag[50]=default;skill.Tick(world,player,14);
  direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection);
  Check(skill.ConfirmedReceived==9998&&direct.life.RequestedCount==4&&direct.craft.Calls==2,"two completed production batches leave four items, not a truncated 9999 total");
  skill=(CraftSkill)AICraft.Make(direct);skill.Start(world,player,15);skill.Tick(world,player,15);bag[0]=default;bag[2]=Item(ObjectID.Wood,9998);bag[50]=Item(ObjectID.Torch,4);skill.Tick(world,player,16);bag[6]=Item(ObjectID.Torch,4);bag[50]=default;skill.Tick(world,player,17);
  Check(skill.Status==SkillStatus.Done&&skill.ConfirmedReceived==4&&AICraft.MainCount(world,player,ObjectID.Torch)==20000&&AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",selection)==null,"all 20000 actual receipts complete the requested craft quantity without resetting total or emitting a fourth request");
 }
}
