using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class SharedTunnelSearchTests
{
    public static void Run()
    {
        AllTargetsAgainstFrozenOracle();
        TerminalAndCaps();
        DetourFacts();
        LargeBatch();
    }

    private static bool Complete(AITunnelSearch.SharedSearchJob tree, int budget)
    {
        bool bounded = !tree.Step(0) && !tree.Step(-1) && tree.LastStepWork == 0;
        while (!tree.IsCompleted)
        {
            int before = tree.CostCalls;
            tree.Step(budget);
            bounded &= tree.LastStepWork > 0 && tree.LastStepWork <= budget && tree.CostCalls - before <= 4L * budget;
        }
        double elapsed = tree.ElapsedMilliseconds;
        return bounded && tree.Step(budget) && tree.LastStepWork == 0 && elapsed == tree.ElapsedMilliseconds;
    }

    private static AITunnelSearch.SharedQueryJob Query(AITunnelSearch.SharedSearchJob tree,
        (int x, int y) target, Func<int, int, int> hits = null, int budget = 7)
    {
        var query = tree.CreateQuery(target, hits);
        if (query.Step(0) || query.Step(-1) || query.LastStepWork != 0) throw new Exception("Query consumed a nonpositive budget.");
        while (!query.IsCompleted)
        {
            query.Step(budget);
            if (query.LastStepWork <= 0 || query.LastStepWork > budget) throw new Exception("Query exceeded its budget.");
        }
        double elapsed = query.ElapsedMilliseconds;
        if (!query.Step(budget) || query.LastStepWork != 0 || elapsed != query.ElapsedMilliseconds)
            throw new Exception("Completed query mutated timing or work.");
        return query;
    }

    private static long Cost(List<(int x, int y)> path, Func<int, int, int> cost) =>
        path == null ? -1 : path.Sum(tile => (long)cost(tile.x, tile.y));

    private static bool Valid(AITunnelSearch.SharedQueryJob query, (int x, int y) start,
        (int x, int y) target, Func<int, int, int> cost, Func<int, int, bool> closed, Func<int, int, int> hits)
    {
        if (query.Result == null) return query.TotalCost == -1;
        var previous = start;
        var visited = new HashSet<(int, int)> { start };
        foreach (var tile in query.Result)
        {
            if (Math.Abs((long)tile.x - previous.x) + Math.Abs((long)tile.y - previous.y) != 1 ||
                !visited.Add(tile) || cost(tile.x, tile.y) < 0 || (tile != target && closed != null && closed(tile.x, tile.y))) return false;
            previous = tile;
        }
        return previous == target && query.TotalCost == Cost(query.Result, cost) &&
            query.Hits == AITunnelSearch.Hits(query.Result, hits) &&
            query.IntermediateHits == AITunnelSearch.Hits(query.Result, hits, true);
    }

    private static void AllTargetsAgainstFrozenOracle()
    {
        var random = new Random(829507);
        foreach (int budget in new[] { 1, 7, 1000000 })
        {
            bool all = true, bounded = true;
            for (int sample = 0; sample < 12; sample++)
            {
                var costs = new int[12, 10]; var edges = new bool[12, 10]; var hits = new int[12, 10];
                for (int x = 0; x < 12; x++) for (int y = 0; y < 10; y++)
                {
                    costs[x, y] = random.Next(6) == 0 ? -1 : random.Next(7);
                    edges[x, y] = random.Next(4) == 0;
                    hits[x, y] = random.Next(-2, 9);
                }
                Func<int, int, int> cost = (x, y) => costs[x, y];
                Func<int, int, bool> edge = (x, y) => edges[x, y];
                Func<int, int, int> hit = (x, y) => hits[x, y];
                foreach (bool close in new[] { false, true })
                {
                    var tree = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (11, 9), cost, close ? edge : null);
                    bounded &= Complete(tree, budget);
                    int pops = tree.HeapPops;
                    for (int x = 0; x < 12; x++) for (int y = 0; y < 10; y++)
                    {
                        var target = (x, y);
                        var expected = TunnelSearchOriginal.Find((0, 0), target, (0, 0), (11, 9),
                            (xx, yy) => close && (xx, yy) != target && edge(xx, yy) ? -1 : cost(xx, yy));
                        var query = Query(tree, target, hit, budget);
                        all &= !query.Capped && query.TotalCost == Cost(expected, cost) &&
                            (query.Result == null) == (expected == null) && Valid(query, (0, 0), target, cost, close ? edge : null, hit);
                    }
                    all &= tree.HeapPops == pops;
                }
            }
            Program.Check(all && bounded, $"shared all targets: frozen oracle optimum/reachability, actual hits, bounded work, budget {budget}");
        }
    }

    private static void TerminalAndCaps()
    {
        Func<int, int, int> cost = (x, y) => 1;
        var tree = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (4, 0), cost, (x, y) => x == 2);
        Complete(tree, 1);
        var terminal = Query(tree, (2, 0), (x, y) => x == 2 ? 9 : 0);
        Program.Check(terminal.TotalCost == 2 && terminal.Hits == 9 && terminal.IntermediateHits == 0 &&
            terminal.EdgeClosed == 0 && Query(tree, (4, 0)).Result == null && !Query(tree, (4, 0)).Capped,
            "shared terminal exception reaches only its target, never crosses it into another target");

        var blocked = Query(tree, (3, 0));
        Program.Check(blocked.Result == null && blocked.EdgeClosed == 1, "shared closed observations exclude only the queried target");
        bool caps = true;
        foreach (int cap in new[] { 0, 1, 3, 5 })
        {
            var capped = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (4, 0), cost, null, cap);
            Complete(capped, 1);
            for (int x = 0; x < 5; x++)
            {
                var query = Query(capped, (x, 0));
                caps &= query.Capped == (x >= cap) && (query.Result != null) == (x < cap);
            }
        }
        Program.Check(caps, "shared caps return settled routes and preserve capped distinction for pending targets");

        var terminalCap = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (4, 0), cost, (x, y) => x == 1, 1);
        Complete(terminalCap, 1);
        Program.Check(Query(terminalCap, (1, 0)).TotalCost == 1 && !Query(terminalCap, (1, 0)).Capped,
            "shared terminal route is proven by a settled adjacent node");

        var outside = new AITunnelSearch.SharedSearchJob((-1, 0), (0, 0), (2, 0), cost);
        Complete(outside, 1);
        Program.Check(Query(outside, (-1, 0)).Result.Count == 0 && Query(outside, (2, 0)).TotalCost == 3,
            "shared start outside bounds remains a valid root and supports start equals target");
        var zeroCap = new AITunnelSearch.SharedSearchJob((-1, 0), (0, 0), (2, 0), cost, null, 0);
        Complete(zeroCap, 1);
        Program.Check(Query(zeroCap, (-1, 0)).Capped, "shared zero guard still caps start equals target outside bounds");

        var extreme = new AITunnelSearch.SharedSearchJob((int.MaxValue - 2, int.MinValue),
            (int.MaxValue - 2, int.MinValue), (int.MaxValue, int.MinValue + 1), (x, y) => int.MaxValue);
        Complete(extreme, 1);
        var largeCost = Query(extreme, (int.MaxValue, int.MinValue));
        Program.Check(largeCost.TotalCost == 2L * int.MaxValue && largeCost.Result.Count == 2,
            "shared coordinate arithmetic and accumulated cost do not overflow");

        var longTree = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (1000, 0), (x, y) => 0);
        Complete(longTree, 7);
        var longQuery = longTree.CreateQuery((1000, 0));
        longQuery.Step(3);
        bool yields = !longQuery.IsCompleted && longQuery.Result == null && longQuery.LastStepWork == 3;
        while (!longQuery.Step(7)) { }
        Program.Check(yields && longQuery.Result.Count == 1000 && longQuery.TotalCost == 0,
            "shared zero-cost long path reconstruction yields without parent cycles");
        bool rejectsEarly = false;
        try { new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (1, 1), cost).CreateQuery((1, 1)); }
        catch (InvalidOperationException) { rejectsEarly = true; }
        Program.Check(rejectsEarly, "shared query requires a completed tree");
    }

    private static void DetourFacts()
    {
        foreach (int height in new[] { 1, 8 })
        foreach (bool targetWall in new[] { false, true })
        {
            Func<int, int, int> cost = (x, y) => 1;
            Func<int, int, bool> edge = (x, y) => x == 1 && y < height;
            Func<int, int, int> hits = (x, y) => targetWall && x == 2 && y == 0 ? 30 : 0;
            var closedTree = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (2, height), cost, edge);
            var openTree = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (2, height), cost);
            Complete(closedTree, 7); Complete(openTree, 7);
            var first = Query(closedTree, (2, 0), hits); var second = Query(openTree, (2, 0), hits);
            float firstEstimate = AITunnelSearch.EstTiles(first.Result.Count, first.Hits);
            float secondEstimate = AITunnelSearch.EstTiles(second.Result.Count, second.Hits);
            bool offer = AITunnelSearch.NeedsDetourCheck(first.Result.Count, first.IntermediateHits > 0, first.EdgeClosed) &&
                AITunnelSearch.OfferDetour(firstEstimate, secondEstimate);
            var legacy = TunnelSearchOriginal.FindWithDetour((0, 0), (2, 0), (0, 0), (2, height), cost, edge, hits, true,
                out bool used, out _, out var detour, out _, out _, out float legacyEstimate, out _, out _, out _, out _);
            Program.Check(!used && firstEstimate == legacyEstimate && Cost(legacy, cost) == first.TotalCost &&
                offer == (detour != null), $"shared detour thresholds and target-hit exclusion match oracle, height {height}, wall {targetWall}");
        }
        var closed = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (4, 0), (x, y) => 1, (x, y) => x == 2);
        var open = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (4, 0), (x, y) => 1);
        Complete(closed, 7); Complete(open, 7);
        var fallback = Query(open, (4, 0), (x, y) => x == 2 ? 11 : 0);
        Program.Check(Query(closed, (4, 0)).Result == null && fallback.TotalCost == 4 && fallback.IntermediateHits == 11,
            "shared open tree supplies edge fallback and actual intermediate mining facts");
    }

    private static void LargeBatch()
    {
        const int width = 182, height = 168, known = 30492, targets = 7526;
        var protectedTiles = new HashSet<(int, int)>();
        for (int y = 1; y < height && protectedTiles.Count < targets; y += 2)
            for (int x = 1; x < width && protectedTiles.Count < targets; x += 2)
                if (y * width + x < known) protectedTiles.Add((x, y));
        Func<int, int, int> cost = (x, y) => y * width + x < known ? 1 + (x + y) % 3 : -1;
        Func<int, int, bool> edge = (x, y) => protectedTiles.Contains((x, y));
        var watch = Stopwatch.StartNew();
        var closed = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (width - 1, height - 1), cost, edge);
        var open = new AITunnelSearch.SharedSearchJob((0, 0), (0, 0), (width - 1, height - 1), cost);
        bool bounded = Complete(closed, 127) && Complete(open, 127);
        int pops = closed.HeapPops + open.HeapPops;
        int calls = closed.CostCalls + open.CostCalls;
        bool all = protectedTiles.Count == targets;
        foreach (var target in protectedTiles)
        {
            var first = Query(closed, target, null, 127); var second = Query(open, target, null, 127);
            all &= first.Result != null && second.Result != null && !first.Capped && !second.Capped &&
                first.TotalCost >= second.TotalCost && Valid(first, (0, 0), target, cost, edge, null);
        }
        watch.Stop();
        Program.Check(all && bounded && !closed.Capped && !open.Capped && pops <= 2 * known &&
            closed.HeapPops + open.HeapPops == pops && closed.CostCalls + open.CostCalls == calls && calls <= 2 * width * height,
            $"shared 7526 protected targets / 30492 known tiles: {pops} pops, {calls} cost calls, two trees only, {watch.ElapsedMilliseconds} ms");
    }
}
