using System;
using System.Collections.Generic;
using System.IO;
namespace Pug.UnityExtensions { } namespace PugTilemap { } namespace Unity.Collections { } namespace Unity.Physics { } namespace Unity.Transforms { }
namespace Unity.Entities { public class World { } public struct Entity { } }
namespace Unity.Mathematics
{
    public struct int2 : IEquatable<int2>
    {
        public int x, y; public int2(int a, int b) { x=a; y=b; }
        public bool Equals(int2 p) => x==p.x && y==p.y; public override bool Equals(object o) => o is int2 p && Equals(p); public override int GetHashCode()=>unchecked(x*397^y);
        public static implicit operator float2(int2 p)=>new float2(p.x,p.y);
    }
    public struct float2 { public float x,y; public float2(float a,float b){x=a;y=b;} public static float2 zero=>new float2(); }
    public struct float3 { public float x,y,z; public float3(float a,float b,float c){x=a;y=b;z=c;} public float2 xz=>new float2(x,z); }
    public static class math { public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); public static float max(float a,float b)=>Math.Max(a,b); }
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
public static class AIEdge { public static bool IsWalkDoorTile(Unity.Mathematics.int2 p)=>false; }
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
public static class AIBrain { public static List<string> Observations=new List<string>(); public static void ObserveExecution(string s)=>Observations.Add(s); }
public class AIObservation { public Unity.Mathematics.float3 pos; }
public class AICandidate
{
    public string kind,key,actionMode,desc,exploreDirection; public int run;public bool progress; public Unity.Mathematics.int2 tile;
}
public enum NavStatus { Moving,Arrived,Failed }
// Physical movement boundary only. The runtime corridor/turn and observed-map code are linked above.
public class AINavigator
{
    private Unity.Mathematics.int2 target; private float tolerance;
    public string FailReason="",Mode="fixture-physical-input";public int StuckCount,Replans;public SkillEnd FailCode;
    public void Start(Unity.Entities.World w,Unity.Entities.Entity p,Unity.Mathematics.int2 t,float tol,float now){target=t;tolerance=tol;}
    public NavStatus Tick(Unity.Entities.World w,Unity.Entities.Entity p,float now,out Unity.Mathematics.float2 move)
    {
        float dx=target.x-AIBody.Position.x,dz=target.y-AIBody.Position.z;
        if(Math.Abs(dx)+Math.Abs(dz)<=tolerance){move=Unity.Mathematics.float2.zero;return NavStatus.Arrived;}
        move=new Unity.Mathematics.float2(Math.Sign(dx),Math.Sign(dz));return NavStatus.Moving;
    }
    public void Dispose(Unity.Entities.World w){}
}
