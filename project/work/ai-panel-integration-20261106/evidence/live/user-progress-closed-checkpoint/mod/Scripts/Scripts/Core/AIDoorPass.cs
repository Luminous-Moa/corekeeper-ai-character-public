// AIDoorPass — going through an own closed door as one action: open (E, the interact-with-object press) -> walk
// through -> close (user decision 2026-11-05). Unity-free so the steps are checked offline (core-tests EdgeTests).
// Game facts (6-문-여닫기.md): the door state is ObjectDataCD.variation (0/2 closed, 1/3 open); the press is the
// ClientInput bit InteractWithObject_Pressed (0x20), kept for one whole ServerTick (every frame of that tick, cleared on
// the next, so it is neither lost nor read twice); the game picks the door only when the player is within its
// interaction radius (default 0.7) and looks at it (targetingDirection within ~70 degrees); a press from less than
// about half a tile off the centre is not taken (재실측 N-a: the press window, below). The door is not opened by
// walking into it. A closed door blocks the game's path search; an open one lets enemies through, so it is closed
// again from the far side. No reaction branches: the steps run in order; a failed step ends the action with its fact.
// Door kinds: Manual (open/close by pressing) and Auto (an electrical door opens by itself: walk through only) — only
// the place for Auto is here (단계 2 decision), its timing is not measured.
using System;
using System.Collections.Generic;

public enum DoorKind { Manual, Auto }

public enum DoorPhase { Approach, Open, Pass, Close, Done, Failed }

public sealed class DoorPassState : SettleState
{
    public DoorPhase phase = DoorPhase.Approach;
    public int presses;          // presses made in the current Open / Close step
    public float stepAt = -1f;   // when the current step began
    public float pressAt = -1f;  // when the last press was asked
    public int pressReq;         // id of the last press asked (the body sends it for one server tick)
    public string fail = "";
    public string facts = "";
    // 2026-11-06 재실측 N-a: the press is made only from the press window, with the body standing still there (its
    // velocity, standing still and the correction pulses: SettleState, one place with the placing's stand, 합치기 전 수정 1)
    public float startAt = -1f;                       // 검수 S3: when the whole door step began (PassTimeout)
    public float pressDist = -1f;                     // distance from the door centre at the last press
    // 실측결함수정3 N3: what the step did, in order (phase changes, presses, correction pulses with the body's distance),
    // for the log of a slow or failed step (the clearing / mining walks took 4.2-4.3 s or failed where a walk took 1.5 s)
    public string trace = "";
    public override void Note(float now, string what) { if (trace.Length < 700) trace += $"{(startAt >= 0f ? now - startAt : 0f):F2}s {what}; "; }
    // C2 (합치기 전 수정): a press the body dropped unsent (its wait ran out) — said on the step's fail
    public string dropped = "";
}

// What the body should do this frame.
public struct DoorStepOut
{
    public float mx, my;   // move direction (0,0 = stand)
    public float fx, fy;   // face direction
    public bool press;     // ask a new press (pressReq was raised)
    public int nudge;      // 검수 M1: the move is a correction: the body moves it one server tick (AISettle.NudgeOut, TickNudge)
}

