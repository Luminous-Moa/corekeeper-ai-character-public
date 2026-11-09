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
    public static Junction NextJunction(Cell at, Cell previous, float radius, Func<Cell, bool> walkable)
    {
        var origin = at;
        var visited = new HashSet<Cell>();
        int steps = 0;
        while (visited.Add(at))
        {
            double dx = (double)at.X - origin.X, dy = (double)at.Y - origin.Y;
            if (dx * dx + dy * dy > radius * radius) return null;
            var exits = Exits(at, previous, walkable);
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
    public static List<Cell> KnownPath(Cell start, Cell goal, IDictionary<Cell, bool> observed, Func<Cell, bool> allowed = null)
    {
        var queue = new Queue<Cell>();
        var previous = new Dictionary<Cell, Cell>();
        var seen = new HashSet<Cell> { start };
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var at = queue.Dequeue();
            if (at.Equals(goal))
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
}
