// ECS, prefab, observation and physical input adapters only. AIFarm and FarmSkill are linked production source.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Pug.UnityExtensions { }
namespace Pug.Properties { }
namespace PlayerEquipment { }
namespace Unity.Physics { }
namespace Unity.Collections { public enum Allocator { Temp } }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace Unity.Transforms { public struct LocalTransform { public float3 Position; } }
namespace Unity.Mathematics
{
    public struct int2 : IEquatable<int2>
    {
        public int x,y; public int2(int a,int b){x=a;y=b;}
        public static int2 operator +(int2 a,int2 b)=>new int2(a.x+b.x,a.y+b.y);
        public static bool operator ==(int2 a,int2 b)=>a.x==b.x&&a.y==b.y;
        public static bool operator !=(int2 a,int2 b)=>!(a==b);
        public bool Equals(int2 b)=>this==b; public override bool Equals(object o)=>o is int2 b&&this==b; public override int GetHashCode()=>HashCode.Combine(x,y);
    }
    public struct float2
    {
        public float x,y;public float2(float a,float b){x=a;y=b;}public static float2 zero=>default;
        public static implicit operator float2(int2 a)=>new float2(a.x,a.y);
        public static float2 operator +(float2 a,float2 b)=>new float2(a.x+b.x,a.y+b.y);
        public static float2 operator -(float2 a,float2 b)=>new float2(a.x-b.x,a.y-b.y);
        public static float2 operator /(float2 a,float b)=>new float2(a.x/b,a.y/b);
    }
    public struct float3 {public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}public float2 xz=>new float2(x,z);public static float3 zero=>default;public static float3 operator +(float3 a,float3 b)=>new float3(a.x+b.x,a.y+b.y,a.z+b.z);}
    public static class math
    {
        public static int min(int a,int b)=>Math.Min(a,b);public static float min(float a,float b)=>Math.Min(a,b);
        public static int max(int a,int b)=>Math.Max(a,b);public static float max(float a,float b)=>Math.Max(a,b);
        public static int abs(int x)=>Math.Abs(x);public static float ceil(float x)=>(float)Math.Ceiling(x); public static float abs(float x)=>Math.Abs(x);public static float2 abs(float2 x)=>new float2(abs(x.x),abs(x.y));
        public static float cmax(float2 x)=>max(x.x,x.y);public static float floor(float x)=>(float)Math.Floor(x);public static float round(float x)=>(float)Math.Round(x);
        public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
        public static float2 normalize(float2 v){float l=(float)Math.Sqrt(v.x*v.x+v.y*v.y);return new float2(v.x/l,v.y/l);}
    }
}
namespace Unity.Entities
{
    public struct Entity
    {
        public int Index,Version;public static Entity Null=>default;
        public static bool operator ==(Entity a,Entity b)=>a.Index==b.Index&&a.Version==b.Version;public static bool operator !=(Entity a,Entity b)=>!(a==b);
        public override bool Equals(object o)=>o is Entity b&&this==b;public override int GetHashCode()=>HashCode.Combine(Index,Version);
    }
    public class World {public bool IsCreated=true;public EntityManager EntityManager=new EntityManager();}
    public class DynamicBuffer<T>:IEnumerable<T>
    {
        readonly List<T> values=new List<T>();public int Length=>values.Count;public T this[int i]{get=>values[i];set=>values[i]=value;}public void Add(T item)=>values.Add(item);
        public IEnumerator<T> GetEnumerator()=>values.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public sealed class EntityManager
    {
        readonly Dictionary<(Entity,Type),object> data=new Dictionary<(Entity,Type),object>();readonly HashSet<Entity> deleted=new HashSet<Entity>();int next=100;
        public Entity New()=>new Entity{Index=next++,Version=1};public bool Exists(Entity e)=>e!=Entity.Null&&!deleted.Contains(e);
        public void Remove(Entity e)=>deleted.Add(e);public void Put<T>(Entity e,T x)=>data[(e,typeof(T))]=x;
        public void PutBuffer<T>(Entity e,DynamicBuffer<T> x)=>data[(e,typeof(DynamicBuffer<T>))]=x;
        public bool HasComponent<T>(Entity e)=>data.ContainsKey((e,typeof(T)));public bool IsComponentEnabled<T>(Entity e)=>HasComponent<T>(e);
        public bool HasBuffer<T>(Entity e)=>data.ContainsKey((e,typeof(DynamicBuffer<T>)));
        public T GetComponentData<T>(Entity e)=>data.TryGetValue((e,typeof(T)),out var x)?(T)x:default;
        public DynamicBuffer<T> GetBuffer<T>(Entity e,bool readOnly=false)=>(DynamicBuffer<T>)data[(e,typeof(DynamicBuffer<T>))];
        public EntityQuery CreateEntityQuery(params Type[] types)=>new EntityQuery(this,types);
        public Entity[] Find(Type[] types)=>data.Keys.Select(k=>k.Item1).Distinct().Where(e=>Exists(e)&&types.All(t=>data.ContainsKey((e,t)))).ToArray();
    }
    public static class ComponentType {public static Type ReadOnly<T>()=>typeof(T);}
    public sealed class TempEntities:IEnumerable<Entity>,IDisposable
    {
        readonly Entity[] values;public TempEntities(Entity[] x){values=x;}public IEnumerator<Entity> GetEnumerator()=>((IEnumerable<Entity>)values).GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();public void Dispose(){}
    }
    public sealed class EntityQuery:IDisposable
    {
        readonly EntityManager em;readonly Type[] types;public EntityQuery(EntityManager manager,Type[] t){em=manager;types=t;}
        public int CalculateEntityCount()=>em.Find(types).Length;public T GetSingleton<T>()=>em.GetComponentData<T>(em.Find(types)[0]);
        public TempEntities ToEntityArray(Unity.Collections.Allocator a)=>new TempEntities(em.Find(types));public void Dispose(){}
    }
}
namespace PugTilemap {public enum TileType {ground,dugUpGround,wateredGround,water} public enum Tileset {Dirt} public struct TileLayer {public TileType tileType;public int tileset;}}
public enum ObjectID {None,SeedA,SeedB,CropA,CropB,FoodA,FoodB,WoodHoe,WaterCan,Wood,WallDirtBlock,CopperOre,TinOre,Chest,Pot,Station,StoneSeeder}
public enum ObjectType {PlaceablePrefab,Hoe,WaterCan,MiningPick,MeleeWeapon,Other,Seeder}
public sealed class ObjectInfo {public ObjectType objectType;public bool isStackable;}
public struct ObjectDataCD {public ObjectID objectID;public int amount,variation,variationUpdateCount;}
public struct ContainedObjectsBuffer
{
    public ObjectDataCD objectData;public int auxDataIndex;public ObjectID objectID=>objectData.objectID;public int variation=>objectData.variation;public int amount=>objectData.amount;public int variationUpdateCount=>objectData.variationUpdateCount;
}
public struct CraftingCD {public int outputSlotIndex;}
public struct HealthCD {public int health,maxHealth;}
public struct EntityDestroyedCD { }
public struct PlantCD {public ObjectID objectToDropWhenHarvested;public int numberOfPlantsToDrop;}
public struct GrowingCD {public int currentStage;public bool HasFinishedGrowing(ObjectPropertiesCD p)=>currentStage>=p.Stages-1;}
public struct HasFinishedGrowingCD { }
public struct GrowTimerRefCD {public Entity GrowTimerEntity;}
public struct GrowTimerCD {public float Value,StageTime;}
public struct ObjectPropertiesCD
{
    public int Stages;public float Time;public ObjectID Next;
    public T Get<T>(int key)=>typeof(T)==typeof(int)?(T)(object)Stages:(T)(object)Time;
    public bool TryGet<T>(int key,out T value){value=(T)(object)Next;return Next!=ObjectID.None;}
}
public struct FullnessCD {public int maxFullness;}
public sealed class BlobArray<T> {public T[] Items=Array.Empty<T>();public int Length=>Items.Length;public T this[int i]=>Items[i];}
public sealed class BlobReference<T> {T value;public BlobReference(T x){value=x;}public bool IsCreated=>true;public ref T Value=>ref value;}
public struct EquipmentSlotConstantCD {public BlobReference<BlobArray<int>> hoeableGroundTilesets;}
public static class PugDatabase
{
    public struct DatabaseBankCD {public object databaseBankBlob;}
    public static readonly Dictionary<(ObjectID,int),Entity> Prefabs=new Dictionary<(ObjectID,int),Entity>();
    public static ObjectInfo GetObjectInfo(ObjectID id,int variation=0)=>id==ObjectID.None?null:new ObjectInfo{objectType=id==ObjectID.SeedA||id==ObjectID.SeedB?ObjectType.PlaceablePrefab:id==ObjectID.WoodHoe?ObjectType.Hoe:id==ObjectID.WaterCan?ObjectType.WaterCan:id==ObjectID.StoneSeeder?ObjectType.Seeder:ObjectType.Other,isStackable=id!=ObjectID.WoodHoe&&id!=ObjectID.WaterCan&&id!=ObjectID.StoneSeeder};
    public static Entity GetPrimaryPrefabEntity(ObjectID id,object bank,int variation)=>Prefabs.TryGetValue((id,variation),out var entity)?entity:Entity.Null;
}
public static class PlayerController {public static bool ItemIsBroken(ObjectDataCD item)=>item.amount<=0;}
public enum EdgeCandidateRole {None}
public static class AISlot {public const int Farm=13,Goals=15,LifeTree=18;}
public sealed class AIAgent
{
    public string Id="a",Name="Farmer";public object[] Slots=new object[19];public static AIAgent Cur=new AIAgent();
    public T Get<T>(int index) where T:class,new(){if(Slots[index]==null)Slots[index]=new T();return (T)Slots[index];}
}
public sealed class AIObservation
{
    public float3 pos;public List<SeenEnemy> enemies=new List<SeenEnemy>();public static AIObservation Take(World w,Entity p,bool refresh)=>new AIObservation{pos=AIBody.Pos(w,p),enemies=FarmWorld.Enemies};
}
public sealed class SeenEnemy {public float dist;}
public static partial class AIPlayerSpikeMod {public static readonly List<string> Logs=new List<string>();public static void Log(string x)=>Logs.Add(x);public static void LogError(string x,Exception e)=>Logs.Add(x+e.Message);public static void Reply(string x,ChatKind kind=ChatKind.Status,string category=null)=>Logs.Add(x);}
public static class FarmWorld
{
    public static readonly Dictionary<int2,PugTilemap.TileLayer> Tiles=new Dictionary<int2,PugTilemap.TileLayer>();public static List<SeenEnemy> Enemies=new List<SeenEnemy>();public static readonly HashSet<int2> Hidden=new HashSet<int2>();
}
public sealed class AITileReader
{
    public AITileReader(World w){}public PugTilemap.TileLayer Top(int2 t)=>FarmWorld.Tiles.TryGetValue(t,out var layer)?layer:new PugTilemap.TileLayer{tileType=PugTilemap.TileType.ground};
    public IEnumerable<PugTilemap.TileLayer> Layers(int2 t){yield return Top(t);}public bool Walkable(int2 t)=>true;
}
public static class AIVision {public static bool CanSee(float3 pos,float2 tile)=>!FarmWorld.Hidden.Contains(new int2((int)tile.x,(int)tile.y));public static float2 HalfExtents()=>new float2(20,20);}
public static partial class AIBody
{
    public static float3 Pos(World w,Entity p)=>w.EntityManager.GetComponentData<LocalTransform>(p).Position;
    public static int2 TileOf(float3 p)=>new int2((int)Math.Round(p.x),(int)Math.Round(p.z));public static float2 DirTo(float3 p,float2 target)=>target-p.xz;
    public static Dictionary<ObjectID,int> InventoryCounts(World w,Entity p){var d=new Dictionary<ObjectID,int>();for(int i=AIInventory.MainStart(w,p);i<AIInventory.MainEnd(w,p);i++)if(AIInventory.Read(w,p,i,out var x)&&x.objectID!=ObjectID.None){d.TryGetValue(x.objectID,out int n);d[x.objectID]=n+AIInventory.Quantity(x);}return d;}
    public static string InventoryDiff(Dictionary<ObjectID,int> a,Dictionary<ObjectID,int> b)=>string.Join(",",b.Select(x=>x.Key+":"+x.Value));
    // weapon / empty hand boundary of the attack path (AIBody.WeaponSlotForAttack); slot 9 is the fixture's hand slot
    public static bool CanHold(World w,Entity p,bool forMining,out string what,out string why){what="hand";why="";return true;}
    public static int WeaponSlotForAttack(World w,Entity p,out string what,out string swap){what="hand";swap="";return 9;}
}
public static class AIMemory
{
    public sealed class Data {public int[] farmXY,waterXY;public long[] farmSince,waterSeen;}
    public sealed class Station {public ObjectID id;public int2 tile;}
    public static void MarkDirty(){}public static void Save(bool all){}public static bool SeenTile(int2 tile)=>true;public static string Ago(long time)=>"last observation";
    public static List<Station> RememberedStations(float3 pos)=>new List<Station>();
}
public static class AIObjects {public static HashSet<int2> BlockedInView(World w,Entity p,out string why){why="";return new HashSet<int2>();}}
public static partial class AIEdge {public static HashSet<int2> ShutDoorsNear(World w,int2 tile,int range)=>new HashSet<int2>();public static bool StandOk(AITileReader tr,int2 tile,HashSet<int2> blocked,HashSet<int2> shut)=>!blocked.Contains(tile)&&!shut.Contains(tile);}
public static class AIFood {public static int HungerAdd(World w,ObjectID id,int variation)=>5;}
public static class AIDrops {public static bool Known(int id)=>false;public static string Text(int id)=>"unknown";}
public static partial class AIBase
{
    public const float BaseRadius=10;public static int2 BaseTile=>default;public static string BaseText=>"(0,0)";public static float BaseDist(float3 p)=>math.distance(p.xz,default);
    public sealed class Chest {public long openedUnix;public Dictionary<ObjectID,int> items=new Dictionary<ObjectID,int>();}
    public static List<Chest> OwnChests()=>new List<Chest>();
    public static List<AICandidate> ItemTakeCandidates(World w,Entity p,AIObservation o,string key,Func<ObjectID,bool> filter,int count,string why,bool progress)=>new List<AICandidate>();
}
public sealed class FarmRoute {public List<int2> path=new List<int2>();public List<int2> walls=new List<int2>();public float EstimatedSeconds=>1;public string WallSummary()=>"none";}
public static class AIApproach
{
    public static FarmRoute FirstImmediate(World w,Entity p,List<int2> tiles,out int2 stand,out string why){stand=tiles.Count>0?tiles[0]:default;why="";return tiles.Count>0?new FarmRoute():null;}
    public static AICandidate WalkCandidate(int2 tile,string label,bool progress,string description)=>new AICandidate{kind="dig",key=$"dig:{tile.x},{tile.y}",tile=tile,desc=description,progress=progress};
}
public enum NavStatus {Failed,Moving,Arrived}
public sealed class AINavigator
{
    int2 target;public string FailReason="blocked";public void Start(World w,Entity p,int2 tile,float reach,float now,Entity targetEntity=default){target=tile;}
    public NavStatus Tick(World w,Entity p,float now,out float2 move){w.EntityManager.Put(p,new LocalTransform{Position=new float3(target.x,0,target.y)});move=default;return NavStatus.Arrived;}
    public void Dispose(World w){}
}
public sealed class AIInventoryMove { }
public static partial class AIInventory
{
    public static int MovedForGoal(AIGoal goal)=>0;
    public static List<int> CarriedSlots(World w,Entity p){var l=new List<int>();for(int i=MainStart(w,p);i<MainEnd(w,p);i++)l.Add(i);return l;}   // main only in this harness (pouches: life-inventory-tests)
    public static int MainStart(World w,Entity p)=>0;public static int MainEnd(World w,Entity p)=>w.EntityManager.HasBuffer<ContainedObjectsBuffer>(p)?Math.Min(40,w.EntityManager.GetBuffer<ContainedObjectsBuffer>(p).Length):0;
    public static bool Read(World w,Entity p,int slot,out ContainedObjectsBuffer x){x=default;if(!w.EntityManager.HasBuffer<ContainedObjectsBuffer>(p))return false;var b=w.EntityManager.GetBuffer<ContainedObjectsBuffer>(p);if(slot<0||slot>=b.Length)return false;x=b[slot];return true;}
    public static int Quantity(ContainedObjectsBuffer x)=>x.objectID==ObjectID.None?0:PugDatabase.GetObjectInfo(x.objectID,x.variation).isStackable?x.amount:1;
    public static bool Identity(ContainedObjectsBuffer a,ContainedObjectsBuffer b)=>a.objectID==b.objectID&&a.variation==b.variation&&a.auxDataIndex==b.auxDataIndex;
    public static InventoryStamp Stamp(ContainedObjectsBuffer x)=>new InventoryStamp((int)x.objectID,x.variation,x.auxDataIndex,x.amount,x.variationUpdateCount,PugDatabase.GetObjectInfo(x.objectID,x.variation)?.isStackable==true);
    public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new List<AILife.Entry>();
}
public sealed class AICraftSelection { }
public sealed class MaintenanceRequest { }
// Actual maintenance runtime is exercised by Tools/maintenance-tests; this harness keeps its farm boundary.
public static class AIMaintenance
{
    public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new List<AILife.Entry>();
}
public static partial class AICraft {public static int HotbarSlotFor(World w,Entity p,ObjectID item,out string swap){swap="";return 0;}public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new List<AILife.Entry>();}
public static partial class AICook {public sealed class Selection{}public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new List<AILife.Entry>();}

