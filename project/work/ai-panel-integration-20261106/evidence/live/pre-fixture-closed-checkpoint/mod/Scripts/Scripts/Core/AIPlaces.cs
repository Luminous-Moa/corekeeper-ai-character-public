// AIPlaces (Core) — place identifiers the upper LLM writes in goto / place goals and the report's places list
// (제작대-놓기 설계 1-설계.md §2.1, §2.6, §2.7, §2.11). Unity-free: Tools/core-tests PlaceGoalTests checks the round trip,
// the forms, the shapes and the sentences.
// Identifiers (§2.7, shared with 코어-안전지대 단계 4 지리 조회):
//   here · base · core · farm                      fixed places
//   tile:x,y                                        a tile (AIBody.TileOf integer tiles, as memgraph)
//   <kind>:<ObjectID>@x,y                          a remembered object; kind = station · chest · pot (AIBaseMap words) · ore
//   water:x,y                                       a known water tile
//   rect:x1,y1,x2,y2 · line:x1,y1,x2,y2           several tiles (place only, §2.11)
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class PlaceRef
{
    public string Kind = "";      // here base core farm tile station chest pot ore water rect line
    public string Item = "";      // the ObjectID name of an object place (station/chest/pot/ore)
    public int X, Y, X2, Y2;
    public bool IsShape => Kind == "rect" || Kind == "line";
    public bool IsObject => Kind == "station" || Kind == "chest" || Kind == "pot";
    public bool HasTile => Kind == "tile" || Kind == "water" || IsObject || Kind == "ore";
    public override string ToString() => AIPlaces.Format(this);
}

// One line of the report's places list (the game side fills it; Pick chooses what goes in).
public sealed class PlaceLine
{
    public string Group;   // fixed · mine · other · ore · water
    public string Sub;     // ore kind (one per kind), else ""
    public float Dist;
    public string Text;
}

public static class AIPlaces
{
    public const int MaxShapeTiles = 30;       // §2.11: the cap of one rect / line (as gather's count)
    public const int PlacesMax = 12;           // §2.6: lines of the report's places list
    public const int MineMax = 5, OtherMax = 2, OreKinds = 3, WaterMax = 1;
    public static readonly string[] Fixed = { "here", "base", "core", "farm" };
    public static readonly string[] ObjectKinds = { "station", "chest", "pot", "ore" };

    public static string ObjectId(string kind, string item, int x, int y) => $"{kind}:{item}@{x},{y}";
    public static string TileId(int x, int y) => $"tile:{x},{y}";

    public static string Format(PlaceRef r)
    {
        if (r == null) return "";
        switch (r.Kind)
        {
            case "tile": return TileId(r.X, r.Y);
            case "water": return $"water:{r.X},{r.Y}";
            case "rect": case "line": return $"{r.Kind}:{r.X},{r.Y},{r.X2},{r.Y2}";
            case "station": case "chest": case "pot": case "ore": return ObjectId(r.Kind, r.Item, r.X, r.Y);
            default: return r.Kind;
        }
    }

