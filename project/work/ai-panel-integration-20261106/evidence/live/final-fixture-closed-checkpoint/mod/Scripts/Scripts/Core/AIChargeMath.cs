// AIChargeMath — what a charging enemy's charge (now: MushroomBrute) would do to a planned short move: Unity-free, so the
// prediction sentences of the close-fight options can be checked offline (Tools/core-tests ChargeTests). Combat stage 2
// (work/2026-11/2026-11-05-전투-적용-계획.md "설계(단계 2 구체화)", "단계 0 결과 반영"). It predicts; it never decides:
// the options carry the prediction and its assumptions as a fact, the decision model chooses (pol.combat-no-auto-dodge).
// Measured values (stage 0, 49 charges, 2-측정.md): anticipation 0.50 s, the aim follows the target until 0.45 s, charge
// 6.0 tiles/s reached in ~0.3 s, at most 3 s, no steering; hit radius 0.75 (+ ~0.3 body) around the brute; after it hits
// anything it is vulnerable 3.6 s; the next anticipation ~1.05 s after the charge state ends; walls around the hit break.
// (If Scripts/Core is ever flattened, this file becomes Scripts/AICoreChargeMath.cs: a rename only.)
using System;

public struct V2
{
    public float x, y;
    public V2(float x, float y) { this.x = x; this.y = y; }
    public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
    public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
    public static V2 operator *(V2 a, float k) => new V2(a.x * k, a.y * k);
    public float Len => (float)Math.Sqrt(x * x + y * y);
    public V2 Norm { get { float l = Len; return l < 1e-6f ? new V2(0f, 0f) : new V2(x / l, y / l); } }
    public static float Dot(V2 a, V2 b) => a.x * b.x + a.y * b.y;
    public static float Cross(V2 a, V2 b) => a.x * b.y - a.y * b.x;
    public V2 Left => new V2(-y, x);   // 90° counter-clockwise (x east, y north)
}

// What the enemy's charge looks like now (from the game's ChargeAttackStateCD, read by AIChargeWatch).
public enum ChargePhase { Ready, Anticipation, Charging, Vulnerable, Recover, Other }

public sealed class ChargeView
{
    public ChargePhase phase = ChargePhase.Other;
    public float phaseElapsed;      // seconds in this phase (server ticks)
    public float vulnLeft;          // Vulnerable: seconds left of the 3.6 s window
    public float recoverLeft;       // Recover (charged into nothing): seconds left
    public float cooldownLeft = -1; // Ready: seconds until a charge may start (-1 unknown, 0 = may start any time)
    public V2 pos;                  // enemy position
    public V2 dir;                  // its aim / charge direction (unit; zero if unknown)
    public bool aimAtMe;            // its charge target is this character
    public bool targetKnown;        // it has a charge target at all
}

public struct ChargePrediction
{
    public bool valid;              // a charge is under way (anticipation or charging) and was simulated
    public bool hitsMe;             // the charge reaches my body (within HitReach) on its way
    public float tHit;              // seconds from now when it reaches me (hitsMe)
    public float passGap;           // not hit: my distance from the charge line when it passes (or stops) beside me
    public bool stops;              // the charge runs into something (a wall, an object, me) within its 3 s
    public bool stopsAtMe;          // what it runs into is me
    public float runTiles;          // tiles it runs before stopping (or the full run)
    public float tStop;             // seconds from now when it stops (collision -> vulnerable from then)
    public V2 stopAt;               // stops, not at me: the blocked point it ran into (the caller says what is there)
    // Review F2 (4-독립검수.md): I am within the hit width of its line but the run is predicted to stop (or end) before it
    // reaches me ("short of me"); elseTHit: when it would reach me if that stop does not happen (-1: it would not anyway).
    public bool shortOfMe;
    public bool meAhead;            // where the run ended I am still ahead of it along its line
    public float elseTHit;
}

public static class AIChargeMath
{
    public const float Anticipation = 0.50f, LockAt = 0.45f, Speed = 6.0f, Accel = 0.3f, MaxRun = 3.0f;
    public const float HitReach = 1.05f;      // hit radius 0.75 + my body ~0.3
    public const float Vulnerable = 3.6f, CooldownAfter = 1.05f, Damage = 58f;
    public const float Walk = 5.7f, StartDelay = 0.13f;   // my walk speed; apply -> first move p50 (measured)
    public const float BruteBody = 0.6f;     // how far in front of its centre the brute touches a wall (assumed)

