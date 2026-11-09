using System;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
public static class Program
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static ContainedObjectsBuffer Item(ObjectID id,int n,int v=0)=>new ContainedObjectsBuffer{objectData=new ObjectDataCD{objectID=id,amount=n,variation=v}};
 sealed class Scene
 {
  public World W=new World();public Entity Player,Pot1,Pot2;public AIObservation O;
  public Scene()
  {
   AIAgent.Cur=new AIAgent();AICraft.Requests.Clear();AIVision.Visible=true;PugDatabase.Prefabs.Clear();
   var em=W.EntityManager;var bank=em.New();em.Set(bank,new PugDatabase.DatabaseBankCD());
   foreach(ObjectID id in new[]{ObjectID.Mushroom,ObjectID.Root,ObjectID.Berry,ObjectID.CookedSoup,ObjectID.CookingPot})
   {
    var p=em.New();PugDatabase.Prefabs[id]=p;
    if(id==ObjectID.CookedSoup)em.Set(p,new CookedFoodCD());
    else if(id!=ObjectID.CookingPot)em.Set(p,new CookingIngredientCD{turnsIntoFood=ObjectID.CookedSoup});
    em.Buffer(p,new GivesConditionsWhenConsumedBuffer{conditionDataContainer=new ConditionDataContainer{conditionData=new ConditionData{conditionID=ConditionID.HungerAddition,value=id==ObjectID.Root?40:5},conditionDataWhenCooked=new ConditionData{conditionID=ConditionID.HungerAddition,value=10}}});
   }
   Player=em.New();em.Set(Player,new LocalTransform{Position=new float3(0,0,0)});em.Set(Player,new CraftingCD{craftingType=CraftingType.Other,outputSlotIndex=10});
   var bag=em.Buffer<ContainedObjectsBuffer>(Player);for(int i=0;i<11;i++)bag.Add(default);
   bag[0]=Item(ObjectID.Mushroom,10,2);bag[1]=Item(ObjectID.Root,10,7);bag[2]=Item(ObjectID.Berry,10);
   em.Buffer(Player,new InventoryBuffer{startIndex=0,size=10});
   Pot1=Pot(1);Pot2=Pot(2);O=new AIObservation{pos=AIBody.Pos(W,Player)};
  }
  Entity Pot(int x){var em=W.EntityManager;var p=em.New();em.Set(p,new LocalTransform{Position=new float3(x,0,0)});em.Set(p,new ObjectDataCD{objectID=ObjectID.CookingPot});em.Set(p,new CraftingCD{craftingType=CraftingType.Cooking,outputSlotIndex=2});em.Buffer<ContainedObjectsBuffer>(p,default,default,default);em.Buffer(p,new InventoryBuffer{startIndex=0,size=3});AICook.NotePlaced(W,p);return p;}
  public LifeSelection Select(string mode="put",int n=4,string item="Root#v7",string second="Mushroom#v2",bool first=false)=>new LifeSelection{Branch="cook",Mode=mode,Item=item,Second=second,Where=first?"pot:CookingPot@1,0":"pot:CookingPot@2,0",RequestedCount=n,MatchesGoal=true};
  public AICandidate Candidate(LifeSelection s)=>AICook.ExactCandidate(W,Player,O,"",s);
  public CookSkill Skill(AICandidate c){var skill=(CookSkill)AICook.Make(c);Check(skill.Key==c.key,"candidate and skill key match");Check(skill.Start(W,Player,0),"selected skill starts");return skill;}
  // Game input-consumer adapter: moves only the queued exact source/amount, optionally simulating partial consumption.
  public MoveRequest Consume(int limit=int.MaxValue,bool cursor=false)
  {
   var r=AICraft.Requests.Dequeue();var from=W.EntityManager.GetBuffer<ContainedObjectsBuffer>(r.From);var to=W.EntityManager.GetBuffer<ContainedObjectsBuffer>(r.To);var x=from[r.FromSlot];int target=r.ToSlot;
   if(target<0){target=cursor?r.Cursor:Enumerable.Range(0,AIInventory.MainEnd(W,r.To)).First(i=>to[i].objectID==ObjectID.None||AIInventory.Identity(to[i],x));}
   int n=Math.Min(Math.Min(limit,r.N),x.amount);var dest=to[target];to[target]=Item(x.objectID,(dest.objectID==ObjectID.None?0:dest.amount)+n,x.variation);from[r.FromSlot]=x.amount==n?default:Item(x.objectID,x.amount-n,x.variation);return r;
  }
 }
 public static void Main(){
  var s=new Scene();var bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);bag[0]=Item(ObjectID.Mushroom,1,2);bag[3]=Item(ObjectID.Mushroom,1,2);
  var e=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook",Mode="put",Item="Mushroom#v2"}).Single(x=>x.Id=="Mushroom#v2").Observe();var c=s.Candidate(s.Select(n:1,item:"Mushroom#v2",second:"Mushroom#v2"));
  Check(e.Op=="second"&&c!=null&&c.cook.SlotA!=c.cook.SlotB&&c.itemCount==1,"split 1+1 offers one pair with exact distinct sources");
  var skill=(CookSkill)AICook.Make(c);Check(skill.Start(s.W,s.Player,0),"split 1+1 starts");skill.Tick(s.W,s.Player,0);s.Consume();skill.Tick(s.W,s.Player,.1f);s.Consume();skill.Tick(s.W,s.Player,.2f);
  Check(skill.ConfirmedWork==1&&skill.ConfirmedReceived==0&&AICook.MainCount(s.W,s.Player,ObjectID.Mushroom,2)==0,"split 1+1 confirms one inserted pair and zero bag food receipt");
  s=new Scene();bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);foreach(int i in new[]{0,3,4,5})bag[i]=Item(ObjectID.Mushroom,2,2);
  c=s.Candidate(s.Select(n:4,item:"Mushroom#v2",second:"Mushroom#v2"));Check(c.itemCount==2&&c.desc.Contains("전체 재고·시설 기준 가능 4")&&c.desc.Contains("이번 고정 출발 슬롯 묶음 가능 2")&&c.desc.Contains("부족 0"),"split 2+2+2+2 reports total4 separately from fixed-source batch2");
  Console.WriteLine($"TARGETED COOK {passed}, FAILED 0");
 }
}