// AIBaseMap — the geography around the base as data for the map query tool of 단계 4 (user decision 2026-11-05):
// the tile grid (walls, floor, water, unknown, objects, doors, tiles the game never takes), the objects near the base
// (name, kind, the tiles they occupy) and building plans laid over the same grid (the edge to place, batch order, door
// spot, torch spots, the inside, what stays outside, unchecked edge tiles), preview (computed, not chosen) and
// confirmed (chosen). Unity-free: the text overlay and the JSON are checked offline (core-tests EdgeTests). Drawing it
// as text or image for the upper LLM is 단계 4.
// Grid rows: top row = the window's highest y, left = lowest x; one character per tile:
//   '#' wall  '.' floor  '~' water  '?' never seen  'N' floor the game never takes (spawn pad, immune)
//   'O' an object  'D' an own door  'd' another player's door
// Plan overlay on the rows: 'X' a tile to place (wall block)  'G' the door spot  'T' a torch spot  ':' inside floor
//   '!' a tile to place next to the unseen or the range edge (unchecked)
using System;
using System.Collections.Generic;
using System.Text;

public sealed class MapObject
{
    public string name, kind;   // kind: core, spawnpad, respawn, station, chest, pot, statue, door, owndoor, light, object
    public readonly List<(int x, int y)> tiles = new List<(int, int)>();
}

public sealed class EdgePlanData
{
    public string state = "preview";   // preview (computed, not chosen) | confirmed (chosen with pick)
    public string label = "";          // the option's facts
    public readonly List<(int x, int y)> place = new List<(int, int)>();
    public readonly List<List<(int x, int y)>> batches = new List<List<(int, int)>>();
    public (int x, int y)? door;
    public readonly List<(int x, int y)> torches = new List<(int, int)>();
    public readonly List<(int x, int y)> inside = new List<(int, int)>();
    public readonly List<string> outside = new List<string>();
    public readonly List<string> onEdge = new List<string>();
    public readonly List<(int x, int y)> unchecked_ = new List<(int, int)>();
}

public static class AIBaseMap
{
    public const int TorchSpacing = 7;   // suggested torch spots at least this far apart (tiles)

