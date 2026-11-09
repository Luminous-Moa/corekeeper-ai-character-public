// G3r — the stage 3 re-measurement fixes (18-문·도달-정리.md; the facts are 17-재실측.md, raw data
// C:\Dev\_scratch\base-s3r). Two lines: the body at a door and its press (N-a, 결함 2, N-c, N-f, N-e) and whether an
// enemy gets at the AI (N-b, N-g, 결함 1). Where possible the logged situation is rebuilt as it was.
using System;
using System.Collections.Generic;
using System.IO;

public static class RemeasureTests
{
    public static void Run()
    {
        PressWindow(); DoorTile(); PlaceThroughDoor(); Option4Base(); DoorBetween(); Small();
    }

    // ------------------------------------------------------------------ a body that slides (for the door step)

    // The AI's body as the live run showed it: 5.7 tiles/s, the input reaches the body one frame late and the speed
    // follows it with a time constant (the tail stopped its input at 0.92 and stood at 0.50: about 0.42 tile of slide);
    // 30 frames a second (lowgfx 30). A shut door's collider holds the body centre 0.35 off the door centre (pressed
    // against it: (-3.85,-5.65) for the door (-4,-6)). The game takes a press from 0.53 to 0.90 off the centre when the
    // body looks at the door (failures at 0.35-0.50, a success while walking in from 1.10).
    internal sealed class Body
    {
        public float x, y, vx, vy; float lagX, lagY;
        public const float Speed = 5.7f, K = 25f, Dt = 1f / 30f;
        public bool canMove = true;
        public void Step(float mx, float my, float doorX, float doorY, bool ew, bool doorShut)
        {
            float ix = lagX, iy = lagY; lagX = mx; lagY = my;   // one frame of input delay
            float a = Math.Min(1f, K * Dt);
            vx += (ix * Speed - vx) * a; vy += (iy * Speed - vy) * a;
            if (!canMove) { vx = vy = 0f; return; }
            x += vx * Dt; y += vy * Dt;
            if (doorShut)
            {
                // along = across the door (the passage axis), side = along the door's wall
                float along = ew ? x - doorX : y - doorY, side = ew ? y - doorY : x - doorX;
                if (Math.Abs(side) < 0.6f && Math.Abs(along) < 0.35f)
                {
                    float s = along >= 0f ? 0.35f : -0.35f;
                    if (ew) { x = doorX + s; vx = 0f; } else { y = doorY + s; vy = 0f; }
                }
            }
        }
    }
    internal static bool Taken(float x, float y, float fx, float fy, float doorX, float doorY)
    {
        float dx = doorX - x, dy = doorY - y, d = (float)Math.Sqrt(dx * dx + dy * dy);
        if (d < 0.53f || d > 0.90f) return false;
        float fl = (float)Math.Sqrt(fx * fx + fy * fy);
        return fl > 0f && (dx * fx + dy * fy) / (d * fl) > 0.342f;
    }

    // a full door pass (open -> through -> close) from (x, y); dir = from the start side through the door
    static DoorPassState RunPass(float x, float y, float doorX, float doorY, float dirX, float dirY, out List<float> pressDist, out bool shutAtEnd, bool canMove = true)
    {
        var s = new DoorPassState(); var b = new Body { x = x, y = y, canMove = canMove };
        bool ew = Math.Abs(dirX) > 0.5f, open = false, pressDone = true; float t = 0f;
        pressDist = new List<float>();
        for (int k = 0; k < 900 && s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed; k++, t += Body.Dt)
        {
            var o = AIDoorPass.Step(s, DoorKind.Manual, doorX, doorY, dirX, dirY, b.x, b.y, open, pressDone, t);
            if (!pressDone) { pressDone = true; }
            if (o.press)
            {
                pressDone = false;
                pressDist.Add((float)Math.Sqrt((b.x - doorX) * (b.x - doorX) + (b.y - doorY) * (b.y - doorY)));
                if (Taken(b.x, b.y, o.fx, o.fy, doorX, doorY)) open = !open;
            }
            b.Step(o.mx, o.my, doorX, doorY, ew, !open);
        }
        shutAtEnd = !open; LastT = t;
        return s;
    }

