// Many remembered chests (2026-11-08 review I3): every seen chest is shared and remembered, so a chest list step can grow
// past the decision request's 255 choices. A list is split by area, never cut or paged: up to SplitAbove chests a step lists
// them as before; above that it lists areas (aligned square tile cells, "area:x,y,size"), and an area lists its chests or,
// while it still holds more than SplitAbove, its quarter-size cells. Every chest stays reachable through the tree.
using System;
using System.Collections.Generic;
using Unity.Mathematics;

public static class AIChestAreas
{
    public const int SplitAbove = 40;
    public sealed class Area<T> { public string id; public int2 min; public int size; public readonly List<T> items = new List<T>(); }

    public static bool IsArea(string where) => where != null && where.StartsWith("area:", StringComparison.Ordinal);
    public static bool Parse(string where, out int2 min, out int size)
    {
        min = default; size = 0;
        if (!IsArea(where)) return false;
        var p = where.Substring(5).Split(',');
        return p.Length == 3 && int.TryParse(p[0], out min.x) && int.TryParse(p[1], out min.y) && int.TryParse(p[2], out size) && size > 0;
    }
    // The chests inside an area id (all of them for a non-area id).
    public static List<T> Within<T>(List<T> items, Func<T, int2> tile, string area)
    {
        if (!Parse(area, out var min, out int size)) return items;
        return items.FindAll(c => { var t = tile(c); return t.x >= min.x && t.x < min.x + size && t.y >= min.y && t.y < min.y + size; });
    }
    // null when the items are few enough to list directly.
    public static List<Area<T>> Split<T>(List<T> items, Func<T, int2> tile, string area)
    {
        if (items.Count <= SplitAbove) return null;
        int size;
        if (Parse(area, out _, out int parent)) size = math.max(1, parent / 4);
        else { size = 32; while (Cells(items, tile, size).Count > SplitAbove) size *= 2; }
        var list = new List<Area<T>>(Cells(items, tile, size).Values);
        list.Sort((a, b) => a.min.x != b.min.x ? a.min.x.CompareTo(b.min.x) : a.min.y.CompareTo(b.min.y));
        return list;
    }
    private static Dictionary<int2, Area<T>> Cells<T>(List<T> items, Func<T, int2> tile, int size)
    {
        var cells = new Dictionary<int2, Area<T>>();
        foreach (var c in items)
        {
            var t = tile(c);
            var min = new int2(FloorDiv(t.x, size) * size, FloorDiv(t.y, size) * size);
            if (!cells.TryGetValue(min, out var cell)) cells[min] = cell = new Area<T> { id = $"area:{min.x},{min.y},{size}", min = min, size = size };
            cell.items.Add(c);
        }
        return cells;
    }
    // The one rule every per-chest choice producer follows (review-4): up to SplitAbove chests each chest is shown (area
    // null); above that one chest per area of Split(items, parent) — the one `rank` puts first (an open chest, then the
    // nearest) — so a decision step never holds more than SplitAbove chest choices, and every other chest stays reachable
    // through the life tree's storage steps (area → smaller area → chest), which use the same Split. No cap, no page.
    public static List<(T item, Area<T> area)> Step<T>(List<T> items, Func<T, int2> tile, string parent, Func<T, float> rank)
    {
        var result = new List<(T, Area<T>)>();
        var areas = Split(items, tile, parent);
        if (areas == null) { foreach (var x in items) result.Add((x, null)); return result; }
        foreach (var a in areas)
        {
            var best = a.items[0]; float r = rank(best);
            foreach (var x in a.items) { float rx = rank(x); if (rx < r) { best = x; r = rx; } }
            result.Add((best, a));
        }
        return result;
    }
    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    public static string Text<T>(Area<T> a, Func<T, int2> tile, float2 pos, bool choice = true)
    {
        float near = float.MaxValue;
        foreach (var c in a.items) near = math.min(near, math.distance(pos, (float2)tile(c)));
        return $"상자 묶음 {a.id} — 타일 ({a.min.x},{a.min.y})~({a.min.x + a.size - 1},{a.min.y + a.size - 1})의 상자 {a.items.Count}개, 가장 가까운 것 {near:F0}칸{(choice ? ". 고르면 그 안의 상자를 봄" : "")}";
    }
}
