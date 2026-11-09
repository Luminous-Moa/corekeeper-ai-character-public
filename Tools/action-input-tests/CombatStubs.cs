using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
namespace Unity.Entities { public struct Entity{public int Index;public static Entity Null=>default;public static bool operator ==(Entity a,Entity b)=>a.Index==b.Index;public static bool operator !=(Entity a,Entity b)=>!(a==b);public override bool Equals(object o)=>o is Entity e&&e==this;public override int GetHashCode()=>Index;}public class World{public EntityManager EntityManager=new EntityManager();}public class EntityManager{public readonly Dictionary<(int,Type),object> Data=new Dictionary<(int,Type),object>();public bool Exists(Entity e)=>e.Index>0;public bool HasComponent<T>(Entity e)=>Data.ContainsKey((e.Index,typeof(T)));public T GetComponentData<T>(Entity e)=>(T)Data[(e.Index,typeof(T))];public void Put<T>(Entity e,T v)=>Data[(e.Index,typeof(T))]=v;public bool IsComponentEnabled<T>(Entity e)=>false;} }
namespace Unity.Physics {public struct PhysicsVelocity{public float3 Linear;}}
namespace UnityEngine {public static class Time{public static float realtimeSinceStartup;}}
public enum ObjectID{None,Slime,Bow}
public enum ObjectType{MeleeWeapon,RangeWeapon}
public class ObjectInfo{public ObjectType objectType;}
public static class PugDatabase{public static ObjectInfo GetObjectInfo(ObjectID id)=>id==ObjectID.None?null:new ObjectInfo{objectType=ObjectType.RangeWeapon};}
public struct Item{}
public struct SecondaryUseCD{public bool hasSecondaryUse,hasWindup,summonsMinion;public int windupTiers,cancelAttackIfNotAtWindupTier;}
public static class AIInventory{public static bool UseSelected;public static bool TryChosenHand(World w,Entity p,bool mining,out int slot,out string what,out string why){slot=UseSelected?3:-1;what=why="";return UseSelected;}public static bool Read(World w,Entity p,int slot,out Item item){item=default;return true;}public static Entity Prefab(World w,Item i)=>new Entity{Index=99};}
public class SeenEnemy{public Entity entity;public ObjectID id;public float3 pos;public float dist;public int health,maxHealth;}
public class AIObservation{public float3 pos;public float time;public List<SeenEnemy> enemies=new List<SeenEnemy>();public static AIObservation Current;public static AIObservation Take(World w,Entity p,bool tiles)=>Current;}
public class AICandidate{public string kind,key,desc,targetLabel,actionMode;public Entity entity;public float2 dest;public float obsAt;}
public class AITileReader{public AITileReader(World w){}public bool Walkable(int2 t)=>!AIObjects.Blocked.Contains(t);}
public static class AIObjects{public static HashSet<int2> Blocked=new HashSet<int2>();public static HashSet<int2> BlockedInView(World w,Entity p,out int dummy){dummy=0;return Blocked;}}
public static class AIVision{public static bool Visible=true;public static bool CanSee(float3 me,float2 t)=>Visible;public static bool CanSee(float3 me,int2 t)=>true;}
public static class AIBody{public const float WalkSpeed=5;public static float3 Position;public static bool Fight=true;public static ObjectID Weapon;public static float3 Pos(World w,Entity p)=>Position;public static bool CanHold(World w,Entity p,bool mining,out string what,out string why){what=why="";return Fight;}public static ObjectID FindWeapon(World w,Entity p)=>Weapon;public static int WeaponSlotForAttack(World w,Entity p,out string what,out string swap){what=Weapon.ToString();swap="";return 3;}public static bool SlotBroken(World w,Entity p,int slot)=>false;public static float2 DirTo(float3 from,float2 to)=>math.normalize(to-from.xz);public static int2 TileOf(float3 p)=>new int2((int)p.x,(int)p.z);}
public class HitOnceSkill{public const float Reach=1.3f;}
public struct ClientInput{public float2 movementDirection;}
public static class AIPlayerSpikeMod{public static void Log(string x){}}
public static class AIChargeWatch{public static void NoteMove(Entity p,float now){}}
public static class MoveStart{public static string Prior(float a,float b,float c,float d)=>"fixture";public static bool Along(float a,float b,float c,float d)=>a*c+b*d>0;}
public enum SkillStatus{Running,Done,Failed}
public abstract class AISkill{public string Name,Key,Reason;public int Run;public float StartTime;public SkillStatus Status;public List<string> Facts=new List<string>();public abstract bool Start(World w,Entity p,float n);public abstract BodyInput Tick(World w,Entity p,float n);public virtual void Cleanup(World w){}public virtual float EtaSeconds(World w,Entity p)=>-1;public virtual float RemainingSeconds(World w,Entity p)=>-1;public virtual string Progress(World w,Entity p)=>"";protected BodyInput Finish(SkillStatus s,string why){Status=s;Reason=why;return default;}protected BodyInput Finish(SkillStatus s,string why,SkillEnd end)=>Finish(s,why);protected void ObserveOnce(string key,string why){Facts.Add(why);}}
