exec(open(__file__.replace('shape.py','edit.py'),encoding='utf8').read().split("edit(S+'Core/AIGoalKinds.cs'")[0])
def core(s):
 a=s.index('    public static List<(int x, int y)> ShapeTiles('); b=s.index('    private static bool Ints',a)
 s=s[:a]+'''    // Coordinate arithmetic is lazy: a million-tile request does not allocate a million entries.
    public sealed class ShapeTileView : IReadOnlyList<(int x, int y)>
    {
        private readonly long _x, _y, _width;
        public int Count { get; }
        public ShapeTileView(PlaceRef r)
        {
            if (r == null || !r.IsShape) return;
            long count = ShapeCount(r);
            if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(r), "shape exceeds integer work-count representation");
            _x = Math.Min(r.X, r.X2); _y = Math.Min(r.Y, r.Y2);
            _width = (long)Math.Max(r.X, r.X2) - _x + 1;
            Count = (int)count;
        }
        public (int x, int y) this[int i]
        {
            get { if (i < 0 || i >= Count) throw new ArgumentOutOfRangeException(nameof(i)); return ((int)(_x + i % _width), (int)(_y + i / _width)); }
        }
        public IEnumerator<(int x, int y)> GetEnumerator() { for (long i = 0; i < Count; i++) yield return this[(int)i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public static ShapeTileView ShapeTiles(PlaceRef r) => new ShapeTileView(r);
    public static bool ShapeContains(PlaceRef r, int x, int y) => r != null && r.IsShape && x >= Math.Min(r.X,r.X2) && x <= Math.Max(r.X,r.X2) && y >= Math.Min(r.Y,r.Y2) && y <= Math.Max(r.Y,r.Y2);

'''+s[b:]
 s=s.replace('return ((long)Math.Max(r.X, r.X2) - Math.Min(r.X, r.X2) + 1) * ((long)Math.Max(r.Y, r.Y2) - Math.Min(r.Y, r.Y2) + 1);','long width = (long)Math.Max(r.X, r.X2) - Math.Min(r.X, r.X2) + 1;\n        long height = (long)Math.Max(r.Y, r.Y2) - Math.Min(r.Y, r.Y2) + 1;\n        return width > long.MaxValue / height ? long.MaxValue : width * height;')
 return s
edit(S+'Core/AIPlaces.cs',core)
def place(s):
 s=s.replace('public string LastWhy = "";','public int ShapeCursor;\n        public string LastWhy = "";',1)
 s=s.replace('s.Placed.Clear(); s.Skipped.Clear(); s.RouteFact = "";','s.Placed.Clear(); s.Skipped.Clear(); s.RouteFact = ""; s.ShapeCursor = 0;')
 old='private static List<int2> ShapeList(PlaceRef r) { var l = new List<int2>(); foreach (var p in AIPlaces.ShapeTiles(r)) l.Add(new int2(p.x, p.y)); return l; }'
 new='''private static List<int2> ShapeList(PlaceRef r)
    {
        var all = AIPlaces.ShapeTiles(r); var s = S;
        bool same = s.Ref != null && AIPlaces.Format(s.Ref) == AIPlaces.Format(r);
        int start = same ? s.ShapeCursor : 0;
        if (same)
        {
            while (start < all.Count)
            {
                var p = all[start]; var t = new int2(p.x,p.y);
                if (!IsSkipped(s,t) && !s.Placed.Exists(x => math.all(x.at == t))) break;
                start++;
            }
            s.ShapeCursor = start;
        }
        // A per-evaluation processing budget, not a goal or placement limit. The original bounds/count remain intact.
        var batch = new List<int2>();
        for (long i = start; i < all.Count && i < (long)start + 128; i++) { var p = all[(int)i]; batch.Add(new int2(p.x,p.y)); }
        return batch;
    }'''
 if old not in s: print('missing shapeList')
 s=s.replace(old,new)
 s=s.replace('t.Shape = new List<int2>();\n                foreach (var p in AIPlaces.ShapeTiles(r)) t.Shape.Add(new int2(p.x, p.y));\n                t.Label = AIPlaces.Format(r); t.Center = t.Shape[0];','t.Shape = ShapeList(r);\n                t.Label = AIPlaces.Format(r); t.Center = t.Shape.Count > 0 ? t.Shape[0] : new int2(r.X,r.Y);')
 s=s.replace('if (todo.Count == 0)\n                return $"{AIPlaces.Format(r)}에 놓을 칸이 없음','if (todo.Count == 0 && AIPlaces.ShapeCount(r) <= t.Shape.Count)\n                return $"{AIPlaces.Format(r)}에 놓을 칸이 없음')
 s=s.replace('int placed = AIPlaces.ShapePlaced(t.Shape.ConvertAll(q => (q.x, q.y)), at);\n            var end = AIPlaces.EndOf(placed, left.Count, have);','int placed = 0; var distinct = new HashSet<(int,int)>();\n            foreach (var p in at) if (AIPlaces.ShapeContains(t.Ref,p.x,p.y) && distinct.Add(p)) placed++;\n            int total = (int)AIPlaces.ShapeCount(t.Ref);\n            int outstanding = Math.Max(0,total - placed - s.Skipped.Count);\n            var end = AIPlaces.EndOf(placed, outstanding, have);')
 s=s.replace('placed, t.Shape.Count, true,','placed, total, true,')
 # All skipped in one bounded chunk is progress in evaluation, not a verdict about the whole request.
 s=s.replace('if (left.Count == 0) { if (s.Skipped.Count > 0)', 'if (left.Count == 0) { if (s.Skipped.Count > 0)')
 return s
edit(S+'AIPlaces.cs',place)
