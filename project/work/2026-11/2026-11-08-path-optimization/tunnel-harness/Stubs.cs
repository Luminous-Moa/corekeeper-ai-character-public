
using System;
using System.Collections.Generic;
public enum ObjectID { None, Stone, Explosive }
public struct Entity { }
public struct int2 : IEquatable<int2> {
 public int x,y; public int2(int x,int y){this.x=x;this.y=y;}
 public bool Equals(int2 o)=>x==o.x&&y==o.y; public override bool Equals(object o)=>o is int2 q&&Equals(q);
 public override int GetHashCode()=>HashCode.Combine(x,y);
 public static bool operator ==(int2 a,int2 b)=>a.Equals(b); public static bool operator !=(int2 a,int2 b)=>!a.Equals(b);
}
public struct float3 : IEquatable<float3> {
 public float x,y,z; public float3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public bool Equals(float3 o)=>x==o.x&&y==o.y&&z==o.z; public override bool Equals(object o)=>o is float3 q&&Equals(q);
 public override int GetHashCode()=>HashCode.Combine(x,y,z);
 public static bool operator ==(float3 a,float3 b)=>a.Equals(b);public static bool operator !=(float3 a,float3 b)=>!a.Equals(b);
}
public static class math { public static bool all(bool b)=>b; public static float ceil(float v)=>(float)Math.Ceiling(v); public static int min(int a,int b)=>Math.Min(a,b); }
public class Cell {public bool Known=true,Wall,Walkable=true,Door,DoorThere,Edge; public int Tileset=1;}
public class World {
 public Dictionary<int2,Cell> Cells=new Dictionary<int2,Cell>(); public HashSet<int2> Blocked=new HashSet<int2>();
 public Dictionary<int,(int hp,int red,ObjectID obj)> Walls=new Dictionary<int,(int,int,ObjectID)>{{1,(20,0,ObjectID.Stone)},{2,(40,2,ObjectID.Explosive)}};
 public float3 Position; public int Damage=20,Effect; public bool EdgeActive;
 public Cell At(int2 t)=>Cells.TryGetValue(t,out var c)?c:null;
}
public struct TileCD { public int tileset; }
public class AITileReader {
 World w;public AITileReader(World w){this.w=w;}
 public bool HasWall(int2 t,out TileCD tile,out bool ore,out int oreSet){var c=w.At(t);tile=new TileCD{tileset=c?.Tileset??0};ore=false;oreSet=0;return c?.Wall??false;}
 public bool Walkable(int2 t)=>w.At(t)?.Walkable??false;
}
public static class AIBody {public static float3 Pos(World w,Entity e)=>w.Position;}
public static class AIVision {public static World Current; public static bool CanSee(float3 p,int2 t)=>Current.At(t)?.Known??false;}
public static class AIMemory {public static bool SeenTile(int2 t)=>false;}
public static class AIEdge {
 static World W=>AIVision.Current; public static bool PlanActive=>W.EdgeActive;
 public static int2 StandTile(World w,float3 p)=>new int2((int)p.x,(int)p.z);
 public static bool IsWalkDoorTile(int2 t)=>W.At(t)?.Door??false;
 public static object DoorAt(World w,int2 t,float3 p)=>(w.At(t)?.DoorThere??false)?new object():null;
 public static bool OpensEdge(int2 t)=>W.At(t)?.Edge??false;
}
public static class AIObjects {public static HashSet<int2> BlockedInView(World w,Entity e,out string note){note="";return new HashSet<int2>(w.Blocked);}}
public static class AIWallData {
 public static bool TryGet(World w,int tileset,out int hp,out int red,out ObjectID obj){bool ok=w.Walls.TryGetValue(tileset,out var x);hp=x.hp;red=x.red;obj=x.obj;return ok;}
 public static string RiskText(World w,ObjectID obj)=>obj==ObjectID.Explosive?"explosive":"";
}
public static class AIPlaceGoal {public static void LogChangedBy(string k,string v,string log){} }
public static class AIMoveEstimate {public static float Seconds(int t,int h)=>t+h;}
