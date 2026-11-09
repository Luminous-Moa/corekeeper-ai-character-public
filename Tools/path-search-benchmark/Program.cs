// Synthetic pure-Core stress benchmark, not a replay of the game map or a Unity frame-time claim.
// The original 707 calls were 357 generated routes + 350 repeated fact lookups, not 707 distinct targets.
// We execute every request here (no invented cache); production candidate/cache coverage belongs elsewhere.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class Program
{
    private const int Budget = 32, Width = 55, Height = 45;
    private static int failures;
    public static void Check(bool ok, string message)
    {
        Console.WriteLine($"[{(ok ? "OK" : "FAIL")}] {message}");
        if (!ok) failures++;
    }
    private static bool Same(List<(int x, int y)> a, List<(int x, int y)> b) =>
        a == null ? b == null : b != null && a.SequenceEqual(b);
    private static int Cost(int x, int y) => (x * 17 + y * 31) % 29 == 0 && x != 0 && y != 0 ? -1 : 1 + (x * 11 + y * 7) % 5;
    private static bool Edge(int x, int y) => x == 13 && y != 5 || x == 31 && y != 29;
    private static int Hits(int x, int y) => Cost(x, y) >= 4 ? 2 : 0;

    private sealed class Metrics
    {
        public readonly List<long> StepTicks = new List<long>();
        public long Work, Calls;
        public int MaxWork, Completed, Found, Capped, Detours, Passes;
        public bool Equal = true, Bounded = true;
        public void Step(int work, long ticks)
        {
            StepTicks.Add(ticks); Calls++; Work += work; MaxWork = Math.Max(MaxWork, work);
            Bounded &= work > 0 && work <= Budget;
        }
        public void Print(string label, int requests, int unique, double baselineMs, double wallMs)
        {
            StepTicks.Sort();
            double scale = 1000.0 / Stopwatch.Frequency;
            double median = StepTicks.Count % 2 == 0 ? (StepTicks[StepTicks.Count / 2 - 1] + StepTicks[StepTicks.Count / 2]) / 2.0 : StepTicks[StepTicks.Count / 2];
            Console.WriteLine($"METRIC {label} requests={requests} unique={unique} completed={Completed} found={Found} capped={Capped} detours={Detours} passes={Passes} step_calls={Calls} work={Work} max_work={MaxWork} baseline_ms={baselineMs:F3} stepped_wall_ms={wallMs:F3} step_mean_ms={StepTicks.Average() * scale:F6} step_median_ms={median * scale:F6} step_p99_ms={StepTicks[(int)Math.Floor((StepTicks.Count - 1) * .99)] * scale:F6} step_max_ms={StepTicks[StepTicks.Count - 1] * scale:F6}");
            Check(Completed == requests && Equal && Bounded, label + ": all targets completed, original results equal, every Step <= 32 work");
        }
    }

    public static int Main()
    {
        // Warm both implementations before measuring. No threshold assertion on machine-dependent timings.
        for (int i = 0; i < 16; i++)
        {
            var target = (20 + i, 20);
            TunnelSearchOriginal.FindWithDetour((0, 0), target, (0, 0), (Width - 1, Height - 1), Cost, Edge, Hits, true,
                out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            var job = new AITunnelSearch.FindWithDetourJob((0, 0), target, (0, 0), (Width - 1, Height - 1), Cost, Edge, Hits, true);
            while (!job.Step(Budget)) { }
        }
        var all = new List<(int x, int y)>();
        for (int i = 1; i <= 2179; i++) all.Add((i % Width, i / Width));
        var repeated = all.Take(357).Concat(all.Take(350)).ToList();
        foreach (var requests in new[] { repeated, all })
        {
            RunFind(requests);
            RunDetour(requests);
        }
        Console.WriteLine(failures == 0 ? "ALL BENCHMARK CHECKS OK" : "BENCHMARK FAILED " + failures);
        return failures;
    }

    private static void RunFind(List<(int x, int y)> targets)
    {
        var expected = new List<(List<(int x, int y)> route, bool capped)>();
        var timer = Stopwatch.StartNew();
        foreach (var target in targets)
        {
            var route = TunnelSearchOriginal.Find((0, 0), target, (0, 0), (Width - 1, Height - 1), Cost, out bool capped);
            expected.Add((route, capped));
        }
        double baseline = timer.Elapsed.TotalMilliseconds;
        var metrics = new Metrics(); timer.Restart();
        for (int i = 0; i < targets.Count; i++)
        {
            var job = new AITunnelSearch.FindJob((0, 0), targets[i], (0, 0), (Width - 1, Height - 1), Cost);
            while (!job.IsCompleted)
            {
                long began = Stopwatch.GetTimestamp(); job.Step(Budget);
                metrics.Step(job.LastStepWork, Stopwatch.GetTimestamp() - began);
            }
            metrics.Completed++; metrics.Passes++;
            if (job.Result != null) metrics.Found++;
            if (job.Capped) metrics.Capped++;
            metrics.Equal &= Same(expected[i].route, job.Result) && expected[i].capped == job.Capped;
        }
        metrics.Print("Find/" + targets.Count, targets.Count, targets.Distinct().Count(), baseline, timer.Elapsed.TotalMilliseconds);
    }

    private static void RunDetour(List<(int x, int y)> targets)
    {
        var expected = new List<(List<(int x, int y)> route, List<(int x, int y)> detour, bool usedEdge, int closed, bool firstCap, bool allCap, float firstEst, float detourEst, int passes)>();
        var timer = Stopwatch.StartNew();
        foreach (var target in targets)
        {
            var route = TunnelSearchOriginal.FindWithDetour((0, 0), target, (0, 0), (Width - 1, Height - 1), Cost, Edge, Hits, true,
                out bool used, out int closed, out var detour, out bool firstCap, out bool allCap, out float firstEst, out float detourEst, out int passes, out _, out _);
            expected.Add((route, detour, used, closed, firstCap, allCap, firstEst, detourEst, passes));
        }
        double baseline = timer.Elapsed.TotalMilliseconds;
        var metrics = new Metrics(); timer.Restart();
        for (int i = 0; i < targets.Count; i++)
        {
            var job = new AITunnelSearch.FindWithDetourJob((0, 0), targets[i], (0, 0), (Width - 1, Height - 1), Cost, Edge, Hits, true);
            while (!job.IsCompleted)
            {
                long began = Stopwatch.GetTimestamp(); job.Step(Budget);
                metrics.Step(job.LastStepWork, Stopwatch.GetTimestamp() - began);
            }
            metrics.Completed++; metrics.Passes += job.Passes;
            if (job.Result != null) metrics.Found++;
            if (job.AllCapped) metrics.Capped++;
            if (job.Detour != null) metrics.Detours++;
            var value = expected[i];
            metrics.Equal &= Same(value.route, job.Result) && Same(value.detour, job.Detour) && value.usedEdge == job.UsedEdge &&
                value.closed == job.EdgeClosed && value.firstCap == job.FirstCapped && value.allCap == job.AllCapped &&
                value.firstEst == job.FirstEst && value.detourEst == job.DetourEst && value.passes == job.Passes;
        }
        metrics.Print("Detour/" + targets.Count, targets.Count, targets.Distinct().Count(), baseline, timer.Elapsed.TotalMilliseconds);
    }
}
