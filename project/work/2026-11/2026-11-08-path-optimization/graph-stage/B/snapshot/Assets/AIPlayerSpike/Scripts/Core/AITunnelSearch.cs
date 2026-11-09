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
        var job = new FindJob(start, target, lo, hi, cost, guardMax);
        while (!job.Step(int.MaxValue)) { }
        capped = job.Capped;
        return job.Result;
    }

    // Pure, resumable Dijkstra search. A work unit is one heap pop (at most four cost calls), one path
    // backtrack, or one reversal swap. The pop cap and tie order are independent of the caller's budget.
    // Call on one thread with stable callbacks/data for the lifetime of the job. Results are final only
    // when IsCompleted; nonpositive budgets do nothing. Timing excludes time between Step calls.
    public sealed class FindJob
    {
        private static readonly int[] Dx = { 1, -1, 0, 0 }, Dy = { 0, 0, 1, -1 };
        private readonly Dictionary<(int, int), int> _g = new Dictionary<(int, int), int>();
        private readonly Dictionary<(int, int), (int, int)> _came = new Dictionary<(int, int), (int, int)>();
        private readonly Heap _heap = new Heap();
        private readonly (int x, int y) _start, _target, _lo, _hi;
        private readonly Func<int, int, int> _cost;
        private readonly int _guardMax;
        private int _guard, _phase, _reverseIndex;
        private (int x, int y) _trace;
        private List<(int x, int y)> _path;
        private long _elapsedTicks;
        public bool IsCompleted => _phase == 3;
        public List<(int x, int y)> Result { get; private set; }
        public bool Capped { get; private set; }
        public int LastStepWork { get; private set; }
        public double ElapsedMilliseconds => _elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        public FindJob((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, int guardMax = 120000)
        {
            _start = start; _target = target; _lo = lo; _hi = hi; _cost = cost; _guardMax = guardMax;
            _g[start] = 0;
            _heap.Push(0, start);
        }

        public bool Step(int workBudget)
        {
            LastStepWork = 0;
            if (IsCompleted || workBudget <= 0) return IsCompleted;
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                while (!IsCompleted && LastStepWork < workBudget)
                {
                    LastStepWork++;
                    if (_phase == 1)
                    {
                        if (_trace != _start) { _path.Add(_trace); _trace = _came[_trace]; }
                        else _phase = 2;
                        continue;
                    }
                    if (_phase == 2)
                    {
                        int other = _path.Count - 1 - _reverseIndex;
                        if (_reverseIndex < other)
                        {
                            var tile = _path[_reverseIndex]; _path[_reverseIndex++] = _path[other]; _path[other] = tile;
                        }
                        else { Result = _path; _phase = 3; }
                        continue;
                    }
                    if (_heap.Count == 0) { _phase = 3; continue; }
                    if (_guard++ >= _guardMax) { Capped = true; _phase = 3; continue; }
                    var (f, c) = _heap.Pop();
                    if (_g.TryGetValue(c, out int cc) && f > cc) continue;
                    if (c == _target) { _path = new List<(int x, int y)>(); _trace = _target; _phase = 1; continue; }
                    for (int d = 0; d < 4; d++)
                    {
                        long nx = (long)c.Item1 + Dx[d], ny = (long)c.Item2 + Dy[d];
                        if (nx < int.MinValue || nx > int.MaxValue || ny < int.MinValue || ny > int.MaxValue) continue;
                        var n = ((int)nx, (int)ny);
                        if (n.Item1 < _lo.x || n.Item2 < _lo.y || n.Item1 > _hi.x || n.Item2 > _hi.y) continue;
                        int k = _cost(n.Item1, n.Item2);
                        if (k < 0) continue;
                        int ng = f + k;
                        if (_g.TryGetValue(n, out int og) && og <= ng) continue;
                        _g[n] = ng;
                        _came[n] = c;
                        _heap.Push(ng, n);
                    }
                }
            }
            finally { _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - began; }
            return IsCompleted;
        }
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
    public const float HitTiles = 1.2f;   // legacy route-selection weight; displayed seconds are separate (AIMoveEstimate)
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
        var job = new FindWithDetourJob(start, target, lo, hi, cost, opensEdge, hitsOf, detourCheck, guardMax);
        while (!job.Step(int.MaxValue)) { }
        usedEdge = job.UsedEdge; edgeClosed = job.EdgeClosed; detour = job.Detour;
        firstCapped = job.FirstCapped; allCapped = job.AllCapped; firstEst = job.FirstEst; detourEst = job.DetourEst;
        passes = job.Passes; ms1 = job.Ms1; ms2 = job.Ms2;
        return job.Result;
    }

    // The two passes share one budget, including hit estimates. Edge tests remain memoized for the whole
    // first pass; the target still bypasses the edge test. Search times include active search/reconstruction
    // only, not frame waits or hit estimates, matching the scope of the synchronous timing measurements.
    public sealed class FindWithDetourJob
    {
        private readonly (int x, int y) _start, _target, _lo, _hi;
        private readonly Func<int, int, int> _cost, _hitsOf;
        private readonly Func<int, int, bool> _opensEdge;
        private readonly bool _detourCheck;
        private readonly int _guardMax;
        private readonly HashSet<(int, int)> _closed = new HashSet<(int, int)>();
        private readonly Dictionary<(int, int), bool> _edgeMemo = new Dictionary<(int, int), bool>();
        private readonly FindJob _firstJob;
        private FindJob _secondJob;
        private List<(int x, int y)> _first, _second;
        private int _phase, _hitIndex, _hits;
        public bool IsCompleted => _phase == 7;
        public List<(int x, int y)> Result { get; private set; }
        public int LastStepWork { get; private set; }
        public bool UsedEdge { get; private set; }
        public int EdgeClosed => _closed.Count;
        public List<(int x, int y)> Detour { get; private set; }
        public bool FirstCapped => _firstJob.Capped;
        public bool AllCapped { get; private set; }
        public float FirstEst { get; private set; } = -1f;
        public float DetourEst { get; private set; } = -1f;
        public int Passes { get; private set; } = 1;
        public double Ms1 => _firstJob.ElapsedMilliseconds;
        public double Ms2 => _secondJob == null ? 0 : _secondJob.ElapsedMilliseconds;

        public FindWithDetourJob((int x, int y) start, (int x, int y) target, (int x, int y) lo, (int x, int y) hi, Func<int, int, int> cost, Func<int, int, bool> opensEdge, Func<int, int, int> hitsOf, bool detourCheck, int guardMax = 120000)
        {
            _start = start; _target = target; _lo = lo; _hi = hi; _cost = cost; _opensEdge = opensEdge;
            _hitsOf = hitsOf; _detourCheck = detourCheck; _guardMax = guardMax;
            _firstJob = new FindJob(start, target, lo, hi, ClosedCost, guardMax);
        }

        private int ClosedCost(int x, int y)
        {
            int k = _cost(x, y);
            if (k >= 0 && (x, y) != _target)
            {
                if (!_edgeMemo.TryGetValue((x, y), out bool opens)) _edgeMemo[(x, y)] = opens = _opensEdge(x, y);
                if (opens) { _closed.Add((x, y)); return -1; }
            }
            return k;
        }

        private bool HitStep(List<(int x, int y)> route, bool skipLast = false)
        {
            if (route == null || _hitsOf == null || _hitIndex >= route.Count - (skipLast ? 1 : 0)) return true;
            var tile = route[_hitIndex++];
            _hits += Math.Max(0, _hitsOf(tile.x, tile.y));
            return false;
        }

        private void StartSecond()
        {
            _secondJob = new FindJob(_start, _target, _lo, _hi, _cost, _guardMax);
            Passes = 2; _phase = 4;
        }

        private void Finish(List<(int x, int y)> result) { Result = result; _phase = 7; }

        public bool Step(int workBudget)
        {
            LastStepWork = 0;
            while (!IsCompleted && LastStepWork < workBudget)
            {
                if (_phase == 0 || _phase == 4)
                {
                    var search = _phase == 0 ? _firstJob : _secondJob;
                    search.Step(workBudget - LastStepWork);
                    LastStepWork += search.LastStepWork;
                    if (!search.IsCompleted) break;
                    if (_phase == 0) { _first = search.Result; _phase = 1; }
                    else { _second = search.Result; _phase = 5; }
                    _hitIndex = 0; _hits = 0;
                    continue;
                }
                LastStepWork++;
                switch (_phase)
                {
                    case 1:
                        if (HitStep(_first))
                        {
                            if (_first != null) FirstEst = EstTiles(_first.Count, _hits);
                            _phase = 2;
                        }
                        break;
                    case 2:
                        if (_closed.Count == 0) { AllCapped = _first == null && FirstCapped; Finish(_first); }
                        else if (_first == null) StartSecond();
                        else if (!_detourCheck) Finish(_first);
                        else { _hitIndex = 0; _hits = 0; _phase = 3; }
                        break;
                    case 3:
                        if (HitStep(_first, true))
                        {
                            if (NeedsDetourCheck(_first.Count, _hits > 0, _closed.Count)) StartSecond();
                            else Finish(_first);
                        }
                        break;
                    case 5:
                        if (HitStep(_second))
                        {
                            if (_second != null) DetourEst = EstTiles(_second.Count, _hits);
                            _phase = 6;
                        }
                        break;
                    case 6:
                        if (_first == null)
                        {
                            UsedEdge = _second != null;
                            AllCapped = _second == null && FirstCapped && _secondJob.Capped;
                            Finish(_second);
                        }
                        else
                        {
                            if (_second != null && OfferDetour(FirstEst, DetourEst)) Detour = _second;
                            Finish(_first);
                        }
                        break;
                }
            }
            return IsCompleted;
        }
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
