from pathlib import Path
r=Path('C:/Dev/CoreKeeperModSDK-unrestricted')
p=r/'Assets/AIPlayerSpike/Scripts/AIPlaces.cs';s=p.read_text(encoding='utf-8').replace('if (t.Ref.Kind == "base") return true; // no inferred base-area boundary','if (t.Ref.Kind == "base") return Arrived(server, player, t, out _); // named anchor arrival, not a permission radius');p.write_text(s,encoding='utf-8')
p=r/'Tools/restriction-tests/prepare.mjs';s=p.read_text(encoding='utf-8').replace("'    private static List<int2> Remaining'","'    private static List<int2> Remaining','    public static bool Arrived','    private static bool InArea'");s=s.replace('private static bool Arrived(World w,Entity p,PlaceTarget t,out float d){d=0;return true;}','public static bool Ready(World w,PlaceTarget t)=>InArea(w,default,t);');p.write_text(s,encoding='utf-8')
p=r/'Tools/restriction-tests/Program.cs';s=p.read_text(encoding='utf-8');s=s.replace('Console.WriteLine("ALL PASS "+checks);','''AIPlaces.Parse("base",out var home,out _);var baseTarget=new PlaceTarget{Ref=home,Center=new int2(0,0),ArriveR=0.7f};baseTarget.Stands.Add(new int2(0,0));var travel=new World{Pos=new float3(100,0,0),Bag=1};
Check(!AIPlaceGoal.Ready(travel,baseTarget),"production explicit-base placement guard requires travel from100tiles away");
travel.Pos=new float3(0,0,0);Check(AIPlaceGoal.Ready(travel,baseTarget),"production guard offers placement only after anchor arrival");
var atBase=new PlaceTilesSkill(ObjectID.Wood,new(){new int2(0,0)});AIPlaceGoal.Begin(one);atBase.Start(travel,default,0);for(int i=0;i<20&&atBase.Status==SkillStatus.Running;i++)atBase.Tick(travel,default,i);Check(travel.Tiles.Contains(new int2(0,0))&&!travel.Tiles.Contains(new int2(100,0)),"actual placement skill confirms base tile after travel boundary");
Console.WriteLine("ALL PASS "+checks);''')
s=s.replace('public int Bag;public HashSet<int2>', 'public float3 Pos;public int Bag;public HashSet<int2>')
s=s.replace('public static AISkill Current;','public static float3 Pos(World w,Entity p)=>w.Pos;public static AISkill Current;')
s=s.replace('public List<int2> Shape;}','public List<int2> Shape;public int2 Center;public float ArriveR,AreaR;public List<int2> Stands=new();public List<int2> Plots;}')
s += '''
namespace Unity.Mathematics {public struct float2 {public float x,y;public float2(float a,float b){x=a;y=b;}public static explicit operator float2(int2 p)=>new(p.x,p.y);}public struct float3{public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}public float2 xz=>new(x,z);}}
public static class AIEdge{public static bool PlanActive=>false;public static bool IsInside(int2 p)=>false;public static int2 StandTile(World w,float3 p)=>new((int)p.x,(int)p.z);}
'''
s=s.replace('public static int max(int a,int b)=>Math.Max(a,b);','public static int max(int a,int b)=>Math.Max(a,b);public static float min(float a,float b)=>Math.Min(a,b);public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));')
p.write_text(s,encoding='utf-8')
