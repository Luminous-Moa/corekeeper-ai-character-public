from pathlib import Path
r=Path('C:/Dev/CoreKeeperModSDK-unrestricted/Tools/restriction-tests')
p=r/'prepare.mjs';s=p.read_text(encoding='utf-8');s=s.replace("'    public static bool GoalDone'","'    public static bool GoalDone','    private static bool Holds','    private static List<int2> Remaining'");s=s.replace('public static int Receipts=>S.Placed.Count;','public static int Receipts=>S.Placed.Count;\npublic static List<int2> Inspect(World w){Remaining(w,default,ObjectID.Wood,ShapeList(S.Ref));return ShapeList(S.Ref);}')
s='\n'.join(l for l in s.split('\n') if not l.startswith('private static List<int2> Remaining(World'))
p.write_text(s,encoding='utf-8')
p=r/'Program.cs';s=p.read_text(encoding='utf-8');s=s.replace('Console.WriteLine("ALL PASS "+checks);','''AIPlaceGoal.Begin(r);var occupied=new World{Bag=200};for(int x=0;x<200;x++)occupied.Tiles.Add(new int2(x,0));
Check(AIPlaceGoal.Inspect(occupied).Count==72,"actual Remaining observes preexisting first chunk and advances");
Check(AIPlaceGoal.Inspect(occupied).Count==0&&AIPlaceGoal.GoalDone(occupied,default,out _),"all preexisting later chunks finish without false new receipts");
var edgePath=AITunnelSearch.Find((int.MaxValue,0),(int.MinValue,0),(int.MinValue,int.MinValue),(int.MaxValue,int.MaxValue),(x,y)=>y==0&&(x==int.MaxValue||x==int.MinValue)?1:-1,out var edgeCap,10);
Check(edgePath==null&&!edgeCap,"search does not wrap across signed coordinate boundary");
var farPath=AITunnelSearch.Find((0,0),(400,0),(int.MinValue,int.MinValue),(int.MaxValue,int.MaxValue),(x,y)=>y==0&&x>=0&&x<=400?1:-1,out var farCap);
Check(farPath.Count==400&&!farCap,"actual route search accepts target beyond old260 distance");
Console.WriteLine("ALL PASS "+checks);''');p.write_text(s,encoding='utf-8')
p=r/'restriction-tests.csproj';s=p.read_text();s=s.replace('</ItemGroup>','<Compile Include="../../Assets/AIPlayerSpike/Scripts/Core/AITunnelSearch.cs" Link="AITunnelSearch.cs" /></ItemGroup>');p.write_text(s)
