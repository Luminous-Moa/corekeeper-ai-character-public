// AIBaseEdgeCore — the base's edge (코어-안전지대 단계 2): which walkable tiles to close so that a space around the
// base is shut off from the rest, as a few nested options with facts; the batches that close them; Unity-free
// (core-tests EdgeTests). Agreed rules (8-단계2-구현.md, 9-단계2-검수반영.md):
// - No radius: the edge follows the ground. The "inside" of a cut is the walk-distance-d ball around the base seed;
//   the "outside" is whatever lies beyond: an unknown tile, the edge of the looked-at window, or (review G1) a tile at
//   walk distance >= d + M — a known space far from the base is outside too, so a seen and closed cave behind a
//   corridor does not swallow the base (enemies spawn in dark places whether seen or not). For each margin M of
//   MarginSteps and each d the cut is a minimum vertex cut (max flow: an open tile costs one block; the spawn pad and
//   immune tiles cannot be cut). Distinct cuts, from the room the AI is in to the larger spaces, are offered nested
//   (no option hides another), at most MaxOptions spread over the areas, with facts so the model chooses (decision '가').
// - What a base must hold is the model's call (user decision 2026-11-05): no minimum area is enforced; each option says
//   what stays outside it (the core, the spawn pad and respawn point, stations, chests, statues ...: EdgeGrid.landmarks)
//   and how many inside tiles can still take an object. "Around" computes the edge that holds given things (objects,
//   tiles, an area): the same cut with those as the inside. More than MaxBlocksOffered blocks is a computing limit:
//   when a larger cut was left out the listing says so.
// - 4-way walking only: tiles that touch only diagonally do not connect (a diagonal gap between two blocks is shut).
// - Water is crossed by some enemies: it counts as open for the edge and can be closed by a block that goes on water.
// - A gap = the cut tiles of one opening (8-connected). Closing works in batches of at most MaxBatch blocks; a longer
//   gap is split into runs of at most MaxBatch along it. The tile that closes a gap — decided at the moment of placing
//   as the last open tile of its gap (IsLastOpen, review G2) — is placed from inside, so the AI does not shut itself
//   out. The door tile of a gap (if chosen) stays open until the door, which is placed from inside too (G3).
using System;
using System.Collections.Generic;

public sealed class EdgeGrid
{
    public int n, loX, loY;      // window: (loX + i, loY + j), index i * n + j
    public bool[] pass;          // known, walkable, no object (a placed/natural object is a wall for the edge; another
                                 // player's open door is walkable: enemies pass it too, review G13)
    public bool[] water;         // known water (not lava): open for the edge, closable by a block that goes on water
    public bool[] known;         // seen at least once (unknown tiles are treated as open: they may lead anywhere)
    public bool[] noPlace;       // tiles the game never takes (spawn pad, immune): a cut holding one cannot be closed
    public bool[] wall;          // a wall tile or a fence now (a door's side, rule door-between-walls; objects are not
                                 // walls until measured, 단계 3 review F5). null: any known tile that is not open counts
    public bool[] myDoor;        // 재실측 (결정 B): a door the AI remembers placing, not yet edge memory — a cut through it
                                 // costs no block (it is taken as the edge's door) (null: none)
    public int seedX, seedY;     // the base (inside)
    public int windowRange;      // tiles from the base to the window's edge (for the facts)
    // things whose side of the edge the facts tell (name, the tiles it occupies)
    public readonly List<(string name, List<(int x, int y)> tiles)> landmarks = new List<(string, List<(int, int)>)>();
    public static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    public bool In(int x, int y) => x >= loX && y >= loY && x < loX + n && y < loY + n;
    public int Idx(int x, int y) => (x - loX) * n + (y - loY);
    public bool Open(int i) => pass[i] || (water != null && water[i]);
}

public sealed class EdgeGap
{
    public readonly List<(int x, int y)> tiles = new List<(int, int)>();   // ordered along the opening
    public int waterTiles;
}

