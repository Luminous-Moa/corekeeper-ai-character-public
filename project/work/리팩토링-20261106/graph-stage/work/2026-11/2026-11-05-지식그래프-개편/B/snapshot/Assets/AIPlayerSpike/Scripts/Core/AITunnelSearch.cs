// AITunnelSearch (Core) — the route search of the dig plan (AITunnel.Plan) without Unity: a cheapest-cost search on tiles
// (4 directions, the same order and heap as before), and the two passes of 제작대-놓기 실게임반영 검수 E1: first with the
// walls that open the base edge closed, then — only when there is no way — with them open (the plan then says so with the
// edge sentences). Not a rule against the edge: a way by a door or around comes first, the edge way stays possible.
// Tools/core-tests PlaceGoalTests checks the door-or-edge choice on small maps.
using System;
using System.Collections.Generic;

public static class AITunnelSearch
{
    public const int DoorStepCost = 2;   // 실게임 D1: an own / my door tile on a dig route (the open / close time)

    // E4 + D1: a remembered door tile (own edge door or my door). seen: in view now; blockedNow: an object blocks it in
    // view; doorThere: a door is seen on it. A door seen there (or the tile out of view: the memory) is a way through at
    // DoorStepCost; in view with another object on it and no door: blocked; in view and empty: an open tile (1).
    public static int DoorTileCost(bool seen, bool blockedNow, bool doorThere)
    {
        if (!seen || doorThere) return DoorStepCost;
        return blockedNow ? -1 : 1;
    }

