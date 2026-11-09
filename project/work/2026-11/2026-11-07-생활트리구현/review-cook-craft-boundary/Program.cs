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
  var s=new LifeSelection{Branch="craft",Item="Torch",RequestedCount=3,Where="station:WoodenWorkBench@2,0"};
  var direct=AICraft.ExactCandidate(world,player,AIObservation.Take(world,player,false),"",s);
  var m=AICraft.Materials(world,player,ObjectID.Torch,3);
  Console.WriteLine($"R3 exact={direct!=null}, calls={direct?.craft.Calls}, expected={direct?.craft.ExpectedOutput}, actualCost={direct?.craft.Costs[0].need}, legacyNeed={m[0].need}, bag={m[0].have}, legacyGate={AICraft.HasMaterials(m)}");
  var before=direct.craft.Where;world.EntityManager.Put(direct.entity,new LocalTransform{Position=new(3,0,0)});
  var stale=AICraft.Stale(world,player,direct);AICraft.ReadRecipe(world,player,direct.entity,ObjectID.Torch,4,out var current,out _);
  Console.WriteLine($"STATION old={before}, current={current.Where}, stale={stale??"null"}");
  return direct!=null && m[0].need==3 && m[0].have==2 && !AICraft.HasMaterials(m) ? 0 : 1;
 }
}