// E2 — 코어-안전지대 단계 2: the base edge (Core/AIBaseEdge.cs) and going through a door (Core/AIDoorPass.cs),
// with the review fixes (9-단계2-검수반영.md: G1 nested options, G2 last open tile, G5 door step limits, G9 time,
// G10 facts, G11 block choice) and open ground (user, HEAD 2026-11-05).
using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class EdgeTests
{
    // a map: '#' wall, '.' floor, 'S' the seed (floor), '?' unknown, '~' water, 'N' floor the game never takes
    static EdgeGrid Grid(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length, n = Math.Max(h, w);
        var g = new EdgeGrid { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], known = new bool[n * n], noPlace = new bool[n * n], windowRange = n / 2 };
        for (int i = 0; i < n * n; i++) g.known[i] = true;   // outside the drawn rows: known walls
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                int y = h - 1 - r; char c = rows[r][x]; int i = x * n + y;
                g.pass[i] = c == '.' || c == 'S' || c == 'N';
                g.water[i] = c == '~';
                g.known[i] = c != '?';
                g.noPlace[i] = c == 'N';
                if (c == 'S') { g.seedX = x; g.seedY = y; }
            }
        return g;
    }

    // an open, fully known field n x n with the seed in the middle (no natural walls)
    static EdgeGrid Field(int n)
    {
        var g = new EdgeGrid { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], known = new bool[n * n], noPlace = new bool[n * n], seedX = n / 2, seedY = n / 2, windowRange = n / 2 };
        for (int i = 0; i < n * n; i++) { g.pass[i] = true; g.known[i] = true; }
        return g;
    }

    static List<EdgeOption> Opts(EdgeGrid g) => AIBaseEdgeCore.Options(g).list;

    public static void Run()
    {
        // 1. a room with one 1-wide corridor out to the unknown: an option closes the corridor with 1 block
        var g1 = Grid(new[]
        {
            "###########",
            "#.......###",
            "#.......###",
            "#...S.......?",
            "#.......###",
            "#.......###",
            "###########",
        });
        var o1 = Opts(g1);
        Program.Check(o1.Exists(o => o.Blocks == 1 && o.gaps.Count == 1) && o1.TrueForAll(o => o.inside.Contains(EdgeGrid.Key(4, 3))), $"1a. a room with one corridor: a 1-block option, the seed inside ({Show(o1)})");
        Program.Check(o1.TrueForAll(o => !o.inside.Contains(EdgeGrid.Key(12, 3))), "1b. the unknown end is never inside");
        Program.Check(o1.Exists(o => o.Blocks > 0 && o.Facts(0, 0).Contains("모자람")), "1c. facts say blocks are missing when the bag has none");

        // 2. a closed room: the shut option carries the dark-corners fact
        var g2 = Grid(new[] { "#######", "#.....#", "#..S..#", "#.....#", "#######" });
        var o2 = Opts(g2);
        Program.Check(o2.Exists(o => o.Blocks == 0 && o.Facts(0, 0).Contains("이미 막혀 있음") && o.Facts(0, 0).Contains("적이 생길 수 있음")), $"2. a closed room: already closed, with the fact that enemies can still spawn inside ({Show(o2)})");

        // 3. diagonal-only contacts are shut
        var g3 = Grid(new[] { "#######?", "#.....#.", "#..S..#.", "#....#..", "#######." });
        var o3 = Opts(g3);
        Program.Check(o3.Exists(o => o.Blocks == 0), $"3. a diagonal-only contact does not connect (4-way): already closed ({Show(o3)})");

        // G1. a room + a 6-tile corridor + a large closed cave, all seen: the corridor option is offered, not only "shut"
        var rowsG1 = new List<string>();
        rowsG1.Add("##############################");
        for (int r = 0; r < 13; r++)
        {
            string room = r >= 2 && r <= 10 ? "#........." : "##########";
            string corr = r == 6 ? "......" : "######";
            string cave = r >= 0 && r <= 12 ? "............." : "#############";
            rowsG1.Add(room + corr + cave + "#");
        }
        rowsG1.Add("##############################");
        var rr = rowsG1[7].ToCharArray(); rr[5] = 'S'; rowsG1[7] = new string(rr);
        var gG1 = Grid(rowsG1.ToArray());
        var oG1 = Opts(gG1);
        Program.Check(oG1.Exists(o => o.Blocks == 1) && oG1.Exists(o => o.Blocks == 0), $"G1a. a fully seen closed cave behind a corridor: the 1-block corridor option and the shut option both ({Show(oG1)})");
        var corridor = oG1.Find(o => o.Blocks == 1);
        Program.Check(corridor != null && corridor.knownBeyond == 1 && corridor.Facts(5, 0).Contains("본 적 있는 바깥 공간"), $"G1b. the corridor option says the outside is a seen space ({corridor?.Facts(5, 0)})");

        // 4. a wide opening of 9 tiles: batches of at most 7, the last one holds the gap's last tiles; door tile left open
        var rows4 = new List<string> { "###########", "#.........#", "#....S....#", "#.........#", "#.........#", "?.........?", "???????????" };
        var g4 = Grid(rows4.ToArray());
        var o4 = Opts(g4);
        var wide = o4.Find(o => o.MaxGap() >= 8);
        Program.Check(wide != null, $"4a. an option with a wide gap exists ({Show(o4)})");
        if (wide != null)
        {
            var bs = AIBaseEdgeCore.Batches(g4, wide, null);
            bool allSmall = bs.TrueForAll(b => b.tiles.Count <= AIBaseEdgeCore.MaxBatch);
            Program.Check(allSmall && bs.Count >= 2 && bs.FindAll(b => b.closesGap).Count == wide.gaps.Count, $"4b. batches of at most 7 ({string.Join(",", bs.ConvertAll(b => b.tiles.Count))}), one closing batch per gap");
            bool insideStands = bs.TrueForAll(b => !b.closesGap || wide.inside.Contains(EdgeGrid.Key(b.stands[b.stands.Count - 1].x, b.stands[b.stands.Count - 1].y)));
            Program.Check(insideStands, "4c. the closing batch's last tile has an inside stand");
            var door = AIBaseEdgeCore.DoorTileOf(g4, wide.gaps[0]);
            var bd = AIBaseEdgeCore.Batches(g4, wide, door);
            Program.Check(!bd.Exists(b => b.tiles.Contains(door)) && !bd.Exists(b => b.gap == 0 && b.closesGap), $"4d. with a door tile ({door.x},{door.y}) the gap's batches leave it open and none closes that gap");
            // G2: the closing tile is decided when placing: the last open tile of the gap, whatever batch it is in
            var gapTiles = wide.gaps[0].tiles;
            var walled = new HashSet<(int, int)>(gapTiles); walled.Remove(gapTiles[3]);   // a late answer: only a middle tile is left open
            Program.Check(AIBaseEdgeCore.IsLastOpen(gapTiles, gapTiles[3], u => !walled.Contains(u)) && !AIBaseEdgeCore.IsLastOpen(gapTiles, gapTiles[3], u => true),
                "G2. the last open tile of a gap is found from the ground now (a middle tile when the rest is walled), not from the batch");
            // G3: with a door tile left open the walls are never the closing tile; the door is
            var doorOpen = new HashSet<(int, int)> { door };
            Program.Check(!AIBaseEdgeCore.IsLastOpen(gapTiles, gapTiles[0].Equals(door) ? gapTiles[1] : gapTiles[0], u => doorOpen.Contains(u)) && AIBaseEdgeCore.IsLastOpen(gapTiles, door, u => doorOpen.Contains(u) && !u.Equals(door)),
                "G3. while the door tile is open no wall closes the gap; the door does (placed from inside)");
        }

        // 5. a cut through the spawn pad cannot be closed
        var g5 = Grid(new[] { "#########", "#...S...#", "#.......#", "####N####", "????.????" });
        var o5 = Opts(g5);
        Program.Check(!o5.Exists(o => o.gaps.Exists(gp => gp.tiles.Contains((4, 1)))), $"5. a cut holding a tile the game never takes is not offered ({Show(o5)})");

        // 6. water at the edge
        var g6 = Grid(new[] { "#######", "#..S..#", "#.....#", "###~###", "???~???" });
        var o6 = Opts(g6);
        Program.Check(o6.Exists(o => o.WaterTiles == 1 && o.Facts(5, 0).Contains("물 위에 놓이는 블록만")), $"6. a water gap is a cut tile with the water fact ({Show(o6)})");

        // G10. the window edge is "the end of the range looked at", not "not seen"
        var gw = Field(21);
        var ow = AIBaseEdgeCore.CutAt(gw, AIBaseEdgeCore.Dist(gw), 3, 0, out _);
        var gu = Grid(new[] { "#######", "#.....?", "#..S..#", "#######" });
        var ou = Opts(gu);
        Program.Check(ow != null && ow.windowTouch == 0 && ou.Exists(o => o.unknownTouch > 0 && o.Facts(1, 0).Contains("가 보지 않은 곳")),
            $"G10. unknown tiles and the window edge are told apart ({Show(ou)})");

        // Open ground (user): no natural walls -> built edges of a few sizes, same facts, under the 60-block limit
        var gf = Field(41);
        var sw = Stopwatch.StartNew();
        var rf = AIBaseEdgeCore.Options(gf);
        long ms = sw.ElapsedMilliseconds;
        var of = rf.list;
        Program.Check(of.Count >= 3 && of.TrueForAll(o => o.Blocks > 0 && o.knownBeyond == o.Blocks) && Distinct(of.ConvertAll(o => o.inside.Count)),
            $"OG1. open ground: {of.Count} built edges of different sizes ({Show(of)})");
        var small = of[0];
        var bsf = AIBaseEdgeCore.Batches(gf, small, null);
        Program.Check(bsf.TrueForAll(b => b.tiles.Count <= AIBaseEdgeCore.MaxBatch) && bsf.Exists(b => b.closesGap), $"OG2. a built edge is laid in batches of at most 7, closed from inside ({string.Join(",", bsf.ConvertAll(b => b.tiles.Count))})");
        int largest = 0; foreach (var o in of) largest = Math.Max(largest, o.inside.Count);
        Program.Check(largest >= 200 && rf.largerLeftOut, $"OG3. the 60-block limit leaves small open-ground bases (largest offered {largest} tiles), larger ones are said to be left out");

        // G9. time: open field 61x61 (the game window) and the G1 cave
        var g61 = Field(61);
        sw.Restart(); var r61 = AIBaseEdgeCore.Options(g61); long ms61 = sw.ElapsedMilliseconds;
        sw.Restart(); AIBaseEdgeCore.Options(gG1); long msG1 = sw.ElapsedMilliseconds;
        Program.Check(ms61 < 2000, $"G9. options time: open 61x61 {ms61} ms ({r61.cutsComputed} cuts), 41x41 {ms} ms, cave {msG1} ms (offline, .NET; logged, not a frame budget)");

        // G11. the block for a tile: ground first takes a block that cannot go on water
        var blocks = new List<(int, bool)> { (10, true), (10, false) };
        Program.Check(AIBaseEdgeCore.PickBlock(blocks, false) == 1 && AIBaseEdgeCore.PickBlock(blocks, true) == 0 && AIBaseEdgeCore.PickBlock(new List<(int, bool)> { (3, true) }, false) == 0 && AIBaseEdgeCore.PickBlock(new List<(int, bool)> { (3, false) }, true) == -1,
            "G11. ground: the non-water block first; water: only a water block; none fits: -1");

        // L1. no minimum area (user): what stays outside is a fact. A room with a workbench in a side pocket behind a
        // 1-tile neck: an option that cuts the neck leaves the workbench outside and says so.
        var gL = Grid(new[]
        {
            "#############",
            "#.......#####",
            "#.......#O..#",
            "#...S........?",
            "#.......#...#",
            "#.......#####",
            "#############",
        });
        // 'O' drawn as wall (an object); its tile (9,4) as a landmark
        gL.landmarks.Add(("WoodenWorkBench", new List<(int, int)> { (9, 4) }));
        var oL = Opts(gL);
        Program.Check(oL.Exists(o => o.outside.Contains("WoodenWorkBench") && o.Facts(5, 0).Contains("둘레 밖에 남는 것: WoodenWorkBench")) && oL.TrueForAll(o => o.placeableInside > 0 && o.Facts(5, 0).Contains("안쪽에 놓을 수 있는 칸")),
            $"L1. an option that leaves a station outside says so; every option says how many inside tiles can take an object ({Show(oL)})");
        // L2. around: hold the workbench -> an edge with it inside (the pocket included), "all held inside"
        var hold = new List<(int, int)> { (9, 4), (10, 4), (9, 3) };
        var aL = AIBaseEdgeCore.Around(gL, hold).list;
        Program.Check(aL.Count >= 1 && aL.Exists(o => !o.outside.Contains("WoodenWorkBench") && o.Facts(5, 0).Contains("품을 것 모두 안쪽")) && aL.TrueForAll(o => o.around),
            $"L2. around the workbench: an edge holding it ({Show(aL)})");

        // M1. the map data: rows, the plan overlay on the same cells, preview and confirmed apart, JSON parses
        var objs = new List<MapObject> { new MapObject { name = "WoodenWorkBench", kind = "station" } };
        objs[0].tiles.Add((9, 4));
        var plan = AIBaseMap.Plan(gL, aL[0], null, "preview", "test");
        var withDoor = oL.Find(o => o.Blocks >= 1);
        var conf = AIBaseMap.Plan(gL, withDoor, AIBaseEdgeCore.DoorTileOf(gL, withDoor.gaps[0]), "confirmed", "test");
        var rows = AIBaseMap.Rows(gL, objs);
        var ov = AIBaseMap.Overlay(gL, rows, plan);
        bool cellsOk = rows.Count == gL.n && ov.Count == gL.n;
        char At(List<string> rr, (int x, int y) t) => rr[gL.n - 1 - (t.y - gL.loY)][t.x - gL.loX];
        foreach (var pt in plan.place) cellsOk &= (At(ov, pt) == 'X' || At(ov, pt) == '!') && At(rows, pt) == '.';
        foreach (var tt in plan.torches) cellsOk &= At(ov, tt) == 'T' && At(rows, tt) == '.';
        cellsOk &= At(rows, (9, 4)) == 'O' && At(rows, (4, 3)) == '.';
        var ovc = AIBaseMap.Overlay(gL, rows, conf);
        cellsOk &= conf.door.HasValue && At(ovc, conf.door.Value) == 'G' && !conf.place.Contains(conf.door.Value);
        Program.Check(cellsOk, $"M1a. the overlay lands on the same cells: X/! on floor, T on inside floor, G the door spot, O the object ({plan.place.Count} place, {plan.torches.Count} torches)");
        var json = AIBaseMap.Json(gL, objs, new List<EdgePlanData> { plan, conf }, "w1", "claude", 1);
        bool parsed = false, statesOk = false;
        try
        {
            using (var doc = System.Text.Json.JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                parsed = root.GetProperty("rows").GetArrayLength() == gL.n && root.GetProperty("objects")[0].GetProperty("tiles")[0][0].GetInt32() == 9;
                var ps = root.GetProperty("plans");
                statesOk = ps[0].GetProperty("state").GetString() == "preview" && ps[1].GetProperty("state").GetString() == "confirmed"
                    && ps[1].GetProperty("door").GetArrayLength() == 2 && ps[0].GetProperty("overlay").GetArrayLength() == gL.n
                    && ps[0].GetProperty("batches").GetArrayLength() >= 1;
            }
        }
        catch (Exception e) { Console.WriteLine("    json: " + e.Message); }
        Program.Check(parsed && statesOk, "M1b. the map JSON parses: rows, objects with tiles, plans with preview/confirmed state, door, batches, overlay rows");

        // D1. door spots (live run: a door goes only between two walls)
        // a 1-wide corridor: natural walls on both sides -> no more walls needed
        var gD = Grid(new[] { "#########", "#...S...#", "#.......#", "####.####", "####.####", "????.????" });
        var oD = Opts(gD);
        var corrD = oD.Find(o => o.Blocks == 1);
        Program.Check(corrD != null && corrD.doorSpots.Count == 1 && corrD.doorSpots[0].extraWalls == 0 && corrD.Facts(1, 0).Contains("양옆이 벽"), $"D1a. a corridor gap: the door spot has walls on both sides ({corrD?.Facts(1, 0)})");
        // a 1-tile cut in open ground (walkable on both sides of the passage): the fact says how many more walls
        var gO = Grid(new[] { "#########", "#...S...#", "#.......#", "###...###", "###.#.###", "???.#.???" });
        var oO = Opts(gO);
        Program.Check(oO.Exists(o => o.doorSpots.Exists(d => d.extraWalls > 0) && o.Facts(5, 0).Contains("양옆에 벽")), $"D1b. a door spot with open sides is kept with the fact '더 필요' ({string.Join(" | ", oO.ConvertAll(o => o.Facts(5, 0)))})");
        // a straight 5-tile gap between two walls (built by hand: the flow may also cut diagonally, which then says
        // "more walls needed"): the middle tile is flanked by the gap's own walls; the batches lay them, not the door
        {
            var g5w = Grid(new[] { "#########", "#...S...#", "#.......#", "#.......#", "##.....##", "?????????" });
            var gp = new EdgeGap(); for (int x = 2; x <= 6; x++) gp.tiles.Add((x, 1));
            var o5w = new EdgeOption(); o5w.gaps.Add(gp);
            foreach (var key in new[] { (4, 4), (4, 3), (4, 2), (3, 2), (5, 2), (2, 2), (6, 2) }) o5w.inside.Add(EdgeGrid.Key(key.Item1, key.Item2));
            var spot = AIBaseEdgeCore.DoorSpotOf(g5w, gp);
            var bsw = AIBaseEdgeCore.Batches(g5w, o5w, spot.tile);
            Program.Check(spot.extraWalls == 0 && spot.tile.Equals((4, 1)) && !bsw.Exists(b => b.tiles.Contains(spot.tile)) && bsw.Exists(b => b.tiles.Contains((3, 1))) && bsw.Exists(b => b.tiles.Contains((5, 1))),
                $"D1c. a straight gap: the door spot ({spot.tile.x},{spot.tile.y}) needs {spot.extraWalls} more walls; its sides (3,1),(5,1) are in the batches, the door is not");
        }

        // 8. door pass: approach -> press (one) -> open -> pass -> press -> closed -> done
        var s = new DoorPassState();
        float t = 0f; float mx = -2f, my = 0f; bool open = false, pressDone = true; int presses = 0;
        DoorPhase last = s.phase; var phases = new List<DoorPhase> { s.phase };
        for (int i = 0; i < 400 && s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed; i++, t += 0.05f)
        {
            var o = AIDoorPass.Step(s, DoorKind.Manual, 0f, 0f, 1f, 0f, mx, my, open, pressDone, t);
            mx += o.mx * 0.2f; my += o.my * 0.2f;
            if (o.press) { presses++; pressDone = false; }
            else if (!pressDone) { pressDone = true; open = !open; }
            if (s.phase != last) { phases.Add(s.phase); last = s.phase; }
        }
        Program.Check(s.phase == DoorPhase.Done && presses == 2 && !open, $"8a. through a closed door: {string.Join("->", phases)}, presses {presses}, door {(open ? "open" : "closed")} ({s.facts}{s.fail})");
        var s2 = new DoorPassState(); mx = -0.62f; pressDone = true; presses = 0;   // 재실측 N-a: from the press window (0.5 off the centre is too close: no press)
        for (int i = 0; i < 400 && s2.phase != DoorPhase.Done && s2.phase != DoorPhase.Failed; i++, t += 0.05f)
        {
            var o = AIDoorPass.Step(s2, DoorKind.Manual, 0f, 0f, 1f, 0f, mx, 0f, false, pressDone, t);
            if (o.press) { presses++; pressDone = false; } else pressDone = true;
        }
        var waitingOpen = AIDoorPass.Step(s2, DoorKind.Manual, 0f, 0f, 1f, 0f, mx, 0f, false, true, t);
        Program.Check(s2.phase == DoorPhase.Open && presses > AIDoorPass.MaxPresses && waitingOpen.observation.Contains("문 열기") && waitingOpen.observation.Contains("누름"), $"8b. a door that does not open: {presses} presses, still opening with '{waitingOpen.observation}'");
        // G5: a press that never finishes is reported while the open step waits.
        var s4 = new DoorPassState(); float t4 = 0f; int steps4 = 0;
        for (; steps4 < 400 && s4.phase != DoorPhase.Failed && s4.phase != DoorPhase.Done; steps4++, t4 += 0.05f)
            AIDoorPass.Step(s4, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.55f, 0f, false, false, t4);
        var waitingPress = AIDoorPass.Step(s4, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.55f, 0f, false, false, t4);
        Program.Check(s4.phase == DoorPhase.Open && s4.fail == "" && waitingPress.observation.Contains("안 보내짐") && !waitingPress.press, $"G5a. a press that never finishes: waiting with an observation ({t4:F1}s, '{waitingPress.observation}')");
        // G5: something stands on the far spot: the close step ends by time instead of stepping forever
        var s5 = new DoorPassState { phase = DoorPhase.Close }; float t5 = 0f;
        for (int i = 0; i < 400 && s5.phase != DoorPhase.Failed && s5.phase != DoorPhase.Done; i++, t5 += 0.05f)
            AIDoorPass.Step(s5, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.2f, 0f, true, true, t5);   // stuck at 0.2 (cannot reach 0.62)
        var waitingClose = AIDoorPass.Step(s5, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.2f, 0f, true, true, t5);
        Program.Check(s5.phase == DoorPhase.Close && s5.fail == "" && waitingClose.observation.Contains("닫기") && !waitingClose.press, $"G5b. a blocked far spot: closing continues with '{waitingClose.observation}'");
        // G5: the close step does not press from far away
        var s6 = new DoorPassState { phase = DoorPhase.Close }; bool pressedFar = false; float t6 = 0f;
        for (int i = 0; i < 20; i++, t6 += 0.05f) { var o = AIDoorPass.Step(s6, DoorKind.Manual, 0f, 0f, 1f, 0f, 4f, 0f, true, true, t6); if (o.press) pressedFar = true; }
        Program.Check(!pressedFar, "G5c. no close press from 4 tiles away (it walks to the door first)");
        var s3 = new DoorPassState(); mx = -1.5f; presses = 0;
        for (int i = 0; i < 400 && s3.phase != DoorPhase.Done && s3.phase != DoorPhase.Failed; i++, t += 0.05f)
        {
            var o = AIDoorPass.Step(s3, DoorKind.Auto, 0f, 0f, 1f, 0f, mx, 0f, true, true, t);
            mx += o.mx * 0.2f; if (o.press) presses++;
        }
        Program.Check(s3.phase == DoorPhase.Done && presses == 0, $"8c. an automatic door: walked through without a press ({s3.phase})");
        Program.Check(AIDoorPass.IsOpen(1) && AIDoorPass.IsOpen(3) && !AIDoorPass.IsOpen(0) && !AIDoorPass.IsOpen(2) && AIDoorPass.PassageEastWest(2), "8d. variation 1/3 open, 0/2 closed; 2/3 is an east-west passage");
    }

    static bool Distinct(List<int> l) { var h = new HashSet<int>(l); return h.Count == l.Count; }
    static string Show(List<EdgeOption> l) => string.Join(" | ", l.ConvertAll(o => $"d{o.d}/m{o.margin}: gaps {o.gaps.Count} blocks {o.Blocks} area {o.inside.Count} unk {o.unknownTouch} win {o.windowTouch} seen {o.knownBeyond}"));
}
