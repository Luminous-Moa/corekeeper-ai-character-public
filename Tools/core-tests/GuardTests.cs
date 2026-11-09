// G3 — 코어-안전지대 단계 3: keeping the base (Core/AIBaseGuard.cs): kill facts and the way to each enemy, enemies that
// cannot be reached and the self-defence / combat re-decision, broken parts of the remembered edge, repair batches, the
// pick-up order, the retreat point inside the base, review G6 (base options are no goal progress).
using System;
using System.Collections.Generic;

public static class GuardTests
{
    // '#' closed, '.' open, 'D' own door, '?' unknown, '~' fence/water (low), letters a-z (not D) = open with a marker
    static GuardGrid Grid(string[] rows, out Dictionary<char, (int x, int y)> marks)
    {
        int h = rows.Length, w = 0; foreach (var r in rows) w = Math.Max(w, r.Length);
        int n = Math.Max(h, w);
        var g = GuardGrid.Make(0, 0, n, GuardCell.Closed);
        marks = new Dictionary<char, (int, int)>();
        for (int r = 0; r < h; r++)
            for (int x = 0; x < rows[r].Length; x++)
            {
                int y = h - 1 - r; char c = rows[r][x];
                GuardCell v = c == '#' ? GuardCell.Closed : c == 'D' ? GuardCell.OwnDoor : c == '?' ? GuardCell.Unknown : c == '~' ? GuardCell.Low : GuardCell.Open;
                g.cell[g.Idx(x, y)] = v;
                if (c != '#' && c != 'D' && c != '?' && c != '~' && c != '.') marks[c] = (x, y);
            }
        return g;
    }

    static List<(int x, int y)> Area(int x1, int y1, int x2, int y2, Func<(int x, int y), bool> keep = null)
    {
        var l = new List<(int, int)>();
        for (int x = x1; x <= x2; x++) for (int y = y1; y <= y2; y++) if (keep == null || keep((x, y))) l.Add((x, y));
        return l;
    }

