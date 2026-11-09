using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
namespace Inventory { }
namespace Pug.Properties { }
public enum Tileset { Dirt=1 }
public enum NavStatus { Moving, Arrived, Failed }
public class AINavigator { public string FailReason="";public void Start(World w,Entity p,int2 t,float d,float now,Entity target=default){} public NavStatus Tick(World w,Entity p,float n,out float2 move){move=default;return NavStatus.Arrived;}public void Dispose(World w){} }
public class AIHandLease { public AIHandLease(World w,Entity p,int slot,bool pick){}public string Check(World w,Entity p,out bool ready){ready=true;return null;} }
public struct EquippedObjectCD { public ContainedObjectsBuffer containedObject; }
public static class AIEdge
{
 public static bool PlanActive;
 public static HashSet<int2> ShutDoorsNear(World w,int2 p,int n)=>new HashSet<int2>();
 public static bool StandOk(AITileReader tr,int2 p,HashSet<int2> blocked,HashSet<int2> shut)=>!AITileReader.Walls.ContainsKey(p);
 public static int2 StandTile(World w,float3 p)=>AIBody.TileOf(p);
 public static string WallFact(int2 p)=>"";
}
public static class AIObjects { public static HashSet<int2> BlockedInView(World w,Entity p,out int ignored){ignored=0;return new HashSet<int2>();} }
public static partial class AIBody
{
 public static int HeldSlot=0;public static float WalkSpeed=3;
 public static int HandSlot(World w,Entity p,bool pick,out string tool,out string swap){tool="pick";swap="";return 0;}
 public static bool CanHold(World w,Entity p,bool pick,out int slot,out string reason){slot=0;reason="";return true;}
 public static int WeaponSlotForAttack(World w,Entity p,out string tool,out string swap)=>HandSlot(w,p,false,out tool,out swap);
 public static int ChooseSlot(World w,Entity p,bool pick,out string tool){tool="pick";return 0;}
 public static float2 DirTo(float3 p,float2 t)=>math.normalize(new float2(t.x-p.x,t.y-p.z));
 public static string InventoryDiff(Dictionary<ObjectID,int> a,Dictionary<ObjectID,int> b)=>"변화 없음";
}
public partial class AITileReader {public PugTilemap.TileCD Top(int2 p)=>default;}
public static partial class AIWallData
{
 public static int EstimateMiningDamage(World w,Entity p,out int eff){eff=0;return 20;}
 public static int TileEntityHealth(World w,int2 p)=>10;
 public static string DamageProbe(World w,Entity p,int2 t)=>"controlled game boundary";
 public static string RiskText(World w,ObjectID id)=>"";
}
public static class AIPlayerSpikeMod {public static void Log(string s){}public static void LogError(string s,Exception e){} }
public enum AISlot { BreakSkill }
public class AIAgent {public static AIAgent Cur=new AIAgent();public T Get<T>(AISlot slot) where T:new()=>new T();}
public static class AIDrops {public const int RootSource=1;public static void Note(int source,Dictionary<ObjectID,int> a,Dictionary<ObjectID,int> b){} }
public static class AIDoorPass {public static (float x,float y) SkillMove(float x,float y,Func<bool> can,float fx,float fy)=>(x,y);}
public static class AIBaseGuard {public static List<string> EdgeWallsOn(List<(int x,int y)> walls,Func<(int x,int y),string> fact,HashSet<(int x,int y)> known)=>new List<string>();public static string DigEdgeStopText(List<string> facts)=>"edge";}
public static class AIPlaceGoal {public static void NoteStop(string s){} }
public static partial class AIBrain {public static void ForgetPlan(int2 p){} }
public static class AITestSwitches {public static bool Test;}
public static class AIOutcomes {public static string Mismatch(string a,bool b,bool c,string d)=>null;public static bool DownText(string s)=>false;}
public class GotoSkill : AISkill
{
 public int HoldSlot;public static int Starts;public GotoSkill(int2 t,string what){Name=what;}
 public override bool Start(World w,Entity p,float now){Starts++;return true;}
 public override BodyInput Tick(World w,Entity p,float now)=>Finish(SkillStatus.Done,"controlled walk arrival");
}
