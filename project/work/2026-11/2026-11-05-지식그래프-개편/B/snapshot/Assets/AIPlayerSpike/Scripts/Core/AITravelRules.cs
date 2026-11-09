using System;
using System.Collections.Generic;

// Topology only, from observed geometry. These functions never select a branch on the model's behalf.
public static class AITravelRules
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is Cell c && Equals(c);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => X + "," + Y;
    }
    public static readonly Cell[] Directions = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };
    public static Cell Add(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
    public static string Direction(Cell a, Cell b) => b.X > a.X ? "동" : b.X < a.X ? "서" : b.Y > a.Y ? "북" : "남";
    // the same step as an index into Directions (0 E, 1 W, 2 N, 3 S)
    public static int DirIndex(Cell a, Cell b) => b.X > a.X ? 0 : b.X < a.X ? 1 : b.Y > a.Y ? 2 : 3;
    public static List<Cell> Exits(Cell at, Cell? previous, Func<Cell, bool> walkable)
    {
        var exits = new List<Cell>();
        foreach (var d in Directions)
        {
            if ((d.X > 0 && at.X == int.MaxValue) || (d.X < 0 && at.X == int.MinValue) ||
                (d.Y > 0 && at.Y == int.MaxValue) || (d.Y < 0 && at.Y == int.MinValue)) continue;
            var n = Add(at, d);
            if ((!previous.HasValue || !n.Equals(previous.Value)) && walkable(n)) exits.Add(n);
        }
        return exits;
    }
    // Finite observed map, no distance or route-length ceiling. Unknown geometry is not an edge.
    public static List<Cell> KnownPath(Cell start, Cell goal, IDictionary<Cell, bool> observed, Func<Cell, bool> allowed = null, float tolerance = 0f)
    {
        var queue = new Queue<Cell>();
        var previous = new Dictionary<Cell, Cell>();
        var seen = new HashSet<Cell> { start };
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var at = queue.Dequeue();
            if (Approach(at, goal, tolerance, observed, allowed))
            {
                var path = new List<Cell>();
                while (!at.Equals(start)) { path.Add(at); at = previous[at]; }
                path.Reverse(); return path;
            }
            foreach (var n in Exits(at, null, p => observed.TryGetValue(p, out bool open) && open && (allowed == null || allowed(p))))
                if (seen.Add(n)) { previous[n] = at; queue.Enqueue(n); }
        }
        return null;
    }
    // A blocked interaction target is never made walkable. Stop at an observed open
    // approach centre inside the caller's real interaction tolerance. Both cells at
    // a diagonal crossing must be open, so tolerance cannot reach through a wall.
    public static bool Approach(Cell at, Cell goal, float tolerance, IDictionary<Cell, bool> map, Func<Cell, bool> allowed)
    {
        if (at.Equals(goal)) return map.TryGetValue(at, out bool sameOpen) && sameOpen && (allowed == null || allowed(at));
        double dx = (double)goal.X - at.X, dy = (double)goal.Y - at.Y;
        if (tolerance <= 0f || dx * dx + dy * dy > tolerance * tolerance || !map.ContainsKey(goal)) return false;
        if (!map.TryGetValue(at, out bool open) || !open || (allowed != null && !allowed(at))) return false;
        int nx = Math.Abs(goal.X - at.X), ny = Math.Abs(goal.Y - at.Y);
        int sx = Math.Sign(dx), sy = Math.Sign(dy), ix = 0, iy = 0;
        var p = at;
        Func<Cell, bool> clear = q => q.Equals(goal) || (map.TryGetValue(q, out bool w) && w && (allowed == null || allowed(q)));
        while (ix < nx || iy < ny)
        {
            long decision = (1L + 2L * ix) * ny - (1L + 2L * iy) * nx;
            if (decision == 0)
            {
                if (!clear(new Cell(p.X + sx, p.Y)) || !clear(new Cell(p.X, p.Y + sy))) return false;
                p = new Cell(p.X + sx, p.Y + sy); ix++; iy++;
            }
            else if (decision < 0) { p = new Cell(p.X + sx, p.Y); ix++; }
            else { p = new Cell(p.X, p.Y + sy); iy++; }
            if (!clear(p)) return false;
        }
        return true;
    }

}

// 공간·출구 단위 이동 (2026-11-08, area-exit-design.md): the observed map is split into areas (rooms, wide caves,
// wide corridors) and narrow connectors. Only observed cells are used; unknown cells are neither wall nor floor.
// Pure functions: the caller supplies the cell states. Nothing here selects an exit on the model's behalf.
public static class AIAreaRules
{
    // Thresholds — one place, to be tuned in live play (user decision 2026-11-08: start with the design values).
    public const int CoreClearance = 2;    // an area core cell has no blocked cell within 2 (width 5+)
    public const int NeckClearance = 1;    // width 3 (and 4) is a neck/corridor: cores need more than this
    public const int MinAreaCells = 9;     // smaller wide spots are part of the corridor
    public const int WindowRadius = 48;    // analysis window around the character (97x97 cells)
    public const int PocketCells = 8;      // an unlabeled dead pocket this small touching one area is that area's shape
    public const int MergeContact = 4;     // two cores joined by an opening this wide (or wider) are one area
    public const int NicheCells = 2;       // a dead side arm of a corridor this short is a niche, not a way