public static class AIDoorPass
{
    // 2026-11-06 재실측 N-a (17-재실측 4절): a press from less than about half a tile off the door centre is not taken by
    // the game — open or close (failures at 0.35, 0.49, 0.50; successes from 0.54 (open) and 0.69 (close)). The body
    // slides on after the walk input stops (the tail stopped its input at 0.92 and stood at 0.50, t=2420), so the old
    // "press as soon as it is close enough" pressed from inside the door tile. Now the press is made only when the body
    // stands still in the press window on its side: PressMin..PressMax from the door centre (the lower end from the
    // failures, the upper end the default interaction radius 0.7, 6-문-여닫기 4.2), within SideMax of the passage axis
    // (looking at the centre is then well within the ~70 degrees). Too close: it steps back first; too far: it steps in.
    // After a press the door's variation tells whether it worked.
    public const float PressMin = 0.55f, PressMax = 0.70f, PressAim = 0.63f, SideMax = 0.30f;
    // After two corrections that jumped over the window (검수 S1: the body ended on the other side of the spot than the
    // correction before it — a step from standing moving the body more than the window is wide; a step that fell short
    // does not count) the far end is widened to this. Records: a press at 0.85 was taken while walking in from 1.10
    // (t=597.5); presses from a standing body at 0.72, 0.75, 0.77, 0.79, 0.81 were all taken (20-실측: 0.79 outside five
    // times on the first press); 0.81-0.85 standing is not recorded. None from 0.50 or closer was taken; the near end is
    // never widened.
    // 25-문단계멈춰서기: a correction that carries the body out past NearSpot (walked back) counts as a jump over too
    // (AISettle.Hold), and the corrections are aimed at a step that ends in the window (AISettle.Aim: the window is
    // narrower than a step along the axis, so off the axis), so the narrow window is usually reached before any widening.
    // 검수 L3: off the axis the distance alone let the along-axis part fall to 0.46; the presses not taken (0.35, 0.49,
    // 0.50) were all on the axis, so whether it is the distance or the along part is not known: both are kept, the along
    // part above AlongMin (the widened window too).
    public const float PressMaxWide = 0.85f;
    public const float AlongMin = 0.50f;
    public static bool Wide(DoorPassState s) => s.overs >= 2;
    public const float BrakeT = AISettle.BrakeT, SettleT = AISettle.SettleT, SettleMove = AISettle.SettleMove;   // (AISettle)
    public const float NearSpot = AISettle.NearSpot, PulseMin = AISettle.PulseMin, PulseMax = AISettle.PulseMax;
    public const float NearDoor = PressAim; // (old name) the press spot's distance from the door centre
    public const float PastDoor = PressMin; // past the door: the body is out of the door tile (pressed against a shut door it stood 0.35 off the centre)
    public const float WaitOpen = 1.0f;     // after a press, wait this long for the state to change, then press again
    public const int MaxPresses = 2;        // presses per step before giving up
    public const float StepTimeout = 6f;    // each step gives up after this
    // 검수 S3: the whole door step (open -> through -> close) gives up after this — the walk counts no "stuck" while it
    // runs (it stands still on purpose), so this bounds the time without a stuck check (4 steps x 6 s = 24 s before)
    public const float PassTimeout = 12f;

    // in the press window on the side the axis (ax, ay) points to; along / side measured along that axis from the centre
    public static bool InPressWindow(float along, float side, bool wide = false)
    {
        float d = (float)Math.Sqrt(along * along + side * side);
        return along > AlongMin && Math.Abs(side) < SideMax && d >= PressMin && d <= (wide ? PressMaxWide : PressMax);
    }

