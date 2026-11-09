// G3d — the door step stands still on its press spot from any start (25-문단계멈춰서기.md; the fact is 23-합치기전수정.md
// 0절 5: `arena go -4 -9` put the body on (-4,-9.00), the 0.28 steps reach the door's near side only at 0.47 and 0.75 off
// its centre, the press window 0.55-0.70 got none, and the walk in failed 1 of 4 in 22-짧은확인 t=318-324).
// 검수 반영 (26-문단계멈춰서기-검수 M1, L1-L3): the body is CmdBody — the game's input as the movetrace shows it: each
// frame writes the input of its server tick (the last frame of a tick wins), tick N moves the body by the input of tick
// N-2 (a tick with none takes the last older one), a tick's move shows on the frame that has it; with the mod's input
// sender in front of it (TickNudge: a correction for one tick, the next two written without a move). Frames 20 / 30 /
// 60 a second, with or without jitter; steps 0.2808 a tick (x1.3 a speed buff, x0.8 a slowdown).
using System;
using System.Collections.Generic;

public static class DoorSettleTests
{
    public static void Run()
    {
        Repro(); Starts(); Variants(); Other(); Place();
    }

    internal sealed class DoorRun
    {
        public SettleState s; public bool shut, done, opened; public float t, openT = -1f; public string why = "", trace = "";
        public readonly List<float> presses = new List<float>();
        public readonly List<float> along = new List<float>();
        public int overs, afterPulse, afterOver, direct, nudges, maxNudgeTicks;
    }

    // ------------------------------------------------------------------ the body

    // The game's input buffer and the body: Input(t, ...) writes the frame's tick; Advance(t) runs the ticks up to t.
    internal sealed class CmdBody
    {
        public float x, y; public float step = MergeFixTests.TickBody.Step; float _p; long _n;
        readonly Dictionary<long, (float mx, float my, int nudge)> _cmd = new Dictionary<long, (float, float, int)>();
        public readonly Dictionary<int, int> nudgeTicks = new Dictionary<int, int>();   // ticks each nudge moved the body
        public CmdBody(float x0, float y0, float phase) { x = x0; y = y0; _p = phase; _n = 1; }
        public long Tick(float t) => (long)Math.Floor((t - _p) / MergeFixTests.TickBody.TickDt + 1e-4);
        public void Advance(float t)
        {
            while (_p + _n * MergeFixTests.TickBody.TickDt <= t + 1e-5f)
            {
                (float mx, float my, int nudge) c = (0f, 0f, 0);
                for (long k = _n - 2; k >= _n - 400; k--) if (_cmd.TryGetValue(k, out c)) break;
                x += c.mx * step; y += c.my * step;
                if (c.nudge != 0 && (c.mx != 0f || c.my != 0f)) { nudgeTicks.TryGetValue(c.nudge, out int m); nudgeTicks[c.nudge] = m + 1; }
                _n++;
            }
        }
        public void Input(float t, float mx, float my, int nudge, bool zeroAhead)
        {
            float l = (float)Math.Sqrt(mx * mx + my * my);
            if (l > 1e-4f) { mx /= l; my /= l; } else { mx = my = 0f; }
            long k = Tick(t);
            _cmd[k] = (mx, my, nudge);
            if (zeroAhead) { _cmd[k + 1] = (0f, 0f, 0); _cmd[k + 2] = (0f, 0f, 0); }
        }
        public int MaxNudgeTicks() { int m = 0; foreach (var v in nudgeTicks.Values) m = Math.Max(m, v); return m; }
    }

    internal enum BodyKind { Cmd, CmdNoNudge, Tick, Smooth }

