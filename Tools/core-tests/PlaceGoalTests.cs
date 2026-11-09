// PG — 제작대-놓기 (work/2026-11/2026-11-05-제작대-놓기/1-설계.md): place identifiers (Core/AIPlaces), the forms of goto /
// place steps, shapes and their cap, the report's places pick and can_do.craft groups, the craft fact sentences, the
// edge facts on place options (Core/AIPlaceGrid), the bridge's where in a plan step (AIPlanProtocol).
using System;
using System.Collections.Generic;

public static class PlaceGoalTests
{
    public static void Run()
    {
        // 1. identifiers: round trip and fixed words
        foreach (var s in new[] { "here", "base", "core", "farm", "tile:-3,12", "water:5,-2", "station:CopperAnvil@2,0", "chest:WoodenChest@-1,4", "pot:CookingPot@3,3", "ore:CopperOre@-40,12", "rect:0,0,2,2", "line:3,4,7,4" })
            Program.Check(AIPlaces.Parse(s, out var r, out string e) && r.ToString() == s, $"1. '{s}' reads and writes back ({e ?? r.ToString()})");
        Program.Check(AIPlaces.Parse(null, out var rb, out _) && rb.Kind == "base" && AIPlaces.Parse("", out var rb2, out _) && rb2.Kind == "base", "1b. no where is base");
        Program.Check(AIPlaces.Parse("BASE", out var rc, out _) && rc.Kind == "base", "1c. fixed words in any case");
        Program.Check(!AIPlaces.Parse("tile:3", out _, out string e1) && e1.Contains("tile:x,y"), $"1d. a bad tile: {e1}");
        Program.Check(!AIPlaces.Parse("station:CopperAnvil", out _, out string e2) && e2.Contains("@x,y"), $"1e. an object without a tile: {e2}");
        Program.Check(!AIPlaces.Parse("somewhere", out _, out string e3) && e3.Contains("모르는 장소"), $"1f. an unknown word: {e3}");
        Program.Check(!AIPlaces.Parse("cave:1,2", out _, out string e4) && e4.Contains("모르는 장소 종류"), $"1g. an unknown kind: {e4}");

        // 2. shapes
        Program.Check(AIPlaces.Parse("rect:0,0,2,2", out var sq, out _) && AIPlaces.ShapeTiles(sq).Count == 9, "2a. rect 3x3 = 9 tiles");
        Program.Check(AIPlaces.Parse("line:7,4,3,4", out var ln, out _) && AIPlaces.ShapeTiles(ln).Count == 5 && AIPlaces.ShapeTiles(ln)[0] == (3, 4), "2b. a line either way, row order");
        Program.Check(!AIPlaces.Parse("line:0,0,3,3", out _, out string e5) && e5.Contains("가로나 세로"), $"2c. a diagonal line is refused: {e5}");
        Program.Check(AIPlaces.Parse("rect:0,0,5,5", out var r36, out string e6) && AIPlaces.ShapeTiles(r36).Count == 36, $"2d. 36 tiles remains a complete request: {e6}");
        Program.Check(AIPlaces.Parse("rect:0,0,1000,1000", out var big, out string e7) && AIPlaces.ShapeTiles(big).Count == 1002001 && AIPlaces.ShapeTiles(big)[1002000] == (1000,1000), $"2e. a million-tile request uses lazy coordinates without allocating every entry: {e7}");
        Program.Check(AIPlaces.Parse("rect:0,0,4,5", out var r30, out _) && AIPlaces.ShapeTiles(r30).Count == 30, "2f. exactly 30 tiles is taken");

        // 3. step forms (a later plan step)
        Program.Check(AIPlaces.WhereForm("goto", null) != null && AIPlaces.WhereForm("goto", "base") == null, "3a. goto needs a where");
        Program.Check(AIPlaces.WhereForm("goto", "rect:0,0,1,1") != null && AIPlaces.WhereForm("place", "rect:0,0,1,1") == null, "3b. a shape is for place only");
        Program.Check(AIPlaces.WhereForm("place", null) == null, "3c. place without a where is base");

        // 4. places pick: caps per group and in all, all distinct ore locations retained, even for the same kind
        var lines = new List<PlaceLine> { new PlaceLine { Group = "fixed", Dist = -2, Text = "base" }, new PlaceLine { Group = "fixed", Dist = -1, Text = "farm" } };
        for (int i = 0; i < 8; i++) lines.Add(new PlaceLine { Group = "mine", Sub = "chest", Dist = i, Text = "chest" + i });
        for (int i = 0; i < 4; i++) lines.Add(new PlaceLine { Group = "other", Sub = "station", Dist = i, Text = "other" + i });
        foreach (var o in new[] { "CopperOre", "CopperOre", "TinOre", "IronOre", "GoldOre" }) lines.Add(new PlaceLine { Group = "ore", Sub = o, Dist = lines.Count, Text = "ore " + o });
        lines.Add(new PlaceLine { Group = "water", Dist = 1, Text = "water" });
        var pick = AIPlaces.Pick(lines, out var more);
        Program.Check(pick.Count == lines.Count, $"4a. {AIPlaces.PlacesMax} all supplied place lines retained ({pick.Count})");
        Program.Check(pick[0] == "base" && pick[1] == "farm" && pick.FindAll(x => x.StartsWith("chest")).Count == 8 && pick.FindAll(x => x.StartsWith("other")).Count == 4, $"4b. fixed first, all own and other stations retained ({string.Join(",", pick)})");
        Program.Check(pick.FindAll(x => x == "ore CopperOre").Count == 2 && more.Count == 0, $"4c. all distinct ore locations retained, even for the same kind ({string.Join(",", more)})");
        // 4d. 검수 보고 길이: a character cap on the places list, what does not fit counted
        var longLines = new List<PlaceLine>();
        for (int i = 0; i < 10; i++) longLines.Add(new PlaceLine { Group = "mine", Sub = "chest", Dist = i, Text = new string('x', 200) });
        var capped = AIPlaces.Pick(longLines, out var moreC);
        int chars = 0; foreach (var x in capped) chars += x.Length + 3;
        Program.Check(capped.Count == 10 && moreC.Count == 0, $"4d. all complete places independent of old character cap {AIPlaces.PlacesChars} chars ({capped.Count} lines, {chars} chars, more {string.Join(",", moreC)})");

        // 5. can_do.craft groups
        var rows = new List<(string, string, int)> { ("손", "Torch", 2), ("손", "WoodenWorkBench", 0), ("CopperWorkbench", "CopperAnvil", 2), ("CopperWorkbench", "Torch", 2), ("CopperWorkbench", "CopperHelm", 1), ("CopperWorkbench", "CopperPants", 0) };
        var gr = AIPlaces.CraftGroups(rows, 4, out int cm);
        Program.Check(gr.Count == 2 && gr[0].where == "손" && gr[1].where == "CopperWorkbench" && gr[1].items[0] == "CopperAnvil" && !gr[1].items.Contains("Torch"), $"5a. grouped, each item once, all materials first ({string.Join(" | ", gr.ConvertAll(g => g.where + ":" + string.Join(",", g.items)))})");
        Program.Check(cm == 1 && gr[1].items.Count == 2, $"5b. the cap counts the rest ({cm})");

        // 6. craft facts (§2.5)
        string f1 = AIPlaces.CraftFact("CopperSword", "CopperAnvil", 1, "CopperWorkbench(보임 (2,0))", "CopperBar 8/8");
        Program.Check(f1.Contains("가방에 CopperAnvil 1개") && f1.Contains("place CopperAnvil"), $"6a. the station in the bag: place it first ({f1})");
        string f2 = AIPlaces.CraftFact("CopperSword", "CopperAnvil", 0, "CopperWorkbench(보임 (2,0))", "CopperBar 0/8");
        Program.Check(f2.Contains("craft CopperAnvil → place CopperAnvil") && f2.Contains("CopperWorkbench"), $"6b. the station can be made: the order ({f2})");
        string f3 = AIPlaces.CraftFact("CopperSword", "CopperAnvil", 0, "", "");
        Program.Check(f3.Contains("만들 곳을 모름"), $"6c. unknown where the station is made ({f3})");
        Program.Check(AIPlaces.CraftFact("X", "", 0, "", "").Contains("아는 제작법이 아님"), "6d. no station known at all");

        // 7. edge facts (§2.10, user decision: the outside close is an option with the door fact)
        Program.Check(AIPlaces.EdgeFact(1, 3, false, false, false, false, false, null) == "둘레 틈 1을(를) 메움(남은 틈 칸 2)", "7a. a gap tile");
        Program.Check(AIPlaces.EdgeFact(1, 1, false, false, false, false, true, "(4,-6)").Contains("바깥에서 닫음, 둘레 문 (4,-6)로 드나듦"), "7b. last tile from outside with a door");
        Program.Check(AIPlaces.EdgeFact(1, 1, false, false, false, false, true, null).Contains("둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함"), "7c. last tile from outside without a door");
        Program.Check(AIPlaces.EdgeFact(1, 1, false, false, false, false, false, null).Contains("안쪽에서 닫음"), "7d. last tile from inside");
        Program.Check(AIPlaces.EdgeFact(0, 0, true, false, false, true, false, null) == "둘레 문 자리에 문 달기" && AIPlaces.EdgeFact(0, 0, true, false, false, false, false, null).Contains("문 자리를 막음"), "7e. the door spot");
        Program.Check(AIPlaces.EdgeFact(2, 3, false, false, false, true, false, null).Contains("양옆이 벽이어야"), "7f. a door on another gap tile");
        Program.Check(AIPlaces.EdgeFact(2, 3, false, false, true, false, false, null).Contains("그대로 열림"), "7g. a floor / bridge on a gap stays open");
        Program.Check(AIPlaces.EdgeFact(0, 0, false, false, false, false, false, null) == "", "7h. no edge tile, no fact");

        // 8. the grid: an edge fact on the option (never dropped), a bridge that joins a space across water
        int n = 9;
        var g = new PlaceGridInput { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], searchR = 4, meX = 2, meY = 4, meFx = 2, meFy = 4 };
        for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) g.pass[x * n + y] = x != 4;   // a wall column x=4 ...
        g.pass[4 * n + 4] = true;                                                            // ... with a one-tile gap at (4,4)
        g.edgeGap = new int[n * n]; g.edgeInside = new bool[n * n]; g.edgeDoorSpot = new bool[n * n]; g.edgeDoorSide = new bool[n * n];
        g.edgeGap[4 * n + 4] = 1; g.gapOpen[1] = 1;
        for (int x = 5; x < n; x++) for (int y = 0; y < n; y++) g.edgeInside[x * n + y] = true;   // inside: east of the wall
        var all = AIPlaceGrid.All(g, out _, out _);
        var gapOpt = all.Find(o => o.ax == 4 && o.ay == 4);
        Program.Check(gapOpt != null && gapOpt.Facts.Contains("바깥에서 닫음") && gapOpt.Facts.Contains("문 없음"), $"8a. the gap's last tile from outside is an option with the fact ({gapOpt?.Facts})");
        Program.Check(AIPlaceGrid.Pick(all).Exists(o => o.EffectKey.Contains("edge:")), "8b. the edge option is picked as its own kind");
        var w = new PlaceGridInput { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], searchR = 4, meX = 2, meY = 4, meFx = 2, meFy = 4, onWater = true, footprintWalkable = true };
        for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) { bool wet = x == 4; w.pass[x * n + y] = !wet; w.water[x * n + y] = wet; }
        var allW = AIPlaceGrid.All(w, out _, out _);
        var br = allW.Find(o => o.ax == 4 && o.ay == 4);
        Program.Check(br != null && br.joins > 0 && br.Facts.Contains("물 건너") && !br.Facts.Contains("막힘") && !br.cutsOut && br.bigPiece == 0 && br.pocket == 0, $"8c. a bridge across a water column joins the space beyond and cuts nothing (검수 F2: no \"막힘\" with it) ({br?.Facts})");

        // 9. the bridge's where reaches the plan step, and the step text names it
        var reply = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"set\",\"for\":{\"report\":3,\"goal_rev\":1},\"plan\":[{\"do\":\"place\",\"item\":\"CopperAnvil\",\"where\":\"base\"},{\"do\":\"craft\",\"item\":\"CopperSword\",\"count\":1}],\"goal\":{\"do\":\"goto\",\"where\":\"ore:CopperOre@-40,12\"}}}");
        var p = reply.Answer?.Plan;
        Program.Check(p != null && p[0].Where == "base" && p[0].Text == "place CopperAnvil base" && p[1].Where == null && p[1].Text == "craft CopperSword 1", $"9a. plan steps keep where ({(p == null ? "null" : string.Join(" / ", p.ConvertAll(x => x.Text)))})");
        Program.Check(reply.Answer.GoalWhere == "ore:CopperOre@-40,12", "9b. goal.where is read");
        Program.Check(new PlanStep("place", "Torch", -1, "null").Where == null, "9c. a JSON null where is none");
        // 9d. 검수 B1: a place without count (the plan step and the answer goal) passes the count check as 1
        AIPlaces.Parse(p[0].Where, out var w0, out _);
        Program.Check(p[0].Count < 1 && AIPlaces.PlaceCount(w0, p[0].Count, out int n0) == null && n0 == 1, $"9d. plan step place CopperAnvil base without count is taken as 1 (count {p[0].Count})");
        var one = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"set\",\"for\":{\"report\":3,\"goal_rev\":1},\"goal\":{\"do\":\"place\",\"item\":\"CopperAnvil\",\"where\":\"base\"}}}");
        AIPlaces.Parse(one.Answer.GoalWhere, out var w1, out _);
        Program.Check(one.Answer.GoalCount < 1 && AIPlaces.PlaceCount(w1, one.Answer.GoalCount, out int n1) == null && n1 == 1, $"9e. the answer goal place without count (brief's example) is taken as 1 (count {one.Answer.GoalCount})");
        Program.Check(AIPlaces.PlaceCount(w1, 200, out int n200) == null && n200 == 200 && AIPlaces.PlaceCount(w1, 0, out int n0b) == null && n0b == 1, "9f. 200 accepted, omitted/zero retains the default1");
        AIPlaces.Parse("rect:0,0,2,2", out var w9, out _);
        Program.Check(AIPlaces.PlaceCount(w9, -1, out int n9) == null && n9 == 9, "9g. a shape's count is its tiles");

        // 10. 검수 F1: on the aimed stand is there
        Program.Check(AIPlaces.ArrivedAt(2.8f, 2.0f, 0.3f) && !AIPlaces.ArrivedAt(2.8f, 2.0f, 1.5f) && AIPlaces.ArrivedAt(1.9f, 2.0f, float.MaxValue), "10a. arrived within the arrive distance or on a stand");
        Program.Check(AIPlaces.ArrivedAt(8.4f, 4f, 0.1f) && !AIPlaces.ArrivedAt(8.4f, 4f, float.MaxValue), "10b. a base stand at ring 6 is home; no stand found is not");

        // 11. 검수 F3/F4: the facts a place goal is taken with, where it stands, its end sentence
        Program.Check(AIPlaces.ShortFact("WoodFloor", 6, 9, true) == "가방에 WoodFloor 6개, 놓을 칸 9개 — 6칸만 놓임" && AIPlaces.ShortFact("WoodFloor", 9, 9, true) == "", "11a. a short bag for a shape");
        Program.Check(AIPlaces.ShortFact("WallDirtBlock", 3, 10, false).Contains("3개만 놓임"), "11b. a short bag for a count");
        Program.Check(AIPlaces.EndOf(8, 0, 4) == AIPlaces.PlaceEnd.Done && AIPlaces.EndOf(6, 3, 0) == AIPlaces.PlaceEnd.Short && AIPlaces.EndOf(0, 0, 4) == AIPlaces.PlaceEnd.Stuck && AIPlaces.EndOf(0, 3, 0) == AIPlaces.PlaceEnd.Stuck && AIPlaces.EndOf(2, 3, 4) == AIPlaces.PlaceEnd.Going, "11c. done / short (bag out after some) / stuck (nothing placed) / going");
        var sk = new List<(int x, int y, string why)> { (1, 1, "이미 벽이 있음") };
        string skt = AIPlaces.SkippedText(sk);
        Program.Check(skt == "건너뛴 칸 1개: 이미 벽이 있음 1칸 (1,1)", $"11d. skipped tiles named ({skt})");
        string et = AIPlaces.EndText("WoodFloor", "rect:0,0,2,2", AIPlaces.PlaceEnd.Done, 8, 9, true, 1, skt);
        Program.Check(et.Contains("9칸 중 8칸") && et.Contains("이미 벽이 있음 1칸 (1,1)"), $"11e. a shape done with a skipped tile says it ({et})");
        string es = AIPlaces.EndText("WoodFloor", "rect:0,0,2,2", AIPlaces.PlaceEnd.Short, 6, 9, true, 0, "");
        Program.Check(es.Contains("9칸 중 6칸") && es.Contains("떨어져 3칸 못 놓음"), $"11f. a shape ended by the bag says placed and not placed ({es})");
        string ec = AIPlaces.EndText("WallDirtBlock", "기지", AIPlaces.PlaceEnd.Short, 3, 10, false, 0, "");
        Program.Check(ec.Contains("3개 놓음") && ec.Contains("7개 못 놓음"), $"11g. a count ended by the bag ({ec})");
        var many = new List<(int x, int y, string why)>(); for (int i = 0; i < 6; i++) many.Add((i, 0, "이미 물건이 있음"));
        Program.Check(AIPlaces.SkippedText(many).Contains("이미 물건이 있음 6칸") && AIPlaces.SkippedText(many).Contains("외 3칸"), $"11h. many skipped: the rest counted ({AIPlaces.SkippedText(many)})");
        // 11i. 실게임 D3: every reason is said (grouped), however many tiles one reason has
        var mixed = new List<(int x, int y, string why)>();
        for (int i = 0; i < 5; i++) mixed.Add((i, 0, "이미 벽이 있음"));
        mixed.Add((-1, -3, "이미 WoodFloor 놓여 있음"));
        string mt = AIPlaces.SkippedText(mixed);
        Program.Check(mt.StartsWith("건너뛴 칸 6개") && mt.Contains("이미 벽이 있음 5칸 (0,0) (1,0) (2,0) 외 2칸") && mt.Contains("이미 WoodFloor 놓여 있음 1칸 (-1,-3)"), $"11i. reasons grouped, none cut off ({mt})");

        // 13. 재검수 N1: a shape counts what this goal placed — tiles holding the item before it are skipped, not done
        var shp = new List<(int x, int y)> { (0, 0), (1, 0), (2, 0) };
        Program.Check(AIPlaces.ShapePlaced(shp, new List<(int x, int y)>()) == 0 && AIPlaces.EndOf(AIPlaces.ShapePlaced(shp, new List<(int x, int y)>()), 0, 5) == AIPlaces.PlaceEnd.Stuck, "13a. none placed, the rest held before or skipped: stuck, not done");
        Program.Check(AIPlaces.ShapePlaced(shp, new List<(int x, int y)> { (1, 0), (1, 0), (9, 9) }) == 1, "13b. placed tiles counted once, off-shape ones not");
        string en = AIPlaces.EndText("WoodFloor", "rect:0,0,2,0", AIPlaces.PlaceEnd.Done, 1, 3, true, 2, AIPlaces.SkippedText(new List<(int x, int y, string why)> { (0, 0, "이미 WoodFloor 놓여 있음"), (2, 0, "이미 벽이 있음") }));
        Program.Check(en.Contains("3칸 중 1칸에 WoodFloor 놓음") && en.Contains("이미 WoodFloor 놓여 있음"), $"13c. the end says what this goal placed and what was there before ({en})");
        // 14. 재검수 N2: a start failure that is not the tile stops the batch
        Program.Check(AIPlaces.OnTileStartFail(true) == AIPlaces.TileStartFail.Skip && AIPlaces.OnTileStartFail(false) == AIPlaces.TileStartFail.Stop, "14a. tile fault: skip the tile; hand slot / bag: stop");

        // 15. 실게임 D2: one rule for "outside the area" (the sentence and the count)
        float far = AIPlaceGrid.Farthest(-9, 0, 1, 1, -10, -2);
        Program.Check(Math.Abs(far - 2.236f) < 0.01f && !AIPlaceGrid.OutsideArea(far, 3f) && AIPlaceGrid.OutsideArea(3.2f, 3f) && !AIPlaceGrid.OutsideArea(3f, 3f), $"15a. (-9,0) is 2.2 from (-10,-2): inside radius 3 ({far:F2})");
        var ga = new PlaceGridInput { n = n, loX = -14, loY = -6, pass = new bool[n * n], water = new bool[n * n], searchR = 4, meX = -10, meY = -2, meFx = -10, meFy = -2, baseItem = true, baseX = -10, baseY = -2, baseRadius = 3f, areaWord = "칸 (-10,-2)" };
        for (int i = 0; i < n * n; i++) ga.pass[i] = true;
        var allA = AIPlaceGrid.All(ga, out _, out _);
        bool agree = allA.Count > 0 && allA.TrueForAll(o => o.Facts.Contains("반경") == (o.baseDist > ga.baseRadius) && o.outsideBase == AIPlaceGrid.OutsideArea(o.baseDist, ga.baseRadius));
        var oneA = allA.Find(o => o.ax == -9 && o.ay == 0);
        Program.Check(agree && oneA != null && !oneA.Facts.Contains("반경"), $"15b. an option says \"반경 밖\" exactly when it is past the radius ((-9,0): {oneA?.Facts})");
        var farA = allA.Find(o => o.baseDist > 3.05f);
        Program.Check(farA != null && farA.Facts.Contains($"{farA.baseDist:F1}칸 — 반경 3.0칸 밖"), $"15c. the outside sentence names the same distance ({farA?.Facts})");

        // 16. 실게임 D1: dig routes through the base edge — the edge sentences of AIBaseGuard; a running walk replanned onto
        // edge walls its option did not state stops, the stated ones go on
        Func<(int x, int y), EdgeWallFact> note = q => q == (-9, -3) ? new EdgeWallFact(EdgeWallKind.BoundaryWall) : q == (-5, -6) ? new EdgeWallFact(EdgeWallKind.HungDoorSide, (-4, -6)) : q == (-6, -6) ? new EdgeWallFact(EdgeWallKind.DoorSideWall) : default;
        var walls = new List<(int x, int y)> { (-5, -5), (-5, -6), (-9, -3), (-6, -6) };
        var ew = AIBaseGuard.EdgeWallsOn(walls, note);
        Program.Check(ew.Count == 2 && ew[0].t == (-5, -6) && ew[1].t == (-9, -3), $"16a. the edge-opening walls of a route: a hung door's side wall and an edge wall, not a plain wall or a door-spot side wall ({ew.Count})");
        string dt = AIBaseGuard.DigEdgeText(ew);
        Program.Check(dt.Contains("그중 2개는 기지 둘레 벽(뚫으면 둘레가 열림)") && dt.Contains("(-5,-6) 기지 문 (-4,-6) 옆 벽 — 캐면 문이 떨어짐") && dt.Contains("(-9,-3) " + AIBaseGuard.EdgeWallText), $"16b. the option's sentence ({dt})");
        Program.Check(AIBaseGuard.DigEdgeText(AIBaseGuard.EdgeWallsOn(new List<(int x, int y)> { (-5, -5) }, note)) == "", "16c. no edge wall: no sentence");
        var stated = new HashSet<(int x, int y)> { (-9, -3) };
        var newEdge = AIBaseGuard.EdgeWallsOn(new List<(int x, int y)> { (-9, -3) }, note, stated);
        Program.Check(newEdge.Count == 0, "16d. a replan onto the edge wall the option stated goes on");
        var newEdge2 = AIBaseGuard.EdgeWallsOn(walls, note, stated);
        string st2 = AIBaseGuard.DigEdgeStopText(newEdge2);
        Program.Check(newEdge2.Count == 1 && newEdge2[0].t == (-5, -6) && st2.StartsWith("멈춤: 길을 다시 찾으니 기지 둘레를 파는 길만 남음") && st2.Contains("문이 떨어짐"), $"16e. a replan onto an edge wall not stated stops with the sentence ({st2})");
        Program.Check(new EdgeWallFact(EdgeWallKind.BoundaryWall).OpensEdge && new EdgeWallFact(EdgeWallKind.HungDoorSide, (1, 2)).OpensEdge && !new EdgeWallFact(EdgeWallKind.DoorSideWall).OpensEdge, "16f. the same judge as the enemy dig option (EdgeWallFact.OpensEdge)");
        // 16g. 실게임 D1 추가 (home walk, enemy dig too): after a stop the option is rebuilt from the new route — it states
        // those edge walls (EdgeOk = what it states), so a choice of it goes on and it does not stop again on them
        var rebuilt = new HashSet<(int x, int y)>(); foreach (var q in AIBaseGuard.EdgeWallsOn(walls, note)) rebuilt.Add(q.t);
        string rebuiltText = AIBaseGuard.DigEdgeText(AIBaseGuard.EdgeWallsOn(walls, note));
        Program.Check(AIBaseGuard.EdgeWallsOn(walls, note, rebuilt).Count == 0 && rebuiltText.Contains("(-5,-6)") && rebuiltText.Contains("(-9,-3)"), $"16g. the rebuilt option states the new edge walls and goes on with them ({rebuiltText})");
        // 16h. a route that leaves the stated edge wall for a plain way goes on (no new edge wall)
        Program.Check(AIBaseGuard.EdgeWallsOn(new List<(int x, int y)> { (-5, -5) }, note, stated).Count == 0, "16h. a replan with no edge wall goes on");

        // 17. 검수 E1: the dig plan's two passes — a way by a door wins over mining the edge however much longer; with no
        // door the edge way is found in the second pass (and marked)
        Func<int, int, int> mapCost(bool door) => (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : x == 10 ? (door && y == 12 ? AITunnelSearch.DoorStepCost : 3) : 1;
        Func<int, int, bool> edgeWall = (x, y) => x == 10;   // the wall column is the base edge (the door is no wall: never closed)
        Func<int, int, bool> edgeWallNoDoorTile = (x, y) => x == 10 && y != 12;
        var onePass = AITunnelSearch.Find((5, 2), (15, 2), (0, 0), (29, 14), mapCost(true));
        Program.Check(onePass != null && onePass.Contains((10, 2)), "17a. one pass (before): the short way mines the edge wall (10,2) though a door is 10 rows down");
        var two = AITunnelSearch.FindAvoidingEdge((5, 2), (15, 2), (0, 0), (29, 14), mapCost(true), edgeWallNoDoorTile, out bool usedE, out int closedE);
        Program.Check(two != null && two.Contains((10, 12)) && !two.Exists(q => q.x == 10 && q.y != 12) && !usedE && closedE > 0, $"17b. two passes: the way by the door (10,12), no edge wall, not marked ({(two == null ? "none" : two.Count + " tiles")}, closed {closedE})");
        var noDoor = AITunnelSearch.FindAvoidingEdge((5, 2), (15, 2), (0, 0), (29, 14), mapCost(false), edgeWall, out bool usedE2, out _);
        Program.Check(noDoor != null && usedE2 && noDoor.Contains((10, 2)), "17c. no door: the second pass mines the edge (marked: the options say it is the only way)");
        var plain = AITunnelSearch.FindAvoidingEdge((5, 2), (15, 2), (0, 0), (29, 14), mapCost(true), (x, y) => false, out bool usedE3, out int closedE3);
        Program.Check(plain != null && plain.Contains((10, 2)) && !usedE3 && closedE3 == 0, "17d. no base edge: one pass, the same route as before");
        var toEdge = AITunnelSearch.FindAvoidingEdge((5, 2), (10, 2), (0, 0), (29, 14), mapCost(false), edgeWall, out bool usedE4, out _);
        Program.Check(toEdge != null && toEdge[toEdge.Count - 1] == (10, 2) && !usedE4, "17e. the target itself on the edge is not closed (digging to it is what was asked)");
        Program.Check(AIBaseGuard.DigEdgeText(new List<((int x, int y) t, string note)> { ((10, 2), AIBaseGuard.EdgeWallText) }, 3, true).Contains("둘레를 파지 않는 길(문·돌아가는 길)은 아는 칸에 없음"), "17f. the edge way from the second pass says it is the only one");
        // the cost of the second pass (printed for the result; warm, best of 3): a 261x261 open map cut by an edge wall
        // column with no door (the first pass searches half), and a target in a closed edge ring (the first pass searches
        // all the map outside the ring before the second)
        Func<int, int, int> bigCost = (x, y) => x < 0 || y < 0 || x > 260 || y > 260 ? -1 : x == 130 ? 3 : 1;
        Func<int, int, bool> ringE = (x, y) => Math.Max(Math.Abs(x - 250), Math.Abs(y - 250)) == 5;
        Func<int, int, int> ringCost = (x, y) => x < 0 || y < 0 || x > 260 || y > 260 ? -1 : ringE(x, y) ? 3 : 1;
        double Best(Action a) { double b = double.MaxValue; for (int i = 0; i < 3; i++) { var w0 = System.Diagnostics.Stopwatch.StartNew(); a(); b = Math.Min(b, w0.Elapsed.TotalMilliseconds); } return b; }
        AITunnelSearch.Find((0, 0), (260, 260), (0, 0), (260, 260), bigCost);   // warm up
        double c1 = Best(() => AITunnelSearch.Find((0, 0), (260, 260), (0, 0), (260, 260), bigCost));
        double c2 = Best(() => AITunnelSearch.FindAvoidingEdge((0, 0), (260, 260), (0, 0), (260, 260), bigCost, (x, y) => x == 130, out _, out _));
        double r1 = Best(() => AITunnelSearch.Find((0, 0), (250, 250), (0, 0), (260, 260), ringCost));
        double r2 = Best(() => AITunnelSearch.FindAvoidingEdge((0, 0), (250, 250), (0, 0), (260, 260), ringCost, ringE, out _, out _));
        Console.WriteLine($"  PG E1 cost (261x261, warm, best of 3): edge column one pass {c1:F1} ms / two passes {c2:F1} ms; edge ring around the target one pass {r1:F1} ms / two passes {r2:F1} ms");

        // 20. 우회 사실 (재검수 G1: estimated time of the first way vs the edge way)
        Program.Check(AITunnelSearch.NeedsDetourCheck(15, false, 1) && AITunnelSearch.NeedsDetourCheck(3, true, 1) && !AITunnelSearch.NeedsDetourCheck(14, false, 1) && !AITunnelSearch.NeedsDetourCheck(40, true, 0), "20a. the edge way is searched too when the first way is 15 tiles or more or mines walls (and edge walls were met)");
        Program.Check(AITunnelSearch.OfferDetour(25f, 15f) && !AITunnelSearch.OfferDetour(25f, 15.5f) && Math.Abs(AITunnelSearch.EstTiles(10, 5) - 16f) < 0.01f, "20b. offered when the edge way is 10 walking tiles or more quicker (a hit = 1.2 tiles)");
        // a long door way (door 12 rows down) vs one edge wall: the door way stays the plan, the edge way comes as the detour
        Func<int, int, int> hitsMap = (x, y) => x == 10 && y != 12 ? 1 : 0;
        var dw = AITunnelSearch.FindWithDetour((5, 0), (15, 0), (0, 0), (29, 14), (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : x == 10 ? (y == 12 ? AITunnelSearch.DoorStepCost : 3) : 1, edgeWallNoDoorTile, hitsMap, true,
            out bool dUsed, out _, out var dDet, out bool dCap, out _, out float dE1, out float dE2, out int dPasses, out _, out _);
        Program.Check(dw != null && dw.Contains((10, 12)) && dDet != null && dDet.Contains((10, 0)) && !dUsed && !dCap && dPasses == 2 && dE1 - dE2 >= 10f, $"20c. a long door way is kept, the edge way offered beside it (first est {dE1:F1}, edge est {dE2:F1}, passes {dPasses})");
        var nearDoor = AITunnelSearch.FindWithDetour((5, 11), (15, 11), (0, 0), (29, 14), (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : x == 10 ? (y == 12 ? AITunnelSearch.DoorStepCost : 3) : 1, edgeWallNoDoorTile, hitsMap, true,
            out _, out _, out var nDet, out _, out _, out _, out _, out int nPasses, out _, out _);
        Program.Check(nearDoor != null && nearDoor.Contains((10, 12)) && nDet == null && nPasses == 1, "20d. a short door way: one pass, no detour");
        // G1: no door, the way round the edge mines 20 plain walls (a wall band) vs one edge wall: the edge way is offered
        Func<int, int, int> bandCost = (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : x == 10 && y < 14 ? 3 : y == 14 ? 3 : 1;
        Func<int, int, bool> bandEdge = (x, y) => x == 10 && y < 14;
        Func<int, int, int> bandHits = (x, y) => (x == 10 && y < 14) || y == 14 ? 1 : 0;
        var band = AITunnelSearch.FindWithDetour((5, 0), (15, 0), (0, 0), (29, 14), bandCost, bandEdge, bandHits, true, out _, out _, out var bDet, out _, out _, out float bE1, out float bE2, out _, out _, out _);
        Program.Check(band != null && !band.Exists(q => bandEdge(q.x, q.y)) && bDet != null && bE1 - bE2 >= 10f, $"20e. a first way that mines plain walls is weighed by its time: the edge way offered (est {bE1:F1} vs {bE2:F1})");
        // G3: a search stopped at its cap is not "no way"
        AITunnelSearch.Find((0, 0), (200, 200), (0, 0), (200, 200), (x, y) => 1, out bool capT, 50);
        AITunnelSearch.Find((0, 0), (5, 0), (0, 0), (3, 3), (x, y) => 1, out bool capF);
        Program.Check(capT && !capF, "20f. the step cap is told apart from no way");
        AITunnelSearch.FindWithDetour((0, 0), (40, 0), (0, 0), (60, 60), (x, y) => x == 20 ? 3 : 1, (x, y) => x == 20, null, false, out bool cUsed, out _, out _, out bool cCap, out _, out _, out _, out _, out _, out _, 300);
        string capText = AIBaseGuard.DigEdgeText(new List<((int x, int y) t, string note)> { ((20, 0), AIBaseGuard.EdgeWallText) }, 3, true, true);
        Program.Check(cCap && capText.Contains("둘레를 파지 않는 길은 찾기 범위 안에서 못 찾음") && !capText.Contains("아는 칸에 없음"), $"20g. a capped first pass: 'not found within the search', not 'none known' (cap {cCap}, used {cUsed})");
        // G2: the edge test once per tile in one search (both passes)
        int edgeAsks = 0; var asked = new HashSet<(int, int)>();
        AITunnelSearch.FindWithDetour((5, 0), (15, 0), (0, 0), (29, 14), mapCost(false), (x, y) => { edgeAsks++; asked.Add((x, y)); return x == 10; }, null, true, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
        Program.Check(edgeAsks == asked.Count, $"20h. the edge test is asked once per tile ({edgeAsks} asks, {asked.Count} tiles)");
        // 22. 검수 H1: the detour check only when asked — other walks (ore, explore, remembered station, farm, pot / chest,
        // retreat, the dig skill's own replans) keep the long first way in one pass
        var off = AITunnelSearch.FindWithDetour((5, 0), (15, 0), (0, 0), (29, 14), (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : x == 10 ? (y == 12 ? AITunnelSearch.DoorStepCost : 3) : 1, edgeWallNoDoorTile, hitsMap, false,
            out _, out _, out var offDet, out _, out _, out _, out _, out int offPasses, out _, out _);
        Program.Check(off != null && off.Contains((10, 12)) && offDet == null && offPasses == 1, "22a. detour check off: the door way in one pass, no edge way searched");
        Program.Check(AITunnelSearch.FindWithDetour((5, 0), (15, 0), (0, 0), (29, 14), mapCost(false), edgeWall, null, false, out bool offUsed, out _, out _, out _, out _, out _, out _, out int offP2, out _, out _) != null && offUsed && offP2 == 2, "22b. detour check off: no way without the edge still falls back to it (two passes)");
        // the target tile's wall is no reason for the check: a short way whose only wall is the target (an ore) stays one pass
        Func<int, int, int> oreCost = (x, y) => x < 0 || y < 0 || x > 29 || y > 14 ? -1 : (x == 9 && y == 0) ? 3 : x == 4 ? 3 : 1;
        Func<int, int, int> oreHits = (x, y) => (x == 9 && y == 0) || x == 4 ? 1 : 0;
        AITunnelSearch.FindWithDetour((5, 0), (9, 0), (0, 0), (29, 14), oreCost, (x, y) => x == 4, oreHits, true, out _, out int oreClosed, out _, out _, out _, out _, out _, out int oreP, out _, out _);
        Program.Check(oreP == 1 && oreClosed > 0 && AITunnelSearch.Hits(new List<(int x, int y)> { (6, 0), (7, 0), (8, 0), (9, 0) }, oreHits, true) == 0, $"22c. the target's own wall does not count as \"walls on the way\" (passes {oreP}, edge met {oreClosed})");
        // 23. 검수 H2: both passes at their cap -> "not found within the search", not "no way"
        AITunnelSearch.FindWithDetour((0, 0), (150, 150), (0, 0), (150, 150), (x, y) => x == 2 ? 3 : 1, (x, y) => x == 2, null, false, out _, out _, out _, out bool capFirst, out bool cAll, out _, out _, out int cP, out _, out _, 200);
        AITunnelSearch.FindWithDetour((0, 0), (9, 0), (0, 0), (5, 5), (x, y) => x == 3 ? 3 : 1, (x, y) => x == 3, null, false, out _, out _, out _, out _, out bool nAll, out _, out _, out _, out _, out _);
        Program.Check(capFirst && cAll && cP == 2 && !nAll, "23a. both passes capped is told apart from no way (a target outside the box: no way, not capped)");

        string df = AIBaseGuard.DetourFact(true, 30, AIMoveEstimate.Seconds(30, 2), 12, AIMoveEstimate.Seconds(12, 4), new List<((int x, int y) t, string note)> { ((10, 0), AIBaseGuard.EdgeWallText) });
        Program.Check(df.StartsWith("문으로 돌아감 30칸·약 6초(둘레 (10,0)를 파면 12칸·약 4초, 둘레가 열림 — " + AIBaseGuard.EdgeWallText), $"20i. the detour fact ({df})");
        Program.Check(AIBaseGuard.DetourFact(false, 30, 40f, 12, 14f, null).StartsWith("둘레를 피해 감 30칸"), "20j. a first way without a door says it goes around");

        // 21. 우회 사실 (코어-안전지대 조건): the edge way option never pushes another out — last, in the edge share, cut first
        AsmCand C(string k, bool prog = false, bool det = false) => new AsmCand { kind = k.StartsWith("dig") ? "dig" : "x", key = k, desc = k, progress = prog, detour = det };
        AsmInput In(List<AsmCand> craft, List<AsmCand> dig, List<AsmCand> edgeL, List<AsmCand> other, bool combat = false) => new AsmInput
        {
            goal = GoalKind.Home, curKey = "", combat = combat, lCont = new List<AsmCand>(), lFollow = new List<AsmCand>(), lEnemy = new List<AsmCand>(), lCombat = new List<AsmCand>(), lCraft = craft, lLife = new List<AsmCand>(),
            lMine = new List<AsmCand>(), lDig = dig, lRemember = new List<AsmCand>(), lItem = new List<AsmCand>(), lExplore = new List<AsmCand>(), lDigExplore = new List<AsmCand>(), lOther = other, lEdge = edgeL, foodPicks = new HashSet<AsmCand>(),
        };
        var craft21 = new List<AsmCand> { C("dig:1,1", true), C("digedge:1,1", true, true) };
        var other21 = new List<AsmCand>(); for (int i = 0; i < 12; i++) other21.Add(C("explore:" + i));
        var edge21 = new List<AsmCand> { C("edgefill:a"), C("edgefill:b") };
        var sel = AICandidateAssembler.Select(In(craft21, new List<AsmCand>(), edge21, other21), c => true, c => true, out _, out _, out _);
        Program.Check(sel[sel.Count - 1].key == "digedge:1,1" && sel[sel.Count - 1].edge && sel[0].key == "dig:1,1", "21a. the edge way goes last, as an edge option; the door way keeps its place");
        AICandidateAssembler.Finish(sel, C("wait"), 14, out var cut21);
        Program.Check(sel.Exists(c => c.key == "dig:1,1") && sel.Exists(c => c.key == "digedge:1,1") && sel.FindAll(c => c.key.StartsWith("edgefill")).Count == 2 && cut21.Count == 0, "21b. over fourteen, door, edge route and other edge options all remain");
        var sel2 = AICandidateAssembler.Select(In(new List<AsmCand> { C("dig:1,1", true), C("digedge:1,1", true, true) }, new List<AsmCand>(), new List<AsmCand>(), new List<AsmCand> { C("explore:0") }), c => true, c => true, out _, out _, out _);
        AICandidateAssembler.Finish(sel2, C("wait"), 14, out _);
        Program.Check(sel2.Exists(c => c.key == "digedge:1,1"), "21c. with room the edge way is offered");
        var many2 = new List<AsmCand>(); for (int i = 0; i < 16; i++) many2.Add(C("attackish:" + i));
        var sel3 = AICandidateAssembler.Select(In(new List<AsmCand>(), new List<AsmCand> { C("dig:9,9", true), C("digedge:9,9", true, true) }, new List<AsmCand>(), many2, true), c => true, c => true, out _, out _, out _);
        AICandidateAssembler.Finish(sel3, C("wait"), 15, out _);
        Program.Check(sel3.Exists(c => c.key == "digedge:9,9") && sel3.Exists(c => c.key == "dig:9,9") && sel3.Count == many2.Count + 3, "21d. a legacy positive combat cap no longer drops the edge route or any other option");

        // 24. 실게임2 P2: every dig / mine option whose way opens the edge says so (once)
        string edgeFact = AIBaseGuard.DigEdgeText(new List<((int x, int y) t, string note)> { ((-10, 3), AIBaseGuard.EdgeWallText) }).Trim();
        string oreDesc = "파 들어가기 — 구리 광석 (-16,13) 쪽, 열린 길이 없어 벽 10개(WallDirtBlock 10)를 뚫으며 감.";
        string withF = AIBaseGuard.WithEdgeFact(oreDesc, edgeFact);
        Program.Check(withF.StartsWith(oreDesc) && withF.Contains("(-10,3) " + AIBaseGuard.EdgeWallText) && AIBaseGuard.WithEdgeFact(withF, edgeFact) == withF, $"24a. an ore dig through the edge gets the edge sentence, once ({withF})");
        Program.Check(AIBaseGuard.WithEdgeFact(oreDesc, "") == oreDesc, "24b. no edge wall on the way: unchanged");
        string mineDesc = "캐기 — 벽 WallDirtBlock (-10,3) 2.0칸.";
        Program.Check(AIBaseGuard.WithEdgeFact(mineDesc, AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.BoundaryWall))).EndsWith(AIBaseGuard.EdgeWallText + ".") && AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.DoorSideWall)) == "" && AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.HungDoorSide, (-4, -6))) != "", "24c. mining the edge wall itself says it (a door-spot side wall does not open the edge)");
        // 25. 실게임2 P3: a bridge goes on water, floors / walls do not
        Program.Check(AIPlaces.TileGoesOnWater("bridge") && !AIPlaces.TileGoesOnWater("floor") && !AIPlaces.TileGoesOnWater("wall") && !AIPlaces.TileGoesOnWater("none"), "25a. tile types on water");
        // 26. 실게임2 P1: between two doors on the same side, the door on the way (an edge door) is taken, not my door behind
        Program.Check(AIPlaces.NearDoor((-10f, -2f), (-10f, 3f), new List<(int x, int y)> { (-10, 0) }, new List<(int x, int y)> { (-10, -5) }, out var nd) && nd == (-10, 0), $"26a. the shortest detour among edge doors and my doors ({nd})");
        Program.Check(AIPlaces.NearDoor((0f, 0f), (5f, 0f), new List<(int x, int y)>(), new List<(int x, int y)> { (2, 1) }, out var nd2) && nd2 == (2, 1) && !AIPlaces.NearDoor((0f, 0f), (5f, 0f), new List<(int x, int y)> { (0, 30) }, new List<(int x, int y)>(), out _), "26b. my door near the way still taken; a far door not");
        // 검수 Q1: edge doors join only with a plan and not when both ends are inside (the P1 scene: neither inside)
        Program.Check(AIPlaces.EdgeDoorsJoin(true, false, false) && AIPlaces.EdgeDoorsJoin(true, true, false) && AIPlaces.EdgeDoorsJoin(true, false, true)
            && !AIPlaces.EdgeDoorsJoin(true, true, true) && !AIPlaces.EdgeDoorsJoin(false, false, false), "26c. edge doors: plan and not both inside");
        Program.Check(AIBaseGuard.WithEdgeFact("dig:-16,13(막힘)", "기지 둘레 벽 — 캐면 둘레가 열림(적 지나감).").EndsWith("둘레가 열림(적 지나감).")
            && AIBaseGuard.WithEdgeFact("dig:-16,13(막힘)", "") == "dig:-16,13(막힘)", "26d. a left-out option keeps its edge fact (Q2), none added when there is none");
        // 27. 실게임2 S-b: the bracket belongs to the gap tiles
        var pg = new SortedDictionary<int, int> { { 1, 2 } };
        string sb1 = AIPlaces.ShapeEdgeText(pg, 0, 3), sb2 = AIPlaces.ShapeEdgeText(new SortedDictionary<int, int>(), 0, 4);
        Program.Check(sb1 == "그중 2칸이 둘레 틈 1 위(틈 위에 걷지 못하는 것을 놓으면 그 틈을 메움), 3칸이 기지 안쪽" && sb2 == "그중 4칸이 기지 안쪽", $"27a. shape edge text ({sb1} | {sb2})");
        // 28. 실게임2 S-c: an option on an edge tile is not cut by the option count
        var opts28 = new List<PlaceOption>
        {
            new PlaceOption { ax = -8, ay = 2, EffectKey = "ok", score = 1f }, new PlaceOption { ax = -9, ay = 3, EffectKey = "pocket", score = 2f },
            new PlaceOption { ax = -7, ay = 2, EffectKey = "outside-base", score = 3f }, new PlaceOption { ax = -6, ay = 2, EffectKey = "plot-on", score = 4f },
            new PlaceOption { ax = -10, ay = 3, EffectKey = "edge:둘레 틈 1의 마지막 칸 — 안쪽에서 닫음(둘레가 닫힘)", score = 9f },
        };
        var p28 = AIPlaceGrid.Pick(opts28);
        Program.Check(p28.Exists(o => o.ax == -10 && o.ay == 3) && p28.Count == opts28.Count && p28.Exists(o => o.EffectKey == "plot-on"), $"28a. every distinct placement consequence survives, including gap and plot ({p28.Count})");

        // 29. 실게임3 R-b: a bridge is water only — ground tiles are no spots; the game's land flag decides other items
        Program.Check(!AIPlaces.LandOk(false, false, "bridge") && !AIPlaces.LandOk(true, true, "bridge"), "29a. a bridge tile never goes on ground");
        Program.Check(AIPlaces.LandOk(false, false, "floor") && AIPlaces.LandOk(false, false, "none") && AIPlaces.LandOk(true, true, "none") && !AIPlaces.LandOk(true, false, "none"),
            "29b. no placement settings: ground; settings: the land flag decides");
        PlaceGridInput G29(bool onLand)
        {
            int n = 11; var g = new PlaceGridInput { n = n, loX = -5, loY = -5, pass = new bool[n * n], water = new bool[n * n], meX = 0, meY = 0, meFx = 0f, meFy = 0f,
                footprintWalkable = true, onWater = true, onLand = onLand };
            for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++) { int i = (x + 5) * n + (y + 5); bool w = y == 3; g.water[i] = w; g.pass[i] = !w; }
            return g;
        }
        var allW29 = AIPlaceGrid.All(G29(false), out _, out _);
        var allL29 = AIPlaceGrid.All(G29(true), out _, out _);
        Program.Check(allW29.Count > 0 && allW29.TrueForAll(o => o.waterOn > 0 && o.ay == 3), $"29c. water only: every spot is a water tile ({allW29.Count} spots)");
        Program.Check(allL29.Exists(o => o.waterOn == 0) && allL29.Exists(o => o.waterOn > 0), $"29d. ground allowed: ground and water spots ({allL29.Count})");
        Program.Check(AIPlaces.WaterOnlyNote("WoodBridge", 0, 5).Contains("물 위에만 놓임") && AIPlaces.WaterOnlyNote("WoodBridge", 3, 5) == "", "29e. the no-water sentence only when no water spot");
        // 30. 실게임3 R-a: the station just placed is a new one — the one on the chosen footprint first, never an old one
        Program.Check(AIPlaces.NewPlacedIndex(new[] { true, false }, new[] { false, true }, new[] { 1.4f, 2f }, 3f) == 1, "30a. an old anvil 1.4 tiles away is not the placed one");
        Program.Check(AIPlaces.NewPlacedIndex(new[] { false, false }, new[] { false, true }, new[] { 1f, 2.5f }, 3f) == 1 && AIPlaces.NewPlacedIndex(new[] { false, false }, new[] { false, false }, new[] { 2.5f, 1f }, 3f) == 1,
            "30b. on the footprint first, else the nearest new one");
        Program.Check(AIPlaces.NewPlacedIndex(new[] { true }, new[] { true }, new[] { 0.5f }, 3f) == -1 && AIPlaces.NewPlacedIndex(new[] { false }, new[] { false }, new[] { 3.5f }, 3f) == -1, "30c. none new (or too far): -1");

        // 18. 검수 E4: a remembered door tile — the object in view first
        Program.Check(AITunnelSearch.DoorTileCost(false, false, false) == AITunnelSearch.DoorStepCost && AITunnelSearch.DoorTileCost(true, true, true) == AITunnelSearch.DoorStepCost, "18a. out of view (memory) or a door seen there: a way through at the door cost");
        Program.Check(AITunnelSearch.DoorTileCost(true, true, false) == -1 && AITunnelSearch.DoorTileCost(true, false, false) == 1, "18b. in view, another object and no door: blocked; in view and empty: an open tile");
        // 19. 검수 E2: the doors of the plan's route are said as the plan, not a promise
        Program.Check(AIPlaces.DoorsOnText(new List<string> { "(-4,-6)" }) == "계획 길은 문 (-4,-6)를 지남(열기→지나기→닫기; 게임 길찾기가 다른 길을 찾으면 그 길로 감)" && AIPlaces.DoorsOnText(new List<string>()) == null, "19a. the door sentence");

        // 12. 검수 보고 길이: char caps on can_do
        var capL = AIPlaces.CapChars(new List<string> { "aaaa", "bbbb", "cccc" }, 15, out int dropped);
        Program.Check(capL.Count == 2 && dropped == 1, $"12a. a list within its chars ({capL.Count}, {dropped} dropped)");
        var grs = new List<(string where, List<string> items)> { ("손", new List<string> { "A", "B" }), ("CopperWorkbench", new List<string> { "C", "D" }) };
        int cmore = 1;
        var capG = AIPlaces.CapGroups(grs, x => new string('y', 40), 110, ref cmore);
        int kept = 0; foreach (var gg in capG) kept += gg.items.Count;
        Program.Check(kept == 2 && cmore == 3, $"12b. craft groups within their chars ({kept} kept, more {cmore})");
    }
}
