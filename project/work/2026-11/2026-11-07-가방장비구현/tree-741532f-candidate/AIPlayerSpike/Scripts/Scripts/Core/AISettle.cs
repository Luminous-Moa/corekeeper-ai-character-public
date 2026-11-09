// AISettle — standing the AI's sliding body still on a spot before a press (합치기 전 수정 1, 2026-11-06): one place for
// the door step's press spot (AIDoorPass) and the placing's stand centre (AIPlaceGrid.PressStep). Unity-free so the
// steps are checked offline (core-tests). Moved out of AIDoorPass unchanged: the body's velocity from the positions of
// the calls, "standing still" (moved less than SettleMove for SettleT), the walk that brakes for the slide, and short
// correction pulses near the spot (longer while they fall short, shorter after one jumps over the window).
// The body moves in steps of a server tick (20 a second, about 0.28 tile at full speed; the move input is a direction
// only) and the input reaches the game about a tick late (movetrace 2026-11-06 t=615.0-616.6).
// 문 단계 멈춰 서기 (25-문단계멈춰서기.md): a window narrower than a step (the door's 0.55-0.70 along its axis) may get no
// step of a straight walk at all: from (-4,-9.00) the near side of the door (-4,-6) is reached only at 0.47 and 0.75 off
// its centre (22-짧은확인 t=318-324, 1 of 4 walks in failed). So a correction is aimed: of the directions round the body
// it takes one whose single step ends in the window — sideways when straight on jumps over it — and a pulse that carries
// the body back out past NearSpot counts as a jump over the window (the walk back used to hide it).
// 검수 M1 (26-문단계멈춰서기-검수): a time pulse is one or two server ticks by the frames' phase against the ticks (0.05 s =
// two frames at 30 fps; a tick with no frame reuses the last input), so the aim held only half the time and most presses
// came after a jump over and the walk back. Each correction is now a nudge: its id goes out with the move (nudge) and
// the body sends that move for exactly one server tick (TickNudge: every frame of the first tick it sees, none on the
// next, the next two ticks written without a move). The step waits NudgeWait for it to land before judging the stand.
// 검수 L2: the step a nudge moved the body (one tick) is measured and the aim uses it (a speed buff, a slowdown).
using System;

public class SettleState
{
    public float lastX, lastY, lastT = -1f, vx, vy;   // last position and the measured velocity (tiles/s)
    public float stillX, stillY, stillSince = -1f;    // the body stayed within SettleMove of (stillX, stillY) since then
    public float pulseUntil = -1f, pulse = PulseStart; // a short correction step near the spot (length adapted)
    public float pulseX, pulseY;                      // the correction's direction (aimed at a step that ends in the window)
    public bool pulsed;                               // a correction ran and the body has not been corrected again since
    public int missSign, misses;                      // the last correction's side (+1 too far, -1 too close), how many
    public int overs;                                 // 검수 S1: corrections that jumped over the window (the side flipped)
    public int nudge;                                 // 검수 M1: the id of the last correction (the body moves it one server tick)
    public float pulseAt = -1f, pulseFromX, pulseFromY; // when and where the last correction began
    public float stepLen = AISettle.StepLen;          // 검수 L2: one tick of the body, as the last nudge moved it
    public float lastMove = 1f;                       // how far the body moved between the last two calls
    public const int Hist = 8; public readonly float[] hT = new float[Hist], hX = new float[Hist], hY = new float[Hist]; public int hN;   // the last positions (Track)
    public const float PulseStart = 0.05f;
    public void ResetPulses() { pulseUntil = -1f; pulse = PulseStart; missSign = 0; misses = 0; overs = 0; pulsed = false; pulseAt = -1f; }
    // what the steps did, for a log (the door step keeps a trace; "" elsewhere)
    public virtual void Note(float now, string what) { }
}

public static class AISettle
{
    public const float BrakeT = 0.075f;     // the body slides about this many seconds of its speed after the input stops (0.42 tile at 5.7 tiles/s)
    public const float SettleT = 0.12f, SettleMove = 0.03f;   // standing still: moved less than SettleMove for SettleT
    public const float NearSpot = 0.35f;    // closer than this to the spot the corrections are short pulses
    public const float PulseMin = 0.02f, PulseMax = 0.25f;
    public const float StepLen = 0.2808f;   // one server tick of the body at full speed (movetrace t=615.0-616.6)
    // 검수 M1: after a correction no judging for this long — the nudge's tick goes out within a tick and moves the body
    // about a tick later (the input two ticks ahead, movetrace t=318.70: asked at stk 2858, moved on 2860)
    public const float NudgeWait = 0.2f;
    const int AimDirs = 72;                 // directions tried for an aimed correction (5 degrees apart)
    static int _nudgeIds;                   // correction ids, unique over every state (a new door step must not reuse the last one)