    // My position t seconds from now when I wait `delay` s and then walk straight from p0 to p1 at Walk.
    public static V2 MoveAt(V2 p0, V2 p1, float delay, float t)
    {
        if (t <= delay) return p0;
        V2 d = p1 - p0; float len = d.Len;
        if (len < 1e-4f) return p0;
        float s = Math.Min(len, (t - delay) * Walk);
        return p0 + d * (s / len);
    }

    // Seconds the move takes to its end (delay + walk).
    public static float MoveSeconds(V2 p0, V2 p1, float delay) => delay + (p1 - p0).Len / Walk;

    // Distance a charge has run `t` seconds after it started (linear acceleration to Speed over Accel seconds).
    public static float Run(float t)
    {
        if (t <= 0f) return 0f;
        if (t < Accel) return 0.5f * (Speed / Accel) * t * t;
        return 0.5f * Speed * Accel + Speed * (t - Accel);
    }

    // Seconds from now until the charge starts moving (anticipation left), 0 when it already runs.
    public static float GoIn(ChargeView v) => v.phase == ChargePhase.Anticipation ? Math.Max(0f, Anticipation - v.phaseElapsed) : 0f;
    // Seconds from now until the aim locks (0 when locked or running).
    public static float LockIn(ChargeView v) => v.phase == ChargePhase.Anticipation ? Math.Max(0f, LockAt - v.phaseElapsed) : 0f;

    // The charge under way (anticipation or charging) against my planned move p0 -> p1 starting after `delay`.
    // blocked(point): the point is inside a wall or an object (the charge stops there). Assumptions (the option text
    // says them): the aim follows my position until LockAt when it is aimed at me (FullAim, measured), otherwise its
    // direction now; it runs straight at Speed; it stops at the first wall/object or at me.
    public static ChargePrediction Predict(ChargeView v, V2 p0, V2 p1, float delay, Func<V2, bool> blocked)
        => ShortOfMe(Sim(v, p0, p1, delay, blocked), () => Sim(v, p0, p1, delay, null));

    // F2: within the width but not reached means the run stopped (or ended) before me. Mark it, and say when it would reach
    // me if that stop does not happen: the stop point may be wrong (S2 ② R2 #1: predicted to stop after 0.0-0.1 tiles, it
    // ran 4.7 tiles and hit).
    private static ChargePrediction ShortOfMe(ChargePrediction r, Func<ChargePrediction> unblocked)
    {
        r.elseTHit = -1f;
        if (r.valid && !r.hitsMe && r.passGap < HitReach && r.meAhead)
        {
            r.shortOfMe = true;
            var e = unblocked();
            if (e.hitsMe) r.elseTHit = e.tHit;
        }
        return r;
    }

    private static ChargePrediction Sim(ChargeView v, V2 p0, V2 p1, float delay, Func<V2, bool> blocked)
    {
        var r = new ChargePrediction();
        if (v.phase != ChargePhase.Anticipation && v.phase != ChargePhase.Charging) return r;
        r.valid = true;
        float go = GoIn(v);
        V2 dir = v.dir;
        V2 start = v.pos;
        if (v.phase == ChargePhase.Anticipation && v.aimAtMe)
        {
            V2 atLock = MoveAt(p0, p1, delay, LockIn(v));
            V2 d = atLock - v.pos;
            if (d.Len > 1e-3f) dir = d.Norm;
        }
        if (dir.Len < 0.5f) { V2 d = p0 - v.pos; dir = d.Len > 1e-3f ? d.Norm : new V2(1f, 0f); }
        // when already charging, the run so far is unknown here: the position now is the start of the rest
        float ranBefore = v.phase == ChargePhase.Charging ? Run(v.phaseElapsed) : 0f;
        float maxT = go + MaxRun - (v.phase == ChargePhase.Charging ? v.phaseElapsed : 0f);
        float minGap = float.MaxValue;
        const float dt = 0.02f;
        for (float t = 0f; t <= maxT + 1e-4f; t += dt)
        {
            float run = t <= go ? 0f : Run(t - go + (v.phase == ChargePhase.Charging ? v.phaseElapsed : 0f)) - ranBefore;
            V2 b = start + dir * run;
            V2 me = MoveAt(p0, p1, delay, t);
            float dme = (me - b).Len;
            if (t > go - 1e-4f && dme <= HitReach)
            {
                r.hitsMe = true; r.tHit = t; r.stops = true; r.stopsAtMe = true; r.runTiles = run; r.tStop = t;
                return r;
            }
            // beside me: the gap to the line at my position along it
            V2 rel = me - start; float along = V2.Dot(rel, dir);
            if (along >= run - 0.5f && along <= run + 0.5f) minGap = Math.Min(minGap, Math.Abs(V2.Cross(dir, rel)));
            if (t > go && blocked != null && blocked(b + dir * BruteBody))
            {
                r.stops = true; r.runTiles = run; r.tStop = t; r.stopAt = b + dir * BruteBody;
                break;
            }
            r.runTiles = run; r.tStop = t;
        }
        if (minGap == float.MaxValue)
        {
            V2 me = MoveAt(p0, p1, delay, r.tStop); V2 rel = me - start;
            minGap = Math.Abs(V2.Cross(dir, rel));
        }
        r.passGap = minGap;
        r.meAhead = V2.Dot(MoveAt(p0, p1, delay, r.tStop) - start, dir) > r.runTiles;
        return r;
    }