    // where -> PlaceRef; null where (place without one) is base. err: why it cannot be read (a sentence for the model).
    public static bool Parse(string where, out PlaceRef r, out string err)
    {
        r = null; err = null;
        string s = (where ?? "").Trim();
        if (s == "" || s == "null") s = "base";
        string low = s.ToLowerInvariant();
        foreach (var f in Fixed) if (low == f) { r = new PlaceRef { Kind = f }; return true; }
        int colon = s.IndexOf(':');
        if (colon <= 0) { err = $"모르는 장소 '{s}'(here·base·core·farm·tile:x,y·station:<ID>@x,y·chest:…·pot:…·ore:<ID>@x,y·water:x,y·rect:x1,y1,x2,y2·line:x1,y1,x2,y2)"; return false; }
        string kind = low.Substring(0, colon), rest = s.Substring(colon + 1).Trim();
        if (kind == "tile" || kind == "water")
        {
            if (!Ints(rest, 2, out var v)) { err = $"'{s}' — {kind}:x,y 꼴이 아님"; return false; }
            r = new PlaceRef { Kind = kind, X = v[0], Y = v[1] }; return true;
        }
        if (kind == "rect" || kind == "line")
        {
            if (!Ints(rest, 4, out var v)) { err = $"'{s}' — {kind}:x1,y1,x2,y2 꼴이 아님"; return false; }
            r = new PlaceRef { Kind = kind, X = v[0], Y = v[1], X2 = v[2], Y2 = v[3] };
            if (kind == "line" && v[0] != v[2] && v[1] != v[3]) { err = $"'{s}' — line은 가로나 세로 한 줄이어야 함"; r = null; return false; }
            long n = ShapeCount(r);   // 검수 S2: the real tile count, not the capped list
            if (n > MaxShapeTiles) { err = $"'{s}' — {n}칸, 한 번에 {MaxShapeTiles}칸까지"; r = null; return false; }
            return true;
        }
        if (Array.IndexOf(ObjectKinds, kind) >= 0)
        {
            int at = rest.LastIndexOf('@');
            if (at <= 0 || !Ints(rest.Substring(at + 1), 2, out var v)) { err = $"'{s}' — {kind}:<ObjectID>@x,y 꼴이 아님"; return false; }
            r = new PlaceRef { Kind = kind, Item = rest.Substring(0, at).Trim(), X = v[0], Y = v[1] }; return true;
        }
        err = $"모르는 장소 종류 '{kind}'";
        return false;
    }

    // the form of a goto / place step's where (a later plan step: only the form is checked, §2.3)
    public static string WhereForm(string doKind, string where)
    {
        if (doKind == "goto" && string.IsNullOrWhiteSpace(where)) return "goto에 where가 없음";
        if (!Parse(where, out var r, out string err)) return err;
        if (doKind == "goto" && r.IsShape) return $"goto의 where는 한 곳(rect·line은 place만)";
        return null;
    }

    // the tiles a rect / line covers (0 for other places), without listing them
    public static long ShapeCount(PlaceRef r)
    {
        if (r == null || !r.IsShape) return 0;
        return ((long)Math.Max(r.X, r.X2) - Math.Min(r.X, r.X2) + 1) * ((long)Math.Max(r.Y, r.Y2) - Math.Min(r.Y, r.Y2) + 1);
    }

    // 검수 B1: a place goal's count. No count (the bridge reads it as -1) or 0 is 1, as brief says; a shape's count is its
    // tiles (the count given is not used). null when taken, else why not.
    public static string PlaceCount(PlaceRef r, int count, out int n)
    {
        if (r != null && r.IsShape) { n = (int)Math.Min(ShapeCount(r), int.MaxValue); return null; }
        n = count < 1 ? 1 : count;
        return n > MaxShapeTiles ? $"개수 범위 밖({count}, 1~{MaxShapeTiles})" : null;
    }

    // 검수 F1: "there" for a goto / home: within the place's arrive distance of its centre, or standing on one of the
    // tiles the walk aims at (a stand of a big station, a base stand out to ring 6)
    public const float StandArrive = 0.7f;
    public static bool ArrivedAt(float centreDist, float arriveR, float nearestStandDist) => centreDist <= arriveR || nearestStandDist <= StandArrive;

    // 검수 F3: the fact a place goal is taken with when the bag holds fewer than it needs ("" when enough). shape: tiles
    public static string ShortFact(string item, int have, int need, bool shape)
    {
        if (need <= 0 || have >= need) return "";
        return shape ? $"가방에 {item} {have}개, 놓을 칸 {need}개 — {have}칸만 놓임" : $"가방에 {item} {have}개, {need}개 놓기 — {have}개만 놓임";
    }