    public static bool Still(SettleState s, float now) => s.stillSince >= 0f && now - s.stillSince >= SettleT;
    // 검수 L2: "near the spot" is at least a step and a quarter (a step longer than NearSpot, x1.3: 0.365, never stood
    // within it and the walk swung round the spot)
    public static float Near(SettleState s) => Math.Max(NearSpot, 1.25f * s.stepLen);
    // the correction id to send with this frame's move (0 = none): the body moves it one server tick (TickNudge)
    public static int NudgeOut(SettleState s, float now) => now < s.pulseUntil ? s.nudge : 0;

    // Onto the spot (sx, sy) and standing still there: true when a press may be made. win(x, y) = the body at (x, y) is
    // where the press is taken (the window). ax, ay = the unit axis the spot lies on, pointing from the thing pressed
    // (the door, the tile to place) to the side the press is made from: "too far" is past the spot along it. Well off
    // the spot it walks there (braking for the slide); near it, once the body stands, a correction aimed so one step
    // ends in the window (Aim; one server tick, NudgeOut); in the window it stands until it is still. (cx, cy) = the
    // centre of the thing pressed, for the pulse note.
    public static bool Hold(SettleState s, ref float mx, ref float my, Func<float, float, bool> win, float sx, float sy, float ax, float ay, float cx, float cy, float meX, float meY, float now)
    {
        bool still = Still(s, now);
        if (win(meX, meY)) { if (still) s.pulsed = false; return still; }   // (a body that stood in the window jumped nothing)
        float off = Dist(meX, meY, sx, sy);
        if (now < s.pulseUntil) { mx = s.pulseX; my = s.pulseY; return false; }
        if (s.pulseAt >= 0f && now < s.pulseAt + NudgeWait) return false;   // the nudge may not have moved the body yet
        if (!still || off > Near(s))
        {
            if (off > Near(s))
            {
                // a correction that carried the body out past NearSpot jumped over the window (22-짧은확인 t=318: 0.47 ->
                // 1.03 off the door, the walk back to 0.47, over and over — the same side each time, so the pulse only grew)
                if (s.pulsed) { s.pulsed = false; s.overs++; s.missSign = 0; s.pulse = Math.Max(PulseMin, s.pulse * 0.5f); s.Note(now, $"over @{Dist(meX, meY, cx, cy):F2}"); }
                Go(s, ref mx, ref my, meX, meY, sx, sy);   // walk; near the spot let the body come to a stand first
            }
            return false;
        }
        // 검수 L2: the last correction (standing before and after) moved the body one tick: that is the step now (a move
        // of two ticks or none is not taken)
        if (s.pulsed && s.pulseAt >= 0f)
        {
            float moved = Dist(meX, meY, s.pulseFromX, s.pulseFromY);
            if (moved >= StepLen * 0.6f && moved <= StepLen * 1.5f) s.stepLen = moved;
        }
        int sign = (meX - sx) * ax + (meY - sy) * ay > 0f ? 1 : -1;
        if (s.missSign != 0) s.pulse = sign == s.missSign ? Math.Min(PulseMax, s.pulse * 1.6f) : Math.Max(PulseMin, s.pulse * 0.5f);
        if (s.missSign != 0 && sign != s.missSign) s.overs++;   // 검수 S1: the last correction jumped over the window
        s.missSign = sign; s.misses++;
        s.pulseUntil = now + s.pulse;
        s.pulseAt = now; s.pulseFromX = meX; s.pulseFromY = meY; s.nudge = ++_nudgeIds;
        s.stillSince = -1f;
        s.pulsed = true;
        bool aimed = Aim(win, meX, meY, sx, sy, out s.pulseX, out s.pulseY, s.stepLen);
        s.Note(now, $"pulse {s.pulse:F2}s {(sign > 0 ? "in" : "out")} @{Dist(meX, meY, cx, cy):F2}{(aimed ? $" aim {Dist(meX + s.pulseX * s.stepLen, meY + s.pulseY * s.stepLen, cx, cy):F2}" : "")}{(Math.Abs(s.stepLen - StepLen) > 0.01f ? $" step {s.stepLen:F2}" : "")}");
        mx = s.pulseX; my = s.pulseY;
        return false;
    }