public sealed class EdgeOption
{
    public int d, margin;                             // the inside ball's walk distance, the outside margin (0 = unknown/window only)
    public readonly List<EdgeGap> gaps = new List<EdgeGap>();
    public readonly HashSet<long> inside = new HashSet<long>();
    public int Blocks { get { int b = 0; foreach (var g in gaps) b += g.tiles.Count; return b; } }
    public int WaterTiles { get { int w = 0; foreach (var g in gaps) w += g.waterTiles; return w; } }
    public int unknownTouch;                          // cut tiles next to a tile never seen (what lies beyond is unseen)
    public int windowTouch;                           // cut tiles at the edge of the looked-at window
    public int knownBeyond;                           // cut tiles whose outside is known ground (a seen space beyond the edge)
    public int placeableInside;                       // inside tiles that can still take an object (walkable, not the spawn pad/immune)
    public int myDoorCut;                             // 재실측: cut tiles that are a door the AI placed (taken as the edge's door: no block)
    public int BlocksNeeded => Math.Max(0, Blocks - myDoorCut);
    public readonly List<string> outside = new List<string>();    // landmarks left outside the edge
    public readonly List<string> onEdge = new List<string>();     // landmarks the edge runs along (inside on one side, outside on another)
    public bool around;                               // computed to hold given things ("around")
    // per gap: the door spot (단계 2 live run: a door goes only between two walls) and how many more walls its sides need
    public readonly List<((int x, int y) tile, int extraWalls)> doorSpots = new List<((int, int), int)>();
    public string Facts(int haveBlocks, int haveWaterBlocks, int windowRange = 30)
    {
        int b = Blocks;
        var s = new List<string>();
        if (b == 0) s.Add($"이미 막혀 있음(틈 0) — 안쪽 전체가 본 적 있는 닫힌 공간 {inside.Count}칸(그 안 어두운 곳에도 적이 생길 수 있음)");
        else s.Add($"틈 {gaps.Count}곳, 막을 칸 {b}칸(가장 긴 틈 {MaxGap()}칸)");
        // 재실측 (결정 B): a cut tile that is my door needs no block (it is taken as the edge's door)
        int need = BlocksNeeded;
        string mine = myDoorCut > 0 ? $"(틈의 내 문 {myDoorCut}칸은 그대로 둘레 문)" : "";
        if (b > 0 && need > 0) s.Add($"블록 {need}개 필요{mine}, 가진 블록 {haveBlocks}개{(need > haveBlocks ? $" — {need - haveBlocks}개 모자람" : "")}");
        else if (b > 0) s.Add($"더 놓을 블록 없음{mine}");
        if (WaterTiles > 0) s.Add($"그중 물 칸 {WaterTiles}칸은 물 위에 놓이는 블록만(가진 것 {haveWaterBlocks}개)");
        if (b > 0) s.Add($"둘러싸이는 넓이 {inside.Count}칸");
        s.Add($"안쪽에 놓을 수 있는 칸 {placeableInside}칸");
        if (outside.Count > 0) s.Add($"둘레 밖에 남는 것: {Names(outside)}");
        else if (around) s.Add("품을 것 모두 안쪽");
        if (onEdge.Count > 0) s.Add($"둘레에 걸친 것: {Names(onEdge)}");
        for (int i = 0; i < doorSpots.Count && i < gaps.Count; i++)
        {
            var ds = doorSpots[i];
            s.Add(ds.extraWalls == 0
                ? $"틈 {i + 1}의 문 자리 ({ds.tile.x},{ds.tile.y}): 양옆이 벽(이 둘레의 벽 포함)"
                : $"틈 {i + 1}의 문 자리 ({ds.tile.x},{ds.tile.y}): 문을 달려면 양옆에 벽 {ds.extraWalls}칸이 더 필요");
        }
        if (knownBeyond > 0) s.Add($"막을 칸 {knownBeyond}칸 너머는 본 적 있는 바깥 공간");
        if (unknownTouch > 0) s.Add($"막을 칸 {unknownTouch}칸은 가 보지 않은 곳과 맞닿음");
        if (windowTouch > 0) s.Add($"막을 칸 {windowTouch}칸은 보는 범위(기지에서 {windowRange}칸) 끝에 닿음");
        return string.Join(", ", s);
    }
    public int MaxGap() { int m = 0; foreach (var g in gaps) m = Math.Max(m, g.tiles.Count); return m; }
    private static string Names(List<string> l) => l.Count <= 6 ? string.Join(", ", l) : string.Join(", ", l.GetRange(0, 6)) + $" 외 {l.Count - 6}개";
}

public sealed class EdgeBatch
{
    public int gap;                                         // index into the option's gaps
    public readonly List<(int x, int y)> tiles = new List<(int, int)>();
    public readonly List<(int x, int y)> stands = new List<(int, int)>();   // an inside neighbour per tile (or the tile itself if none)
    public bool closesGap;                                  // this batch holds the gap's last open tiles (decided again when placing)
    // review F3 (2026-11-06): the batch holding the last tile of a gap whose door spot is not hung yet: after it only the
    // door spot is open (the way in). closeTile = that last gap tile (decision A: it may be placed from outside)
    public bool closesToDoor;
    public (int x, int y)? closeTile;
    public readonly List<(int x, int y)> doorSides = new List<(int, int)>();   // review F5: tiles off the plan walled so the door spot has walls on both sides
    public bool repair;                                     // 단계 3: a repair batch (gap = index of the broken opening, AIBaseGuard.RepairBatches)
    public readonly List<(int x, int y)> group = new List<(int, int)>();   // repair: every tile of its opening (the closing check)
}

public sealed class EdgeOptions
{
    public readonly List<EdgeOption> list = new List<EdgeOption>();
    public bool largerLeftOut;   // a cut needing more than MaxBlocksOffered blocks was left out
    public int cutsComputed;
}

public static class AIBaseEdgeCore
{
    public const int MaxBatch = 7;
    public const int MaxOptions = 4;
    public const int MaxBlocksOffered = 60;
    public static readonly int[] MarginSteps = { 6, 12, 0 };   // outside = walk distance >= d + M (0: unknown/window only)
    public const int LadderSteps = 3;   // open ground (no chokepoint): this many built-edge sizes besides the smallest

    private static readonly int[] Dx = { 1, -1, 0, 0 }, Dy = { 0, 0, 1, -1 };