    // dir = unit vector from the near side through the door (the passage axis); door = door centre; me = AI position.
    // open = the door's variation is an open one; pressDone = the body finished sending the last asked press.
    public static DoorStepOut Step(DoorPassState s, DoorKind kind, float doorX, float doorY, float dirX, float dirY,
        float meX, float meY, bool open, bool pressDone, float now)
    {
        var o = new DoorStepOut();
        if (s.stepAt < 0f) s.stepAt = now;
        if (s.startAt < 0f) s.startAt = now;
        AISettle.Track(s, meX, meY, now);
        // signed distance of the AI along the passage axis, from the door centre (negative = near side)
        float along = (meX - doorX) * dirX + (meY - doorY) * dirY;
        float side = (meX - doorX) * -dirY + (meY - doorY) * dirX;
        if (s.phase != DoorPhase.Done && s.phase != DoorPhase.Failed && now - s.startAt > PassTimeout)
            return Fail(s, $"문 지나기 전체 {PassTimeout:F0}초 넘음({PhaseName(s.phase)} 단계, {(open ? "문 열림" : "문 닫힘")}{WhereNote(s, s.phase == DoorPhase.Close ? along : -along, s.phase == DoorPhase.Close ? side : -side)})");
        switch (s.phase)
        {
            case DoorPhase.Approach:
                if (kind == DoorKind.Auto) { Next(s, DoorPhase.Pass, now); goto case DoorPhase.Pass; }
                if (open) { s.facts += "문이 이미 열려 있음; "; Next(s, DoorPhase.Pass, now); goto case DoorPhase.Pass; }
                {
                    float sx = doorX - dirX * PressAim, sy = doorY - dirY * PressAim;
                    if (InPressWindow(-along, -side) || Dist(meX, meY, sx, sy) <= NearSpot + 0.15f) { Next(s, DoorPhase.Open, now); goto case DoorPhase.Open; }
                    if (now - s.stepAt > StepTimeout) return Fail(s, "문 앞까지 못 감");
                    Go(s, ref o, meX, meY, sx, sy);   // to the press spot on the near side (braking for the slide)
                    Face(ref o, meX, meY, doorX, doorY, dirX, dirY);
                    return o;
                }
            case DoorPhase.Open:
                Face(ref o, meX, meY, doorX, doorY, dirX, dirY);   // look at the door
                if (open) { s.facts += $"열림(누름 {s.presses}번{PressNote(s)}); "; Next(s, DoorPhase.Pass, now); goto case DoorPhase.Pass; }
                // review G5: a press that never finishes (no server tick) or a door that does not answer ends the step
                if (now - s.stepAt > StepTimeout) return Fail(s, $"문 열기 {StepTimeout:F0}초 넘음(누름 {s.presses}번{(pressDone ? "" : ", 마지막 누름이 아직 안 보내짐")}{WhereNote(s, -along, -side)})");
                if (Pressing(s, pressDone, now)) return o;   // a press in flight or just made: stand still and watch the door
                if (s.presses >= MaxPresses) return Fail(s, $"{MaxPresses}번 눌러도 문이 열리지 않음(누른 자리 문 중심에서 {s.pressDist:F2}칸)");
                if (!Hold(s, ref o, doorX, doorY, -dirX, -dirY, meX, meY, now)) return o;   // into the press window first (too close: back off)
                Press(s, ref o, pressDone, now, -along, -side);
                return o;
            case DoorPhase.Pass:
                if (along >= PastDoor && Math.Abs(side) < 0.45f)
                {
                    if (kind == DoorKind.Auto) { s.facts += "자동문: 지나감; "; Next(s, DoorPhase.Done, now); return o; }
                    Next(s, DoorPhase.Close, now); goto case DoorPhase.Close;
                }
                if (!open && kind == DoorKind.Manual && along < 0f) return Fail(s, "지나가기 전에 문이 닫힘");
                if (now - s.stepAt > StepTimeout) return Fail(s, "문을 지나가지 못함");
                // through to the press spot on the far side; the input stops when the slide carries the body there
                Go(s, ref o, meX, meY, doorX + dirX * PressAim, doorY + dirY * PressAim);
                return o;
            case DoorPhase.Close:
                Face(ref o, meX, meY, doorX, doorY, -dirX, -dirY);   // look back at the door
                if (!open) { s.facts += $"닫힘(누름 {s.presses}번{PressNote(s)})"; Next(s, DoorPhase.Done, now); return o; }
                if (now - s.stepAt > StepTimeout) return Fail(s, $"문 닫기 {StepTimeout:F0}초 넘음(누름 {s.presses}번{WhereNote(s, along, side)})");
                if (Pressing(s, pressDone, now)) return o;
                if (s.presses >= MaxPresses) return Fail(s, $"지나간 뒤 {MaxPresses}번 눌러도 문이 닫히지 않음(열린 채; 누른 자리 문 중심에서 {s.pressDist:F2}칸)");
                // press only from the press window just past the door (out of the door tile, within the interaction radius)
                if (!Hold(s, ref o, doorX, doorY, dirX, dirY, meX, meY, now)) return o;
                Press(s, ref o, pressDone, now, along, side);
                return o;
            default:
                return o;
        }
    }

