// G3m — 합치기 전 수정 (23-합치기전수정.md; the facts are 22-짧은확인.md, raw data C:\Dev\_scratch\base-s3q; the review
// Review/fix3-review.md C1, C2, D1, D4-D7). The logged situation is rebuilt first (as it was), then the fix.
using System;
using System.Collections.Generic;

public static class MergeFixTests
{
    public static void Run()
    {
        Place(); Press(); Breaker(); Small();
    }

    // The body as the movetrace shows it (22-짧은확인 t=615.0-616.6): frames at 30 a second, server ticks at 20; a tick
    // moves the body 0.2808 tile along the input direction of the last frame at least one tick old (the input is a
    // direction only: full speed). (-4.00,-3.37) walking south gave -3.65, -3.93 ... -4.78, -5.06, -5.34, as logged.
    internal sealed class TickBody
    {
        public float x, y; public const float Step = 0.2808f, TickDt = 0.05f, Frame = 1f / 30f;
        readonly List<(float t, float mx, float my)> _in = new List<(float, float, float)>();
        float _next = TickDt;
        // the server tick's phase against the frames (0 .. TickDt: the first tick comes this much later; DoorSettleTests)
        public TickBody Phase(float p) { _next = TickDt + p; return this; }
        public float Lag = TickDt;   // how old the input a tick uses is at least (DoorSettleTests varies it)
        public void Frame1(float t, float mx, float my)
        {
            float l = (float)Math.Sqrt(mx * mx + my * my);
            if (l > 1e-4f) { mx /= l; my /= l; } else { mx = my = 0f; }
            _in.Add((t, mx, my));
            while (_next <= t + 1e-5f)
            {
                float ix = 0f, iy = 0f;
                for (int i = _in.Count - 1; i >= 0; i--) if (_in[i].t <= _next - Lag + 1e-5f) { ix = _in[i].mx; iy = _in[i].my; break; }
                x += ix * Step; y += iy * Step;
                _next += TickDt;
            }
        }
        // The order the movetrace of the walk in from (-4,-9) shows (22-짧은확인 t=318.70-318.84, DoorSettleTests): a
        // tick is done before the frame that has it is read (px moves on the frame whose server tick went up), and tick N
        // moves by the input of the last frame of tick N-2 (a frame earlier than Lag before it). Advance, read, Input.
        public void Advance(float t)
        {
            while (_next <= t + 1e-5f)
            {
                float ix = 0f, iy = 0f;
                for (int i = _in.Count - 1; i >= 0; i--) if (_in[i].t < _next - Lag - 1e-5f) { ix = _in[i].mx; iy = _in[i].my; break; }
                x += ix * Step; y += iy * Step;
                _next += TickDt;
            }
        }
        public void Input(float t, float mx, float my)
        {
            float l = (float)Math.Sqrt(mx * mx + my * my);
            if (l > 1e-4f) { mx /= l; my /= l; } else { mx = my = 0f; }
            _in.Add((t, mx, my));
        }
    }

    sealed class PlaceRun { public bool pressed, failed; public float t = -1f, x, y, dist; public string why = ""; public int frames; }

