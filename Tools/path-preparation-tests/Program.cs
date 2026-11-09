using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class Program
{
    private static int failures, checks;
    internal static void Check(bool ok, string message)
    {
        checks++; if (!ok) failures++;
        Console.WriteLine($"[{(ok ? "OK" : "FAIL")}] {message}");
    }
    public static int Main()
    {
        AIBrain.RunSchedulerChecks();
        Console.WriteLine(failures == 0 ? $"ALL SCHEDULER CHECKS OK ({checks})" : $"FAILED {failures} of {checks}");
        return failures;
    }
}

// This partial supplies the surrounding game dependencies. Scheduling, context switching, cancellation,
// disposal, completion ordering and the global stopwatch budget execute from AIBrain.Preparation.cs.
public static partial class AIBrain
{
    private sealed class State
    {
        public Preparation Preparation;
        public readonly Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> _plans = new Dictionary<long, (float, int2, int, TunnelPlan, string)>();
        public int Revision = 1, ExecutionRevision = 1, Moves, Completions, Disposals;
        public bool NeedCall, Throw, ChangeTerrain, ContextCorrect = true;
        public readonly int Id;
        public State(int id) { Id = id; }
    }
    private sealed class BuildContext
    {
        public AITunnel.PlanBatch Batch;
        public int _nPlans, _nPlansCached, RouteCountHint;
        public object Query;
        public readonly Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> Plans = new Dictionary<long, (float, int2, int, TunnelPlan, string)>();
        public readonly Dictionary<long, int> PlanToolRed = new Dictionary<long, int>();
    }
    private sealed class Options { public float waitSeconds = 2f, combatRange = 8f; }
    private static State S;
    private static int Revision => S.Revision;
    private static bool _needCall { get => S.NeedCall; set => S.NeedCall = value; }
    private static bool _inCombat = false;
    private static string _needReason = "test";
    private static float _retryAt;
    private static int _nPlans, _nPlansCached;
    private static double _msPlans;
    private static BuildContext _building;
    private static Options Settings() => new Options();
    private static void WorkUnit()
    {
        // Deliberately exceed one scheduler allowance so each call must yield before the next unit.
        // A real Step cannot be interrupted mid-callback either; this also exposes that boundary.
        var work = Stopwatch.StartNew();
        while (work.Elapsed.TotalMilliseconds < 5.0) { }
    }
    private static IEnumerable<int> BuildCandidateSteps(World world, Entity player, AIObservation observation,
        bool prefetch, bool breach, Action<List<AICandidate>> accept)
    {
        var owner = S;
        var context = _building;
        try
        {
            for (int i = 0; i < 2; i++)
            {
                if (owner.Throw) throw new InvalidOperationException("injected candidate failure");
                owner.ContextCorrect &= ReferenceEquals(owner, S) && ReferenceEquals(context, _building);
                owner.ContextCorrect &= context.Plans.Count == 0 || context.Plans.ContainsKey(owner.Id);
                context.Plans[owner.Id] = (0, default, 0, new TunnelPlan(), "owner " + owner.Id);
                owner.Moves++; WorkUnit(); yield return 1;
            }
            accept(new List<AICandidate> { new AICandidate() });
        }
        finally { owner.Disposals++; }
    }
    private static IEnumerable<int> CompletePreparationSteps(World world, Entity player, float now, Preparation job)
    {
        Program.Check(job.Candidates != null && S.Moves == 2 && ReferenceEquals(_building, job.Context),
            "completion runs after candidate generation in its own build context");
        WorkUnit(); yield return 1;
        if (S.ChangeTerrain) job.Context.Batch.Changed = true;
        foreach (var step in ValidatePreparation(job)) yield return step;
        S.Completions++;
    }

