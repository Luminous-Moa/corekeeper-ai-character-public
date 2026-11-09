using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
namespace Pug.UnityExtensions { }
namespace PugTilemap { public enum TileType { wall, bigRoot } public struct TileCD { public int tileset; public TileType tileType; } }
namespace Unity.Collections { public enum Allocator { Temp } }
namespace Unity.Transforms { public struct LocalTransform { public float3 Position; } }
namespace Unity.Physics { }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace Unity.Entities
{
    public class World { public EntityManager EntityManager = new EntityManager(); }
    public struct Disabled { }
    public struct ComponentType { public Type Type; public static ComponentType ReadOnly<T>() => new ComponentType { Type=typeof(T) }; }
    public class EntityManager
    {
        public readonly Dictionary<(Entity,Type),object> Data=new Dictionary<(Entity,Type),object>();
        public void Set<T>(Entity e,T value) => Data[(e,typeof(T))]=value;
        public void Buffer<T>(Entity e,params T[] values) => Data[(e,typeof(T))]=values;
        public bool Exists(Entity e)=>Data.Keys.Any(k=>k.Item1==e);
        public void Remove(Entity e) { foreach(var key in Data.Keys.Where(k=>k.Item1==e).ToArray())Data.Remove(key); }
        public bool HasComponent<T>(Entity e)=>Data.ContainsKey((e,typeof(T)));
        public bool HasBuffer<T>(Entity e)=>Data.ContainsKey((e,typeof(T)));
        public bool IsComponentEnabled<T>(Entity e)=>HasComponent<T>(e);
        public T GetComponentData<T>(Entity e)=>(T)Data[(e,typeof(T))];
        public T[] GetBuffer<T>(Entity e,bool ro)=>(T[])Data[(e,typeof(T))];
        public Query CreateEntityQuery(params ComponentType[] types) => new Query(Data.Keys.Select(k=>k.Item1).Distinct().Where(e=>types.All(t=>Data.ContainsKey((e,t.Type)))));
    }
    public class Query : IDisposable
    {
        readonly IEnumerable<Entity> values;public Query(IEnumerable<Entity> entities){values=entities;}
        public TempEntities ToEntityArray(Unity.Collections.Allocator a)=>new TempEntities(values.ToArray());
        public void Dispose() { }
    }
    public class TempEntities : List<Entity>,IDisposable { public TempEntities(IEnumerable<Entity> values):base(values){} public void Dispose(){} }
    public struct Entity : IEquatable<Entity>
    {
        public int Index, Version;
        public static Entity Null => default;
        public bool Equals(Entity other) => Index == other.Index && Version == other.Version;
        public override bool Equals(object other) => other is Entity e && Equals(e);
        public override int GetHashCode() => Index ^ Version;
        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
    }
}
namespace Unity.Mathematics
{
    public struct int2 : IEquatable<int2>
    {
        public int x,y; public int2(int a,int b) {x=a;y=b;}
        public bool Equals(int2 other)=>x==other.x&&y==other.y;
        public override bool Equals(object other)=>other is int2 p&&Equals(p);
        public override int GetHashCode()=>x*37+y;
        public static bool operator ==(int2 a,int2 b)=>a.Equals(b);
        public static bool operator !=(int2 a,int2 b)=>!a.Equals(b);
        public static implicit operator float2(int2 p)=>new float2{x=p.x,y=p.y};
        public static int2 operator +(int2 a,int2 b)=>new int2(a.x+b.x,a.y+b.y);
        public static int2 operator -(int2 a,int2 b)=>new int2(a.x-b.x,a.y-b.y);
        public override string ToString()=>x+","+y;
    }
    public struct float2 { public float x,y; public float2(float a,float b){x=a;y=b;}public static float2 zero=>default; }
    public struct float3 { public float x,y,z; public float2 xz=>new float2{x=x,y=z};public static float3 zero=>default; }
    public static class math { public static bool all(bool b)=>b; public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));public static float2 normalize(float2 p){float d=distance(p,default);return d==0?default:new float2(p.x/d,p.y/d);}public static int max(int a,int b)=>Math.Max(a,b);public static float max(float a,float b)=>Math.Max(a,b); }
}
public enum ObjectID { None, CopperOre, Wood, Mushroom, WallDirtBlock }
public enum SkillEnd { None, Down }
public static partial class AIBrain
{
    public const float ExecutionReviewSeconds = 5f;
    public static void ObserveExecution(string reason) { }
    public static void ObserveExecution(string reason, AISkill source) { }
    public static bool TryOreObject(World w,int ts,out ObjectID item){item=ObjectID.CopperOre;return true;}
}
public struct ObjectDataCD { public ObjectID objectID;public int variation; }
public struct HealthCD { public int health; }
public struct EntityDestroyedCD { }
public enum PickUpItemState { OnGround, HasBeenPickedUp }
public struct PickUpItemCD { public PickUpItemState state; }
public struct ContainedObjectsBuffer { public ObjectID objectID; public int variation, amount; }
public class AIObservation
{
    public float3 pos;
    public List<(int2 p, int tileset, bool ore, int oreTileset)> wallTiles = new List<(int2,int,bool,int)>();
    public List<(int2 p, int tileset)> rootTiles = new List<(int2,int)>();
    public List<(ObjectID id, float3 pos, float dist)> objects = new List<(ObjectID,float3,float)>();
    public List<(ObjectID id, int amount, float3 pos, float dist)> items = new List<(ObjectID,int,float3,float)>();
    public static AIObservation Take(World w, Entity p, bool tiles)
    {
        var result = new AIObservation();
        foreach (var entry in w.EntityManager.Data)
            if (entry.Key.Item2 == typeof(ContainedObjectsBuffer))
            {
                var array=(ContainedObjectsBuffer[])entry.Value;
                if(array.Length>0) result.items.Add((array[0].objectID,array[0].amount,w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(entry.Key.Item1).Position,0));
            }
        return result;
    }
}
public enum GoalKind { None, Gather, Home }
public class AIGoal { public GoalKind kind; public ObjectID item;public int count, startCount, maxCount=-1; public string Text => kind + " " + item + " " + count; }
public static class AIGoals { public static AIGoal Goal = new AIGoal(); public static int GoalRevision; }
public static class AIExpedition
{
    public static IReadOnlyList<ExpeditionLimit> Limits;
    public static long Gathered(AIGoal g,int net)=>Math.Max(0,net);
    public static IReadOnlyList<ExpeditionLimit> ActiveLimits()
    {
        if(Limits!=null)return Limits;
        var g=AIGoals.Goal;AIBody.Bag.TryGetValue(g.item,out int current);
        return g.kind==GoalKind.Gather&&g.maxCount>=0 ? new[]{new ExpeditionLimit{OrderId=17,Item=g.item.ToString(),Maximum=g.maxCount,Acquired=Math.Max(0,current-g.startCount)}} : Array.Empty<ExpeditionLimit>();
    }
}
public class AICandidate
{
    public string[] resources;
    public string kind,key,desc,targetLabel,actionMode;public int2 tile;public Entity entity;
    public ObjectID itemId; public bool progress,digAllowEdge;public AIGatherSelection gather;
    public List<int2> tiles;
}
public static class AIVision
{
    public static HashSet<int2> Hidden=new HashSet<int2>();
    public static bool CanSee(float3 pos,int2 tile)=>!Hidden.Contains(tile);
    public static bool CanSee(float3 pos,float2 tile)=>!Hidden.Contains(new int2((int)tile.x,(int)tile.y));
}
public static partial class AIBody
{
    public static Dictionary<ObjectID,int> Bag=new Dictionary<ObjectID,int>();public static bool Room=true;
    public static float3 Pos(World w,Entity p)=>default;
    public static int2 TileOf(float3 pos)=>new int2((int)pos.x,(int)pos.z);
    public static bool BagHasRoomFor(World w,Entity p,ObjectID id)=>Room;
    public static Dictionary<ObjectID,int> InventoryCounts(World w,Entity p)=>new Dictionary<ObjectID,int>(Bag);
}
public partial class AITileReader
{
    public struct Wall { public int tileset,oreTileset; public bool ore; }
    public static Dictionary<int2,Wall> Walls=new Dictionary<int2,Wall>();
    public static Dictionary<int2,int> Roots=new Dictionary<int2,int>();
    public AITileReader(World world) { }
    public bool HasWall(int2 p,out PugTilemap.TileCD tile,out bool ore,out int oreTileset)
    { bool exists=Walls.TryGetValue(p,out var w);tile=new PugTilemap.TileCD{tileset=w.tileset,tileType=PugTilemap.TileType.wall};ore=w.ore;oreTileset=w.oreTileset;return exists; }
    public List<PugTilemap.TileCD> Layers(int2 p)=>Roots.TryGetValue(p,out int ts)?new List<PugTilemap.TileCD>{new PugTilemap.TileCD{tileset=ts,tileType=PugTilemap.TileType.bigRoot}}:new List<PugTilemap.TileCD>();
}
public static partial class AIWallData
{
    public static bool TryGet(World w,int ts,out int hp,out int red,out ObjectID item){hp=10;red=0;item=ObjectID.WallDirtBlock;return true;}
}
public class PhysicalBoundarySkill : AISkill
{
    private readonly Action<World> finish;
    public PhysicalBoundarySkill(Action<World> a){finish=a;Name="world boundary";}
    public override bool Start(World w,Entity p,float n)=>true;
    public override BodyInput Tick(World w,Entity p,float n){finish(w);return Finish(SkillStatus.Done,"world observation");}
}
#if !NATIVE_BOUNDARY
public class MineSkill : PhysicalBoundarySkill
{
    public MineSkill(int2 tile,bool ore):base(w=>{AITileReader.Walls.Remove(tile);AIBody.Bag.TryGetValue(ObjectID.CopperOre,out int n);AIBody.Bag[ObjectID.CopperOre]=n+2;}){}
}
public class RootSkill : PhysicalBoundarySkill { public RootSkill(int2 tile):base(w=>AITileReader.Roots.Remove(tile)){} }
public class BreakSkill : PhysicalBoundarySkill { public BreakSkill(Entity e):base(w=>w.EntityManager.Remove(e)){} }
public class GotoSkill : PhysicalBoundarySkill { public GotoSkill(int2 tile,string what):base(w=>{}){} }
#endif
public class TunnelPlan
{
    public List<int2> path=new List<int2>(); public List<(int2 p,ObjectID obj,int hits)> walls=new List<(int2,ObjectID,int)>();
    public int totalHits,damage,toolShortSeen;public string damageNote="";
    public string WallSummary()=>"observed route";public string RiskText(World w)=>"";
}
public static class AITunnel
{
    public static TunnelPlan Next=new TunnelPlan();
    public static TunnelPlan Plan(World w,Entity p,int2 t,out string why,int n,bool edge){why="controlled route boundary";return Next;}
}
public static class AIFarm
{
 public struct Crop {public Entity e;public ObjectID id,harvest;public int n;public bool ripe;}
 public static Dictionary<int2,Crop> Crops=new Dictionary<int2,Crop>();
 public static Dictionary<int2,Crop> CropsInView(World w,float3 p)=>Crops;
}
