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
 public static void Main()
 {
  var s=new Scene();
  CookedFoodCD.PredictReads=0;
  var firstPage=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook",Mode="put"});Check(firstPage.Count==3&&CookedFoodCD.PredictReads==0,"first ingredient axis does not predict pairs");
  var secondPage=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook",Mode="put",Item="Root#v7"});Check(secondPage.Count==3&&CookedFoodCD.PredictReads==0,"second axis scan defers prediction until displayed page");
  var observedSecond=secondPage[0].Observe();Check(CookedFoodCD.PredictReads==1&&observedSecond.Op=="second","one displayed ingredient evaluates only its selected-first prediction");
  var pair=AICook.Predict(s.W,ObjectID.Root,ObjectID.Mushroom);Check(pair!=null&&!pair.better,"valid pair below raw hunger is valid");
  var c=s.Candidate(s.Select());Check(c!=null&&c.entity==s.Pot2,"non-BestPair ingredients and second own pot selectable");
  var leaf=AICook.LifeEntries(s.W,s.Player,s.O,"",s.Select()).Single(e=>e.Candidate!=null).Candidate;
  Check(leaf.key==c.key&&leaf.cook.SlotA==c.cook.SlotA&&leaf.cook.VarA==c.cook.VarA&&leaf.cook.Amount==c.cook.Amount,"direct and tree leaf share exact payload producer");
  var swapped=s.Candidate(s.Select(item:"Mushroom#v2",second:"Root#v7"));Check(swapped.key!=c.key,"ingredient order and variation included in identity");
  Check(s.Candidate(s.Select(n:12)).itemCount==5,"twelve requested offers a five-pair execution batch");
  var a=s.Candidate(s.Select(item:"Mushroom#v2",second:"Mushroom#v2",n:9));Check(a.itemCount==5,"same ingredient consumes twice per pair");
  var bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);bag[0]=Item(ObjectID.Mushroom,6,2);a=s.Candidate(s.Select(item:"Mushroom#v2",second:"Mushroom#v2",n:9));Check(a.itemCount==3,"same ingredient available amount divided by two");
  bag[0]=Item(ObjectID.Mushroom,10,2);c=s.Candidate(s.Select());bag[1]=Item(ObjectID.Root,9,7);Check(AICook.Stale(s.W,s.Player,c)!=null,"changed source amount rejects stale execution");
  s=new Scene();c=s.Candidate(s.Select());s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2)[0]=Item(ObjectID.Berry,1);Check(AICook.Stale(s.W,s.Player,c)!=null,"changed pot slot rejects stale execution");
  s=new Scene();c=s.Candidate(s.Select());s.W.EntityManager.Set(s.Pot2,new LocalTransform{Position=new float3(1,0,0)});Check(AICook.Stale(s.W,s.Player,c)!=null,"same pot entity moved to another owned tile is stale");
  s=new Scene();c=s.Candidate(s.Select());var skill=s.Skill(c);skill.Tick(s.W,s.Player,0);Check(AICraft.Requests.Count==1,"only first input queued initially");s.Consume(2);skill.Tick(s.W,s.Player,.1f);Check(AICraft.Requests.Peek().N==2,"partial first input constrains second request");s.Consume();skill.Tick(s.W,s.Player,.2f);Check(skill.ConfirmedWork==2&&skill.ConfirmedReceived==0&&skill.ResultStage=="inserted","partial put confirms two inserted pairs, zero food received");
  Check(skill.Facts.Any(x=>x.Contains("이번 미확인 2")),"partial quantity shortfall recorded");
  s=new Scene();c=s.Candidate(s.Select());skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume();skill.Tick(s.W,s.Player,.1f);s.Consume(2);skill.Tick(s.W,s.Player,.2f);Check(skill.ConfirmedWork==2,"partial second input does not claim full first input amount");
  s=new Scene();c=s.Candidate(s.Select());skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume();skill.Tick(s.W,s.Player,.1f);s.Consume();var pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[0]=Item(ObjectID.Root,2,7);pot[1]=Item(ObjectID.Mushroom,2,2);pot[2]=Item(c.cook.Output,2,c.cook.OutputVariation);skill.Tick(s.W,s.Player,.2f);Check(skill.ConfirmedWork==4&&skill.ConfirmedReceived==0,"consumed inputs and output growth corroborate automatic production without food receipt");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[0]=Item(ObjectID.Root,4,7);c=s.Candidate(s.Select("fill",4,"Mushroom#v2",""));skill=s.Skill(c);skill.Tick(s.W,s.Player,0);skill.Tick(s.W,s.Player,3);skill.Tick(s.W,s.Player,6);Check(AICraft.Requests.Count==1&&skill.ConfirmedWork==0,"unconfirmed fill is not retransmitted or declared successful");s.Consume(2);skill.Tick(s.W,s.Player,7);Check(skill.ConfirmedWork==2&&skill.ConfirmedReceived==0,"fill reports actual inserted pairs separately");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[2]=Item(ObjectID.CookedSoup,5,302);c=s.Candidate(s.Select("take",2,"CookedSoup#v302",""));Check(c!=null&&c.itemCount==2,"take offers requested dishes only");skill=s.Skill(c);skill.Tick(s.W,s.Player,0);var moved=s.Consume();Check(moved.N==2&&pot[2].amount==3,"normal take UI boundary receives two and leaves three in pot");skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedReceived==2&&skill.ResultStage=="received","take confirms exact variant main inventory gain");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[2]=Item(ObjectID.CookedSoup,5,302);c=s.Candidate(s.Select("take",2,"CookedSoup#v302",""));skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume();pot[2]=Item(ObjectID.CookedSoup,5,302);skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedReceived==2,"concurrent pot production does not hide actual main inventory receipt");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[2]=Item(ObjectID.CookedSoup,5,302);c=s.Candidate(s.Select("take",2,"CookedSoup#v302",""));skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume(cursor:true);skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedReceived==0&&skill.Status==SkillStatus.Running,"cursor-only output is not main inventory receipt");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[2]=Item(ObjectID.CookedSoup,5,302);c=s.Candidate(s.Select("take",2,"CookedSoup#v302",""));pot[2]=Item(ObjectID.CookedSoup,5,301);Check(AICook.Stale(s.W,s.Player,c)!=null,"changed output variation rejects old take");
  s=new Scene();bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);bag[0]=Item(ObjectID.Mushroom,1,2);bag[3]=Item(ObjectID.Mushroom,1,2);
  var splitEntries=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook",Mode="put",Item="Mushroom#v2"});var splitSecond=splitEntries.Single(e=>e.Id=="Mushroom#v2").Observe();Check(splitSecond.Op=="second","same ingredient split one plus one is selectable on second axis");
  c=s.Candidate(s.Select(n:1,item:"Mushroom#v2",second:"Mushroom#v2"));Check(c!=null&&c.itemCount==1&&c.cook.SlotA!=c.cook.SlotB,"same ingredient one plus one binds distinct exact source slots");skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume();skill.Tick(s.W,s.Player,.1f);s.Consume();skill.Tick(s.W,s.Player,.2f);Check(skill.ConfirmedWork==1&&skill.ConfirmedReceived==0&&AICook.MainCount(s.W,s.Player,ObjectID.Mushroom,2)==0,"same ingredient split sources execute and confirm exactly one pair");
  s=new Scene();bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);bag[0]=Item(ObjectID.Mushroom,2,2);bag[3]=Item(ObjectID.Mushroom,2,2);bag[4]=Item(ObjectID.Mushroom,2,2);bag[5]=Item(ObjectID.Mushroom,2,2);
  c=s.Candidate(s.Select(n:4,item:"Mushroom#v2",second:"Mushroom#v2"));Check(c!=null&&c.itemCount==2&&c.desc.Contains("전체 재고·시설 기준 가능 4")&&c.desc.Contains("이번 고정 출발 슬롯 묶음 가능 2")&&c.desc.Contains("부족 0"),"whole split-stock availability is separate from exact-source execution batch");
  bag[c.cook.SlotB]=Item(ObjectID.Mushroom,2,3);Check(AICook.Stale(s.W,s.Player,c)!=null,"changed second split source variation rejects selected execution");
  s=new Scene();bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);bag[0]=Item(ObjectID.Mushroom,1,2);bag[3]=Item(ObjectID.Mushroom,1,2);pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[0]=Item(ObjectID.Root,2,7);c=s.Candidate(s.Select("fill",2,"Mushroom#v2",""));Check(c!=null&&c.itemCount==1&&c.desc.Contains("전체 재고·시설 기준 가능 2")&&c.desc.Contains("부족 0"),"fill split-stock availability does not report a false material shortage");
  s=new Scene();bag=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Player);for(int i=0;i<bag.Count;i++)bag[i]=default;bag[0]=Item(ObjectID.Mushroom,1,2);pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[0]=Item(ObjectID.Root,1,7);Check(AICook.CanCook(s.W,s.Player)==null,"current partial pot remains reachable before remembered busy status refreshes");
  s=new Scene();AIVision.Visible=false;c=s.Candidate(s.Select());Check(c!=null&&c.kind=="dig"&&c.cook==null,"screen-out memory offers approach only without current buffer execution");
  // 2026-11-08 gap M3: asymmetric pots (input0 empty, input1 holding an ingredient), clearing an input, topping up.
  AIVision.Visible=true;
  var modes=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook"});Check(modes.Any(e=>e.Id=="clear")&&modes.Any(e=>e.Id=="fill"),"cook mode axis offers clear beside fill");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[1]=Item(ObjectID.Root,3,7);
  Check(s.Candidate(s.Select("put",3))==null,"asymmetric pot still refuses a two-slot put");
  c=s.Candidate(s.Select("fill",4,"Mushroom#v2",""));Check(c!=null&&c.cook.TargetSlot==0&&c.itemCount==3,"input1-only pot offers fill of empty input0 up to the input1 count");
  skill=s.Skill(c);skill.Tick(s.W,s.Player,0);moved=s.Consume();Check(moved.ToSlot==0&&pot[0].objectID==ObjectID.Mushroom&&pot[0].amount==3,"fill request moves the exact source into input0");skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedWork==3&&skill.ConfirmedReceived==0&&skill.ResultStage=="inserted","input0 fill confirms the actual inserted amount, no food receipt");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[1]=Item(ObjectID.Root,3,7);
  var clearItems=AICook.LifeEntries(s.W,s.Player,s.O,"",new LifeSelection{Branch="cook",Mode="clear"});Check(clearItems.Any(e=>e.Id=="slot1"&&e.Text.Contains("Root"))&&clearItems.Any(e=>e.Id=="slot0"),"clear item axis names both input slots with the observed contents");
  Check(s.Candidate(s.Select("clear",0,"slot0",""))==null,"clearing an empty input is not offered");
  c=s.Candidate(s.Select("clear",0,"slot1",""));Check(c!=null&&c.cook.TargetSlot==1&&c.itemCount==3&&c.itemId==ObjectID.Root,"clear input1 offers the whole leftover back to the bag");
  int rootBefore=AICook.MainCount(s.W,s.Player,ObjectID.Root,7);skill=s.Skill(c);skill.Tick(s.W,s.Player,0);moved=s.Consume();Check(moved.From==s.Pot2&&moved.FromSlot==1&&moved.To==s.Player,"clear uses the normal pot-to-bag move from input1");
  skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedReceived==3&&AICook.MainCount(s.W,s.Player,ObjectID.Root,7)==rootBefore+3&&pot[1].objectID==ObjectID.None,"clear confirms the bag gain and the emptied input");
  s=new Scene();pot=s.W.EntityManager.GetBuffer<ContainedObjectsBuffer>(s.Pot2);pot[0]=Item(ObjectID.Root,2,7);pot[1]=Item(ObjectID.Mushroom,2,2);
  c=s.Candidate(s.Select("fill",3,"Root#v7",""));Check(c!=null&&c.cook.TargetSlot==0&&c.itemCount==3,"cooking pot offers a top-up of the input already holding that ingredient");
  Check(s.Candidate(s.Select("fill",3,"Berry",""))==null,"top-up refuses an ingredient that is in neither input");
  skill=s.Skill(c);skill.Tick(s.W,s.Player,0);s.Consume();pot[0]=Item(ObjectID.Root,4,7);pot[1]=Item(ObjectID.Mushroom,1,2);pot[2]=Item(c.cook.Output,1,c.cook.OutputVariation);skill.Tick(s.W,s.Player,.1f);Check(skill.ConfirmedWork==3,"top-up counts the added amount through concurrent automatic production");
  Console.WriteLine($"Life cooking production adapter: {passed} passed, 0 failed");
 }
}
