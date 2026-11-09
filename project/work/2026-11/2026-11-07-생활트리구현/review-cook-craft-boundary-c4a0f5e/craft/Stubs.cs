using System.Collections;
namespace Unity.Collections { public enum Allocator {Temp} }
namespace Unity.Mathematics {
 public struct float2 {public float x,y;public float2(float a,float b){x=a;y=b;}public static implicit operator float2(int2 v)=>new(v.x,v.y);}
 public struct float3 {public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}public float2 xz=>new(x,z);}
 public struct int2 {public int x,y;public int2(int a,int b){x=a;y=b;}public static bool operator ==(int2 a,int2 b)=>a.x==b.x&&a.y==b.y;public static bool operator !=(int2 a,int2 b)=>!(a==b);public static int2 operator +(int2 a,int2 b)=>new(a.x+b.x,a.y+b.y);public static int2 operator -(int2 a,int2 b)=>new(a.x-b.x,a.y-b.y);public static int2 operator /(int2 a,int b)=>new(a.x/b,a.y/b);public static int2 zero=>default;public override bool Equals(object x)=>x is int2 y&&this==y;public override int GetHashCode()=>HashCode.Combine(x,y);}
 public static class math {public static float round(float x)=>MathF.Round(x);public static float floor(float x)=>MathF.Floor(x);public static int min(int a,int b)=>Math.Min(a,b);public static float min(float a,float b)=>Math.Min(a,b);public static int max(int a,int b)=>Math.Max(a,b);public static float max(float a,float b)=>Math.Max(a,b);public static bool all(bool a)=>a;public static float distance(float2 a,float2 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
}
namespace Unity.Transforms {public struct LocalTransform {public Unity.Mathematics.float3 Position;}}
namespace Unity.Entities {
 public struct Entity {public int Index, Version;public static Entity Null=>default;public static bool operator ==(Entity a,Entity b)=>a.Index==b.Index;public static bool operator !=(Entity a,Entity b)=>!(a==b);public override bool Equals(object o)=>o is Entity e&&e==this;public override int GetHashCode()=>Index;public override string ToString()=>Index.ToString();}
 public class World {public bool IsCreated=true;public EntityManager EntityManager=new();}
 public class DynamicBuffer<T>:IEnumerable<T> {readonly List<T> values=new();public int Length=>values.Count;public T this[int i]{get=>values[i];set=>values[i]=value;}public void Add(T x)=>values.Add(x);public IEnumerator<T> GetEnumerator()=>values.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
 public class EntityManager {
 readonly Dictionary<int,int> versions=new();readonly Dictionary<(int,Type),object> data=new();readonly HashSet<int> deleted=new();
 public bool Exists(Entity e)=>e.Index>0&&!deleted.Contains(e.Index);
 public void Remove(Entity e)=>deleted.Add(e.Index);
 public bool HasBuffer<T>(Entity e)=>data.ContainsKey((e.Index,typeof(DynamicBuffer<T>)));
 public bool HasComponent<T>(Entity e)=>data.ContainsKey((e.Index,typeof(T)));
 public DynamicBuffer<T> GetBuffer<T>(Entity e,bool readOnly=false)=>(DynamicBuffer<T>)data[(e.Index,typeof(DynamicBuffer<T>))];
 public T GetComponentData<T>(Entity e)=>(T)data[(e.Index,typeof(T))];
 public void Put<T>(Entity e,T x){versions[e.Index]=e.Version;data[(e.Index,typeof(T))]=x;}
 public void PutBuffer<T>(Entity e,DynamicBuffer<T> b){versions[e.Index]=e.Version;data[(e.Index,typeof(DynamicBuffer<T>))]=b;}
 public Query CreateEntityQuery(params object[] x)=>new(this,x.Cast<Type>().ToArray());
 public Entity[] Find(Type[] types)=>data.Keys.Select(k=>k.Item1).Distinct().Where(i=>!deleted.Contains(i)&&types.All(t=>data.ContainsKey((i,t))||data.ContainsKey((i,typeof(DynamicBuffer<>).MakeGenericType(t))))).Select(i=>new Entity{Index=i,Version=versions.GetValueOrDefault(i)}).ToArray();
 }
 public static class ComponentType {public static object ReadOnly<T>()=>typeof(T);public static object ReadWrite<T>()=>typeof(T);}
 public sealed class TempEntities:IEnumerable<Entity>,IDisposable {readonly Entity[] items;public TempEntities(Entity[] x){items=x;}public IEnumerator<Entity> GetEnumerator()=>((IEnumerable<Entity>)items).GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>items.GetEnumerator();public void Dispose(){}}
 public class Query:IDisposable {readonly EntityManager em;readonly Type[] types;public Query(EntityManager e,Type[] t){em=e;types=t;}public int CalculateEntityCount()=>em.Find(types).Length;public T GetSingleton<T>()=>em.GetComponentData<T>(em.Find(types)[0]);public Entity GetSingletonEntity()=>em.Find(types)[0];public TempEntities ToEntityArray(Unity.Collections.Allocator a)=>new(em.Find(types));public void Dispose(){}}
}
public enum ObjectID {None,Wood,WallDirtBlock,CopperOre,TinOre,TinMiningPick,IronMiningPick,ScarletMiningPick,OctarineMiningPick,GalaxiteMiningPick,SolariteMiningPick,WoodenWorkBench,WoodSword,WoodMiningPick,CopperMiningPick,CopperBar,Furnace,Torch,SalvageAndRepairStation,CopperBreastArmor,CopperHelm,CopperSword,MiningPick,Chest,Ring,Pouch,Bag}
public enum ObjectType {Helm,BreastArmor,PantsArmor,Necklace,Ring,Offhand,Bag,Lantern,Pouch,MiningPick,MeleeWeapon,NonUsable}
public enum Rarity {Common,Legendary}
public class ObjectInfo {public ObjectWithAmount[] requiredObjectsToCraft;public Unity.Mathematics.int2 prefabTileSize,prefabCornerOffset;public bool centerIsAtEntityPosition;public bool isStackable;public ObjectType objectType;public Rarity rarity;}
public struct ObjectDataCD {public ObjectID objectID;public int amount,variation,variationUpdateCount;}
public struct ContainedObjectsBuffer {public ObjectDataCD objectData;public int auxDataIndex;public ObjectID objectID=>objectData.objectID;public int variation=>objectData.variation;public int amount=>objectData.amount;public int variationUpdateCount=>objectData.variationUpdateCount;public bool EqualsExact(ContainedObjectsBuffer x)=>objectID==x.objectID&&variation==x.variation&&amount==x.amount&&variationUpdateCount==x.variationUpdateCount&&auxDataIndex==x.auxDataIndex;}
public struct InventoryBuffer {public int startIndex,size,sizeX,sizeY,extraInventorySizeSlot,maxSize,extraSize;public ulong extraInventoryCategoryTagsMask;public bool cantAddObjectsToInventory,canOnlyContainOneItemPerSlot,objectsGetLockedInPlace;}
public struct InventoryAutoTransferEnabledCD {}
public struct InventorySlotRequirementBuffer {public int inventoryIndex,slotIndex;public bool requirementAppliesToAllSlots,denyLegendaryRarity;public ulong acceptsObjectsWithTags;public ObjectID[] acceptsObjectIds;}
public struct EquipmentCD {public int helmSlotIndex,breastSlotIndex,pantsSlotIndex,necklaceSlotIndex,ring1SlotIndex,ring2SlotIndex,offHandIndex,bagIndex,lanternIndex,pouch1Index;public int GetPouchSlotIndex(int i)=>pouch1Index+i;}
public struct EquipmentPresetsBuffer {public EquipmentCD equipment;}
public struct ActiveEquipmentPresetCD {public int Value;}
public struct UIActionBuffer {public Unity.NetCode.NetworkTick tick;public UIInputActionData actionData;}
public struct HealthCD {public int health;}
public struct WorldInfoCD {public bool guestMode;}
public struct PlayerGhost {public int adminPrivileges;}
public enum CraftingType {Manual,ProcessResources} public struct CraftingCD {public int outputSlotIndex;public CraftingType craftingType;public static bool IsProcessAutoCrafter(CraftingType t)=>t==CraftingType.ProcessResources;}
public struct ObjectCategoryTagsCD {public ulong tagsBitMask;}
public struct OverrideLegendaryForSlotRequirementsCD {}
public struct ExtraInventoryCD {public int size;public ulong categoryTagsMask;}
public struct LevelEntitiesBuffer {public Unity.Entities.Entity entity;}
public struct LevelCD {public int level;}
public struct EquippedObjectCD {public int equippedSlotIndex;public ContainedObjectsBuffer containedObject;}
public enum PickUpItemState {BlockPickupUntilReEnterStart,IsBeingPickedUp,ForcePickUp,HasBeenPickedUp}
public struct PickUpItemCD {public Unity.Entities.Entity targetEntity;public PickUpItemState state;}
public static class LevelScaling {public static int GetMaxLevel()=>20;}
public static class PugDatabase {public struct CraftingSettings {public ObjectCategoryTag canOnlyUseAnyMaterialsWithTag;}public struct EntityObjectInfo {public bool isStackable;public int initialAmount;public CraftingSettings CraftingSettings;public FakeArray<ObjectWithAmount> requiredObjectsToCraft;}public static EntityObjectInfo[] Data=new EntityObjectInfo[100];public static ref EntityObjectInfo GetEntityObjectInfo(ObjectID id,object bank)=>ref Data[(int)id];public static ref FakeArray<ObjectWithAmount> GetRequiredObjectsToCraft(ObjectID id,object bank)=>ref Data[(int)id].requiredObjectsToCraft;
 public struct DatabaseBankCD {public object databaseBankBlob;}
 public static ObjectInfo GetObjectInfo(ObjectID id,int v=0)=>id==ObjectID.None?null:new(){requiredObjectsToCraft=id==ObjectID.Torch?new[]{new ObjectWithAmount{objectID=ObjectID.Wood,amount=1}}:null,isStackable=id==ObjectID.Wood||id==ObjectID.Torch,objectType=id==ObjectID.CopperBreastArmor?ObjectType.BreastArmor:id==ObjectID.CopperSword?ObjectType.MeleeWeapon:id==ObjectID.MiningPick?ObjectType.MiningPick:id==ObjectID.Wood?ObjectType.NonUsable:id==ObjectID.Ring?ObjectType.Ring:id==ObjectID.Pouch?ObjectType.Pouch:id==ObjectID.Bag?ObjectType.Bag:ObjectType.Helm};
 public static Unity.Entities.Entity GetPrimaryPrefabEntity(ObjectID id,object blob,int variation)=>new(){Index=1000+(int)id};
}
public static class PlayerController {public static bool ItemIsBroken(ObjectDataCD d)=>d.amount==0;}
public enum GoalKind {Equip,Unequip,Hold,Store,Take,Drop,Give,None,Free,Farm,Cook,Craft,Gather,Other}
public class AIGoal {public GoalKind kind;public ObjectID item;public int slot=-1,keep=-1,count,startCount;public string where="",itemName="",mode="",second="";}
public static class AIGoals {public static int GoalRevision;public static AIGoal Goal;}
public class AIAgent {public string Id=Guid.NewGuid().ToString();public object[] Slots=new object[1];public T Get<T>(int slot) {if(Slots[slot]==null)Slots[slot]=Activator.CreateInstance(typeof(T),true);return(T)Slots[slot];}public string Name="fixture";public static AIAgent Cur=new();}
public class AIObservation {public static AIObservation Take(Unity.Entities.World w,Unity.Entities.Entity p,bool d)=>new(){pos=AIBody.Pos(w,p)};public Unity.Mathematics.float3 pos;}
public class AICandidate {public AICraftSelection craft;public Unity.Entities.Entity entity;public string targetLabel;public string kind,actionMode,key,desc;public LifeSelection life;public AIInventoryMove inventory;public ObjectID itemId;public int itemVariation,itemCount;public bool progress;}
public static class AIBody {public static bool HasWorkingPick(Unity.Entities.World w,Unity.Entities.Entity p,out bool broken){broken=false;return true;}public static ObjectID FindWeapon(Unity.Entities.World w,Unity.Entities.Entity p)=>ObjectID.CopperSword;public static Dictionary<ObjectID,int> InventoryCounts(Unity.Entities.World w,Unity.Entities.Entity p)=>new();public static int MainEnd(Unity.Entities.EntityManager em,Unity.Entities.Entity p,int len)=>Math.Min(40,len);public static Unity.Mathematics.float3 Pos(Unity.Entities.World w,Unity.Entities.Entity p)=>w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(p).Position;public static Unity.Mathematics.int2 TileOf(Unity.Mathematics.float3 p)=>new((int)p.x,(int)p.z);}
public enum SkillStatus {Running,Failed,Done}
public struct BodyInput {public int slot;public Unity.Mathematics.float2 move;public static BodyInput Idle=>new(){slot=-1};}
public abstract class AISkill {public string Name,Key,Reason;public float StartTime;public SkillStatus Status;public List<string> Facts=new();public abstract bool Start(Unity.Entities.World w,Unity.Entities.Entity p,float now);public abstract BodyInput Tick(Unity.Entities.World w,Unity.Entities.Entity p,float now);public virtual string Progress(Unity.Entities.World w,Unity.Entities.Entity p)=>"fixture";public virtual void Cleanup(Unity.Entities.World w){}protected void ObserveOnce(string key,string reason){if(!Facts.Contains(reason))Facts.Add(reason);}protected BodyInput Finish(SkillStatus s,string reason){Status=s;Reason=reason;return BodyInput.Idle;}}

namespace Inventory {
 public class InventoryChangeData {public string mode;public Unity.Entities.Entity fromEntity,toEntity;public int from,to,count;public Unity.Mathematics.float3 pos;}
 public static class Create {public static CraftActionData Craft(ObjectID item,int amount,int free,Unity.Entities.Entity p,Unity.Entities.Entity station)=>new(){objectId=item,amount=amount,craftingEntity=station};public static InventoryChangeData MoveOrDropItem(Unity.Entities.Entity a,int source,Unity.Entities.Entity b,int dest,int output,Unity.Mathematics.float3 pos)=>new(){mode="stow",fromEntity=a,toEntity=b,from=source,to=dest};
 public static InventoryChangeData Swap(Unity.Entities.Entity a,Unity.Entities.Entity b,int x,int y)=>new(){mode="swap",fromEntity=a,toEntity=b,from=x,to=y};
 public static InventoryChangeData MoveAmount(Unity.Entities.Entity a,int x,Unity.Entities.Entity b,int y,int end,int count,bool destroy)=>new(){mode="move",fromEntity=a,toEntity=b,from=x,to=y,count=count};
 public static InventoryChangeData DropItem(Unity.Entities.Entity a,int x,int count,Unity.Mathematics.float3 pos,Unity.Entities.Entity blocked)=>new(){mode="drop",fromEntity=a,from=x,count=count,pos=pos};
 }}
public class AINavigator {public string FailReason="blocked";public void Start(Unity.Entities.World w,Unity.Entities.Entity p,Unity.Mathematics.int2 t,float r,float n,Unity.Entities.Entity e){}public NavStatus Tick(Unity.Entities.World w,Unity.Entities.Entity p,float n,out Unity.Mathematics.float2 move){move=default;return NavStatus.Arrived;}public void Dispose(Unity.Entities.World w){}}
public enum NavStatus {Running,Failed,Arrived}
public static class AIVision {public static bool CanSee(Unity.Mathematics.float3 p,Unity.Mathematics.int2 t)=>true;public static bool CanSee(Unity.Mathematics.float3 p,Unity.Mathematics.float2 t)=>true;}
public static class AIMemory {public struct Station {public ObjectID id;public Unity.Mathematics.int2 tile;public long seenUnix;public bool placed;}public static string Ago(long x)=>"last seen";public static List<Station> RememberedStations(Unity.Mathematics.float3 p)=>new();public static bool SeenTile(Unity.Mathematics.int2 p)=>true;}
public class SeenPlayer {public Unity.Entities.Entity entity;public string name;public bool isAi;public int health=100,maxHealth=100;public Unity.Mathematics.float3 pos;}
public static class AITeammates {public static List<SeenPlayer> Seen=new();public static List<SeenPlayer> Visible(Unity.Entities.World w,Unity.Entities.Entity p)=>Seen;}
public static class AIApproach {public class Route {public List<Unity.Mathematics.int2> path=new(),walls=new();public float EstimatedSeconds;public string WallSummary()=>"fixture";}public static Route FirstCached(Unity.Entities.World w,Unity.Entities.Entity p,List<Unity.Mathematics.int2> targets,out Unity.Mathematics.int2 stand,out string why){stand=targets[0];why=null;return new();}public static AICandidate WalkCandidate(Unity.Mathematics.int2 t,string label,bool progress,string desc)=>new(){kind="walk",key="walk:"+t.x+","+t.y,desc=desc,progress=progress};}
public static class AIBase {public static Dictionary<ObjectID,int> Missing=new();public static void ResetMissing()=>Missing.Clear();public static void NoteMissing(ObjectID item,int need)=>Missing[item]=need;public static ObjectID ChestTarget(Unity.Entities.World w,Unity.Entities.Entity p)=>ObjectID.None;public static Unity.Mathematics.int2 BaseTile=>default;
 public const float ChestReach=2,BaseRadius=10;public static readonly List<ChestMem> Memories=new();
 public class ChestMem {public ObjectID id;public Unity.Mathematics.int2 tile;public int slots,free;public long openedUnix;public Dictionary<ObjectID,int> items=new();}
 public class ChestView {public Unity.Entities.Entity entity;public ObjectID id;public Unity.Mathematics.float3 pos;public float dist;}
 public static List<ChestMem> OwnChests()=>new(Memories);
 public static float BaseDistTile(Unity.Mathematics.int2 t)=>Unity.Mathematics.math.distance(default,t);
 public static ChestMem Own(ChestView v)=>Memories.Find(m=>m.id==v.id&&m.tile==AIBody.TileOf(v.pos));
 public static string ChestText(ChestMem m,Unity.Mathematics.float3 p)=>"known own chest";
 public static bool StorableKind(Unity.Entities.World w,ObjectID id,int variation)=>id==ObjectID.Wood;
 public static List<ChestView> ChestsInView(Unity.Entities.World w,Unity.Entities.Entity p) {var result=new List<ChestView>();using var q=w.EntityManager.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<InventoryAutoTransferEnabledCD>(),Unity.Entities.ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());using var es=q.ToEntityArray(Unity.Collections.Allocator.Temp);foreach(var e in es){var pos=w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(e).Position;result.Add(new(){entity=e,pos=pos,id=w.EntityManager.GetComponentData<ObjectDataCD>(e).objectID,dist=Unity.Mathematics.math.distance(AIBody.Pos(w,p).xz,pos.xz)});}return result;}
 public static ChestMem NoteOpened(Unity.Entities.World w,Unity.Entities.Entity chest)=>Own(new(){entity=chest,pos=w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(chest).Position,id=w.EntityManager.GetComponentData<ObjectDataCD>(chest).objectID});
}





namespace Pug.Properties {}
namespace PugTilemap {}
namespace UnityEngine {}
namespace Unity.NetCode {public struct NetworkTick {public uint Value;public void Add(uint amount){Value+=amount;}}public struct NetworkTime {public NetworkTick ServerTick;}}
public enum ObjectCategoryTag {None,Any}
public enum ConditionID {ReducedAnvilEquipmentCosts}
public struct AnvilCD {}
public struct SummarizedConditionsBuffer {}
public static class EntityUtility {public static int GetConditionValue(ConditionID id,Unity.Entities.Entity entity,Unity.Entities.World world)=>0;}
public struct ObjectWithAmount {public ObjectID objectID;public int amount;}
public struct FakeArray<T> {public T[] Values;public int Length=>Values?.Length??0;public T this[int i]=>Values[i];}
public struct CanCraftObjectsBuffer {public ObjectID objectID;public int amount,entityAmountToConsume;public bool allowCraftingNone;public float craftingTimeOverride;}
public struct CraftBuffer {public Unity.Entities.Entity playerEntity;public Inventory.CraftActionData craftActionData;}
public enum UIInputAction {Craft,InventoryChange}
public struct UIInputActionData {public UIInputAction action;public Inventory.InventoryChangeData inventoryChangeData;public Inventory.CraftActionData craftActionData;}
public struct EnemyCD {}
public struct TileCD {}
public static class AIPlayerSpikeMod {public static void Log(string s){}public static void LogError(string s,Exception e){}public static void Reply(string s){}}
public static class AIPlaces {public static string ObjectId(string prefix,string id,int x,int y)=>$"{prefix}:{id}@{x},{y}";public static string CraftFact(params object[] a)=>"fixture";}
public class AITileReader {public AITileReader(Unity.Entities.World w){}public IEnumerable<TopData> Layers(Unity.Mathematics.int2 p)=>Array.Empty<TopData>();public bool Walkable(Unity.Mathematics.int2 p)=>true;public TopData Top(Unity.Mathematics.int2 p)=>new();public struct TopData {public object tileType;public int tileset;}}
namespace Inventory {public struct CraftActionData {public ObjectID objectId;public int amount;public Unity.Entities.Entity craftingEntity;}}
public static class AISlot {public const int LifeTree=0;}
namespace UnityEngine.Networking {}
public static partial class AIBrain {
 private sealed class State {public string _prepNote="";public bool _blockToolShort;}
 private static State S=new();private static string _blockNote="";private static int _toolShortRed;
 public static AIGoal Goal=>AIGoals.Goal;
 private static bool ToolShortRule(string text,bool blocked,string context)=>false;
 public static List<AICandidate> TestPrep(Unity.Entities.World world,Unity.Entities.Entity player,out List<ObjectID> wanted){wanted=WantedItems(world,player,out var candidates);return candidates;}
}
public static class AIFarm {public static List<(ObjectID id,string why)> ToolTargets(Unity.Entities.World w,Unity.Entities.Entity p)=>new();}
public static class AICook {public static List<(ObjectID id,string why)> ToolTargets(Unity.Entities.World w,Unity.Entities.Entity p)=>new();}
public static class AITunnel {public static Dictionary<ObjectID,int> ToolBonus=new();}
public static class AIPlaceSpot {public static void AddUnique(List<AICandidate> target,List<AICandidate> source)=>target.AddRange(source);public static List<AICandidate> Candidates(Unity.Entities.World w,Unity.Entities.Entity p,ObjectID item,string title,string reason,bool progress,out string why,bool flag=false){why="boundary fixture";return new();}}