    public static void Run()
    {
        // K. the base room (left) with an own door D to a room east (enemy b); a sealed pocket south-east (enemy c);
        //    enemy a inside the base
        string[] rowsK =
        {
            "#############",
            "#.....#.....#",
            "#..A..D..b..#",
            "#.....#.....#",
            "#..a..#######",
            "#######..c..#",
            "#######.....#",
            "#############",
        };
        var gK = Grid(rowsK, out var mk);
        var mapK = AIBaseGuard.Reach(gK, mk['A']);
        var ra = AIBaseGuard.ReachOf(mapK, mk['a']);
        var rb = AIBaseGuard.ReachOf(mapK, mk['b']);
        var rc = AIBaseGuard.ReachOf(mapK, mk['c']);
        Program.Check(ra.kind == ReachKind.Open && ra.steps == 2 && ra.comes, $"K1. an enemy in the same room: open way 2 tiles, it can come ({ra.kind} {ra.steps} {ra.comes})");
        Program.Check(rb.kind == ReachKind.Door && rb.door == (6, 5) && rb.steps == 6 && !rb.comes, $"K2. an enemy past the own door: through door (6,5), 6 tiles, it cannot come (doors stay shut for enemies) ({rb.kind} {rb.door} {rb.steps} {rb.comes})");
        Program.Check(rc.kind == ReachKind.None && !rc.comes, $"K3. an enemy in a sealed pocket: cannot reach now, it cannot come ({rc.kind})");
        // the base: the left room is the inside, the door tile its gap
        var inside = Area(1, 3, 5, 6);
        var shape = AIBaseGuard.Shape(inside, new List<(int, int)> { (6, 5) });
        string fa = AIBaseGuard.KillFacts(shape, mk['a'], ra, "SlimeBlob");
        string fb = AIBaseGuard.KillFacts(shape, mk['b'], rb, "MushroomBrute");
        string fc = AIBaseGuard.KillFacts(shape, mk['c'], rc, "MushroomEnemy");
        Program.Check(fa.Contains("기지 안쪽, 둘레에서 1칸") && fa.Contains("열린 길 2칸"), $"K4. inside enemy facts: side, distance from the edge, the way ({fa})");
        Program.Check(fb.Contains("기지 바깥, 둘레에서 3칸") && fb.Contains("문 (6,5)을 지나 6칸") && fb.Contains("벽을 부수는 종류: 돌진") && fb.Contains("걸어올 길은 지금 없음"), $"K5. outside enemy past the door: 'through the door N tiles', a wall-breaking kind (brute), cannot come ({fb})");
        Program.Check(fc.Contains("벽 너머라 지금 닿을 수 없음") && fc.Contains("알려지지 않음"), $"K6. sealed enemy: 'behind a wall, cannot reach now', not a known breaker ({fc})");
        Program.Check(AIBaseGuard.KillFacts(null, mk['a'], ra, "SlimeBlob").StartsWith("닿는 길"), "K7. no base: no side facts, the way and the kind stay");

        // U. unseen tiles: the way through them may exist; a sealed room next to unknown tiles is not "cannot reach"
        string[] rowsU =
        {
            "#########",
            "#.A.?.u.#",
            "#########",
        };
        var gU = Grid(rowsU, out var mu);
        var ru = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gU, mu['A']), mu['u']);
        Program.Check(ru.kind == ReachKind.Unseen && ru.comes, $"U1. only through an unseen tile: 'unseen way', it may come ({ru.kind} {ru.comes})");
        // the window's border is one place outside: two openings to the border connect
        string[] rowsB =
        {
            "#.#####.#",
            "#.#####.#",
            "#.#####.#",
            "#.#####.#",
            "#.#####.#",
            "#.#####.#",
            "#.#####.#",
            "#A#####u#",
            "#########",
        };
        var gB = Grid(rowsB, out var mb);
        var rbb = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gB, mb['A']), mb['u']);
        Program.Check(rbb.kind == ReachKind.Unseen && rbb.comes, $"U2. both open to the window's edge: may connect outside the window ({rbb.kind})");

        // F. a fence between: walkers stop, a flier comes
        string[] rowsF = { "#######", "#.A~.f#", "#######" };
        var gF = Grid(rowsF, out var mf);
        var mapF = AIBaseGuard.Reach(gF, mf['A']);
        var rfw = AIBaseGuard.ReachOf(mapF, mf['f']);
        var rff = AIBaseGuard.ReachOf(mapF, mf['f'], true);
        Program.Check(rfw.kind == ReachKind.None && !rfw.comes && rff.comes, $"F1. fence: the AI cannot walk to it; a walker cannot come, a flier can ({rfw.kind} {rfw.comes} {rff.comes})");
        Program.Check(AIBaseGuard.IsFlier("CaveBat") && !AIBaseGuard.IsFlier("SlimeBlob"), "F2. bat names count as fliers (estimate)");

        // T. threats: unreachable enemies do not start or keep self-defence nor the combat re-decision (단계 1 M4c: an
        //    enemy behind a fence for 100 s made 287 calls)
        const float range = 8f;
        var only = new List<(EnemyReach r, float dist)> { (rb, 6f), (rc, 5f) };
        Program.Check(!AIBaseGuard.CombatNow(only, range), "T1. enemies behind a closed door / walls only: no fight state (no 'ask again at once')");
        Program.Check(AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (rc, 5f), (ra, 4f) }, range), "T2. one enemy that can come: fight state");
        // 재실측 N-g: the HitReach rule needs nothing shut between (rc is behind walls); with nothing between it is a threat
        var rcTouch = rc; rcTouch.touch = true;
        Program.Check(!AIBaseGuard.IsThreat(rc, 1.2f, range) && AIBaseGuard.IsThreat(rcTouch, 1.2f, range), "T3. so close it can hit now: a threat when nothing is shut between (재실측 N-g: behind walls it is none)");
        Program.Check(!AIBaseGuard.IsThreat(ra, 9f, range), "T4. beyond the range: not a threat");
        // 100 s at 0.5 s checks with an unreachable enemy at 5 tiles: self-defence never starts, no re-decision ticks
        bool on = false; float last = -1e9f; int starts = 0, runningTicks = 0, combatTicks = 0;
        for (float t = 0f; t <= 100f; t += 0.5f)
        {
            bool threat = AIBaseGuard.CombatNow(only, range);
            var ev = AIBaseGuard.DefenceStep(ref on, ref last, threat, false, t);
            if (ev == AIBaseGuard.DefenceEvent.Started) starts++;
            if (ev == AIBaseGuard.DefenceEvent.Running) runningTicks++;
            if (threat) combatTicks++;
        }
        Program.Check(starts == 0 && runningTicks == 0 && combatTicks == 0, $"T5. 100 s with only an unreachable enemy: no self-defence episode, no combat ticks (starts {starts}, running {runningTicks}, combat {combatTicks})");
        // reachable for 2 s, then the door shuts it out: the episode ends 3 s after the last threat
        on = false; last = -1e9f; float ended = -1f;
        for (float t = 0f; t <= 20f; t += 0.5f)
        {
            bool threat = t < 2f;
            var ev = AIBaseGuard.DefenceStep(ref on, ref last, threat, false, t);
            if (ev == AIBaseGuard.DefenceEvent.Ended) { ended = t; break; }
        }
        Program.Check(Math.Abs(ended - 4.5f) < 0.01f, $"T6. the enemy is shut out at 2 s: self-defence ends 3 s after the last threat (at {ended:F1} s)");
        on = false; last = -1e9f;
        var hurtEv = AIBaseGuard.DefenceStep(ref on, ref last, false, true, 0f);
        Program.Check(hurtEv == AIBaseGuard.DefenceEvent.Started, "T7. damage still starts self-defence (something hits from where the AI cannot see a way)");

        // R. broken parts of the remembered edge. Base: a 7x3 room (x 1..7, y 1..3); the plan's gap: tiles (8,1)-(8,3) on
        //    the east, (8,2) the door spot. Placed: walls (8,1),(8,3); the door (8,2). Now: (8,1) open again (placed wall
        //    gone), the door gone, the north natural wall (1..7,4) open on 1..6 except (7,4), (0,2) not seen.
        var insideR = Area(1, 1, 7, 3);
        var gapR = new List<(int, int)> { (8, 1), (8, 2), (8, 3) };
        var shapeR = AIBaseGuard.Shape(insideR, gapR);
        var ownWalls = new List<(int, int)> { (8, 1), (8, 3) };
        var ownDoors = new List<(int, int)> { (8, 2) };
        var openNow = new HashSet<(int, int)> { (8, 1), (8, 2), (1, 4), (2, 4), (3, 4), (4, 4), (5, 4), (6, 4) };
        EdgeNow Now((int x, int y) t) => t == (0, 2) ? EdgeNow.Unseen : openNow.Contains(t) ? EdgeNow.Open : EdgeNow.Closed;
        var scan = AIBaseGuard.Breaches(shapeR, ownWalls, ownDoors, Now);
        Program.Check(scan.list.Exists(b => b.tile == (8, 1) && b.kind == BreachKind.WallGone), "R1. a placed wall gone is found (memory against the tile now)");
        Program.Check(scan.list.Exists(b => b.tile == (8, 2) && b.kind == BreachKind.DoorGone), "R2. a placed door gone is found");
        Program.Check(scan.list.FindAll(b => b.kind == BreachKind.NaturalGone).Count == 6 && !scan.list.Exists(b => b.tile == (7, 4)), "R3. natural edge tiles opened are found (6), a closed one is not");
        Program.Check(scan.unseen == 1 && !scan.list.Exists(b => b.tile == (0, 2)), "R4. an edge tile not in view is counted as unchecked, not as broken");
        Program.Check(scan.edgeTiles == shapeR.edge.Count && scan.ownWalls == 2 && scan.doors == 1, $"R5. edge counts: {scan.edgeTiles} tiles, own walls {scan.ownWalls}, doors {scan.doors}");
        // a planned gap tile never built is filling (단계 2), not repair
        var scan2 = AIBaseGuard.Breaches(shapeR, new List<(int, int)>(), new List<(int, int)>(), t => t == (8, 3) ? EdgeNow.Open : EdgeNow.Closed);
        Program.Check(scan2.list.Count == 0 && scan2.notBuilt == 1, "R6. an unbuilt planned gap tile is not a breach (the fill options cover it)");
        // repair batches: the north opening (6 tiles) and the east wall are two openings; the door is not in a batch
        (int, int)? StandR((int x, int y) t) { foreach (var d in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) }) { var u = (t.x + d.Item1, t.y + d.Item2); if (shapeR.Inside(u.Item1, u.Item2)) return u; } return null; }
        var rbs = AIBaseGuard.RepairBatches(scan, StandR);
        Program.Check(rbs.Count == 2 && rbs.TrueForAll(b => b.repair && b.closesGap) && !rbs.Exists(b => b.tiles.Contains((8, 2))), $"R7. repair batches: one per opening, each closes it, the gone door is not a wall batch ({rbs.Count})");
        Program.Check(rbs.TrueForAll(b => b.stands.TrueForAll(s => shapeR.Inside(s.x, s.y))), "R8. every repair tile is placed from an inside stand");
        // a long breach (9 tiles): batches of at most 7, only the last closes
        var shapeL = AIBaseGuard.Shape(Area(0, 0, 10, 2), null);
        var scanL = AIBaseGuard.Breaches(shapeL, new List<(int, int)>(), new List<(int, int)>(), t => t.y == 3 && t.x >= 1 && t.x <= 9 ? EdgeNow.Open : EdgeNow.Closed);
        var rbl = AIBaseGuard.RepairBatches(scanL, t => (t.x, t.y - 1));
        Program.Check(rbl.Count == 2 && rbl[0].tiles.Count == 7 && rbl[1].tiles.Count == 2 && !rbl[0].closesGap && rbl[1].closesGap && rbl[0].group.Count == 9, $"R9. a 9-tile breach: 7 + 2, the last batch closes, both know the whole opening ({string.Join("/", rbl.ConvertAll(b => b.tiles.Count + (b.closesGap ? "c" : "")))})");
        string mf2 = AIBaseGuard.MaterialFacts(3, "WallDirtBlock 3", scan, 0, 0);
        Program.Check(mf2.Contains("가진 벽 블록 3개") && mf2.Contains("놓은 벽 2칸") && mf2.Contains("부서진 곳 7칸") && mf2.Contains("없어진 문 1개") && mf2.Contains("wall-no-drop"), $"R10. repair material facts: blocks held against the edge, broken tiles, doors ({mf2})");

        // P. pick-up order: a melee weapon is refused; with a pick: approach, hit 2->1->0, the bag gains it, place again
        var ps = new PickupState { replace = true };
        var o0 = AIBaseGuard.PickupStep(ps, new PickupObs { holdOk = true, heldMelee = true, objectThere = true, objHp = 2, dist = 3f, bagCount = 0, now = 0f });
        Program.Check(ps.phase == PickupPhase.Failed && ps.fail.Contains("근접 무기"), "P1. only a melee weapon to hold: fails (melee weapons do not hit placed objects)");
        ps = new PickupState { replace = true };
        var seq = new List<string>();
        var obs = new PickupObs { holdOk = true, heldMelee = false, objectThere = true, objHp = 2, dist = 3f, bagCount = 0, now = 0f };
        void St(PickupObs ob) { var r = AIBaseGuard.PickupStep(ps, ob); seq.Add($"{ps.phase}{(r.walk ? ":walk" : "")}{(r.swing ? ":swing" : "")}{(r.place ? ":place" : "")}"); }
        St(obs);                                                   // approach
        obs.dist = 1.2f; obs.now = 1f; St(obs);                    // hit
        obs.objHp = 1; obs.now = 1.5f; St(obs);                    // hp 2 -> 1
        obs.objectThere = false; obs.now = 2f; St(obs);            // gone, wait for the bag
        obs.bagCount = 1; obs.now = 2.4f; St(obs);                 // in the bag -> place
        Program.Check(string.Join(" ", seq) == "Approach:walk Hit:swing Hit:swing Collect Place:place" && ps.facts.Contains("체력 2→1") && ps.facts.Contains("가방 +1"), $"P2. pick-up order: approach, swing, health drops, gone, bag +1, place again ({string.Join(" ", seq)}; {ps.facts})");
        var pn = new PickupState();
        AIBaseGuard.PickupStep(pn, new PickupObs { holdOk = true, objectThere = true, objHp = 2, dist = 1f, now = 0f });
        var unchangedHit = AIBaseGuard.PickupStep(pn, new PickupObs { holdOk = true, objectThere = true, objHp = 2, dist = 1f, now = 5.5f });
        Program.Check(pn.phase == PickupPhase.Hit && unchangedHit.swing && unchangedHit.observation.Contains("체력이 줄지 않음"), "P3. 5 s without a health change: report the fact and keep the chosen swing");
        var pd = new PickupState();
        AIBaseGuard.PickupStep(pd, new PickupObs { holdOk = true, objectThere = false, now = 0f, bagCount = 2 });
        var waitingBag = AIBaseGuard.PickupStep(pd, new PickupObs { holdOk = true, objectThere = false, now = 3.5f, bagCount = 2 });
        Program.Check(pd.phase == PickupPhase.Collect && waitingBag.observation.Contains("가방에 들어오지 않음"), "P4. delayed collection remains pending and reports the fact");
        AIBaseGuard.PickupStep(pd, new PickupObs { holdOk = true, objectThere = false, now = 4f, bagCount = 3 });
        Program.Check(pd.phase == PickupPhase.Done, "P4b. delayed inventory receipt completes the same pickup");
        var pk = new PickupState();
        AIBaseGuard.PickupStep(pk, new PickupObs { holdOk = true, objectThere = false, now = 0f, bagCount = 0 });
        AIBaseGuard.PickupStep(pk, new PickupObs { holdOk = true, objectThere = false, now = 0.5f, bagCount = 1 });
        Program.Check(pk.phase == PickupPhase.Done, "P5. pick-up only (no new tile): done once in the bag");

        // Q. back into the base: the spawn pad is never the point; outside, the way goes through the own door
        string[] rowsQ =
        {
            "###########",
            "#.......#..",
            "#.......D.A",
            "#.......#..",
            "###########",
        };
        var gQ = Grid(rowsQ, out var mq);
        var mapQ = AIBaseGuard.Reach(gQ, mq['A']);
        var shapeQ = AIBaseGuard.Shape(Area(1, 1, 7, 3), new List<(int, int)> { (8, 2) });
        (int x, int y) seedQ = (4, 2);
        bool Pad((int x, int y) t) => Math.Abs(t.x - seedQ.x) <= 1 && Math.Abs(t.y - seedQ.y) <= 1;
        var tq = AIBaseGuard.InsideTarget(shapeQ, t => !Pad(t) && gQ.At(t.x, t.y) == GuardCell.Open, seedQ, mapQ, false, out var howQ);
        Program.Check(tq == (7, 2) && howQ.kind == ReachKind.Door && howQ.door == (8, 2) && howQ.steps == 3, $"Q1. outside: the nearest inside tile through the own door ({tq} {howQ.kind} {howQ.door} {howQ.steps})");
        var tqi = AIBaseGuard.InsideTarget(shapeQ, t => !Pad(t) && gQ.At(t.x, t.y) == GuardCell.Open, seedQ, mapQ, true, out _);
        Program.Check(tqi.HasValue && !Pad(tqi.Value) && shapeQ.Inside(tqi.Value.x, tqi.Value.y) && Math.Max(Math.Abs(tqi.Value.x - 4), Math.Abs(tqi.Value.y - 2)) == 2, $"Q2. inside: a walkable inside tile next to the pad, not the pad ({tqi})");
        // the old point (0,-1) of the core is a pad tile: never chosen
        var shapeC = AIBaseGuard.Shape(Area(-4, -4, 4, 4), null);
        var tc = AIBaseGuard.InsideTarget(shapeC, t => !(Math.Abs(t.x) <= 1 && Math.Abs(t.y) <= 1), (0, 0), null, true, out _);
        Program.Check(tc.HasValue && tc.Value != (0, -1) && Math.Max(Math.Abs(tc.Value.x), Math.Abs(tc.Value.y)) == 2, $"Q3. core base: the point is off the spawn pad ({tc})");

        // G6. base options are never goal progress; in a close fight with a gather goal they are not the "continue the
        //     goal" option and they do not hide the goal stall (no progress candidate in the set)
        Program.Check(Array.TrueForAll(AIBaseGuard.Kinds, k => !AIBaseGuard.CountsAsGoalProgress(k)), "G6a. every base option kind counts as no goal progress");
        int id = 0;
        AsmCand C(string kind, string key) => new AsmCand { id = ++id, kind = kind, key = key, desc = kind, progress = AIBaseGuard.CountsAsGoalProgress(kind) };
        var a = new AsmInput
        {
            goal = GoalKind.Gather, closeFight = true, curKey = "",
            lCont = new List<AsmCand>(), lFollow = new List<AsmCand>(), lEnemy = new List<AsmCand> { C("attack", "attack:7") },
            lCombat = new List<AsmCand> { C("retreat", "retreat:7,2") },
            lCraft = new List<AsmCand> { C("edgerepair", "edgerepair:1,4"), C("edgemat", "edgemat:9,9"), C("edgepickup", "edgepickup:8,2"), C("edgefill", "edgefill:0:8,1") },
            lLife = new List<AsmCand>(), lMine = new List<AsmCand>(), lDig = new List<AsmCand>(), lRemember = new List<AsmCand>(), lItem = new List<AsmCand>(),
            lExplore = new List<AsmCand>(), lDigExplore = new List<AsmCand>(), lOther = new List<AsmCand>(), foodPicks = new HashSet<AsmCand>(),
            enemy0 = "SlimeBlob", enemy0Dist = "3.0", near6 = 1,
        };
        var sel = AICandidateAssembler.Select(a, c => true, c => true, out string setLog, out bool closeSet, out _);
        Program.Check(closeSet && sel.Exists(c => c.desc.Contains("목표 계속하기")), $"G6b. close fight: supported base options remain selectable in close combat ({setLog})");
        Program.Check(!sel.Exists(c => c.progress), "G6c. no progress candidate in the set: the gather goal's stall check still sees 'nothing moves the goal'");
        var a2 = new AsmInput
        {
            goal = GoalKind.Gather, closeFight = false, curKey = "",
            lCont = new List<AsmCand>(), lFollow = new List<AsmCand>(), lEnemy = new List<AsmCand> { C("attack", "attack:8") }, lCombat = new List<AsmCand>(),
            lCraft = new List<AsmCand> { C("edgerepair", "edgerepair:2,4"), C("edgemat", "edgemat:9,8") },
            lLife = new List<AsmCand>(), lMine = new List<AsmCand>(), lDig = new List<AsmCand>(), lRemember = new List<AsmCand>(), lItem = new List<AsmCand>(),
            lExplore = new List<AsmCand>(), lDigExplore = new List<AsmCand>(), lOther = new List<AsmCand>(), foodPicks = new HashSet<AsmCand>(),
        };
        var sel2 = AICandidateAssembler.Select(a2, c => true, c => true, out _, out _, out _);
        Program.Check(sel2.Exists(c => c.kind == "edgerepair") && !sel2.Exists(c => c.progress), "G6d. calm: the base options are offered and none of them is progress");

        Review3(rb, scan, rbs);
        GuardLiveTests.Run();   // 15-실측결함수정.md: the stage 3 live-run fixes
    }

    // an EdgeGrid with a wall layer: '#' wall tile, '.' floor, 'g' floor (a gap tile), 'o' an object (not walkable, not
    // a wall), '?' unseen
    static EdgeGrid EGrid(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length, n = Math.Max(h, w);
        var g = new EdgeGrid { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], known = new bool[n * n], noPlace = new bool[n * n], wall = new bool[n * n], windowRange = n / 2 };
        for (int i = 0; i < n * n; i++) { g.known[i] = true; g.wall[i] = true; }
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                int y = h - 1 - r; char c = rows[r][x]; int i = x * n + y;
                g.pass[i] = c == '.' || c == 'g';
                g.wall[i] = c == '#';
                g.known[i] = c != '?';
            }
        return g;
    }

    // 13-단계3-검수반영.md: the fixes of the stage 3 review (Review/stage3-review.md F1-F5, S1, S3, S4)
    static void Review3(EnemyReach rbDoor, BreachScan scanR, List<EdgeBatch> rbsR)
    {
        const float range = 8f;
        // F1. an enemy behind a fence that shoots, jumps or flies is a threat; any enemy in range is one while the AI is
        //     being hurt; a walker behind a fence that does not hit stays none (단계 1 M4c)
        string[] rowsF = { "#######", "#.A~.f#", "#######" };
        var gF = Grid(rowsF, out var mf);
        var mapF = AIBaseGuard.Reach(gF, mf['A']);
        var walker = AIBaseGuard.ReachOf(mapF, mf['f']);
        var ranged = AIBaseGuard.ReachOf(mapF, mf['f'], EnemyMove.Ranged);
        var jumper = AIBaseGuard.ReachOf(mapF, mf['f'], EnemyMove.Jump);
        Program.Check(ranged.comes && !ranged.walks && jumper.comes && !walker.comes, $"RF1a. behind a fence: a ranged attacker and a jumper get at the AI (shoot / jump over), a walker does not ({ranged.comes} {jumper.comes} {walker.comes})");
        Program.Check(AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (ranged, 4f) }, range), "RF1b. a ranged enemy 4 tiles away behind a fence: fight state (fast re-decision, close-fight set, combat hp trigger)");
        Program.Check(!AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (walker, 4f) }, range) && AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (walker, 4f) }, range, hurt: true),
            "RF1c. T: an enemy behind a fence + health lost within 1.5 s -> fight state (any enemy in range); not hurt -> no fight state (M4c kept)");
        Program.Check(!AIBaseGuard.IsThreat(walker, 9f, range, true) && AIBaseGuard.IsThreat(walker, 6f, range, true) && !AIBaseGuard.IsThreat(rbDoor, 6f, range, true), "RF1d. hurt: an enemy in range over a fence is a threat, beyond the range none; one past a closed door none (recheck R1)");
        // self-defence while a ranged enemy shoots over the fence: it starts at the first hit and runs while it hits
        bool on = false; float last = -1e9f; int combat = 0, started = 0; float ended = -1f;
        for (float t = 0f; t <= 20f; t += 0.5f)
        {
            bool hurt = t >= 2f && t < 8f && (t % 1f) < 0.01f;   // a hit every second from 2 s to 7 s
            bool hurtWin = t >= 2f && t < 8.5f;                   // HpChange(1.5 s) < 0 after each hit
            bool threat = AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (walker, 4f) }, range, hurtWin);
            var ev = AIBaseGuard.DefenceStep(ref on, ref last, threat, hurt, t);
            if (threat) combat++;
            if (ev == AIBaseGuard.DefenceEvent.Started) started++;
            if (ev == AIBaseGuard.DefenceEvent.Ended && ended < 0f) ended = t;
        }
        Program.Check(started == 1 && combat == 13 && ended >= 11f && ended <= 11.6f, $"RF1e. hit over a fence for 6 s: one self-defence episode, fight state while hit ({combat} ticks), ends 3 s after ({ended:F1} s)");
        string kfR = AIBaseGuard.KillFacts(null, mf['f'], ranged, "SlimeShooter");
        Program.Check(kfR.Contains("울타리·물 너머로 쏠 수 있음") && kfR.Contains("투사체") && AIBaseGuard.KillFacts(null, mf['f'], jumper, "KingSlime").Contains("뛰어넘을"), $"RF1f. kill facts: a ranged attacker shoots over fences, a jumper jumps them ({kfR})");

        // F2. a walk whose game path search finds no way: an own door between comes before the short tile-A* hop (which
        //     treats door tiles as blocked): the target 2 tiles past an own door goes through it
        var step = AIDoorPass.NoPathNext(ownDoorToward: true, doorsPassable: false, passedDoor: false, shortHopTried: false, shortNear: true, knownWay: false, padHopDone: false, onPadWithExit: false);
        Program.Check(step == AIDoorPass.NoPathStep.OwnDoor, $"RF2a. target 2 tiles away with an own door between: through the door first, not the short hop ({step})");
        var after = AIDoorPass.NoPathNext(true, false, true, false, true, false, false, false);
        var after2 = AIDoorPass.NoPathNext(true, false, true, true, true, false, false, false);
        Program.Check(after == AIDoorPass.NoPathStep.ShortHop && after2 == AIDoorPass.NoPathStep.FailAfterDoor, $"RF2b. past the door: the short hop once, then 'no way after the door' (one door per walk, N1) ({after} {after2})");
        Program.Check(AIDoorPass.NoPathNext(false, false, false, false, true, false, false, false) == AIDoorPass.NoPathStep.ShortHop
            && AIDoorPass.NoPathNext(false, false, false, true, true, false, false, true) == AIDoorPass.NoPathStep.PadHop
            && AIDoorPass.NoPathNext(false, false, false, true, false, false, false, false) == AIDoorPass.NoPathStep.Fail, "RF2c. no own door between: the short hop, the pad hop and the failure as before");
        // the case of the review: AI inside next to its door, the enemy 2 tiles outside; the only walk is through the door
        string[] rowsD = { "#######", "#.A.D.e", "#######" };
        var gD = Grid(rowsD, out var md);
        var rd = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gD, md['A']), md['e']);
        Program.Check(rd.kind == ReachKind.Door && rd.door == (4, 1) && rd.steps == 4, $"RF2d. the enemy 2 tiles past the own door: reached only through door (4,1) ({rd.kind} {rd.door} {rd.steps})");

        // F3. what cannot be done goes to the action model: repair with no blocks, a gone door with no door in the bag,
        //     a door spot waiting for its side walls
        string rn = AIBaseGuard.RepairBlockedNote(scanR, 0, rbsR.Count);
        Program.Check(rn.Contains("부서진 곳 7칸") && rn.Contains("가진 벽 블록 0개") && rn.Contains("놓을 수 없음"), $"RF3a. repair, no wall block: 'broken N tiles, 0 blocks — cannot place' ({rn})");
        string dn = AIBaseGuard.DoorBackBlockedNote((8, 2), true, false, "");
        Program.Check(dn.Contains("(8,2)") && dn.Contains("가방에 문 없음") && dn.Contains("적도 지나감"), $"RF3b. door back, no door in the bag ({dn})");

        // F5. door spots: (a) a diagonal gap whose door spot needs a wall off the plan: that tile is in the fill batches;
        //     (b) DoorSpotOf and SidesReady read walls the same way (a wall tile or a fence; an object is not a wall)
        var gDg = EGrid(new[] { "#...#", "##.g#", "#.g##", "#...#", "#####" });
        var gap = new EdgeGap(); gap.tiles.Add((2, 2)); gap.tiles.Add((3, 3));
        var spot = AIBaseEdgeCore.DoorSpotOf(gDg, gap);
        var need = AIBaseEdgeCore.DoorSides(gDg, gap, spot.tile);
        var opt = new EdgeOption(); opt.gaps.Add(gap);
        var bs = AIBaseEdgeCore.Batches(gDg, opt, spot.tile);
        bool inBatch = need.place.Count == 1 && bs.Exists(b => b.tiles.Contains(need.place[0]) && b.doorSides.Contains(need.place[0]))
            && !AIBaseEdgeCore.Batches(gDg, opt, spot.tile, t => t == need.place[0]).Exists(b => b.tiles.Contains(need.place[0]));   // a gone placed wall there is a repair instead
        Program.Check(spot.extraWalls == 1 && need.Need == 1 && inBatch && !bs.Exists(b => b.tiles.Contains(spot.tile)), $"RF5a. diagonal gap: door spot ({spot.tile.x},{spot.tile.y}) needs 1 wall off the plan ({(need.place.Count > 0 ? need.place[0].ToString() : "-")}); it is in the fill batch, the door is not");
        bool Wall((int x, int y) u) => gDg.In(u.x, u.y) && gDg.wall[gDg.Idx(u.x, u.y)];
        bool before = AIBaseEdgeCore.SidesReady(Wall, spot.tile);
        foreach (var b in bs) foreach (var t in b.tiles) gDg.wall[gDg.Idx(t.x, t.y)] = true;   // the batches placed
        Program.Check(!before && AIBaseEdgeCore.SidesReady(Wall, spot.tile), "RF5b. before the batch the door cannot go in; after it (gap tile + the extra side wall) both sides are walls");
        string wt = need.Text(spot.tile.x, spot.tile.y);
        Program.Check(wt.Contains("양옆 벽 대기(") && wt.Contains("1칸 더 필요") && wt.Contains("메우기 묶음"), $"RF3c. the door spot's wait is a fact: '양옆 벽 대기(… 1칸 더 필요 …)' ({wt})");
        var gO = EGrid(new[] { "#####", "#...#", "#og##", "#...#", "#####" });
        var gapO = new EdgeGap(); gapO.tiles.Add((2, 2));
        var spotO = AIBaseEdgeCore.DoorSpotOf(gO, gapO);
        var needO = AIBaseEdgeCore.DoorSides(gO, gapO, spotO.tile);
        bool WallO((int x, int y) u) => gO.In(u.x, u.y) && gO.wall[gO.Idx(u.x, u.y)];
        Program.Check(spotO.extraWalls == 1 && !AIBaseEdgeCore.SidesReady(WallO, spotO.tile) && needO.cannot.Contains((1, 2)) && needO.Text(2, 2).Contains("놓을 수 없음"),
            $"RF5c. an object beside the door spot is no wall for both: the spot says 1 more needed (an object tile: no block can go there), SidesReady agrees ({spotO.extraWalls})");

        // F5 follow-up (HEAD): a door spot's side wall placed off the cut is edge memory: when it is gone it is a breach
        //     and a repair batch of its own (it closes no opening, so it is never the closing tile)
        var shapeS = AIBaseGuard.Shape(Area(1, 1, 3, 3), new List<(int, int)> { (4, 2) }, new List<(int, int)> { (5, 2), (4, 1) });
        var scanS = AIBaseGuard.Breaches(shapeS, new List<(int, int)> { (5, 2), (4, 1) }, new List<(int, int)> { (4, 2) },
            t => t == (5, 2) || t == (2, 4) ? EdgeNow.Open : t == (4, 2) ? EdgeNow.OwnDoor : EdgeNow.Closed);
        var sideBr = scanS.list.Find(x => x.tile == (5, 2));
        var rbsS = AIBaseGuard.RepairBatches(scanS, t => shapeS.Inside(t.x, t.y - 1) ? (t.x, t.y - 1) : ((int, int)?)null);
        var sideB = rbsS.Find(x => x.tiles.Contains((5, 2)));
        var openB = rbsS.Find(x => x.tiles.Contains((2, 4)));
        Program.Check(shapeS.IsEdge(5, 2) && shapeS.sides.Count == 1 && sideBr != null && sideBr.side && sideBr.kind == BreachKind.WallGone && sideBr.Text.Contains("문 자리 옆")
            && sideB != null && sideB.group.Count == 0 && !sideB.closesGap && sideB.doorSides.Contains((5, 2)) && !sideB.tiles.Contains((2, 4))
            && openB != null && openB.closesGap && openB.group.Contains((2, 4)),
            $"RF5d. a side wall placed off the cut is edge memory: gone -> breach '{sideBr?.Text}', its own repair batch closing no opening; the other opening still closes ({rbsS.Count} batches)");

        // F4. repair materials in a base closed all round: the walls outside are reached through the own door (the walls
        //     reached openly from inside are all edge walls); nearest first, at most 3
        string[] rowsM =
        {
            "#########",
            "#...#...#",
            "#.A.D...#",
            "#...#...#",
            "#########",
        };
        var gM = Grid(rowsM, out var mm);
        var mapM = AIBaseGuard.Reach(gM, mm['A']);
        var spots = AIBaseGuard.MaterialSpots(mapM, new List<(int, int)> { (8, 2), (6, 4), (5, 0), (7, 0) });
        Program.Check(spots.Count == 3 && spots.TrueForAll(s => s.kind == ReachKind.Door && s.door == (4, 2)) && spots[0].tile == (5, 0) && spots[0].steps == 4 && !spots.Exists(s => s.tile == (7, 0)),
            $"RF4a. closed base: material walls through door (4,2), nearest first, 3 at most ({string.Join(" ", spots.ConvertAll(s => $"{s.tile}:{s.steps}"))})");
        Program.Check(AIBaseGuard.MatWayText(spots[0]).Contains("문 (4,2)을 지나 4칸"), $"RF4b. the fact says the door and the steps ({AIBaseGuard.MatWayText(spots[0])})");
        var openSpot = AIBaseGuard.MaterialSpots(mapM, new List<(int, int)> { (2, 4) });
        Program.Check(openSpot.Count == 1 && openSpot[0].kind == ReachKind.Open && openSpot[0].steps == 1, "RF4c. a wall reached by an open walk stays 'open way N tiles'");

        // S1. the door between is the weak part of the way (health 2)
        var shapeK = AIBaseGuard.Shape(Area(1, 3, 5, 6), new List<(int, int)> { (6, 5) });
        string kfD = AIBaseGuard.KillFacts(shapeK, (9, 5), rbDoor, "SlimeBlob");
        Program.Check(kfD.Contains("문은 체력 2") && kfD.Contains("wall-break-ways (1)") && kfD.Contains("추정") && AIBaseGuard.KnownNoBreaker.Contains("체력 2"), $"RS1. past a closed door: 'the door has health 2, a blocked chaser can break it soon (estimate)' ({kfD})");

        // S3. pick-up: the memory forgets a gone object also when the pick-up fails; the empty hand before a broken pick;
        //     a pick swing can hit the edge walls next to it
        var pg = new PickupState();
        AIBaseGuard.PickupStep(pg, new PickupObs { holdOk = true, objectThere = false, now = 0f, bagCount = 2 });
        AIBaseGuard.PickupStep(pg, new PickupObs { holdOk = true, objectThere = false, now = 3.5f, bagCount = 2 });
        var pt = new PickupState();
        AIBaseGuard.PickupStep(pt, new PickupObs { holdOk = true, objectThere = true, objHp = 2, dist = 1f, now = 0f });
        AIBaseGuard.PickupStep(pt, new PickupObs { holdOk = true, objectThere = true, objHp = 2, dist = 1f, now = 5.5f });
        Program.Check(pg.phase == PickupPhase.Collect && AIBaseGuard.ShouldForget(pg) && pt.phase == PickupPhase.Hit && !AIBaseGuard.ShouldForget(pt), "RS3a. a gone object is forgotten while collecting; an unchanged object stays observed and actionable");
        Program.Check(AIBaseGuard.PickupHoldChoice(false, true, true, true) == AIBaseGuard.PickupHold.EmptyHand
            && AIBaseGuard.PickupHoldChoice(false, false, true, true) == AIBaseGuard.PickupHold.FreeHand
            && AIBaseGuard.PickupHoldChoice(false, false, false, true) == AIBaseGuard.PickupHold.BrokenPick
            && AIBaseGuard.PickupHoldChoice(true, true, true, true) == AIBaseGuard.PickupHold.WorkingPick, "RS3b. hold: a working pick, else the empty hand (free or made free), a broken pick last");
        Program.Check(AIBaseGuard.PickupToolNote("WoodMiningPick", true).Contains("옆 둘레 벽") && !AIBaseGuard.PickupToolNote("맨손", false).Contains("옆 둘레 벽"), "RS3c. a pick swing: 'it can also hit the edge wall next to it (estimate)'");

        // S4. the breach trigger: a reason that comes while another waits is added; a new plan counts again
        var w = new AIBaseGuard.BreachWatch();
        var two = new List<Breach> { new Breach { tile = (1, 4), kind = BreachKind.NaturalGone }, new Breach { tile = (8, 1), kind = BreachKind.WallGone } };
        string r1 = AIBaseGuard.BreachTrigger(w, 1, two), r2 = AIBaseGuard.BreachTrigger(w, 1, two), r3 = AIBaseGuard.BreachTrigger(w, 2, two);
        Program.Check(r1 != null && r1.Contains("부서짐 2곳") && r2 == null && r3 != null, $"RS4a. breaches: rings once, not again for the same, again with a new plan ({r1 != null} {r2 == null} {r3 != null})");
        string merged = AIBaseGuard.AddReason("체력 감소(80→70)", r1);
        Program.Check(merged.Contains("체력 감소") && merged.Contains("기지 둘레 부서짐") && AIBaseGuard.AddReason(merged, r1) == merged && AIBaseGuard.AddReason("", r1) == r1, $"RS4b. a waiting reason keeps the breach ({merged})");

        // ---- the stage 3 recheck (Review/stage3-recheck.md R1-R5)
        // R1. "hurt" adds only enemies that could be hitting (no wall / closed door between); a lower max health is no hit
        var gSeal = Grid(new[] { "#####", "#A#e#", "#####" }, out var msl);
        var sealedR = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gSeal, msl['A']), msl['e']);
        Program.Check(!sealedR.flyReach && !AIBaseGuard.IsThreat(sealedR, 2f, range, true) && AIBaseGuard.IsThreat(walker, 4f, range, true),
            "RR1a. a walker sealed in a wall pocket + health -1: no threat (nothing crosses a wall); behind a fence + hurt: a threat");
        Program.Check(!AIBaseGuard.HurtBy(10, 10, 9, 9) && AIBaseGuard.HurtBy(10, 10, 8, 9) && AIBaseGuard.HurtBy(10, 10, 9, 10) && !AIBaseGuard.HurtBy(7, 10, 7, 9),
            "RR1b. hurt: a drop with the max health (hunger) is no hit; a drop beyond it, or with the max unchanged, is");
        // R2. a door's side tiles are the two along its wall axis, not the passage tiles
        Program.Check(AIBaseEdgeCore.IsDoorSide((5, 2), (4, 2), true) && AIBaseEdgeCore.IsDoorSide((3, 2), (4, 2), true) && !AIBaseEdgeCore.IsDoorSide((4, 3), (4, 2), true)
            && AIBaseEdgeCore.IsDoorSide((4, 1), (4, 2), false) && !AIBaseEdgeCore.IsDoorSide((5, 2), (4, 2), false), "RR2. door side tiles: the wall axis only (east/west or north/south), never the passage in front / behind");
        // R3. a known open way before the own door; a failed try's A* takes the next try
        var k1 = AIDoorPass.NoPathNext(true, false, false, false, true, true, false, false);
        var k2 = AIDoorPass.NoPathNext(true, false, false, true, true, true, false, false);
        var d1 = AIDoorPass.NoPathNext(true, false, false, false, true, false, false, false);
        var d2 = AIDoorPass.NoPathNext(false, false, false, false, true, false, false, false);   // the door's A* failed: door used, not passable
        var d3 = AIDoorPass.NoPathNext(false, false, false, true, true, false, false, false);
        Program.Check(k1 == AIDoorPass.NoPathStep.ShortHop && k2 == AIDoorPass.NoPathStep.OwnDoor && d1 == AIDoorPass.NoPathStep.OwnDoor && d2 == AIDoorPass.NoPathStep.ShortHop && d3 == AIDoorPass.NoPathStep.Fail,
            $"RR3. known open way first, then the door if its hop fails; door first, then the short hop if the door's A* fails, then fail ({k1} {k2} / {d1} {d2} {d3})");
        // R4. an inside door-side wall that is gone: repaired (a side batch), and the wait text says repair, not fill
        var shapeI = AIBaseGuard.Shape(Area(1, 1, 3, 3), new List<(int, int)> { (4, 2) }, new List<(int, int)> { (3, 2) });
        var scanI = AIBaseGuard.Breaches(shapeI, new List<(int, int)> { (3, 2) }, new List<(int, int)>(), t => t == (3, 2) || t == (4, 2) ? EdgeNow.Open : EdgeNow.Closed);
        var rbsI = AIBaseGuard.RepairBatches(scanI, t => ((int, int)?)null);
        var gD2 = EGrid(new[] { "#...#", "##.g#", "#.g##", "#...#", "#####" });
        var gap2 = new EdgeGap(); gap2.tiles.Add((2, 2)); gap2.tiles.Add((3, 3));
        string wt2 = AIBaseEdgeCore.DoorSides(gD2, gap2, (3, 3)).Text(3, 3, t => t == (2, 3));
        Program.Check(shapeI.sides.Contains(EdgeGrid.Key(3, 2)) && scanI.list.Exists(b => b.tile == (3, 2) && b.side) && rbsI.Exists(b => b.tiles.Contains((3, 2)) && b.group.Count == 0)
            && wt2.Contains("보수 묶음") && !wt2.Contains("메우기 묶음"), $"RR4. an inside door-side wall gone: a side repair batch; the door-spot fact says 'in a repair batch' ({wt2})");
        // R5. the mine fact of a door's side wall off the cut; the jump fact matches the pit calculation
        Program.Check(AIBaseGuard.EdgeWallNote(shapeS, (5, 2)) == AIBaseGuard.DoorSideWallText && AIBaseGuard.DoorSideWallText.Contains("캐면 문 양옆이 빔")
            && AIBaseGuard.EdgeWallNote(shapeS, (2, 4)) == AIBaseGuard.EdgeWallText && AIBaseGuard.EdgeWallNote(shapeS, (2, 2)) == "", "RR5a. mining a door's side wall: 'the door loses a side', not 'the edge opens'; an edge wall keeps 'the edge opens'");
        var none = AIBaseGuard.WallFact(null, (0, 0));
        var sideFact = AIBaseGuard.WallFact(shapeS, (5, 2));
        var wallFact = AIBaseGuard.WallFact(shapeS, (2, 4));
        Program.Check(none.Kind == EdgeWallKind.None && !none.OpensEdge && none.Text == ""
            && AIBaseGuard.WallFact(shapeS, (2, 2)).Kind == EdgeWallKind.None
            && AIBaseGuard.WallFact(shapeS, (100, 100)).Kind == EdgeWallKind.None, "R2b. null, inside and unrelated walls have no edge fact");
        Program.Check(sideFact.Kind == EdgeWallKind.DoorSideWall && !sideFact.OpensEdge && sideFact.Text == AIBaseGuard.DoorSideWallText
            && wallFact.Kind == EdgeWallKind.BoundaryWall && wallFact.OpensEdge && wallFact.Text == AIBaseGuard.EdgeWallText,
            "R2b. wall meaning and exact display remain compatible");
        string jf = AIBaseGuard.KillFacts(null, mf['f'], jumper, "KingSlime");
        Program.Check(jf.Contains("울타리·물을 뛰어넘을") && !jf.Contains("구덩이를 뛰어넘을") && jf.Contains("구덩이를 막힘으로"), $"RR5b. the jump fact says fences / water, and that pits count as closed here ({jf})");
    }
}
