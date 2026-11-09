// AIPlaceGrid — where an object can go next to the AI and what placing it there does to the walkable tiles around it
// (코어-안전지대 단계 1, HEAD instruction with the user's approval 2026-11-05). Unity-free so the sentences and the
// exclusion rule are checked offline (Tools/core-tests PlaceTests).
// Rule (same as the combat options, pol.combat-no-auto-dodge): only a physically impossible spot is left out — not
// known/walkable/free, a tile the game never takes (noPlace: the core's spawn pad 3x3, TileType.immune), the AI's own tile, a footprint the AI cannot reach,
// no reachable tile to stand on after placing, or a spot the game refused shortly before (temporary, kept apart). What a spot cuts (a passage,
// the way to an own field, water, a station or chest, a base radius) is stated as a fact on the option and the decision
// model chooses; it is never a reason to drop the spot.
// (Was AIPlaceSpot.Choose's filter: spots that cut a passage, a field/water/object approach, own plot or dug ground and,
// for a chest/pot at base, tiles outside the base radius were skipped.)
using System;
using System.Collections.Generic;

public sealed class PlaceGridInput
{
    public int n, loX, loY;            // window: tiles (loX + i, loY + j), i, j < n; index i * n + j
    public bool[] pass;                // known, walkable, no object, no water (the AI's own tile counts as walkable)
    public bool[] noPlace, plot, dug;  // tiles the game never takes (spawn pad, immune), own field tiles, dug ground (null: none)
    public bool[] water;               // known water (not lava) without an object (null: none)
    public bool[] noStand;             // tiles never used to stand on (the spawn pad: the game's path search does not
                                       // reach them, 단계 1 M1/M4e; recheck R1) (null: none)
    public bool[] door;                // own door tiles: the walk to a stand opens and passes them (재실측 N-f; never a stand,
                                       // never a footprint) (null: none)
    public bool footprintWalkable;     // the item is a walkable tile (floor, rug, bridge): placing it cuts no way and may
                                       // go under the AI (review F10)
    public bool onWater;               // the item may go on water (the game's placement flag of the item): water tiles are spots too
    public bool onLand = true;         // 실게임3 R-b: the item may go on ground (walkable) tiles; false for a bridge: water only
    public int meX, meY, sizeX = 1, sizeY = 1, searchR = 5;
    public float meFx, meFy;           // the AI's exact position (tile units)
    public readonly List<(int x, int y, string name)> targets = new List<(int, int, string)>();   // must stay reachable (named in the fact)
    public readonly HashSet<long> refused = new HashSet<long>();   // anchors the game refused shortly before (Key)
    public bool baseItem;              // a chest or pot: the base radius matters
    public int baseX, baseY;
    public float baseRadius = 10f;
    public string baseWord = "기지";
    // 제작대-놓기 §2.3: a place goal's area instead of the base (areaWord = the place; the radius fact says "반경 밖")
    public string areaWord;
    // 제작대-놓기 §2.10: the base edge plan's marks per window tile (null: no plan). edgeGap: 1-based gap number (0 none);
    // gapOpen: open tiles of each gap now; edgeDoorSpot / edgeDoorSide: the door spot / beside it; edgeInside: inside tiles;
    // itemIsDoor: the item is a door; edgeDoorNow: an own edge door to come in by "(x,y)" (null none)
    public int[] edgeGap;
    public readonly Dictionary<int, int> gapOpen = new Dictionary<int, int>();
    public bool[] edgeDoorSpot, edgeDoorSide, edgeInside;
    public bool itemIsDoor;
    public string edgeDoorNow;
    public static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}

public sealed class PlaceOption
{
    public int ax, ay, sx, sy, standX, standY;
    public float score, dist;
    public int pocket, bigPiece, plotOn, plotAdj, dugOn, waterOn;
    public bool cutsOut, outsideBase;
    public string edgeFact = "";   // 제작대-놓기 §2.10: what placing here does to the base edge ("" none)
    public int joins;              // 제작대-놓기 §2.10: walkable tiles across water the AI reaches after placing a bridge here
    public float baseDist;
    public readonly List<string> blocks = new List<string>();   // targets that become unreachable
    public string EffectKey;   // the kind of consequence (for picking one option per kind)
    public string Facts;       // the sentence on the option
    public bool Clean => EffectKey == "ok";
}

