using System.Linq;using System;using System.Collections.Generic;using Unity.Entities;using Unity.Mathematics;using UnityEngine;
class Program {
static int checks;static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;Console.WriteLine("OK "+m);}
static void Main(){
Check(AIPlaces.Parse("line:0,0,199,0",out var r,out _),"200 tiles accepted");
Check(AIPlaces.Parse("line:2147483647,0,2147483647,0",out var one,out _)&&AIPlaces.ShapeTiles(one).Count==1&&AIPlaces.ShapeTiles(one)[0].x==int.MaxValue,"maximum coordinate single tile remains finite");
Check(AIPlaces.Parse("line:0,0,100000000,0",out var big,out _)&&AIPlaces.ShapeTiles(big).Count==100000001&&AIPlaces.ShapeTiles(big)[100000000].x==100000000,"large lazy view retains far endpoint without allocation");
Check(!AIPlaces.Parse("rect:-2147483648,-2147483648,2147483647,2147483647",out _,out _),"unrepresentable signed-int work count rejected without arithmetic overflow");
AIPlaceGoal.Begin(r);var world=new World{Bag=200};AIBrain.Goal=new AIGoal{kind=GoalKind.Place,item=ObjectID.Wood,count=200};
int batches=0;
while(world.Bag>0){var batch=AIPlaceGoal.Batch();Check(batch.Count>0&&batch.Count<=128,"bounded evaluation batch preserves remaining goal");var action=new PlaceTilesSkill(ObjectID.Wood,batch);AIBody.Current=action;Check(action.Start(world,default,Time.realtimeSinceStartup),"actual production placement skill starts");for(int n=0;n<1000&&action.Status==SkillStatus.Running;n++){Time.realtimeSinceStartup+=0.1f;action.Tick(world,default,Time.realtimeSinceStartup);}Check(action.Status==SkillStatus.Done,"actual production placement skill completes batch");AIBody.Current=null;batches++;if(world.Bag>0)Check(!AIPlaceGoal.GoalDone(world,default,out _),"first chunk does not falsely complete total200");}
Check(world.Tiles.Count==200&&AIPlaceGoal.Receipts==200,"all200 actual game-boundary receipts retained beyond old64");
Check(AIPlaceGoal.GoalDone(world,default,out var text)&&text.Contains("200"),"actual production GoalDone completes only at total200");
Check(batches==2,"200 goal spans evaluation chunks without truncation");
AIPlaceGoal.Begin(r);for(int x=0;x<128;x++)AIPlaceGoal.NoteSkip(new int2(x,0),"already occupied");var next=AIPlaceGoal.Batch();Check(next.Count==72&&next[0].x==128,"fully skipped first chunk advances to later work");
AIPlaceGoal.Begin(r);var occupied=new World{Bag=200};for(int x=0;x<200;x++)occupied.Tiles.Add(new int2(x,0));
Check(AIPlaceGoal.Inspect(occupied).Count==72,"actual Remaining observes preexisting first chunk and advances");
Check(AIPlaceGoal.Inspect(occupied).Count==0&&AIPlaceGoal.Receipts==0&&!AIPlaceGoal.GoalDone(occupied,default,out _),"all preexisting chunks are exhausted without falsely claiming new placement");
var edgePath=AITunnelSearch.Find((int.MaxValue,0),(int.MinValue,0),(int.MinValue,int.MinValue),(int.MaxValue,int.MaxValue),(x,y)=>y==0&&(x==int.MaxValue||x==int.MinValue)?1:-1,out var edgeCap,10);
Check(edgePath==null&&!edgeCap,"search does not wrap across signed coordinate boundary");
var farPath=AITunnelSearch.Find((0,0),(400,0),(int.MinValue,int.MinValue),(int.MaxValue,int.MaxValue),(x,y)=>y==0&&x>=0&&x<=400?1:-1,out var farCap);
Check(farPath.Count==400&&!farCap,"actual route search accepts target beyond old260 distance");
AIPlaces.Parse("base",out var home,out _);var baseTarget=new PlaceTarget{Ref=home,Center=new int2(0,0),ArriveR=0.7f};baseTarget.Stands.Add(new int2(0,0));var travel=new World{Pos=new float3(100,0,0),Bag=1};
Check(!AIPlaceGoal.Ready(travel,baseTarget),"production explicit-base placement guard requires travel from100tiles away");
travel.Pos=new float3(0,0,0);Check(AIPlaceGoal.Ready(travel,baseTarget),"production guard offers placement only after anchor arrival");
var atBase=new PlaceTilesSkill(ObjectID.Wood,new(){new int2(0,0)});AIPlaceGoal.Begin(one);atBase.Start(travel,default,0);for(int i=0;i<20&&atBase.Status==SkillStatus.Running;i++)atBase.Tick(travel,default,i);Check(travel.Tiles.Contains(new int2(0,0))&&!travel.Tiles.Contains(new int2(100,0)),"actual placement skill confirms base tile after travel boundary");
var blockedHome=new World{Pos=new float3(3,0,0),HasStand=true,Stand=new int2(3,0)};
Check(BaseArrivalProbe.ArrivedAtBase(blockedHome,default),"production shared home arrival accepts actual stand three tiles from blocked anchor");
blockedHome.Pos=new float3(100,0,0);Check(!BaseArrivalProbe.ArrivedAtBase(blockedHome,default),"shared stand predicate still rejects100tile remote context");
AIBase.SeedFood();
foreach(var goal in new[]{GoalKind.None,GoalKind.Gather})foreach(bool danger in new[]{false,true}) {
 var f=new FoodProbe{Goal=new AIGoal{kind=goal},inCombat=danger,o=new AIObservation{hunger=90}};f.Food();
 Check(f.lLife.Exists(c=>c.kind=="eat"),$"actual Food producer calls real AIFood.Candidates at90hunger goal={goal} combat={danger}");
 var empty=new List<AsmCand>();var foods=f.lLife.ConvertAll(c=>new AsmCand{kind=c.kind,key=c.key,desc=c.desc});
 var input=new AsmInput{goal=goal,combat=danger,closeFight=danger,lLife=foods,lCont=new(),lFollow=new(),lEnemy=new(),lCombat=new(),lCraft=new(),lMine=new(),lDig=new(),lRemember=new(),lItem=new(),lExplore=new(),lDigExplore=new(),lOther=new(),foodPicks=new()};
 var offered=AICandidateAssembler.Select(input,_=>true,_=>true,out _,out _,out _);AICandidateAssembler.Finish(offered,new AsmCand{kind="wait",key="wait"});
 Check(offered.Exists(c=>c.kind=="eat"),$"actual final assembler preserves produced food goal={goal} combat={danger}");
 Check(offered.Count(c=>c.kind=="dig")==2&&offered.Exists(c=>c.key=="dig:1,0"&&c.desc.Contains("Berry")&&c.desc.Contains("Mushroom")),"actual final assembler preserves all supply alternatives (three sources: two chest walks, the second food of chest (1,0) joined to its walk)");
 Check(f.lLife.Count(c=>c.kind=="dig")==2&&f.lLife.Exists(c=>c.key=="dig:2,0"),"actual supply producer preserves all three remembered chest/item sources while food is already carried (verbatim TakeOrGo, review-5 G2)");
}
AIEdge.Enabled=true;var edges=new FoodProbe{Goal=new(){kind=GoalKind.None}};edges.Edge();Check(edges.lEdge.Count==2,"actual Prep edge and guard producer calls remain active with None goal");AIEdge.Enabled=false;
// Review-5 G3 (measure only, no change): the chest-side worst case through the real assembler. Per producer at most 40
// (AIChestAreas.SplitAbove): flat store / slot-area representatives (lCraft 40 + 1 off-screen walk), food takes (lLife 40),
// then the life tree step (40 + back/root/close) that AILife.Compose adds after Select. Walls etc. are counted separately.
{Func<string,int,List<AsmCand>> many=(k,n)=>{var l=new List<AsmCand>();for(int i=0;i<n;i++)l.Add(new AsmCand{kind=k,key=k+":"+i,desc=k});return l;};
 foreach(int walls in new[]{0,120,130}){
 var g3=new AsmInput{goal=GoalKind.None,combat=false,closeFight=false,lLife=many("take",40),lCont=new(),lFollow=new(),lEnemy=new(),lCombat=new(),lCraft=many("store",41),lMine=new(),lDig=new(),lRemember=new(),lItem=new(),lExplore=new(),lDigExplore=new(),lOther=many("mine",walls),foodPicks=new()};
 var g3o=AICandidateAssembler.Select(g3,_=>true,_=>true,out _,out _,out _);AICandidateAssembler.Finish(g3o,new AsmCand{kind="wait",key="wait"});
 int total=g3o.Count+40+3;
 Console.WriteLine($"MEASURE review-5 G3: chest-side worst 41 store + 40 food + 43 life step, walls {walls} -> assembler {g3o.Count} + life step 43 = {total} ({(total<=255?"within":"OVER")} 255; chest-side alone {g3o.Count-walls+43})");
 Check(walls>0||total<=255,"review-5 G3: the chest-side worst alone stays within 255 (measured, not changed)");}}
// Review-5 G2: an off-screen chest with nine foods and an in-view source; duplicates and the running walk spend no route budget.
{AIBase.SeedFoodWarehouse();var g2=new FoodProbe{Goal=new AIGoal{kind=GoalKind.None},o=new AIObservation{hunger=10}};var g2r=AIBase.FoodTakeCandidates(default,default,g2.o,"","hungry");
 Check(g2r.Count(c=>c.kind=="dig")==1&&g2r.Exists(c=>c.kind=="take"&&c.itemId==ObjectID.Kelp),"review-5 G2: nine foods of one off-screen chest join one walk and the in-view source is still offered");
 Check(g2r.Single(c=>c.kind=="dig").desc.Contains("HeartBerry"),"review-5 G2: the joined walk names the other foods of that chest");
 g2r=AIBase.FoodTakeCandidates(default,default,g2.o,"dig:30,0","hungry");
 Check(g2r.Exists(c=>c.kind=="take"&&c.itemId==ObjectID.Kelp),"review-5 G2: while walking to that chest (the running action), its foods spend no route budget and the in-view source is offered");
 AIApproach.NoRoute.Add(new int2(30,0));g2r=AIBase.FoodTakeCandidates(default,default,g2.o,"","hungry");
 Check(g2r.Exists(c=>c.kind=="take"&&c.itemId==ObjectID.Kelp)&&!g2r.Exists(c=>c.kind=="dig"),"review-5 G2: nine missing routes of one chest spend the budget of 8; off-screen tries stop, the in-view source is still offered");
 AIApproach.NoRoute.Clear();AIBase.SeedFood();AIBase.InView=new();}
foreach(var goal in new[]{GoalKind.None,GoalKind.Gather}){var other=new FoodProbe{Goal=new(){kind=goal},gatherOre=goal==GoalKind.Gather};other.mates.Add(new(){entity=new Entity{Index=5},dist=8,name="host"});other.FollowVisible();Check(other.lFollow.Count==1&&!other.lFollow[0].progress,"actual visible follow producer works outside Follow/Explore/Free without inventing goal progress");AIMemory.SeedOres();other.wanted.Add(ObjectID.CopperOre);other.RememberedOres();Check(other.lRemember.Count==2&&other.lRemember.Find(c=>c.targetLabel.Contains("TinOre")).progress==false&&other.lRemember.Find(c=>c.targetLabel.Contains("CopperOre")).progress==other.gatherOre,"actual remembered ore producer exposes both known ore kinds outside wantedFirst restriction");}
var plant=new FoodProbe{Goal=new(){kind=GoalKind.Gather},gatherOre=true};plant.wanted.Add(ObjectID.Mushroom);var wild=new FoodPlant{plant=true,ripe=false,harvest=ObjectID.Mushroom,food=ObjectID.Mushroom};Check(plant.PlantEntry(wild,false,false)&&!plant.PlantProgress(wild),"actual wild unripe entry remains selectable for matching harvest goal without false harvest progress");Check(!plant.PlantEntry(wild,true,false)&&!plant.PlantEntry(wild,false,true),"actual owned growing crop and foreign crop protections remain");
Console.WriteLine("ALL PASS "+checks);
}}
namespace Unity.Mathematics {public struct int2:IEquatable<int2>{public int x,y;public int2(int a,int b){x=a;y=b;}public static bool operator ==(int2 a,int2 b)=>a.Equals(b);public static bool operator !=(int2 a,int2 b)=>!a.Equals(b);public bool Equals(int2 b)=>x==b.x&&y==b.y;public override bool Equals(object o)=>o is int2 b&&Equals(b);public override int GetHashCode()=>HashCode.Combine(x,y);}public static class math {public static bool all(bool b)=>b;public static int max(int a,int b)=>Math.Max(a,b);public static int min(int a,int b)=>Math.Min(a,b);public static float min(float a,float b)=>Math.Min(a,b);public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}}
namespace Unity.Entities{public struct Entity{public int Index;public static Entity Null=>default;public static bool operator ==(Entity a,Entity b)=>true;public static bool operator !=(Entity a,Entity b)=>false;public override bool Equals(object o)=>o is Entity;public override int GetHashCode()=>0;}public class World{public float3 Pos;public int2 Stand;public bool HasStand;public int Bag;public HashSet<int2> Tiles=new();}}
namespace UnityEngine{public static class Time{public static float realtimeSinceStartup;}}
namespace PugTilemap{}
public enum ObjectID{None,Wood,Mushroom,Berry,CopperOre,TinOre,Carrot,Pumpkin,Kelp,Wheat,GlowingTulipFlower,BloatOat,HeartBerry,Fish}public enum TileType{none,ground}public enum GoalKind{None,Gather,Follow,Goto,Place,Explore,Free}
public class AIGoal{public GoalKind kind;public ObjectID item;public int count;}public static class AIBrain{public static AIGoal Goal;}
public enum SkillStatus{Running,Done,Failed}public struct BodyInput{public static BodyInput Idle=>default;}
public class AISkill{public string Name,Key,Reason;public float StartTime;public SkillStatus Status;public List<string> Facts=new();public virtual bool Start(World w,Entity p,float n)=>true;public virtual BodyInput Tick(World w,Entity p,float n)=>default;public virtual void Cleanup(World w){}protected BodyInput Finish(SkillStatus st,string why){Status=st;Reason=why;return default;}}
public class PlaceSkill:AISkill{readonly ObjectID id;readonly int2 at;public bool TileFault;public PlaceSkill(ObjectID i,int2 t){id=i;at=t;}public override BodyInput Tick(World w,Entity p,float n){w.Bag--;w.Tiles.Add(at);AIPlaceGoal.NotePlaced(id,at);Status=SkillStatus.Done;return default;}}
public static class AIBody{public static int2 TileOf(float3 p)=>new((int)p.x,(int)p.z);public static float3 Pos(World w,Entity p)=>w.Pos;public static AISkill Current;public static Dictionary<ObjectID,int> InventoryCounts(World w,Entity p)=>new(){{ObjectID.Wood,w.Bag}};}
public class ObjectInfo{public TileType tileType;}public static class PugDatabase{public static ObjectInfo GetObjectInfo(ObjectID i,int v)=>new(){tileType=TileType.ground};}
public class TileLayer{public TileType tileType;}public class AITileReader{readonly World w;public AITileReader(World a){w=a;}public List<TileLayer> Layers(int2 t)=>w.Tiles.Contains(t)?new(){new(){tileType=TileType.ground}}:new();}
public static class AIPlaceSpot{public static Entity EntityOn(World w,ObjectID i,int2 t,int2 size,object unused)=>default;}
public static class AIPlayerSpikeMod{public static void Log(string s){}public static void LogError(string s,Exception e){}}
public class PlaceTarget{public PlaceRef Ref;public string Label;public List<int2> Shape;public int2 Center;public float ArriveR,AreaR;public List<int2> Stands=new();public List<int2> Plots;}