    // the body for a run; the frames' times (fps, jitter: each frame +-jitter of its length, seeded)
    sealed class Bod
    {
        readonly CmdBody _c; readonly MergeFixTests.TickBody _b; readonly RemeasureTests.Body _sb; readonly BodyKind _k;
        readonly TickNudge _nudger = new TickNudge();
        float _doorX, _doorY; bool _ew;
        public Bod(float x, float y, float doorX, float doorY, bool ew, BodyKind k, float phase, float speed)
        {
            _k = k; _doorX = doorX; _doorY = doorY; _ew = ew;
            _c = new CmdBody(x, y, phase) { step = MergeFixTests.TickBody.Step * speed };
            _b = new MergeFixTests.TickBody { x = x, y = y }.Phase(phase); _sb = new RemeasureTests.Body { x = x, y = y };
        }
        public CmdBody Cmd => _c;
        public float X => _k == BodyKind.Smooth ? _sb.x : _k == BodyKind.Tick ? _b.x : _c.x;
        public float Y => _k == BodyKind.Smooth ? _sb.y : _k == BodyKind.Tick ? _b.y : _c.y;
        public void Before(float t, bool shut) { if (_k == BodyKind.Cmd || _k == BodyKind.CmdNoNudge) { _c.Advance(t); Clamp(shut); } }
        public void After(float t, float mx, float my, int nudge, bool shut)
        {
            if (_k == BodyKind.Smooth) { _sb.Step(mx, my, _doorX, _doorY, _ew, shut); return; }
            if (_k == BodyKind.Tick) { _b.Frame1(t, mx, my); Clamp(shut); return; }
            bool ahead = false;
            if (_k == BodyKind.Cmd) _nudger.Move(nudge, ref mx, ref my, (uint)Math.Max(0, _c.Tick(t) + 1000), out ahead);
            _c.Input(t, mx, my, _k == BodyKind.Cmd ? _nudger.id * (_nudger.on ? 1 : 0) : nudge, ahead);
        }
        // a shut door holds the body centre 0.35 off its centre (RemeasureTests.Body)
        void Clamp(bool shut)
        {
            if (!shut) return;
            ref float ax = ref _c.x, ay = ref _c.y;
            if (_k == BodyKind.Tick) { ax = ref _b.x; ay = ref _b.y; }
            float along = _ew ? ax - _doorX : ay - _doorY, side = _ew ? ay - _doorY : ax - _doorX;
            if (Math.Abs(side) < 0.6f && Math.Abs(along) < 0.35f) { float sg = along >= 0f ? 0.35f : -0.35f; if (_ew) ax = _doorX + sg; else ay = _doorY + sg; }
        }
    }

    internal sealed class Opt
    {
        public BodyKind body = BodyKind.Cmd; public float phase, fps = 30f, jitter, speed = 1f; public int seed = 1;
    }
    static float NextFrame(Random rng, Opt o) => (1f / o.fps) * (1f + (o.jitter > 0f ? (float)(rng.NextDouble() * 2.0 - 1.0) * o.jitter : 0f));

    // A full door pass (open -> through -> close) from (x0, y0); the door (doorX, doorY); dir = from the start side through
    // it. The game takes a press 0.53-0.90 off the centre looking at the door (RemeasureTests.Taken).
    internal static DoorRun Pass(float x0, float y0, float doorX, float doorY, float dirX, float dirY, Opt opt = null)
    {
        opt = opt ?? new Opt();
        var s = new DoorPassState(); var r = new DoorRun { s = s };
        var b = new Bod(x0, y0, doorX, doorY, Math.Abs(dirX) > 0.5f, opt.body, opt.phase, opt.speed);
        var rng = new Random(opt.seed);
        bool open = false, pressDone = true; float t = 0f;
        int lastMisses = 0, lastOvers = 0; DoorPhase lastPhase = s.phase; char lastEv = 'n';
        while (t < 20f && s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed)
        {
            b.Before(t, !open);
            float bx = b.X, by = b.Y;
            var o = AIDoorPass.Step(s, DoorKind.Manual, doorX, doorY, dirX, dirY, bx, by, open, pressDone, t);
            if (s.phase != lastPhase) { lastPhase = s.phase; lastMisses = 0; lastOvers = 0; lastEv = 'n'; }
            if (s.overs > lastOvers) { r.overs += s.overs - lastOvers; if (s.misses == lastMisses) lastEv = 'o'; }
            if (s.misses > lastMisses) lastEv = 'p';
            lastMisses = s.misses; lastOvers = s.overs;
            if (!pressDone) pressDone = true;
            if (o.press)
            {
                pressDone = false;
                r.presses.Add(Dist(bx, by, doorX, doorY));
                float al = Math.Abs((bx - doorX) * dirX + (by - doorY) * dirY); r.along.Add(al);
                if (lastEv == 'p') r.afterPulse++; else if (lastEv == 'o') r.afterOver++; else r.direct++;
                if (RemeasureTests.Taken(bx, by, o.fx, o.fy, doorX, doorY)) { open = !open; if (open && r.openT < 0f) { r.openT = t; r.opened = true; } }
            }
            b.After(t, o.mx, o.my, o.nudge, !open);
            t += NextFrame(rng, opt);
        }
        r.t = t; r.shut = !open; r.done = s.phase == DoorPhase.Done; r.why = s.fail; r.trace = s.trace;
        r.nudges = b.Cmd.nudgeTicks.Count; r.maxNudgeTicks = b.Cmd.MaxNudgeTicks();
        return r;
    }

