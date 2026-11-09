// G3f — the review of the live-run fixes and the nl-chat merge (Review/fix-merge-review.md F1-F4, S1-S8; the fixes are
// 16-검수반영.md). Each scenario is rebuilt from the review's failure story.
using System;
using System.Collections.Generic;

public static class FixMergeReviewTests
{
    static int _id;
    static AsmCand C(string kind, string key, bool progress = false) => new AsmCand { id = ++_id, kind = kind, key = key, desc = kind + " " + key, progress = progress };

    static AsmInput In(GoalKind goal)
    {
        return new AsmInput
        {
            goal = goal, curKey = "",
            lCont = new List<AsmCand>(), lFollow = new List<AsmCand>(), lEnemy = new List<AsmCand>(), lCombat = new List<AsmCand>(), lCraft = new List<AsmCand>(), lLife = new List<AsmCand>(),
            lMine = new List<AsmCand>(), lDig = new List<AsmCand>(), lRemember = new List<AsmCand>(), lItem = new List<AsmCand>(), lExplore = new List<AsmCand>(), lDigExplore = new List<AsmCand>(), lOther = new List<AsmCand>(),
            foodPicks = new HashSet<AsmCand>(),
        };
    }

    // the model's input: Select, Mark (no failures), Finish with the build's cut
    static List<AsmCand> Build(AsmInput a, bool combat)
    {
        var l = AICandidateAssembler.Select(a, c => true, c => true, out _, out _, out _);
        AICandidateAssembler.Mark(l, 100f, new Dictionary<string, (int, float, string)>(), 30f, new List<(string, float)>());
        AICandidateAssembler.Finish(l, new AsmCand { id = ++_id, kind = "wait", key = "wait", desc = "기다리기" }, AICandidateAssembler.LimitFor(combat, 0));
        return l;
    }

    static bool Has(List<AsmCand> l, string key) => l.Exists(c => c.key == key);

    // a door owed and open next to the AI, as the tail sees it; runs the tail until done (the door answers the press)
    static bool RunTail(DoorOwe o, float x, float y, out float endY)
    {
        bool open = true, done = false; float t = 0f;
        for (int k = 0; k < 300 && !done; k++, t += 0.05f)
        {
            var r = AIBaseGuard.TailStep(o, open, true, x, y, t, out done);
            y += r.my * 0.05f * 5.7f; x += r.mx * 0.05f * 5.7f;
            if (r.press) open = false;
        }
        endY = y;
        return done;
    }

    public static void Run()
    {
        F1(); F2(); F3(); F4(); S1(); S2(); S3(); S4(); S5(); S7();
        N1(); N2(); N3(); N4();   // the recheck (Review/fix-merge-recheck.md)
    }