    private static Preparation Begin(State state, World world, Entity player)
    {
        S = state;
        return state.Preparation = new Preparation(world, player, Time.realtimeSinceStartup, "test", false, false, false, "key");
    }
    private static void Tick(State state, World world, Entity player, bool newFrame = true)
    {
        if (newFrame) { Time.frameCount++; Time.realtimeSinceStartup += .016f; }
        S = state;
        StepPreparation(world, player, Time.realtimeSinceStartup);
    }
    public static void RunSchedulerChecks()
    {
        var world = new World(); var player = new Entity(1);
        _building = new BuildContext(); var outer = _building;
        // Warm JIT/initialization so budget checks below test actual work rather than compilation startup.
        var warm = new State(99); Begin(warm, world, player);
        while (warm.Preparation != null) Tick(warm, world, player);
        var a = new State(1); var b = new State(2);
        var ja = Begin(a, world, player); var jb = Begin(b, world, new Entity(2));
        while (a.Moves == 0) Tick(a, world, player);
        int aMoves = a.Moves;
        Tick(b, world, new Entity(2), false);
        Program.Check(b.Moves == 0 && jb.Frames == 0 && aMoves == 1, "two AIs share one frame allowance; second AI cannot claim a fresh budget");
        Program.Check(ReferenceEquals(outer, _building), "previous build context restored after yielding");
        Tick(b, world, new Entity(2));
        Program.Check(b.Moves == 1 && a.Moves == 1 && ja.Context.Plans.ContainsKey(1) && jb.Context.Plans.ContainsKey(2),
            "next frame resumes second AI with its own context and progress");
        while (a.Preparation != null || b.Preparation != null)
        {
            if (a.Preparation != null) Tick(a, world, player);
            if (b.Preparation != null) Tick(b, world, new Entity(2));
        }
        Program.Check(a.ContextCorrect && b.ContextCorrect && a.Completions == 1 && b.Completions == 1 && a.Moves == 2 && b.Moves == 2,
            "interleaved jobs complete once without losing or mixing work");
        Program.Check(a.Disposals == 1 && b.Disposals == 1 && ja.Context.Plans.Count == 0 && jb.Context.Plans.Count == 0,
            "completed iterators disposed and build-local plans released");

        var fixedA = new State(51); var fixedB = new State(52);
        Begin(fixedA, world, player); Begin(fixedB, world, new Entity(2));
        for (int frame = 0; frame < 8; frame++)
        {
            Tick(fixedA, world, player);
            Tick(fixedB, world, new Entity(2), false);
        }
        Program.Check(fixedA.Completions == 1 && fixedB.Completions == 1 && fixedA.Moves == 2 && fixedB.Moves == 2,
            "fixed a-then-b frame order gives both AIs progress despite indivisible 5ms units");

        foreach (string change in new[] { "world", "player", "goal", "execution" })
        {
            var state = new State(10); var job = Begin(state, world, player);
            while (state.Moves == 0) Tick(state, world, player);
            int before = state.Moves;
            if (change == "goal") state.Revision++;
            if (change == "execution") state.ExecutionRevision++;
            Tick(state, change == "world" ? new World() : world, change == "player" ? new Entity(100) : player, false);
            Program.Check(state.Preparation == null && state.NeedCall && state.Moves == before && state.Completions == 0 && state.Disposals == 1 && job.Context.Plans.Count == 0,
                change + " change cancels before advancing even when frame budget is already exhausted");
            Program.Check(ReferenceEquals(outer, _building), change + " cancellation preserves outer context");
        }

        var changedTerrain = new State(61) { ChangeTerrain = true }; Begin(changedTerrain, world, player);
        while (changedTerrain.Preparation != null) Tick(changedTerrain, world, player);
        Program.Check(changedTerrain.Completions == 0 && changedTerrain.NeedCall && changedTerrain.Disposals == 1,
            "changed terrain cancels the whole preparation before sending instead of reporting no path");

        var broken = new State(30) { Throw = true }; Begin(broken, world, player);
        while (broken.Preparation != null) Tick(broken, world, player);
        Program.Check(broken.NeedCall && broken.Disposals == 1 && broken.Completions == 0 && _retryAt > Time.realtimeSinceStartup && ReferenceEquals(outer, _building),
            "candidate exception disposes job, restores context and schedules retry without completion");
        Program.Check(AIPlayerSpikeMod.Errors == 1, "injected failure is reported exactly once");
        foreach (int targets in new[] { 357, 2179 })
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
        Console.WriteLine("Production scheduler source: Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs (direct Compile link)");
    }
}