    // the walk to the stand as the skill has it: straight at the stand until within 0.35 (the walk's tolerance), then
    // "arrived" (no input from the walk)
    static bool WalkArrived(float x, float y, int sx, int sy) => Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy)) <= 0.35f;

    // as a52384f: in reach (1.1) = press; else walk; arrived = step onto the stand centre within 0.06 at full speed,
    // after 1.2 s "설 칸에 왔지만 닿지 않음". The press branch runs only in reach; a press is taken here when its held
    // frames (0.05-0.55 s after it began) last 3 frames in a row in reach (two server ticks): the swing through the
    // window gave single frames (the real run pressed twice, t=615.4 / 616.2, and nothing was placed).
    static PlaceRun Old(float x0, float y0, int ax, int ay, int stx, int sty)
    {
        var b = new TickBody { x = x0, y = y0 }; var r = new PlaceRun();
        float t = 0f, pressAt = -1f, adjustAt = -1f; bool arrived = false; int held = 0;
        for (int k = 0; k < 120; k++, t += TickBody.Frame)
        {
            float mx = 0f, my = 0f;
            float cx = ax, cy = ay, d = Dist(b.x, b.y, cx, cy);
            bool inReach = Math.Abs(b.x - ax) <= 0.35f && d <= 1.1f && d >= 0.85f || Math.Abs(b.y - ay) <= 0.35f && d <= 1.1f && d >= 0.85f;
            if (!inReach)
            {
                held = 0;
                if (!arrived && !WalkArrived(b.x, b.y, stx, sty)) { mx = stx - b.x; my = sty - b.y; adjustAt = -1f; }
                else
                {
                    arrived = true;
                    if (adjustAt < 0f) adjustAt = t;
                    if (t - adjustAt > 1.2f) { r.failed = true; r.why = "설 칸에 왔지만 닿지 않음"; r.t = t; r.x = b.x; r.y = b.y; return r; }
                    float tx = stx - b.x, ty = sty - b.y;
                    if (tx * tx + ty * ty >= 0.0036f) { mx = tx; my = ty; }
                }
            }
            else
            {
                if (pressAt < 0f) pressAt = t;
                float pt = t - pressAt; if (pt > 0.8f) { pressAt = t; pt = 0f; }
                if (pt > 0.05f && pt < 0.55f) { if (++held >= 3) { r.pressed = true; r.t = t; r.x = b.x; r.y = b.y; r.dist = d; return r; } } else held = 0;
            }
            b.Frame1(t, mx, my);
        }
        r.x = b.x; r.y = b.y;
        return r;
    }

    // now: the walk until it arrives, then AIPlaceGrid.PressStep (standing still on the stand, the press only still in
    // the window); a press is sent once PressStep says press
    static PlaceRun New(float x0, float y0, int ax, int ay, int sx, int sy, int stx, int sty, bool smooth = false, float vy0 = 0f)
    {
        var b = new TickBody { x = x0, y = y0 }; var sb = new RemeasureTests.Body { x = x0, y = y0 };
        var r = new PlaceRun(); var s = new PlacePressState();
        float t = 0f; bool settle = false;
        for (int k = 0; k < 240; k++, t += TickBody.Frame)
        {
            float bx = smooth ? sb.x : b.x, by = smooth ? sb.y : b.y;
            float mx = 0f, my = 0f;
            bool reach = AIPlaceGrid.PressFrom(ax, ay, sx, sy, bx, by);
            if (!settle && !reach && !WalkArrived(bx, by, stx, sty)) { mx = stx - bx; my = sty - by; }
            else
            {
                settle = true;
                var o = AIPlaceGrid.PressStep(s, ax, ay, sx, sy, stx, sty, bx, by, true, t);
                if (o.fail != null) { r.failed = true; r.why = o.fail; r.t = t; r.x = bx; r.y = by; return r; }
                if (o.press) { r.pressed = true; r.t = t; r.x = bx; r.y = by; r.dist = s.pressDist; r.frames = k; return r; }
                mx = o.mx; my = o.my;
            }
            if (smooth) sb.Step(mx, my, 0f, 0f, false, false); else b.Frame1(t, mx, my);
        }
        r.x = smooth ? sb.x : b.x; r.y = smooth ? sb.y : b.y;
        return r;
    }

    static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    static bool InWindow(PlaceRun r) => r.pressed && r.dist >= 0.85f && r.dist <= AIPlaceGrid.PressReach + 1e-4f;

    // ------------------------------------------------------------------ 1. the door re-hang from the north

    static void Place()
    {
        // 22-짧은확인 t=615.0: inside (-4.00,-3.37), the door (-4,-6), the side stand (-4,-5) — walked south, stopped at
        // (-4.00,-4.78) 1.22 off the tile, swung -4.78 / -5.06 / -5.34 and failed after 1.6 s (twice: t=616.6, 769.4)
        var old = Old(-4f, -3.3714f, -4, -6, -4, -5);
        Program.Check(old.failed && !old.pressed && old.why == "설 칸에 왔지만 닿지 않음",
            $"M1a. (as a52384f) from the north the press never came: '{old.why}' after {old.t:F1} s at (-4.00,{old.y:F2}) (logged: (-4.00,-4.78), 1.6 s)");
        var now = New(-4f, -3.3714f, -4, -6, 1, 1, -4, -5);
        Program.Check(InWindow(now) && now.t < 2f && Math.Abs(now.x + 4f) < 0.35f,
            $"M1b. now: the body stands still on the stand side and presses after {now.t:F2} s from (-4.00,{now.y:F2}), {now.dist:F2} off the tile");
        // every phase of the 0.28 steps (where the walk began), from the north, the south (the door tile side, t=788.8),
        // the side; the smooth body of RemeasureTests too
        int ok = 0, n = 0; float worst = 0f;
        for (int i = 0; i < 14; i++)
        {
            float o = i * 0.02f;
            foreach (var r in new[] { New(-4f, -3.0f - o, -4, -6, 1, 1, -4, -5), New(-4f, -6.9f + o, -4, -6, 1, 1, -4, -5), New(-6.5f - o, -5f, -4, -6, 1, 1, -4, -5), New(-4f, -3.0f - o, -4, -6, 1, 1, -4, -5, true) })
            {
                n++; if (InWindow(r) && r.t < AIPlaceGrid.StandTimeout) { ok++; worst = Math.Max(worst, r.t); }
            }
        }
        Program.Check(ok == n, $"M1c. {ok}/{n} walks to the stand (14 start phases x north, south, side, smooth body) press standing still in the window; the slowest after {worst:F2} s");
        // the perpendicular walks that worked before: the repair (2,1) from the pad (0,0) to the side stand (2,0)
        // (t=567.0, 0.6 s), a 2x2 chest from its side; a stand blocked off (the body held back: no press, a fact)
        var rep = New(0f, 0f, 2, 1, 1, 1, 2, 0);
        var chest = New(5f, 0.5f, 0, 0, 2, 2, 2, 0);
        Program.Check(InWindow(rep) && rep.t < 1.5f && InWindow(chest), $"M1d. the repair (2,1) from (0,0): press after {rep.t:F2} s ({rep.dist:F2} off); a 2x2 from its side ({chest.dist:F2} off)");
        var blk = new PlacePressState(); PlacePressOut bo = default; float bt = 0f;
        for (int k = 0; k < 200 && bo.fail == null; k++, bt += TickBody.Frame) bo = AIPlaceGrid.PressStep(blk, -4, -6, 1, 1, -4, -5, -4f, -4.4f, true, bt);   // pressed against something at 1.6
        Program.Check(bo.fail == null && !bo.press && bo.observation.Contains("멈춰 서지 못함") && bt > AIPlaceGrid.StandTimeout, $"M1e. a body held off the stand: keeps settling and reports after {bt:F1} s '{bo.observation}'");

        // the grave: (-4.1,-6.9) next to the door tile (-4,-6), refused 3 presses from 0.94 (t=716.1); placed once picked up
        Program.Check(AIPlaceGrid.OverlapsPlaceBox(-4, -6, -4.6f, -7.4f, -3.6f, -6.4f) && !AIPlaceGrid.OverlapsPlaceBox(-4, -6, -4.6f, -7.5f, -3.6f, -6.46f)
            && !AIPlaceGrid.OverlapsPlaceBox(-5, -6, -4.5f, -6.5f, -3.5f, -5.5f),
            "M1f. the 0.9 box: a grave body reaching 0.05 into the door tile's box overlaps it; one short of it does not; a hung door's own tile does not overlap its side tile");
        string ov = AIPlaceGrid.OverlapNote("PlayerGrave", -4.1f, -6.9f, -4, -6);
        Program.Check(ov.Contains("PlayerGrave(-4.1,-6.9)") && ov.Contains("0.9칸 상자") && ov.Contains("치워야"), $"M1g. the fact: '{ov}'");
        // D5: a tile with a diagonal stand only — no stand, and the diagonal said
        int nn = 9; var g = new PlaceGridInput { n = nn, loX = -4, loY = -4, pass = new bool[nn * nn], meX = 2, meY = 2, meFx = 2f, meFy = 2f };
        foreach (var t in new[] { (2, 2), (1, 1), (1, 2), (2, 1) }) g.pass[(t.Item1 + 4) * nn + (t.Item2 + 4)] = true;   // (0,0) to place: (1,1) its diagonal
        g.pass[(1 + 4) * nn + (0 + 4)] = false; g.pass[(0 + 4) * nn + (1 + 4)] = false;
        bool side = AIPlaceGrid.StandFor(g, 0, 0, 1, 1, null, null, out _, out _);
        bool diag = AIPlaceGrid.DiagonalStand(g, 0, 0, 1, 1, null, out int dx, out int dy);
        Program.Check(!side && diag && dx == 1 && dy == 1 && AIPlaceGrid.DiagonalLeftOutNote(dx, dy).Contains("대각 칸 (1,1)은 갈 수 있으나"),
            $"M1h. (검수 D5) a diagonal stand only: no stand, and the fact says the diagonal (1,1) is left out by the rule: '{AIPlaceGrid.DiagonalLeftOutNote(dx, dy)}'");
    }

    // ------------------------------------------------------------------ C2: the press not sent

    static void Press()
    {
        // N5a kept: a press held 1.4 s (Release) then sent once
        var q = new PressQueue(); q.Ask(1, 0f);
        int bits = 0; bool f; uint tick = 1;
        for (float t = 0f; t < 2f; t += 0.05f, tick++) if (q.Bit(tick, t >= 1.4f, out f)) bits++;
        Program.Check(bits == 1 && q.done == 1 && q.lastDropId == 0, $"M2a. a press waiting 1.4 s (Release) is sent once, not dropped ({bits} tick)");
        // review C2 scenario 1: the door step's press waits (Release), the model picks a retreat: the skill ends.
        // As a52384f nothing dropped it and it went out when Release ended; now the end drops it
        var oldQ = new PressQueue(); oldQ.Ask(1, 0f); tick = 1; int oldBits = 0;
        for (float t = 0f; t < 2f; t += 0.05f, tick++) if (oldQ.Bit(tick, t >= 1.2f, out f)) oldBits++;
        var nq = new PressQueue(); nq.Ask(1, 0f); tick = 1; int newBits = 0; string line = null;
        for (float t = 0f; t < 2f; t += 0.05f, tick++) { if (Math.Abs(t - 0.5f) < 0.01f) line = nq.Drop("기술 공격 끝남", t); if (nq.Bit(tick, t >= 1.2f, out f)) newBits++; }
        Program.Check(oldBits == 1 && newBits == 0 && line != null && line.Contains("dropped unsent") && nq.DroppedWhy(1).Contains("버림"),
            $"M2b. a press left by an ended skill: (as a52384f) sent later at another place ({oldBits}); now dropped at the end, never sent ('{line}')");
        // scenario 3: a state that never takes a press (guest world, riding) — the first press blocked every later one
        var bq = new PressQueue(); bq.Ask(1, 0f);
        string cap = null; float tc = 0f; for (; tc < 4f && cap == null; tc += 0.1f) cap = bq.CheckWait(tc, "state=VehicleRiding");
        bool next = bq.Ask(2, tc);
        Program.Check(cap != null && tc > PressQueue.WaitMax && tc < PressQueue.WaitMax + 0.2f && next && !new PressQueue { asked = 1, done = 0 }.Ask(2, 0f),
            $"M2c. a press no state takes: dropped after {tc:F1} s with the state ('{cap}'), and the next press is taken (it was blocked behind it)");
        var sending = new PressQueue(); sending.Ask(1, 0f); sending.Bit(7, true, out f);
        Program.Check(sending.Drop("새 기술", 0.1f) == null && sending.Bit(7, true, out f) && !sending.Bit(8, true, out f) && sending.done == 1, "M2d. a press already on its server tick is not dropped (it finishes)");
        // the door step learns it: not a press, the fail says so
        var s = new DoorPassState(); bool pressDone = true; int req = 0; var dq = new PressQueue(); float tt = 0f; uint tk = 1;
        for (int k = 0; k < 400 && s.phase != DoorPhase.Failed && s.phase != DoorPhase.Done; k++, tt += 1f / 30f, tk++)
        {
            AIDoorPass.PressDropped(s, dq.DroppedWhy(req), tt);
            var o = AIDoorPass.Step(s, DoorKind.Manual, -4f, -6f, 0f, -1f, -4f, -5.37f, false, pressDone, tt);
            if (o.press) { req = dq.asked + 1; dq.Ask(req, tt); }
            dq.CheckWait(tt, "state=Release"); dq.Bit(tk, false, out f);
            pressDone = req == 0 || dq.done >= req;
        }
        var waitingDoor = AIDoorPass.Step(s, DoorKind.Manual, -4f, -6f, 0f, -1f, -4f, -5.37f, false, false, tt);
        Program.Check(s.phase == DoorPhase.Open && s.fail == "" && s.dropped.Contains("버림") && waitingDoor.observation.Contains("안 보내짐"), $"M2e. dropped presses remain facts while the door step waits: '{waitingDoor.observation}', '{s.dropped}'");
    }

    // ------------------------------------------------------------------ C1 / D6: the door from the object key

    static void Breaker()
    {
        // review C1: an object (door) damage, no tile damage — as a52384f "타일 피해 없음" (no breaker); now a door breaker
        var doorOnly = new TileBreak { known = true, chase = true, bump = 0, obj = 3, melee = 0, charge = 0 };
        string dn = AIBaseGuard.BreakerNote("X", doorOnly);
        var g = GuardGrid.Make(-16, -18, 25, GuardCell.Closed);
        for (int x = -6; x <= -2; x++) for (int y = -5; y <= -1; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        for (int x = -12; x <= 4; x++) for (int y = -14; y <= -7; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        g.cell[g.Idx(-4, -6)] = GuardCell.OwnDoor;
        var r = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (-4, -5)), (-4, -7)); r.tileBreak = doorOnly; r.breaker = AIBaseGuard.IsBreaker("X", doorOnly);
        Program.Check(r.breaker && dn.Contains("문을 부수는 종류") && dn.Contains("피해 3로 때림") && AIBaseGuard.IsThreat(r, 1.4f, 8f),
            $"M3a. object damage 3, tile 0: a door breaker, a threat past the shut door 1.4 off ('{dn}')");
        var wallOnly = new TileBreak { known = true, chase = true, bump = 198, obj = 0, melee = 0, charge = 0 };   // Caveling's bump (22 t=330.8), its object key 0 here
        string wn = AIBaseGuard.BreakerNote("Y", wallOnly);
        Program.Check(AIBaseGuard.IsBreaker("Y", wallOnly) && wn.Contains("문은 직접 안 때림") && !wn.Contains("문은 체력 2라 때림"),
            $"M3b. tile damage 198, object 0: walls yes; the door not said hit (its side walls: it falls) ('{wn}')");
        var unread = new TileBreak { known = true, chase = true, bump = 0, obj = null, melee = 0, charge = 0 };
        string un = AIBaseGuard.BreakerNote("Z", unread);
        string zn = AIBaseGuard.BreakerNote("Z", new TileBreak { known = true, chase = true, bump = 0, obj = 0, melee = 0, charge = 0 });
        string nc = AIBaseGuard.BreakerNote("Z", new TileBreak { known = true, chase = false, bump = 0, obj = 0, melee = 0, charge = 0 });
        Program.Check(un.Contains("못 읽음(모름): 부딪힌 물체(문) 피해") && !un.Contains("없음(") && zn.Contains("벽·문 피해 없음") && zn.Contains("부딪힌 물체(문) 피해 0")
            && nc.Contains("쫓다가 부딪힌 것 때리기 없음") && !zn.Contains("약한 벽은 때릴 수 있음"),
            $"M3c. (D6) only the keys read are said: unread '{un}'; all zero '{zn}'; no chase state '{nc}'");
        Program.Check(new TileBreak { known = true, chase = true, bump = 198, obj = null, melee = 0, charge = 0 }.Log == "bump 198 obj ? melee 0 charge 0", "M3d. the log line shows a key not read as '?'");
    }

    // ------------------------------------------------------------------ D1, 6

    static void Small()
    {
        // D1: a door step that failed holds no door — the close option of that door is back at once (AIBody.DoorStepOn)
        Program.Check(!AIDoorPass.HoldsDoor(new DoorPassState { phase = DoorPhase.Failed }) && AIDoorPass.HoldsDoor(new DoorPassState { phase = DoorPhase.Pass }),
            "M4a. (D1) a failed door step holds no door; a passing one does");
        // 6: 22-짧은확인 t=1047.5 stand-up 105/100 -> 100 (the bonus max gone): no hit; a real hit still is
        Program.Check(AIBaseGuard.HurtAmount(105, 100, 100) == 0 && !AIBaseGuard.HurtBy(105, 105, 100, 100) && AIBaseGuard.HurtAmount(105, 100, 105) == 5 && AIBaseGuard.HurtAmount(100, 80, 100) == 20 && AIBaseGuard.HurtAmount(80, 90, 100) == 0,
            "M5a. health above the new max going with it is no hit (105 -> 100 at max 100: 0); a loss under the max is (105 -> 100 at max 105: 5; 100 -> 80: 20)");
    }
}