    // F1: no goal (self-defence) and the close fight left the own-door close out (it was in lCraft / not progress)
    static void F1()
    {
        // the review's story: no goal, chased into the base at (-4,-5), the retreat's tail cut by the model's new attack;
        // the next self-defence call
        var a = In(GoalKind.None);
        a.curCombat = true; a.curKey = "attack:7"; a.combat = true;
        a.lCont.Add(C("continue", "attack:7"));
        a.lEnemy.Add(C("attack", "attack:9"));
        a.lCombat.Add(C("away", "away:0")); a.lCombat.Add(C("retreat", "retreat:-4,-5"));
        a.lDoor.Add(C("doorclose", "doorclose:-4,-6"));
        var l = Build(a, true);
        Program.Check(Has(l, "doorclose:-4,-6") && Has(l, "attack:9") && Has(l, "away:0"),
            $"F1a. no goal + self-defence + an own door left open: the close option is in the model's input next to the combat options ({string.Join(" ", l.ConvertAll(c => c.key))})");
        // the same input as before the fix (the close in lCraft): not in the input — the failure the review found
        var old = In(GoalKind.None); old.curCombat = true; old.curKey = "attack:7";
        old.lCont.Add(C("continue", "attack:7")); old.lEnemy.Add(C("attack", "attack:9")); old.lCraft.Add(C("doorclose", "doorclose:-4,-6"));
        Program.Check(Has(Build(old, true), "doorclose:-4,-6"), "F1b. no-goal state also preserves supported lCraft actions");
        // close fight with a goal: the set is combat + one break-off step; the close (no progress) is in it all the same
        var cf = In(GoalKind.Gather); cf.closeFight = true; cf.combat = true; cf.near6 = 1; cf.enemy0 = "Slime"; cf.enemy0Dist = "1.2";
        cf.lEnemy.Add(C("attack", "attack:3")); cf.lMine.Add(C("mine", "mine:5,5", true));
        cf.lDoor.Add(C("doorclose", "doorclose:-4,-6"));
        var lc = Build(cf, true);
        Program.Check(Has(lc, "doorclose:-4,-6") && Has(lc, "mine:5,5") && Has(lc, "attack:3"), $"F1c. close fight with a goal: the close option stays (it is no break-off step, the break-off step stays too) ({string.Join(" ", lc.ConvertAll(c => c.key))})");
        // out of combat with a goal: right after the combat options, before the craft / goal lists
        var nc = In(GoalKind.Gather);
        nc.lCraft.Add(C("craft", "craft:x")); nc.lDoor.Add(C("doorclose", "doorclose:-4,-6")); nc.lMine.Add(C("mine", "mine:1,1", true));
        var ln = Build(nc, false);
        Program.Check(ln.FindIndex(c => c.key == "doorclose:-4,-6") < ln.FindIndex(c => c.key == "craft:x"), "F1d. with a goal out of combat: the close comes before the craft options");
    }