    public enum ExitKind { Connector, Contact, Frontier }
    public sealed class AreaExit
    {
        public ExitKind Kind;
        public AITravelRules.Cell Gate;    // the cell to walk to (connector/next-area cell, or the area's frontier cell)
        public readonly List<AITravelRules.Cell> Mouth = new List<AITravelRules.Cell>();   // every gate cell of this exit
        // 최종 검수 G3: the step out of the area at each Mouth cell (AITravelRules.Directions index: 0 E, 1 W, 2 N, 3 S);
        // an exit record is keyed by gate cell + this direction, so the way back through the same cell is another record
        public readonly List<int> MouthOut = new List<int>();
        public int GateOut;
        internal readonly HashSet<AITravelRules.Cell> MouthSet = new HashSet<AITravelRules.Cell>();
        public int Sector;                 // 0..7 from the area centre (E, NE, N, NW, W, SW, S, SE)
        public int ToArea = -1;            // a connector/contact leading into another area (local index)
        public bool Unexplored;            // leads to unknown cells
        public bool DeadEnd;               // a connector that ends with no other area and no unknown
        public bool Loop;                  // a connector that comes back into this same area at another mouth
        public int Connector = -1;
        public bool Has(AITravelRules.Cell c) => Gate.Equals(c) || MouthSet.Contains(c);
        public bool Near(AITravelRules.Cell c, int r)
        {
            for (int dx = -r; dx <= r; dx++) for (int dy = -r + Math.Abs(dx); dy <= r - Math.Abs(dx); dy++) if (MouthSet.Contains(new AITravelRules.Cell(c.X + dx, c.Y + dy))) return true;
            return false;
        }
    }
    public sealed class Area
    {
        public int Index;
        internal Map Owner;
        public int Count;                   // cells
        public bool Contains(AITravelRules.Cell c) => Owner != null && Owner.AreaIndex(c) == Index;
        public bool IsCore(AITravelRules.Cell c) => Contains(c) && Owner.CoreAt(c);
        internal readonly Dictionary<long, List<AreaExit>> ExitIndex = new Dictionary<long, List<AreaExit>>();
        public AITravelRules.Cell Centre;   // the core cell nearest the centroid
        public bool Junction;               // a corridor junction (small area)
        public readonly List<AreaExit> Exits = new List<AreaExit>();
    }
    // Where a connector meets an area: Inside is the connector cell, Outside the area cell next to it.
    public struct Opening { public AITravelRules.Cell Inside, Outside; public int Area; }
    public sealed class Connector
    {
        public int Index;
        public readonly HashSet<AITravelRules.Cell> Cells = new HashSet<AITravelRules.Cell>();
        public readonly HashSet<int> Areas = new HashSet<int>();
        public readonly List<Opening> Openings = new List<Opening>();
        public readonly List<AITravelRules.Cell> Edge = new List<AITravelRules.Cell>();   // cells next to unknown (or the window edge)
        public bool Unknown => Edge.Count > 0;
    }
    public sealed class Map
    {
        public readonly List<Area> Areas = new List<Area>();
        public readonly List<Connector> Connectors = new List<Connector>();
        public int Cells;
        internal int Ox, Oy, W; internal int[] AreaGrid, ConnectorGrid; internal bool[] CoreGrid;
        private int Idx(AITravelRules.Cell c) { int x = c.X - Ox, y = c.Y - Oy; return W == 0 || x < 0 || y < 0 || x >= W || y >= W ? -1 : y * W + x; }
        public int AreaIndex(AITravelRules.Cell c) { int i = Idx(c); return i < 0 || AreaGrid == null ? -1 : AreaGrid[i]; }
        public int ConnectorIndex(AITravelRules.Cell c) { int i = Idx(c); return i < 0 || ConnectorGrid == null ? -1 : ConnectorGrid[i]; }
        public Area AreaAt(AITravelRules.Cell c) { int i = AreaIndex(c); return i < 0 ? null : Areas[i]; }
        public Connector ConnectorAt(AITravelRules.Cell c) { int i = ConnectorIndex(c); return i < 0 ? null : Connectors[i]; }
        public bool CoreAt(AITravelRules.Cell c) { int i = Idx(c); return i >= 0 && CoreGrid != null && CoreGrid[i]; }
    }
    public static int SectorOf(AITravelRules.Cell from, AITravelRules.Cell to)
    {
        double a = Math.Atan2(to.Y - from.Y, to.X - from.X);
        int s = (int)Math.Round(a / (Math.PI / 4)); return ((s % 8) + 8) % 8;
    }
    public static int SectorGap(int a, int b) { int d = Math.Abs(a - b) % 8; return Math.Min(d, 8 - d); }
    public static readonly string[] SectorNames = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };

    // state(cell): true walkable, false blocked, null unknown. The window is centred on 'centre'.
    public static Map Analyze(AITravelRules.Cell centre, Func<AITravelRules.Cell, bool?> state, int radius = WindowRadius)
    {
        int W = radius * 2 + 1, ox = centre.X - radius, oy = centre.Y - radius;
        var st = new sbyte[W * W];
        for (int iy = 0; iy < W; iy++) for (int ix = 0; ix < W; ix++) { bool? v = state(new AITravelRules.Cell(ox + ix, oy + iy)); st[iy * W + ix] = v == true ? (sbyte)1 : v == false ? (sbyte)0 : (sbyte)-1; }
        return Analyze(centre, st, radius);
    }
    // The observed map kept in 32x32 blocks: the analysis window is copied block by block (in play the map is large).
    public static Map Analyze(AITravelRules.Cell centre, ObservedGrid grid, int radius = WindowRadius)
    {
        int W = radius * 2 + 1;
        var st = new sbyte[W * W];
        grid.Fill(centre.X - radius, centre.Y - radius, W, st);
        return Analyze(centre, st, radius);
    }
    // st: 1 walkable, 0 blocked, -1 unknown, row-major over the (2*radius+1)^2 window centred on 'centre'.
    private static Map Analyze(AITravelRules.Cell centre, sbyte[] st, int radius)
    {
        var map = new Map();
        int W = radius * 2 + 1, N = W * W, ox = centre.X - radius, oy = centre.Y - radius;
        map.Ox = ox; map.Oy = oy; map.W = W;
        int C0 = radius * W + radius;
        if (st[C0] != 1) return map;
        AITravelRules.Cell CellOf(int i) => new AITravelRules.Cell(ox + i % W, oy + i / W);
        var nb = new int[4];
        int Nb(int i) { int x = i % W, y = i / W, n = 0; if (x + 1 < W) nb[n++] = i + 1; if (x > 0) nb[n++] = i - 1; if (y + 1 < W) nb[n++] = i + W; if (y > 0) nb[n++] = i - W; return n; }
        bool UnknownNear(int i) { int x = i % W, y = i / W; if (x == 0 || y == 0 || x == W - 1 || y == W - 1) return true; return st[i + 1] == -1 || st[i - 1] == -1 || st[i + W] == -1 || st[i - W] == -1; }
        // 1. the walkable region connected to the character
        var walk = new bool[N]; var queue = new int[N]; int head = 0, tail = 0; var cells = new List<int>();
        walk[C0] = true; queue[tail++] = C0;
        while (head < tail) { int c = queue[head++]; cells.Add(c); int n = Nb(c); for (int k = 0; k < n; k++) { int m = nb[k]; if (!walk[m] && st[m] == 1) { walk[m] = true; queue[tail++] = m; } } }
        map.Cells = cells.Count;
        // 2. clearance: steps to the nearest blocked cell, minus one (unknown is not blocked)
        var clear = new int[N]; for (int i = 0; i < N; i++) clear[i] = int.MaxValue / 4;
        head = tail = 0;
        foreach (int c in cells) { int n = Nb(c); for (int k = 0; k < n; k++) if (st[nb[k]] == 0) { clear[c] = 0; queue[tail++] = c; break; } }
        while (head < tail) { int c = queue[head++]; int n = Nb(c); for (int k = 0; k < n; k++) { int m = nb[k]; if (walk[m] && clear[m] > clear[c] + 1) { clear[m] = clear[c] + 1; queue[tail++] = m; } } }
        // 3. cores
        var core = new int[N]; for (int i = 0; i < N; i++) core[i] = -1; int cores = 0;
        foreach (int s in cells)
        {
            if (clear[s] < CoreClearance || core[s] >= 0) continue;
            head = tail = 0; core[s] = cores; queue[tail++] = s;
            while (head < tail) { int c = queue[head++]; int n = Nb(c); for (int k = 0; k < n; k++) { int m = nb[k]; if (walk[m] && core[m] < 0 && clear[m] >= CoreClearance) { core[m] = cores; queue[tail++] = m; } } }
            cores++;
        }
        // 4. each core claims the free space around it (budget = its clearance), largest budget first (bucket queue):
        // wall bumps and corners become the area's shape, not exits
        var label = new int[N]; var budget = new int[N]; for (int i = 0; i < N; i++) { label[i] = -1; budget[i] = -1; }
        int maxB = 0; foreach (int c in cells) if (core[c] >= 0) maxB = Math.Max(maxB, Math.Min(clear[c], W));
        var buckets = new List<int>[maxB + 1]; for (int b = 0; b <= maxB; b++) buckets[b] = new List<int>();
        foreach (int c in cells) if (core[c] >= 0) { int b = Math.Min(clear[c], W); label[c] = core[c]; budget[c] = b; buckets[b].Add(c); }
        for (int b = maxB; b > 0; b--)
            for (int j = 0; j < buckets[b].Count; j++)
            {
                int c = buckets[b][j]; if (budget[c] != b) continue;
                int n = Nb(c);
                for (int k = 0; k < n; k++) { int m = nb[k]; if (!walk[m] || budget[m] >= b - 1) continue; budget[m] = b - 1; label[m] = label[c]; buckets[b - 1].Add(m); }
            }
        // 5. cores joined by a wide opening are one area
        var parent = new int[cores]; for (int i = 0; i < cores; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        var contact = new Dictionary<long, int>();
        foreach (int c in cells)
        {
            if (label[c] < 0) continue; int n = Nb(c);
            for (int k = 0; k < n; k++) { int m = nb[k]; if (label[m] < 0 || label[m] == label[c]) continue; int a = Math.Min(label[c], label[m]), b2 = Math.Max(label[c], label[m]); long key = ((long)a << 32) | (uint)b2; contact.TryGetValue(key, out int cnt); contact[key] = cnt + 1; break; }
        }
        foreach (var kv in contact) if (kv.Value >= MergeContact * 2) parent[Find((int)(kv.Key >> 32))] = Find((int)(kv.Key & 0xffffffff));
        // 6. areas (index order by first cell in row-major order), small ones dissolved
        var areaOf = new int[N]; for (int i = 0; i < N; i++) areaOf[i] = -1;
        var size = new Dictionary<int, int>(); var first = new Dictionary<int, int>();
        foreach (int c in cells) if (label[c] >= 0) { int r = Find(label[c]); size.TryGetValue(r, out int z); size[r] = z + 1; if (!first.ContainsKey(r) || c < first[r]) first[r] = c; }
        var roots = new List<int>(size.Keys); roots.Sort((x, y) => first[x].CompareTo(first[y]));
        var rootArea = new Dictionary<int, int>();
        foreach (int r in roots) if (size[r] >= MinAreaCells) { rootArea[r] = map.Areas.Count; map.Areas.Add(new Area { Index = map.Areas.Count }); }
        foreach (int c in cells) if (label[c] >= 0 && rootArea.TryGetValue(Find(label[c]), out int a)) areaOf[c] = a;
        // 7. the rest: an area's own shape, corridor junctions (small areas) and connectors (flat arrays: no per-cell hashing)
        var connOf = new int[N]; var compOf = new int[N]; var openA = new int[N]; var pieceA = new int[N]; var clusterA = new int[N]; var uStamp = new int[N];
        for (int i = 0; i < N; i++) { connOf[i] = -1; compOf[i] = -1; pieceA[i] = -1; clusterA[i] = -1; uStamp[i] = -1; }
        var sortedCells = new List<int>(cells); sortedCells.Sort();
        int compCount = 0;
        var comp = new List<int>(); var touch = new Dictionary<int, int>();
        var pieceSize = new List<int>(); var pieceWay = new List<bool>(); var pieceCluster = new List<int>(); var pieceMulti = new List<bool>();
        var clusterStart = new List<int>(); var clusterCells = new List<int>();
        var areasNear = new List<int>(); var piecesNear = new List<int>();
        foreach (int s in sortedCells)
        {
            if (areaOf[s] >= 0 || compOf[s] >= 0) continue;
            int ci = compCount++; comp.Clear(); touch.Clear(); bool unknown = false;
            head = tail = 0; compOf[s] = ci; queue[tail++] = s;
            while (head < tail)
            {
                int c = queue[head++]; comp.Add(c); unknown |= UnknownNear(c); bool counted = false;
                int n = Nb(c);
                for (int k = 0; k < n; k++)
                {
                    int m = nb[k];
                    if (areaOf[m] >= 0) { if (!counted) { touch.TryGetValue(areaOf[m], out int t); touch[areaOf[m]] = t + 1; counted = true; } }
                    else if (walk[m] && compOf[m] < 0) { compOf[m] = ci; queue[tail++] = m; }
                }
            }
            // part of one area's shape: a small dead pocket, or a wide continuation (opening at least MergeContact)
            if (touch.Count == 1)
            {
                int only = -1, len = 0; foreach (var kv in touch) { only = kv.Key; len = kv.Value; }
                if ((!unknown && comp.Count <= PocketCells) || len >= MergeContact) { foreach (int c in comp) areaOf[c] = only; continue; }
            }
            comp.Sort();
            foreach (int c in comp) { int o = 0, n = Nb(c); for (int k = 0; k < n; k++) { int m = nb[k]; if (compOf[m] == ci || areaOf[m] >= 0) o++; } openA[c] = o; }
            // corridor pieces (cells with fewer than three ways) and branching clusters (three or more)
            pieceSize.Clear(); pieceWay.Clear(); pieceCluster.Clear(); pieceMulti.Clear();
            foreach (int c0 in comp)
            {
                if (openA[c0] >= 3 || pieceA[c0] >= 0) continue;
                int p = pieceSize.Count; int sz = 0; bool way = false;
                head = tail = 0; pieceA[c0] = p; queue[tail++] = c0;
                while (head < tail)
                {
                    int c = queue[head++]; sz++; way |= UnknownNear(c); int n = Nb(c);
                    for (int k = 0; k < n; k++) { int m = nb[k]; if (areaOf[m] >= 0) way = true; else if (compOf[m] == ci && openA[m] < 3 && pieceA[m] < 0) { pieceA[m] = p; queue[tail++] = m; } }
                }
                pieceSize.Add(sz); pieceWay.Add(way); pieceCluster.Add(-1); pieceMulti.Add(false);
            }
            clusterStart.Clear(); clusterCells.Clear();
            foreach (int c0 in comp)
            {
                if (openA[c0] < 3 || clusterA[c0] >= 0) continue;
                int q = clusterStart.Count; clusterStart.Add(clusterCells.Count);
                head = tail = 0; clusterA[c0] = q; queue[tail++] = c0;
                while (head < tail)
                {
                    int c = queue[head++]; clusterCells.Add(c); int n = Nb(c);
                    for (int k = 0; k < n; k++)
                    {
                        int m = nb[k];
                        if (compOf[m] == ci && openA[m] >= 3 && clusterA[m] < 0) { clusterA[m] = q; queue[tail++] = m; }
                        else if (compOf[m] == ci && pieceA[m] >= 0) { int pp = pieceA[m]; if (pieceCluster[pp] < 0) pieceCluster[pp] = q; else if (pieceCluster[pp] != q) pieceMulti[pp] = true; }
                    }
                }
            }
            bool Niche(int p) => !pieceWay[p] && pieceSize[p] <= NicheCells && !pieceMulti[p];
            for (int q = 0; q < clusterStart.Count; q++)
            {
                int from = clusterStart[q], to = q + 1 < clusterStart.Count ? clusterStart[q + 1] : clusterCells.Count;
                // ways out of this branching spot: neighbouring areas, corridor arms (not niches), separate unknown edges
                areasNear.Clear(); piecesNear.Clear(); int unknownGroups = 0;
                for (int j = from; j < to; j++)
                {
                    int c = clusterCells[j], n = Nb(c);
                    for (int k = 0; k < n; k++)
                    {
                        int m = nb[k];
                        if (areaOf[m] >= 0) { if (!areasNear.Contains(areaOf[m])) areasNear.Add(areaOf[m]); }
                        else if (compOf[m] == ci && pieceA[m] >= 0 && !piecesNear.Contains(pieceA[m])) piecesNear.Add(pieceA[m]);
                    }
                    if (!UnknownNear(c) || uStamp[c] == q) continue;
                    unknownGroups++; head = tail = 0; uStamp[c] = q; queue[tail++] = c;
                    while (head < tail) { int x = queue[head++]; int nn = Nb(x); for (int k = 0; k < nn; k++) { int m = nb[k]; if (compOf[m] == ci && clusterA[m] == q && uStamp[m] != q && UnknownNear(m)) { uStamp[m] = q; queue[tail++] = m; } } }
                }
                int leads = areasNear.Count + unknownGroups;
                foreach (int pp in piecesNear) if (!Niche(pp)) leads++;
                if (leads < 3) continue;
                var area = new Area { Index = map.Areas.Count, Junction = true }; map.Areas.Add(area);
                for (int j = from; j < to; j++) areaOf[clusterCells[j]] = area.Index;
                foreach (int pp in piecesNear) if (Niche(pp)) foreach (int c in comp) if (pieceA[c] == pp) areaOf[c] = area.Index;
            }
            // connectors: the comp cells not taken by a junction, as connected pieces
            foreach (int s2 in comp)
            {
                if (areaOf[s2] >= 0 || connOf[s2] >= 0) continue;
                var conn = new Connector { Index = map.Connectors.Count }; map.Connectors.Add(conn);
                head = tail = 0; connOf[s2] = conn.Index; queue[tail++] = s2;
                while (head < tail)
                {
                    int c = queue[head++]; var cc = CellOf(c); conn.Cells.Add(cc); if (UnknownNear(c)) conn.Edge.Add(cc);
                    int n = Nb(c);
                    for (int k = 0; k < n; k++)
                    {
                        int m = nb[k];
                        if (areaOf[m] >= 0) { conn.Areas.Add(areaOf[m]); conn.Openings.Add(new Opening { Inside = cc, Outside = CellOf(m), Area = areaOf[m] }); }
                        else if (compOf[m] == ci && connOf[m] < 0) { connOf[m] = conn.Index; queue[tail++] = m; }
                    }
                }
            }
        }
        // materialise areas (counts, core flags and the centre: the core cell nearest the centroid, row-major on ties)
        var coreGrid = new bool[N]; int AN = map.Areas.Count; var sumX = new long[AN]; var sumY = new long[AN]; var cellsOf = new int[AN];
        foreach (int c in sortedCells)
        {
            int a = areaOf[c]; if (a < 0) continue;
            cellsOf[a]++; sumX[a] += c % W; sumY[a] += c / W;
            if (core[c] >= 0 || map.Areas[a].Junction) coreGrid[c] = true;
        }
        map.AreaGrid = areaOf; map.ConnectorGrid = connOf; map.CoreGrid = coreGrid;
        var bestD = new int[AN]; var bestI = new int[AN]; var bestCore = new bool[AN];
        for (int a = 0; a < AN; a++) { bestD[a] = int.MaxValue; bestI[a] = -1; map.Areas[a].Owner = map; map.Areas[a].Count = cellsOf[a]; }
        foreach (int c in sortedCells)
        {
            int a = areaOf[c]; if (a < 0) continue;
            int d = (int)(Math.Abs(c % W - Math.Round((double)sumX[a] / cellsOf[a])) + Math.Abs(c / W - Math.Round((double)sumY[a] / cellsOf[a])));
            if (coreGrid[c] && !bestCore[a] || coreGrid[c] == bestCore[a] && d < bestD[a]) { bestD[a] = d; bestI[a] = c; bestCore[a] = coreGrid[c]; }
        }
        for (int a = 0; a < AN; a++) map.Areas[a].Centre = CellOf(bestI[a]);
        // 8. exits of each area: one per connector mouth, per neighbouring area and per frontier sector (user decision 5)
        foreach (int c in sortedCells)
        {
            int a = areaOf[c]; if (a < 0) continue; var area = map.Areas[a]; var cc = CellOf(c); int n = Nb(c);
            for (int k = 0; k < n; k++)
            {
                int m = nb[k]; int dir = m == c + 1 ? 0 : m == c - 1 ? 1 : m == c + W ? 2 : 3;
                if (connOf[m] >= 0) KeepExit(area, ExitKind.Connector, connOf[m], CellOf(m), dir);
                else if (areaOf[m] >= 0 && areaOf[m] != a) KeepExit(area, ExitKind.Contact, areaOf[m], CellOf(m), dir);
                else if (st[m] == -1) KeepExit(area, ExitKind.Frontier, SectorOf(area.Centre, cc), cc, dir);
            }
            if (n < 4)   // the window edge
            {
                int x = c % W, y = c / W;
                KeepExit(area, ExitKind.Frontier, SectorOf(area.Centre, cc), cc, x == W - 1 ? 0 : x == 0 ? 1 : y == W - 1 ? 2 : 3);
            }
        }
        foreach (var area in map.Areas)
        {
            foreach (var e in area.Exits)
            {
                if (e.Kind != ExitKind.Frontier) e.Sector = SectorOf(area.Centre, e.Gate);
                if (e.Kind == ExitKind.Connector)
                {
                    var conn = map.Connectors[e.Connector]; foreach (int o in conn.Areas) if (o != area.Index) { e.ToArea = o; break; }
                    e.Loop = area.ExitIndex[Key(ExitKind.Connector, e.Connector)].Count > 1;
                    e.Unexplored = conn.Unknown; e.DeadEnd = e.ToArea < 0 && !conn.Unknown && !e.Loop;
                }
            }
            area.Exits.Sort((x, y) => x.Sector != y.Sector ? x.Sector.CompareTo(y.Sector) : x.Kind != y.Kind ? x.Kind.CompareTo(y.Kind) : Order(x.Gate, y.Gate));
        }
        return map;
    }
    private static int Order(AITravelRules.Cell a, AITravelRules.Cell b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
    // The gate of an exit is its cell nearest the area centre; every candidate cell is kept in Mouth.
    private static long Key(ExitKind kind, int key) => ((long)kind << 40) | (uint)key;
    private static void KeepExit(Area area, ExitKind kind, int key, AITravelRules.Cell gate, int dir)
    {
        long k = Key(kind, key);
        if (!area.ExitIndex.TryGetValue(k, out var list)) area.ExitIndex[k] = list = new List<AreaExit>();
        foreach (var e in list)
        {
            // one connector meeting this area at two separate mouths (a corridor looping back) is two exits
            if (kind == ExitKind.Connector && !e.Near(gate, 2)) continue;
            if (!e.MouthSet.Add(gate)) return;
            e.Mouth.Add(gate); e.MouthOut.Add(dir);
            int d = Dist(gate, area.Centre), old = Dist(e.Gate, area.Centre);
            if (d < old || d == old && Order(gate, e.Gate) < 0) { e.Gate = gate; e.GateOut = dir; }
            return;
        }
        var exit = new AreaExit { Kind = kind, Gate = gate, GateOut = dir, Connector = kind == ExitKind.Connector ? key : -1, ToArea = kind == ExitKind.Contact ? key : -1, Sector = kind == ExitKind.Frontier ? key : 0, Unexplored = kind == ExitKind.Frontier };
        exit.Mouth.Add(gate); exit.MouthOut.Add(dir); exit.MouthSet.Add(gate); area.Exits.Add(exit); list.Add(exit);
    }
    public static int Dist(AITravelRules.Cell a, AITravelRules.Cell b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    // ---- following a connector (no exit choice is made here: a fork or a dead end is reported to the caller)
    public enum FollowKind { Way, Fork, DeadEnd, Loop, None }
    public sealed class Follow
    {
        public FollowKind Kind;
        public AITravelRules.Cell Target;   // the cell to walk to: the next area's first cell or the unknown edge ahead
        public int ToArea = -1;             // local index of the area the way leads into (-1 = unknown edge)
        public int Ways;
    }
    // The ways on along 'conn' from a walker that came from 'entry' and is heading through 'first'. A way is a
    // neighbouring area or a group of cells at the unknown edge; one is ahead when it is nearer to 'first' than to
    // 'entry' (ties count as ahead), so the parallel lane of a 2-4 wide corridor is not a way back.
    public static Follow FollowOn(Map map, Connector conn, AITravelRules.Cell entry, AITravelRules.Cell first)
    {
        var result = new Follow { Kind = FollowKind.None };
        if (conn == null) return result;
        var fromFirst = Distances(conn, first, default(AITravelRules.Cell), false);
        var fromEntry = Distances(conn, entry, first, true);
        var ways = new List<(int dFirst, AITravelRules.Cell target, int area)>();
        // openings into the same area side by side are one way (a wide mouth); a far mouth into the same area is another
        var groups = new List<(int area, List<Opening> list)>();
        foreach (var o in conn.Openings)
        {
            int g = groups.FindIndex(x => x.area == o.Area && x.list.Exists(p => Dist(p.Inside, o.Inside) <= 2));
            if (g < 0) groups.Add((o.Area, new List<Opening> { o })); else groups[g].list.Add(o);
        }
        foreach (var grp in groups)
        {
            int df = int.MaxValue, de = int.MaxValue; var target = grp.list[0].Outside;
            foreach (var o in grp.list)
            {
                if (fromFirst.TryGetValue(o.Inside, out int a) && a + 1 < df) { df = a + 1; target = o.Outside; }
                int b = o.Outside.Equals(entry) ? 0 : fromEntry.TryGetValue(o.Inside, out int x) ? x + 1 : int.MaxValue;
                de = Math.Min(de, b);
            }
            if (df != int.MaxValue && df <= de) ways.Add((df, target, grp.area));
        }
        // unknown-edge groups
        var edge = new HashSet<AITravelRules.Cell>(conn.Edge); var done = new HashSet<AITravelRules.Cell>();
        foreach (var e in conn.Edge)
        {
            if (!done.Add(e)) continue;
            var group = new List<AITravelRules.Cell> { e };
            for (int h = 0; h < group.Count; h++) foreach (var d in AITravelRules.Directions) { var n = AITravelRules.Add(group[h], d); if (edge.Contains(n) && done.Add(n)) group.Add(n); }
            int df = int.MaxValue, de = int.MaxValue; var target = e;
            foreach (var g in group)
            {
                if (fromFirst.TryGetValue(g, out int a) && a < df) { df = a; target = g; }
                if (fromEntry.TryGetValue(g, out int b) && b < de) de = b;
            }
            if (df != int.MaxValue && df <= de) ways.Add((df, target, -1));
        }
        result.Ways = ways.Count;
        if (ways.Count == 1) { result.Kind = FollowKind.Way; result.Target = ways[0].target; result.ToArea = ways[0].area; return result; }
        if (ways.Count > 1) { result.Kind = FollowKind.Fork; return result; }
        // nothing ahead: a ring of single cells (every cell has exactly two ways) is a loop, anything else a dead end
        bool ring = conn.Areas.Count == 0;
        foreach (var c in conn.Cells)
        {
            if (!ring) break;
            int n = 0; foreach (var d in AITravelRules.Directions) if (conn.Cells.Contains(AITravelRules.Add(c, d))) n++;
            ring = n == 2;
        }
        result.Kind = ring ? FollowKind.Loop : FollowKind.DeadEnd;
        return result;
    }
    // The walk along a connector from a cell to a target (a connector cell or the next area's first cell), inside the
    // connector only: a navigator given a far target could take a shorter way back through the area just left.
    public static List<AITravelRules.Cell> PathAlong(Connector conn, AITravelRules.Cell from, AITravelRules.Cell target)
    {
        var prev = new Dictionary<AITravelRules.Cell, AITravelRules.Cell>(); var q = new Queue<AITravelRules.Cell>();
        prev[from] = from; q.Enqueue(from);
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (c.Equals(target)) break;
            foreach (var d in AITravelRules.Directions) { var n = AITravelRules.Add(c, d); if ((conn.Cells.Contains(n) || n.Equals(target)) && !prev.ContainsKey(n)) { prev[n] = c; q.Enqueue(n); } }
        }
        var path = new List<AITravelRules.Cell>();
        if (!prev.ContainsKey(target)) return path;
        for (var c = target; !c.Equals(from); c = prev[c]) path.Add(c);
        path.Reverse(); return path;
    }
    public const int FollowLead = 4;   // the navigator is given the connector cell this many steps ahead
    private static Dictionary<AITravelRules.Cell, int> Distances(Connector conn, AITravelRules.Cell start, AITravelRules.Cell forbidden, bool forbid)
    {
        var dist = new Dictionary<AITravelRules.Cell, int>(); var q = new Queue<AITravelRules.Cell>();
        if (conn.Cells.Contains(start)) { dist[start] = 0; q.Enqueue(start); }
        else foreach (var d in AITravelRules.Directions) { var n = AITravelRules.Add(start, d); if (conn.Cells.Contains(n) && !(forbid && n.Equals(forbidden)) && !dist.ContainsKey(n)) { dist[n] = 1; q.Enqueue(n); } }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in AITravelRules.Directions) { var n = AITravelRules.Add(c, d); if (conn.Cells.Contains(n) && !dist.ContainsKey(n) && !(forbid && n.Equals(forbidden))) { dist[n] = dist[c] + 1; q.Enqueue(n); } }
        }
        return dist;
    }
}

