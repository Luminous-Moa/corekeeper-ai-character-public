using System;
using System.Collections.Generic;

// Topology only. The caller supplies either observed geometry or the explicitly bounded
// direction-only lookahead. These functions never select a branch on the model's behalf.
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
    public static float LeadDistance(float speed, float responseSeconds)
        => Math.Max(1f, Math.Max(0f, speed) * Math.Max(0f, responseSeconds));
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
    public sealed class Junction
    {
        public Cell At, Previous;
        public List<Cell> Exits;
        public int Steps;
        public string Token;
    }
    // A lane inside a wide corridor is not a separate passage. Preserve the incoming
    // direction while its cross-section stays the same. A newly opening side passage
    // becomes a choice when it extends beyond the previous corridor's width and there
    // is still a forward arm; a unique bend remains steering.
    public static int Width(Cell at, Cell heading, Func<Cell, bool> walkable)
        => Math.Abs(heading.X) + Math.Abs(heading.Y) == 1 ? 1 + RayDepth(at, new Cell(-heading.Y, heading.X), walkable) + RayDepth(at, new Cell(heading.Y, -heading.X), walkable) : 1;
    public static List<Cell> PassageExits(Cell at, Cell previous, Func<Cell, bool> walkable, int corridorWidth = 0)
    {
        var exits = Exits(at, previous, walkable);
        var heading = new Cell(at.X - previous.X, at.Y - previous.Y);
        if (Math.Abs(heading.X) + Math.Abs(heading.Y) != 1) return exits;
        var forward = Add(at, heading);
        if (!exits.Contains(forward))
        {
            if (corridorWidth > 1)
            {
                var arms = exits.FindAll(p => RayDepth(at, new Cell(p.X - at.X, p.Y - at.Y), walkable) >= corridorWidth);
                return arms; // a lane across a closed end is not a new corridor; ask instead of U-turning
            }
            return exits;
        }
        var left = new Cell(-heading.Y, heading.X);
        var right = new Cell(heading.Y, -heading.X);
        int oldLeft = RayDepth(previous, left, walkable), oldRight = RayDepth(previous, right, walkable);
        int width = 1 + oldLeft + oldRight;
        if (corridorWidth > 0) width = Math.Min(width, corridorWidth);
        var passages = new List<Cell> { forward };
        if (RayDepth(at, heading, walkable) < width) return passages;
        int newLeft = RayDepth(at, left, walkable), newRight = RayDepth(at, right, walkable);
        if (newLeft > oldLeft && newLeft >= width) passages.Add(Add(at, left));
        if (newRight > oldRight && newRight >= width) passages.Add(Add(at, right));
        // Stable world-axis order makes answer tokens independent of the order above.
        passages.Sort((a, b) => DirectionIndex(at, a).CompareTo(DirectionIndex(at, b)));
        return passages;
    }
    private static int DirectionIndex(Cell from, Cell to)
        => to.X > from.X ? 0 : to.X < from.X ? 1 : to.Y > from.Y ? 2 : 3;
    private static int RayDepth(Cell at, Cell direction, Func<Cell, bool> walkable)
    {
        if (direction.X == 0 && direction.Y == 0) return 0;
        int depth = 0;
        while ((direction.X <= 0 || at.X < int.MaxValue) && (direction.X >= 0 || at.X > int.MinValue) &&
               (direction.Y <= 0 || at.Y < int.MaxValue) && (direction.Y >= 0 || at.Y > int.MinValue))
        {
            at = Add(at, direction);
            if (!walkable(at)) break;
            depth++;
        }
        return depth;
    }
    public static Junction NextJunction(Cell at, Cell previous, float radius, Func<Cell, bool> walkable, int corridorWidth = 0)
    {
        var origin = at;
        var visited = new HashSet<Cell>();
        int steps = 0;
        while (visited.Add(at))
        {
            double dx = (double)at.X - origin.X, dy = (double)at.Y - origin.Y;
            if (dx * dx + dy * dy > radius * radius) return null;
            var heading = new Cell(at.X - previous.X, at.Y - previous.Y);
            int width = Width(previous, heading, walkable);
            corridorWidth = corridorWidth > 0 ? Math.Min(corridorWidth, width) : width;
            var exits = PassageExits(at, previous, walkable, corridorWidth);
            if (exits.Count > 1)
                return new Junction { At = at, Previous = previous, Exits = exits, Steps = steps,
                    Token = at + "|" + previous + "|" + string.Join(";", exits) };
            if (exits.Count != 1) return null;
            previous = at; at = exits[0]; steps++;
        }
        return null;
    }
    public static bool CanUseTurn(Junction current, string token, Cell selected)
        => current != null && current.Token == token && current.Exits.Contains(selected);

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
