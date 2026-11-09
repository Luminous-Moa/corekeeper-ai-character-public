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
  var(world,player,bag)=Setup();bag[0]=Item(ObjectID.Wood,2);
  var goal=new AIGoal{kind=GoalKind.Craft,item=ObjectID.Torch,count=3,startCount=0,where="station:WoodenWorkBench@2,0"};AIGoals.Goal=goal;
  var prep=AIBrain.TestPrep(world,player,out var wanted);var direct=prep.Single(x=>x.kind=="craft");
  var leaf=AICraft.LifeEntries(world,player,AIObservation.Take(world,player,false),"",AILife.ForGoal(goal)).Single().Candidate;
  Check(direct.key==leaf.key&&direct.craft.Station==leaf.craft.Station&&direct.craft.Calls==2&&direct.craft.CraftAmount==2&&direct.craft.ExpectedOutput==4&&direct.craft.Costs.Single().need==2&&!AIBase.Missing.ContainsKey(ObjectID.Wood)&&!wanted.Contains(ObjectID.Wood),"R3 fixed: actual complete WantedItems offers same exact payload with Wood2");
  bag[0]=Item(ObjectID.Wood,1);prep=AIBrain.TestPrep(world,player,out wanted);
  Check(!prep.Any(x=>x.kind=="craft")&&AIBase.Missing[ObjectID.Wood]==1&&AIBrain.PrepNote.Contains("1개 더"),"R3 fixed: actual complete WantedItems reports shortage1 with Wood1");
  Console.WriteLine($"TARGETED CRAFT {checks}, FAILED {failed}");return failed;
 }
}