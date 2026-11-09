// G3 (continued) — the stage 3 live-run fixes (15-실측결함수정.md; the facts are 14-단계3-측정.md 0절 1-8, user
// decisions A and B). Where possible the measured situation is rebuilt as it was logged (C:\Dev\_scratch\base-s3).
using System;
using System.Collections.Generic;

public static class GuardLiveTests
{
    static List<(int x, int y)> Rect(int x1, int y1, int x2, int y2)
    {
        var l = new List<(int, int)>();
        for (int x = x1; x <= x2; x++) for (int y = y1; y <= y2; y++) l.Add((x, y));
        return l;
    }

    // a walk window around `at` (2r+1 square) over a world function
    static GuardGrid Window((int x, int y) at, int r, Func<(int x, int y), GuardCell> world)
    {
        int n = 2 * r + 1;
        var g = GuardGrid.Make(at.x - r, at.y - r, n, GuardCell.Unknown);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) g.cell[i * n + j] = world((g.loX + i, g.loY + j));
        return g;
    }

    public static void Run()
    {
        Fix1(); Fix2(); Fix3(); Fix4(); Fix5(); Fix6(); Fix7(); Fix8(); DecisionA(); DecisionB(); Small();
    }

    // 사실 1: option 4 base (y -19 … 11, 31 tall) with its door (-10,-18) on the bottom edge; the AI at (-10,-15); the
    // window is 49x49 (ReachRange 24): y -39 … 9, so the inside runs off the window's top. A MushroomEnemy at (-10,-21)
    // past the shut door was a threat (12 s, 30 calls).
    static void Fix1()
    {
        var inside = Rect(-16, -17, -4, 11);
        var ins = new HashSet<(int, int)>(inside);
        (int, int) door = (-10, -18);
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)> { door });
        GuardCell World((int x, int y) t)
        {
            if (t == door) return GuardCell.OwnDoor;
            if (ins.Contains(t)) return GuardCell.Open;
            if (shape.IsEdge(t.x, t.y)) return GuardCell.Closed;   // the ring of natural walls
            return GuardCell.Open;                                  // the cave outside
        }
        (int, int) ai = (-10, -15), enemy = (-10, -21);
        var g = Window(ai, 24, World);
        var old = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, ai), enemy);
        var mem = new BaseMemory(shape, null, new[] { door });
        var now = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, ai, mem), enemy);
        Program.Check(old.comes, $"L1a. (as measured) without the base memory the inside leaves by the window's top and the enemy past the shut door 'can come' ({old.kind} comes={old.comes})");
        Program.Check(!now.comes && now.kind == ReachKind.Door && now.door == door && !AIBaseGuard.IsThreat(now, 6f, 8f), $"L1b. with the edge memory: the enemy past the shut door cannot come, no threat; the way is through door (-10,-18) ({now.kind} {now.door} {now.steps} comes={now.comes})");
        // an edge tile in the window that was never seen: memory says natural wall (the unknown rule is not for the base)
        var g2 = Window(ai, 24, t => t == (-3, -10) ? GuardCell.Unknown : World(t));
        var old2 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g2, ai), (2, -10));
        var now2 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g2, ai, mem), (2, -10));
        Program.Check(old2.comes && !now2.comes, $"L1c. an unseen edge tile is remembered closed (the unknown-tile rule only off the base): before comes={old2.comes}, now comes={now2.comes}");
        // a gap tile not built yet beyond the window: the base may lead out there — the window rule stays
        var shapeGap = AIBaseGuard.Shape(inside, new List<(int, int)> { door, (-8, 12) });
        var memGap = new BaseMemory(shapeGap, null, new[] { door });
        var gapR = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, ai, memGap), enemy);
        Program.Check(gapR.comes && memGap.OpenBeyond(g), $"L1d. a remembered open gap (-8,12) beyond the window: the inside may lead out there, the enemy can come (unknown) ({gapR.kind} comes={gapR.comes})");
        // option 2 sized base (fits the window): the same answer as before (no change for small bases)
        var small = Rect(-5, -5, -3, -1); var smallS = new HashSet<(int, int)>(small);
        var shS = AIBaseGuard.Shape(small, new List<(int, int)> { (-4, -6) });
        GuardCell W2((int x, int y) t) => t == (-4, -6) ? GuardCell.OwnDoor : smallS.Contains(t) ? GuardCell.Open : shS.IsEdge(t.x, t.y) ? GuardCell.Closed : GuardCell.Open;
        var gS = Window((-4, -4), 24, W2);
        var a = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gS, (-4, -4)), (-6, -10));
        var b = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gS, (-4, -4), new BaseMemory(shS, null, new[] { (-4, -6) })), (-6, -10));
        Program.Check(!a.comes && !b.comes && a.kind == b.kind && a.steps == b.steps, $"L1e. a base inside the window: the same reach with or without memory ({a.kind} {a.steps} / {b.kind} {b.steps})");
    }

    // 사실 2: pressed against the shut tunnel door (-4,-6) at (-3.85,-5.65), the rounded tile was the door tile; from
    // there the outside was an open way (the enemy a threat, 9.7 s 20 calls) and the own-door branch was off.
    static void Fix2()
    {
        (int, int) door = (-4, -6);
        var st = AIBaseGuard.StandTile(-3.85f, -5.65f, t => t != door);
        Program.Check(st == (-4, -5), $"L2a. the stand tile next to a shut door is the nearest tile it can stand on, not the door tile ({st.Item1},{st.Item2})");
        var st2 = AIBaseGuard.StandTile(-4.1f, -4.6f, t => t != door);
        Program.Check(st2 == (-4, -5), $"L2b. away from the door the stand tile is the rounded one ({st2.Item1},{st2.Item2})");
        // tunnel base: inside (-4,-5)…(-4,-1) corridor, door (-4,-6), the cave south
        var inside = Rect(-4, -5, -4, -1); var ins = new HashSet<(int, int)>(inside);
        GuardCell W((int x, int y) t) => t == door ? GuardCell.OwnDoor : ins.Contains(t) ? GuardCell.Open : t.y <= -7 ? GuardCell.Open : GuardCell.Closed;
        var g = Window((-4, -5), 24, W);
        var fromDoor = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, door), (-4, -8));
        var fromStand = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, st), (-4, -8));
        Program.Check(fromDoor.comes && fromDoor.kind == ReachKind.Open && !fromStand.comes && fromStand.kind == ReachKind.Door,
            $"L2c. (as measured) from the door tile the enemy 2 tiles out is an open way and comes; from the stand tile it is past the door and cannot come ({fromDoor.kind}/{fromDoor.comes} -> {fromStand.kind}/{fromStand.comes})");
    }

    // 사실 3: a walk to (-4,-5), a kill in the doorway and a retreat to (-4,-5) ended next to the door with it open; the
    // enemy followed in and the AI went down. Now the AI owes the door a close and the end waits for it.
    static void Fix3()
    {
        var o = new DoorOwe();
        AIBaseGuard.NoteDoorTile(o, (-4, -5), false);
        Program.Check(!o.door.HasValue, "L3a. not in a door: nothing owed");
        AIBaseGuard.NoteDoorTile(o, (-4, -6), true);
        AIBaseGuard.NoteDoorState(o, true, true);
        Program.Check(o.door == (-4, -6), "L3b. standing in the open own door (-4,-6): it is owed a close");
        Program.Check(AIBaseGuard.TailNeeded(o, true, 0.77f, false) && !AIBaseGuard.TailNeeded(o, true, 4f, false) && !AIBaseGuard.TailNeeded(o, true, 0.77f, true) && !AIBaseGuard.TailNeeded(o, false, 0.77f, false),
            "L3c. the closing tail runs at a skill's end when the owed door is open and the AI is within 2.5 tiles (not when far, down, or the door is shut)");
        // the retreat ended at (-4,-5.23) right behind the door (measured ⑨-b): it closes from inside
        AIBaseGuard.StartTail(o, false, -4f, -5.23f);
        bool open = true, done = false; float t = 0f; int presses = 0; float y = -5.23f;
        for (int k = 0; k < 200 && !done; k++, t += 0.05f)
        {
            var r = AIBaseGuard.TailStep(o, open, true, -4f, y, t, out done);
            y += r.my * 0.05f * 5.7f;
            if (r.press) { presses++; open = false; }
        }
        Program.Check(done && o.dirY > 0 && presses == 1 && !o.door.HasValue && o.fact.StartsWith("지난 문 (-4,-6) 닫음"), $"L3d. retreat end behind the door: closed from the inside side with one press ('{o.fact}')");
        // the kill in the doorway at (-4.01,-6.46): in the door tile itself (review S1, 2026-11-06) the side is no fact of
        // where the AI is: it closes from the inside stand (-4,-5) (the outside close failed in the live run)
        var o2 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o2, false, -4.01f, -6.46f, 1, true, true);
        float y2 = -6.46f; bool d2 = false; t = 0f; open = true;
        for (int k = 0; k < 200 && !d2; k++, t += 0.05f)
        {
            var r = AIBaseGuard.TailStep(o2, open, true, -4f, y2, t, out d2);
            y2 += r.my * 0.05f * 5.7f;
            if (r.press) open = false;
        }
        Program.Check(d2 && o2.dirY > 0 && y2 > -5.5f && !o2.door.HasValue, $"L3e. a kill in the doorway (in the door tile): it goes to the inside stand (y {y2:F2}) and closes from the inside (review S1)");
        // the door does not answer: the failure is a fact and the door stays owed (the close option says so)
        var o3 = new DoorOwe { door = (-4, -6) };
        AIBaseGuard.StartTail(o3, false, -4f, -5.3f);
        bool d3 = false; t = 0f;
        for (int k = 0; k < 400 && !d3; k++, t += 0.05f) AIBaseGuard.TailStep(o3, true, true, -4f, -5.3f, t, out d3);
        var closeWait = AIBaseGuard.TailStep(o3, true, true, -4f, -5.3f, t, out d3);
        Program.Check(!d3 && o3.door.HasValue && o3.st.phase == DoorPhase.Close && closeWait.observation.Contains("문 닫기"), $"L3f. unanswered closing remains active and owed with '{closeWait.observation}'");
        AIBaseGuard.TailStep(o3, false, true, -4f, -5.3f, t + 0.05f, out d3);
        Program.Check(d3 && !o3.door.HasValue, "L3g. the same pending close completes when the door is observed shut");
    }

    // 사실 4: the game's path search gave a (partial) way and the AI got stuck at the shut door; the stuck walk went to
    // the tile A* with no tries and failed "길 없음" without the door.
    static void Fix4()
    {
        var s1 = AIDoorPass.NoPathNext(ownDoorToward: true, doorsPassable: false, passedDoor: false, shortHopTried: false, shortNear: false, knownWay: false, padHopDone: false, onPadWithExit: true);
        Program.Check(s1 == AIDoorPass.NoPathStep.OwnDoor, $"L4a. stuck on the game's path from the spawn pad to an outside target (pad -> (-10,-21)): the own door is the next try, not 'no way' ({s1})");
        var s2 = AIDoorPass.NoPathNext(true, false, false, false, false, true, false, false, doorAhead: true);
        var s3 = AIDoorPass.NoPathNext(true, false, false, false, false, true, false, false);
        Program.Check(s2 == AIDoorPass.NoPathStep.OwnDoor && s3 == AIDoorPass.NoPathStep.ShortHop, $"L4b. stuck right at the own door: the door before a known-way hop ({s2}); not stuck there: the known way first as before ({s3})");
        var s4 = AIDoorPass.NoPathNext(true, false, true, false, false, false, false, false, doorAhead: true);
        Program.Check(s4 == AIDoorPass.NoPathStep.FailAfterDoor, $"L4c. one door per walk still: past a door, stuck again is no second door ({s4})");
    }

    // 사실 5: the tunnel door (-4,-6) between natural walls (-5,-6) and (-3,-6); mining (-5,-6) let the door fall into the
    // bag; (-5,-6) was no breach, the door-back note pointed to an empty fill batch, the materials offered (-5,-6)/(-3,-6).
    static void Fix5()
    {
        var inside = Rect(-4, -5, -4, -1);
        (int, int) door = (-4, -6);
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)> { door }, null, door, true);
        Program.Check(shape.IsEdge(-5, -6) && shape.IsEdge(-3, -6) && shape.sides.Contains(EdgeGrid.Key(-5, -6)), "L5a. the hung door's two natural side walls are edge memory (so no material wall)");
        var fact = AIBaseGuard.WallFact(shape, (-3, -6));
        string note = fact.Text;
        Program.Check(note.Contains("캐면 문이 떨어짐") && fact.OpensEdge, $"L5b. the mine option on a side wall says the door falls and the edge opens ('{note}')");
        Program.Check(fact.Kind == EdgeWallKind.HungDoorSide && fact.Door == (-4, -6) && fact.Text == AIBaseGuard.DoorFallText((-4, -6)), "R2b. hung door fact preserves coordinate and exact text");
        // as measured: (-5,-6) mined, the door gone
        var scan = AIBaseGuard.Breaches(shape, new List<(int, int)>(), new List<(int, int)> { door }, t => t == (-5, -6) || t == door ? EdgeNow.Open : EdgeNow.Closed);
        var side = scan.list.Find(b => b.tile == (-5, -6));
        Program.Check(side != null && side.kind == BreachKind.NaturalGone && side.side && scan.list.Exists(b => b.kind == BreachKind.DoorGone && b.tile == door),
            $"L5c. the side wall gone and the door gone are both broken parts ({string.Join(", ", scan.list.ConvertAll(b => b.Text))})");
        var bs = AIBaseGuard.RepairBatches(scan, t => null);
        Program.Check(bs.Count == 1 && bs[0].tiles.Count == 1 && bs[0].tiles[0] == (-5, -6) && bs[0].group.Count == 0, "L5d. the side wall has its own repair batch (no opening of its own); the door goes back by 'door back'");
        var need = new AIBaseEdgeCore.DoorSideNeed { eastWest = true };
        need.place.Add((-5, -6));
        string txt = need.Text(-4, -6, t => shape.sides.Contains(EdgeGrid.Key(t.x, t.y)));
        Program.Check(txt.Contains("보수 묶음에 들어 있음") && !txt.Contains("메우기 묶음"), $"L5e. the door-back note points to the repair batch, not an empty fill batch ('{txt}')");
        var before = AIBaseGuard.Shape(inside, new List<(int, int)> { door });
        Program.Check(!before.IsEdge(-5, -6), "L5f. before the door is hung the side tiles are no edge memory (the fill batches handle them)");
    }

    // 사실 6: '/ai hunger 0' took health 100 -> 75 within 1 s with HealthCD.maxHealth 100 unchanged: the game lowers the
    // max with conditions (StarvingHealthDecrease -> MaxHealthPercentage -25%, HungerAndRunningSystem /
    // HealthCD.GetMaxHealthWithConditions), so the vitals sample that max now.
    static void Fix6()
    {
        Program.Check(AIBaseGuard.HurtBy(100, 100, 75, 100), "L6a. (as measured) read against HealthCD.maxHealth, starving 100 -> 75 looked like a hit");
        Program.Check(!AIBaseGuard.HurtBy(100, 100, 75, 75), "L6b. against the max with conditions (75 at hunger 0) it is no hit: no self-defence, no fence enemy made a threat");
        Program.Check(AIBaseGuard.HurtBy(75, 75, 50, 75), "L6c. a hit while starving is still a hit");
    }

    // 사실 7: the wood goal cut 7 edge roots; breaches 1 -> 3 were logged, but the trigger was in no call: the skill-end
    // reason overwrote it before the call went out.
    static void Fix7()
    {
        var w = new AIBaseGuard.BreachWatch();
        string r1 = AIBaseGuard.BreachTrigger(w, 1, new List<Breach> { new Breach { tile = (-5, -5), kind = BreachKind.WallGone }, new Breach { tile = (3, -1), kind = BreachKind.NaturalGone } });
        string waiting = "기술 끝(뿌리 캐기 완료)";   // AIBrain.OnSkillEnded overwrote the waiting reason
        string sent = AIBaseGuard.TakePending(w, waiting);
        Program.Check(r1 != null && sent.StartsWith("기술 끝") && sent.Contains("기지 둘레 부서짐 2곳"), $"L7a. the breach reason reaches the next call even after the skill end overwrote the waiting reason ('{sent}')");
        Program.Check(AIBaseGuard.TakePending(w, "x") == "x", "L7b. it is carried once");
        AIBaseGuard.BreachTrigger(w, 1, new List<Breach> { new Breach { tile = (-5, -5) }, new Breach { tile = (3, -1) }, new Breach { tile = (3, 0), kind = BreachKind.NaturalGone } });
        Program.Check(AIBaseGuard.TakePending(w, "").Contains("3곳"), "L7c. a newer count replaces the older waiting one");
        // the east edge root (3,-1) of the core room
        var inside = Rect(-2, -1, 2, 1);
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)>());
        string root = AIBaseGuard.EdgeTileNote(shape, (3, -1), "큰 뿌리");
        Program.Check(root.Contains("둘레가 열림") && AIBaseGuard.EdgeTileNote(shape, (0, 0), "큰 뿌리") == "" && AIBaseGuard.EdgeTileNote(shape, (6, 0), "큰 뿌리") == "", $"L7d. a root on an edge tile says cutting it opens the edge; inside / outside roots say nothing ('{root}')");
    }

    // 사실 8: the door (-10,-18) went in from outside at (-10,-18.86): in reach, but not from the inside stand (-10,-17).
    static void Fix8()
    {
        Program.Check(!AIBaseGuard.OnStandSide((-10, -18), (-10, -17), -10f, -18.86f), "L8a. (as measured) at (-10,-18.86) the AI is not on the inside stand's side: it walks to the stand first");
        Program.Check(AIBaseGuard.OnStandSide((-10, -18), (-10, -17), -10f, -17.1f) && !AIBaseGuard.OnStandSide((-10, -18), (-10, -17), -10f, -17.6f), "L8b. past the tile edge towards the stand it places; in the tile's own half not yet");
        Program.Check(AIBaseGuard.OnStandSide((-10, -18), (-10, -19), -10f, -18.86f), "L8c. an outside stand (decision A) is placed from outside");
    }

    // user decision A: the closing tile / the door from outside is an option too, with its fact
    static void DecisionA()
    {
        Program.Check(AIBaseGuard.OutsideCloseText((-4, -6)) == "바깥에서 닫음, 둘레 문 (-4,-6)로 드나듦", "LA1. with a door on the edge: '바깥에서 닫음, 둘레 문 (x,y)로 드나듦'");
        Program.Check(AIBaseGuard.OutsideCloseText(null) == "바깥에서 닫음, 둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함", "LA2. without a door: '…들어가려면 벽 하나를 부숴야 함'");
        // a one-tile gap (2,4) in a wall row y=4; inside below, outside above
        var g = new EdgeGrid { n = 7, loX = 0, loY = 0, pass = new bool[49], water = new bool[49], known = new bool[49], noPlace = new bool[49], wall = new bool[49] };
        for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++) { int i = x * 7 + y; g.known[i] = true; g.pass[i] = y != 4 || x == 2; g.wall[i] = !g.pass[i]; }
        var o = new EdgeOption(); foreach (var t in Rect(0, 0, 6, 3)) o.inside.Add(EdgeGrid.Key(t.x, t.y));
        var ins = AIBaseEdgeCore.InsideStand(g, o, (2, 4)); var outs = AIBaseEdgeCore.OutsideStand(g, o, (2, 4));
        Program.Check(ins == (2, 3) && outs == (2, 5), $"LA3. the gap tile has an inside stand and an outside stand: both are options ({ins} / {outs})");
    }

    // user decision B: a door the AI placed that stands on a gap tile of a new plan is the edge's door
    static void DecisionB()
    {
        var o = new EdgeOption();
        var gp = new EdgeGap(); gp.tiles.Add((-11, -18)); gp.tiles.Add((-10, -18)); gp.tiles.Add((-9, -18)); o.gaps.Add(gp);
        var gp2 = new EdgeGap(); gp2.tiles.Add((5, 5)); o.gaps.Add(gp2);
        var ad = AIBaseEdgeCore.AdoptDoors(o, t => t == (-10, -18));
        Program.Check(ad.Count == 1 && ad[0].gap == 0 && ad[0].tile == (-10, -18), "LB1. a remembered own door on a gap tile is taken as that gap's door");
        Program.Check(AIBaseEdgeCore.AdoptDoors(o, null).Count == 0 && AIBaseEdgeCore.AdoptDoors(o, t => t == (0, 0)).Count == 0, "LB2. no list, or a door off the gaps: nothing taken");
    }

    static void Small()
    {
        var r = new EnemyReach { kind = ReachKind.Door, steps = 3, door = (-4, -6) };
        string kill = AIBaseGuard.ReachText(r), walk = AIBaseGuard.ReachText(r, false);
        Program.Check(kill.Contains("나가서 침") && !walk.Contains("나가서 침") && walk.Contains("지나간 뒤 문은 닫음"), $"LS1. the retreat's way has no kill wording ('{walk}')");
        var st = new PickupState { phase = PickupPhase.Hit, lastHp = 1, hitSince = 0f };
        AIBaseGuard.PickupStep(st, new PickupObs { holdOk = true, objectThere = false, now = 1f });
        Program.Check(st.facts.Contains("체력 1→0") && !st.facts.Contains("力"), $"LS2. the pick-up facts say 체력 (no CJK 力) ('{st.facts.Trim()}')");
    }
}
