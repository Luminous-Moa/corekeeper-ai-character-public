using System;
using System.Collections.Generic;
using System.IO;
namespace Pug.UnityExtensions { } namespace PugTilemap { } namespace Unity.Collections { } namespace Unity.Physics { } namespace Unity.Transforms { }
namespace Unity.Entities {
 public class World { public bool IsCreated=true; public EntityManager EntityManager=new EntityManager(); }
 public struct Entity { public int Index; public static Entity Null=>default; public static bool operator ==(Entity a, Entity b)=>a.Index==b.Index; public static bool operator !=(Entity a, Entity b)=>a.Index!=b.Index; public override bool Equals(object o)=>o is Entity e && e.Index==Index; public override int GetHashCode()=>Index; }
 public class EntityManager { public bool Exists(Entity e)=>false; public bool HasComponent<T>(Entity e)=>false; public T GetComponentData<T>(Entity e)=>default; public void SetComponentData<T>(Entity e,T v){} public void AddComponentData<T>(Entity e,T v){} public void AddComponent<T>(Entity e){} public Entity CreateEntity()=>default; public void DestroyEntity(Entity e){} public DynamicBuffer<T> AddBuffer<T>(Entity e)=>new DynamicBuffer<T>(); public DynamicBuffer<T> GetBuffer<T>(Entity e,bool readOnly)=>new DynamicBuffer<T>(); }
 public class DynamicBuffer<T> { private readonly List<T> list=new List<T>(); public int Length=>list.Count; public T this[int i]=>list[i]; public void Add(T v)=>list.Add(v); }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions Options=new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
        public static T FromJson<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,Options);
        public static string ToJson<T>(T o)=>System.Text.Json.JsonSerializer.Serialize(o,Options);
    }
}
public enum SkillEnd { None, NoPath }
internal static class AISlot { public const int Travel=20; }
public sealed class AIAgent
{
    public static AIAgent Cur=new AIAgent("a"); public readonly string Id;
    public AIAgent(string id){Id=id;} private readonly object[] states=new object[21];
    internal T Get<T>(int slot) where T:class,new() =>(T)(states[slot]??(states[slot]=new T()));
}
public static class AIBody
{
    public static Unity.Mathematics.float3 Position; public static AISkill Current; public const float WalkSpeed=5f;
    public static int UseDone, NextUseId; public static string UseDroppedWhy(int n)=>"";
    public static Unity.Mathematics.float2 DirTo(Unity.Mathematics.float3 p,Unity.Mathematics.float2 target)=>Unity.Mathematics.math.normalizesafe(target-p.xz);
    public static string HandNote(Unity.Entities.World w,Unity.Entities.Entity e)=>"";
    public static void NavDoorStep(Unity.Mathematics.float2 f,int use,Unity.Mathematics.float2 m,DoorPassState s,Unity.Mathematics.int2 t,Unity.Mathematics.float2 d,bool o,bool n){}

    public static Unity.Mathematics.float3 Pos(Unity.Entities.World w,Unity.Entities.Entity p)=>Position;
    public static Unity.Mathematics.int2 TileOf(Unity.Mathematics.float3 p)=>new Unity.Mathematics.int2((int)Math.Round(p.x),(int)Math.Round(p.z));
}
public static class AIVision
{
    public static float Half=1f;
    public static Unity.Mathematics.float2 HalfExtents()=>new Unity.Mathematics.float2(Half,Half);
    public static bool CanSee(Unity.Mathematics.float3 a,Unity.Mathematics.float2 p)=>Math.Abs(a.x-p.x)<=Half&&Math.Abs(a.z-p.y)<=Half;
}
public class AITileReader
{
    public static readonly HashSet<Unity.Mathematics.int2> Open=new HashSet<Unity.Mathematics.int2>();
    public AITileReader(Unity.Entities.World w){}
    public bool Walkable(Unity.Mathematics.int2 p)=>Open.Contains(p);
    public (string tileType,int ignored) Top(Unity.Mathematics.int2 p)=>(Walkable(p)?"ground":"wall",0);
}
public static class AIObjects
{
    public static readonly HashSet<Unity.Mathematics.int2> Blocked=new HashSet<Unity.Mathematics.int2>();
    public static HashSet<Unity.Mathematics.int2> BlockedInView(Unity.Entities.World w,Unity.Entities.Entity p,out string note){note="";var result=new HashSet<Unity.Mathematics.int2>();foreach(var t in Blocked)if(AIVision.CanSee(AIBody.Position,t))result.Add(t);return result;}
}

public static class AIMemory
{
    public static string Root=Path.Combine(Path.GetTempPath(),"travel-tests-"+Guid.NewGuid().ToString("N"));
    public static string CharDir(string world){string p=Path.Combine(Root,world,AIAgent.Cur.Id);Directory.CreateDirectory(p);return p;}
}
public static class AIPlayerSpikeMod
{
    public static string WorldId="world";
    public static void Log(string s){} public static void LogError(string s,Exception e){throw new Exception(s,e);}
}
public enum GoalKind { None, Explore, Gather, Home, Goto }
public class AIGoal { public GoalKind kind; }
public static class AIBrain { public static AIGoal Goal=new AIGoal(); public static List<string> Observations=new List<string>(); public static void ObserveExecution(string s)=>Observations.Add(s); }
public class AIObservation { public Unity.Mathematics.float3 pos; }
public class AICandidate
{
    public string kind,key,actionMode,desc,exploreDirection; public int run;public bool progress; public Unity.Mathematics.int2 tile;
}