// 합치기 전 수정 1: the placing's press step (AIPlaceGrid.PressStep) — the settle of the door step (AISettle)
public sealed class PlacePressState : SettleState
{
    public float startAt = -1f;     // the first call for this stand (StandTimeout)
    public float pressDist = -1f;   // from the footprint tile at the press
}
public struct PlacePressOut
{
    public float mx, my;   // move direction (0,0 = stand)
    public bool press;     // standing still in the press window: press now
    public int nudge;      // 검수 M1: the move is a correction: the body moves it one server tick (AISettle.NudgeOut, TickNudge)
    public string fail;    // the stand was not stood on in time (null: going on)
    public string observation;  // elapsed execution fact while continuing to settle on the chosen stand
}

public static class AIPlaceGrid
{
    // 제작대-놓기 실게임 D2: one rule for "outside the area": the footprint's farthest tile from the centre past the
    // radius (the option's sentence and the place goal's count use it alike)
    public static bool OutsideArea(float farthest, float radius) => farthest > radius;
    public static float Farthest(int ax, int ay, int sx, int sy, int cx, int cy)
    {
        float bd = 0f;
        for (int fx = 0; fx < sx; fx++) for (int fy = 0; fy < sy; fy++) bd = Math.Max(bd, Dist(ax + fx, ay + fy, cx, cy));
        return bd;
    }

    public const int MaxOptions = 0; // compatibility: all consequence representatives are offered

    // The core's spawn pad (the round platform under the spawn point at the world origin): the game refuses placing on
    // its 3x3 (단계 1 live run M1, 5-단계1-측정.md: torches refused 3 presses on (0,-1), (1,-1), (-1,1), (1,1); (0,-2),
    // (2,0), (-3,0) took them; no immune tiles there). What makes the game refuse it (a pad object or tile in the
    // canNotBePlaceOnObjects list) was not read, so this is the measured area, not game data. Permanent, unlike the
    // temporary refusals.
    public const int SpawnPadHalf = 1;
    public static bool SpawnPad(int x, int y) => Math.Abs(x) <= SpawnPadHalf && Math.Abs(y) <= SpawnPadHalf;

    // A tile to stand on for placing on (ax, ay, sx x sy): one the AI walks to without crossing the footprint (4-way
    // walk over pass, the footprint taken out), nearest by that walk; side neighbours before diagonal ones at the same
    // walk length. `prefer` (the stand the chosen option stated) wins while it is still reachable that way. -1 when
    // there is none. Replaces the straight-line pick that could stand on the far side of a corridor tile (review F1).
    // 재실측 N-e: the stand of a placing that is reached through the tile to place (recheck N2), by its side of the base
    public static string ThroughStandNote(bool outside) => outside ? "바깥, 놓을 칸을 지나 나가서 놓음" : "안쪽, 놓을 칸을 지나 들어가서 놓음";

    public static bool StandFor(PlaceGridInput g, int ax, int ay, int sx, int sy, HashSet<long> bad, long? prefer, out int standX, out int standY, bool crossFootprint = false)
        => StandSearch(g, ax, ay, sx, sy, bad, prefer, out standX, out standY, crossFootprint, false);

    // 합치기 전 수정 1 (검수 D5): a diagonal neighbour the walk reaches when no side one is (the fact of the left-out
    // diagonal: the game itself would take a press from there, the rule is from the live run)
    public static bool DiagonalStand(PlaceGridInput g, int ax, int ay, int sx, int sy, HashSet<long> bad, out int standX, out int standY)
        => StandSearch(g, ax, ay, sx, sy, bad, null, out standX, out standY, false, true);
    public static string DiagonalLeftOutNote(int x, int y) => $"대각 칸 ({x},{y})은 갈 수 있으나 설 칸으로 쓰지 않음(대각 자리에서 누른 놓기가 실측에서 2번 다 거절됨, 20-실측 run2 t=267.5·335.9; 게임의 놓기 범위로는 대각도 닿지만 그 거절의 원인은 못 가림)";