    // F2: a stop the program makes (goal done, self-defence over, the upper LLM's hold, ...) does not cut the closing tail
    static void F2()
    {
        Program.Check(AIBaseGuard.OnStop(false, false) == AIBaseGuard.TailOnStop.TryTail && AIBaseGuard.OnStop(false, true) == AIBaseGuard.TailOnStop.KeepTail
            && AIBaseGuard.OnStop(true, true) == AIBaseGuard.TailOnStop.CutTail && AIBaseGuard.OnStop(true, false) == AIBaseGuard.TailOnStop.End,
            "F2a. a program stop keeps a running tail or checks for one; only a hard stop (host /ai stop, leaving) cuts it");
        // the review's story: goal "wood N", the last one picked up outside, the walk in ends next to the door with it
        // open; the goal is done in the same frame and EndGoal stops the body
        var o = new DoorOwe(); AIBaseGuard.NoteDoorTile(o, (-4, -6), true); AIBaseGuard.NoteDoorState(o, true, true);
        bool closed = false; float y;
        var act = AIBaseGuard.OnStop(false, false);   // goal done: a program stop, no tail yet (the walk was still running)
        if (act == AIBaseGuard.TailOnStop.TryTail && AIBaseGuard.TailNeeded(o, true, 0.8f, false))
        {
            AIBaseGuard.StartTail(o, false, -4f, -5.2f, 1, true, true);
            closed = RunTail(o, -4f, -5.2f, out y) && !o.door.HasValue;
        }
        Program.Check(closed && o.fact.StartsWith("지난 문 (-4,-6) 닫음"), $"F2b. goal done -> stop next to the open own door: the door is closed before the end is reported ('{o.fact}')");
        // the walk ended by itself first (tail running), then the goal is done: the tail goes on
        var o2 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o2, false, -4f, -5.2f, 1, true, true);
        bool tailOn = o2.tail;
        var act2 = AIBaseGuard.OnStop(false, tailOn);
        bool closed2 = act2 == AIBaseGuard.TailOnStop.KeepTail && RunTail(o2, -4f, -5.2f, out y) && !o2.door.HasValue;
        Program.Check(closed2, "F2c. goal done while the tail runs (also self-defence over / upper LLM hold): the close goes on and the door is closed");
        // a new choice of the model cuts it (agreed): the door stays owed and the next call (no goal) offers the close
        var o3 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o3, false, -4f, -5.2f, 1, true, true);
        o3.tail = false;   // AIEdge.TailAbort (StartSkill of the chosen skill)
        AIBaseGuard.NoteDoorState(o3, true, true);
        var a = In(GoalKind.None); a.curCombat = true; a.curKey = "attack:9"; a.lCont.Add(C("continue", "attack:9")); a.lEnemy.Add(C("attack", "attack:9"));
        if (o3.door.HasValue) a.lDoor.Add(C("doorclose", $"doorclose:{o3.door.Value.x},{o3.door.Value.y}"));
        Program.Check(o3.door.HasValue && Has(Build(a, true), "doorclose:-4,-6"), "F2d. cut by the model's new choice: the door stays owed and the close option is in the next input (no goal)");
    }

    // F3: the outside close's fact when other places stay open (HEAD decision), the door gap's outside option, S8
    static void F3()
    {
        // a wall row y=4 x 0..10 with three gaps: (1,4)-(2,4) gap 1, (5,4) gap 2, (8,4)-(9,4) gap 3 with its door spot (8,4)
        var g = new EdgeGrid { n = 11, loX = 0, loY = 0, pass = new bool[121], water = new bool[121], known = new bool[121], noPlace = new bool[121], wall = new bool[121] };
        var gapTiles = new HashSet<(int, int)> { (1, 4), (2, 4), (5, 4), (8, 4), (9, 4) };
        for (int x = 0; x < 11; x++) for (int y = 0; y < 11; y++) { int i = x * 11 + y; g.known[i] = true; g.pass[i] = y != 4 || gapTiles.Contains((x, y)); g.wall[i] = !g.pass[i]; }
        var o = new EdgeOption(); for (int x = 0; x < 11; x++) for (int y = 0; y < 4; y++) o.inside.Add(EdgeGrid.Key(x, y));
        var g1 = new EdgeGap(); g1.tiles.Add((1, 4)); g1.tiles.Add((2, 4));
        var g2 = new EdgeGap(); g2.tiles.Add((5, 4));
        var g3 = new EdgeGap(); g3.tiles.Add((8, 4)); g3.tiles.Add((9, 4));
        o.gaps.Add(g1); o.gaps.Add(g2); o.gaps.Add(g3);
        var bs = AIBaseEdgeCore.Batches(g, o, (8, 4));
        var b1 = bs.Find(b => b.gap == 0); var b3 = bs.Find(b => b.gap == 2);
        Program.Check(b1.closesGap && !b1.closesToDoor && !b3.closesGap && b3.closesToDoor && b3.closeTile == (9, 4),
            $"F3a. the door gap's last batch: no closing of the gap (the door spot stays open) but its last gap tile (9,4) is the outside option's tile ({b3.closeTile})");
        var open = new List<(int x, int y)>(gapTiles);
        // closing gap 1 from outside at (2,5): gap 2 and the door gap stay open
        var after1 = AIBaseGuard.OpenAfter(open, null, g1.tiles);
        string t1 = AIBaseGuard.OutsideCloseText(null, after1, (2, 5));
        Program.Check(t1 == "바깥에서 닫음, 둘레에 아직 열린 곳 2곳 있음(가장 가까운 (5,4)) — 그쪽으로 드나들 수 있음",
            $"F3b. closing gap 1 from outside with gap 2 and the door gap open: the open places, not 'break a wall' ('{t1}')");
        // the door gap's last batch from outside at (9,5): only the door spot stays open (with gap 1, 2 shut)
        var after3 = AIBaseGuard.OpenAfter(new List<(int x, int y)> { (8, 4), (9, 4) }, null, new[] { (9, 4) });
        string t3 = AIBaseGuard.OutsideCloseText(null, after3, (9, 5));
        Program.Check(t3 == "바깥에서 닫음, 둘레에 아직 열린 곳 1곳 있음(가장 가까운 (8,4)) — 그쪽으로 드나들 수 있음", $"F3c. the door gap from outside: the door spot (8,4) is the way in ('{t3}')");
        // the edge shut by it: the user's two sentences
        Program.Check(AIBaseGuard.OutsideCloseText((8, 4), AIBaseGuard.OpenAfter(new[] { (5, 4) }, null, new[] { (5, 4) }), (5, 5)) == "바깥에서 닫음, 둘레 문 (8,4)로 드나듦"
            && AIBaseGuard.OutsideCloseText(null, new List<(int x, int y)>(), (5, 5)) == "바깥에서 닫음, 둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함",
            "F3d. closing the last open place: '둘레 문 (x,y)로 드나듦' with a door, '벽 하나를 부숴야 함' without");
        // broken parts are open places; a door spot's side wall off the cut is none; a gone door is one (S8: it is no door to come in by)
        var scan = new BreachScan();
        scan.list.Add(new Breach { tile = (0, 6), kind = BreachKind.WallGone });
        scan.list.Add(new Breach { tile = (7, 3), kind = BreachKind.WallGone, side = true });
        scan.list.Add(new Breach { tile = (8, 4), kind = BreachKind.DoorGone });
        var afterR = AIBaseGuard.OpenAfter(null, scan, new[] { (0, 6) });
        Program.Check(afterR.Count == 1 && afterR[0] == (8, 4), $"F3e. repair closing (0,6): the side wall off the cut opens no way, the gone door (8,4) stays an open place ({afterR.Count})");
        string tR = AIBaseGuard.OutsideCloseText(null, afterR, (0, 7));
        Program.Check(!tR.Contains("둘레 문") && tR.Contains("열린 곳 1곳") && tR.Contains("(8,4)"), $"F3f. (S8) a remembered door that is gone is an open place, not '둘레 문으로 드나듦' ('{tR}')");
    }

    // F4: the edge options pushed a gather goal's progress options out of the 14 (GOAL stalled: no progress option)
    static void F4()
    {
        AsmInput Story(bool asBefore)
        {
            var a = In(GoalKind.Gather);
            a.curKey = "mine:3,3"; a.lCont.Add(C("continue", "mine:3,3"));   // continue: no progress mark (as built)
            for (int i = 0; i < 2; i++) a.lEnemy.Add(C("attack", $"attack:{i}"));
            a.lLife.Add(C("eat", "eat:berry"));
            // two holes + two gaps: repair 2 + :out 2, fill 2 + :out 2, materials 2, torch 1, door 1 (12 edge options)
            var edge = new List<AsmCand>();
            foreach (var k in new[] { "edgerepair:1,1", "edgerepair:1,1:out", "edgerepair:2,2", "edgerepair:2,2:out", "edgefill:0:3,4", "edgefill:0:3,4:out", "edgefill:1:6,4", "edgefill:1:6,4:out", "edgemat:a", "edgemat:b", "place:Torch@1,1", "place:WoodDoor@8,4" })
                edge.Add(C(k.Split(':')[0], k));
            if (asBefore) a.lCraft.AddRange(edge); else a.lEdge.AddRange(edge);
            a.lMine.Add(C("mine", "mine:root1", true)); a.lMine.Add(C("mine", "mine:root2", true));
            a.lItem.Add(C("pickup", "pickup:wood", true));
            a.lExplore.Add(C("explore", "explore:9,9"));
            return a;
        }
        // the Loop's stall rule (AIBrain.Send): no candidate marked progress -> GOAL stalled
        bool Stalls(List<AsmCand> l) => !l.Exists(c => c.progress);
        var before = Build(Story(true), false);
        Program.Check(!Stalls(before) && Has(before, "mine:root1") && Has(before, "pickup:wood"), $"F4a. removing the count quota keeps goal work even in the historical crowded craft-first ordering ({before.Count - 1} options)");
        var now = Build(Story(false), false);
        int edgeN = now.FindAll(c => c.edge).Count;
        Program.Check(!Stalls(now) && Has(now, "mine:root1") && Has(now, "mine:root2") && Has(now, "pickup:wood"), $"F4b. the edge options after the goal's own: every progress option stays, the goal does not stall ({string.Join(" ", now.ConvertAll(c => c.key))})");
        Program.Check(Has(now, "mine:3,3") && edgeN > 0 && now.FindIndex(c => c.key == "mine:root1") < now.FindIndex(c => c.key == "edgerepair:1,1"), $"F4c. 'continue' is kept; the goal's options come before the edge options, which fill the rest ({edgeN})");
        // more than 14 other options (8 filler mines with no progress): the edge options still keep their share, taken
        // from the fillers, never from a progress option or 'continue'
        var crowd = Story(false);
        for (int i = 0; i < 8; i++) crowd.lMine.Insert(0, C("mine", $"mine:filler{i}"));
        var lcr = Build(crowd, false);
        int edgeC = lcr.FindAll(c => c.edge).Count;
        Program.Check(edgeC == crowd.lEdge.Count && crowd.lEdge.TrueForAll(c => lcr.Contains(c)) && lcr.Count > 15 && Has(lcr, "mine:root1") && Has(lcr, "mine:root2") && Has(lcr, "mine:3,3") && Has(lcr, "mine:filler0") && Has(lcr, "mine:filler7"),
            $"F4d. crowded: every edge, root, filler and continue stays without quota ({edgeC} edge options)");
        // no edge options: the cut is the old positional one (the L1 replay covers the rest)
        var plain = In(GoalKind.Gather); for (int i = 0; i < 20; i++) plain.lOther.Add(C("x", $"x:{i}"));
        var lp = Build(plain, false);
        Program.Check(lp.Count == 21 && lp[19].key == "x:19" && lp[20].kind == "wait", "F4e. all twenty ordinary choices and wait remain without edge options");
    }

    // S1: in the door tile the tail closes from the inside stand; outside only when the inside cannot be stood on
    static void S1()
    {
        var o = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o, false, -4.01f, -6.46f, 1, true, true);
        Program.Check(o.dirY > 0, "S1a. in the door tile (-4.01,-6.46), inside north: it closes from the inside");
        var o2 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o2, false, -4.01f, -5.9f, -1, false, true);
        Program.Check(o2.dirY > 0, "S1b. inside south but its stand cannot be stood on: the outside close (north) is used");
        var o3 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o3, false, -4f, -6.8f, 1, true, true);
        Program.Check(o3.dirY < 0, "S1c. out of the door tile (on the outside half past the tile edge) it closes from where it is, as before");
    }

    // S2: a lower max health hides only the health above the new max
    static void S2()
    {
        Program.Check(AIBaseGuard.HurtBy(80, 150, 60, 100), "S2a. health 80 of 150, the bonus ends (max 100) and a hit of 20: a hit");
        Program.Check(!AIBaseGuard.HurtBy(120, 150, 100, 100), "S2b. health 120 of 150, the max drops to 100 and the health with it: no hit");
        Program.Check(!AIBaseGuard.HurtBy(100, 100, 75, 75) && AIBaseGuard.HurtBy(75, 75, 50, 75), "S2c. hunger still no hit; a hit while starving still one");
    }

    // S3: "cannot come" over remembered edge tiles says it rests on memory
    static void S3()
    {
        var inside = new List<(int, int)>(); for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) inside.Add((x, y));
        var ins = new HashSet<(int, int)>(inside);
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)> { (0, -3) });
        GuardCell W((int x, int y) t) => t == (0, -3) ? GuardCell.OwnDoor : ins.Contains(t) ? GuardCell.Open : t == (3, 0) ? GuardCell.Unknown : shape.IsEdge(t.x, t.y) ? GuardCell.Closed : GuardCell.Open;
        int n = 31; var g = GuardGrid.Make(-15, -15, n, GuardCell.Unknown);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) g.cell[i * n + j] = W((g.loX + i, g.loY + j));
        var mem = new BaseMemory(shape, null, new[] { (0, -3) });
        var r = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0), mem), (6, 0));
        string f = AIBaseGuard.KillFacts(shape, (6, 0), r, "Slime");
        Program.Check(!r.comes && r.memEdge == 1 && f.Contains("지금 안 보이는 곳의 기억으로 정함") && f.Contains("본 적 없는 둘레 1칸"), $"S3a. the enemy cannot come over an unseen edge tile taken from memory: the fact says so ('{f}')");
        var gSeen = GuardGrid.Make(-15, -15, n, GuardCell.Unknown);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) gSeen.cell[i * n + j] = W((gSeen.loX + i, gSeen.loY + j)) == GuardCell.Unknown ? GuardCell.Closed : W((gSeen.loX + i, gSeen.loY + j));
        var r2 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gSeen, (0, 0), mem), (6, 0));
        Program.Check(!r2.comes && !AIBaseGuard.KillFacts(shape, (6, 0), r2, "Slime").Contains("기억한 둘레"), "S3b. every edge tile seen: no memory note");
    }

    // S4: AIBody.Reset aborts the tail (AIEdge.TailAbort); then a door seen shut is owed nothing (the body flag is
    // AIBody's and compile-checked only)
    static void S4()
    {
        var o = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o, false, -4f, -5.2f);
        AIBaseGuard.NoteDoorState(o, true, false);
        bool keptWhileTail = o.door.HasValue;
        o.tail = false;   // Reset -> TailAbort
        AIBaseGuard.NoteDoorState(o, true, false);
        Program.Check(keptWhileTail && !o.door.HasValue, "S4. while the tail runs the owed door is kept; after Reset (tail aborted) a shut door is owed nothing");
    }

    // S5: a breach reason of an older plan is not carried into a call of the new plan
    static void S5()
    {
        var w = new AIBaseGuard.BreachWatch();
        AIBaseGuard.BreachTrigger(w, 3, new List<Breach> { new Breach { tile = (1, 1), kind = BreachKind.WallGone } });
        Program.Check(AIBaseGuard.TakePending(w, "기술 끝", 4) == "기술 끝" && w.pending == "", "S5a. the memory cleared / a new plan (4): the old plan's breach reason is dropped");
        AIBaseGuard.BreachTrigger(w, 4, new List<Breach> { new Breach { tile = (2, 2), kind = BreachKind.WallGone } });
        Program.Check(AIBaseGuard.TakePending(w, "기술 끝", 4).Contains("기지 둘레 부서짐 1곳"), "S5b. this plan's breach reason is carried");
    }

    // S7: the chosen door gap when the adopted own door is on another gap
    static void S7()
    {
        var adopted = new List<(int gap, (int x, int y) tile)> { (0, (-10, -18)) };
        string n2 = AIBaseEdgeCore.AdoptGapNote(adopted, 2, (-10, -18));
        Program.Check(n2.Contains("고른 문틈 2") && n2.Contains("(-10,-18)(틈 1)") && n2.Contains("틈 2의 칸은 모두 벽으로 메움"), $"S7a. pick N 2 with my door on gap 1: said, not ignored ('{n2}')");
        Program.Check(AIBaseEdgeCore.AdoptGapNote(adopted, 1, (-10, -18)) == "" && AIBaseEdgeCore.AdoptGapNote(adopted, 0, (-10, -18)) == "" && AIBaseEdgeCore.AdoptGapNote(new List<(int, (int, int))>(), 2, null) == "",
            "S7b. the door on the chosen gap, no gap chosen, or nothing adopted: no note");
    }

    // N1: the close option of the door the tail is closing now says so, and choosing it continues the tail
    static void N1()
    {
        var o = new DoorOwe { door = (-4, -6) };
        Program.Check(!AIBaseGuard.TailContinues(o, "doorclose:-4,-6") && AIBaseGuard.TailNote(o) == "", "N1a. no tail running: the close option is a new close (no note)");
        AIBaseGuard.StartTail(o, false, -4f, -5.2f, 1, true, true);
        string n0 = AIBaseGuard.TailNote(o);
        Program.Check(AIBaseGuard.TailContinues(o, "doorclose:-4,-6") && !AIBaseGuard.TailContinues(o, "doorclose:5,5") && n0.Contains("지금 이 문을 닫는 중") && n0.Contains("문 앞 자리로 가서 E로 닫기") && n0.Contains("처음부터 다시 하지 않음"),
            $"N1b. tail running: the same door's close continues it, another door's does not; the option says it is closing now and what is left ('{n0}')");
        float t = 0f, y = -5.2f;
        for (int k = 0; k < 200 && o.st.presses == 0; k++, t += 0.05f) { var r = AIBaseGuard.TailStep(o, true, true, -4f, y, t, out _); y += r.my * 0.05f * 5.7f; }
        Program.Check(AIBaseGuard.TailNote(o).Contains("E 1번 누름"), $"N1c. after the press the remaining step is the shut check ('{AIBaseGuard.TailNote(o)}')");
    }

    // N2: the edge share takes no combat option, eat or own-door close
    static void N2()
    {
        var a = In(GoalKind.Gather);
        a.curKey = "mine:3,3"; a.lCont.Add(C("continue", "mine:3,3"));
        a.lEnemy.Add(C("attack", "attack:1")); a.lEnemy.Add(C("attack", "attack:2"));
        a.lCombat.Add(C("away", "away:0")); a.lCombat.Add(C("retreat", "retreat:0,0"));
        a.lDoor.Add(C("doorclose", "doorclose:-4,-6"));
        a.lCraft.Add(C("craft", "craft:filler"));
        a.lLife.Add(C("eat", "eat:berry"));
        for (int i = 0; i < 4; i++) a.lMine.Add(C("mine", $"mine:root{i}", true));
        for (int i = 0; i < 4; i++) a.lExplore.Add(C("explore", $"explore:{i}", true));
        foreach (var k in new[] { "place:Torch@1,1", "edgemat:a", "edgemat:b" }) a.lEdge.Add(C(k.Split(':')[0], k));
        var l = Build(a, false);
        bool allKept = Has(l, "attack:1") && Has(l, "attack:2") && Has(l, "away:0") && Has(l, "retreat:0,0") && Has(l, "doorclose:-4,-6") && Has(l, "eat:berry") && Has(l, "mine:3,3");
        Program.Check(allKept && l.FindAll(c => c.edge).Count == a.lEdge.Count && Has(l, "craft:filler"), $"N2. no edge quota displaces combat, goal work, other choices or continue ({string.Join(" ", l.ConvertAll(c => c.key))})");
    }

    // N3: the memory note only for an enemy whose answer memory changed
    static void N3()
    {
        // a base far off (beyond the window: every edge tile is "멀어서 안 보이는"); the enemy in a sealed room by the AI
        var inside = new List<(int, int)>(); for (int x = 40; x <= 44; x++) for (int y = -2; y <= 2; y++) inside.Add((x, y));
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)>());
        int n = 31; var g = GuardGrid.Make(-15, -15, n, GuardCell.Open);
        for (int y = -15; y <= 15; y++) { g.cell[g.Idx(3, y)] = GuardCell.Closed; g.cell[g.Idx(15, y)] = GuardCell.Closed; }
        for (int x = 3; x <= 15; x++) { g.cell[g.Idx(x, 15)] = GuardCell.Closed; g.cell[g.Idx(x, -15)] = GuardCell.Closed; }
        var mem = new BaseMemory(shape, null, null);
        var r = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0), mem), (6, 0));
        string f = AIBaseGuard.KillFacts(shape, (6, 0), r, "Slime");
        Program.Check(!r.comes && r.memBeyond > 0 && !r.memChanged && !f.Contains("기억한 둘레"), $"N3a. an enemy sealed off anyway, the base far beyond the window ({r.memBeyond} edge tiles remembered): no memory note");
        // the S3 case (an unseen edge tile memory closes) still says it
        var ins2 = new List<(int, int)>(); for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) ins2.Add((x, y));
        var set2 = new HashSet<(int, int)>(ins2);
        var sh2 = AIBaseGuard.Shape(ins2, new List<(int, int)> { (0, -3) });
        var g2 = GuardGrid.Make(-15, -15, n, GuardCell.Open);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        {
            var t = (g2.loX + i, g2.loY + j);
            g2.cell[i * n + j] = t == (0, -3) ? GuardCell.OwnDoor : set2.Contains(t) ? GuardCell.Open : t == (3, 0) ? GuardCell.Unknown : sh2.IsEdge(t.Item1, t.Item2) ? GuardCell.Closed : GuardCell.Open;
        }
        var r2 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g2, (0, 0), new BaseMemory(sh2, null, new[] { (0, -3) })), (6, 0));
        Program.Check(r2.memChanged && AIBaseGuard.KillFacts(sh2, (6, 0), r2, "Slime").Contains("지금 안 보이는 곳의 기억으로 정함"), "N3b. memory closed the unseen edge tile the enemy would come by: the note stays");
    }

    // N4: a breach call puts the repairs and door re-hangs first in the edge share; the left-out edge options are one line
    static AsmCand E(string kind, string key, string desc, EdgeCandidateRole role = EdgeCandidateRole.None) { var c = C(kind, key); c.desc = desc; c.edgeRole = role; return c; }
    static AsmInput Crowd(bool breach)
    {
        var a = In(GoalKind.Gather); a.breachCall = breach;
        a.curKey = "mine:3,3"; a.lCont.Add(C("continue", "mine:3,3"));
        for (int i = 0; i < 8; i++) a.lMine.Add(C("mine", $"mine:filler{i}"));
        for (int i = 0; i < 6; i++) a.lMine.Add(C("mine", $"mine:root{i}", true));
        a.lEdge.Add(E("edgefill", "edgefill:0:1,4", "기지 둘레 틈 1 메우기"));
        a.lEdge.Add(E("edgefill", "edgefill:0:1,4:out", "기지 둘레 틈 1 메우기(바깥에서 닫기)"));
        a.lEdge.Add(E("place", "place:Torch@1,1", "기지 안 횃불 놓기", EdgeCandidateRole.TorchPlace));
        a.lEdge.Add(E("edgerepair", "edgerepair:5,5", "기지 둘레 보수"));
        a.lEdge.Add(E("edgerepair", "edgerepair:5,5:out", "기지 둘레 보수(바깥에서 닫기)"));
        a.lEdge.Add(E("place", "place:WoodDoor@8,4", "기지 문 다시 달기 — (8,4) 달았던 문이 없어짐", EdgeCandidateRole.DoorRepair));
        return a;
    }
    static List<AsmCand> RunCut(AsmInput a, out List<AsmCand> cut)
    {
        var l = AICandidateAssembler.Select(a, c => true, c => true, out _, out _, out _);
        AICandidateAssembler.Finish(l, new AsmCand { id = ++_id, kind = "wait", key = "wait" }, 14, out cut);
        return l;
    }
    static void N4()
    {
        var plain = RunCut(Crowd(false), out var cut0);
        Program.Check(Has(plain, "place:WoodDoor@8,4") && Has(plain, "edgerepair:5,5:out") && cut0.Count == 0, "N4a. an ordinary crowded call retains every repair and door option");
        var br = RunCut(Crowd(true), out var cut);
        Program.Check(Has(br, "edgerepair:5,5") && Has(br, "edgerepair:5,5:out") && Has(br, "place:WoodDoor@8,4") && br.FindAll(c => c.edge).Count == 6 && cut.Count == 0
            && br.FindIndex(c => c.key == "edgerepair:5,5") < br.FindIndex(c => c.key == "edgefill:0:1,4"),
            $"N4b. breach still orders repairs first while retaining all six edge choices");
        var renamed = Crowd(true);
        foreach (var c in renamed.lEdge) c.desc = "a changed display sentence";
        var renamedResult = RunCut(renamed, out var renamedCut);
        Program.Check(string.Join("|", renamedResult.ConvertAll(c => c.key)) == string.Join("|", br.ConvertAll(c => c.key))
            && AICandidateAssembler.EdgeCutNote(renamedCut) == AICandidateAssembler.EdgeCutNote(cut),
            "R2a: changing every edge description leaves repair priority, quota, order and cut categories unchanged");
        Program.Check(!AICandidateAssembler.IsRepair(new AsmCand { kind = "place", desc = "기지 문 다시 달기" })
            && AICandidateAssembler.IsRepair(new AsmCand { kind = "place", edgeRole = EdgeCandidateRole.DoorRepair }),
            "R2a: repair meaning comes from the role, never from the description");
        string note = AICandidateAssembler.EdgeCutNote(cut);
        Program.Check(note == "" && AICandidateAssembler.EdgeCutNote(new List<AsmCand>()) == "",
            "N4c. no count cut means no misleading omitted-edge note");
        string n0 = AICandidateAssembler.EdgeCutNote(cut0);
        Program.Check(n0 == "", "N4d. an ordinary call has no omitted-edge note either");
    }
}
