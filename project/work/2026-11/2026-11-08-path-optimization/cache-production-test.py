from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig');a=s.index('    internal static TunnelPlan PlanC(');b=s.index('    // Seconds to kill',a);method=s[a:b];s=s[:a]+s[b:];p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');a=s.index('    // Long-lived hints');s=s[:a]+method+s[a:];p.write_text(s,encoding='utf-8')
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');a=s.index('    private static TunnelPlan PlanC(');b=s.index('    private static void WorkUnit()',a);s=s[:a]+s[b:]
s=s.replace('        public Preparation Preparation;', '        public Preparation Preparation;\n        public readonly Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> _plans = new Dictionary<long, (float, int2, int, TunnelPlan, string)>();')
s=s.replace('    private static int _nPlans;', '    private static int _nPlans, _nPlansCached;')
s=s.replace('namespace Unity.Mathematics { public struct int2 { public int x, y; } }', '''namespace Unity.Mathematics
{
    public struct int2 { public int x,y; public int2(int x,int y) {this.x=x;this.y=y;} public static int2 operator -(int2 a,int2 b)=>new int2(a.x-b.x,a.y-b.y); }
    public struct float2 { public float x,y; public static explicit operator float2(int2 a)=>new float2{x=a.x,y=a.y}; }
    public static class math { public static int cmax(int2 v)=>Math.Max(v.x,v.y); public static int2 abs(int2 v)=>new int2(Math.Abs(v.x),Math.Abs(v.y)); public static int max(int a,int b)=>Math.Max(a,b); public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
}''')
s=s.replace('public sealed class TunnelPlan { }', 'public sealed class TunnelPlan { public int toolShortRed; public readonly List<int2> path = new List<int2>(); }')
s=s.replace('    public static int LastToolShortRed;', '    public static int LastToolShortRed, Calls;')
s=s.replace('public PlanJob BeginPlan(int2 target, int damage, bool edge, bool detour) => new PlanJob();', 'public PlanJob BeginPlan(int2 target, int damage, bool edge, bool detour) { Calls++; return new PlanJob(); }')
s=s.replace('        public bool Step(int budget) => true;', '        public bool Step(int budget) => true;\n        public void RunUntilDone() { }')
s=s.replace('    public static PlanJob BeginPlan(World world', '    public static TunnelPlan Plan(World world, Entity player, int2 target, out string why, int damage, bool edge, bool detour) { Calls++; why="stub"; return new TunnelPlan(); }\n    public static PlanJob BeginPlan(World world')
# Actual production warm/result-cache methods, backend counted independently.
pos=s.index('        Console.WriteLine("Production scheduler source:')
s=s[:pos]+'''        foreach (int targets in new[] { 357, 2179 })
        {
            S = new State(80);
            _building = new BuildContext { Batch = AITunnel.BeginBatch(world, player, default) };
            AITunnel.Calls = 0; _nPlans = 0; _nPlansCached = 0;
            for (int i = 0; i < targets; i++)
            {
                var target = new int2(i, i % 41);
                foreach (var unit in PreparePlan(world, player, target)) { Time.realtimeSinceStartup += .016f; }
                PlanC(world, player, target, out _);
            }
            int afterGeneration = AITunnel.Calls;
            Time.realtimeSinceStartup += 60f;
            int revisit = targets == 357 ? 350 : targets;
            for (int i = 0; i < revisit; i++) PlanC(world, player, new int2(i, i % 41), out _);
            Program.Check(afterGeneration == targets && AITunnel.Calls == targets && _nPlans == targets && _building.Plans.Count == targets,
                $"production PreparePlan→PlanC keeps {targets} routes through TTL and {revisit} annotation requests without recomputation");
            Console.WriteLine($"CACHE targets={targets} annotation={revisit} actualPlans={AITunnel.Calls} cacheReads={_nPlansCached}");
            _building = outer;
        }
'''+s[pos:]
p.write_text(s,encoding='utf-8')