    // walk distance from the seed over open tiles (4-way); -1 unreached
    public static int[] Dist(EdgeGrid g, List<(int x, int y)> sources = null)
    {
        int n = g.n;
        var dist = new int[n * n];
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;
        var q = new Queue<int>();
        if (sources == null) sources = new List<(int, int)> { (g.seedX, g.seedY) };
        foreach (var src in sources)
        {
            if (!g.In(src.x, src.y)) continue;
            int s0 = g.Idx(src.x, src.y);
            if (dist[s0] == 0) continue;
            dist[s0] = 0; q.Enqueue(s0);
        }
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = g.loX + i / n, y = g.loY + i % n;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!g.In(nx, ny)) continue;
                int j = g.Idx(nx, ny);
                if (dist[j] >= 0 || !g.Open(j)) continue;
                dist[j] = dist[i] + 1; q.Enqueue(j);
            }
        }
        return dist;
    }

    // 0: inside the window and known on all four sides; 1: next to an unknown tile; 2: at the window's edge
    private static int Beyond(EdgeGrid g, int x, int y)
    {
        int r = 0;
        for (int k = 0; k < 4; k++)
        {
            int nx = x + Dx[k], ny = y + Dy[k];
            if (!g.In(nx, ny)) return 2;
            if (!g.known[g.Idx(nx, ny)]) r = 1;
        }
        return r;
    }

    // Reused buffers of the max flow (one Options call computes many cuts on the same window).
    private sealed class Flow
    {
        public int[] head, prev, to, cap, nxt; public int edges; public bool[] side;
        public void Reset(int nodes)
        {
            if (head == null || head.Length < nodes) { head = new int[nodes]; prev = new int[nodes]; side = new bool[nodes]; }
            for (int i = 0; i < nodes; i++) head[i] = -1;
            edges = 0;
        }
        public void Edge(int a, int b, int c)
        {
            if (to == null || edges + 2 > to.Length) { int sz = Math.Max(1024, (to?.Length ?? 0) * 2); Array.Resize(ref to, sz); Array.Resize(ref cap, sz); Array.Resize(ref nxt, sz); }
            to[edges] = b; cap[edges] = c; nxt[edges] = head[a]; head[a] = edges++;
            to[edges] = a; cap[edges] = 0; nxt[edges] = head[b]; head[b] = edges++;
        }
    }
    [ThreadStatic] private static Flow _flow;

    // The cut for one d and margin: the fewest open tiles that separate every tile within walk distance d of the seed
    // from the outside (unknown, window edge, or walk distance >= d + margin when margin > 0). Null when the d-ball
    // itself touches the outside, or the cut needs more than MaxBlocksOffered blocks (tooMany then true).
    public static EdgeOption CutAt(EdgeGrid g, int[] dist, int d, int margin, out bool tooMany)
    {
        tooMany = false;
        int n = g.n, T = n * n;
        const int INF = 1 << 20;
        int far = margin > 0 ? d + margin : int.MaxValue;
        int S = 2 * T, K = 2 * T + 1;
        var f = _flow ?? (_flow = new Flow());
        f.Reset(2 * T + 2);
        for (int i = 0; i < T; i++)
        {
            int di = dist[i];
            if (di < 0 || di > far) continue;   // unreached, or beyond the outside layer (merged into the sink)
            int x = g.loX + i / n, y = g.loY + i % n;
            bool inner = di <= d;
            bool leak = Beyond(g, x, y) > 0;
            if (inner && leak) return null;     // the d-ball leaks itself
            if (di == far) { f.Edge(2 * i, K, INF); continue; }   // the outside layer: the sink
            // 재실측 (결정 B): my door on the way costs nothing to "close" (it is the door), so a cut through it comes first
            f.Edge(2 * i, 2 * i + 1, inner || (g.noPlace != null && g.noPlace[i]) ? INF : g.myDoor != null && g.myDoor[i] ? 0 : 1);
            if (inner) f.Edge(S, 2 * i, INF);
            if (leak) f.Edge(2 * i + 1, K, INF);
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!g.In(nx, ny)) continue;
                int j = g.Idx(nx, ny);
                if (dist[j] < 0 || dist[j] > far) continue;
                f.Edge(2 * i + 1, 2 * j, INF);
            }
        }
        int flow = 0;
        var q = new Queue<int>();
        int nodes = 2 * T + 2;
        while (true)
        {
            for (int i = 0; i < nodes; i++) f.prev[i] = -2;
            f.prev[S] = -1; q.Clear(); q.Enqueue(S);
            while (q.Count > 0 && f.prev[K] == -2)
            {
                int u = q.Dequeue();
                for (int e = f.head[u]; e >= 0; e = f.nxt[e])
                    if (f.cap[e] > 0 && f.prev[f.to[e]] == -2) { f.prev[f.to[e]] = e; q.Enqueue(f.to[e]); }
            }
            if (f.prev[K] == -2) break;
            int add = INF;
            for (int v = K; v != S; v = f.to[f.prev[v] ^ 1]) add = Math.Min(add, f.cap[f.prev[v]]);
            for (int v = K; v != S; v = f.to[f.prev[v] ^ 1]) { f.cap[f.prev[v]] -= add; f.cap[f.prev[v] ^ 1] += add; }
            flow += add;
            if (flow > MaxBlocksOffered) { tooMany = true; return null; }
        }
        for (int i = 0; i < nodes; i++) f.side[i] = false;
        q.Clear(); q.Enqueue(S); f.side[S] = true;
        while (q.Count > 0)
        {
            int u = q.Dequeue();
            for (int e = f.head[u]; e >= 0; e = f.nxt[e])
                if (f.cap[e] > 0 && !f.side[f.to[e]]) { f.side[f.to[e]] = true; q.Enqueue(f.to[e]); }
        }
        var opt = new EdgeOption { d = d, margin = margin };
        var cut = new List<(int x, int y)>();
        for (int i = 0; i < T; i++)
        {
            int di = dist[i];
            if (di < 0 || di >= far) continue;
            int x = g.loX + i / n, y = g.loY + i % n;
            if (f.side[2 * i] && !f.side[2 * i + 1])
            {
                cut.Add((x, y));
                if (g.myDoor != null && g.myDoor[i]) opt.myDoorCut++;
                int b = Beyond(g, x, y);
                if (b == 1) opt.unknownTouch++; else if (b == 2) opt.windowTouch++; else opt.knownBeyond++;
            }
            else if (f.side[2 * i + 1]) opt.inside.Add(EdgeGrid.Key(x, y));
        }
        foreach (var gap in Group(g, cut)) opt.gaps.Add(gap);
        return opt;
    }

    // the cut tiles grouped by 8-connectivity, each ordered along its run (from one end, by walk over the group)
    public static List<EdgeGap> Group(EdgeGrid g, List<(int x, int y)> cut)
    {
        var res = new List<EdgeGap>();
        var left = new HashSet<long>();
        foreach (var t in cut) left.Add(EdgeGrid.Key(t.x, t.y));
        foreach (var t0 in cut)
        {
            if (!left.Contains(EdgeGrid.Key(t0.x, t0.y))) continue;
            var members = new List<(int x, int y)>();
            var q = new Queue<(int x, int y)>(); q.Enqueue(t0); left.Remove(EdgeGrid.Key(t0.x, t0.y));
            while (q.Count > 0)
            {
                var c = q.Dequeue(); members.Add(c);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        long k = EdgeGrid.Key(c.x + dx, c.y + dy);
                        if (left.Remove(k)) q.Enqueue((c.x + dx, c.y + dy));
                    }
            }
            var start = members[0]; int farthest = -1;
            foreach (var m in members) { int dd = Math.Abs(m.x - members[0].x) + Math.Abs(m.y - members[0].y); if (dd > farthest) { farthest = dd; start = m; } }
            var gap = new EdgeGap();
            var rest = new List<(int x, int y)>(members); rest.Remove(start);
            var cur = start; gap.tiles.Add(cur);
            while (rest.Count > 0)
            {
                int bi = 0, bd = int.MaxValue;
                for (int i = 0; i < rest.Count; i++) { int dd = Math.Max(Math.Abs(rest[i].x - cur.x), Math.Abs(rest[i].y - cur.y)) * 10 + Math.Abs(rest[i].x - cur.x) + Math.Abs(rest[i].y - cur.y); if (dd < bd) { bd = dd; bi = i; } }
                cur = rest[bi]; rest.RemoveAt(bi); gap.tiles.Add(cur);
            }
            foreach (var t in gap.tiles) if (g.water != null && g.water[g.Idx(t.x, t.y)]) gap.waterTiles++;
            res.Add(gap);
        }
        return res;
    }

    // Which landmarks stay outside / lie on the edge, and how many inside tiles can take an object. A landmark (an
    // object: not walkable itself) is judged by the walkable tiles next to it: all inside -> inside; none -> outside;
    // both -> on the edge. A walkable landmark tile (the spawn pad) counts itself.
    public static void Annotate(EdgeGrid g, EdgeOption o)
    {
        o.outside.Clear(); o.onEdge.Clear(); o.placeableInside = 0; o.doorSpots.Clear();
        foreach (var gp in o.gaps) o.doorSpots.Add(DoorSpotOf(g, gp));
        foreach (var key in o.inside)
        {
            int x = (int)(key >> 32), y = (int)(uint)key;
            if (!g.In(x, y)) continue;
            int i = g.Idx(x, y);
            if (g.pass[i] && (g.noPlace == null || !g.noPlace[i])) o.placeableInside++;
        }
        foreach (var lm in g.landmarks)
        {
            int ins = 0, outs = 0;
            var own = new HashSet<long>();
            foreach (var t in lm.tiles) own.Add(EdgeGrid.Key(t.x, t.y));
            foreach (var t in lm.tiles)
            {
                if (g.In(t.x, t.y) && g.Open(g.Idx(t.x, t.y))) { if (o.inside.Contains(EdgeGrid.Key(t.x, t.y))) ins++; else outs++; continue; }
                for (int k = 0; k < 4; k++)
                {
                    int nx = t.x + Dx[k], ny = t.y + Dy[k];
                    if (own.Contains(EdgeGrid.Key(nx, ny)) || !g.In(nx, ny) || !g.Open(g.Idx(nx, ny))) continue;
                    if (o.inside.Contains(EdgeGrid.Key(nx, ny))) ins++; else outs++;
                }
            }
            if (ins == 0 && outs == 0) continue;
            if (ins == 0) o.outside.Add(lm.name); else if (outs > 0) o.onEdge.Add(lm.name);
        }
    }

    // The edge that holds given things (user decision 2026-11-05: what the base must hold is the model's call): the
    // walkable tiles next to the given objects and the given tiles/area are the inside; the same cut as Options for each
    // margin (outside = unknown, window edge, or that far from all of them). Distinct cuts, smaller first.
    public static EdgeOptions Around(EdgeGrid g, List<(int x, int y)> hold)
    {
        var res = new EdgeOptions();
        var src = new List<(int x, int y)>();
        foreach (var t in hold) if (g.In(t.x, t.y) && g.Open(g.Idx(t.x, t.y))) src.Add(t);
        if (src.Count == 0) return res;
        var dist = Dist(g, src);
        var seen = new HashSet<string>();
        foreach (int margin in MarginSteps)
        {
            var o = CutAt(g, dist, 0, margin, out bool tooMany);
            res.cutsComputed++;
            if (tooMany) res.largerLeftOut = true;
            if (o == null || !seen.Add(KeyOf(o))) continue;
            o.around = true;
            Annotate(g, o);
            res.list.Add(o);
        }
        res.list.Sort((a, b) => a.inside.Count.CompareTo(b.inside.Count));
        return res;
    }

    // The options shown: for every margin and d the cut; distinct cuts holding the seed area; per margin the cuts
    // whose block count is a local minimum over d (the chokepoints); then at most MaxOptions spread over the areas
    // (the smallest and the largest, then the fewest blocks), smaller areas first. Nested: no option is dropped for
    // being beaten by a larger one (a shut cave does not hide the corridor mouth of the room, review G1).
    public static EdgeOptions Options(EdgeGrid g)
    {
        var res = new EdgeOptions();
        var dist = Dist(g);
        int maxD = 0; foreach (var v in dist) maxD = Math.Max(maxD, v);
        var pool = new List<EdgeOption>();
        var ladder = new List<EdgeOption>();   // every closable cut of the first margin, for open ground sizes
        var seen = new HashSet<string>();
        foreach (int margin in MarginSteps)
        {
            var seq = new List<EdgeOption>();
            int overRun = 0;
            for (int d = 1; d <= maxD; d++)
            {
                var o = CutAt(g, dist, d, margin, out bool tooMany);
                res.cutsComputed++;
                if (tooMany) res.largerLeftOut = true;
                if (o == null && !tooMany) break;   // the d-ball leaks: no larger d can be closed either
                // review G9: two cuts in a row over the block limit: stop this margin (a larger space behind a wide
                // one rarely drops under the limit again; said in the facts as "larger left out")
                overRun = tooMany ? overRun + 1 : 0;
                if (overRun >= 2) break;
                seq.Add(o);
                if (o != null && o.Blocks == 0) break;   // everything around is shut
            }
            if (margin == MarginSteps[0]) foreach (var o in seq) if (o != null && o.Blocks > 0) ladder.Add(o);
            for (int i = 0; i < seq.Count; i++)
            {
                var o = seq[i];
                if (o == null) continue;
                int prev = i > 0 && seq[i - 1] != null ? seq[i - 1].BlocksNeeded : int.MaxValue;
                int next = i + 1 < seq.Count && seq[i + 1] != null ? seq[i + 1].BlocksNeeded : int.MaxValue;
                if (o.BlocksNeeded > prev || o.BlocksNeeded > next) continue;   // not a chokepoint around its d
                if (!seen.Add(KeyOf(o))) continue;
                pool.Add(o);
            }
        }
        // Open ground (user, HEAD 2026-11-05): with no or few natural walls the block count only grows with d, so there
        // is one chokepoint at most; built edges of a few sizes are offered too, as cuts of the same computation (a ring
        // of new blocks around the d-ball; on open ground a diamond: diagonal-only contacts do not let anyone through).
        if (ladder.Count > 0)
        {
            int want = LadderSteps;
            for (int k = 1; k <= want; k++)
            {
                var o = ladder[Math.Min(ladder.Count - 1, (int)Math.Round((ladder.Count - 1) * k / (double)want))];
                if (seen.Add(KeyOf(o))) pool.Add(o);
            }
        }
        pool.Sort((a, b) => a.inside.Count.CompareTo(b.inside.Count));
        // the same space told twice (the same block count and an area within ~15%: the same opening closed a few tiles
        // further along): keep the smaller one
        var byArea = new List<EdgeOption>();
        foreach (var o in pool)
        {
            int k = byArea.FindIndex(x => x.inside.Count == o.inside.Count || x.BlocksNeeded == o.BlocksNeeded && Math.Abs(x.inside.Count - o.inside.Count) <= Math.Max(4, x.inside.Count * 15 / 100));
            if (k < 0) byArea.Add(o); else if (o.BlocksNeeded < byArea[k].BlocksNeeded) byArea[k] = o;
        }
        var list = byArea;
        if (list.Count > MaxOptions)
        {
            var keep = new List<EdgeOption> { list[0], list[list.Count - 1] };
            var mid = list.GetRange(1, list.Count - 2);
            mid.Sort((a, b) => a.BlocksNeeded.CompareTo(b.BlocksNeeded));
            for (int i = 0; i < mid.Count && keep.Count < MaxOptions; i++) keep.Add(mid[i]);
            keep.Sort((a, b) => a.inside.Count.CompareTo(b.inside.Count));
            list = keep;
        }
        foreach (var o in list) Annotate(g, o);
        res.list.AddRange(list);
        return res;
    }

    private static string KeyOf(EdgeOption o)
    {
        var keys = new List<string>();
        foreach (var gp in o.gaps) foreach (var t in gp.tiles) keys.Add($"{t.x},{t.y}");
        keys.Sort();
        return o.Blocks == 0 ? $"shut:{o.inside.Count}" : string.Join(";", keys);
    }

    // The batches that close an option's gaps (the door tile, if any, left open): runs of at most MaxBatch along each
    // gap; the last run holds the gap's last open tiles. Stands are inside walkable neighbours (one walk-distance map
    // for all, review G9).
    // sideElsewhere (optional): door-side tiles another option already covers (a placed wall gone: a repair, 단계 3)
    public static List<EdgeBatch> Batches(EdgeGrid g, EdgeOption o, (int x, int y)? door, Func<(int x, int y), bool> sideElsewhere = null)
    {
        var res = new List<EdgeBatch>();
        var dist = Dist(g);
        for (int gi = 0; gi < o.gaps.Count; gi++)
        {
            var tiles = new List<(int x, int y)>(o.gaps[gi].tiles);
            bool hasDoor = door.HasValue && tiles.Remove(door.Value);
            // review F5: the door spot's side tiles that still need a wall (off the plan, e.g. a diagonal gap) are filled
            // in the same batches; without them the door can never go in
            var sides = new HashSet<long>();
            if (hasDoor)
                foreach (var t in DoorSides(g, o.gaps[gi], door.Value).place)
                    if (!tiles.Contains(t) && (sideElsewhere == null || !sideElsewhere(t))) { tiles.Add(t); sides.Add(EdgeGrid.Key(t.x, t.y)); }
            int lastGapTile = -1;   // review F3: the last tile of the gap itself (not a side wall off the plan)
            if (hasDoor) for (int i = tiles.Count - 1; i >= 0; i--) if (!sides.Contains(EdgeGrid.Key(tiles[i].x, tiles[i].y))) { lastGapTile = i; break; }
            for (int s = 0; s < tiles.Count; s += MaxBatch)
            {
                var b = new EdgeBatch { gap = gi };
                for (int i = s; i < Math.Min(tiles.Count, s + MaxBatch); i++) { b.tiles.Add(tiles[i]); if (sides.Contains(EdgeGrid.Key(tiles[i].x, tiles[i].y))) b.doorSides.Add(tiles[i]); }
                b.closesGap = !hasDoor && s + MaxBatch >= tiles.Count;
                if (lastGapTile >= s && lastGapTile < s + MaxBatch) { b.closesToDoor = true; b.closeTile = tiles[lastGapTile]; }
                foreach (var t in b.tiles) b.stands.Add(InsideStand(g, o, t, dist) ?? t);
                res.Add(b);
            }
        }
        return res;
    }

    // an inside walkable 4-neighbour of t (nearest to the seed by walk), or null
    public static (int x, int y)? InsideStand(EdgeGrid g, EdgeOption o, (int x, int y) t, int[] dist = null)
    {
        if (dist == null) dist = Dist(g);
        (int, int)? best = null; int bd = int.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            int nx = t.x + Dx[k], ny = t.y + Dy[k];
            if (!g.In(nx, ny) || !o.inside.Contains(EdgeGrid.Key(nx, ny))) continue;
            int j = g.Idx(nx, ny);
            if (!g.pass[j] || (g.noPlace != null && g.noPlace[j])) continue;
            int d = dist[j] < 0 ? int.MaxValue - 1 : dist[j];
            if (d < bd) { bd = d; best = (nx, ny); }
        }
        return best;
    }

    // User decision A: a walkable 4-neighbour of t that is not inside (the outside stand to close t from), nearest the
    // seed by walk is no matter here: the first walkable one in E, W, N, S order; null when none
    public static (int x, int y)? OutsideStand(EdgeGrid g, EdgeOption o, (int x, int y) t)
    {
        for (int k = 0; k < 4; k++)
        {
            int nx = t.x + Dx[k], ny = t.y + Dy[k];
            if (!g.In(nx, ny) || o.inside.Contains(EdgeGrid.Key(nx, ny))) continue;
            int j = g.Idx(nx, ny);
            if (!g.pass[j] || (g.noPlace != null && g.noPlace[j])) continue;
            return (nx, ny);
        }
        return null;
    }

    // User decision B (2026-11-05): a door the AI remembers placing ("my door") that stands on a gap tile of a new plan
    // is taken as the edge's door: no second door and no wall there. Returns (gap index, tile) for each.
    // Review S7 (2026-11-06): the door gap chosen (1-based, 0 none) when the plan's door is an adopted own door on another
    // gap: said, not ignored ("" when it is the chosen gap, or nothing was adopted)
    public static string AdoptGapNote(List<(int gap, (int x, int y) tile)> adopted, int doorGap, (int x, int y)? door)
    {
        if (adopted == null || adopted.Count == 0 || doorGap < 1 || !door.HasValue) return "";
        foreach (var a in adopted) if (a.gap == doorGap - 1) return "";
        int dg = -1; foreach (var a in adopted) if (a.tile == door.Value) dg = a.gap;
        if (dg < 0) return "";
        return $", 고른 문틈 {doorGap}에는 문 자리를 두지 않음 — 이미 놓은 내 문 ({door.Value.x},{door.Value.y})(틈 {dg + 1})을 둘레 문으로 씀, 틈 {doorGap}의 칸은 모두 벽으로 메움";
    }

    public static List<(int gap, (int x, int y) tile)> AdoptDoors(EdgeOption o, Func<(int x, int y), bool> myDoor)
    {
        var res = new List<(int, (int, int))>();
        if (myDoor == null) return res;
        for (int gi = 0; gi < o.gaps.Count; gi++)
            foreach (var t in o.gaps[gi].tiles) if (myDoor(t)) res.Add((gi, t));
        return res;
    }

    // 내문-판정 결함 3 (HEAD decision 2026-11-06): the old plan's edge doors when a new plan is chosen (AIEdge.Pick). One on
    // a gap tile of the new plan stays an edge door (keep). One off them is no edge door any more (no repair, no breach,
    // not the base's door): still standing as the AI knows it (stands: AIBaseGuard.OwnDoorStands) it is my door (toMine:
    // the walk passes it, it gets the close option, the reach map reads it as an own door), else it is forgotten (drop).
    // The old Pick kept them all as edge doors (제작대-놓기 실게임2: (-10,0) of edge 3 stayed one under edge 3').
    public static void RepickDoors(IEnumerable<(int x, int y)> oldEdgeDoors, EdgeOption plan, Func<(int x, int y), bool> stands,
        out List<(int x, int y)> keep, out List<(int x, int y)> toMine, out List<(int x, int y)> drop)
    {
        keep = new List<(int, int)>(); toMine = new List<(int, int)>(); drop = new List<(int, int)>();
        var gapTiles = new HashSet<(int, int)>();
        if (plan != null) foreach (var gp in plan.gaps) foreach (var t in gp.tiles) gapTiles.Add(t);
        if (oldEdgeDoors == null) return;
        foreach (var t in oldEdgeDoors)
        {
            if (gapTiles.Contains(t)) keep.Add(t);
            else if (stands != null && stands(t)) toMine.Add(t);
            else drop.Add(t);
        }
    }

    // A door tile per gap (단계 2 live run: the game takes a door only between two walls — on both sides along one axis;
    // a fence gate is taken to follow the same rule, not measured): the gap tile whose two opposite sides (east+west or
    // north+south) are walls now or walls of this plan (the gap's other tiles), with the fewest more walls needed; ties
    // go to the middle of the run. extraWalls = walkable side tiles outside the plan that would also need a wall.
    public static ((int x, int y) tile, int extraWalls) DoorSpotOf(EdgeGrid g, EdgeGap gap)
    {
        var own = OwnOf(gap);
        (int, int) best = gap.tiles[gap.tiles.Count / 2]; int bestNeed = int.MaxValue, bestMid = int.MaxValue;
        for (int k = 0; k < gap.tiles.Count; k++)
        {
            var t = gap.tiles[k];
            int ew = DoorSide(g, own, t.x + 1, t.y) + DoorSide(g, own, t.x - 1, t.y), ns = DoorSide(g, own, t.x, t.y + 1) + DoorSide(g, own, t.x, t.y - 1);
            int need = Math.Min(ew, ns);
            int mid = Math.Abs(k - gap.tiles.Count / 2);
            if (need < bestNeed || need == bestNeed && mid < bestMid) { bestNeed = need; bestMid = mid; best = t; }
        }
        return (best, bestNeed);
    }

    private static HashSet<long> OwnOf(EdgeGap gap)
    {
        var own = new HashSet<long>();
        foreach (var t in gap.tiles) own.Add(EdgeGrid.Key(t.x, t.y));
        return own;
    }

    // review F5: one reading of rule door-between-walls for the door spot (DoorSpotOf, DoorSides) and for placing the
    // door (SidesReady on the game side): a side is ready when it is a wall tile or a fence now (EdgeGrid.wall), or a
    // tile of this plan's gap (walled before the door). Objects are not walls until measured.
    private static int DoorSide(EdgeGrid g, HashSet<long> own, int x, int y)
    {
        if (own.Contains(EdgeGrid.Key(x, y))) return 0;                     // walled by this plan (placed before the door)
        if (!g.In(x, y)) return 1;
        int i = g.Idx(x, y);
        if (!g.known[i]) return 1;                                         // unseen: count it as needing a wall
        if (g.wall != null) return g.wall[i] ? 0 : 1;
        return g.Open(i) ? 1 : 0;                                          // (no wall layer given: anything not open)
    }

    // Recheck R2: a door's side tile is one of the two along its wall axis (sidesEastWest: east and west of the door),
    // never the passage tiles in front of and behind it
    public static bool IsDoorSide((int x, int y) t, (int x, int y) door, bool sidesEastWest)
        => sidesEastWest ? t.y == door.y && Math.Abs(t.x - door.x) == 1 : t.x == door.x && Math.Abs(t.y - door.y) == 1;

    public static bool SidesReady(Func<(int x, int y), bool> wall, (int x, int y) d)
        => wall((d.x + 1, d.y)) && wall((d.x - 1, d.y)) || wall((d.x, d.y + 1)) && wall((d.x, d.y - 1));

    public sealed class DoorSideNeed
    {
        public bool eastWest;                                                          // the axis with the fewest walls missing
        public readonly List<(int x, int y)> place = new List<(int, int)>();          // open tiles a wall block can go on
        public readonly List<(int x, int y)> cannot = new List<(int, int)>();         // not open, not a wall (object, pit, unseen)
        public int Need => place.Count + cannot.Count;
        // repairCovers (recheck R4): side tiles whose placed wall is gone are in a repair batch, not a fill batch (the same
        // test Batches(…, sideElsewhere) uses)
        public string Text(int dx, int dy, Func<(int x, int y), bool> repairCovers = null)
        {
            if (Need == 0) return "";
            string axis = eastWest ? "동서" : "남북";
            var fill = place.FindAll(t => repairCovers == null || !repairCovers(t)); var rep = place.FindAll(t => repairCovers != null && repairCovers(t));
            var parts = new List<string>();
            if (fill.Count > 0) parts.Add($"{string.Join(" ", fill.ConvertAll(t => $"({t.x},{t.y})"))}는 메우기 묶음에 들어 있음");
            if (rep.Count > 0) parts.Add($"{string.Join(" ", rep.ConvertAll(t => $"({t.x},{t.y})"))}는 문 옆 벽(놓은 벽·자연 벽)이 없어진 칸이라 보수 묶음에 들어 있음");
            string p = string.Join("; ", parts);
            string c = cannot.Count > 0 ? $"{string.Join(" ", cannot.ConvertAll(t => $"({t.x},{t.y})"))}는 열린 칸이 아니라(물체·구덩이·안 본 칸 등) 벽 블록을 놓을 수 없음" : "";
            return $"문 자리 ({dx},{dy}): 양옆 벽 대기({axis} {Need}칸 더 필요{(p != "" ? "; " + p : "")}{(c != "" ? "; " + c : "")})";
        }
    }

    // The side tiles of door spot d that still need a wall (the axis DoorSpotOf counted: the fewest missing).
    public static DoorSideNeed DoorSides(EdgeGrid g, EdgeGap gap, (int x, int y) d)
    {
        var own = OwnOf(gap);
        int ew = DoorSide(g, own, d.x + 1, d.y) + DoorSide(g, own, d.x - 1, d.y), ns = DoorSide(g, own, d.x, d.y + 1) + DoorSide(g, own, d.x, d.y - 1);
        var r = new DoorSideNeed { eastWest = ew <= ns };
        var two = r.eastWest ? new[] { (d.x + 1, d.y), (d.x - 1, d.y) } : new[] { (d.x, d.y + 1), (d.x, d.y - 1) };
        foreach (var (x, y) in two)
        {
            if (DoorSide(g, own, x, y) == 0) continue;
            bool open = g.In(x, y) && g.known[g.Idx(x, y)] && g.Open(g.Idx(x, y)) && (g.noPlace == null || !g.noPlace[g.Idx(x, y)]);
            (open ? r.place : r.cannot).Add((x, y));
        }
        return r;
    }

    public static (int x, int y) DoorTileOf(EdgeGrid g, EdgeGap gap) => DoorSpotOf(g, gap).tile;

    // Review G2/G3: is t the last open tile of its gap right now (the others walled, or the gap's door placed)? Then
    // placing it shuts the gap and it is placed from inside only. `open` says whether a gap tile is still open.
    public static bool IsLastOpen(List<(int x, int y)> gapTiles, (int x, int y) t, Func<(int x, int y), bool> open)
    {
        foreach (var u in gapTiles)
        {
            if (u.x == t.x && u.y == t.y) continue;
            if (open(u)) return false;
        }
        return true;
    }

    // Review G11: the block for a tile — on water only one that goes on water; on ground first one that does not (keeps
    // the water-capable blocks for the water tiles), then the most plentiful. -1 when none fits.
    public static int PickBlock(List<(int count, bool water)> blocks, bool waterTile)
    {
        int best = -1;
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (b.count <= 0 || (waterTile && !b.water)) continue;
            if (best < 0) { best = i; continue; }
            var c = blocks[best];
            bool better = !waterTile && (c.water && !b.water) || (c.water == b.water || waterTile) && b.count > c.count;
            if (!waterTile && !c.water && b.water) better = false;
            if (better) best = i;
        }
        return best;
    }
}
