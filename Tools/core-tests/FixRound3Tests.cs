// G3f3 — 실측결함수정3 (21-실측결함수정3.md; the facts are 20-실측.md, raw data C:\Dev\_scratch\base-s3m). One group per
// new defect N1-N8; where the code allows, the logged situation is rebuilt and the old reading is shown next to the new.
using System;
using System.Collections.Generic;

public static class FixRound3Tests
{
    public static void Run()
    {
        N1(); N2(); N3(); N4(); N5(); N6(); N7(); N8();
    }

    // ------------------------------------------------------------------ N1: which kinds break walls (game data first)

    // run1 t=858-948: a Caveling broke a natural wall 1 s after it was spawned and a placed dirt wall 2.5 s after, then 14
    // edge places; its fact said "벽을 부수는 종류로 알려지지 않음" (it was not on the name list)
    static void N1()
    {
        var none = default(TileBreak);
        string cav = AIBaseGuard.BreakerNote("Caveling", none);
        Program.Check(AIBaseGuard.IsBreaker("Caveling") && cav.Contains("실측") && !cav.Contains("알려지지 않음"),
            $"N1a. Caveling (no data read): a wall breaker from the live run, said so ('{cav}')");
        Program.Check(!AIBaseGuard.IsBreaker("MushroomEnemy") && AIBaseGuard.BreakerNote("MushroomEnemy") == AIBaseGuard.KnownNoBreaker,
            "N1b. a kind with no data, no measurement and no report stays 'not known'");
        // the game data: DamageObjectState damage 30 hits dirt (135/1: 30-1 >= 27) and wood (150/1: 29 >= 30? no)
        var bump = new TileBreak { known = true, bump = 30 };
        string nb = AIBaseGuard.BreakerNote("SomeEnemy", bump);
        Program.Check(AIBaseGuard.IsBreaker("SomeEnemy", bump) && nb.Contains("게임 데이터") && nb.Contains("진흙 블록은 때림") && !nb.Contains("나무 벽") && !nb.Contains("문은 체력 2"),
            $"N1c. data: bump damage 30 meets dirt (hp/5 = 27 + 1) but not wood (30 + 1); the door is not said from the tile key (합치기 전 수정 C1: the object key): '{nb}'");
        var melee = new TileBreak { known = true, melee = 50 };
        Program.Check(AIBaseGuard.IsBreaker("X", melee) && AIBaseGuard.BreakerNote("X", melee).Contains("근접 공격이 칸에도 타일 피해 50"), "N1d. data: a melee tile damage makes a breaker");
        var zero = new TileBreak { known = true, chase = true, bump = 0, obj = 0, melee = 0, charge = 0 };   // 합치기 전 수정 C1: every key read
        string nz = AIBaseGuard.BreakerNote("MushroomEnemy", zero);
        Program.Check(!AIBaseGuard.IsBreaker("MushroomEnemy", zero) && nz.Contains("게임 데이터에 벽·문 피해 없음") && !nz.Contains("알려지지 않음"),
            $"N1e. data read, no tile damage: said as data, not 'not known' ('{nz}')");
        // the data comes first, the measurement stays with it
        string both = AIBaseGuard.BreakerNote("Caveling", new TileBreak { known = true, bump = 40 });
        Program.Check(both.IndexOf("게임 데이터") >= 0 && both.IndexOf("실측") > both.IndexOf("게임 데이터"), "N1f. Caveling with data: the data, then the measurement");
        Program.Check(AIBaseGuard.BumpHits(28, 135, 1) && !AIBaseGuard.BumpHits(27, 135, 1) && AIBaseGuard.BumpHits(115, 300, 55) && !AIBaseGuard.BumpHits(114, 300, 55),
            "N1g. the bump rule (damage − reduction ≥ hp/5) gives the table of 1-게임조사 2.3 (dirt 28, stone 115)");
        // the 1.5-tile rule across a shut door: a Caveling 1.4 tiles off past the door is a threat, a walker is not
        (int, int) door = (-4, -6);
        var g = GuardGrid.Make(-16, -18, 25, GuardCell.Closed);
        for (int x = -6; x <= -2; x++) for (int y = -5; y <= -1; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        for (int x = -12; x <= 4; x++) for (int y = -14; y <= -7; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        g.cell[g.Idx(door.Item1, door.Item2)] = GuardCell.OwnDoor;
        var m = AIBaseGuard.Reach(g, (-4, -5));
        var r = AIBaseGuard.ReachOf(m, (-4, -7));
        var walker = r; walker.breaker = AIBaseGuard.IsBreaker("MushroomEnemy", walker.tileBreak);
        var caveling = r; caveling.breaker = AIBaseGuard.IsBreaker("Caveling", caveling.tileBreak);
        Program.Check(!AIBaseGuard.IsThreat(walker, 1.4f, 8f) && AIBaseGuard.IsThreat(caveling, 1.4f, 8f) && AIBaseGuard.CloseNote(caveling, 1.4f).Contains("벽·문을 부수는 종류"),
            "N1h. past the shut door 1.4 tiles: a walker is no threat, a Caveling is (a breaker: hits across), with the fact why");
        string kf = AIBaseGuard.KillFacts(null, (-4, -7), caveling, "Caveling", 1.4f);
        Program.Check(kf.Contains("실측") && !kf.Contains("알려지지 않음"), $"N1i. its kill facts (no base) carry the breaker fact: '{kf}'");
    }

    // ------------------------------------------------------------------ N2: the running action's door

    // block test 1 #16: the attack's door step was passing the door (-4,-6) and a separate "close (-4,-6)" option was
    // offered and chosen; block test 2 #33: "keep away" chosen — both broke the attack off with the door open
    static void N2()
    {
        string note = AIDoorPass.LeftOpenNote(-4, -6);
        Program.Check(note.Contains("문 (-4,-6)") && note.Contains("열린 채 남음") && note.Length < 40, $"N2a. the short fact for the other options: '{note.Trim()}'");
        string cur = "attack:29297";
        Program.Check(!AIDoorPass.GetsLeftOpenNote("continue", cur, cur) && !AIDoorPass.GetsLeftOpenNote("attack", cur, cur) && !AIDoorPass.GetsLeftOpenNote("attacknow", "attacknow:29297", cur)
            && AIDoorPass.GetsLeftOpenNote("away", "away:near", cur) && AIDoorPass.GetsLeftOpenNote("retreat", "retreat:-4,-5", cur) && AIDoorPass.GetsLeftOpenNote("eat", "eat:FoodRation", cur)
            && AIDoorPass.GetsLeftOpenNote("wait", "wait", cur) && AIDoorPass.GetsLeftOpenNote("attack", "attack:11", cur),
            "N2b. it goes on every other option (keep away, retreat, eat, wait, another target); not on going on, nor on 'hit now' (that one says the door stays open)");
        // the "continue" option keeps the door's state (F7) — the only place the door step is said
        var ps = new DoorPassState { phase = DoorPhase.Pass };
        string cont = AIDoorPass.StepNote(ps, -4, -6, true, true);
        Program.Check(cont.Contains("지나가기 → 닫기") && cont.Contains("열린 채 남음") && cont.Contains("닫기를 먼저 마치고 침"), "N2c. the running action's own option carries the door step (open, through, close)");
    }

    // ------------------------------------------------------------------ N3: the door step leaves its trace

    static void N3()
    {
        // the inside start pressed on the door (-4,-6) at (-4.00,-5.65) — the clearing / mining starts of t=1250 / t=1781
        var s = new DoorPassState(); var b = new RemeasureTests.Body { x = -4f, y = -5.65f };
        bool open = false, pressDone = true; float t = 0f;
        for (int k = 0; k < 900 && s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed; k++, t += RemeasureTests.Body.Dt)
        {
            var o = AIDoorPass.Step(s, DoorKind.Manual, -4f, -6f, 0f, -1f, b.x, b.y, open, pressDone, t);
            if (!pressDone) pressDone = true;
            if (o.press) { pressDone = false; if (RemeasureTests.Taken(b.x, b.y, o.fx, o.fy, -4f, -6f)) open = !open; }
            b.Step(o.mx, o.my, -4f, -6f, false, !open);
        }
        Program.Check(s.phase == DoorPhase.Done && s.trace.Contains("열기") && s.trace.Contains("press 1 @") && s.trace.Contains("지나가기") && s.trace.Contains("닫기"),
            $"N3a. a door step keeps its trace (phases, presses with the distance): '{s.trace}'");
        var f = new DoorPassState { phase = DoorPhase.Open }; float tf = 0f;
        for (int k = 0; k < 400 && f.phase == DoorPhase.Open; k++, tf += RemeasureTests.Body.Dt) AIDoorPass.Step(f, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.62f, 0f, false, true, tf);
        var waiting = AIDoorPass.Step(f, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.62f, 0f, false, true, tf);
        Program.Check(f.phase == DoorPhase.Open && f.trace.Contains("press 2") && !f.trace.EndsWith("failed") && waiting.observation.Contains("문이 열리지 않음"), $"N3b. an unanswered step keeps its press trace and reports '{waiting.observation}'");
    }

    // ------------------------------------------------------------------ N4: a door seen gone, out of view

    // 결정 B base: the door (-4,-6) hung in the one-tile gap; the AI dug its side wall out from outside (the door fell,
    // t=1997.3), then went 15 tiles away: "(−5,−6) side wall gone (memory)" stayed, "(−4,−6) the door is gone" was dropped
    // (the door tile is a gap tile, and an unseen gap tile was skipped as "not built yet")
    static void N4()
    {
        var ins = new List<(int x, int y)>(); for (int x = 0; x <= 4; x++) for (int y = 0; y <= 4; y++) ins.Add((x, y));
        var gap = (-1, 2);
        var shape = AIBaseGuard.Shape(ins, new List<(int x, int y)> { gap });
        var doors = new List<(int x, int y)> { gap };
        var seen = new EdgeLastSeen();
        EdgeNow Now(bool inView, (int x, int y) t) => t == gap ? (inView ? EdgeNow.Open : EdgeNow.Unseen) : EdgeNow.Closed;
        var inV = AIBaseGuard.Breaches(shape, new List<(int, int)>(), doors, t => Now(true, t), seen);
        var outV = AIBaseGuard.Breaches(shape, new List<(int, int)>(), doors, t => Now(false, t), seen);
        var gone = outV.list.Find(x => x.kind == BreachKind.DoorGone);
        Program.Check(inV.list.Exists(x => x.kind == BreachKind.DoorGone && !x.remembered) && gone != null && gone.remembered && gone.Text.Contains("달았던 문이 없어짐") && gone.Text.Contains("기억"),
            $"N4a. the door seen gone stays 'the door is gone (memory)' out of view: '{(gone != null ? gone.Text : "(dropped)")}'");
        // a gap tile never built (no door, no wall) is still open by plan, not a breach, out of view as in view
        var notBuilt = AIBaseGuard.Breaches(shape, new List<(int, int)>(), new List<(int, int)>(), t => Now(false, t), seen);
        Program.Check(notBuilt.list.Count == 0, "N4b. a gap tile with nothing built stays 'not built' (no repair), even seen open");
        // the reach map out of view: the door seen gone is open (an enemy outside comes); seen hanging, it is the door
        GuardGrid G()
        {
            var g = GuardGrid.Make(-8, -8, 21, GuardCell.Open); g.view = new bool[g.n * g.n];
            for (int i = 0; i < g.cell.Length; i++) { int x = g.loX + i / g.n, y = g.loY + i % g.n; if (shape.IsEdge(x, y)) g.cell[i] = GuardCell.Closed; g.view[i] = x >= 0 && x <= 4 && y >= 0 && y <= 4; }
            g.cell[g.Idx(gap.Item1, gap.Item2)] = GuardCell.OwnDoor;   // the game side: an own door out of view is the remembered door
            return g;
        }
        var memGone = new BaseMemory(shape, null, doors, seen);
        var rGone = AIBaseGuard.ReachOf(AIBaseGuard.Reach(G(), (2, 2), memGone), (-4, 2));
        var seenHang = new EdgeLastSeen(); seenHang.Note(3, 3, false);
        var rHang = AIBaseGuard.ReachOf(AIBaseGuard.Reach(G(), (2, 2), new BaseMemory(shape, null, doors, seenHang)), (-4, 2));
        Program.Check(rGone.comes && !rHang.comes, $"N4c. out of view: the door seen gone lets the enemy outside come ({rGone.kind}); a door seen hanging does not ({rHang.kind})");
    }

    // ------------------------------------------------------------------ N5: the close right after a swing

    // t=1563.7: the attack ended next to its open door; the tail pressed at 0.70 at 1564.1 and 1565.1, "2번 눌러도 문이
    // 닫히지 않음(열린 채)" at 1566.1; the next tail closed it with one press at 1567.6 (the measurement's "closed 51 s
    // later"). The game takes no interact press in Release (the state after a melee swing): the body now holds a press
    // until the state takes it, and the door is watched WaitOpen from when it went out (not from when it was asked).
    static void N5()
    {
        var s = new DoorPassState { phase = DoorPhase.Close };
        bool open = true, pending = false; int asked = 0; var due = new List<float>();
        for (int k = 0; k < 300 && s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed; k++)
        {
            float t = k / 30f;
            if (pending && t >= 1.5f) { pending = false; due.Add(t + 0.1f); }   // Release until 1.5 s: the press goes out then
            for (int i = due.Count - 1; i >= 0; i--) if (t >= due[i]) { open = !open; due.RemoveAt(i); }
            var o = AIDoorPass.Step(s, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.63f, 0f, open, !pending, t);
            if (o.press) { pending = true; asked++; }
        }
        Program.Check(s.phase == DoorPhase.Done && !open && asked == 1, $"N5a. a press held 1.4 s (Release) then sent: the door gets its second after sending; one press, closed ({s.facts}{s.fail}, presses {asked})");
    }

    // ------------------------------------------------------------------ N6: the stand search from the spawn pad

    // run2 t=226.6: a repair of (-5,-6) chosen at the core (0,-0.14) failed in 0.0 s "설 칸이 없음"; the same tile got a
    // stand from (-3,0) at t=267.5. The spawn pad (3x3 round the core spawn) reads as no walk for the game's path search;
    // the stand search started on it and reached nothing.
    static void N6()
    {
        PlaceGridInput G(bool padMarked)
        {
            int n = 21; var g = new PlaceGridInput { n = n, loX = -10, loY = -10, pass = new bool[n * n], meX = 0, meY = 0, meFx = 0f, meFy = -0.14f };
            if (padMarked) g.noStand = new bool[n * n];
            g.noPlace = new bool[n * n];   // as the game side: the pad is never a spot (AICraft.Grid)
            for (int x = -10; x <= 10; x++) for (int y = -10; y <= 10; y++)
            {
                int i = (x + 10) * n + (y + 10);
                bool pad = AIPlaceGrid.SpawnPad(x, y);
                g.pass[i] = !pad && !(x == -5 && y == -6);   // (-5,-6) the tile to place (open, not a way here)
                if (padMarked) g.noStand[i] = pad;
                g.noPlace[i] = pad;
            }
            return g;
        }
        bool oldFound = AIPlaceGrid.StandFor(G(false), -5, -6, 1, 1, null, null, out _, out _);
        bool found = AIPlaceGrid.StandFor(G(true), -5, -6, 1, 1, null, null, out int sx, out int sy);
        Program.Check(!oldFound && found && Math.Abs(sx + 5) + Math.Abs(sy + 6) == 1 && !AIPlaceGrid.SpawnPad(sx, sy),
            $"N6a. from the pad: the old search (pad = no walk) found no stand (the 0.0 s failure); the pad walked off, a side stand ({sx},{sy})");
        var opts = AIPlaceGrid.All(G(true), out _, out _);
        Program.Check(opts.Count > 0 && !opts.Exists(o => AIPlaceGrid.SpawnPad(o.standX, o.standY) || AIPlaceGrid.SpawnPad(o.ax, o.ay)), $"N6b. the place options from the pad: {opts.Count}, none on the pad");
    }

    // ------------------------------------------------------------------ N7: where the press is taken

    static void N7()
    {
        // run1 t=992 / t=1033: the door (-4,-6) pressed from (-4.00,-4.87), 3 presses each, not taken — and taken from there
        // at t=1058. 합치기 전 수정 1: the reach is 1.2 (inside the game's aim box), the press made standing still
        // (AIPlaceGrid.PressStep; MergeFixTests M1); 1.22 (22-짧은확인 t=616.6) is out
        Program.Check(!AIPlaceGrid.PressFrom(-4, -6, 1, 1, -4.00f, -4.78f) && AIPlaceGrid.PressFrom(-4, -6, 1, 1, -4.00f, -4.87f) && AIPlaceGrid.PressFrom(-4, -6, 1, 1, -4.00f, -5.0f),
            "N7a. the door (-4,-6): no press from (-4.00,-4.78) (1.22 off), yes from (-4.00,-4.87) (1.13, taken at t=1058), the stand centre (-4,-5) and nearer");
        // run2 t=267.5 / 335.9: the wall (-5,-6) pressed from the diagonal stand (-4,-5) at (-4.00,-5.17) / (-4.00,-5.31)
        Program.Check(!AIPlaceGrid.PressFrom(-5, -6, 1, 1, -4.00f, -5.17f) && !AIPlaceGrid.PressFrom(-5, -6, 1, 1, -4.00f, -5.31f) && AIPlaceGrid.PressFrom(-5, -6, 1, 1, -4.00f, -6.01f),
            "N7b. the wall (-5,-6): no press from the diagonal places (-4.00,-5.17), (-4.00,-5.31); from the side (-4.00,-6.01) yes");
        Program.Check(!AIPlaceGrid.PressFrom(-5, -6, 1, 1, -5.0f, -6.2f) && AIPlaceGrid.PressFrom(0, 0, 2, 2, 2.0f, 1.0f) && !AIPlaceGrid.PressFrom(0, 0, 2, 2, 2.2f, 2.2f),
            "N7c. not on the footprint; a 2x2 from its side yes, from its corner no");
        // the stand pick: the run2 tiles — (-5,-6) to place, (-4,-6) the door spot (door gone, in view: ground), (-4,-5)
        // inside the diagonal, (-5,-7) / (-4,-7) outside; walls elsewhere round (-5,-6)
        PlaceGridInput G(bool doorRemembered)
        {
            int n = 11; var g = new PlaceGridInput { n = n, loX = -9, loY = -10, pass = new bool[n * n], door = new bool[n * n], meX = -4, meY = -3, meFx = -4f, meFy = -3f };
            foreach (var t in new[] { (-4, -1), (-4, -2), (-4, -3), (-4, -4), (-4, -5), (-3, -5), (-4, -6), (-4, -7), (-5, -7), (-3, -7) }) g.pass[(t.Item1 + 9) * n + (t.Item2 + 10)] = true;
            if (doorRemembered) { int i = (-4 + 9) * n + (-6 + 10); g.door[i] = true; g.pass[i] = false; }
            return g;
        }
        bool ok = AIPlaceGrid.StandFor(G(false), -5, -6, 1, 1, null, null, out int sx, out int sy);
        Program.Check(ok && sx == -4 && sy == -6, $"N7d. the door seen gone (ground): the side stand (-4,-6) next to the tile, not the diagonal (-4,-5): ({sx},{sy})");
        bool ok2 = AIPlaceGrid.StandFor(G(true), -5, -6, 1, 1, null, null, out int sx2, out int sy2);
        Program.Check(ok2 && sx2 == -5 && sy2 == -7, $"N7e. read as a door (the old memory reading): through the door to the outside side stand (-5,-7), never the diagonal: ({sx2},{sy2})");
    }

    // ------------------------------------------------------------------ N8: health falling with no enemy in view

    // run2 t=146-154: CavelingShaman fight, "enemy out of range" at 152.7, health 15 -> 4 -> 0 while retreating, the down
    // record "보이는 적 없음, 원인 모름"
    static void N8()
    {
        string why = AIOutcomes.HurtWithoutEnemy(new List<string> { "Poisoned(값 5)" }, new List<string> { "9201 1.2칸" });
        string c1 = AIOutcomes.DownCause("", 99, why, true);
        Program.Check(c1.Contains("보이는 적 없음") && c1.Contains("Poisoned") && c1.Contains("9201"), $"N8a. the down record names what was found when the health fell: '{c1}'");
        string c2 = AIOutcomes.DownCause("", 99, "", true);
        Program.Check(c2.Contains("원인 모름") && c2.Contains("지속 피해 상태도 가까운 투사체도 없었음"), $"N8b. nothing found: said so ('{c2}')");
        Program.Check(AIOutcomes.DownCause("CavelingShaman 3.0 hp200", 99, why, true).StartsWith("적 근처") && AIOutcomes.DownCause("", 0, "", false).StartsWith("배고픔 0")
            && AIOutcomes.HurtWithoutEnemy(null, new List<string>()) == "", "N8c. an enemy seen comes first; hunger 0 next; no finding is empty");
    }
}