// Which area this is across recomputations and sessions. A new analysis inherits ids by the core cells it shares with
// the previous analysis (same world cells), or by a remembered area's anchor cell inside it (re-entry, reload). A
// split leaves the id with the larger part (larger areas pick first); a merge keeps the older id (the smallest id among
// the substantial overlaps). Ids are per character and world.
public sealed class AreaTracker
{
    public sealed class Known { public int Id, Visits, Seq; public AITravelRules.Cell Anchor; }
    public readonly Dictionary<int, Known> Areas = new Dictionary<int, Known>();
    public int NextId = 1, Seq;
    public const int MaxAreas = 1000;   // memory bound (least recently seen dropped)
    private AIAreaRules.Map _prev; private Dictionary<int, int> _prevIds;
    public void Reset() { Areas.Clear(); NextId = 1; Seq = 0; _prev = null; _prevIds = null; }
    // local index -> id
    public Dictionary<int, int> Assign(AIAreaRules.Map map)
    {
        var result = new Dictionary<int, int>(); var taken = new HashSet<int>();
        var votes = new Dictionary<int, int>[map.Areas.Count];
        for (int a = 0; a < votes.Length; a++) votes[a] = new Dictionary<int, int>();
        if (map.AreaGrid != null && _prev != null && _prev.AreaGrid != null)
        {
            int W = map.W, dx = map.Ox - _prev.Ox, dy = map.Oy - _prev.Oy, PW = _prev.W;
            for (int i = 0; i < map.AreaGrid.Length; i++)
            {
                int a = map.AreaGrid[i]; if (a < 0 || !map.CoreGrid[i]) continue;
                int px = i % W + dx, py = i / W + dy; if (px < 0 || py < 0 || px >= PW || py >= PW) continue;
                int pa = _prev.AreaGrid[py * PW + px]; if (pa < 0 || !_prevIds.TryGetValue(pa, out int pid)) continue;
                votes[a].TryGetValue(pid, out int v); votes[a][pid] = v + 1;
            }
        }
        foreach (var k in Areas.Values) { int a = map.AreaIndex(k.Anchor); if (a >= 0 && !votes[a].ContainsKey(k.Id)) votes[a][k.Id] = 1; }
        var order = new List<AIAreaRules.Area>(map.Areas); order.Sort((x, y) => y.Count != x.Count ? y.Count.CompareTo(x.Count) : x.Index.CompareTo(y.Index));
        foreach (var area in order)
        {
            int max = 0; foreach (var kv in votes[area.Index]) if (!taken.Contains(kv.Key) && Areas.ContainsKey(kv.Key)) max = Math.Max(max, kv.Value);
            int best = -1; foreach (var kv in votes[area.Index]) if (!taken.Contains(kv.Key) && Areas.ContainsKey(kv.Key) && kv.Value * 4 >= max && (best < 0 || kv.Key < best)) best = kv.Key;
            if (best < 0) { best = NextId++; Areas[best] = new Known { Id = best, Anchor = area.Centre }; }
            var known = Areas[best]; taken.Add(best); known.Seq = ++Seq;
            if (map.AreaIndex(known.Anchor) != area.Index) known.Anchor = area.Centre;
            result[area.Index] = best;
        }
        while (Areas.Count > MaxAreas) { int oldest = -1, s = int.MaxValue; foreach (var k in Areas.Values) if (k.Seq < s) { s = k.Seq; oldest = k.Id; } Areas.Remove(oldest); }
        _prev = map; _prevIds = result;
        return result;
    }
}