    // 검수 F3/F4: where a place goal stands. done: placed (a shape: tiles holding the item), left: still to place (skipped
    // tiles not counted), have: in the bag now.
    public enum PlaceEnd { Going, Done, Short, Stuck }
    public static PlaceEnd EndOf(int done, int left, int have)
    {
        if (left <= 0) return done > 0 ? PlaceEnd.Done : PlaceEnd.Stuck;   // nothing left: all there, or every tile skipped
        if (have <= 0) return done > 0 ? PlaceEnd.Short : PlaceEnd.Stuck;  // the bag ran out after some
        return PlaceEnd.Going;
    }

    // 재검수 N1: the tiles of a shape this goal placed (distinct, on the shape) — tiles that held the item before the
    // goal are not counted (they are skipped when the goal is taken: "이미 놓여 있음")
    public static int ShapePlaced(List<(int x, int y)> shape, List<(int x, int y)> placedAt)
    {
        var on = new HashSet<(int, int)>(shape); var seen = new HashSet<(int, int)>();
        foreach (var p in placedAt) if (on.Contains(p)) seen.Add(p);
        return seen.Count;
    }

    // 재검수 N2: a tile's PlaceSkill that did not start: skip that tile when the tile is why (no spot / stand), else stop
    // the whole batch (the bag, the hand slot: every other tile would fail the same way)
    public enum TileStartFail { Skip, Stop }
    public static TileStartFail OnTileStartFail(bool tileFault) => tileFault ? TileStartFail.Skip : TileStartFail.Stop;