    // where the body was when a step timed out (for the fact)
    private static string WhereNote(DoorPassState s, float along, float side)
    {
        float d = (float)Math.Sqrt(along * along + side * side);
        string at = InPressWindow(along, side, Wide(s)) ? "누를 자리에 섬" : along <= 0f || d < PressMin ? "누를 자리보다 문에 가까움(문 칸 안이거나 밀림)" : "누를 자리에 못 섬(무언가 막음)";
        return $", 문 중심에서 {d:F2}칸 — {at}";
    }
    private static string PressNote(DoorPassState s) => s.pressDist >= 0f ? $", 문 중심에서 {s.pressDist:F2}칸" : "";

    // a press in flight, or made less than WaitOpen ago: the body stands and the door is watched
    // (실측결함수정3 N5: a press still waiting to be sent — the body holds it while the player's state takes no interact
    // press, e.g. Release after a swing — moves the wait's start along: the door gets WaitOpen after the press went out)
    private static bool Pressing(DoorPassState s, bool pressDone, float now)
    {
        if (s.pressReq != 0 && !pressDone) { s.pressAt = now; return true; }
        return s.presses > 0 && now - s.pressAt < WaitOpen;
    }

    // 합치기 전 수정 C2: the body dropped this step's last press unsent (PressQueue.Drop; why = its sentence, "" none):
    // it does not count as a press (the step may press again within its own time limits) and the fail says so
    public static void PressDropped(DoorPassState s, string why, float now)
    {
        if (string.IsNullOrEmpty(why) || s.dropped == why) return;
        s.dropped = why;
        if (s.presses > 0) s.presses--;
        s.pressAt = now - WaitOpen;
        s.Note(now, "press dropped");
    }

    // ask a press when none is in flight and the last one had time to act; true when one was asked now
    private static bool Press(DoorPassState s, ref DoorStepOut o, bool pressDone, float now, float along, float side)
    {
        bool idle = s.pressReq == 0 || pressDone;
        if (!idle) return false;
        if (s.presses > 0 && now - s.pressAt < WaitOpen) return false;
        if (s.presses >= MaxPresses) return false;
        s.presses++; s.pressReq++; s.pressAt = now;
        s.pressDist = (float)Math.Sqrt(along * along + side * side);
        s.Note(now, $"press {s.presses} @{s.pressDist:F2}");
        o.press = true;
        return true;
    }

    // Into the press window on the side (ax, ay) points to and standing still there: true when a press may be made
    // (AISettle.Hold: the spot PressAim off the door centre on that side; the window as a place, so a correction can be
    // aimed at a step that ends in it — the window is narrower than a step along the axis, 25-문단계멈춰서기).
    private static bool Hold(DoorPassState s, ref DoorStepOut o, float doorX, float doorY, float ax, float ay, float meX, float meY, float now)
    {
        Func<float, float, bool> win = (x, y) => InPressWindow((x - doorX) * ax + (y - doorY) * ay, (x - doorX) * -ay + (y - doorY) * ax, Wide(s));
        bool held = AISettle.Hold(s, ref o.mx, ref o.my, win, doorX + ax * PressAim, doorY + ay * PressAim, ax, ay, doorX, doorY, meX, meY, now);
        o.nudge = AISettle.NudgeOut(s, now);   // 검수 M1: a correction moves one server tick
        return held;
    }

    // walk towards (tx, ty), braking for the slide (AISettle.Go)
    private static void Go(DoorPassState s, ref DoorStepOut o, float meX, float meY, float tx, float ty) => AISettle.Go(s, ref o.mx, ref o.my, meX, meY, tx, ty);

