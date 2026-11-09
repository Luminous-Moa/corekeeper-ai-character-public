// Only Unity/ECS, game data and input boundaries. Candidate, validation and skill policies are linked production.
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
namespace Unity.Collections { public enum Allocator { Temp } }
namespace Unity.Transforms { public struct LocalTransform { public float3 Position; } }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace Unity.Mathematics {
 public struct int2 { public int x,y; public int2(int a,int b){x=a;y=b;} public static explicit operator float2(int2 v)=>new float2(v.x,v.y); }
 public struct float2 {public float x,y;public float2(float a,float b){x=a;y=b;}}
 public struct float3 {public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}public float2 xz=>new float2(x,z);}
 public static class math { public static int min(int a,int b)=>Math.Min(a,b);public static int max(int a,int b)=>Math.Max(a,b); public static float max(float a,float b)=>Math.Max(a,b);public static float round(float a)=>(float)Math.Round(a);public static float ceil(float a)=>(float)Math.Ceiling(a);public static int clamp(int x,int a,int b)=>Math.Clamp(x,a,b); public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
}
namespace Unity.Entities {
 public struct Entity : IEquatable<Entity> {public int Index,Version;public static Entity Null=>default;public bool Equals(Entity o)=>Index==o.Index&&Version==o.Version;public override bool Equals(object o)=>o is Entity e&&Equals(e);public override int GetHashCode()=>HashCode.Combine(Index,Version);public static bool operator==(Entity a,Entity b)=>a.Equals(b);public static bool operator!=(Entity a,Entity b)=>!a.Equals(b);}
 public struct BlobAssetReference<T> {public T Value;}
 public struct ComponentType {public Type Type;public static ComponentType ReadOnly<T>()=>new ComponentType{Type=typeof(T)};}
 public sealed class DynamicBuffer<T> : List<T> {public int Length=>Count;}
 public sealed class EntityArray:List<Entity>,IDisposable {public void Dispose(){}}
 public sealed class EntityQuery:IDisposable {readonly EntityManager em;readonly ComponentType[] types;public EntityQuery(EntityManager e,ComponentType[] t){em=e;types=t;}public EntityArray ToEntityArray(Unity.Collections.Allocator a){var l=new EntityArray();l.AddRange(em.All.Where(e=>types.All(t=>em.Has(e,t.Type))));return l;}public int CalculateEntityCount()=>ToEntityArray(default).Count;public T GetSingleton<T>()=>em.GetComponentData<T>(ToEntityArray(default)[0]);public void Dispose(){}}
 public sealed class EntityManager {readonly Dictionary<(Entity,Type),object> values=new Dictionary<(Entity,Type),object>();public readonly List<Entity> All=new List<Entity>();public Entity New(){var e=new Entity{Index=All.Count+1,Version=1};All.Add(e);return e;}public bool Has(Entity e,Type t)=>values.ContainsKey((e,t));public bool Exists(Entity e)=>All.Contains(e);public bool HasComponent<T>(Entity e)=>Has(e,typeof(T));public bool HasBuffer<T>(Entity e)=>Has(e,typeof(DynamicBuffer<T>));public T GetComponentData<T>(Entity e)=>(T)values[(e,typeof(T))];public DynamicBuffer<T> GetBuffer<T>(Entity e,bool readOnly=false)=>(DynamicBuffer<T>)values[(e,typeof(DynamicBuffer<T>))];public void Set<T>(Entity e,T v)=>values[(e,typeof(T))]=v;public DynamicBuffer<T> Buffer<T>(Entity e,params T[] v){var b=new DynamicBuffer<T>();b.AddRange(v);Set(e,b);return b;}public EntityQuery CreateEntityQuery(params ComponentType[] types)=>new EntityQuery(this,types);}
 public sealed class World {public EntityManager EntityManager=new EntityManager();}
}
namespace Inventory { public sealed class Placeholder {} }
public enum ObjectID { None, Mushroom, Berry, Root, CookedSoup, CookingPot }
public enum ConditionID {None,HungerAddition,IncreasedMaxHealthPermanent}
public enum Rarity {Common,Rare,Epic}
public sealed class ObjectInfo {public bool isStackable=true;public Rarity rarity;}
public static class PugDatabase {public struct PugDatabaseBank {} public struct DatabaseBankCD{public BlobAssetReference<PugDatabaseBank> databaseBankBlob;}public static readonly Dictionary<ObjectID,Entity> Prefabs=new Dictionary<ObjectID,Entity>();public static Entity GetPrimaryPrefabEntity(ObjectID id,BlobAssetReference<PugDatabaseBank> bank,int variation=0)=>Prefabs.TryGetValue(id,out var p)?p:Entity.Null;public static ObjectInfo GetObjectInfo(ObjectID id,int variation=0)=>new ObjectInfo();}
public struct ObjectDataCD { public ObjectID objectID;public int amount,variation,variationUpdateCount; }
public struct ContainedObjectsBuffer {public ObjectDataCD objectData;public int auxDataIndex;public ObjectID objectID=>objectData.objectID;public int amount=>objectData.amount;public int variation=>objectData.variation;public int variationUpdateCount=>objectData.variationUpdateCount;public bool EqualsExact(ContainedObjectsBuffer b)=>EqualsIgnoreAmount(b)&&amount==b.amount;public bool EqualsIgnoreAmount(ContainedObjectsBuffer b)=>objectID==b.objectID&&variation==b.variation&&auxDataIndex==b.auxDataIndex&&variationUpdateCount==b.variationUpdateCount;}
public struct InventoryBuffer {public int startIndex,size;public bool objectsGetLockedInPlace,canOnlyContainOneItemPerSlot;}
public struct CookingIngredientCD {public ObjectID turnsIntoFood;}
public enum CraftingType {Cooking,Other}
public struct CraftingCD {public CraftingType craftingType;public int outputSlotIndex;}
public struct PlayerGhost {}
public struct CraftingTimerSlotBuffer {public float timeLeftToCraft;}
public struct CookedFoodCD {public static int PredictReads;public static ObjectID GetPrimaryIngredient(ObjectID a,ObjectID b){PredictReads++;return a;}public static int GetFoodVariation(ObjectID a,ObjectID b)=>(int)a*100+(int)b;public static ObjectID GetPrimaryIngredientFromVariation(int v)=>(ObjectID)(v/100);public static ObjectID GetSecondaryIngredientFromVariation(int v)=>(ObjectID)(v%100);public static bool IngredientShouldBePrimary(ObjectID id)=>false;}
public struct ConditionData {public ConditionID conditionID;public int value;}
public struct ConditionDataContainer {public ConditionData conditionData,conditionDataWhenCooked;}
public struct GivesConditionsWhenConsumedBuffer {public ConditionDataContainer conditionDataContainer;}
public static class AISlot {public const int Cook=14;}
public sealed class AIAgent {public string Name="test";public static AIAgent Cur=new AIAgent();readonly Dictionary<int,object> s=new Dictionary<int,object>();public T Get<T>(int key) where T:class,new(){if(!s.ContainsKey(key))s[key]=new T();return (T)s[key];}}
public static class AIPlayerSpikeMod {public static void Log(string s){}public static void LogError(string s,Exception e){}public static void Reply(string s){}}
public static class AIMemory {public sealed class Data{public int[] potId,potXY;public long[] potSeen;}public sealed class Station{public ObjectID id;public int2 tile;}public static void MarkDirty(){}public static void Save(bool force){}public static string Ago(long t)=>"last seen";}
public sealed class AIObservation {public float3 pos;}
public enum GoalKind {None,Cook,Free}
public sealed class AIGoal {public GoalKind kind;}
public static class AIBrain {public static AIGoal Goal=new AIGoal();public static string PrepNote=>"";public static Plan PlanC(World w,Entity p,int2 t,out string why){why="";return new Plan();}public static void ObserveExecution(string s){}}
public sealed class LifeSelection {public string Branch,Mode,Item,Second,Where;public int RequestedCount,OfferedCount;public bool MatchesGoal;public LifeSelection Copy()=>(LifeSelection)MemberwiseClone();}
public interface ILifeResult {int ConfirmedWork{get;}int ConfirmedReceived{get;}int RequestedWork{get;}string ResultStage{get;}}
public static class AILife {public sealed class Entry {public string Op,Id,Text;public AICandidate Candidate;public Func<Entry> Observe;}public static bool RequestSatisfied(LifeSelection s)=>false;public static LifeSelection ForGoal(AIGoal g)=>new LifeSelection{Branch="cook"};}
public sealed class AICandidate {public string kind,key,desc,actionMode;public ObjectID itemId,secondItemId;public int itemVariation,itemCount,waitSeconds;public Entity entity;public int2 tile;public bool progress;public LifeSelection life;public AICook.Selection cook;}
public static class AIVision {public static bool Visible=true;public static bool CanSee(float3 a,float2 b)=>Visible;public static bool CanSee(float3 a,int2 b)=>Visible;}
public static class AIFood {public sealed class Food{public ObjectID id;public int variation,count,hunger;}public static List<Food> InBag(World w,Entity p)=>new List<Food>();public static int HungerAdd(World w,ObjectID id,int variation)=>id==ObjectID.Root?40:5;}
public static class AIBody {public static AISkill Current;public static float3 Pos(World w,Entity p)=>w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(p).Position;public static int2 TileOf(float3 p)=>new int2((int)p.x,(int)p.z);public static float2 DirTo(float3 p,float2 d)=>new float2(d.x-p.x,d.y-p.z);public static Dictionary<ObjectID,int> InventoryCounts(World w,Entity p){var d=new Dictionary<ObjectID,int>();foreach(var x in w.EntityManager.GetBuffer<ContainedObjectsBuffer>(p)){d.TryGetValue(x.objectID,out int n);d[x.objectID]=n+x.amount;}return d;}}
public static class AIInventory {public static string Gate(World w,Entity p)=>null;public static int MainStart(World w,Entity p)=>0;public static int MainEnd(World w,Entity p)=>w.EntityManager.GetBuffer<InventoryBuffer>(p)[0].size;public static bool Read(World w,Entity p,int i,out ContainedObjectsBuffer x){x=default;if(!w.EntityManager.HasBuffer<ContainedObjectsBuffer>(p))return false;var b=w.EntityManager.GetBuffer<ContainedObjectsBuffer>(p);if(i<0||i>=b.Count)return false;x=b[i];return true;}public static string SlotAllows(World w,Entity p,int i,ContainedObjectsBuffer x)=>null;public static ObjectInfo Info(ContainedObjectsBuffer x)=>PugDatabase.GetObjectInfo(x.objectID,x.variation);public static bool Identity(ContainedObjectsBuffer a,ContainedObjectsBuffer b)=>a.objectID==b.objectID&&a.variation==b.variation&&a.auxDataIndex==b.auxDataIndex;public static int Quantity(ContainedObjectsBuffer x)=>x.objectID==ObjectID.None?0:x.amount;public static System.Collections.Generic.List<int> CarriedSlots(World w,Entity p){var l=new System.Collections.Generic.List<int>();for(int i=MainStart(w,p);i<MainEnd(w,p);i++)l.Add(i);return l;}}
public sealed class Plan {public List<int2> path=new List<int2>();public float EstimatedSeconds;}
public static class AIApproach {public static Plan FirstCached(World w,Entity p,List<int2> t,out int2 st,out string why){st=t[0];why="";return new Plan();}public static AICandidate WalkCandidate(int2 tile,string label,bool progress,string text)=>new AICandidate{kind="dig",key=$"dig:{tile.x},{tile.y}",desc=text};}
public static class AIBase {public const float ChestReach=2;public static int2 BaseTile;public static string BaseText=>"base";public static float BaseDist(float3 p)=>0;public static bool AtBase(float3 p)=>true;public static bool BaseStand(World w,Entity p,out int2 t){t=default;return true;}}
public static class AIPlaceSpot {public static void AddUnique(List<AICandidate> d,List<AICandidate> x)=>d.AddRange(x);public static List<AICandidate> Candidates(World w,Entity p,ObjectID id,string label,string reason,bool progress,out string why){why="";return new List<AICandidate>();}}
public struct MoveRequest {public Entity From,To;public int FromSlot,ToSlot,Cursor,N;}
public static class Create {public static MoveRequest MoveOrDropAmount(Entity f,int fs,Entity t,int ts,int cursor,int n,float3 pos)=>new MoveRequest{From=f,FromSlot=fs,To=t,ToSlot=ts,Cursor=cursor,N=n};}
public static class AICraft {public static readonly Queue<MoveRequest> Requests=new Queue<MoveRequest>();public static bool QueueInventoryChange(World w,Entity p,MoveRequest request){Requests.Enqueue(request);return true;}public static List<int2> StandTilesFor(World w,Entity p,AIMemory.Station s)=>new List<int2>{s.tile};}
public enum NavStatus {Arrived,Failed,Moving}
public sealed class AINavigator {public string FailReason="";public void Start(World w,Entity p,int2 tile,float reach,float now,Entity pot){}public NavStatus Tick(World w,Entity p,float now,out float2 move){move=default;return NavStatus.Arrived;}public void Dispose(World w){}}
public enum SkillStatus {Running,Done,Failed,Cancelled}
public struct BodyInput {public float2 face,move;public int slot;public static BodyInput Idle=>new BodyInput{slot=-1};}
public abstract class AISkill {public string Name,Key,Reason;public SkillStatus Status=SkillStatus.Running;public List<string> Facts=new List<string>();public abstract bool Start(World w,Entity p,float now);public abstract BodyInput Tick(World w,Entity p,float now);public virtual string Progress(World w,Entity p)=>"";public virtual void Cleanup(World w){}protected BodyInput Finish(SkillStatus s,string why){Status=s;Reason=why;return BodyInput.Idle;}protected void ObserveOnce(string key,string text){Facts.Add(text);}}