    // The same charge against an action that follows the enemy (the continuing attack): after `delay` I walk straight at
    // the enemy's position of the moment while I am farther than `reach`, and stand (swing) inside it — as AttackSkill does
    // (S2 live run: 3 of 5 "escapes" that were hit were a continuing attack chosen during a charge, predicted as standing).
    public static ChargePrediction PredictChase(ChargeView v, V2 me0, float delay, float reach, Func<V2, bool> blocked)
        => ShortOfMe(SimChase(v, me0, delay, reach, blocked), () => SimChase(v, me0, delay, reach, null));

    private static ChargePrediction SimChase(ChargeView v, V2 me0, float delay, float reach, Func<V2, bool> blocked)
    {
        var r = new ChargePrediction();
        if (v.phase != ChargePhase.Anticipation && v.phase != ChargePhase.Charging) return r;
        r.valid = true;
        float go = GoIn(v), lockIn = LockIn(v);
        V2 dir = v.dir, start = v.pos, me = me0;
        bool locked = !(v.phase == ChargePhase.Anticipation && v.aimAtMe);
        if (dir.Len < 0.5f) { V2 d0 = me0 - v.pos; dir = d0.Len > 1e-3f ? d0.Norm : new V2(1f, 0f); }
        float ranBefore = v.phase == ChargePhase.Charging ? Run(v.phaseElapsed) : 0f;
        float maxT = go + MaxRun - (v.phase == ChargePhase.Charging ? v.phaseElapsed : 0f);
        float minGap = float.MaxValue;
        const float dt = 0.02f;
        for (float t = 0f; t <= maxT + 1e-4f; t += dt)
        {
            if (!locked && t >= lockIn) { V2 d = me - v.pos; if (d.Len > 1e-3f) dir = d.Norm; locked = true; }
            float run = t <= go ? 0f : Run(t - go + (v.phase == ChargePhase.Charging ? v.phaseElapsed : 0f)) - ranBefore;
            V2 b = start + dir * run;
            if (t > delay) { V2 tb = b - me; float l = tb.Len; if (l > reach) me = me + tb.Norm * Math.Min(Walk * dt, l - reach); }
            if (t > go - 1e-4f && (me - b).Len <= HitReach) { r.hitsMe = true; r.tHit = t; r.stops = true; r.stopsAtMe = true; r.runTiles = run; r.tStop = t; return r; }
            V2 rel = me - start; float along = V2.Dot(rel, dir);
            if (along >= run - 0.5f && along <= run + 0.5f) minGap = Math.Min(minGap, Math.Abs(V2.Cross(dir, rel)));
            if (t > go && blocked != null && blocked(b + dir * BruteBody)) { r.stops = true; r.runTiles = run; r.tStop = t; r.stopAt = b + dir * BruteBody; break; }
            r.runTiles = run; r.tStop = t;
        }
        r.passGap = minGap == float.MaxValue ? Math.Abs(V2.Cross(dir, me - start)) : minGap;
        r.meAhead = V2.Dot(me - start, dir) > r.runTiles;
        return r;
    }

    // Review F1: the charge state is read only for chargers near a character whose AI is on (none: nothing is read).
    // 24 tiles: three times the close-fight range (8), more than a 3 s charge covers past it (18), so a charger that comes
    // into the close fight has been watched since before its anticipation.
    public const float WatchTiles = 24f;
    public static bool Watched(V2 charger, System.Collections.Generic.IList<V2> aiPlayers)
    {
        if (aiPlayers == null) return false;
        foreach (var p in aiPlayers) if ((p - charger).Len <= WatchTiles) return true;
        return false;
    }