    private static void Face(ref DoorStepOut o, float meX, float meY, float doorX, float doorY, float fbX, float fbY)
    {
        float dx = doorX - meX, dy = doorY - meY, l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l > 0.05f) { o.fx = dx / l; o.fy = dy / l; } else { o.fx = fbX; o.fy = fbY; }
    }

    private static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    private static void Next(DoorPassState s, DoorPhase p, float now)
    {
        s.Note(now, PhaseName(p));
        s.phase = p; s.stepAt = now; s.presses = 0; s.pressAt = -1f; s.pressDist = -1f;
        s.ResetPulses();
    }
    private static string PhaseName(DoorPhase p) => p == DoorPhase.Approach ? "문 앞으로 가기" : p == DoorPhase.Open ? "열기" : p == DoorPhase.Pass ? "지나가기" : p == DoorPhase.Close ? "닫기" : p.ToString();

    // 검수 S3: the door may be open because of this step (a press made, passing, closing): the walk does not end
    // "arrived" in the middle of it (open -> through -> close is one bundle)
    public static bool HoldsDoor(DoorPassState s)
        => s != null && (s.phase == DoorPhase.Pass || s.phase == DoorPhase.Close || s.phase == DoorPhase.Open && s.presses > 0);

    // 검수 F7: the fact of a door step that is running, for the "continue" option and the running action — what a new
    // choice costs ("" when there is no step)
    // waitsSwing (재검수 R2a): the running action is one whose swing waits for the close (attack, clearing)
    public static string StepNote(DoorPassState s, int doorX, int doorY, bool open, bool waitsSwing = false)
    {
        if (s == null || s.phase == DoorPhase.Done || s.phase == DoorPhase.Failed) return "";
        string wait = waitsSwing ? " " + NoSwingNote : "";
        if (!open) return $" 지금 문 ({doorX},{doorY}) 지나기 단계({PhaseName(s.phase)}, 문은 아직 닫힘: 열기 → 지나가기 → 닫기).{wait}";
        string rest = s.phase == DoorPhase.Close ? "닫기" : "지나가기 → 닫기";
        return $" 지금 문 ({doorX},{doorY}) 지나기 단계(문 열림, 남은 단계 {rest}): 다른 행동을 고르면 문이 열린 채 남음(열린 문은 적도 지나감).{wait}";
    }

    // 실측결함수정3 N2: while the running action's door step holds its door open, every option other than going on with
    // it says, in a few words, that choosing it leaves that door open (the close of that door is no option of its own:
    // it is the end of the running action — a separate "close" option was chosen twice and broke the attack off)
    public static string LeftOpenNote(int doorX, int doorY) => $" 고르면 지금 지나는 문 ({doorX},{doorY})이 열린 채 남음.";
    // the options that get it: all but going on with the running action and "hit now" (it says the door stays open)
    public static bool GetsLeftOpenNote(string kind, string key, string curKey)
        => kind != "continue" && kind != "attacknow" && key != curKey;

    // 재검수 R2a: the order inside the bundle, as a fact (the attack option through a door, the running attack / clearing)
    public static readonly string NoSwingNote = $"문을 연 뒤 닫기 전에는 치지 않음(닫기를 먼저 마치고 침; 보통 1초 안팎, 막히면 문 지나기 전체 최대 {PassTimeout:F0}초).";

    // 재검수 R1/R2: whether an attack (or a clearing) swings now. In reach and nothing shut between (a closed door or a wall
    // between: the walk and its door step go on — "문으로 나가서 치기" swung at the shut door from inside, Ng5); while the
    // walk's door step holds the door open the close comes first, unless the model chose to hit now (the door left open).
    public static bool SwingNow(float dist, float reach, bool nothingBetween, bool holdsDoor, bool hitNowChosen)
        => dist <= reach && nothingBetween && (!holdsDoor || hitNowChosen);

    // 재검수 R2b: the close is not getting on — an enemy's body in the door tile or on the far press spot the step walks
    // to, or the AI hit since the door opened. The code does not end the wait itself: this fact goes to the model with a
    // "hit now (the door left open)" option. "" when nothing blocks. enemies = positions of the enemies in view.
    public const float BodyBlock = 0.6f;
    public static string BlockedNote(DoorPassState s, int doorX, int doorY, float dirX, float dirY, bool open, IList<(float x, float y)> enemies, int hpOpen, int hpNow)
    {
        if (s == null || !open || s.phase != DoorPhase.Pass && s.phase != DoorPhase.Close) return "";
        var why = new List<string>();
        float spX = doorX + dirX * PressAim, spY = doorY + dirY * PressAim;
        bool inDoor = false, onSpot = false;
        if (enemies != null)
            foreach (var e in enemies)
            {
                if (Dist(e.x, e.y, doorX, doorY) < BodyBlock) inDoor = true;
                else if (s.phase == DoorPhase.Pass && Dist(e.x, e.y, spX, spY) < BodyBlock) onSpot = true;
            }
        if (inDoor) why.Add("적의 몸이 문 칸에 있음(문이 닫히지 않음)");
        if (onSpot) why.Add("적의 몸이 먼 쪽 누를 자리를 막음(지나가지 못함)");
        if (hpOpen > 0 && hpNow < hpOpen) why.Add($"문을 연 뒤 맞음(체력 {hpOpen}→{hpNow})");
        return why.Count == 0 ? "" : $"문 ({doorX},{doorY}) 닫기가 막힘: {string.Join(", ", why)}.";
    }

    // 검수 F1: one owner of the move input. A skill's own move: the walk's direction, else its straight step toward the
    // target when that step is allowed (directOk is asked only then). While a door step runs, it alone holds the move —
    // a skill's straight step (attack, pick-up, root) between the door step's stand-still frames pushed the body off the
    // press spot again and again, and the close timed out with the door open.
    public static (float x, float y) SkillMove(float navX, float navY, Func<bool> directOk, float faceX, float faceY)
    {
        if (navX * navX + navY * navY > 1e-4f) return (navX, navY);
        return directOk != null && directOk() ? (faceX, faceY) : (0f, 0f);
    }
    public static (float x, float y) BodyMove(bool doorStep, float doorX, float doorY, float skillX, float skillY)
        => doorStep ? (doorX, doorY) : (skillX, skillY);
    private static DoorStepOut Fail(DoorPassState s, string why) { s.phase = DoorPhase.Failed; s.fail = why + (s.dropped != "" ? $" — {s.dropped}" : ""); s.trace += "failed"; return new DoorStepOut(); }

    // open/closed from the door's variation (6-문-여닫기.md 1절)
    public static bool IsOpen(int variation) => variation == 1 || variation == 3;
    // the passage axis of a door: variation 0/1 horizontal (a wall running east-west: passage north-south),
    // 2/3 vertical (passage east-west)
    public static bool PassageEastWest(int variation) => variation == 2 || variation == 3;

    // What a walk (AINavigator) does when the game's path search found no path (단계 3 review F2). The own door comes
    // first: the short tile-A* hop treats every door tile as blocked, so with the target 1-3 tiles past an own door
    // ("go out of the door and hit", "back into the base" from just outside) it found no way and the walk failed before
    // the door branch was reached. One door per walk (N1): past a door, no second door branch.
    // Recheck R3: a known open way (tiles seen, doors in view counted as blocked) comes before the own door — the door
    // was a detour when a gap being filled or a broken part still led there. The same order picks the next try when one
    // try's tile A* finds no way (the caller marks it tried: shortHopTried, or the door as used: ownDoorToward false),
    // so a failed A* to the door still tries the short hop, and a failed known-way hop still tries the door.
    public enum NoPathStep { OwnDoor, ShortHop, PadHop, FailAfterDoor, Fail }
    // Live-run fix 4 (14-단계3-측정 사실 4): doorAhead = the walk got stuck on the game's path with the own door it leads
    // to just ahead (the game gave a partial way and the AI stood pushing at the shut door): the door comes first. The
    // stuck walk takes this same order (it used to switch to the tile A* with no tries left and fail "no way").
    public static NoPathStep NoPathNext(bool ownDoorToward, bool doorsPassable, bool passedDoor, bool shortHopTried, bool shortNear, bool knownWay, bool padHopDone, bool onPadWithExit, bool doorAhead = false)
    {
        if (doorAhead && ownDoorToward && !doorsPassable && !passedDoor) return NoPathStep.OwnDoor;
        if (!shortHopTried && knownWay) return NoPathStep.ShortHop;
        if (ownDoorToward && !doorsPassable && !passedDoor) return NoPathStep.OwnDoor;
        if (!shortHopTried && shortNear) return NoPathStep.ShortHop;
        if (!padHopDone && onPadWithExit) return NoPathStep.PadHop;
        return passedDoor ? NoPathStep.FailAfterDoor : NoPathStep.Fail;
    }
}