// The observed walkability in 32x32 blocks (-1 unknown, 0 blocked, 1 walkable): a copy of AITravel's map for the area
// analysis, which reads a whole window at once.
public sealed class ObservedGrid
{
    private const int S = 32;
    private readonly Dictionary<long, sbyte[]> _blocks = new Dictionary<long, sbyte[]>();
    private static int Block(int v) => v >= 0 ? v / S : (int)(((long)v - S + 1) / S);
    private static long Key(int bx, int by) => ((long)bx << 32) | (uint)by;
    public void Clear() => _blocks.Clear();
    public void Set(AITravelRules.Cell c, bool walkable)
    {
        int bx = Block(c.X), by = Block(c.Y); long key = Key(bx, by);
        if (!_blocks.TryGetValue(key, out var b)) { b = new sbyte[S * S]; for (int i = 0; i < b.Length; i++) b[i] = -1; _blocks[key] = b; }
        b[(int)((long)c.Y - (long)by * S) * S + (int)((long)c.X - (long)bx * S)] = walkable ? (sbyte)1 : (sbyte)0;
    }
    public void Fill(int ox, int oy, int W, sbyte[] st)
    {
        for (int i = 0; i < st.Length; i++) st[i] = -1;
        int bx0 = Block(ox), bx1 = Block(ox + W - 1), by0 = Block(oy), by1 = Block(oy + W - 1);
        for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                if (!_blocks.TryGetValue(Key(bx, by), out var b)) continue;
                int x0 = Math.Max(ox, bx * S), x1 = Math.Min(ox + W - 1, bx * S + S - 1), y0 = Math.Max(oy, by * S), y1 = Math.Min(oy + W - 1, by * S + S - 1);
                for (int y = y0; y <= y1; y++)
                {
                    int src = (y - by * S) * S + (x0 - bx * S), dst = (y - oy) * W + (x0 - ox);
                    Array.Copy(b, src, st, dst, x1 - x0 + 1);
                }
            }
    }
}