    // the closing tail from (x, y) with the door open
    static DoorOwe RunTail((int x, int y) door, float x, float y, int insideSign, out List<float> pressDist, out bool shut)
    {
        var o = new DoorOwe { door = door };
        AIBaseGuard.StartTail(o, false, x, y, insideSign, true, true);
        var b = new Body { x = x, y = y };
        bool open = true, pressDone = true, done = false; float t = 0f;
        pressDist = new List<float>();
        for (int k = 0; k < 600 && !done; k++, t += Body.Dt)
        {
            var r = AIBaseGuard.TailStep(o, open, pressDone, b.x, b.y, t, out done);
            if (!pressDone) pressDone = true;
            if (r.press)
            {
                pressDone = false;
                pressDist.Add((float)Math.Sqrt((b.x - door.x) * (b.x - door.x) + (b.y - door.y) * (b.y - door.y)));
                if (Taken(b.x, b.y, r.fx, r.fy, door.x, door.y)) open = !open;
            }
            b.Step(r.mx, r.my, door.x, door.y, false, !open);
        }
        shut = !open; LastT = t;
        return o;
    }

    static float LastT;
    static string L(List<float> l) => string.Join(" ", l.ConvertAll(v => v.ToString("F2")));
    static bool AllIn(List<float> l) { foreach (var v in l) if (v < AIDoorPass.PressMin - 1e-3f || v > AIDoorPass.PressMaxWide + 1e-3f) return false; return l.Count > 0; }