    // ------------------------------------------------------------------ 0927753, kept in the test (검수 L1: its own values)

    sealed class OldState : SettleState
    {
        public string trace = "";
        public OldState() { pulse = OldPulseStart; }
        public override void Note(float now, string what) { if (trace.Length < 700) trace += $"{now:F2}s {what}; "; }
    }
    const float OldPulseStart = 0.05f, OldPulseMin = 0.02f, OldPulseMax = 0.25f, OldNearSpot = 0.35f;
    static bool OldWindow(float along, float side, bool wide)
    {
        float d = (float)Math.Sqrt(along * along + side * side);
        return along > 0f && Math.Abs(side) < 0.30f && d >= 0.55f && d <= (wide ? 0.85f : 0.70f);
    }

    // As 0927753 (AIDoorPass.Step's Approach and Open with AISettle.Hold before this change): the press only standing
    // still in the window (0.55-0.70; 0.85 after two corrections that flipped side); near the spot the correction pulses
    // run straight at the spot, the direction taken again every frame; a pulse that carries the body out past NearSpot
    // is walked back and not counted. Ends at the open (or its 6 s / the whole 12 s). The body: the tick body in the
    // movetrace order (0927753 has no one-tick correction).
    static DoorRun OldOpen(float x0, float y0, float doorX, float doorY, float dirX, float dirY, float phase)
    {
        var s = new OldState(); var r = new DoorRun { s = s };
        var b = new MergeFixTests.TickBody { x = x0, y = y0 }.Phase(phase);
        bool approach = true; float t = 0f, dt = 1f / 30f, stepAt = 0f;
        for (int k = 0; t < 12f; k++, t = k * dt)
        {
            b.Advance(t);
            if (Math.Abs(b.x - doorX) < 0.6f && Math.Abs(b.y - doorY) < 0.35f) b.y = b.y >= doorY ? doorY + 0.35f : doorY - 0.35f;
            float mx = 0f, my = 0f, bx = b.x, by = b.y;
            OldTrack(s, bx, by, t);
            float along = -((bx - doorX) * dirX + (by - doorY) * dirY), side = -((bx - doorX) * -dirY + (by - doorY) * dirX);
            float sx = doorX - dirX * 0.63f, sy = doorY - dirY * 0.63f;
            if (approach && (OldWindow(along, side, false) || Dist(bx, by, sx, sy) <= OldNearSpot + 0.15f)) { approach = false; stepAt = t; s.Note(t, "열기"); }
            if (approach) OldGo(s, ref mx, ref my, bx, by, sx, sy);
            else
            {
                if (t - stepAt > 6f) { r.why = $"문 열기 6초 넘음(누름 0번, 문 중심에서 {Dist(bx, by, doorX, doorY):F2}칸)"; break; }
                if (OldHold(s, ref mx, ref my, OldWindow(along, side, s.overs >= 2), sx, sy, -dirX, -dirY, doorX, doorY, bx, by, t))
                {
                    r.presses.Add(Dist(bx, by, doorX, doorY));
                    r.opened = RemeasureTests.Taken(bx, by, doorX - bx, doorY - by, doorX, doorY); r.openT = t; break;
                }
            }
            b.Input(t, mx, my);
        }
        r.t = t; r.trace = s.trace;
        return r;
    }