    // Open length behind `p` along `dir` until the first blocked point (max `maxTiles`): where a charge aimed through p
    // would stop. Returns maxTiles + 1 when nothing within.
    public static float OpenBehind(V2 p, V2 dir, float maxTiles, Func<V2, bool> blocked)
    {
        if (blocked == null || dir.Len < 0.5f) return maxTiles + 1f;
        for (float s = 0.25f; s <= maxTiles + 1e-4f; s += 0.25f)
            if (blocked(p + dir * s)) return s;
        return maxTiles + 1f;
    }

    // My distance from the charge line now (perpendicular), and whether I am in front of it within reach.
    public static float LineGap(ChargeView v, V2 me, out bool ahead)
    {
        V2 dir = v.dir.Len > 0.5f ? v.dir : (me - v.pos).Norm;
        V2 rel = me - v.pos;
        ahead = V2.Dot(rel, dir) > -0.3f;
        return Math.Abs(V2.Cross(dir, rel));
    }

    // Seconds until the charge (anticipation/charging) would reach my present position if I stay (-1 if it would not).
    public static float ArriveIn(ChargeView v, V2 me, Func<V2, bool> blocked)
    {
        var p = Predict(v, me, me, 0f, blocked);
        return p.valid && p.hitsMe ? p.tHit : -1f;
    }

    // ---- the sentences (one format for every option; assumptions in the text; no advice)

    public static string F1(float f) => f.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    public static string F2(float f) => f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    // What a run stops at (review F2): the caller reads the map at ChargePrediction.stopAt. Stage 0 measured stops at walls
    // (7), players (28) and other things (14, roots / core structures by guess); whether a given object or a non-walkable
    // floor stops it was not measured, so those sentences say so.
    public enum StopKind { Unknown, Wall, Tile, Object, Stuck }
    public struct StopWhat
    {
        public StopKind kind; public string name;
        public StopWhat(StopKind k, string n = null) { kind = k; name = n; }
    }

    public static string StopText(StopWhat w)
    {
        switch (w.kind)
        {
            case StopKind.Wall: return "벽";
            case StopKind.Tile: return $"걸을 수 없는 칸({(string.IsNullOrEmpty(w.name) ? "종류 모름" : w.name)}, 돌진을 멈추는지 확인 못 함)";
            case StopKind.Object: return $"물체 {(string.IsNullOrEmpty(w.name) ? "(이름 모름)" : w.name)}(돌진을 멈추는지 확인 못 함)";
            case StopKind.Stuck: return "전에 걷다 막힌 자리(무엇인지 모름, 돌진을 멈추는지 확인 못 함)";
            default: return "벽·물체";
        }
    }

    public static bool WallStop(StopWhat w) => w.kind == StopKind.Wall || w.kind == StopKind.Unknown;

    public static string StopCode(StopWhat w) => w.kind == StopKind.Tile ? "tile" : w.kind == StopKind.Object ? "obj" : w.kind == StopKind.Stuck ? "stuck" : "wall";

