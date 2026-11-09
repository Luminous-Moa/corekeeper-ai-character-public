// P1 — 코어-안전지대 단계 1: place spots (Core/AIPlaceGrid). Only physically impossible spots are left out; what a spot
// cuts is a fact on the option; the spot chosen without a decision (and so the chest / pot / station placements of the
// test command) is the same one the old AIPlaceSpot.Choose filter chose, checked against a copy of that filter on
// random rooms.
using System;
using System.Collections.Generic;

public static class PlaceTests
{
    // a map: '#' wall, '.' floor, 'A' the AI, 'I' a tile the game never takes (noPlace), 'O' object (blocked, a target), 'P' own plot, 'D' dug
    static PlaceGridInput Grid(string[] rows, int searchR = 5)
    {
        int h = rows.Length, w = rows[0].Length, n = Math.Max(h, w);
        var g = new PlaceGridInput { n = n, loX = 0, loY = 0, pass = new bool[n * n], noPlace = new bool[n * n], plot = new bool[n * n], dug = new bool[n * n], searchR = searchR };
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                int y = h - 1 - r; char c = rows[r][x]; int i = x * n + y;
                g.pass[i] = c != '#' && c != 'O' && c != 'W';
                g.noPlace[i] = c == 'I';
                g.plot[i] = c == 'P';
                g.dug[i] = c == 'D';
                if (g.water == null) g.water = new bool[n * n];
                g.water[i] = c == 'W';
                if (c == 'A') { g.meX = x; g.meY = y; g.meFx = x; g.meFy = y; }
                if (c == 'O') g.targets.Add((x, y, "WoodenChest"));
                if (c == 'P') g.targets.Add((x, y, "내 밭"));
            }
        return g;
    }

    static PlaceOption At(List<PlaceOption> l, int x, int y) => l.Find(o => o.ax == x && o.ay == y);

    public static void Run()
    {
        // 1. a room with a one-tile corridor leading out of the window on the right
        var g1 = Grid(new[]
        {
            "#############",
            "#.........###",
            "#.........###",
            "#....A.......",
            "#.........###",
            "#.........###",
            "#############",
        });
        var all1 = AIPlaceGrid.All(g1, out int looked1, out int imp1);
        var corridor = At(all1, 10, 3);
        Program.Check(corridor != null && corridor.cutsOut && corridor.Facts.Contains("이 너머로 이어지는 길이 막힘"),
            $"1a. the corridor tile is an option with the fact, not dropped: {(corridor == null ? "missing" : corridor.Facts)}");
        Program.Check(At(all1, 5, 3) == null, "1b. the AI's own tile is not an option");
        Program.Check(At(all1, 0, 3) == null, "1c. a wall tile is not an option");
        var open1 = At(all1, 3, 4);
        Program.Check(open1 != null && open1.Clean && open1.Facts == "길을 끊지 않음", $"1d. an open room tile: clean ({open1?.Facts})");
        var pick1 = AIPlaceGrid.Pick(all1);
        Program.Check(pick1.Count >= 2 && pick1[0].Clean && pick1.Exists(o => o.cutsOut), $"1e. the options: a clean spot first and the cutting spot kept as its own kind ({string.Join(" | ", pick1.ConvertAll(o => o.EffectKey))})");
        Program.Check(AIPlaceGrid.Best(all1).Clean, "1f. without a decision a clean spot is chosen");

        // 2. a chest reachable only through one tile; immune tiles; the plot
        var g2 = Grid(new[]
        {
            "#########",
            "#O#.....#",
            "#.#..A..#",
            "#...III.#",
            "#.PP....#",
            "#########",
        });
        var all2 = AIPlaceGrid.All(g2, out _, out int imp2);
        Program.Check(At(all2, 4, 2) == null && At(all2, 5, 2) == null && At(all2, 6, 2) == null, "2a. tiles the game never takes (noPlace) are not options");
        var gate = At(all2, 1, 3);
        Program.Check(gate != null && gate.blocks.Exists(b => b.StartsWith("WoodenChest(1,4)")) && gate.Facts.Contains("WoodenChest(1,4)로 가는 길이 막힘"),
            $"2b. the only way to the chest: an option that says it blocks the chest ({gate?.Facts})");
        var onPlot = At(all2, 2, 1);
        Program.Check(onPlot != null && onPlot.plotOn == 1 && onPlot.Facts.Contains("내 밭 1칸 위"), $"2c. an own plot tile is an option with the fact ({onPlot?.Facts})");
        Program.Check(!AIPlaceGrid.Best(all2).blocks.Exists(b => b.StartsWith("WoodenChest")) && AIPlaceGrid.Best(all2).plotOn == 0, "2d. without a decision neither the chest's way nor the plot is chosen");

        // 3. refused anchors are left out; a base item outside the radius gets the fact
        var g3 = Grid(new[]
        {
            "###########",
            "#.........#",
            "#....A....#",
            "#.........#",
            "###########",
        });
        g3.refused.Add(PlaceGridInput.Key(4, 2));
        g3.baseItem = true; g3.baseX = 1; g3.baseY = 2; g3.baseRadius = 4f;
        var all3 = AIPlaceGrid.All(g3, out _, out _);
        Program.Check(At(all3, 4, 2) == null, "3a. a spot the game refused is not an option");
        var far3 = At(all3, 9, 2);
        Program.Check(far3 != null && far3.outsideBase && far3.Facts.Contains("기지 반경 4칸 밖"), $"3b. outside the base radius: an option with the fact ({far3?.Facts})");

        // 3c. water: a spot only for an item the game lets go on water, with the fact
        var rowsW = new[] { "#######", "#..A..#", "#.....#", "#WWWWW#", "#######" };
        var gw = Grid(rowsW);
        Program.Check(AIPlaceGrid.All(gw, out _, out _).TrueForAll(o => o.waterOn == 0), "3c. water is not a spot for an item that cannot go on water");
        gw.onWater = true;
        var w1 = AIPlaceGrid.All(gw, out _, out _).Find(o => o.waterOn > 0);
        Program.Check(w1 != null && w1.Facts.Contains("물 1칸을 메움"), $"3d. water is a spot for a water-placeable item, with the fact ({w1?.Facts})");

        // 5. the spawn pad 3x3 at the origin is never an option (review F2), the tiles around it are
        {
            int n = 13;
            var g5 = new PlaceGridInput { n = n, loX = -6, loY = -6, pass = new bool[n * n], noPlace = new bool[n * n], meX = 0, meY = -3, meFx = 0, meFy = -3 };
            for (int x = -6; x <= 6; x++)
                for (int y = -6; y <= 6; y++)
                {
                    int i = (x + 6) * n + (y + 6);
                    g5.pass[i] = Math.Abs(x) <= 5 && Math.Abs(y) <= 5;
                    g5.noPlace[i] = AIPlaceGrid.SpawnPad(x, y);
                }
            var all5 = AIPlaceGrid.All(g5, out _, out _);
            Program.Check(!all5.Exists(o => AIPlaceGrid.SpawnPad(o.ax, o.ay)) && At(all5, 0, -2) != null && At(all5, 2, 0) != null && At(all5, -2, 1) != null,
                $"5. the spawn pad 3x3 is never an option; (0,-2), (2,0), (-2,1) are ({all5.Count} options)");
        }

        // 6. the stand (review F1): reached without crossing the footprint, not the straight-line nearest one
        {
            // corridor y=0 x=1..5, a connector (1,1), a top row y=2 x=1..5, the AI at (5,2); (5,1) is a wall. Placing on
            // (4,0): (5,0) is 2 tiles away in a straight line but only reachable through (4,0); (3,0) is the stand.
            int n = 8;
            var g6 = new PlaceGridInput { n = n, loX = 0, loY = 0, pass = new bool[n * n], meX = 5, meY = 2, meFx = 5, meFy = 2 };
            foreach (var t in new[] { (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (1, 1), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2) }) g6.pass[t.Item1 * n + t.Item2] = true;
            bool ok6 = AIPlaceGrid.StandFor(g6, 4, 0, 1, 1, null, null, out int sx6, out int sy6);
            Program.Check(ok6 && sx6 == 3 && sy6 == 0, $"6a. the stand on the AI's side of a corridor tile: ({sx6},{sy6})");
            bool okP = AIPlaceGrid.StandFor(g6, 4, 0, 1, 1, null, PlaceGridInput.Key(5, 0), out int px6, out int py6);
            Program.Check(okP && px6 == 3 && py6 == 0, $"6b. an option's stand that is only reachable through the footprint is not used: ({px6},{py6})");
            var bad = new HashSet<long> { PlaceGridInput.Key(3, 0) };
            Program.Check(!AIPlaceGrid.StandFor(g6, 4, 0, 1, 1, bad, null, out _, out _), "6c. no other stand without crossing: none (fails instead of walking through)");
            var gr = Grid(new[] { "#######", "#.....#", "#..A..#", "#.....#", "#######" });
            bool okR = AIPlaceGrid.StandFor(gr, 4, 3, 1, 1, null, PlaceGridInput.Key(4, 2), out int rx, out int ry);
            Program.Check(okR && rx == 4 && ry == 2, $"6d. the option's stand is used while it is reachable (side (4,2)): ({rx},{ry})");
            // 실측결함수정3 N7: a diagonal is never a stand, also when the option named it (it was (5,2) here before)
            bool okD = AIPlaceGrid.StandFor(gr, 4, 3, 1, 1, null, PlaceGridInput.Key(5, 2), out int dx6, out int dy6);
            Program.Check(okD && !(dx6 == 5 && dy6 == 2) && (dx6 == 4 || dy6 == 3), $"6d2. a diagonal stand the option named is not used; a side one is: ({dx6},{dy6})");
            // every option's own stand passes StandFor unchanged (the facts and the walk agree)
            var allR = AIPlaceGrid.All(gr, out _, out _);
            bool sameStand = allR.TrueForAll(o => AIPlaceGrid.StandFor(gr, o.ax, o.ay, o.sx, o.sy, null, PlaceGridInput.Key(o.standX, o.standY), out int ox, out int oy) && ox == o.standX && oy == o.standY);
            Program.Check(sameStand, $"6e. each option's stated stand is the one the skill walks to ({allR.Count} options)");
        }

        // 7. recheck R1: a tile marked noStand (the spawn pad) is never a stand; review F10: a walkable tile item (floor)
        // cuts no way and may go on the AI's own tile
        {
            var g7 = Grid(new[] { "#######", "#.....#", "#..A..#", "#.....#", "#######" });
            g7.noStand = new bool[g7.n * g7.n];
            g7.noStand[2 * g7.n + 2] = true; g7.noStand[4 * g7.n + 2] = true;   // (2,2) and (4,2): left and right of the AI
            var all7 = AIPlaceGrid.All(g7, out _, out _);
            Program.Check(!all7.Exists(o => (o.standX == 2 && o.standY == 2) || (o.standX == 4 && o.standY == 2)), "7a. a noStand tile is never an option's stand (R1)");
            bool sf = AIPlaceGrid.StandFor(g7, 3, 1, 1, 1, null, PlaceGridInput.Key(2, 2), out int s7x, out int s7y);
            Program.Check(sf && !(s7x == 2 && s7y == 2), $"7b. StandFor skips a noStand tile even when the option preferred it: ({s7x},{s7y})");

            // a corridor tile: placing a wall there cuts; a floor there cuts nothing
            var g7w = Grid(new[] { "#########", "#...#####", "#.A......", "#...#####", "#########" });
            var wall = AIPlaceGrid.All(g7w, out _, out _).Find(o => o.ax == 5 && o.ay == 2);
            g7w.footprintWalkable = true;
            var floorAll = AIPlaceGrid.All(g7w, out _, out _);
            var floor = floorAll.Find(o => o.ax == 5 && o.ay == 2);
            Program.Check(wall != null && wall.cutsOut && floor != null && !floor.cutsOut && floor.Clean, $"7c. a corridor tile: a wall cuts ({wall?.Facts}), a floor does not ({floor?.Facts}) (F10)");
            Program.Check(floorAll.Exists(o => o.ax == 2 && o.ay == 2), "7d. a floor may go on the AI's own tile (F10)");
        }

        // N2 (stage 2 recheck): an AI outside a 1-wide corridor; the inside stand is reached only through the tile to place
        {
            int n = 9;
            var gN = new PlaceGridInput { n = n, loX = 0, loY = 0, pass = new bool[n * n], meX = 6, meY = 4, meFx = 6, meFy = 4 };
            for (int x = 1; x <= 7; x++) gN.pass[x * n + 4] = true;   // corridor y=4, x=1..7; the tile to place (4,4), inside stand (3,4)
            bool plain = AIPlaceGrid.StandFor(gN, 4, 4, 1, 1, null, PlaceGridInput.Key(3, 4), out int px, out int py);
            bool cross = AIPlaceGrid.StandFor(gN, 4, 4, 1, 1, null, PlaceGridInput.Key(3, 4), out int cx, out int cy, true);
            Program.Check(plain && px == 5 && cross && cx == 3 && cy == 4, $"N2. without crossing the stand is the outside (5,4) ({px},{py}); allowed to cross the tile, the inside stand (3,4) is reached ({cx},{cy})");
        }

        // 4. the same choice as the old filter (Choose before 단계 1) on random rooms with objects, plots and dug tiles
        var rnd = new Random(20261105);
        int same = 0, runs = 0, bothNone = 0;
        string firstDiff = null;
        for (int k = 0; k < 400; k++)
        {
            int n = 21;
            var rows = new string[n];
            int ax = 10, ay = 10;
            for (int r = 0; r < n; r++)
            {
                var cs = new char[n];
                for (int x = 0; x < n; x++)
                {
                    double v = rnd.NextDouble();
                    cs[x] = v < 0.22 ? '#' : v < 0.25 ? 'O' : v < 0.28 ? 'P' : v < 0.30 ? 'D' : '.';
                }
                rows[r] = new string(cs);
            }
            var ch = rows[n - 1 - ay].ToCharArray(); ch[ax] = 'A'; rows[n - 1 - ay] = new string(ch);
            var g = Grid(rows);
            bool baseItem = rnd.NextDouble() < 0.3;
            if (baseItem) { g.baseItem = true; g.baseX = 10 + rnd.Next(-4, 5); g.baseY = 10 + rnd.Next(-4, 5); g.baseRadius = 6f; }
            var all = AIPlaceGrid.All(g, out _, out _);
            var b = AIPlaceGrid.Best(all);
            var old = OldChoose(g);
            runs++;
            bool bOld = b != null && AIPlaceGrid.OldFilterKept(b);
            if (old == null && !bOld) { same++; bothNone++; continue; }
            if (old != null && bOld && old.Value.ax == b.ax && old.Value.ay == b.ay && old.Value.sx == b.standX && old.Value.sy == b.standY) { same++; continue; }
            if (firstDiff == null) firstDiff = $"run {k}: old {(old == null ? "none" : $"({old.Value.ax},{old.Value.ay}) stand ({old.Value.sx},{old.Value.sy})")} new {(b == null ? "none" : $"({b.ax},{b.ay}) stand ({b.standX},{b.standY}) {b.EffectKey}")}";
        }
        Program.Check(same == runs, $"4. same spot and stand as the old filter: {same}/{runs} (old found none in {bothNone}){(firstDiff != null ? "; first difference " + firstDiff : "")}");
    }

    // A copy of AIPlaceSpot.Choose before 단계 1 (fc8161b, AICraft.cs), on the same grid: returns anchor and stand.
    static (int ax, int ay, int sx, int sy)? OldChoose(PlaceGridInput g)
    {
        int n = g.n;
        var pass = (bool[])g.pass.Clone();
        Func<int, int, int> Idx = (x, y) => (x - g.loX) * n + (y - g.loY);
        Func<int, int, bool> In = (x, y) => x >= g.loX && y >= g.loY && x < g.loX + n && y < g.loY + n;
        pass[Idx(g.meX, g.meY)] = true;
        var before = Label(pass, n, null, g);
        int mainB = before[Idx(g.meX, g.meY)];
        var reachB = new List<(int x, int y)>();
        foreach (var t in g.targets) if (In(t.x, t.y) && Reach(t.x, t.y, pass, before, mainB, g, null)) reachB.Add((t.x, t.y));
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        (int, int, int, int)? best = null; float bestScore = 0f;
        for (int ox = -g.searchR; ox <= g.searchR; ox++)
            for (int oy = -g.searchR; oy <= g.searchR; oy++)
            {
                int ax = g.meX + ox, ay = g.meY + oy;
                var F = new HashSet<long>();
                bool ok = true;
                for (int fx = 0; fx < g.sizeX && ok; fx++)
                    for (int fy = 0; fy < g.sizeY && ok; fy++)
                    {
                        int x = ax + fx, y = ay + fy;
                        if (!In(x, y) || !pass[Idx(x, y)] || before[Idx(x, y)] != mainB || (x == g.meX && y == g.meY) || g.dug[Idx(x, y)] || g.plot[Idx(x, y)]) ok = false;
                        else if (g.baseItem && Math.Sqrt((x - g.baseX) * (x - g.baseX) + (y - g.baseY) * (y - g.baseY)) > g.baseRadius - 1f) ok = false;
                        else F.Add(PlaceGridInput.Key(x, y));
                    }
                if (!ok) continue;
                var after = Label(pass, n, F, g);
                int meComp = after[Idx(g.meX, g.meY)];
                var ring = new List<(int x, int y)>();
                foreach (var f in F)
                    for (int k = 0; k < 4; k++)
                    {
                        int rx = (int)(f >> 32) + dx[k], ry = (int)(uint)f + dy[k];
                        if (In(rx, ry) && !F.Contains(PlaceGridInput.Key(rx, ry)) && pass[Idx(rx, ry)] && !ring.Contains((rx, ry))) ring.Add((rx, ry));
                    }
                if (ring.Count == 0) continue;
                bool bad = false; int pocket = 0;
                var seenComp = new HashSet<int>();
                foreach (var r in ring)
                {
                    int c = after[Idx(r.x, r.y)];
                    if (c == meComp || !seenComp.Add(c)) continue;
                    int sz = 0; bool border = false;
                    for (int i = 0; i < n * n; i++) if (after[i] == c) { sz++; int ix = i / n, iy = i % n; if (ix == 0 || iy == 0 || ix == n - 1 || iy == n - 1) border = true; }
                    if (border || sz > 12) { bad = true; break; }
                    pocket += sz;
                }
                if (!bad) foreach (var t in reachB) if (!Reach(t.x, t.y, pass, after, meComp, g, F)) { bad = true; break; }
                if (bad) continue;
                int sX = ax, sY = ay; float sd = float.MaxValue;
                foreach (var r in ring)
                    if (after[Idx(r.x, r.y)] == meComp) { float d = (float)Math.Sqrt((g.meFx - r.x) * (g.meFx - r.x) + (g.meFy - r.y) * (g.meFy - r.y)); if (d < sd) { sd = d; sX = r.x; sY = r.y; } }
                if (sd == float.MaxValue) continue;
                int plotAdj = 0, open = 0;
                foreach (var f in F)
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++)
                        {
                            int qx = (int)(f >> 32) + x, qy = (int)(uint)f + y;
                            if (F.Contains(PlaceGridInput.Key(qx, qy)) || !In(qx, qy)) continue;
                            if (g.plot[Idx(qx, qy)]) plotAdj++;
                            if (pass[Idx(qx, qy)]) open++;
                        }
                float score = sd + 0.6f * plotAdj - 0.15f * open + (pocket > 0 ? 3f + 0.3f * pocket : 0f)
                    + (g.baseItem ? 0.1f * (float)Math.Sqrt((ax - g.baseX) * (ax - g.baseX) + (ay - g.baseY) * (ay - g.baseY)) : 0f);
                if (best == null || score < bestScore) { best = (ax, ay, sX, sY); bestScore = score; }
            }
        return best;
    }

    static int[] Label(bool[] pass, int n, HashSet<long> cut, PlaceGridInput g)
    {
        var lab = new int[n * n]; int next = 0; var q = new Queue<int>();
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        for (int s = 0; s < n * n; s++)
        {
            if (!pass[s] || lab[s] != 0 || (cut != null && cut.Contains(PlaceGridInput.Key(g.loX + s / n, g.loY + s % n)))) continue;
            next++; lab[s] = next; q.Enqueue(s);
            while (q.Count > 0)
            {
                int i = q.Dequeue(); int x = i / n, y = i % n;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                    int j = nx * n + ny;
                    if (!pass[j] || lab[j] != 0 || (cut != null && cut.Contains(PlaceGridInput.Key(g.loX + nx, g.loY + ny)))) continue;
                    lab[j] = next; q.Enqueue(j);
                }
            }
        }
        return lab;
    }

    static bool Reach(int tx, int ty, bool[] pass, int[] lab, int comp, PlaceGridInput g, HashSet<long> cut)
    {
        int n = g.n;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                int qx = tx + x, qy = ty + y;
                if (qx < g.loX || qy < g.loY || qx >= g.loX + n || qy >= g.loY + n) continue;
                if (cut != null && cut.Contains(PlaceGridInput.Key(qx, qy))) continue;
                int i = (qx - g.loX) * n + (qy - g.loY);
                if (pass[i] && lab[i] == comp) return true;
            }
        return false;
    }
}