    // 0927753's Track (the velocity between two frames) and Go, as they were
    static void OldTrack(SettleState s, float meX, float meY, float now)
    {
        if (s.lastT >= 0f && now > s.lastT) { float dt = now - s.lastT; s.vx = (meX - s.lastX) / dt; s.vy = (meY - s.lastY) / dt; }
        if (s.lastT < 0f || now > s.lastT) { s.lastX = meX; s.lastY = meY; s.lastT = now; }
        if (s.stillSince < 0f || Dist(meX, meY, s.stillX, s.stillY) > 0.03f) { s.stillX = meX; s.stillY = meY; s.stillSince = now; }
    }
    static bool OldStill(SettleState s, float now) => s.stillSince >= 0f && now - s.stillSince >= 0.12f;
    static void OldGo(SettleState s, ref float mx, ref float my, float meX, float meY, float tx, float ty)
    {
        float dx = tx - meX, dy = ty - meY, l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l < 0.06f) return;
        float ux = dx / l, uy = dy / l, vToward = s.vx * ux + s.vy * uy;
        if (vToward > 0f && vToward * 0.075f >= l - 0.03f) return;
        mx = ux; my = uy;
    }

    static bool OldHold(SettleState s, ref float mx, ref float my, bool inWindow, float sx, float sy, float ax, float ay, float cx, float cy, float meX, float meY, float now)
    {
        bool still = OldStill(s, now);
        if (inWindow) return still;
        float off = Dist(meX, meY, sx, sy);
        if (now < s.pulseUntil) { Toward(ref mx, ref my, meX, meY, sx, sy); return false; }
        if (!still || off > OldNearSpot)
        {
            if (off > OldNearSpot) OldGo(s, ref mx, ref my, meX, meY, sx, sy);
            return false;
        }
        int sign = (meX - sx) * ax + (meY - sy) * ay > 0f ? 1 : -1;
        if (s.missSign != 0) s.pulse = sign == s.missSign ? Math.Min(OldPulseMax, s.pulse * 1.6f) : Math.Max(OldPulseMin, s.pulse * 0.5f);
        if (s.missSign != 0 && sign != s.missSign) s.overs++;
        s.missSign = sign; s.misses++;
        s.pulseUntil = now + s.pulse;
        s.stillSince = -1f;
        s.Note(now, $"pulse {s.pulse:F2}s {(sign > 0 ? "in" : "out")} @{Dist(meX, meY, cx, cy):F2}");
        Toward(ref mx, ref my, meX, meY, sx, sy);
        return false;
    }
    static void Toward(ref float mx, ref float my, float meX, float meY, float tx, float ty)
    {
        float dx = tx - meX, dy = ty - meY, l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l < 0.06f) return;
        mx = dx / l; my = dy / l;
    }

    static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    static string L(List<float> l) => string.Join(" ", l.ConvertAll(v => v.ToString("F2")));
    // every press made from the press window (0.55 .. the widened far end 0.85; along the axis above 0.5)
    static bool InWin(DoorRun r)
    {
        foreach (var v in r.presses) if (v < AIDoorPass.PressMin - 1e-3f || v > AIDoorPass.PressMaxWide + 1e-3f) return false;
        foreach (var v in r.along) if (v <= AIDoorPass.AlongMin) return false;
        return r.presses.Count > 0;
    }
    static bool Ok(DoorRun r) => r.done && r.shut && InWin(r);
    const int Phases = 10;   // tick phases against the frames, 0.005 s apart (a tick is 0.05 s)
    static string Cut(string tr) => tr.Length > 260 ? tr.Substring(0, 260) + "…" : tr;

    // the tally of many runs
    sealed class Tally
    {
        public int ok, n, presses, wide, overs, afterPulse, afterOver, direct, nudges, maxTicks; public float worst, minAlong = 9f; public string bad = "";
        public void Add(DoorRun r, string what)
        {
            n++;
            if (Ok(r)) { ok++; worst = Math.Max(worst, r.t); } else if (bad == "") bad = $" BAD {what}: presses {L(r.presses)} '{r.why}' {Cut(r.trace)}";
            foreach (var v in r.presses) { presses++; if (v > AIDoorPass.PressMax + 1e-3f) wide++; }
            foreach (var v in r.along) minAlong = Math.Min(minAlong, v);
            overs += r.overs; afterPulse += r.afterPulse; afterOver += r.afterOver; direct += r.direct; nudges += r.nudges; maxTicks = Math.Max(maxTicks, r.maxNudgeTicks);
        }
        public bool All => ok == n;
        public string Say => $"{ok}/{n}, the slowest {worst:F2} s; presses {presses} ({wide} widened past 0.70, along >= {minAlong:F2}): right after a correction {afterPulse}, after a jump over and the walk back {afterOver}, without a correction {direct}; jumps over {overs}; corrections {nudges}, at most {maxTicks} tick each{bad}";
    }

    // ------------------------------------------------------------------ the logged failure

    static void Repro()
    {
        // 22-짧은확인 t=318.1-324.5: `arena go -4 -9`, then the walk in through the door (-4,-6) (outside south, passage
        // north): 0 presses in 6 s, only "pulse … out @0.47 / @0.35", the body between 0.35 and 0.75 off the centre
        int oldFail = 0, oldPattern = 0; string oldEx = "";
        var now = new Tally(); string ex = "";
        for (int ph = 0; ph < Phases; ph++)
        {
            var o = OldOpen(-4f, -9f, -4f, -6f, 0f, 1f, ph * 0.005f);
            if (!o.opened)
            {
                oldFail++;
                if (o.presses.Count == 0 && o.trace.Contains("out @0.47") && !o.trace.Contains(" in @")) { oldPattern++; if (oldEx == "") oldEx = $"phase {ph}: '{o.why}' — {Cut(o.trace)}"; }
            }
            var n = Pass(-4f, -9f, -4f, -6f, 0f, 1f, new Opt { phase = ph * 0.005f });
            now.Add(n, $"phase {ph}"); if (ex == "") ex = Cut(n.trace);
        }
        Program.Check(oldFail > 0 && oldPattern == oldFail,
            $"D1a. (as 0927753) from (-4,-9.00) the open fails in {oldFail}/{Phases} tick phases, each as logged (no press, corrections only 'out' from 0.47 / 0.35): {oldEx}");
        Program.Check(now.All && now.overs == 0 && now.afterOver == 0, $"D1b. now from (-4,-9.00), open, pass and close: {now.Say}; e.g. {ex}");
    }

    // ------------------------------------------------------------------ every start phase x north, south, side

    static (float x, float y, float dy) Start(int d, float o) => d == 2 ? (-6.5f - o, -7f, 1f) : d == 1 ? (-4f, -3f + o, -1f) : (-4f, -9f - o, 1f);

    static void Starts()
    {
        // 14 start offsets 0.02 apart (a 0.28 step's every phase) x tick phases, from the south (outside, the walk in),
        // the north (inside, the walk out), the side (outside, along the wall from the west)
        var names = new[] { "south", "north", "side" };
        var all = new Tally(); int oldFail = 0, oldN = 0;
        for (int i = 0; i < 14; i++)
            for (int ph = 0; ph < Phases; ph++)
                for (int d = 0; d < 3; d++)
                {
                    var st = Start(d, i * 0.02f);
                    all.Add(Pass(st.x, st.y, -4f, -6f, 0f, st.dy, new Opt { phase = ph * 0.005f }), $"{names[d]}+{i * 0.02f:F2} ph{ph}");
                    oldN++; if (!OldOpen(st.x, st.y, -4f, -6f, 0f, st.dy, ph * 0.005f).opened) oldFail++;
                }
        Program.Check(all.All && all.wide == 0 && all.overs == 0,
            $"D2a. door passes, 14 start offsets x {Phases} tick phases x south / north / side: {all.Say} (as 0927753 the open failed in {oldFail}/{oldN})");
        // an east-west door (passage along x), from the west and the east
        var ew = new Tally();
        for (int i = 0; i < 14; i++)
            for (int ph = 0; ph < Phases; ph += 3)
            {
                ew.Add(Pass(-3f - i * 0.02f, 2f, 0f, 2f, 1f, 0f, new Opt { phase = ph * 0.005f }), $"west+{i * 0.02f:F2} ph{ph}");
                ew.Add(Pass(3f + i * 0.02f, 2f, 0f, 2f, -1f, 0f, new Opt { phase = ph * 0.005f }), $"east+{i * 0.02f:F2} ph{ph}");
            }
        Program.Check(ew.All && ew.overs == 0, $"D2b. a door with its passage east-west, from the west and the east: {ew.Say}");
    }

    // ------------------------------------------------------------------ frame rates, jitter, speed, other bodies

    static Tally Sweep(Func<int, float, int, Opt> opt, int every = 2)
    {
        var t = new Tally();
        for (int i = 0; i < 14; i += every)
            for (int ph = 0; ph < Phases; ph += every)
                for (int d = 0; d < 3; d++)
                {
                    var st = Start(d, i * 0.02f); var o = opt(i, ph * 0.005f, d);
                    t.Add(Pass(st.x, st.y, -4f, -6f, 0f, st.dy, o), $"{d}+{i * 0.02f:F2} ph{ph} {o.fps}fps j{o.jitter} x{o.speed} {o.body}");
                }
        return t;
    }

    static void Variants()
    {
        // 검수 M1: one correction = one server tick at any frame rate (20 / 30 / 60, with jitter +-30% of a frame) and phase
        var lines = new List<string>(); bool fpsOk = true;
        foreach (var fps in new[] { 20f, 30f, 60f })
            foreach (var j in new[] { 0f, 0.3f })
            {
                var t = Sweep((i, ph, d) => new Opt { fps = fps, jitter = j, phase = ph, seed = 7 + i * 31 + d });
                lines.Add($"{fps} fps{(j > 0f ? " jitter" : "")} {t.ok}/{t.n} (jumps {t.overs}, right after a correction {t.afterPulse}/{t.presses}, widened {t.wide}, max {t.maxTicks} tick){(t.bad != "" ? t.bad : "")}");
                fpsOk &= t.All && t.maxTicks == 1 && t.wide == 0;
            }
        Program.Check(fpsOk, $"D5a. frame rates: {string.Join("; ", lines)}");
        // 검수 L2: a speed buff (+30%) and a slowdown (-20%): the step a correction moved is measured and used
        var fast = Sweep((i, ph, d) => new Opt { speed = 1.3f, phase = ph }, 1);
        var slow = Sweep((i, ph, d) => new Opt { speed = 0.8f, phase = ph }, 1);
        Program.Check(fast.All && slow.All && fast.wide == 0 && slow.wide == 0, $"D5b. steps x1.3: {fast.Say} | x0.8: {slow.Say}");
        // a body without the one-tick correction (the time pulses alone; the tick order of MergeFixTests; the sliding body)
        var noN = Sweep((i, ph, d) => new Opt { body = BodyKind.CmdNoNudge, phase = ph });
        var tick = Sweep((i, ph, d) => new Opt { body = BodyKind.Tick, phase = ph });
        var smooth = Sweep((i, ph, d) => new Opt { body = BodyKind.Smooth, phase = ph }, 7);
        Program.Check(noN.All && tick.All && smooth.All, $"D5c. without the one-tick sender (time pulses only): {noN.ok}/{noN.n} (jumps {noN.overs}, widened {noN.wide}); MergeFixTests order {tick.ok}/{tick.n}; sliding body {smooth.ok}/{smooth.n}{noN.bad}{tick.bad}{smooth.bad}");
    }

    // ------------------------------------------------------------------ the parts

    static void Other()
    {
        // a correction is aimed at a step that ends in the window: from 0.47 (too close) and 0.75 (too far) straight on
        // jumps over 0.55-0.70, so the aim is off the axis, its single step in the window
        Func<float, float, bool> win = (x, y) => AIDoorPass.InPressWindow(x, y);
        bool a1 = AISettle.Aim(win, 0.47f, 0f, AIDoorPass.PressAim, 0f, out float ux1, out float uy1);
        bool a2 = AISettle.Aim(win, 0.75f, 0f, AIDoorPass.PressAim, 0f, out float ux2, out float uy2);
        float q1x = 0.47f + ux1 * AISettle.StepLen, q1y = uy1 * AISettle.StepLen, q2x = 0.75f + ux2 * AISettle.StepLen, q2y = uy2 * AISettle.StepLen;
        Program.Check(a1 && a2 && win(q1x, q1y) && win(q2x, q2y) && Math.Abs(uy1) > 0.3f && Math.Abs(uy2) > 0.3f && ux1 > 0f && ux2 < 0f
            && !win(0.47f + AISettle.StepLen, 0f) && !win(0.75f - AISettle.StepLen, 0f),
            $"D3a. the aim: from 0.47 ({ux1:F2},{uy1:F2}) to ({q1x:F2},{q1y:F2}) {Dist(q1x, q1y, 0f, 0f):F2} off; from 0.75 ({ux2:F2},{uy2:F2}) to ({q2x:F2},{q2y:F2}) {Dist(q2x, q2y, 0f, 0f):F2} off (straight on: 0.75 / 0.47, out)");
        // nowhere a step reaches the window (held 1.5 off): straight at the spot, as before
        bool a3 = AISettle.Aim(win, 1.5f, 0f, AIDoorPass.PressAim, 0f, out float ux3, out float uy3);
        Program.Check(!a3 && ux3 < -0.99f, $"D3b. no step reaches the window: straight at the spot ({ux3:F2},{uy3:F2})");
        // a correction that carries the body out past NearSpot counts as a jump over the window (it was walked back
        // uncounted, 0.47 <-> 1.03, and the pulse only grew)
        var s = new DoorPassState { phase = DoorPhase.Open };
        float mx = 0f, my = 0f, t = 0f; int k;
        for (k = 0; k < 10 && s.misses == 0; k++, t += 1f / 30f) { AISettle.Track(s, -0.47f, 0f, t); mx = my = 0f; AISettle.Hold(s, ref mx, ref my, (x, y) => false, -0.63f, 0f, -1f, 0f, 0f, 0f, -0.47f, 0f, t); }
        bool pulsed = s.misses == 1 && s.pulsed;
        t = s.pulseAt + AISettle.NudgeWait;
        for (k = 0; k < 10; k++, t += 1f / 30f) { AISettle.Track(s, -1.03f, 0f, t); mx = my = 0f; AISettle.Hold(s, ref mx, ref my, (x, y) => false, -0.63f, 0f, -1f, 0f, 0f, 0f, -1.03f, 0f, t); }
        Program.Check(pulsed && s.overs == 1 && s.pulse < SettleState.PulseStart && mx > 0.9f && s.trace.Contains("over @1.03"),
            $"D3c. a correction from 0.47 that ends 1.03 off (out past NearSpot): counted over ({s.overs}), the next pulse shorter ({s.pulse:F3} s), the walk back ('{s.trace}')");
        // a body that cannot move from 0.80 never presses and is never widened (DoorReachReview S1a)
        var sh = new DoorPassState { phase = DoorPhase.Close }; int presses = 0; float t1 = 0f;
        for (int i = 0; i < 300 && sh.phase == DoorPhase.Close; i++, t1 += 1f / 30f)
            if (AIDoorPass.Step(sh, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.80f, 0f, true, true, t1).press) presses++;
        var waiting = AIDoorPass.Step(sh, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.80f, 0f, true, true, t1);
        Program.Check(presses == 0 && sh.overs == 0 && sh.phase == DoorPhase.Close && waiting.observation.Contains("문 닫기"), $"D3d. held at 0.80: reports the failed settling while keeping corrections and the press window ('{waiting.observation}')");
        // 검수 L3: off the axis the along part is kept above 0.5 (0.55 off at side 0.29 is along 0.47: out), widened too
        Program.Check(!AIDoorPass.InPressWindow(0.47f, 0.29f) && !AIDoorPass.InPressWindow(0.50f, 0.25f, true) && AIDoorPass.InPressWindow(0.52f, 0.25f) && AIDoorPass.InPressWindow(0.62f, 0f),
            "D3e. (검수 L3) the window keeps the along-axis part above 0.50 (0.47 / 0.29 out, 0.50 / 0.25 widened out; 0.52 / 0.25 in)");
        // 검수 M1: the sender — a nudge's move on every frame of its first tick, none after while asked, the next two
        // ticks written without a move; an ordinary move again once the step stops asking; a new id is a new nudge
        var n = new TickNudge(); float ax, ay; bool z1, z2, z3, z4;
        ax = 1f; ay = 0f; n.Move(5, ref ax, ref ay, 100, out z1); bool f1 = ax == 1f && z1;
        ax = 0.3f; ay = 0f; n.Move(5, ref ax, ref ay, 100, out z2); bool f2 = ax == 1f && z2;          // the same tick: still the nudge
        ax = 1f; ay = 0f; n.Move(5, ref ax, ref ay, 101, out z3); bool f3 = ax == 0f && !z3;          // the next tick: no move
        ax = 0f; ay = 1f; n.Move(0, ref ax, ref ay, 102, out z4); bool f4 = ay == 1f && !n.on;        // not asked: the step's own move
        ax = 0f; ay = -1f; n.Move(6, ref ax, ref ay, 103, out _); bool f5 = ay == -1f && n.tick == 103 && n.sent == 2;
        Program.Check(f1 && f2 && f3 && f4 && f5, "D3f. (검수 M1) TickNudge: the move on every frame of the first tick, none on the next while asked, the next two written without a move; the step's own move once not asked; a new id is a new nudge");
        // 검수 L2: the step a correction moved (one tick, standing before and after) is the step of the next aim
        var m = new DoorPassState { phase = DoorPhase.Open }; t = 0f;
        for (k = 0; k < 10 && m.misses == 0; k++, t += 1f / 30f) { AISettle.Track(m, -0.47f, 0f, t); mx = my = 0f; AISettle.Hold(m, ref mx, ref my, (x, y) => false, -0.63f, 0f, -1f, 0f, 0f, 0f, -0.47f, 0f, t); }
        float nx = -0.47f + m.pulseX * 0.365f, ny = m.pulseY * 0.365f;   // a +30% step
        for (t = m.pulseAt + AISettle.NudgeWait, k = 0; k < 10 && m.misses == 1; k++, t += 1f / 30f) { AISettle.Track(m, nx, ny, t); mx = my = 0f; AISettle.Hold(m, ref mx, ref my, (x, y) => false, -0.63f, 0f, -1f, 0f, 0f, 0f, nx, ny, t); }
        Program.Check(Math.Abs(m.stepLen - 0.365f) < 0.01f && m.trace.Contains("step 0.3"), $"D3g. (검수 L2) a correction that moved 0.365 makes that the step of the next aim ({m.stepLen:F3}; '{m.trace}')");
    }

    // ------------------------------------------------------------------ the placing (the same settle)

    // MergeFixTests M1c on CmdBody and every tick phase: the door re-hang (-4,-6) from its side stand (-4,-5) — the walk
    // straight at the stand until within 0.35 (or in reach), then AIPlaceGrid.PressStep
    static (int ok, int n, float worst, int maxTicks, string bad) PlaceSweep(float speed)
    {
        int ok = 0, n = 0, maxTicks = 0; float worst = 0f; string bad = "";
        for (int i = 0; i < 14; i++)
            for (int ph = 0; ph < Phases; ph++)
                for (int d = 0; d < 3; d++)
                {
                    float o = i * 0.02f, x0 = d == 2 ? -6.5f - o : -4f, y0 = d == 0 ? -3f - o : d == 1 ? -6.9f + o : -5f;
                    var b = new CmdBody(x0, y0, ph * 0.005f) { step = MergeFixTests.TickBody.Step * speed }; var s = new PlacePressState(); var nudger = new TickNudge();
                    bool settle = false, pressed = false; float t = 0f, dist = -1f; string fail = null;
                    for (int k = 0; k < 240 && !pressed && fail == null; k++, t = k / 30f)
                    {
                        b.Advance(t);
                        float mx = 0f, my = 0f; int nudge = 0;
                        bool reach = AIPlaceGrid.PressFrom(-4, -6, 1, 1, b.x, b.y);
                        if (!settle && !reach && Dist(b.x, b.y, -4f, -5f) > 0.35f) { mx = -4f - b.x; my = -5f - b.y; }
                        else
                        {
                            settle = true;
                            var po = AIPlaceGrid.PressStep(s, -4, -6, 1, 1, -4, -5, b.x, b.y, true, t);
                            if (po.press) { pressed = true; dist = s.pressDist; }
                            fail = po.fail; mx = po.mx; my = po.my; nudge = po.nudge;
                        }
                        nudger.Move(nudge, ref mx, ref my, (uint)(b.Tick(t) + 1000), out bool ahead);
                        b.Input(t, mx, my, nudger.on ? nudger.id : 0, ahead);
                    }
                    n++; maxTicks = Math.Max(maxTicks, b.MaxNudgeTicks());
                    if (pressed && dist >= 0.85f && dist <= AIPlaceGrid.PressReach + 1e-4f) { ok++; worst = Math.Max(worst, t); }
                    else if (bad == "") bad = $" BAD {(d == 0 ? "north" : d == 1 ? "south" : "side")}+{o:F2} ph{ph}: {(pressed ? $"pressed {dist:F2} off" : fail ?? "no press")}";
                }
        return (ok, n, worst, maxTicks, bad);
    }

    static void Place()
    {
        var a = PlaceSweep(1f); var f = PlaceSweep(1.3f);
        Program.Check(a.ok == a.n && f.ok == f.n && a.maxTicks <= 1 && f.maxTicks <= 1,
            $"D4a. the placing's press (the same settle): {a.ok}/{a.n} walks to the stand (14 start offsets x {Phases} tick phases x north / south / side) press standing still in the window, the slowest after {a.worst:F2} s; steps x1.3 {f.ok}/{f.n} ({f.worst:F2} s); at most {Math.Max(a.maxTicks, f.maxTicks)} tick a correction{a.bad}{f.bad}");
    }
}