namespace Unity.Entities
{
    public sealed class World { }
    public readonly struct Entity
    {
        public readonly int Index;
        public Entity(int index) { Index = index; }
        public static bool operator ==(Entity a, Entity b) => a.Index == b.Index;
        public static bool operator !=(Entity a, Entity b) => !(a == b);
        public override bool Equals(object obj) => obj is Entity other && this == other;
        public override int GetHashCode() => Index;
    }
}
namespace Unity.Mathematics
{
    public struct int2 { public int x,y; public int2(int x,int y) {this.x=x;this.y=y;} public static int2 operator -(int2 a,int2 b)=>new int2(a.x-b.x,a.y-b.y); }
    public struct float2 { public float x,y; public static explicit operator float2(int2 a)=>new float2{x=a.x,y=a.y}; }
    public static class math { public static int cmax(int2 v)=>Math.Max(v.x,v.y); public static int2 abs(int2 v)=>new int2(Math.Abs(v.x),Math.Abs(v.y)); public static int max(int a,int b)=>Math.Max(a,b); public static float distance(float2 a,float2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
}
namespace UnityEngine { public static class Time { public static int frameCount; public static float realtimeSinceStartup; } }
public sealed class TunnelPlan { public int toolShortRed; public readonly List<int2> path = new List<int2>(); }
public sealed class AICandidate { }
public sealed class AIObservation
{
    public int2 pos;
    public readonly List<SeenEnemy> enemies = new List<SeenEnemy>();
    public static AIObservation Take(World world, Entity player, bool force) => new AIObservation();
}
public sealed class SeenEnemy { public Entity entity; public float dist; }
public static class AIMemory { public static void Observe(World world, Entity player, float now, bool force) { } }
public static class AIGoals { public static void RefreshGuidance(World world, Entity player) { } }
public static class AIEdge
{
    public static bool PlanActive, BreachPending;
    public static bool LastAssessedCannotCome(Entity entity) => false;
    public static int2 StandTile(World world, int2 pos) => pos;
}
public static class AIBody { public static int2 Pos(World world, Entity player) => default; }
public static class AITunnel
{
    public static int LastToolShortRed, Calls;
    public sealed class PlanBatch
    {
        public bool Changed;
        public int SearchCount, CapturedTileCount;
        public long ExpandedTotal;
        public string Reason => "terrain changed";
        public int2 Start => default;
        public bool StepCapture(int budget) => true;
        public bool SearchesReady;
        public bool StepSearches(int budget) { SearchesReady = true; return true; }
        public PlanJob BeginSinglePlan(int2 target, int damage, bool edge, bool detour) { Calls++; return new PlanJob(); }
        public PlanJob BeginPlan(int2 target, int damage, bool edge, bool detour) { Calls++; return new PlanJob(); }
        public void BeginValidation() { }
        public bool StepValidation(int budget) => true;
    }
    public static PlanBatch BeginBatch(World world, Entity player, int2 pos) => new PlanBatch();
    public sealed class PlanJob
    {
        public bool Changed => false;
        public TunnelPlan Result => new TunnelPlan();
        public string Reason => "stub";
        public bool Step(int budget) => true;
        public void RunUntilDone() { }
    }
    public static TunnelPlan Plan(World world, Entity player, int2 target, out string why, int damage, bool edge, bool detour) { Calls++; why="stub"; return new TunnelPlan(); }
    public static PlanJob BeginPlan(World world, Entity player, int2 target, int guard, bool edge, bool detour) => new PlanJob();
}
public static class AIPlayerSpikeMod
{
    public static int Errors;
    public static void Log(string text) { }
    public static void LogError(string text, Exception error) { Errors++; }
}