    // Cheapest route from start to target within [lo, hi]; cost(x, y) < 0 = blocked. The path excludes the start and ends
    // at the target; null when none. capped (재검수 G3): the search stopped at its step cap (guardMax), not "no way".
    public static List<(int x, int y)> Find((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, int guardMax = 120000) =>
        Find(start, target, lo, hi, cost, out _, guardMax);
    public static List<(int x, int y)> Find((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, out bool capped, int guardMax = 120000)
    {
        var g = new Dictionary<(int, int), int>();
        var came = new Dictionary<(int, int), (int, int)>();
        var heap = new Heap();
        g[start] = 0;
        heap.Push(0, start);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        int guard = 0;
        bool found = false;
        capped = false;
        while (heap.Count > 0)
        {
            if (guard++ >= guardMax) { capped = true; break; }
            var (f, c) = heap.Pop();
            if (g.TryGetValue(c, out int cc) && f > cc) continue;
            if (c == target) { found = true; break; }
            for (int d = 0; d < 4; d++)
            {
                var n = (c.Item1 + dx[d], c.Item2 + dy[d]);
                if (n.Item1 < lo.x || n.Item2 < lo.y || n.Item1 > hi.x || n.Item2 > hi.y) continue;
                int k = cost(n.Item1, n.Item2);
                if (k < 0) continue;
                int ng = f + k;
                if (g.TryGetValue(n, out int og) && og <= ng) continue;
                g[n] = ng;
                came[n] = c;
                heap.Push(ng, n);
            }
        }
        if (!found) return null;
        var path = new List<(int x, int y)>();
        var p = target;
        while (p != start) { path.Add(p); p = came[p]; }
        path.Reverse();
        return path;
    }

    // E1: the two passes. opensEdge(x, y): a wall that opens the base edge when mined (AIEdge.OpensEdge on a wall tile).
    // usedEdge: the second pass was needed (no way without mining the edge); edgeClosed: such walls met in the first pass.
    public static List<(int x, int y)> FindAvoidingEdge((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, Func<int, int, bool> opensEdge, out bool usedEdge, out int edgeClosed, int guardMax = 120000) =>
        FindWithDetour(start, target, lo, hi, cost, opensEdge, null, false, out usedEdge, out edgeClosed, out _, out _, out _, out _, out _, out _, out _, out _, guardMax);

    // 우회 사실 (코어-안전지대 의견; 재검수 G1 기준, HEAD 처음 값 — 실게임에서 맞춤): the first way (edge closed) is checked
    // against the way that may mine the edge when it is DetourMinTiles tiles or more, or has walls to mine; when the edge
    // way's estimated time is DetourGainTiles walking tiles or more shorter, the first way's option says so and the edge
    // way is offered as its own option (the first way stays the plan; the model decides).
    public const int DetourMinTiles = 15, DetourGainTiles = 10;
    public const float HitTiles = 1.2f;   // one hit (~0.5 s) in walking tiles (~2.4 tiles/s), as the options' time estimate
    public static float EstTiles(int tiles, int hits) => tiles + HitTiles * hits;
    public static bool NeedsDetourCheck(int firstTiles, bool firstHasWalls, int edgeClosed) => edgeClosed > 0 && (firstTiles >= DetourMinTiles || firstHasWalls);
    public static bool OfferDetour(float firstEst, float edgeEst) => edgeEst >= 0f && firstEst - edgeEst >= DetourGainTiles;

    // the hits of a route (hitsOf: hits to mine a tile, 0 when it is no wall); skipLast (우회 검사 H1): not the target
    // tile (a wall there is what the dig is for, not a reason to look for another way)
    public static int Hits(List<(int x, int y)> route, Func<int, int, int> hitsOf, bool skipLast = false)
    {
        int h = 0;
        if (route != null && hitsOf != null) for (int i = 0; i < route.Count - (skipLast ? 1 : 0); i++) h += Math.Max(0, hitsOf(route[i].x, route[i].y));
        return h;
    }

    // FindAvoidingEdge plus the detour check (hitsOf null: no hits known, tiles only). detour: the edge way when
    // OfferDetour (else null). firstCapped (G3): the first pass stopped at its cap (the second pass then is no proof that
    // no way without the edge exists). passes: 1 or 2; ms1 / ms2: their times. The edge test is asked once per tile (G2).
    // detourCheck (H1): the detour check runs only when asked (the walks whose options offer the edge way); else only the
    // fallback (no way with the edge closed -> open it). allCapped (H2): no way found and every pass run hit its cap.
    public static List<(int x, int y)> FindWithDetour((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, Func<int, int, bool> opensEdge, Func<int, int, int> hitsOf, bool detourCheck, out bool usedEdge, out int edgeClosed, out List<(int x, int y)> detour, out bool firstCapped, out bool allCapped, out float firstEst, out float detourEst, out int passes, out double ms1, out double ms2, int guardMax = 120000)
    {
        usedEdge = false; detour = null; passes = 1; ms2 = 0; firstEst = -1f; detourEst = -1f; allCapped = false;
        var closed = new HashSet<(int, int)>();
        var edgeMemo = new Dictionary<(int, int), bool>();
        bool Opens(int x, int y) { if (!edgeMemo.TryGetValue((x, y), out bool b)) edgeMemo[(x, y)] = b = opensEdge(x, y); return b; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var first = Find(start, target, lo, hi, (x, y) =>
        {
            int k = cost(x, y);
            if (k >= 0 && (x, y) != target && Opens(x, y)) { closed.Add((x, y)); return -1; }
            return k;
        }, out firstCapped, guardMax);
        ms1 = sw.Elapsed.TotalMilliseconds;
        edgeClosed = closed.Count;
        if (first != null) firstEst = EstTiles(first.Count, Hits(first, hitsOf));
        if (closed.Count == 0) { allCapped = first == null && firstCapped; return first; }
        if (first != null && (!detourCheck || !NeedsDetourCheck(first.Count, Hits(first, hitsOf, true) > 0, closed.Count))) return first;
        sw.Restart();
        var second = Find(start, target, lo, hi, cost, out bool secondCapped, guardMax);
        ms2 = sw.Elapsed.TotalMilliseconds; passes = 2;
        if (second != null) detourEst = EstTiles(second.Count, Hits(second, hitsOf));
        if (first == null) { usedEdge = second != null; allCapped = second == null && firstCapped && secondCapped; return second; }
        if (second != null && OfferDetour(firstEst, detourEst)) detour = second;
        return first;
    }
    private sealed class Heap
    {
        private readonly List<(int f, (int x, int y) p)> _a = new List<(int, (int, int))>();
        public int Count => _a.Count;
        public void Push(int f, (int x, int y) p)
        {
            _a.Add((f, p));
            int i = _a.Count - 1;
            while (i > 0) { int up = (i - 1) / 2; if (_a[up].f <= _a[i].f) break; (_a[up], _a[i]) = (_a[i], _a[up]); i = up; }
        }
        public (int f, (int x, int y) p) Pop()
        {
            var top = _a[0];
            var last = _a[_a.Count - 1];
            _a.RemoveAt(_a.Count - 1);
            if (_a.Count > 0)
            {
                _a[0] = last;
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _a.Count && _a[l].f < _a[m].f) m = l;
                    if (r < _a.Count && _a[r].f < _a[m].f) m = r;
                    if (m == i) break;
                    (_a[m], _a[i]) = (_a[i], _a[m]); i = m;
                }
            }
            return top;
        }
    }
}
