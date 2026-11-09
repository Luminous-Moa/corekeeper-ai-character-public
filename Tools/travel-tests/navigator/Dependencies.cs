using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
namespace Unity.Physics { public struct PhysicsCollider { public bool IsValid; public Blob Value; } public struct Blob { public Collider Value; } public struct Collider { public Filter GetCollisionFilter()=>default; } public struct Filter { public uint BelongsTo; } }
public struct PathFindCD { public Entity startEntity,targetEntity; public uint belongsToLayer; public int2 targetPosition,searchRadius,lastCalculatedPosition; public bool blockedByCreatures,isFlying; public float pathValidTime,timer; }
public struct PathFindBfsCD {}
public struct PathFindNodeBuffer { public const int MAXIMUM_NODE_COUNT=5;public int2 position; }
public static class AIEdge {
 public static bool IsWalkDoorTile(int2 p)=>false; public static int2 StandTile(World w,float3 p)=>AIBody.TileOf(p);
 public static HashSet<int2> ShutDoorsNear(World w,int2 p,int r)=>new HashSet<int2>();
 public static bool PlanActive=>false; public static bool IsInside(int2 p)=>false;
 public static List<int2> OwnDoorTiles=new List<int2>(),MyDoorTiles=new List<int2>();
 public class Door { public string id,kind;public int variation; } public static Door DoorAt(World w,int2 t,float3 p)=>null;
}
public static class AIBaseGuard { public static (int x,int y) PlanFrom(float x,float y,Func<(int x,int y),bool> terrain,Func<(int x,int y),bool> allowed,(int x,int y) target)=>((int)Math.Round(x),(int)Math.Round(y)); }
public static class AIPlaceGrid { public const int SpawnPadHalf=2; public static bool SpawnPad(int x,int y)=>false; }
public static class AIPlaces { public static bool EdgeDoorsJoin(bool a,bool b,bool c)=>false; public static bool NearDoor((float x,float y) a,(float x,float y) b,List<(int x,int y)> c,List<(int x,int y)> d,out (int x,int y) e){e=default;return false;} }
public static class AIMoveTrace { public static bool On; public static string Nav; public static string T(int2 p)=>p.ToString(); }
public enum DoorPhase { None,Done,Failed }
public class DoorPassState { public DoorPhase phase; public float startAt;public string trace,facts,fail; }
public static class AIDoorPass {
 public static bool HoldsDoor(DoorPassState s)=>false;public static bool IsOpen(int v)=>true;public static void PressDropped(DoorPassState s,string r,float n){}
 public struct Output { public string observation;public bool press,nudge;public float fx,fy,mx,my; }
 public static Output Step(DoorPassState s,string k,int x,int y,float dx,float dy,float px,float py,bool o,bool d,float n)=>default;
 public enum NoPathStep { OwnDoor,ShortHop,PadHop,FailAfterDoor,None }
 public static NoPathStep NoPathNext(bool dn,bool dp,bool pd,bool st,bool sn,bool kp,bool ph,bool pe,bool da)=>!st&&sn?NoPathStep.ShortHop:NoPathStep.None;
}