namespace Unity.Mathematics {public struct float2 {public float x,y;public float2(float a,float b){x=a;y=b;}public static explicit operator float2(int2 p)=>new(p.x,p.y);}public struct float3{public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}public float2 xz=>new(x,z);}}
public static class AIEdge{public static bool Enabled;public static bool PlanActive=>Enabled;public static List<AICandidate> Candidates(World w,Entity p,AIObservation o,string k,List<string>b)=>new(){new(){kind="edgefill",key="edge"}};public static List<AICandidate> GuardCandidates(World w,Entity p,AIObservation o,string k,List<string>b)=>new(){new(){kind="edgerepair",key="guard"}};public static bool IsInside(int2 p)=>false;public static int2 StandTile(World w,float3 p)=>new((int)p.x,(int)p.z);}

public class AICandidate {public Entity entity;public int2 tile;public bool progress,digWall;public string kind,key,desc,targetLabel;public ObjectID itemId;public int itemVariation,itemCount;}
public class AIObservation{public int hunger;public float3 pos;}
public partial class FoodProbe {public World server=new();public Entity player;public AIObservation o=new();public string curKey="";public List<AICandidate> lCombat=new(),lLife;public bool foodNeed,inCombat;public AIGoal Goal;}
public static partial class AIFood {public class Food{public ObjectID id=ObjectID.Mushroom;public int variation,hunger=10,count=5;public string heal="buff";}public static bool HaveFood=true;public static int HungerAdd(World w,ObjectID id,int v)=>id==ObjectID.Berry?20:id==ObjectID.Kelp?5:10;public static List<Food> InBag(World w,Entity p)=>HaveFood?new(){new()}:new();public static string Meaning(int h)=>"hunger facts";public const string Rule="observed food data";public static bool NeedsFood(World w,Entity p,AIObservation o)=>false;}
public static partial class AIBase {
 public class ChestMem{public int openedUnix=1;public ObjectID id=ObjectID.Wood;public int2 tile;public bool placed;public Dictionary<ObjectID,int> items=new();}public class ChestView{public Entity entity;public float3 pos;public float dist;public ObjectID id=ObjectID.Wood;}
 private class State {public Dictionary<int,ChestMem> chests=new();}private static State S=new();
 private const int RouteTriesPerDecision=8;   // production AIBase value (review-4 route budget)
 private static bool InViewChest(List<ChestView> inView,ChestMem c)=>inView.Exists(x=>math.all(AIBody.TileOf(x.pos)==c.tile)&&x.id==c.id);
 public static void SeedFood(){S.chests.Clear();S.chests[1]=new(){tile=new int2(1,0),items=new(){{ObjectID.Mushroom,3},{ObjectID.Berry,4}}};S.chests[2]=new(){tile=new int2(2,0),items=new(){{ObjectID.Mushroom,6}}};}
 public static List<ChestView> InView=new();public static List<ChestView> ChestsInView(World w,Entity p)=>InView;
 public static void SeedFoodWarehouse(){S.chests.Clear();var nine=new Dictionary<ObjectID,int>{{ObjectID.Mushroom,3},{ObjectID.Berry,4},{ObjectID.Carrot,2},{ObjectID.Pumpkin,2},{ObjectID.Fish,2},{ObjectID.Wheat,2},{ObjectID.GlowingTulipFlower,2},{ObjectID.BloatOat,2},{ObjectID.HeartBerry,2}};S.chests[1]=new(){tile=new int2(30,0),items=nine};S.chests[2]=new(){tile=new int2(1,0),items=new(){{ObjectID.Kelp,1}}};InView=new(){new ChestView{entity=new Entity{Index=77},pos=new float3(1,0,0),dist=1,id=ObjectID.Wood}};}
 public static string ChestText(ChestMem c,float3 p)=>$"chest ({c.tile.x},{c.tile.y})";   // TakeOrGo is extracted verbatim from production (review-5 G2)
}