    // "예상(가정: …): …" for the charge under way, given my planned move. what: what the run stops at (not at me).
    public static string OutcomeText(ChargePrediction r, float delay, bool aimFollows, bool wallBreaks, string moveAssume = null, StopWhat what = default)
    {
        if (!r.valid) return "";
        string assume = $"가정: 약 {F2(delay)}초 뒤 출발{(string.IsNullOrEmpty(moveAssume) ? "" : ", " + moveAssume)}, {(aimFollows ? $"조준은 전조 시작 {F2(LockAt)}초까지 내 위치를 따라온 뒤 고정" : "지금 방향 그대로")}, 돌진 {F1(Speed)}칸/초 직선";
        if (r.hitsMe) return $"예상({assume}): 돌진선에서 못 벗어남 — 약 {F2(r.tHit)}초 뒤 나에게 부딪힘(피해 약 {Damage:F0}), 그 뒤 버섯 야수 취약 {F1(Vulnerable)}초.";
        // re-review: the wall-break note only when it stops at a wall (Unknown = the old "벽·물체" caller)
        string walls = wallBreaks && WallStop(what) ? ", 부딪힌 둘레 벽은 부서짐" : "";
        if (r.shortOfMe)
        {
            // F2: on the line but the run ends first — not "out of the line"
            string on = $"돌진선 안(선 중심에서 약 {F1(r.passGap)}칸, 맞는 폭 약 {F2(HitReach)}칸)";
            if (!r.stops) return $"예상({assume}): {on}이지만 돌진은 {F1(r.runTiles)}칸 가는 동안 나에게 닿지 않음(부딪힐 것 없음, 취약 안 됨).";
            string other = r.elseTHit >= 0f ? $"그것에 멈추지 않으면 약 {F2(r.elseTHit)}초 뒤 나에게 부딪힘(피해 약 {Damage:F0})." : "그것에 멈추지 않아도 나에게 닿지 않음.";
            return $"예상({assume}): {on}이지만 돌진이 약 {F1(r.runTiles)}칸 가서 {StopText(what)}에 먼저 부딪혀 멈춘다고 봄 — 나에게 닿기 전. 멈추면 버섯 야수 취약 {F1(Vulnerable)}초(약 {F2(r.tStop)}초 뒤부터){walls}. {other}";
        }
        string pass = $"돌진선에서 벗어남(지나갈 때 선 중심에서 약 {F1(r.passGap)}칸, 맞는 폭 약 {F2(HitReach)}칸)";
        if (r.stops) return $"예상({assume}): {pass}, 돌진은 약 {F1(r.runTiles)}칸 가서 {StopText(what)}에 부딪혀 버섯 야수 취약 {F1(Vulnerable)}초(약 {F2(r.tStop)}초 뒤부터){walls}.";
        return $"예상({assume}): {pass}, 돌진은 {F1(r.runTiles)}칸 가는 동안 부딪힐 것 없음(취약 안 됨).";
    }

    // Short code for the logs and the prediction-vs-result table: hit@<t> / miss<gap>/<wall|tile|obj|stuck><run> /
    // miss<gap>/none / short<gap>/<stop><run>[>hit@<t if it does not stop>] / short<gap>/none.
    public static string Code(ChargePrediction r, StopWhat what = default)
    {
        if (!r.valid) return "-";
        if (r.hitsMe) return $"hit@{F2(r.tHit)}";
        string stop = r.stops ? StopCode(what) + F1(r.runTiles) : "none";
        if (r.shortOfMe) return $"short{F1(r.passGap)}/{stop}{(r.stops && r.elseTHit >= 0f ? $">hit@{F2(r.elseTHit)}" : "")}";
        return $"miss{F1(r.passGap)}/{stop}";
    }

    // For the ready phase: a charge aimed at `dest` from the enemy now would stop `open` tiles behind dest.
    public static string NextChargeText(float open, float maxTiles, bool wallBreaks, StopWhat what = default)
    {
        if (open > maxTiles) return $"예상(가정: 다음 돌진이 이 도착지로 겨눠 오고 내가 맞지 않을 때): 도착지 뒤 {maxTiles:F0}칸 안에 부딪힐 것 없음(취약 안 됨).";
        return $"예상(가정: 다음 돌진이 이 도착지로 겨눠 오고 내가 맞지 않을 때): 도착지 뒤 약 {F1(open)}칸에서 {StopText(what)}에 부딪혀 버섯 야수 취약 {F1(Vulnerable)}초{(wallBreaks && WallStop(what) ? "(부딪힌 둘레 벽은 부서짐)" : "")}. 내가 맞으면 피해 약 {Damage:F0}, 그때도 취약은 열림.";
    }

    // "시간: 끝날 때까지 약 X초, 그때 적 취약 남은 약 Y초, 그때 적과 거리 Z칸(휘두르는 거리 안/밖, 적이 제자리일 때)."
    public static string TimeText(float seconds, ChargeView v, float distAtEnd, float reach)
    {
        string vuln;
        if (v.phase == ChargePhase.Vulnerable) { float left = v.vulnLeft - seconds; vuln = left > 0f ? $"그때 적 취약 남은 약 {F1(left)}초" : "그때 적 취약 끝남"; }
        else vuln = "그때 적 취약 아님(지금 취약 아님)";
        return $"시간: 끝날 때까지 약 {F2(seconds)}초, {vuln}, 그때 적과 거리 {F1(distAtEnd)}칸(휘두르는 거리 {(distAtEnd <= reach ? "안" : "밖")}, 적이 제자리일 때).";
    }
}
