using Unity.Entities;using Unity.Mathematics;
namespace Pug.UnityExtensions{} namespace PugTilemap{} namespace Unity.Collections{} namespace Unity.Transforms{public struct LocalTransform{public float3 Position;}}
namespace PlayerEquipment{public struct EquipmentSlotCD{public bool atMaxWindup;public float currentWindup;public int currentWindupTier;}}
public struct HealthCD{public int health,maxHealth;}public struct Disabled{}public struct EntityDestroyedCD{}
public enum SkillEnd{None,Killed}
public static class AITargetEnd{public static SkillEnd How(EntityManager em,Entity e,int hp,out string why){why="fixture";return SkillEnd.None;}}
public sealed class AIHandLease{public AIHandLease(World w,Entity p,int s,bool m){}public string Check(World w,Entity p,out bool ready){ready=true;return null;}}
public static class AIVitals{public static ObjectID HitKey(ObjectID id,bool broken)=>id;public static void RecordHit(ObjectID id,int amount,float time){}public static bool HitStats(ObjectID id,out float per,out float sec){per=sec=0;return false;}}
public enum NavStatus{Moving,Arrived,Failed}
public class AINavigator{public bool HoldsDoor;public int StuckCount,Replans;public string Mode="fixture",FailReason="fixture";public SkillEnd FailCode;public void Start(World w,Entity p,int2 t,float range,float now,Entity target){}public void Retarget(World w,int2 t,float now){}public void Dispose(){}public void Dispose(World w){}public NavStatus Tick(World w,Entity p,float now,out float2 move){move=default;return NavStatus.Moving;}}
public static class AIEdge{public struct Reach{public bool touch;}public static int2 EnemyTile(World w,float3 p)=>new int2((int)p.x,(int)p.z);public static Reach ReachOf(World w,Entity p,int2 t,ObjectID id,Entity e)=>new Reach{touch=true};public static bool DirectStepOk(World w,float3 p,float2 f)=>true;}
public static class AIDoorPass{public static bool SwingNow(float a,float b,bool touch,bool held,bool noWait)=>true;public static (float x,float y) SkillMove(float x,float y,System.Func<bool> allowed,float a,float b)=>(x,y);}