    // The direction of a correction from the body (meX, meY): of AimDirs directions, one whose single step (step, one
    // server tick) ends in the window — where there is one, with a step 15% shorter and longer ending there too and two
    // steps staying within NearSpot of the spot (a body that does not keep a nudge to one tick); of those the one whose
    // step ends nearest the spot. False: no single step ends in the window — straight at the spot, as before.
    public static bool Aim(Func<float, float, bool> win, float meX, float meY, float sx, float sy, out float ux, out float uy, float step = StepLen)
    {
        float best = float.MaxValue; ux = 0f; uy = 0f;
        for (int i = 0; i < AimDirs; i++)
        {
            double a = 2.0 * Math.PI * i / AimDirs;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
            float qx = meX + dx * step, qy = meY + dy * step;
            if (!win(qx, qy)) continue;
            float score = Dist(qx, qy, sx, sy);
            if (!win(meX + dx * step * 0.85f, meY + dy * step * 0.85f) || !win(meX + dx * step * 1.15f, meY + dy * step * 1.15f)) score += 10f;
            if (Dist(meX + dx * step * 2f, meY + dy * step * 2f, sx, sy) > NearSpot) score += 1f;
            if (score < best) { best = score; ux = dx; uy = dy; }
        }
        if (best < float.MaxValue) return true;
        float l = Dist(meX, meY, sx, sy);
        if (l >= 0.06f) { ux = (sx - meX) / l; uy = (sy - meY) / l; }
        return false;
    }

    // walk towards (tx, ty), but no input while the body's slide already carries it there (a body that stopped short
    // gets the input again on the next call: its measured speed is then 0)
    public static void Go(SettleState s, ref float mx, ref float my, float meX, float meY, float tx, float ty)
    {
        float dx = tx - meX, dy = ty - meY, l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l < 0.06f) return;
        float ux = dx / l, uy = dy / l;
        float vToward = s.vx * ux + s.vy * uy;
        if (vToward > 0f && vToward * BrakeT >= l - 0.03f) return;
        mx = ux; my = uy;
    }

    // the body's velocity and how long it has stood still (from the positions of the calls). 검수 I1 / L2: the velocity
    // over the last VelWindow (two server ticks), not between two frames — the body moves on ticks, so a frame without
    // one read 0 and the walk gave its input again (at a step longer than NearSpot, x1.3, it swung round the spot for good)
    public const float VelWindow = 0.1f;
    public static void Track(SettleState s, float meX, float meY, float now)
    {
        if (s.lastT < 0f || now > s.lastT)
        {
            int i = s.hN % SettleState.Hist; s.hT[i] = now; s.hX[i] = meX; s.hY[i] = meY; s.hN++;
            int pick = -1;
            for (int k = 1; k < Math.Min(s.hN, SettleState.Hist); k++)
            {
                int j = (s.hN - 1 - k) % SettleState.Hist;
                pick = j;
                if (s.hT[j] <= now - VelWindow + 1e-4f) break;
            }
            if (pick >= 0 && now > s.hT[pick]) { float dt = now - s.hT[pick]; s.vx = (meX - s.hX[pick]) / dt; s.vy = (meY - s.hY[pick]) / dt; }
            // 검수 L2: a jump after a frame of standing is one server tick of the body (a speed buff, a slowdown)
            if (s.lastT >= 0f)
            {
                float fd = Dist(meX, meY, s.lastX, s.lastY);
                if (s.lastMove < 1e-3f && fd >= StepLen * 0.6f && fd <= StepLen * 1.5f) s.stepLen = fd;
                s.lastMove = fd;
            }
            s.lastX = meX; s.lastY = meY; s.lastT = now;
        }
        if (s.stillSince < 0f || Dist(meX, meY, s.stillX, s.stillY) > SettleMove) { s.stillX = meX; s.stillY = meY; s.stillSince = now; }
    }
    public static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}

// 검수 M1: the body side of a correction (the input sender, with the server tick each frame's input goes to; like the
// press bit, PressQueue): a new nudge id sends its move on every frame of the first tick it is seen on and no move on
// later ticks while the step still asks it; zeroAhead = write the next two ticks without a move too (a tick that gets
// no frame takes the last older input, which would move the body a second tick — 20 fps, a long frame).
public sealed class TickNudge
{
    public int id; public uint tick; public bool on; public float dx, dy;
    public int sent;   // nudges sent (for a log)
    public void Move(int ask, ref float mx, ref float my, uint t, out bool zeroAhead)
    {
        zeroAhead = false;
        if (ask != 0 && ask != id) { id = ask; tick = t; on = true; dx = mx; dy = my; sent++; }
        if (!on) return;
        if (t == tick) { mx = dx; my = dy; zeroAhead = true; return; }
        if (ask == id) { mx = 0f; my = 0f; return; }
        on = false;
    }
}