    private static bool StandSearch(PlaceGridInput g, int ax, int ay, int sx, int sy, HashSet<long> bad, long? prefer, out int standX, out int standY, bool crossFootprint, bool diagonalOnly)
    {
        standX = standY = 0;
        int n = g.n;
        var F = new HashSet<long>();
        for (int fx = 0; fx < sx; fx++) for (int fy = 0; fy < sy; fy++) F.Add(PlaceGridInput.Key(ax + fx, ay + fy));
        var dist = new int[n * n];
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;
        if (!In(g.meX, g.meY, g)) return false;
        var q = new Queue<int>();
        int s0 = Idx(g.meX, g.meY, g);
        dist[s0] = 0; q.Enqueue(s0);
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = g.loX + i / n, y = g.loY + i % n;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!In(nx, ny, g) || (!crossFootprint && F.Contains(PlaceGridInput.Key(nx, ny)))) continue;
                int j = Idx(nx, ny, g);
                if (dist[j] >= 0 || !Walk(g, j) && !(g.door != null && g.door[j])) continue;   // N-f: through an own door too
                dist[j] = dist[i] + 1; q.Enqueue(j);
            }
        }
        int best = int.MaxValue; bool found = false;
        for (int fx = -1; fx <= sx; fx++)
            for (int fy = -1; fy <= sy; fy++)
            {
                // 실측결함수정3 N7: a side neighbour of the footprint only — a diagonal stand put the body 1.2-1.3 off the
                // tile and the game took none of 3 presses (run2 t=267.5, t=335.9); the press is made only in reach from
                // that side (PressFrom)
                bool corner = (fx < 0 || fx >= sx) && (fy < 0 || fy >= sy);
                if (corner != diagonalOnly) continue;
                int x = ax + fx, y = ay + fy;
                long key = PlaceGridInput.Key(x, y);
                if (F.Contains(key) || !In(x, y, g) || (bad != null && bad.Contains(key))) continue;
                if (g.noStand != null && g.noStand[Idx(x, y, g)] || g.door != null && g.door[Idx(x, y, g)]) continue;
                int d = dist[Idx(x, y, g)];
                if (d < 0) continue;
                int score = d;
                if (prefer.HasValue && prefer.Value == key) score = -1;
                if (score < best) { best = score; standX = x; standY = y; found = true; }
            }
        return found;
    }

    // Every physically possible spot within searchR of the AI, with its facts; free = footprints looked at, out-of-reach
    // ones not counted.
    public static List<PlaceOption> All(PlaceGridInput g, out int looked, out int impossible)
    {
        var res = new List<PlaceOption>();
        looked = 0; impossible = 0;
        int n = g.n;
        var pass = new bool[g.pass.Length];
        for (int i = 0; i < pass.Length; i++) pass[i] = Walk(g, i);   // 실측결함수정3 N6: the spawn pad is walked off
        pass[Idx(g.meX, g.meY, g)] = true;
        var before = Label(pass, n, null, g);
        int mainB = before[Idx(g.meX, g.meY, g)];
        var reachB = new List<(int x, int y, string name)>();
        foreach (var t in g.targets) if (In(t.x, t.y, g) && Reachable(t.x, t.y, pass, before, mainB, g, null)) reachB.Add(t);

        for (int ox = -g.searchR; ox <= g.searchR; ox++)
            for (int oy = -g.searchR; oy <= g.searchR; oy++)
            {
                int ax = g.meX + ox, ay = g.meY + oy;
                looked++;
                if (g.refused.Contains(PlaceGridInput.Key(ax, ay))) { impossible++; continue; }
                var F = new HashSet<long>();
                bool ok = true; int plotOn = 0, dugOn = 0, waterOn = 0;
                for (int fx = 0; fx < g.sizeX && ok; fx++)
                    for (int fy = 0; fy < g.sizeY && ok; fy++)
                    {
                        int x = ax + fx, y = ay + fy;
                        if (!In(x, y, g)) { ok = false; break; }
                        int i = Idx(x, y, g);
                        bool onW = g.onWater && g.water != null && g.water[i];   // water is not walkable: its stand decides if it is reachable
                        if (!(onW || g.onLand && pass[i] && before[i] == mainB) || (x == g.meX && y == g.meY && !g.footprintWalkable) || (g.noPlace != null && g.noPlace[i])) { ok = false; break; }
                        if (onW) waterOn++;
                        if (g.plot != null && g.plot[i]) plotOn++;
                        if (g.dug != null && g.dug[i]) dugOn++;
                        F.Add(PlaceGridInput.Key(x, y));
                    }
                if (!ok) { impossible++; continue; }
                var after = g.footprintWalkable ? before : Label(pass, n, F, g);   // a walkable tile item cuts nothing (F10)
                int meComp = after[Idx(g.meX, g.meY, g)];
                // walkable tiles around the footprint
                var ring = new List<(int x, int y)>();
                for (int fx = 0; fx < g.sizeX; fx++)
                    for (int fy = 0; fy < g.sizeY; fy++)
                        for (int k = 0; k < 4; k++)
                        {
                            int rx = ax + fx + Dx[k], ry = ay + fy + Dy[k];
                            if (!In(rx, ry, g) || F.Contains(PlaceGridInput.Key(rx, ry)) || !pass[Idx(rx, ry, g)] || ring.Contains((rx, ry))) continue;
                            if (g.noStand != null && g.noStand[Idx(rx, ry, g)]) continue;   // R1: never stand on the spawn pad
                            ring.Add((rx, ry));
                        }
                // a stand: a walkable tile next to the footprint the AI still reaches after placing, nearest to the AI
                int sX = 0, sY = 0; float sd = float.MaxValue;
                foreach (var r in ring)
                    if (after[Idx(r.x, r.y, g)] == meComp)
                    {
                        float d = Dist(g.meFx, g.meFy, r.x, r.y);
                        if (d < sd) { sd = d; sX = r.x; sY = r.y; }
                    }
                if (sd == float.MaxValue) { impossible++; continue; }

                var o = new PlaceOption { ax = ax, ay = ay, sx = g.sizeX, sy = g.sizeY, standX = sX, standY = sY, dist = sd, plotOn = plotOn, dugOn = dugOn, waterOn = waterOn };
                // pieces the footprint splits off: a small dead end, a larger space, or a way that leads on out of the window
                // 검수 F2: a walkable tile item (floor, bridge) splits nothing off; a space across water was apart before
                // (the bridge's join is said below), not cut by it
                var seen = new HashSet<int>();
                foreach (var r in ring)
                {
                    if (g.footprintWalkable) break;
                    int c = after[Idx(r.x, r.y, g)];
                    if (c == meComp || !seen.Add(c)) continue;
                    int sz = 0; bool border = false;
                    for (int i = 0; i < n * n; i++)
                        if (after[i] == c) { sz++; int ix = i / n, iy = i % n; if (ix == 0 || iy == 0 || ix == n - 1 || iy == n - 1) border = true; }
                    if (border) o.cutsOut = true;
                    else if (sz > 12) o.bigPiece += sz;
                    else o.pocket += sz;
                }
                foreach (var t in reachB) if (!Reachable(t.x, t.y, pass, after, meComp, g, g.footprintWalkable ? null : F)) o.blocks.Add($"{t.name}({t.x},{t.y})");
                int open = 0;
                foreach (var f in F)
                {
                    int fx = (int)(f >> 32), fy = (int)(uint)f;
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++)
                        {
                            int qx = fx + x, qy = fy + y;
                            if (F.Contains(PlaceGridInput.Key(qx, qy)) || !In(qx, qy, g)) continue;
                            int qi = Idx(qx, qy, g);
                            if (g.plot != null && g.plot[qi]) o.plotAdj++;
                            if (pass[qi]) open++;
                        }
                }
                if (g.baseItem)
                {
                    float bd = 0f;
                    for (int fx = 0; fx < g.sizeX; fx++)
                        for (int fy = 0; fy < g.sizeY; fy++)
                            bd = Math.Max(bd, Dist(ax + fx, ay + fy, g.baseX, g.baseY));
                    o.baseDist = bd;
                    // 실게임 D2: a place goal's area uses one rule with its sentence and its count (OutsideArea); the base keeps
                    // its own margin (생활-기지: a chest within a tile of the radius is not counted as the base's)
                    o.outsideBase = g.areaWord != null ? OutsideArea(bd, g.baseRadius) : bd > g.baseRadius - 1f;
                }
                o.score = sd + 0.6f * o.plotAdj - 0.15f * open + (o.pocket > 0 ? 3f + 0.3f * o.pocket : 0f) + (g.baseItem ? 0.1f * Dist(ax, ay, g.baseX, g.baseY) : 0f);
                o.edgeFact = EdgeFactOf(g, F, sX, sY);
                // 제작대-놓기 §2.10: a walkable tile item on water (a bridge) may join a space across it
                if (g.footprintWalkable && waterOn > 0)
                {
                    var pass2 = (bool[])pass.Clone();
                    foreach (var f in F) { int fx2 = (int)(f >> 32), fy2 = (int)(uint)f; if (In(fx2, fy2, g)) pass2[Idx(fx2, fy2, g)] = true; }
                    var lab2 = Label(pass2, n, null, g);
                    int me2 = lab2[Idx(g.meX, g.meY, g)], gained = 0;
                    for (int i = 0; i < n * n; i++) if (lab2[i] == me2 && pass[i] && before[i] != mainB) gained++;
                    o.joins = gained;
                }
                Describe(o, g);
                res.Add(o);
            }
        return res;
    }

    private static void Describe(PlaceOption o, PlaceGridInput g)
    {
        var keys = new List<string>();
        var facts = new List<string>();
        if (o.cutsOut) { keys.Add("cut-out"); facts.Add("여기 놓으면 이 너머로 이어지는 길이 막힘(보이는 범위 밖까지 이어진 길)"); }
        if (o.bigPiece > 0) { keys.Add("cut-big"); facts.Add($"여기 놓으면 옆 공간 {o.bigPiece}칸으로 가는 길이 막힘"); }
        if (o.blocks.Count > 0) { keys.Add("block:" + string.Join(",", o.blocks)); facts.Add($"여기 놓으면 {string.Join(", ", o.blocks)}로 가는 길이 막힘"); }
        if (o.pocket > 0) { keys.Add("pocket"); facts.Add($"막다른 {o.pocket}칸만 막힘"); }
        if (o.plotOn > 0) { keys.Add("plot-on"); facts.Add($"내 밭 {o.plotOn}칸 위"); }
        else if (o.plotAdj > 0) facts.Add($"밭 옆 {o.plotAdj}칸");
        if (o.dugOn > 0) { keys.Add("dug"); facts.Add("판 땅 위"); }
        if (o.waterOn > 0) { keys.Add("water"); facts.Add($"물 {o.waterOn}칸을 메움"); }
        if (o.outsideBase) { keys.Add("outside-base"); facts.Add(g.areaWord != null ? $"{g.areaWord} 중심에서 {o.baseDist:F1}칸 — 반경 {g.baseRadius:F1}칸 밖" : $"{g.baseWord} 중심에서 {o.baseDist:F0}칸 — 기지 반경 {g.baseRadius:F0}칸 밖이라 기지 것으로 세지 않음"); }
        if (!string.IsNullOrEmpty(o.edgeFact)) { keys.Add("edge:" + o.edgeFact); facts.Add(o.edgeFact); }   // 제작대-놓기 §2.10: never a reason to drop the spot
        if (o.joins > 0) { keys.Add("joins"); facts.Add($"여기 놓으면 물 건너 {o.joins}칸 공간과 이어짐"); }
        o.EffectKey = keys.Count == 0 ? "ok" : string.Join("|", keys);
        if (o.EffectKey == "ok") facts.Insert(0, "길을 끊지 않음");
        o.Facts = string.Join(", ", facts);
    }

    // 제작대-놓기 §2.10: the edge fact of a footprint (the first footprint tile with an edge mark; the stand's side)
    private static string EdgeFactOf(PlaceGridInput g, HashSet<long> F, int standX, int standY)
    {
        if (g.edgeGap == null) return "";
        bool standOut = In(standX, standY, g) && g.edgeInside != null && !g.edgeInside[Idx(standX, standY, g)];
        foreach (var f in F)
        {
            int x = (int)(f >> 32), y = (int)(uint)f;
            if (!In(x, y, g)) continue;
            int i = Idx(x, y, g);
            int gap = g.edgeGap[i];
            bool spot = g.edgeDoorSpot != null && g.edgeDoorSpot[i], side = g.edgeDoorSide != null && g.edgeDoorSide[i];
            if (gap == 0 && !spot && !side) continue;
            g.gapOpen.TryGetValue(gap, out int open);
            return AIPlaces.EdgeFact(gap, open, spot, side, g.footprintWalkable, g.itemIsDoor, standOut, g.edgeDoorNow);
        }
        return "";
    }

    // The options shown to the decision model: the best (lowest score) spot of each kind of consequence, a clean spot
    // first. Every consequence representative is kept; the legacy max argument no longer cuts choices.
    public const int EdgeKeep = 0;   // legacy field; every consequence is retained
    public static List<PlaceOption> Pick(List<PlaceOption> all, int max = MaxOptions)
    {
        var best = new Dictionary<string, PlaceOption>();
        foreach (var o in all)
            if (!best.TryGetValue(o.EffectKey, out var b) || o.score < b.score) best[o.EffectKey] = o;
        var list = new List<PlaceOption>(best.Values);
        list.Sort((a, b) => a.Clean != b.Clean ? (a.Clean ? -1 : 1) : a.score.CompareTo(b.score));
        return list;
    }

    // One spot without a decision (a placement no option was built for, e.g. the test command without a tile): the spot
    // the old filter would have chosen when there is one (lowest score among spots that cut nothing but a small dead end
    // — same pick as before), otherwise the lowest score with the consequences weighed in; nothing is left out.
    public static PlaceOption Best(List<PlaceOption> all)
    {
        PlaceOption best = null; float bs = float.MaxValue; bool bestOld = false;
        foreach (var o in all)
        {
            bool old = OldFilterKept(o);
            float s = o.score + (o.cutsOut ? 30f : 0f) + (o.bigPiece > 0 ? 20f : 0f) + 15f * o.blocks.Count + (o.plotOn > 0 ? 10f : 0f) + (o.dugOn > 0 ? 5f : 0f) + (o.outsideBase ? 10f : 0f);
            if (old && !bestOld || old == bestOld && s < bs) { bs = s; best = o; bestOld = old; }
        }
        return best;
    }

    // what AIPlaceSpot.Choose kept before 단계 1: no cut but a small dead end, no unreachable target, not on an own plot
    // or dug ground, a base item at base inside the radius
    public static bool OldFilterKept(PlaceOption o) => o.waterOn == 0 && !o.cutsOut && o.bigPiece == 0 && o.blocks.Count == 0 && o.plotOn == 0 && o.dugOn == 0 && !o.outsideBase;

    private static readonly int[] Dx = { 1, -1, 0, 0 }, Dy = { 0, 0, 1, -1 };
    private static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    private static int Idx(int x, int y, PlaceGridInput g) => (x - g.loX) * g.n + (y - g.loY);
    private static bool In(int x, int y, PlaceGridInput g) => x >= g.loX && y >= g.loY && x < g.loX + g.n && y < g.loY + g.n;

    // 실측결함수정3 N6: a tile the AI walks on — walkable, or the core's spawn pad (never a stand, noStand, but the AI stands
    // on it after a respawn and walks off it: the walk's pad hop). From the pad the stand search reached nothing: a repair
    // chosen at the core failed in 0.0 s "설 칸이 없음" (run2 t=226.6); the same tile got a stand from (-3,0) later.
    public static bool Walk(PlaceGridInput g, int i) => g.pass[i] || g.noStand != null && g.noStand[i];

    // 실측결함수정3 N7: the press is made from the stand side in reach — the body in the footprint's band on one axis (a
    // side neighbour, not a diagonal), its nearest footprint tile centre at most PressReach off, and off the footprint
    // (0.35 from its box). The stand centre is 1.0 off.
    // 합치기 전 수정 1 (22-짧은확인): PressReach 1.1 -> 1.2. The door re-hang from the north stopped 1.22 off and never
    // pressed (2 of 2): the walk ends within 0.35 of the stand and a step of the body is ~0.28 (a server tick), so a
    // 0.25-wide window (0.85-1.1) was jumped over and the old "step onto the centre within 0.06" swung round it. Records:
    // taken from 1.13 (20-실측 t=1058) and 0.94 (22 t=788.8); the 1.13 "refusals" (t=992 / 1033) came with other causes
    // likely (a grave beside the tile was refused 3 times at 0.94, 22 t=716.1). The game's own limit is looser: the aimed
    // point is clamped to a box of (size + 1.5) round the body and rounded (PlacementHandler.
    // FindPlaceablePositionFromMouseOrJoystick: 1.25 for a 1x1); 1.2 keeps the aim inside that box (never clamped). The
    // press is now made only with the body standing still in the window (PressStep: AISettle, as the door step).
    public const float PressReach = 1.2f;
    public const float PressNear = 0.35f;   // off the footprint's box by at least this (the game: a player inside the tile area less 0.1 blocks it)
    public static bool PressFrom(int ax, int ay, int sx, int sy, float meX, float meY)
    {
        float lox = ax - 0.5f, hix = ax + sx - 0.5f, loy = ay - 0.5f, hiy = ay + sy - 0.5f;
        float qx = Math.Max(lox, Math.Min(hix, meX)), qy = Math.Max(loy, Math.Min(hiy, meY));
        if ((float)Math.Sqrt((meX - qx) * (meX - qx) + (meY - qy) * (meY - qy)) < PressNear) return false;   // on the footprint
        bool bandX = meX >= ax - 0.35f && meX <= ax + sx - 1 + 0.35f, bandY = meY >= ay - 0.35f && meY <= ay + sy - 1 + 0.35f;
        if (!bandX && !bandY) return false;   // a diagonal
        float cx = Math.Max(ax, Math.Min(ax + sx - 1, meX)), cy = Math.Max(ay, Math.Min(ay + sy - 1, meY));
        return (float)Math.Sqrt((meX - cx) * (meX - cx) + (meY - cy) * (meY - cy)) <= PressReach;
    }

    // 합치기 전 수정 1 (검수 D4): the placing's press, as the door step's: to the stand centre, standing still there,
    // and the press only with the body still in the press window (PressFrom on the stand's side; AISettle). Before, the
    // press came at the first frame in reach while walking, and at the stand a full-speed step toward the centre "until
    // within 0.06" swung round it (0.28 a server tick, a tick of input delay: (-4.00,-4.78) / -5.06 / -5.34 over and over,
    // 22-짧은확인 t=615.0-616.6) until "설 칸에 왔지만 닿지 않음" after 1.2 s.
    public const float StandTimeout = 3f;
    public static PlacePressOut PressStep(PlacePressState s, int ax, int ay, int sx, int sy, int standX, int standY, float meX, float meY, bool sideOk, float now)
    {
        var o = new PlacePressOut();
        if (s.startAt < 0f) s.startAt = now;
        AISettle.Track(s, meX, meY, now);
        Func<float, float, bool> win = (x, y) => sideOk && PressFrom(ax, ay, sx, sy, x, y);
        // the footprint tile nearest the stand, and the axis from it to the stand ("too far" is past the stand centre)
        float cx = Math.Max(ax, Math.Min(ax + sx - 1, standX)), cy = Math.Max(ay, Math.Min(ay + sy - 1, standY));
        float dx = standX - cx, dy = standY - cy, l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l > 1e-3f) { dx /= l; dy /= l; }
        if (AISettle.Hold(s, ref o.mx, ref o.my, win, standX, standY, dx, dy, cx, cy, meX, meY, now)) { o.press = true; s.pressDist = AISettle.Dist(meX, meY, cx, cy); return o; }
        o.nudge = AISettle.NudgeOut(s, now);
        if (now - s.startAt > StandTimeout)
            o.observation = $"설 칸 ({standX},{standY})에 {StandTimeout:F0}초 동안 멈춰 서지 못함(몸이 설 칸 중심에서 {AISettle.Dist(meX, meY, standX, standY):F2}칸, 놓을 칸에서 {AISettle.Dist(meX, meY, cx, cy):F2}칸 — 무언가 막음)";
        return o;
    }

    // 합치기 전 수정 1: the game refuses a tile when a collider overlaps a 0.9 box at the tile's centre (PlacementHandler.
    // CanPlaceObjectAtPosition: CastCollider of a 0.9 box, CheckEntityIsBlockingPlacement — a placed object, a grave, an
    // enemy; not a tile, pet, minion or critter). A grave 0.9 off the door tile (its centre (-4.1,-6.9)) took 3 presses
    // from 0.94 (22-짧은확인 t=716.1); with it picked up the same press placed the door (t=788.8). The game side gives the
    // colliders' boxes; this is the overlap (a hundredth of a tile of contact is not counted).
    public const float PlaceBoxHalf = 0.45f;
    public static bool OverlapsPlaceBox(int x, int y, float minX, float minY, float maxX, float maxY)
        => maxX > x - PlaceBoxHalf + 0.01f && minX < x + PlaceBoxHalf - 0.01f && maxY > y - PlaceBoxHalf + 0.01f && minY < y + PlaceBoxHalf - 0.01f;
    public static string OverlapNote(string what, float ox, float oy, int x, int y)
        => $"놓을 칸 ({x},{y})에 {what}({ox:F1},{oy:F1})의 몸이 겹침 — 게임은 놓을 칸 가운데 0.9칸 상자에 물체가 겹치면 놓지 않음(먼저 치워야 놓임)";

    // 4-neighbour pieces of walkable tiles (excluding `cut`); 0 = not walkable
    private static int[] Label(bool[] pass, int n, HashSet<long> cut, PlaceGridInput g)
    {
        var lab = new int[n * n];
        int next = 0;
        var q = new Queue<int>();
        for (int s = 0; s < n * n; s++)
        {
            if (!pass[s] || lab[s] != 0) continue;
            if (cut != null && cut.Contains(PlaceGridInput.Key(g.loX + s / n, g.loY + s % n))) continue;
            next++; lab[s] = next; q.Enqueue(s);
            while (q.Count > 0)
            {
                int i = q.Dequeue(); int x = i / n, y = i % n;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + Dx[k], ny = y + Dy[k];
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                    int j = nx * n + ny;
                    if (!pass[j] || lab[j] != 0) continue;
                    if (cut != null && cut.Contains(PlaceGridInput.Key(g.loX + nx, g.loY + ny))) continue;
                    lab[j] = next; q.Enqueue(j);
                }
            }
        }
        return lab;
    }

    // t has a walkable 8-neighbour (not t, not in `cut`) in piece `comp`
    private static bool Reachable(int tx, int ty, bool[] pass, int[] lab, int comp, PlaceGridInput g, HashSet<long> cut)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                int qx = tx + x, qy = ty + y;
                if (!In(qx, qy, g)) continue;
                if (cut != null && cut.Contains(PlaceGridInput.Key(qx, qy))) continue;
                int i = Idx(qx, qy, g);
                if (pass[i] && lab[i] == comp) return true;
            }
        return false;
    }
}