    // N-a: a press from less than ~0.5 off the door centre is not taken; the press is made only from the press window
    static void PressWindow()
    {
        Program.Check(!AIDoorPass.InPressWindow(0.35f, 0f) && !AIDoorPass.InPressWindow(0.50f, 0f) && !AIDoorPass.InPressWindow(0.54f, 0f) && AIDoorPass.InPressWindow(0.62f, 0f) && AIDoorPass.InPressWindow(0.70f, 0f)
            && !AIDoorPass.InPressWindow(0.75f, 0f) && AIDoorPass.InPressWindow(0.75f, 0f, true) && !AIDoorPass.InPressWindow(0.92f, 0f, true) && !AIDoorPass.InPressWindow(-0.62f, 0f) && !AIDoorPass.InPressWindow(0.62f, 0.35f),
            "Na1. the press window: 0.35 / 0.50 / 0.54 no (재실측 failures), 0.62 / 0.70 yes, 0.75 only widened, 0.92 never, not on the other side, not far off the axis");
        // 14-단계3 4절 'outside close failed': the AI went 0.46 <-> 1.02 off the centre and pressed at both — both out of the window
        Program.Check(!AIDoorPass.InPressWindow(0.46f, 0f, true) && !AIDoorPass.InPressWindow(1.02f, 0f, true), "Na2. the earlier outside-close failure (14-단계3: pressed at 0.46 and 1.02) is the same cause: both places are out of the window");

        // t=2023.8 (inside): pressed against the shut door at (-3.85,-5.65) = 0.35 off the centre, a walk out through it
        var s1 = RunPass(-3.85f, -5.65f, -4f, -6f, 0f, -1f, out var p1, out var shut1);
        Program.Check(s1.phase == DoorPhase.Done && shut1 && AllIn(p1), $"Na3. pressed against the door from inside (0.35 off): steps back, opens, passes, closes; presses at {L(p1)}, {LastT:F1} s ({s1.facts}{s1.fail})");
        // t=705.9 / t=1885 (outside): pressed at (-14,-17.35) = 0.35 off the door (-14,-17), a walk in
        var s2 = RunPass(-14f, -17.35f, -14f, -17f, 0f, 1f, out var p2, out var shut2);
        Program.Check(s2.phase == DoorPhase.Done && shut2 && AllIn(p2), $"Na4. pressed against the door from outside (0.35 off): the same; presses at {L(p2)}, {LastT:F1} s ({s2.facts}{s2.fail})");
        // a walk from 3 tiles off at full speed (the slide would carry an early press into the door)
        var s3 = RunPass(-4f, -3f, -4f, -6f, 0f, -1f, out var p3, out var shut3);
        Program.Check(s3.phase == DoorPhase.Done && shut3 && AllIn(p3), $"Na5. from 3 tiles off at full speed: presses at {L(p3)}, {LastT:F1} s ({s3.facts}{s3.fail})");
        // t=2420: the tail started at (-4.00,-4.38) (1.62 off), the old step stopped the input at 0.92 and slid to 0.50
        var o4 = RunTail((-4, -6), -4f, -4.38f, 1, out var p4, out var shut4);
        Program.Check(shut4 && AllIn(p4) && o4.fact.Contains("닫음"), $"Na6. the tail from (-4.00,-4.38): closes; presses at {L(p4)}, {LastT:F1} s ('{o4.fact}')");
        // t=1992: the tail at (-4.00,-5.51) — 0.49 off, too close: it steps back first
        var o5 = RunTail((-4, -6), -4f, -5.51f, 1, out var p5, out var shut5);
        Program.Check(shut5 && AllIn(p5), $"Na7. the tail at 0.49 off (t=1992): steps back, then closes; presses at {L(p5)}, {LastT:F1} s ('{o5.fact}')");
        // a body that cannot leave 0.35 (something holds it there): no press at all, the step ends by time with where it stood
        var s6 = RunPass(-3.85f, -5.65f, -4f, -6f, 0f, -1f, out var p6, out _, false);
        var heldNear = AIDoorPass.Step(s6, DoorKind.Manual, -4f, -6f, 0f, -1f, -3.85f, -5.65f, false, true, LastT);
        Program.Check(s6.phase != DoorPhase.Failed && s6.phase != DoorPhase.Done && p6.Count == 0 && heldNear.observation != null && !heldNear.press, $"Na8. held at 0.35 off: never presses there; reports '{heldNear.observation}' without ending");
        // after a press the door state (variation) says whether it worked: a door that never answers ends after MaxPresses
        var s7 = new DoorPassState { phase = DoorPhase.Open }; int n7 = 0; float t7 = 0f;
        for (int k = 0; k < 400 && s7.phase == DoorPhase.Open; k++, t7 += Body.Dt) { var o = AIDoorPass.Step(s7, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.62f, 0f, false, true, t7); if (o.press) n7++; }
        var waitingState = AIDoorPass.Step(s7, DoorKind.Manual, 0f, 0f, 1f, 0f, -0.62f, 0f, false, true, t7);
        Program.Check(s7.phase == DoorPhase.Open && n7 > AIDoorPass.MaxPresses && waitingState.observation.Contains("0.62칸"), $"Na9. unanswered presses remain observable while opening: {n7}, '{waitingState.observation}'");
    }

    // 결함 2: pressed against the shut door (-4,-6) at (-3.85,-5.65) the rounded tile is the door tile; a walk that may
    // plan through own doors planned from it (A* 0 tiles: 걷기·치우기·캐기·공격 failed)
    static void DoorTile()
    {
        (int, int) door = (-4, -6);
        var walls = new HashSet<(int, int)> { (-5, -6), (-3, -6) };
        Func<(int x, int y), bool> canStand = t => t != door && !walls.Contains(t);
        Func<(int x, int y), bool> allowedThroughDoor = t => !walls.Contains(t);   // _doorsPassable: the own door tile is allowed
        var rounded = ((int)Math.Round(-3.85), (int)Math.Round(-5.65));
        var from = AIBaseGuard.PlanFrom(-3.85f, -5.65f, canStand, allowedThroughDoor, (-5, -8));
        Program.Check(rounded == door && allowedThroughDoor(rounded), "D2a. (as logged) the rounded tile is the door tile and the door-passing plan allows it (the A* from there to the door was 0 tiles)");
        Program.Check(from == (-4, -5), $"D2b. the plan starts from the stand tile (-4,-5), not the door ({from.Item1},{from.Item2})");
        // the path from the stand tile to the door is one step, through the door southwards: the door step gets its axis
        var path = Bfs(from, door, t => canStand(t) || t == door);
        Program.Check(path == 1, $"D2c. from the stand tile the door is {path} step away (the door step runs: open -> through -> close)");
        // outside, pressed at (-14,-17.35) against the door (-14,-17)
        var fromOut = AIBaseGuard.PlanFrom(-14f, -17.35f, t => t != (-14, -17), t => true, (-10, -12));
        Program.Check(fromOut == (-14, -18), $"D2d. pressed from outside the plan starts outside ({fromOut.Item1},{fromOut.Item2})");
        // the stand tile not allowed (a tile the walk got stuck on): its nearest allowed neighbour
        var fromStuck = AIBaseGuard.PlanFrom(-4.1f, -4.6f, canStand, t => t != (-4, -5), (-4, -1));
        Program.Check(fromStuck == (-4, -4), $"D2e. the stand tile itself blocked for the plan: the nearest allowed neighbour ({fromStuck.Item1},{fromStuck.Item2})");
    }

