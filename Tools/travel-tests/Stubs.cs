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
    public static float Half=1f,HalfY=-1f;   // HalfY < 0: square view (Half both ways)
    public static Unity.Mathematics.float2 HalfExtents()=>new Unity.Mathematics.float2(Half,HalfY<0?Half:HalfY);
    public static bool CanSee(Unity.Mathematics.float3 a,Unity.Mathematics.float2 p)=>Math.Abs(a.x-p.x)<=Half&&Math.Abs(a.z-p.y)<=(HalfY<0?Half:HalfY);
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
public static class AITestControl { public static bool Held; }   // TEST ONLY /ai hold gate (the travel run stands while held)
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
public class AIGoal { public GoalKind kind; public string Text => kind.ToString(); }
public static class AIGoals { public static AIGoal Goal => AIBrain.Goal; public static int GoalRevision; }
public static class AIBrain
{
    public const float ExecutionReviewSeconds = 5f;
    public static AIGoal Goal=new AIGoal();
    public static List<string> Observations=new List<string>();
    public static void ObserveExecution(string s)=>Observations.Add(s);
    public static void ObserveExecution(string s,AISkill source)=>Observations.Add(s);
    public static int AreaQuestions; public static void MarkAreaQuestion()=>AreaQuestions++;
}
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
        var at=AIBody.TileOf(AIBody.Position);var step=Next(at,target);float tx=step.HasValue?step.Value.x:target.x,tz=step.HasValue?step.Value.y:target.y;
        float mx=tx-AIBody.Position.x,mz=tz-AIBody.Position.z;
        move=new Unity.Mathematics.float2(Math.Max(-0.5f,Math.Min(0.5f,mx)),Math.Max(-0.5f,Math.Min(0.5f,mz)));return NavStatus.Moving;
    }
    // walks along the fixture's open cells (4-neighbour shortest path); no path = straight toward the target
    private static Unity.Mathematics.int2? Next(Unity.Mathematics.int2 from,Unity.Mathematics.int2 to)
    {
        if(from.Equals(to)||!AITileReader.Open.Contains(to)||!AITileReader.Open.Contains(from))return null;
        var prev=new Dictionary<Unity.Mathematics.int2,Unity.Mathematics.int2>();var q=new Queue<Unity.Mathematics.int2>();q.Enqueue(from);prev[from]=from;
        while(q.Count>0){var c=q.Dequeue();if(c.Equals(to))break;foreach(var d in new[]{new Unity.Mathematics.int2(1,0),new Unity.Mathematics.int2(-1,0),new Unity.Mathematics.int2(0,1),new Unity.Mathematics.int2(0,-1)}){var n=new Unity.Mathematics.int2(c.x+d.x,c.y+d.y);if(AITileReader.Open.Contains(n)&&!AIObjects.Blocked.Contains(n)&&!prev.ContainsKey(n)){prev[n]=c;q.Enqueue(n);}}}
        if(!prev.ContainsKey(to))return null;var s=to;while(!prev[s].Equals(from))s=prev[s];
        float ox=Math.Abs(AIBody.Position.x-from.x),oz=Math.Abs(AIBody.Position.z-from.y);
        if(ox>0.05f&&s.y!=from.y||oz>0.05f&&s.x!=from.x)return from;   // centre on this cell before turning a corner
        return s;
    }
    public void Dispose(Unity.Entities.World w){}
}
