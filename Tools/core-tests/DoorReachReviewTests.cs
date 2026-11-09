// G3rr — the door / reach review of 9a1b9f9 (Review/door-reach-review.md; results 19-문·도달-검수반영.md). Each finding's
// failure is rebuilt first (as it was), then the fix. The door line runs the whole skill layer with the door step: the
// skill's own move (AIDoorPass.SkillMove: the walk's direction, else its straight step toward the target) and the body's
// one owner of the move (AIDoorPass.BodyMove), on the sliding body of RemeasureTests.
using System;
using System.Collections.Generic;

public static class DoorReachReviewTests
{
    public static void Run()
    {
        SkillWithDoorStep(); Memory(); CloseWords(); Small(); Recheck();
    }

    // ------------------------------------------------------------------ 재검수 (Review/door-reach-recheck.md)

    // the base of N-g (RemeasureTests.DoorBetween): inside x -6..-2, y -5..-1, the cave outside, the own door (-4,-6)
    static GuardGrid Base(bool doorOpen)
    {
        var g = GuardGrid.Make(-16, -18, 25, GuardCell.Closed);
        for (int x = -6; x <= -2; x++) for (int y = -5; y <= -1; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        for (int x = -12; x <= 4; x++) for (int y = -14; y <= -7; y++) g.cell[g.Idx(x, y)] = GuardCell.Open;
        g.cell[g.Idx(-4, -6)] = doorOpen ? GuardCell.Open : GuardCell.OwnDoor;
        return g;
    }
    // "nothing between" as the attack asks it: the touch of the facts, from the AI's stand tile to the enemy's
    static bool Touch(float ax, float ay, float ex, float ey, bool open)
    {
        var g = Base(open);
        Func<(int x, int y), bool> stand = t => (open || t != (-4, -6)) && g.At(t.x, t.y) != GuardCell.Closed;
        var m = AIBaseGuard.Reach(g, AIBaseGuard.StandTile(ax, ay, stand));
        return AIBaseGuard.ReachOf(m, AIBaseGuard.StandTile(ex, ey, stand)).touch;
    }

    sealed class Run2 { public bool swungShut, swung, done, shut; public float tOpen = -1f, tPass = -1f, tNote = -1f, tSwing = -1f, t; public int hits, presses; public string note = "", fail = "", observation = ""; }
    // The attack's walk out through the door with the enemy standing still at (ex, ey). rule: 0 = as 61f57e6 (in reach and
    // the door not held: swing), 1 = now (SwingNow: nothing between too); bodyBlock: the enemy's body keeps the AI 0.6 off
    // and hits it every second (10); hitNowOnNote: the model takes "hit now" as soon as the blocked fact is there
    static Run2 RunAttack(float ax, float ay, float ex, float ey, int rule, bool bodyBlock, bool hitNowOnNote)
    {
        float doorX = -4f, doorY = -6f;
        var r = new Run2(); var s = new DoorPassState();
        var b = new RemeasureTests.Body { x = ax, y = ay };
        bool open = false, pressDone = true, hitNow = false; float t = 0f, nextHit = -1f; int hp = 100, hpOpen = -1;
        for (int k = 0; k < 600; k++, t += RemeasureTests.Body.Dt)
        {
            float dx = ex - b.x, dy = ey - b.y, d = (float)Math.Sqrt(dx * dx + dy * dy);
            bool holds = AIDoorPass.HoldsDoor(s);
            bool swing = rule == 0 ? d <= 1.3f && !holds : AIDoorPass.SwingNow(d, 1.3f, Touch(b.x, b.y, ex, ey, open), holds, hitNow);
            if (swing) { r.swung = true; r.swungShut = !open; r.tSwing = t; break; }
            if (s.phase == DoorPhase.Done) { r.done = true; break; }
            if (s.phase == DoorPhase.Failed) { r.fail = s.fail; break; }
            var o = AIDoorPass.Step(s, DoorKind.Manual, doorX, doorY, 0f, -1f, b.x, b.y, open, pressDone, t);
            if (o.observation != null) r.observation = o.observation;
            if (s.phase == DoorPhase.Failed) { r.fail = s.fail; break; }   // the walk ends with it
            if (!pressDone) pressDone = true;
            if (o.press) { pressDone = false; r.presses++; if (RemeasureTests.Taken(b.x, b.y, o.fx, o.fy, doorX, doorY)) open = !open; }
            if (open && hpOpen < 0) { hpOpen = hp; r.tOpen = t; }
            if (s.phase == DoorPhase.Pass && r.tPass < 0f) r.tPass = t;
            string note = AIDoorPass.BlockedNote(s, -4, -6, 0f, -1f, open, new List<(float x, float y)> { (ex, ey) }, hpOpen, hp);
            if (note != "" && r.tNote < 0f) { r.tNote = t; r.note = note; if (hitNowOnNote) hitNow = true; }
            float fx = dx / d, fy = dy / d;
            bool shutNow = !open;
            var sm = AIDoorPass.SkillMove(o.mx, o.my, () => DirectOk(b.x, b.y, fx, fy, shutNow), fx, fy);
            var mv = AIDoorPass.BodyMove(true, o.mx, o.my, sm.x, sm.y);
            b.Step(mv.x, mv.y, doorX, doorY, false, !open);
            if (bodyBlock)
            {
                float bx = b.x - ex, by = b.y - ey, bl = (float)Math.Sqrt(bx * bx + by * by);
                if (bl < 0.6f) { b.x = ex + bx / Math.Max(bl, 1e-3f) * 0.6f; b.y = ey + by / Math.Max(bl, 1e-3f) * 0.6f; b.vx = b.vy = 0f; }
                if (open && d <= 1.3f && t >= nextHit) { hp -= 10; r.hits++; nextHit = t + 1f; }
            }
        }
        r.shut = !open; r.t = t;
        return r;
    }

    static void Recheck()
    {
        // R1 (Ng5): the enemy pressed on the far side of the shut door (-4,-6) at (-4,-6.45); the AI comes from inside
        var before = RunAttack(-4f, -4f, -4f, -6.45f, 0, false, false);
        Program.Check(before.swungShut && before.presses == 0, $"R1a. (as 61f57e6) in reach at the inner press spot the attack swung at the shut door ({before.t:F1} s, no press)");
        var after = RunAttack(-4f, -4f, -4f, -6.45f, 1, false, false);
        // (in the model the AI passes the enemy's place; the first swing comes once both are on the far side, the door shut behind)
        Program.Check(after.presses >= 2 && after.shut && after.fail == "" && (after.done || after.swung), $"R1b. now: no swing through the shut door; the door step opens, passes and closes first ({after.t:F1} s, {after.presses} presses, then {(after.swung ? "the swing" : "done")})");
        Program.Check(!Touch(-4f, -5.37f, -4f, -6.45f, false) && Touch(-4f, -5.37f, -4f, -6.45f, true), "R1c. 'nothing between' is the facts' touch: false through the shut door, true once it is open");
        // an enemy against an object next to the AI (its rounded tile the object's): nothing between (the 4-neighbour rule)
        var og = GuardGrid.Make(-5, -5, 11, GuardCell.Open); og.cell[og.Idx(1, 0)] = GuardCell.Closed;
        Program.Check(AIBaseGuard.ReachOf(AIBaseGuard.Reach(og, (0, 0)), (1, 0)).touch, "R1d. an enemy whose tile is an object's next to the AI: nothing between (the swing is not lost)");

        // R2b: the door opened, the enemy's body on the far press spot (-4,-6.63) keeps the AI in the doorway and hits it
        var wait = RunAttack(-4f, -4.5f, -4f, -6.63f, 1, true, false);
        Program.Check(!wait.swung && !wait.shut && wait.hits >= 5 && wait.fail == "" && wait.observation.Contains("지나가지 못함"), $"R2a. a blocked passage reports '{wait.observation}' while preserving the choice until the model switches ({wait.hits} hits, door open)");
        var offer = RunAttack(-4f, -4.5f, -4f, -6.63f, 1, true, true);
        Program.Check(offer.tNote >= 0f && offer.tNote - offer.tPass < 0.2f && offer.note.Contains("먼 쪽 누를 자리를 막음") && offer.swung && offer.tSwing - offer.tNote < 0.3f && offer.hits <= 1,
            $"R2b. the blocked fact is there {offer.tNote - offer.tPass:F2} s into the pass ('{offer.note}'); taking 'hit now' swings {offer.tSwing - offer.tNote:F2} s later ({offer.hits} hits)");
        var ps = new DoorPassState { phase = DoorPhase.Close };
        string hurt = AIDoorPass.BlockedNote(ps, -4, -6, 0f, -1f, true, new List<(float x, float y)> { (-8f, -10f) }, 100, 90);
        string inDoor = AIDoorPass.BlockedNote(ps, -4, -6, 0f, -1f, true, new List<(float x, float y)> { (-4f, -6.1f) }, 100, 100);
        string free = AIDoorPass.BlockedNote(ps, -4, -6, 0f, -1f, true, new List<(float x, float y)> { (-8f, -10f) }, 100, 100);
        Program.Check(hurt.Contains("문을 연 뒤 맞음(체력 100→90)") && inDoor.Contains("문 칸에 있음") && free == "" && AIDoorPass.BlockedNote(new DoorPassState(), -4, -6, 0f, -1f, false, null, 100, 50) == "",
            $"R2c. blocked: hit since the door opened ('{hurt}'), an enemy in the door tile ('{inDoor}'); nothing: no fact (and none before the door opened)");
        Program.Check(AIDoorPass.SwingNow(1f, 1.3f, true, true, true) && !AIDoorPass.SwingNow(1f, 1.3f, true, true, false) && !AIDoorPass.SwingNow(1f, 1.3f, false, false, true) && AIDoorPass.SwingNow(1f, 1.3f, true, false, false),
            "R2d. the swing: nothing between always; while the door step holds the door only after 'hit now'");
        // R2a: the order is a fact on the attack option through a door and on the running step
        var st = new DoorPassState { phase = DoorPhase.Pass };
        Program.Check(AIDoorPass.StepNote(st, -4, -6, true, true).Contains("닫기 전에는 치지 않음") && !AIDoorPass.StepNote(st, -4, -6, true, false).Contains("치지 않음") && AIDoorPass.NoSwingNote.Contains("최대 12초"),
            $"R2e. '{AIDoorPass.NoSwingNote}' on the running attack's step and on the attack option through a door");

        // R3: the last looks survive a restart (memory.json): a hole seen open is still open out of view
        var shape = Room(true, true).shape;
        var seen = new EdgeLastSeen(); Look(seen, shape, true, true);
        var saved = seen.ToArray();
        var fresh = new EdgeLastSeen();
        var lost = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Room(true, false).g, (2, 2), new BaseMemory(shape, null, null, fresh)), (-4, 2));
        var loaded = new EdgeLastSeen(); loaded.Load(saved);
        var kept = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Room(true, false).g, (2, 2), new BaseMemory(shape, null, null, loaded)), (-4, 2));
        var scan = Look(loaded, shape, true, false);
        Program.Check(!lost.comes && lost.memPlanned == 1 && kept.comes && loaded.Last(-1, 2) == true && scan.list.Count == 1 && scan.list[0].remembered,
            $"R3. (as 61f57e6) after a restart the seen hole was 'closed as planned'; kept with the edge memory it is open, and still a repair ({saved.Length / 3} tiles saved)");
        int v0 = loaded.Version; loaded.Note(-1, 2, true); int v1 = loaded.Version; loaded.Note(-1, 2, false);
        Program.Check(v1 == v0 && loaded.Version == v0 + 1, "R3b. only a new or changed look marks the memory to save");

        // R4: someone else's shut door between (1.3 tiles): a door, not a wall; it may open
        var g = GuardGrid.Make(-6, -6, 13, GuardCell.Closed);
        g.cell[g.Idx(0, 0)] = GuardCell.Open; g.cell[g.Idx(0, -2)] = GuardCell.Open; g.cell[g.Idx(0, -1)] = GuardCell.ShutDoor;
        var r4 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0)), (0, -2));
        string f4 = AIBaseGuard.CloseNote(r4, 1.3f);
        g.cell[g.Idx(0, -1)] = GuardCell.Closed;
        string f4old = AIBaseGuard.CloseNote(AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0)), (0, -2)), 1.3f);
        Program.Check(!r4.comes && r4.otherDoor && f4.Contains("닫힌 문(남의 것)") && f4.Contains("문이 열리면 달라짐") && !f4.Contains("부서지면") && f4old.Contains("벽이나 막힌 칸"),
            $"R4. someone else's shut door between: '{f4.Trim()}' (as 61f57e6 read as a wall: '{f4old.Trim()}')");
    }

    // ------------------------------------------------------------------ F1: a skill's straight step and the door step

    // The walk of an attack (or a pick-up) out through the own door (-4,-6) southwards; the target stands beyond the far
    // press spot. Each frame as the game runs it: the target in reach (and the door step not holding the door) -> swing,
    // the walk disposed (its door step is cut); else the walk's door step (AIDoorPass.Step), the skill's move, and the
    // body's move — with the one owner (arbiter) or as 9a1b9f9 did (the skill's move straight to the body).
    sealed class Run1 { public DoorPassState s; public bool shut, swungOpen, done; public float t; public List<float> press = new List<float>(); public int offSpot; public string observation = ""; }
    static Run1 RunSkill(float ax, float ay, float ex, float ey, float reach, bool arbiter)
    {
        float doorX = -4f, doorY = -6f;
        var r = new Run1 { s = new DoorPassState() };
        var b = new RemeasureTests.Body { x = ax, y = ay };
        bool open = false, pressDone = true; float t = 0f;
        for (int k = 0; k < 900; k++, t += RemeasureTests.Body.Dt)
        {
            float dx = ex - b.x, dy = ey - b.y, d = (float)Math.Sqrt(dx * dx + dy * dy);
            bool holds = arbiter && AIDoorPass.HoldsDoor(r.s);   // the fix: the swing waits while the door step holds the door
            if (d <= reach && !holds) { r.swungOpen = open; break; }   // the skill swings; its walk (and door step) is disposed
            if (r.s.phase == DoorPhase.Done) { r.done = true; break; }
            if (r.s.phase == DoorPhase.Failed) break;
            var o = AIDoorPass.Step(r.s, DoorKind.Manual, doorX, doorY, 0f, -1f, b.x, b.y, open, pressDone, t);
            if (o.observation != null) r.observation = o.observation;
            if (!pressDone) pressDone = true;
            if (o.press)
            {
                pressDone = false;
                r.press.Add((float)Math.Sqrt((b.x - doorX) * (b.x - doorX) + (b.y - doorY) * (b.y - doorY)));
                if (RemeasureTests.Taken(b.x, b.y, o.fx, o.fy, doorX, doorY)) open = !open;
            }
            float fx = dx / d, fy = dy / d;
            bool shutNow = !open;
            var sm = AIDoorPass.SkillMove(o.mx, o.my, () => DirectOk(b.x, b.y, fx, fy, shutNow), fx, fy);
            bool stepOn = r.s.phase != DoorPhase.Done && r.s.phase != DoorPhase.Failed;
            var mv = arbiter ? AIDoorPass.BodyMove(stepOn, o.mx, o.my, sm.x, sm.y) : sm;
            if (r.s.phase == DoorPhase.Close && (o.mx != 0f || o.my != 0f)) r.offSpot++;   // the step had to walk back to its spot
            b.Step(mv.x, mv.y, doorX, doorY, false, !open);
        }
        if (r.s.phase == DoorPhase.Done) r.done = true;
        r.shut = !open; r.t = t;
        return r;
    }
    // AIEdge.DirectStepOk on this map: not into the shut door, not into its side walls
    static bool DirectOk(float x, float y, float fx, float fy, bool doorShut)
    {
        var me = ((int)Math.Round(x), (int)Math.Round(y));
        var ahead = ((int)Math.Round(x + fx * 0.7f), (int)Math.Round(y + fy * 0.7f));
        if (ahead == me) return true;
        if (ahead == (-4, -6)) return !doorShut;
        return ahead != (-5, -6) && ahead != (-3, -6);
    }
    static string L(List<float> l) => string.Join(" ", l.ConvertAll(v => v.ToString("F2")));

    static void SkillWithDoorStep()
    {
        // "문으로 나가서 치기": the AI inside at (-4,-4.5), the enemy beyond the far press spot (0.63) at 1.37 tiles off it
        var before = RunSkill(-4f, -4.5f, -4f, -8.0f, 1.3f, false);
        Program.Check(!before.shut && !before.done, $"F1a. (as 9a1b9f9) attack through the door: the straight step toward the enemy between the door step's stand-still frames — the close never happens, the door stays open ({before.s.phase}, presses {L(before.press)}, swung with the door open={before.swungOpen}{(before.s.fail != "" ? ", '" + before.s.fail + "'" : "")})");
        // the enemy farther (4 tiles past the door, out of reach all the time): the review's case — the body never stands
        // still at the press spot (the step walks it back, the straight step pushes it on), the close times out open
        var osc = RunSkill(-4f, -4.5f, -4f, -10f, 1.3f, false);
        Program.Check(osc.s.phase == DoorPhase.Close && !osc.shut && osc.observation.Contains("닫기"), $"F1a2. competing movement leaves closing active and reported ({osc.offSpot} frames walking back), '{osc.observation}', the door open");
        var oscAfter = RunSkill(-4f, -4.5f, -4f, -10f, 1.3f, true);
        Program.Check(oscAfter.done && oscAfter.shut, $"F1a3. now: closes ({oscAfter.t:F1} s, presses {L(oscAfter.press)})");
        var after = RunSkill(-4f, -4.5f, -4f, -8.0f, 1.3f, true);
        Program.Check(after.done && after.shut && after.press.TrueForAll(v => v >= AIDoorPass.PressMin - 1e-3f), $"F1b. with one owner of the move (the door step while it runs): opens, passes, closes from the press window; presses at {L(after.press)}, {after.t:F1} s ({after.s.facts})");
        // the same from 3 tiles inside at full speed (the approach's brake), and the clear (pick-up: reach 1.4) of an
        // object just past the far press spot (1.37 off it: in reach while the step closes)
        var far = RunSkill(-4f, -3f, -4f, -8.0f, 1.3f, true);
        Program.Check(far.done && far.shut, $"F1c. from 3 tiles inside at full speed with the straight step: closes ({far.t:F1} s, presses {L(far.press)})");
        var clrBefore = RunSkill(-4f, -4.5f, -4f, -8.0f, 1.4f, false);
        var clr = RunSkill(-4f, -4.5f, -4f, -8.0f, 1.4f, true);
        Program.Check(!clrBefore.shut && clr.done && clr.shut, $"F1d. clearing an object in reach of the far press spot: before the swing cut the step with the door open ({clrBefore.s.phase}, swung open={clrBefore.swungOpen}); now the close comes first, then the swing ({clr.t:F1} s)");
        Program.Check(AIDoorPass.BodyMove(false, 0.5f, 0f, 0f, 1f) == (0f, 1f) && AIDoorPass.BodyMove(true, 0f, 0f, 0f, 1f) == (0f, 0f)
            && AIDoorPass.SkillMove(0f, 0f, () => true, 1f, 0f) == (1f, 0f) && AIDoorPass.SkillMove(0f, 0f, () => false, 1f, 0f) == (0f, 0f) && AIDoorPass.SkillMove(0f, 1f, () => throw new Exception("asked"), 1f, 0f) == (0f, 1f),
            "F1e. the rules: the door step's move (also its stand-still) wins while it runs; a skill's straight step only without a walk direction, and the step check is asked only then");

        // S3: the walk does not end "arrived" in the middle of a door step that holds the door
        var st = new DoorPassState();
        bool approach = AIDoorPass.HoldsDoor(st); st.phase = DoorPhase.Open; bool openNoPress = AIDoorPass.HoldsDoor(st); st.presses = 1; bool pressed = AIDoorPass.HoldsDoor(st);
        st.phase = DoorPhase.Pass; bool pass = AIDoorPass.HoldsDoor(st); st.phase = DoorPhase.Close; bool close = AIDoorPass.HoldsDoor(st); st.phase = DoorPhase.Done;
        Program.Check(!approach && !openNoPress && pressed && pass && close && !AIDoorPass.HoldsDoor(st) && !AIDoorPass.HoldsDoor(null), "S3a. the arrival waits only while the step may hold the door open (a press made, passing, closing)");
        // S3: the whole step has a time cap (no stuck check while it runs): 12 s, not 4 x 6 s
        var cap = new DoorPassState { phase = DoorPhase.Close, startAt = 0f, stepAt = 11.5f };
        var elapsedDoor = AIDoorPass.Step(cap, DoorKind.Manual, -4f, -6f, 0f, -1f, -4f, -6.63f, true, true, 12.1f);
        Program.Check(cap.phase == DoorPhase.Close && elapsedDoor.observation.Contains("전체 12초") && elapsedDoor.observation.Contains("닫기 단계"), $"S3b. the door step continues after {AIDoorPass.PassTimeout:F0} s and reports '{elapsedDoor.observation}'");

        // S1: widened only after two corrections that jumped over the window — a body that falls short (here it cannot
        // move at all, 0.80 off) is never pressed from outside the window (9a1b9f9 widened after any two corrections)
        var sh = new DoorPassState { phase = DoorPhase.Close }; int presses = 0; float t1 = 0f;
        for (int k = 0; k < 300 && sh.phase == DoorPhase.Close; k++, t1 += RemeasureTests.Body.Dt)
            if (AIDoorPass.Step(sh, DoorKind.Manual, 0f, 0f, 1f, 0f, 0.80f, 0f, true, true, t1).press) presses++;
        Program.Check(presses == 0 && sh.misses >= 2 && sh.overs == 0 && !AIDoorPass.Wide(sh), $"S1a. corrections that fall short ({sh.misses}) do not widen the window: no press from 0.80 ('{sh.fail}')");
        var ov = new DoorPassState(); ov.missSign = 1; ov.overs = 2;
        Program.Check(AIDoorPass.Wide(ov) && AIDoorPass.InPressWindow(0.80f, 0f, AIDoorPass.Wide(ov)), "S1b. after two jumps over the window the far end is 0.85 (an estimate; a standing press at 0.70-0.85 is not measured)");

        // F7: the door step's fact for the "continue" option and the running action
        var ps = new DoorPassState { phase = DoorPhase.Pass };
        string pn = AIDoorPass.StepNote(ps, -4, -6, true);
        var ap = new DoorPassState();
        string an = AIDoorPass.StepNote(ap, -4, -6, false);
        Program.Check(pn.Contains("문 (-4,-6)") && pn.Contains("문 열림") && pn.Contains("다른 행동을 고르면 문이 열린 채 남음") && !an.Contains("열린 채") && an.Contains("닫힘") && AIDoorPass.StepNote(null, 0, 0, true) == "",
            $"F7. in the door step: '{pn.Trim()}'; before it opened: '{an.Trim()}'");
    }

    // ------------------------------------------------------------------ F2-F5, F9: memory out of view

    // A room x 0..4, y 0..4 (inside), its edge the ring round it; the AI inside at (2,2); outside open ground beyond the
    // ring (x -3..7, y -3..7). The edge tile (-1,2) is a natural edge (a root) the AI dug out.
    static (GuardGrid g, BaseShape shape) Room(bool holeOpen, bool holeInView)
    {
        var ins = new List<(int x, int y)>(); for (int x = 0; x <= 4; x++) for (int y = 0; y <= 4; y++) ins.Add((x, y));
        var shape = AIBaseGuard.Shape(ins, new List<(int x, int y)>());
        var g = GuardGrid.Make(-8, -8, 21, GuardCell.Open);
        g.view = new bool[g.n * g.n];
        for (int i = 0; i < g.cell.Length; i++)
        {
            int x = g.loX + i / g.n, y = g.loY + i % g.n;
            if (shape.IsEdge(x, y)) g.cell[i] = GuardCell.Closed;
            g.view[i] = x >= -1 && x <= 5 && y >= -1 && y <= 5;   // the room and its ring in view
        }
        g.cell[g.Idx(-1, 2)] = holeOpen ? GuardCell.Open : GuardCell.Closed;
        g.view[g.Idx(-1, 2)] = holeInView;
        return (g, shape);
    }

    // the breach scan's look at the room (the hole (-1,2) open or not, in view or not): it notes the last looks
    static BreachScan Look(EdgeLastSeen seen, BaseShape shape, bool holeOpen, bool inView)
        => AIBaseGuard.Breaches(shape, new List<(int, int)>(), new List<(int, int)>(), t => t == (-1, 2) ? (!inView ? EdgeNow.Unseen : holeOpen ? EdgeNow.Open : EdgeNow.Closed) : EdgeNow.Closed, seen);

    static void Memory()
    {
        (int, int) ai = (2, 2), enemy = (-4, 2);
        var seen = new EdgeLastSeen();
        var a = Room(true, true);
        // 재검수 R5: the reach map only reads the last looks; the breach scan notes them (one place)
        var readOnly = new EdgeLastSeen();
        AIBaseGuard.Reach(a.g, ai, new BaseMemory(a.shape, null, null, readOnly));
        Program.Check(readOnly.Count == 0, "R5. the reach map notes no last look (the breach scan is the one place)");
        Look(seen, a.shape, true, true);
        var inView = AIBaseGuard.ReachOf(AIBaseGuard.Reach(a.g, ai, new BaseMemory(a.shape, null, null, seen)), enemy);
        // the AI walks off: the hole out of view, its tiles still read open (as the game reads a seen tile)
        var b = Room(true, false);
        var old = AIBaseGuard.ReachOf(AIBaseGuard.Reach(b.g, ai, new BaseMemory(a.shape, null, null)), enemy);
        var now = AIBaseGuard.ReachOf(AIBaseGuard.Reach(b.g, ai, new BaseMemory(a.shape, null, null, seen)), enemy);
        Program.Check(inView.comes && seen.Last(-1, 2) == true, "F2a. the dug-out root (-1,2) in view: the enemy past it can come, the look is kept (open)");
        Program.Check(!old.comes && old.memPlanned == 1 && now.comes && now.memSeenOpen == 1 && now.memPlanned == 0,
            $"F2b. (as 9a1b9f9) out of view the hole was 'closed as planned' and the enemy 'cannot come'; now it is what was seen there last: open, the enemy can come (seen open {now.memSeenOpen})");
        // even when the out-of-view tile reads closed now (the game would leak a change it did not see): the last look
        var c = Room(false, false);
        var stale = AIBaseGuard.ReachOf(AIBaseGuard.Reach(c.g, ai, new BaseMemory(a.shape, null, null, seen)), enemy);
        Program.Check(stale.comes, "F2c. out of view the last look decides, not the tile now (seen open: open)");
        // seen closed at the last look: out of view closed, with the words of the last look (no "if it broke meanwhile")
        var seen2 = new EdgeLastSeen();
        Look(seen2, a.shape, false, true);
        var shutR = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Room(true, false).g, ai, new BaseMemory(a.shape, null, null, seen2)), enemy);
        string kf = AIBaseGuard.KillFacts(a.shape, enemy, shutR, "Slime", 6f);
        Program.Check(!shutR.comes && shutR.memHidden == 1 && kf.Contains("마지막으로 볼 때 막혀 있던 둘레 1칸") && !kf.Contains("그사이 부서졌으면"),
            $"F2d. seen closed at the last look: out of view closed, said as the last look ('{kf}')");
        // the breach scan: the hole in view is a breach; out of view it stays one (remembered), with its fact; the count
        // stays, so walking off and back raises no new breach trigger
        var seen3 = new EdgeLastSeen(); var w = new AIBaseGuard.BreachWatch();
        EdgeNow Now(bool view, (int x, int y) t) => !view && t == (-1, 2) ? EdgeNow.Unseen : t == (-1, 2) ? EdgeNow.Open : EdgeNow.Closed;
        var s1 = AIBaseGuard.Breaches(a.shape, new List<(int, int)>(), new List<(int, int)>(), t => Now(true, t), seen3);
        string tr1 = AIBaseGuard.BreachTrigger(w, 1, s1.list);
        var s0 = AIBaseGuard.Breaches(a.shape, new List<(int, int)>(), new List<(int, int)>(), t => Now(false, t));
        var s2 = AIBaseGuard.Breaches(a.shape, new List<(int, int)>(), new List<(int, int)>(), t => Now(false, t), seen3);
        string tr2 = AIBaseGuard.BreachTrigger(w, 1, s2.list);
        var s3 = AIBaseGuard.Breaches(a.shape, new List<(int, int)>(), new List<(int, int)>(), t => Now(true, t), seen3);
        string tr3 = AIBaseGuard.BreachTrigger(w, 1, s3.list);
        Program.Check(s1.list.Count == 1 && tr1 != null && s0.list.Count == 0, "F2e. (as 9a1b9f9) the hole in view is a breach; out of view it was dropped (no repair option, no fact)");
        Program.Check(s2.list.Count == 1 && s2.list[0].remembered && s2.list[0].Text.Contains("기억: (-1,2) 마지막으로 볼 때 열림, 지금 안 보임") && s2.remembered == 1 && tr2 == null && tr3 == null && AIBaseGuard.RepairBatches(s2, t => (0, 2)).Count == 1,
            $"F2f. out of view the seen hole stays a breach with its fact ('{(s2.list.Count > 0 ? s2.list[0].Text : "")}'), a repair batch, and no new trigger walking off and back");

        // F3: a large object seen before decides "cannot come" — the facts say so, also without a base. A corridor
        // x -6..6 at y = 0, an object of 2 tiles on (1,0),(2,0) out of view; the enemy at (5,0)
        var gc = GuardGrid.Make(-8, -8, 17, GuardCell.Closed); gc.view = new bool[gc.n * gc.n];
        for (int x = -6; x <= 6; x++) gc.cell[gc.Idx(x, 0)] = GuardCell.Open;
        for (int x = -6; x <= 0; x++) gc.view[gc.Idx(x, 0)] = true;
        var big = new BigObjectMemory(); big.Seen(7, "Workbench", 1.5f, 0f, new List<(int x, int y)> { (1, 0), (2, 0) });
        var noBase = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gc, (-4, 0), null, big), (5, 0));
        string nf = AIBaseGuard.KillFacts(null, (5, 0), noBase, "Slime", 9f);
        Program.Check(!noBase.comes && noBase.memChanged && noBase.memBig == 1 && nf.Contains("큰 물체 1개(Workbench)"), $"F3a. without a base the large object memory decided 'cannot come': the fact says so ('{nf}')");
        // F4: a tile in view is what it is now — the object's tiles in view (its centre out of view) are open
        gc.view[gc.Idx(1, 0)] = true; gc.view[gc.Idx(2, 0)] = true;
        var seenTiles = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gc, (-4, 0), null, big), (5, 0));
        Program.Check(seenTiles.comes && seenTiles.memBig == 0, "F4a. the remembered object's tiles in view (the object gone) are open: memory never covers a tile in view");
        // the AI standing inside a large footprint (passable for it): the entry is dropped (not closed out of view)
        big.Drop(7);
        Program.Check(!big.Has(1, 0) && big.Count == 0, "F4b. an object the AI stands inside is not kept as closed");
        big.Seen(8, "Statue", 0f, 0f, new List<(int x, int y)> { (0, 0), (0, 1) }); big.Clear();
        Program.Check(big.Count == 0 && !big.Has(0, 0), "F5. the large object memory clears (AIObjects.ClearMemory on another world, AIMemory.Ensure)");

        // F9: a stuck mark is no wall in the reach grid (an object is)
        Program.Check(AIBaseGuard.CellOf(true, true, true, false) == GuardCell.Open && AIBaseGuard.CellOf(true, true, false, false) == GuardCell.Closed && AIBaseGuard.CellOf(false, false, false, true) == GuardCell.Low,
            "F9. a tile blocked only by a stuck mark reads open in the reach grid (an object closes it, fence/water are low)");
    }

    // ------------------------------------------------------------------ F6: the words of an enemy within 1.5 tiles

    static void CloseWords()
    {
        // the AI on (0,0), a 1-tile wall (0,-1), the enemy on (0,-2) 1.3 tiles off, and an open way round of 8 tiles
        var g = GuardGrid.Make(-6, -6, 13, GuardCell.Closed);
        for (int x = 0; x <= 2; x++) { g.cell[g.Idx(x, 0)] = GuardCell.Open; g.cell[g.Idx(x, -2)] = GuardCell.Open; }
        for (int y = -2; y <= 0; y++) g.cell[g.Idx(3, y)] = GuardCell.Open;   // the way round: (0,0)->(3,0)->(3,-2)->(0,-2)
        var r = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0)), (0, -2));
        float dist = 1.3f;
        string f = AIBaseGuard.CloseNote(r, dist);
        Program.Check(r.comes && !r.touch && !r.doorBetween && AIBaseGuard.IsThreat(r, dist, 8f) && !f.Contains("위협으로 세지 않음") && f.Contains("걸어서 돌아올 길은 있어 위협으로 셈") && !f.Contains("문"),
            $"F6a. a wall between and a walk round: counted as a threat and said so, no door words ('{f.Trim()}')");
        // sealed behind the wall: not counted, still no door words
        g.cell[g.Idx(3, -1)] = GuardCell.Closed;
        var r2 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0)), (0, -2));
        string f2 = AIBaseGuard.CloseNote(r2, dist);
        Program.Check(!r2.comes && !AIBaseGuard.IsThreat(r2, dist, 8f) && f2.Contains("위협으로 세지 않음") && !f2.Contains("문"), $"F6b. a wall between, no way round: not counted, no door words ('{f2.Trim()}')");
        // a shut own door between: the door words
        g.cell[g.Idx(0, -1)] = GuardCell.OwnDoor;
        var r3 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(g, (0, 0)), (0, -2));
        string f3 = AIBaseGuard.CloseNote(r3, dist);
        Program.Check(r3.doorBetween && f3.Contains("닫힌 문") && f3.Contains("문이 열리거나 부서지면"), $"F6c. a shut door between: '{f3.Trim()}'");
    }

    // ------------------------------------------------------------------ F8: the enemy's tile

    static void Small()
    {
        // Ng5: the AI inside at (-4,-5), an enemy pressed on the far side of the shut door (-4,-6) at (-4,-6.4). Read by
        // its rounded tile the attack's walk went to the door tile with tolerance 1.0 and "arrived" at once inside (45 s
        // standing); read by its stand tile (the one reading, AIEdge.EnemyTile) the walk goes out through the door.
        (int, int) door = (-4, -6);
        Func<(int x, int y), bool> canStand = t => t != door && t != (-5, -6) && t != (-3, -6);
        var rounded = ((int)Math.Round(-4f), (int)Math.Round(-6.4f));
        var stand = AIBaseGuard.StandTile(-4f, -6.4f, canStand);
        float Dist((int x, int y) t) => (float)Math.Sqrt((t.x + 4f) * (t.x + 4f) + (t.y + 5f) * (t.y + 5f));
        Program.Check(rounded == door && Dist(rounded) <= 1.0f && stand == (-4, -7) && Dist(stand) > 1.0f,
            $"F8. the attack's walk target: rounded = the door tile, 'arrived' from inside at {Dist(rounded):F1}; the stand tile (-4,-7) is {Dist(stand):F1} off (the walk goes through the door)");
    }
}