    // 실게임2 P1: the door a walk tries when the game's path search found no way and the plan's sides do not tell (own
    // edge doors and my doors alike): within NearDoorR of the AI, a detour of at most NearDoorExtra over the straight way,
    // the shortest detour first
    public const float NearDoorR = 12f, NearDoorExtra = 12f;
    // 검수 Q1: the own edge doors join that choice only with a plan and when the AI and the target are not both inside
    // (a walk between two inside tiles never goes out through the edge — base-s3's same-side rule); my doors always
    public static bool EdgeDoorsJoin(bool planActive, bool atInside, bool targetInside) => planActive && !(atInside && targetInside);
    public static bool NearDoor((float x, float y) a, (float x, float y) b, List<(int x, int y)> edgeDoors, List<(int x, int y)> myDoors, out (int x, int y) door)
    {
        door = default; float best = float.MaxValue;
        float D(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        float straight = D(a.x, a.y, b.x, b.y);
        foreach (var list in new[] { edgeDoors, myDoors })
            if (list != null)
                foreach (var d in list)
                {
                    float da = D(a.x, a.y, d.x, d.y);
                    if (da > NearDoorR) continue;
                    float c = da + D(d.x, d.y, b.x, b.y);
                    if (c > straight + NearDoorExtra) continue;
                    if (c < best) { best = c; door = d; }
                }
        return best < float.MaxValue;
    }

    // 실게임2 P3: the tile types whose item goes on water (by name: Core has no TileType) — a bridge
    public static bool TileGoesOnWater(string tileType) => string.Equals(tileType, "bridge", StringComparison.OrdinalIgnoreCase);

    // 실게임3 R-b: may the item go on a walkable (ground) tile? The game (PlacementHandler.Activate) reads
    // canPlaceOnWalkableTiles from the placement prefab's properties: when the placement-settings flag (-975748197) is
    // there, the land flag (1497889171) decides; without settings it is true. A bridge tile (TileGoesOnWater) is water
    // only: the game refused WoodBridge on ground tiles (-12,3)·(-13,3) three presses each (실게임3 t=384) — [관찰]; which
    // game flag makes it so is not read (bridge prefab properties not dumped), so the tile type decides as for water.
    public static bool LandOk(bool hasPlacementSettings, bool hasLandFlag, string tileType) =>
        (!hasPlacementSettings || hasLandFlag) && !TileGoesOnWater(tileType);

    // 실게임3 R-a: which station of the placed kind is the one just placed: not one that stood there before the place
    // (an old anvil 1.4 tiles away was named), the one on the chosen footprint first, else the nearest within maxDist;
    // -1 when none is new.
    public static int NewPlacedIndex(bool[] wasBefore, bool[] onSpot, float[] dist, float maxDist)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < wasBefore.Length; i++)
        {
            if (wasBefore[i] || dist[i] > maxDist) continue;
            float d = onSpot[i] ? -1f : dist[i];
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // the reason a water-only item has no spot in the window (no known water there) — the options' "none" sentence
    public static string WaterOnlyNote(string item, int waterSpots, int searchR) =>
        waterSpots > 0 ? "" : $"{item}은(는) 물 위에만 놓임 — 지금 둘레 {searchR}칸 안에 놓을 수 있는 아는 물 칸이 없음(물가로 가서 놓기: place의 where를 물 근처 칸으로)";

    // 실게임2 S-b: the rect / line edge sentence (the bracket belongs to the gap tiles only)
    public static string ShapeEdgeText(SortedDictionary<int, int> perGap, int door, int inside)
    {
        var parts = new List<string>();
        var gaps = new List<string>();
        if (perGap != null) foreach (var kv in perGap) gaps.Add($"{kv.Value}칸이 둘레 틈 {kv.Key} 위");
        if (gaps.Count > 0) parts.Add(string.Join(", ", gaps) + "(틈 위에 걷지 못하는 것을 놓으면 그 틈을 메움)");
        if (door > 0) parts.Add("1칸이 둘레 문 자리");
        if (inside > 0) parts.Add($"{inside}칸이 기지 안쪽");
        return parts.Count == 0 ? "" : "그중 " + string.Join(", ", parts);
    }

    // 검수 E2: the doors on a dig plan's route ("(x,y)" each), or null
    public static string DoorsOnText(List<string> doors) =>
        doors == null || doors.Count == 0 ? null : $"계획 길은 문 {string.Join(" ", doors)}를 지남(열기→지나기→닫기; 게임 길찾기가 다른 길을 찾으면 그 길로 감)";

    // the skipped tiles of a shape, grouped by why (실게임 D3: every reason is said, with its count and up to `perWhy`
    // tiles; the rest of a reason's tiles counted)
    public static string SkippedText(List<(int x, int y, string why)> skipped, int perWhy = 3)
    {
        if (skipped == null || skipped.Count == 0) return "";
        var order = new List<string>(); var by = new Dictionary<string, List<(int x, int y)>>();
        foreach (var s in skipped)
        {
            string w = s.why ?? "";
            if (!by.TryGetValue(w, out var l)) { by[w] = l = new List<(int, int)>(); order.Add(w); }
            l.Add((s.x, s.y));
        }
        var parts = new List<string>();
        foreach (var w in order)
        {
            var l = by[w];
            var tiles = new List<string>();
            for (int i = 0; i < l.Count && i < perWhy; i++) tiles.Add($"({l[i].x},{l[i].y})");
            parts.Add($"{w} {l.Count}칸 {string.Join(" ", tiles)}{(l.Count > perWhy ? $" 외 {l.Count - perWhy}칸" : "")}");
        }
        return $"건너뛴 칸 {skipped.Count}개: {string.Join("; ", parts)}";
    }

    // the end sentence of a place goal (Done or Short). need: what the goal asked (a shape: its tiles)
    public static string EndText(string item, string label, PlaceEnd end, int done, int need, bool shape, int skippedN, string skippedText)
    {
        string unit = shape ? "칸" : "개";
        string head = shape ? $"{label}의 {need}칸 중 {done}칸에 {item} 놓음" : $"{item} {done}개 놓음({label})";
        string tail = end == PlaceEnd.Short ? $" — 가방에 {item}이(가) 떨어져 {Math.Max(0, need - done - skippedN)}{unit} 못 놓음" : "";
        return head + tail + (string.IsNullOrEmpty(skippedText) ? "" : " · " + skippedText);
    }

    // 보고 길이 (검수): a list kept within maxChars (each item's length + 3 for quotes and comma), in its order; dropped counted
    public static List<string> CapChars(List<string> items, int maxChars, out int dropped)
    {
        var res = new List<string>(); dropped = 0; int used = 0;
        foreach (var x in items)
        {
            int len = (x ?? "").Length + 3;
            if (used + len <= maxChars) { res.Add(x); used += len; } else dropped++;
        }
        return res;
    }

    // can_do.craft groups kept within maxChars (the group names and the item texts); what does not fit is counted in more
    public static List<(string where, List<string> items)> CapGroups(List<(string where, List<string> items)> groups, Func<string, string> textOf, int maxChars, ref int more)
    {
        var res = new List<(string, List<string>)>(); int used = 0;
        foreach (var g in groups)
        {
            var items = new List<string>();
            int head = g.where.Length + 6;
            foreach (var x in g.items)
            {
                int len = (textOf(x) ?? "").Length + 3 + (items.Count == 0 ? head : 0);
                if (used + len <= maxChars) { items.Add(x); used += len; } else more++;
            }
            if (items.Count > 0) res.Add((g.where, items));
        }
        return res;
    }

    // rect / line tiles in row order (x then y), deduplicated
    public static List<(int x, int y)> ShapeTiles(PlaceRef r)
    {
        var res = new List<(int, int)>();
        if (r == null || !r.IsShape) return res;
        int x0 = Math.Min(r.X, r.X2), x1 = Math.Max(r.X, r.X2), y0 = Math.Min(r.Y, r.Y2), y1 = Math.Max(r.Y, r.Y2);
        if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 4 * MaxShapeTiles) { for (int i = 0; i <= MaxShapeTiles; i++) res.Add((x0 + i, y0)); return res; }   // too big: just over the cap
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) res.Add((x, y));
        return res;
    }

    private static bool Ints(string s, int n, out int[] v)
    {
        v = new int[n];
        var p = s.Split(',');
        if (p.Length != n) return false;
        for (int i = 0; i < n; i++) if (!int.TryParse(p[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v[i])) return false;
        return true;
    }

    // The report's places (§2.6): fixed lines first, then my objects (MineMax), others' stations (OtherMax), the nearest
    // ore of each kind (OreKinds), the nearest water (WaterMax), nearest first in each group, PlacesMax lines in all.
    // more: what did not fit, by group (ore, station, …).
    public const int PlacesChars = 900;        // §2.6 / 검수 보고 길이: the places list in characters (the rest counted in more)
    public static List<string> Pick(List<PlaceLine> lines, out Dictionary<string, int> more, int maxChars = PlacesChars)
    {
        var mo = new Dictionary<string, int>(); more = mo;
        var res = new List<string>();
        int used = 0;
        var byGroup = new Dictionary<string, List<PlaceLine>>();
        foreach (var l in lines) { if (!byGroup.TryGetValue(l.Group, out var g)) byGroup[l.Group] = g = new List<PlaceLine>(); g.Add(l); }
        foreach (var g in byGroup.Values) g.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        void Take(string group, int cap, bool perSub)
        {
            if (!byGroup.TryGetValue(group, out var g)) return;
            var subs = new HashSet<string>();
            int n = 0;
            foreach (var l in g)
            {
                int len = (l.Text ?? "").Length + 3;
                bool fits = res.Count < PlacesMax && n < cap && used + len <= maxChars && (!perSub || subs.Add(l.Sub ?? ""));
                if (fits) { res.Add(l.Text); n++; used += len; }
                else { string k = group == "mine" || group == "other" ? (l.Sub ?? group) : group; mo.TryGetValue(k, out int c); mo[k] = c + 1; }
            }
        }
        Take("fixed", 4, false);
        Take("mine", MineMax, false);
        Take("other", OtherMax, false);
        Take("ore", OreKinds, true);
        Take("water", WaterMax, false);
        return res;
    }

    // can_do.craft grouped by where it is made (§2.8): items in the order given (hand first, then stations nearest first),
    // each item once (its first place); in a group the ones with all materials first, then some, then none; at most
    // `max` in all, the rest counted in more.
    public static List<(string where, List<string> items)> CraftGroups(List<(string where, string item, int have)> rows, int max, out int more)
    {
        more = 0;
        var seen = new HashSet<string>();
        var order = new List<string>();
        var g = new Dictionary<string, List<(string text, int have, int i)>>();
        int idx = 0;
        foreach (var r in rows)
        {
            if (!seen.Add(r.item)) continue;
            if (!g.TryGetValue(r.where, out var l)) { g[r.where] = l = new List<(string, int, int)>(); order.Add(r.where); }
            l.Add((r.item, r.have, idx++));
        }
        var res = new List<(string, List<string>)>();
        int total = 0;
        foreach (var w in order)
        {
            var l = g[w];
            l.Sort((a, b) => a.have != b.have ? b.have.CompareTo(a.have) : a.i.CompareTo(b.i));   // have: 2 all, 1 some, 0 none
            var items = new List<string>();
            foreach (var x in l) { if (total < max) { items.Add(x.text); total++; } else more++; }
            if (items.Count > 0) res.Add((w, items));
        }
        return res;
    }

    // §2.5: the craft check's sentence when no station the AI knows makes the item. station: the station that makes it
    // (None/"" = not found); inBag: how many of that station the bag has; makeAt: where that station itself is made
    // ("" = not known); stationMats: its materials text.
    public static string CraftFact(string item, string station, int inBag, string makeAt, string stationMats)
    {
        if (string.IsNullOrEmpty(station)) return $"아는 제작법이 아님 '{item}' — 손 제작·보이는·기억한 제작대·가방 속 제작대 가운데 {item}을(를) 만드는 곳이 없음(game how로 만드는 곳을 찾아볼 수 있음)";
        if (inBag > 0) return $"{item}은(는) {station}에서 만듦 — 가방에 {station} {inBag}개, 놓인 {station}은 모름. 먼저 place {station}(장소)로 놓은 뒤 craft {item}";
        if (!string.IsNullOrEmpty(makeAt)) return $"{item}은(는) {station}에서 만듦 — {station}은(는) {makeAt}에서 만듦({stationMats}). 순서: craft {station} → place {station}(장소) → craft {item}";
        return $"{item}을(를) 만드는 제작대({station})를 만들 곳을 모름";
    }

    // §2.10: what placing on an edge tile does (Unity-free; the game side gives the tile's marks). "" when nothing.
    // gap: 1-based gap number (0 = no gap tile); openLeft: open tiles of that gap now (this one included); doorSpot: the
    // plan's door spot; doorSide: beside the door spot; walkable: the item is a walkable tile (floor, bridge); isDoor: the
    // item is a door; standOutside: the option's stand is off the inside; doorNow: an own edge door to come in by "(x,y)"
    // or null.
    public static string EdgeFact(int gap, int openLeft, bool doorSpot, bool doorSide, bool walkable, bool isDoor, bool standOutside, string doorNow)
    {
        if (walkable) return gap > 0 || doorSpot ? $"둘레 틈 칸 위 — 둘레는 그대로 열림" : "";
        if (doorSpot) return isDoor ? "둘레 문 자리에 문 달기" : "둘레 문 자리를 막음 — 문을 달 곳이 없어짐";
        if (gap > 0)
        {
            if (isDoor) return $"둘레 틈 {gap}에 문 — 양옆이 벽이어야 놓임";
            if (openLeft <= 1)
                return standOutside
                    ? (doorNow != null ? $"둘레 틈 {gap}의 마지막 칸 — 바깥에서 닫음, 둘레 문 {doorNow}로 드나듦" : $"둘레 틈 {gap}의 마지막 칸 — 바깥에서 닫음, 둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함")
                    : $"둘레 틈 {gap}의 마지막 칸 — 안쪽에서 닫음(둘레가 닫힘)";
            return $"둘레 틈 {gap}을(를) 메움(남은 틈 칸 {openLeft - 1})";
        }
        if (doorSide) return "둘레 문 옆 칸(문은 양옆이 벽일 때만 놓임)";
        return "";
    }
}