    static int Bfs((int x, int y) a, (int x, int y) b, Func<(int x, int y), bool> ok)
    {
        var d = new Dictionary<(int, int), int> { [a] = 0 }; var q = new Queue<(int, int)>(); q.Enqueue(a);
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (c == b) return d[c];
            foreach (var n in new[] { (c.Item1 + 1, c.Item2), (c.Item1 - 1, c.Item2), (c.Item1, c.Item2 + 1), (c.Item1, c.Item2 - 1) })
                if (!d.ContainsKey(n) && ok(n) && Math.Abs(n.Item1) < 40 && Math.Abs(n.Item2) < 40) { d[n] = d[c] + 1; q.Enqueue(n); }
        }
        return -1;
    }

    // N-f: the repair stand inside reached through the own door from outside (t=1867.9: "갈 수 있는 설 칸이 없음");
    // N-e: the stand's side is said as it is
    static void PlaceThroughDoor()
    {
        // a small base: inside x -6..-2, y -5..-1; the door (-4,-6) on the south edge; the AI outside at (-4,-7);
        // the repair tile (-5,0) on the north edge, its inside stand (-5,-1)
        int n = 21, lo = -14;
        var g = new PlaceGridInput { n = n, loX = lo, loY = lo, pass = new bool[n * n], noStand = new bool[n * n], door = new bool[n * n], meX = -4, meY = -7, meFx = -4f, meFy = -7f };
        bool Inside(int x, int y) => x >= -6 && x <= -2 && y >= -5 && y <= -1;
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                int x = lo + i, y = lo + j, k = i * n + j;
                bool outside = y <= -7 || x <= -8 || x >= 0 || y >= 1 && y != 1;   // the cave around
                g.pass[k] = Inside(x, y) || outside && !(y >= 0);
                if (x == -5 && y == 0) g.pass[k] = true;   // the broken edge tile (the place to repair)
                if (x == -4 && y == -6) g.door[k] = true;
            }
        bool found = AIPlaceGrid.StandFor(g, -5, 0, 1, 1, null, PlaceGridInput.Key(-5, -1), out int sx, out int sy);
        var noDoor = new PlaceGridInput { n = n, loX = lo, loY = lo, pass = g.pass, noStand = g.noStand, meX = -4, meY = -7 };
        bool foundOld = AIPlaceGrid.StandFor(noDoor, -5, 0, 1, 1, null, PlaceGridInput.Key(-5, -1), out _, out _);
        Program.Check(!foundOld && found && sx == -5 && sy == -1, $"Nf1. (as logged) without the door no stand is reached from outside; through the own door the inside stand ({sx},{sy}) is");
        bool onDoor = AIPlaceGrid.StandFor(g, -5, -6, 1, 1, null, null, out int dx, out int dy) && dx == -4 && dy == -6;
        Program.Check(!onDoor, "Nf2. the door tile itself is never a stand");
        Program.Check(AIPlaceGrid.ThroughStandNote(true).StartsWith("바깥") && AIPlaceGrid.ThroughStandNote(false).StartsWith("안쪽"),
            $"Ne1. a stand reached through the tile to place is said by its side: outside '{AIPlaceGrid.ThroughStandNote(true)}' / inside '{AIPlaceGrid.ThroughStandNote(false)}'");
    }

    // ------------------------------------------------------------------ the option 4 base (basemap-1.json)

    static Dictionary<string, object> Fixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "basemap-opt4.json");
        if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "core-tests", "fixtures", "basemap-opt4.json");
        return ExpJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
    }
    static List<(int x, int y)> Tiles(object o)
    {
        var l = new List<(int, int)>();
        if (o is List<object> a) foreach (var e in a) if (e is List<object> p && p.Count == 2) l.Add(((int)(double)p[0], (int)(double)p[1]));
        return l;
    }

    // N-b and 결함 1: the AI at (-13,-15) inside the option 4 base, the door (-14,-17) shut, a MushroomEnemy at (-14,-22)
    // (7 tiles). The inside joins the core room only over TheCore's tiles; the core was out of view (15 tiles off), its
    // tiles read as walkable ground, and the way inside -> core -> core room -> never-seen tiles made the enemy "can come"
    // (SD #1 t=591.8: 19 calls in 5.7 s; SD #2 t=875.0: 41 calls in 16.4 s).
    static void Option4Base()
    {
        var d = Fixture();
        if (d == null) { Program.Check(false, "B0. fixture basemap-opt4.json"); return; }
        var rows = ExpJson.GetSA(d, "rows");
        int top = ExpJson.GetI(d, "rowsTopIsY"), left = ExpJson.GetI(d, "rowsLeftIsX");
        char At(int x, int y) { int r = top - y, c = x - left; return r >= 0 && r < rows.Count && c >= 0 && c < rows[r].Length ? rows[r][c] : '?'; }
        var plan = ExpJson.GetO(d, "plan");
        var inside = Tiles(plan["inside"]); var gaps = Tiles(plan["gaps"]); var placed = Tiles(plan["place"]);
        var doorL = plan["door"] as List<object>; (int, int) door = ((int)(double)doorL[0], (int)(double)doorL[1]);
        var objs = new List<(string name, List<(int x, int y)> tiles)>();
        if (d["objects"] is List<object> ol) foreach (var e in ol) if (e is Dictionary<string, object> od) objs.Add((ExpJson.GetS(od, "name"), Tiles(od["tiles"])));
        var core = objs.Find(o => o.name == "TheCore");
        Program.Check(inside.Count == 136 && door == (-14, -17) && core.tiles != null && core.tiles.Count == 25, $"B0. the fixture: inside {inside.Count}, door ({door.Item1},{door.Item2}), TheCore {core.tiles?.Count} tiles");

        (int, int) ai = (-13, -15), enemy = (-14, -22);
        float hx = 15f, hy = 8.5f;   // the view (the camera fallback 30x17 tiles)
        bool InView(int x, int y) => Math.Abs(x - ai.Item1) <= hx && Math.Abs(y - ai.Item2) <= hy;
        // the walk grid as GuardGridAround reads it: tiles (also out of view, once seen), objects only in view
        GuardGrid Grid(BigObjectMemory big, bool withView)
        {
            int r = 24, n = 2 * r + 1;
            var g = GuardGrid.Make(ai.Item1 - r, ai.Item2 - r, n, GuardCell.Unknown);
            if (withView) g.view = new bool[n * n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    int x = g.loX + i, y = g.loY + j, k = i * n + j; char c = At(x, y);
                    bool v = InView(x, y);
                    if (withView) g.view[k] = v;
                    g.cell[k] = c == '?' ? GuardCell.Unknown : c == '#' ? GuardCell.Closed : c == 'D' ? GuardCell.OwnDoor : c == 'd' ? GuardCell.Closed : c == '~' ? GuardCell.Low
                        : c == 'O' ? (v || big != null && big.Has(x, y) ? GuardCell.Closed : GuardCell.Open) : GuardCell.Open;
                }
            return g;
        }
        var shape = AIBaseGuard.Shape(inside, gaps);
        var mem = new BaseMemory(shape, placed, new[] { door });
        bool coreOnEdge = core.tiles.Exists(t => shape.IsEdge(t.x, t.y));
        // as measured: the base memory without the view (the edge read from its tiles), no remembered objects
        var old = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(null, false), ai, mem), enemy);
        Program.Check(coreOnEdge && old.comes && !InView(0, 4), $"B1. (as measured) the core is on the remembered edge and out of view; read from its tiles the enemy past the shut door 'can come' ({old.kind} comes={old.comes})");
        var oldThreat = AIBaseGuard.IsThreat(old, 7.0f, 8f);
        // the fix, one part at a time: the out-of-view edge is what memory says ...
        var byEdge = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(null, true), ai, mem), enemy);
        Program.Check(!byEdge.comes && byEdge.kind == ReachKind.Door && byEdge.memPlanned > 0 && byEdge.memHidden == 0, $"B2. the edge out of view, no look kept since: as the plan says (closed): cannot come, the way is through the door ({byEdge.kind} {byEdge.steps}, {byEdge.memPlanned} edge tiles by the plan)");
        // ... and a large object seen before stays closed out of view (here alone, without the edge rule)
        var big = new BigObjectMemory();
        foreach (var o in objs) if (o.name != "스폰 받침(3x3)" && o.name != "WayPoint") big.Seen(o.name.GetHashCode(), o.name, o.tiles[0].x, o.tiles[0].y, o.tiles);
        var byObj = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(big, false), ai, mem), enemy);
        Program.Check(!byObj.comes, $"B3. the core remembered as a large object: its tiles closed out of view, cannot come ({byObj.kind} comes={byObj.comes})");
        // 검수 F3: the game side now gives the large objects to Reach as its memory layer (not read into the tiles), so the
        // facts say when they decided "cannot come"
        var both = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(null, true), ai, mem, big), enemy);
        string kf = AIBaseGuard.KillFacts(shape, enemy, both, "MushroomEnemy", 7.0f);
        Program.Check(!both.comes && kf.Contains("걸어올 길은 지금 없음") && kf.Contains("문 (-14,-17)을 지나") && kf.Contains("기억으로 정함"), $"B4. both (the game side): the facts say it cannot come, the way is through the door, and that memory decided it ('{kf}')");
        // 결함 1: the fight state over the 16 s of SD #2 (4 walkers at 5-7 tiles behind the shut door)
        int oldCalls = 0, newCalls = 0; bool onO = false, onN = false; float lastO = -10f, lastN = -10f;
        for (float t = 0f; t < 16.4f; t += 0.25f)
        {
            var eo = AIBaseGuard.DefenceStep(ref onO, ref lastO, oldThreat, false, t);
            if (onO && AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (old, 7f) }, 8f)) oldCalls++;
            AIBaseGuard.DefenceStep(ref onN, ref lastN, AIBaseGuard.IsThreat(both, 7f, 8f), false, t);
            if (onN && AIBaseGuard.CombatNow(new List<(EnemyReach, float)> { (both, 7f) }, 8f)) newCalls++;
        }
        Program.Check(oldCalls > 30 && newCalls == 0 && !onN, $"B5. 결함 1 (option 4, 16.4 s): before {oldCalls} combat re-decisions at the 0.25 s floor (measured 41), now {newCalls}, self-defence off");
        // a broken edge tile in view still opens the way (memory does not hide what is seen): the wall (-13,-18) gone
        var g6 = Grid(null, true);
        var gtile = (-13, -18);
        g6.cell[g6.Idx(gtile.Item1, gtile.Item2)] = GuardCell.Open;
        var broken = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g6, ai, new BaseMemory(shape, new[] { (-2, -16) }, new[] { door }), big), enemy);
        Program.Check(broken.comes && InView(gtile.Item1, gtile.Item2), $"B6. a placed wall in view that is gone: the enemy can come again ({broken.kind} {broken.steps})");
        // the object memory forgets an object whose centre is in view and that was not seen there
        var bm = new BigObjectMemory(); bm.Seen(1, "TheCore", 0f, 4f, core.tiles);
        bm.Forget(new HashSet<long>(), (x, y) => false);
        bool kept = bm.Has(0, 4);
        bm.Forget(new HashSet<long>(), (x, y) => true);
        Program.Check(kept && !bm.Has(0, 4) && bm.Count == 0, "B7. a remembered object stays while its centre is out of view, and goes when its centre is in view without it");
    }

    // N-g: the AI pressed inside on the shut door (-4,-6) at (-4.15,-5.65), a walker outside 1.2-1.5 tiles off: no
    // threat across the door (t=2030-2058: SD #4-#6, 28 calls, health unchanged); a ranged or a wall/door-breaking kind is
    static void DoorBetween()
    {
        (int, int) door = (-4, -6);
        GuardGrid G(bool doorOpen)
        {
            var g = GuardGrid.Make(-16, -18, 25, GuardCell.Closed);
            for (int x = -6; x <= -2; x++) for (int y = -5; y <= -1; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;   // inside
            for (int x = -12; x <= 4; x++) for (int y = -14; y <= -7; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;  // the cave outside
            g.cell[g.Idx(door.Item1, door.Item2)] = doorOpen ? GuardCell.Open : GuardCell.OwnDoor;
            return g;
        }
        var shut = G(false);
        var stand = AIBaseGuard.StandTile(-4.15f, -5.65f, t => t != door && shut.At(t.x, t.y) != GuardCell.Closed);
        var m = AIBaseGuard.Reach(shut, stand);
        var ePos = (-4.1f, -7.05f);
        float dist = (float)Math.Sqrt((-4.15f - ePos.Item1) * (-4.15f - ePos.Item1) + (-5.65f - ePos.Item2) * (-5.65f - ePos.Item2));
        var eTile = AIBaseGuard.StandTile(ePos.Item1, ePos.Item2, t => t != door && shut.At(t.x, t.y) != GuardCell.Closed);
        var walker = AIBaseGuard.ReachOf(m, eTile);
        Program.Check(dist <= AIBaseGuard.HitReach && !walker.comes && !walker.touch && !AIBaseGuard.IsThreat(walker, dist, 8f), $"Ng1. a walker {dist:F2} tiles off past the shut door: nothing gets across, no threat ({walker.kind} touch={walker.touch})");
        string fact = AIBaseGuard.CloseNote(walker, dist);
        Program.Check(fact.Contains("사이에 닫힌 문이 있음") && fact.Contains("서로 칠 수 없음") && fact.Contains("문이 열리거나 부서지면"), $"Ng2. the fact says so ('{fact.Trim()}')");
        var brute = walker; brute.breaker = AIBaseGuard.IsBreaker("MushroomBrute");
        var ranged = AIBaseGuard.ReachOf(m, eTile, EnemyMove.Ranged);
        Program.Check(AIBaseGuard.IsThreat(brute, dist, 8f) && AIBaseGuard.IsThreat(ranged, dist, 8f) && AIBaseGuard.CloseNote(brute, dist).Contains("부수는 종류") && AIBaseGuard.CloseNote(ranged, dist).Contains("원거리"),
            "Ng3. a wall/door-breaking kind and a ranged one within 1.5 tiles stay threats, with the fact why");
        var mOpen = AIBaseGuard.Reach(G(true), stand);
        var through = AIBaseGuard.ReachOf(mOpen, eTile);
        Program.Check(through.touch && AIBaseGuard.IsThreat(through, dist, 8f) && AIBaseGuard.CloseNote(through, dist).Contains("막힌 것 없음"), "Ng4. the door open: nothing between, a threat");
        // an enemy pressed against the door from outside rounds onto the door tile; read from its stand tile it is outside
        var pressedOut = AIBaseGuard.ReachOf(m, (-4, -6));
        var eStand = AIBaseGuard.StandTile(-4f, -6.4f, t => t != door && shut.At(t.x, t.y) != GuardCell.Closed);
        var fromStand = AIBaseGuard.ReachOf(m, eStand);
        Program.Check(pressedOut.comes && eStand == (-4, -7) && !fromStand.comes, $"Ng5. an enemy at (-4,-6.4) pressed against the door: read on the door tile it 'comes' (the inside neighbour), from its stand tile (-4,-7) it does not");
        // the 28 calls: 35 s with that walker in front of the door
        bool on = false; float last = -10f; int starts = 0;
        for (float t = 0f; t < 35f; t += 0.25f) if (AIBaseGuard.DefenceStep(ref on, ref last, AIBaseGuard.IsThreat(walker, dist, 8f), false, t) == AIBaseGuard.DefenceEvent.Started) starts++;
        Program.Check(starts == 0 && !on, "Ng6. 35 s with the walker at the door: self-defence never starts (measured SD #4-#6, 28 calls)");
        // a diagonal neighbour round a wall corner is not "nothing between"
        var corner = GuardGrid.Make(0, 0, 5, GuardCell.Open);
        corner.cell[corner.Idx(2, 1)] = GuardCell.Closed; corner.cell[corner.Idx(1, 2)] = GuardCell.Closed; corner.cell[corner.Idx(3, 2)] = GuardCell.Closed; corner.cell[corner.Idx(2, 3)] = GuardCell.Closed;
        corner.cell[corner.Idx(1, 1)] = GuardCell.Closed; corner.cell[corner.Idx(3, 3)] = GuardCell.Closed; corner.cell[corner.Idx(1, 3)] = GuardCell.Closed; corner.cell[corner.Idx(3, 1)] = GuardCell.Closed;
        var sealedIn = AIBaseGuard.ReachOf(AIBaseGuard.Reach(corner, (2, 2)), (3, 3));
        Program.Check(!sealedIn.touch, "Ng7. walls all round: an enemy on the diagonal is not 'nothing between'");
    }

    static void Small()
    {
        // 결정 B: the adopted door of a cut needs no block (the option no longer says "블록 1개 필요")
        var o = new EdgeOption(); var gp = new EdgeGap(); gp.tiles.Add((-4, -6)); o.gaps.Add(gp); o.myDoorCut = 1;
        for (int i = 0; i < 32; i++) o.inside.Add(EdgeGrid.Key(i, 100));
        string f = o.Facts(0, 0);
        Program.Check(!f.Contains("블록 1개 필요") && f.Contains("더 놓을 블록 없음") && o.BlocksNeeded == 0, $"Sb1. the cut through my door: '{f}'");
        // the cut prefers my door: a corridor with two one-tile necks, my door on the farther one
        int n = 13;
        var g = new EdgeGrid { n = n, loX = 0, loY = 0, pass = new bool[n * n], water = new bool[n * n], known = new bool[n * n], noPlace = new bool[n * n], seedX = 6, seedY = 2, windowRange = 6 };
        for (int i = 0; i < n * n; i++) g.known[i] = true;
        for (int x = 4; x <= 8; x++) for (int y = 1; y <= 3; y++) g.pass[g.Idx(x, y)] = true;   // the room
        for (int y = 4; y <= 11; y++) g.pass[g.Idx(6, y)] = true;                                // the corridor north
        for (int x = 1; x <= 11; x++) g.pass[g.Idx(x, 12)] = true;                               // the window's top row (outside)
        var dist = AIBaseEdgeCore.Dist(g);
        var plain = AIBaseEdgeCore.CutAt(g, dist, 2, 0, out _);
        g.myDoor = new bool[n * n]; g.myDoor[g.Idx(6, 8)] = true;
        var withDoor = AIBaseEdgeCore.CutAt(g, dist, 2, 0, out _);
        string P(EdgeOption e) => e == null ? "none" : string.Join(" ", e.gaps.ConvertAll(x => $"({x.tiles[0].x},{x.tiles[0].y})"));
        Program.Check(plain != null && withDoor != null && plain.Blocks == 1 && withDoor.myDoorCut == 1 && withDoor.gaps[0].tiles[0] == (6, 8) && withDoor.BlocksNeeded == 0,
            $"Sb2. the cut goes through my door when it can: without it {P(plain)}, with my door at (6,8) {P(withDoor)} (blocks needed {withDoor?.BlocksNeeded})");
    }
}