    // A plan from an edge option: the tiles to place (the door spot apart), the batches in order, torch spots spread
    // over the inside floor, the inside, what stays outside or on the edge, the unchecked tiles.
    public static EdgePlanData Plan(EdgeGrid g, EdgeOption o, (int x, int y)? door, string state, string label, List<(int x, int y)> lightsPlaced = null)
    {
        var p = new EdgePlanData { state = state, label = label, door = door };
        foreach (var gp in o.gaps) foreach (var t in gp.tiles) if (!(door.HasValue && door.Value.Equals(t))) p.place.Add(t);
        foreach (var b in AIBaseEdgeCore.Batches(g, o, door)) p.batches.Add(new List<(int, int)>(b.tiles));
        foreach (var key in o.inside) p.inside.Add(((int)(key >> 32), (int)(uint)key));
        p.inside.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        p.outside.AddRange(o.outside); p.onEdge.AddRange(o.onEdge);
        foreach (var gp in o.gaps)
            foreach (var t in gp.tiles)
                for (int k = 0; k < 4; k++)
                {
                    int nx = t.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = t.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!g.In(nx, ny) || !g.known[g.Idx(nx, ny)]) { p.unchecked_.Add(t); break; }
                }
        // torch spots: inside placeable floor, farthest-first from the placed lights and each other
        var lights = new List<(int x, int y)>(lightsPlaced ?? new List<(int, int)>());
        var cand = new List<(int x, int y)>();
        foreach (var t in p.inside) { int i = g.Idx(t.x, t.y); if (g.pass[i] && (g.noPlace == null || !g.noPlace[i])) cand.Add(t); }
        while (cand.Count > 0)
        {
            int bi = -1; int bd = -1;
            for (int i = 0; i < cand.Count; i++)
            {
                int d = int.MaxValue;
                foreach (var l in lights) d = Math.Min(d, Math.Max(Math.Abs(l.x - cand[i].x), Math.Abs(l.y - cand[i].y)));
                if (lights.Count == 0) d = -Math.Abs(cand[i].x - g.seedX) - Math.Abs(cand[i].y - g.seedY) + 1000;   // the first near the base
                if (d > bd) { bd = d; bi = i; }
            }
            if (bi < 0 || (lights.Count > 0 && bd < TorchSpacing)) break;
            p.torches.Add(cand[bi]); lights.Add(cand[bi]); cand.RemoveAt(bi);
            if (p.torches.Count >= 12) break;
        }
        return p;
    }

    // the base rows (see the legend above)
    public static List<string> Rows(EdgeGrid g, List<MapObject> objects)
    {
        int n = g.n;
        var c = new char[n, n];
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int i = x * n + y;
                c[x, y] = !g.known[i] ? '?' : g.water != null && g.water[i] ? '~' : g.pass[i] ? (g.noPlace != null && g.noPlace[i] ? 'N' : '.') : '#';
            }
        if (objects != null)
            foreach (var o in objects)
            {
                char ch = o.kind == "owndoor" ? 'D' : o.kind == "door" ? 'd' : o.kind == "spawnpad" || o.kind == "respawn" ? '\0' : 'O';
                if (ch == '\0') continue;
                foreach (var t in o.tiles) if (g.In(t.x, t.y)) c[t.x - g.loX, t.y - g.loY] = ch;
            }
        var rows = new List<string>();
        for (int y = n - 1; y >= 0; y--)
        {
            var sb = new StringBuilder(n);
            for (int x = 0; x < n; x++) sb.Append(c[x, y]);
            rows.Add(sb.ToString());
        }
        return rows;
    }

    // the plan laid over the base rows
    public static List<string> Overlay(EdgeGrid g, List<string> rows, EdgePlanData p)
    {
        int n = g.n;
        var c = new char[n][];
        for (int r = 0; r < n; r++) c[r] = rows[r].ToCharArray();
        void Put((int x, int y) t, char ch) { if (!g.In(t.x, t.y)) return; c[n - 1 - (t.y - g.loY)][t.x - g.loX] = ch; }
        foreach (var t in p.inside) if (c[n - 1 - (t.y - g.loY)][t.x - g.loX] == '.') Put(t, ':');
        foreach (var t in p.place) Put(t, 'X');
        foreach (var t in p.unchecked_) if (!(p.door.HasValue && p.door.Value.Equals(t))) Put(t, '!');
        if (p.door.HasValue) Put(p.door.Value, 'G');
        foreach (var t in p.torches) Put(t, 'T');
        var res = new List<string>();
        foreach (var r in c) res.Add(new string(r));
        return res;
    }

    // ---------------------------------------------------------------- JSON (written by hand: no library in the mod)

    public static string Json(EdgeGrid g, List<MapObject> objects, List<EdgePlanData> plans, string world, string who, long unix)
    {
        var rows = Rows(g, objects);
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"kind\": \"basemap\", \"version\": 1, \"world\": {Q(world)}, \"character\": {Q(who)}, \"unix\": {unix},\n");
        sb.Append($"  \"origin\": [{g.loX}, {g.loY}], \"size\": {g.n}, \"seed\": [{g.seedX}, {g.seedY}], \"range\": {g.windowRange},\n");
        sb.Append("  \"legend\": {\"#\": \"wall\", \".\": \"floor\", \"~\": \"water\", \"?\": \"never seen\", \"N\": \"floor the game never takes\", \"O\": \"object\", \"D\": \"own door\", \"d\": \"other door\"},\n");
        sb.Append("  \"rowsTopIsY\": ").Append(g.loY + g.n - 1).Append(", \"rowsLeftIsX\": ").Append(g.loX).Append(",\n");
        sb.Append("  \"rows\": [\n");
        for (int i = 0; i < rows.Count; i++) sb.Append("    ").Append(Q(rows[i])).Append(i + 1 < rows.Count ? ",\n" : "\n");
        sb.Append("  ],\n  \"objects\": [\n");
        for (int i = 0; objects != null && i < objects.Count; i++)
            sb.Append($"    {{\"name\": {Q(objects[i].name)}, \"kind\": {Q(objects[i].kind)}, \"tiles\": {Tiles(objects[i].tiles)}}}").Append(i + 1 < objects.Count ? ",\n" : "\n");
        sb.Append("  ],\n  \"overlayLegend\": {\"X\": \"tile to place\", \"G\": \"door spot\", \"T\": \"torch spot\", \":\": \"inside floor\", \"!\": \"tile to place next to the unseen or the range edge\"},\n");
        sb.Append("  \"plans\": [\n");
        for (int i = 0; plans != null && i < plans.Count; i++)
        {
            var p = plans[i];
            sb.Append("    {\n");
            sb.Append($"      \"state\": {Q(p.state)}, \"label\": {Q(p.label)},\n");
            sb.Append($"      \"place\": {Tiles(p.place)},\n");
            sb.Append("      \"batches\": [").Append(string.Join(", ", p.batches.ConvertAll(Tiles))).Append("],\n");
            sb.Append($"      \"door\": {(p.door.HasValue ? $"[{p.door.Value.x}, {p.door.Value.y}]" : "null")},\n");
            sb.Append($"      \"torches\": {Tiles(p.torches)},\n");
            sb.Append($"      \"outside\": [{string.Join(", ", p.outside.ConvertAll(Q))}], \"onEdge\": [{string.Join(", ", p.onEdge.ConvertAll(Q))}],\n");
            sb.Append($"      \"unchecked\": {Tiles(p.unchecked_)},\n");
            sb.Append($"      \"inside\": {Tiles(p.inside)},\n");
            var ov = Overlay(g, rows, p);
            sb.Append("      \"overlay\": [\n");
            for (int r = 0; r < ov.Count; r++) sb.Append("        ").Append(Q(ov[r])).Append(r + 1 < ov.Count ? ",\n" : "\n");
            sb.Append("      ]\n");
            sb.Append("    }").Append(i + 1 < plans.Count ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    private static string Tiles(List<(int x, int y)> l)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(", "); sb.Append('[').Append(l[i].x).Append(", ").Append(l[i].y).Append(']'); }
        return sb.Append(']').ToString();
    }

    private static string Q(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s ?? "")
        {
            if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
            else if (ch == '\n') sb.Append("\\n");
            else if (ch < 0x20) sb.Append($"\\u{(int)ch:x4}");
            else sb.Append(ch);
        }
        return sb.Append('"').ToString();
    }
}