public class SeenMate{public Entity entity;public int maxHealth=100,health=100;public bool isAi;public float dist;public float3 pos;public string name,role="host",dir="E",doing="observed";}
public partial class FoodProbe {public List<AICandidate> lCraft=new(),lEdge=new(),lFollow=new(),lRemember=new();public List<SeenMate> mates=new();public List<string> guardBlocks,blocks=new();public List<ObjectID>wanted=new();public bool gatherOre;public int toolRed;private static string Toward(AIObservation o,int2 p)=>"";private static TunnelPlan PlanC(World w,Entity p,int2 t,out string why){why="";return new();}private static int MinRed(int a,int b)=>Math.Min(a,b);}
public class TunnelPlan{public List<int> walls=new();public List<int2> path=new();public float EstimatedSeconds;public int totalHits;public string WallSummary()=>"";}
public static class AIAgent{public sealed class Agent{public string Name="Tester";}public static Agent Cur=new();}
public static class AICraft{public static List<int2> StandTilesFor(World w,Entity p,AIMemory.Station s)=>new(){s.tile};}
public static class AIApproach{public static HashSet<int2> NoRoute=new();public static TunnelPlan FirstCached(World w,Entity p,List<int2> stands,out int2 st,out string why){st=stands[0];why="fixture";return NoRoute.Contains(st)?null:new TunnelPlan();}public static AICandidate WalkCandidate(int2 st,string name,bool progress,string desc)=>new AICandidate{kind="dig",key=$"dig:{st.x},{st.y}",desc=desc,progress=progress};}public static class AITunnel{public static int LastToolShortRed=>0;}
public static partial class AIMemory {public struct Station{public ObjectID id;public int2 tile;}public struct Ore{public ObjectID ore;public long seenUnix;}private static Dictionary<int2,Ore> _ores=new();private static void Ensure(){}public static void SeedOres(){_ores.Clear();_ores[new int2(5,0)]=new(){ore=ObjectID.CopperOre};_ores[new int2(8,0)]=new(){ore=ObjectID.TinOre};}public static string Ago(long u)=>"last observed";}
public static class AIVision{public static bool CanSee(float3 p,int2 t)=>false;}
public class FoodPlant{public bool plant,ripe;public ObjectID harvest,food;}public class GrowingCD{}public struct LocalTransform{public float3 Position;}public class PlantEcs{public bool HasComponent<T>(Entity e)=>false;public T GetComponentData<T>(Entity e)=>default;}
public static class AIFarm{public static bool Own,Foreign;public static bool ForeignCrop(World w,Entity e)=>Foreign;public static bool IsOwnPlot(int2 t)=>Own;}public static class AIDrops{public static bool Gave(int i,ObjectID w)=>true;}

// The leaf-eligibility adapter resolves registered routes immediately. Production catalog routing is checked separately.
public partial class FoodProbe { private void DeferRoute(string key,int2 at,string label,Action<TunnelPlan,string> complete){var p=PlanC(server,player,at,out var why);complete(p,why);} }
