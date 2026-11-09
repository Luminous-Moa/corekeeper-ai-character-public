// C2: combat stage 2 (work/2026-11/2026-11-05-전투-단계2/1-구현.md). The charge prediction (AIChargeMath), the run-number
// contract of answers and actions (AIRunGate: the five asynchronous conditions of the original plan 단계 2), the one-swing
// rule (HitOnceLogic) and the movement-kind rule that replaced IsMoveDesc.
using System;
using System.Collections.Generic;

public static class CombatS2Tests
{
    static bool Near(float a, float b, float tol) => Math.Abs(a - b) <= tol;

    public static void Run()
    {
        Charge();
        RunGate();
        HitOnce();
        MoveKind();
        Review();
    }

    // 5-검수반영·회귀 (4-독립검수.md F1, F2, F4, the small items and HEAD decisions 1-2)
    static void Review()
    {
        Func<V2, bool> open = p => false;
        var me = new V2(0f, 0f);
        // F2: on the line, a blocker 2 tiles in front of the brute (x = 2) -> it stops before me: "short", not "out of the line"
        Func<V2, bool> block2 = p => Math.Round(p.x) == 2 && Math.Abs(p.y) < 0.6;
        var sh = AIChargeMath.Predict(Ant(0f), me, me, 0f, block2);
        var obj = new AIChargeMath.StopWhat(AIChargeMath.StopKind.Object, "GiantMushroom");
        string shText = AIChargeMath.OutcomeText(sh, 0.4f, true, true, null, obj), shCode = AIChargeMath.Code(sh, obj);
        Program.Check(!sh.hitsMe && sh.stops && sh.shortOfMe && sh.passGap < AIChargeMath.HitReach && Near(sh.elseTHit, 1.14f, 0.05f),
            $"F2. on the line, blocker in front: short of me (gap {sh.passGap:F2}, run {sh.runTiles:F1}), if it does not stop: hit at {sh.elseTHit:F2}s");
        Program.Check(shText.Contains("돌진선 안") && !shText.Contains("벗어남") && shText.Contains("먼저 부딪혀 멈춘다고 봄") && shText.Contains("멈추지 않으면 약 1.1") && shText.Contains("확인 못 함") && shText.Contains("GiantMushroom"),
            $"F2. sentence: on the line, stops first (assumed), the object and that it was not measured, what if it does not stop: {shText.Substring(shText.IndexOf("):") + 2)}");
        Program.Check(shCode.StartsWith("short0.0/obj") && shCode.Contains(">hit@1.1"), $"F2. code: {shCode}");
        // a wall instead: the same, the wall is not called unmeasured
        var wallW = new AIChargeMath.StopWhat(AIChargeMath.StopKind.Wall);
        string wt = AIChargeMath.OutcomeText(sh, 0.4f, true, true, null, wallW);
        Program.Check(wt.Contains("벽에 먼저 부딪혀") && !wt.Contains("확인 못 함") && AIChargeMath.Code(sh, wallW).StartsWith("short0.0/wall"), "F2. a wall stop: 'wall', no 'not measured' note");
        // off the line (2 tiles sideways) stays "miss": the escape sentence and code are unchanged; the old default says 벽·물체
        var two = AIChargeMath.Predict(Ant(0f), me, new V2(0f, 2f), 0.4f, p => Math.Round(p.x) <= -3);
        Program.Check(!two.shortOfMe && AIChargeMath.Code(two).StartsWith("miss") && AIChargeMath.Code(two).Contains("/wall") && AIChargeMath.OutcomeText(two, 0.4f, true, true).Contains("벽·물체에 부딪혀")
            && AIChargeMath.Code(two, obj).Contains("/obj"), $"F2. off the line: still 'miss' ({AIChargeMath.Code(two)} / {AIChargeMath.Code(two, obj)})");
        // behind the brute on its line extension: not "short of me"
        var behind = AIChargeMath.Predict(new ChargeView { phase = ChargePhase.Charging, phaseElapsed = 0.1f, pos = new V2(4f, 0f), dir = new V2(1f, 0f) }, me, me, 0f, open);
        Program.Check(!behind.hitsMe && !behind.shortOfMe, $"F2. behind a charge going away: not short of me (gap {behind.passGap:F2})");
        // ② R2 #1 reconstructed: brute (-25.88,36.10) anticipating at Claude at (-29.21,36.80), who steps left 1 to (-29.01,37.78)
        // after 0.40 s; the prediction counted tile (-27,36) as blocked (two object tiles in view). Old text: "벗어남(0.7칸)"
        // + "0.1칸 가서 부딪혀". Now: short of me, with the hit if that stop does not happen.
        var r2 = new ChargeView { phase = ChargePhase.Anticipation, phaseElapsed = 0.0f, pos = new V2(-25.88f, 36.10f), dir = new V2(-0.98f, 0.21f), aimAtMe = true, targetKnown = true };
        Func<V2, bool> t2736 = p => Math.Round(p.x) == -27 && Math.Round(p.y) == 36;
        var rr = AIChargeMath.Predict(r2, new V2(-29.21f, 36.80f), new V2(-29.01f, 37.78f), 0.40f, t2736);
        Program.Check(rr.stops && rr.runTiles < 0.5f && rr.shortOfMe && rr.elseTHit > 0f && AIChargeMath.Code(rr, obj).StartsWith("short"),
            $"F2. ② R2 #1 (blocked tile (-27,36)): {AIChargeMath.Code(rr, obj)} — was miss0.7/wall0.1; it ran 4.7 tiles and hit");
        string ns = AIChargeMath.NextChargeText(3f, 8f, true, obj);
        Program.Check(ns.Contains("물체 GiantMushroom") && ns.Contains("확인 못 함"), "F2. ready-phase sentence names what it would stop at");
        foreach (var s in new[] { shText, wt, ns }) Program.Check(!s.Contains("좋") && !s.Contains("추천") && !s.Contains("하세요") && !s.Contains("권"), "F2. no advice words in the new sentences");

        // F2 cause (R3 live run, TEST-ONLY line: a YellowFirefly tile in front of the brute, run 0.62): the charge prediction
        // leaves out tiles held only by critters; walking still counts them
        string cand = System.IO.File.ReadAllText(System.IO.Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIBrain.Candidates.cs"));
        string expl = System.IO.File.ReadAllText(System.IO.Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIObjects.cs"));
        Program.Check(expl.Contains("em.HasComponent<CritterCD>(e)") && cand.Contains("AIObjects.CritterOnly(pl, t)") && Count(cand, "AIChargeMath.Predict(") == Count(cand, "chargeBlockedV) : default") + Count(cand, ", delay, chargeBlockedV)")
            && Count(cand, "PredictChase(bv, me, delay, HitOnceSkill.Reach, chargeBlockedV)") == 1 && cand.Contains("if (blockedV(p)) return false;"),
            "F2. every charge prediction uses the set without critter-only tiles; the walking check (ClearWay) keeps them");

        // F1: chargers are read only near a character whose AI is on
        Program.Check(!AIChargeMath.Watched(new V2(0f, 0f), new List<V2>()) && AIChargeMath.Watched(new V2(0f, 0f), new List<V2> { new V2(30f, 0f), new V2(20f, 10f) })
            && !AIChargeMath.Watched(new V2(0f, 0f), new List<V2> { new V2(25f, 0f) }), $"F1. watched: none without an AI; within {AIChargeMath.WatchTiles:F0} tiles of an AI; not beyond");
        string scripts = System.IO.Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts");
        string mod = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "AIPlayerSpikeMod.cs"))
            + System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "AIPlayerSpikePatches.cs"));
        string watch = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "AIChargeWatch.cs"));
        string loop = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "AIBrain.Loop.cs"));
        Program.Check(!mod.Contains("AIChargeWatch.Frame"), "F1. the mod loop no longer reads chargers every frame (only the decision loop of an AI that is on)");
        Program.Check(Count(watch, "AIPlayerSpikeMod.Log(") == 1 && watch.Contains("private static void MLog(string s) { if (AITestSwitches.Test) AIPlayerSpikeMod.Log(s); }"),
            "F1. every CHARGE / BRUTE-HP line goes through MLog (test switch testcmds.on)");
        Program.Check(loop.Contains("try { AIChargeWatch.Frame(server); }") && loop.IndexOf("AIChargeWatch.Frame(server)") > loop.IndexOf("AITestSwitches.Refresh(now)"), "F1. the decision loop reads them after the test switches are refreshed");
        Program.Check(watch.Contains("if (_world != server)") && mod.Contains("AIChargeWatch.Reset("), "small: the watch forgets its chargers and events when the world changes / unloads");

        // range condition of the urgent re-ask
        Program.Check(AIRunGate.ChargeAsk(true, 5f, 8f) == AIRunGate.ChargeAskNow.Ask && AIRunGate.ChargeAsk(true, 9f, 8f) == AIRunGate.ChargeAskNow.Wait
            && AIRunGate.ChargeAsk(false, 5f, 8f) == AIRunGate.ChargeAskNow.Drop && AIRunGate.ChargeAsk(false, 12f, 8f) == AIRunGate.ChargeAskNow.Drop,
            "small: urgent re-ask within the close-fight range only; from farther it waits while the charge is on, dropped when it is over");
        // HEAD decision 1: the anticipation does not drop the waiting switch
        int a0 = loop.IndexOf("private static void ChargeTrigger("), a1 = loop.IndexOf("private static void CombatTrack(");
        string ct = a0 >= 0 && a1 > a0 ? loop.Substring(a0, a1 - a0) : "";
        Program.Check(ct.Length > 0 && !ct.Contains("_held = null") && !ct.Contains("HeldDropped") && ct.Contains("AIRunGate.ChargeAsk("), "HEAD 1. ChargeTrigger keeps the waiting switch (no _held = null, no HeldDropped)");
        Program.Check(AIRunGate.HeldSurvives(AnswerOutcome.StaleContinue) && !AIRunGate.HeldSurvives(AnswerOutcome.ApplyNow) && !AIRunGate.HeldSurvives(AnswerOutcome.Hold),
            "HEAD 1. the urgent answer settles it: applied / held replaces it, a stale one leaves it");

        // Re-review R1: a request in flight + a held long switch + an anticipation. Walk the brain's sequence with the rules it
        // uses (ChargeTrigger -> AbortRequest -> TryApplyHeld / ApplyOrHold)
        {
            string Abort(string held, string why) => AIRunGate.AbortDropsWaiting(why) ? null : held;
            // the anticipation drops the request (built before it, not urgent) but keeps the held switch
            bool drop = AIRunGate.AbortForAnticipation(true, 0.0f, false, 0.1f);
            string held = drop ? Abort("away:near", AIRunGate.AbortForCharge) : "away:near";
            Program.Check(drop && held == "away:near", "R1. request in flight + held switch + anticipation: the request is dropped, the held switch stays");
            // A: the urgent answer (a short move) arrives before the boundary: applied at once, it replaces the held switch
            var b = new Body(); b.Start("attack:7");
            Program.Check(AIRunGate.HeldApplyNow(true, false, 0.2f, 2f) == null, "R1a. before the boundary the held switch keeps waiting");
            var oa = Answer(b, "step", "step:left:2", 0, atBoundary: false, emergency: true);
            string heldA = AIRunGate.HeldSurvives(oa) ? held : null;
            Program.Check(oa == AnswerOutcome.ApplyNow && heldA == null, $"R1a. the urgent answer arrives: {oa}, the held switch is replaced (one owner)");
            // A': the urgent answer is a long action mid-motion in an emergency request: applied at once too, replaces it
            var oa2 = Answer(b, "retreat", "retreat:0_-1", 0, atBoundary: false, emergency: true);
            Program.Check(oa2 == AnswerOutcome.ApplyNow && !AIRunGate.HeldSurvives(oa2), "R1a'. a long urgent answer: applied at once, replaces the held switch");
            // B: the boundary comes before the urgent answer: the held switch starts then (the model's choice is not lost)
            Program.Check(AIRunGate.HeldApplyNow(true, true, 0.1f, 2f) == "boundary" && AIRunGate.HeldApplyNow(false, false, 0.1f, 2f) == "idle" && AIRunGate.HeldApplyNow(true, false, 2.1f, 2f) == "timeout",
                "R1b. the held switch starts at the boundary (or idle / 2 s) while the urgent request is in flight");
            // C: a stale urgent answer (continue of a run that ended) leaves the held switch, which then starts at the boundary
            var oc = Answer(b, "continue", "attack:7", 99);
            Program.Check(oc == AnswerOutcome.StaleContinue && AIRunGate.HeldSurvives(oc), "R1c. a stale urgent answer leaves the held switch");
            // D: every other cause of dropping a request still drops the waiting choices
            foreach (var why in new[] { "timeout", "AI down", "no goal", "self-defence over", "reset", "goal cleared", "new goal", "free time", "prefetch: 'x' Done" })
                Program.Check(AIRunGate.AbortDropsWaiting(why) == (why == "AI down" || why == "reset" || why == "goal cleared" || why == "new goal"), $"R1d. '{why}' preserves waiting choices unless their target context actually changed");
            string brain = System.IO.File.ReadAllText(System.IO.Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIBrain.cs"));
            Program.Check(brain.Contains("if (AIRunGate.AbortDropsWaiting(why))") && loop.Contains("AbortRequest(AIRunGate.AbortForCharge)") && loop.Contains("AIRunGate.HeldApplyNow(cur != null, AIMotion.AtBoundary, now - _heldAt, MaxHoldSeconds)"),
                "R1. wiring: AbortRequest asks AbortDropsWaiting, ChargeTrigger aborts with AbortForCharge, TryApplyHeld uses HeldApplyNow");
            // the wall-break note only for a wall stop
            Program.Check(!AIChargeMath.OutcomeText(sh, 0.4f, true, true, null, obj).Contains("부서짐") && AIChargeMath.OutcomeText(sh, 0.4f, true, true, null, wallW).Contains("부서짐")
                && !AIChargeMath.NextChargeText(3f, 8f, true, new AIChargeMath.StopWhat(AIChargeMath.StopKind.Tile, "water")).Contains("부서짐"), "re-review: 'walls around break' only when it stops at a wall");
        }

        // F4: switch classes and the movement-switch counts
        Program.Check(AIRunGate.SwitchClass("short", false) == "short" && AIRunGate.SwitchClass("boundary", true) == "boundary" && AIRunGate.SwitchClass("timeout", false) == "mid"
            && AIRunGate.SwitchClass("emergency", false) == "emergency" && AIRunGate.SwitchClass("boundary", false) == "mid", "F4. short switches are their own class; boundary / timeout / emergency as before");
        Program.Check(!AIRunGate.SimilarMove(true, "step", "short") && AIRunGate.ShortMove(true, "step", "short") && AIRunGate.SimilarMove(true, "away", "boundary") && AIRunGate.SimilarMove(true, "step", "emergency")
            && !AIRunGate.ShortMove(false, "step", "short"), "F4. step -> step at once counted apart from 'between movement options'");
        Program.Check(loop.Contains("shortKind && !AIMotion.AtBoundary ? \"short\" : S._reqEmergency ? \"emergency\""), "F4. a short action at once is 'short' also in an emergency request");

        // HEAD decision 2: the switch line
        var sw = new List<(string from, string fromKind, string to, string toKind, float ran)> { ("짧은 이동", "step", "적 기준 왼쪽 2칸", "step", 0.3f), ("공격", "attack", "한 번 치기", "hit", 1.2f), ("한 번 치기", "hit", "짧은 이동", "step", 0.4f) };
        string old = AIRunGate.SwitchHistoryText(sw, false);
        Program.Check(old == $"최근 10초 동안 행동을 3번 바꿈: 짧은 이동({0.3f:F1}초 만에)→적 기준 왼쪽 2칸, 공격({1.2f:F1}초 만에)→한 번 치기, 한 번 치기({0.4f:F1}초 만에)→짧은 이동. 바꿀 때마다 새 행동은 처음부터 시작했다.",
            "HEAD 2. other inputs: the line as before (same text)");
        string bf = AIRunGate.SwitchHistoryText(sw, true);
        Program.Check(bf.Contains("긴 동작") && bf.Contains("공격(") && bf.Contains("1번:") && bf.Contains("처음부터 다시 시작") && bf.Contains("끼리 바꾼 것 2번") && !bf.Contains("짧은 이동(0") && !bf.Contains("새 행동은 처음부터 시작했다"),
            $"HEAD 2. close fight with a charger: long switches listed with the restart fact, short-short only counted: {bf}");
        Program.Check(!AIRunGate.SwitchHistoryText(new List<(string, string, string, string, float)> { ("짧은 이동", "step", "x", "hit", 0.2f) }, true).Contains("처음부터"), "HEAD 2. only short switches: no restart sentence");
        Program.Check(AIRunGate.SwitchHistoryText(new List<(string, string, string, string, float)>(), true) == "" && AIRunGate.KindOfKey("step:left:2") == "step" && AIRunGate.KindOfKey("hold") == "hold", "HEAD 2. none: empty; kind of a key");

        // small: one swing — after the swing started nothing is pressed (also with the timer unreadable)
        var h = new HitOnceLogic(); bool pressedAfter = false;
        for (float t = 0f; t < 0.5f && !h.Done; t += 0.02f) { bool st = t >= 0.10f; bool p = h.Step(t, false, st, 0f, true); if (st && p) pressedAfter = true; }
        Program.Check(!pressedAfter && h.Started, "small: timer unreadable, the swing seen at 0.10 s: no press after it (no room for a second swing)");
        // small: a kill only when the health is read at or below 0
        Program.Check(AIRunGate.KilledSeen(true, true, 0) && AIRunGate.KilledSeen(true, true, -5) && !AIRunGate.KilledSeen(true, false, -1) && !AIRunGate.KilledSeen(false, false, -1) && !AIRunGate.KilledSeen(true, true, 3),
            "small: kill = health read and <= 0 (no health component, being destroyed: not a kill)");
        string ss = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "AIShortSkills.cs"));
        Program.Check(ss.Contains("AIRunGate.KilledSeen(true, hasHp, hp)") && !ss.Contains("(destroyed && hp <= 0)"), "small: AITargetEnd uses that rule");
        // small: the weapon breaks / is replaced during one swing
        var w1 = new HitOnceLogic(); int pr = 0; bool brokeOnce = false, brokeTwice = false;
        for (float t = 0f; t < 1.2f && !w1.Done; t += 0.02f)
        {
            bool st = t >= 0.10f; bool broken = t >= 0.2f;
            bool b = w1.WeaponCheck(true, broken, false); if (b) { if (brokeOnce) brokeTwice = true; brokeOnce = true; }
            if (w1.Done) break;
            if (w1.Step(t, true, st, st ? Math.Max(0f, 0.4f - (t - 0.10f)) : 0f, true)) pr++;
        }
        Program.Check(w1.Done && !w1.Failed && brokeOnce && !brokeTwice && pr <= 6, $"small: the weapon breaks after the swing started: told once (hits recorded as broken), still one swing ({w1.Reason})");
        var w2 = new HitOnceLogic(); w2.Step(0f, true, false, 0.2f, true); bool w2b = w2.WeaponCheck(false, false, false); bool w2p = w2.Step(0.04f, true, false, 0.2f, true);
        Program.Check(w2.Done && w2.Failed && !w2b && !w2p && w2.Reason.Contains("무기"), $"small: the weapon gone before the swing: fails, nothing pressed after ({w2.Reason})");
        var w3 = new HitOnceLogic(); w3.Step(0f, true, true, 0.4f, true); w3.WeaponCheck(false, false, false);
        Program.Check(w3.Done && !w3.Failed && !w3.Step(0.1f, true, true, 0.3f, true), $"small: replaced after the swing started: the one swing is over ({w3.Reason})");
        var w4 = new HitOnceLogic(); bool w4b = w4.WeaponCheck(true, true, true);
        Program.Check(!w4b && !w4.Done, "small: broken already at the start: nothing new to tell, the swing goes on");
        Program.Check(ss.Contains("_logic.WeaponCheck(inSlot == _weaponId, brokenNow, _brokenAtStart)"), "small: HitOnceSkill checks its weapon slot every frame");
    }

    static int Count(string s, string sub) { int n = 0, i = 0; while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; } return n; }

    // brute at (4,0) east of me at (0,0), anticipation just started, aimed at me
    static ChargeView Ant(float elapsed) => new ChargeView { phase = ChargePhase.Anticipation, phaseElapsed = elapsed, pos = new V2(4f, 0f), dir = new V2(-1f, 0f), aimAtMe = true, targetKnown = true };

    static void Charge()
    {
        var me = new V2(0f, 0f);
        Func<V2, bool> open = p => false;
        Func<V2, bool> wallWest = p => Math.Round(p.x) <= -3;   // a wall from x = -3 westward
        // staying: the charge leaves at 0.5 s and reaches my body (1.05) after 2.95 tiles: 0.5 + 0.3 + (2.95 - 0.9) / 6
        var stay = AIChargeMath.Predict(Ant(0f), me, me, 0f, open);
        Program.Check(stay.valid && stay.hitsMe && Near(stay.tHit, 0.5f + 0.3f + (2.95f - 0.9f) / 6f, 0.04f), $"staying in the anticipation: hit at {stay.tHit:F2}s (expected ~1.14)");
        // one tile sideways starting 0.4 s from now: the aim follows until 0.45 s, so the line ends ~0.7 from me: hit
        var one = AIChargeMath.Predict(Ant(0f), me, new V2(0f, 1f), 0.4f, open);
        Program.Check(one.hitsMe, $"1 tile sideways after a 0.4 s delay: the aim follows to 0.45 s and the move is too short (hit={one.hitsMe}, gap {one.passGap:F2})");
        // two tiles sideways: out of the line when it passes
        var two = AIChargeMath.Predict(Ant(0f), me, new V2(0f, 2f), 0.4f, open);
        Program.Check(!two.hitsMe && two.passGap > AIChargeMath.HitReach, $"2 tiles sideways after a 0.4 s delay: out of the line (gap {two.passGap:F2})");
        // the same with a wall west: the charge runs on and stops at the wall (vulnerable), not at me
        var wall = AIChargeMath.Predict(Ant(0f), me, new V2(0f, 2f), 0.4f, wallWest);
        Program.Check(!wall.hitsMe && wall.stops && !wall.stopsAtMe && wall.runTiles > 5.5f && wall.runTiles < 7f, $"2 tiles sideways, wall 3 tiles behind me: the charge stops at the wall after {wall.runTiles:F1} tiles");
        Program.Check(AIChargeMath.Code(wall).StartsWith("miss") && AIChargeMath.Code(wall).Contains("/wall") && AIChargeMath.Code(stay).StartsWith("hit@"), $"codes: {AIChargeMath.Code(wall)} / {AIChargeMath.Code(stay)}");
        // anticipation past the lock (0.47 s): the aim no longer follows; moving 1 tile sideways at once escapes
        var late = AIChargeMath.Predict(Ant(0.47f), me, new V2(0f, 1.5f), 0.0f, open);
        Program.Check(!late.hitsMe, $"after the aim lock a 1.5-tile side move with no delay is out of the line (gap {late.passGap:F2})");
        // HEAD review item 12: the trajectory's closest approach decides, not the end point — a completed 1.1-tile side move
        // (just over the 1.05 reach) is not counted as a hit; a 1.0-tile one is
        var p11 = AIChargeMath.Predict(Ant(0.47f), me, new V2(0f, 1.1f), 0f, open);
        var p10 = AIChargeMath.Predict(Ant(0.47f), me, new V2(0f, 1.0f), 0f, open);
        Program.Check(!p11.hitsMe && p10.hitsMe, $"boundary: a finished 1.1-tile move escapes (gap {p11.passGap:F2}), a 1.0-tile one does not");
        // a move that is still under way when the charge arrives: hit before arriving (slow start, delay 0.6 s, 3 tiles)
        var mid = AIChargeMath.Predict(new ChargeView { phase = ChargePhase.Charging, phaseElapsed = 0.3f, pos = new V2(2.5f, 0f), dir = new V2(-1f, 0f) }, me, new V2(0f, 3f), 0.6f, open);
        Program.Check(mid.hitsMe && mid.tHit < 0.6f, $"a move that starts too late is hit before it gets away (at {mid.tHit:F2}s)");
        // ② fix: the continuing attack follows the enemy. 1.5 tiles off a charge coming from 4 tiles: standing escapes, walking
        // at the enemy to swing does not (S2 run: a continuing attack chosen during a charge was predicted as standing)
        var chV = new ChargeView { phase = ChargePhase.Charging, phaseElapsed = 0.1f, pos = new V2(4f, 0f), dir = new V2(-1f, 0f), aimAtMe = true };
        var standP = AIChargeMath.Predict(chV, new V2(0f, 1.5f), new V2(0f, 1.5f), 0.3f, open);
        var chaseP = AIChargeMath.PredictChase(chV, new V2(0f, 1.5f), 0.3f, 1.3f, open);
        Program.Check(!standP.hitsMe && chaseP.hitsMe, $"following attack: standing 1.5 tiles off escapes (gap {standP.passGap:F2}), walking at the enemy to swing is hit (hit={chaseP.hitsMe} at {chaseP.tHit:F2}s, gap {chaseP.passGap:F2})");
        var farP = AIChargeMath.PredictChase(chV, new V2(0f, 6f), 0.3f, 1.3f, open);
        Program.Check(!farP.hitsMe, $"following attack from 6 tiles off: not reached in time (gap {farP.passGap:F2})");
        // already charging, I am 2 tiles off its line: not hit
        var ch = new ChargeView { phase = ChargePhase.Charging, phaseElapsed = 0.2f, pos = new V2(4f, 0f), dir = new V2(-1f, 0f), aimAtMe = true };
        var off = AIChargeMath.Predict(ch, new V2(0f, 2f), new V2(0f, 2f), 0f, open);
        Program.Check(!off.hitsMe && Near(off.passGap, 2f, 0.3f), $"charging past me 2 tiles off the line: not hit (gap {off.passGap:F2})");
        // not under way: no prediction
        Program.Check(!AIChargeMath.Predict(new ChargeView { phase = ChargePhase.Ready, pos = new V2(4f, 0f) }, me, me, 0f, open).valid, "ready phase: no charge prediction");
        // open behind
        float ob = AIChargeMath.OpenBehind(me, new V2(-1f, 0f), 8f, wallWest);
        Program.Check(ob >= 2.25f && ob <= 3.0f && AIChargeMath.OpenBehind(me, new V2(1f, 0f), 8f, open) > 8f, $"open behind: wall at {ob:F2} tiles; nothing within 8 tiles the other way");
        // sentences: assumptions written, vulnerability time, no advice
        string t = AIChargeMath.OutcomeText(two, 0.4f, true, true);
        string tw = AIChargeMath.OutcomeText(wall, 0.4f, true, true);
        string th = AIChargeMath.OutcomeText(stay, 0.4f, true, true);
        Program.Check(t.StartsWith("예상(가정:") && t.Contains("0.45초") && t.Contains("벗어남") && th.Contains("못 벗어남") && tw.Contains("취약 3.6초") && tw.Contains("부서짐"), "outcome sentences: assumption first, escape / no escape, wall -> vulnerable 3.6 s, the wall breaks");
        foreach (var s in new[] { t, tw, th, AIChargeMath.NextChargeText(3f, 8f, true), AIChargeMath.NextChargeText(9f, 8f, true) })
            Program.Check(!s.Contains("좋") && !s.Contains("추천") && !s.Contains("하세요") && !s.Contains("권"), $"no advice words: {s.Substring(0, Math.Min(40, s.Length))}…");
        var vul = new ChargeView { phase = ChargePhase.Vulnerable, vulnLeft = 2.0f };
        Program.Check(AIChargeMath.TimeText(0.5f, vul, 1.0f, 1.3f).Contains("취약 남은 약 1.5초") && AIChargeMath.TimeText(0.5f, vul, 1.0f, 1.3f).Contains("휘두르는 거리 안")
            && AIChargeMath.TimeText(2.5f, vul, 2.0f, 1.3f).Contains("취약 끝남") && AIChargeMath.TimeText(2.5f, vul, 2.0f, 1.3f).Contains("거리 밖"), "time sentence: vulnerability left then, distance then (in / out of reach)");
    }

    // A tiny model of the body for the five conditions: the running run (number, key) or none.
    sealed class Body { public int seq; public int run; public string key = ""; public bool running; public int Start(string k) { run = ++seq; key = k; running = true; return run; } public void End() { running = false; } }

    static AnswerOutcome Answer(Body b, string kind, string key, int answerRun, bool atBoundary = true, bool emergency = false)
        => AIRunGate.Decide(kind, key, answerRun, b.running, b.run, b.key, atBoundary, emergency);

    static void RunGate()
    {
        // 1. the answer comes after a one-swing / short move has ended: its late "continue" does not restart it
        var b = new Body();
        int r5 = b.Start("hit:7");                 // a request is built while run r5 runs: its continue option carries r5
        b.End();                                   // the swing ends before the answer
        Program.Check(Answer(b, "continue", "hit:7", r5) == AnswerOutcome.StaleContinue && AIRunGate.ContinueStale(r5, b.running, b.run, "hit:7", b.key) != null,
            $"1. late continue after the action ended: stale ({AIRunGate.ContinueStale(r5, b.running, b.run, "hit:7", b.key)})");
        // 2. a new run with the same target and key started since: the old continue neither continues it nor (being stale,
        //    discarded before any apply) cancels a waiting switch
        int r6 = b.Start("hit:7");
        string why2 = AIRunGate.ContinueStale(r5, b.running, b.run, "hit:7", b.key);
        Program.Check(Answer(b, "continue", "hit:7", r5) == AnswerOutcome.StaleContinue && why2 != null && why2.Contains("같은 키의 새 실행"), $"2. continue of run #{r5} while run #{r6} of the same key runs: stale ({why2})");
        Program.Check(Answer(b, "continue", "hit:7", r6) == AnswerOutcome.Continue, "2b. continue of the running run itself: continue");
        // 3. a held switch was applied, then an answer to an older request arrives: its continue is stale, its new choice is
        //    checked as any new choice (a short action applies at once; the action that runs now is continued, not restarted)
        b = new Body();
        int r3 = b.Start("attack:7");
        Program.Check(Answer(b, "away", "away:near", 0, atBoundary: false) == AnswerOutcome.Hold, "3a. a long action switch in mid-motion waits for the boundary (as before)");
        int r4 = b.Start("away:near");            // the held switch applied at the boundary
        Program.Check(Answer(b, "continue", "attack:7", r3) == AnswerOutcome.StaleContinue, "3b. the older request's continue (attack run) after the held switch: stale");
        Program.Check(Answer(b, "step", "step:left:2", 0, atBoundary: false) == AnswerOutcome.ApplyNow, "3c. the older request's short move: applied at once (no boundary wait, no 2 s forced apply)");
        Program.Check(Answer(b, "away", "away:near", 0, atBoundary: false) == AnswerOutcome.Continue && b.run == r4, "3d. the older request choosing what runs now: continue, no restart");
        // 4. impossible to run (target gone / out of reach / blocked) is the skill's own reason (HitOnce below, StepSkill);
        //    the rule never turns a choice into another action: every outcome is about the chosen kind itself
        foreach (var k in new[] { "step", "hit", "approach", "hold", "attack", "away", "retreat", "eat", "wait" })
        {
            var o1 = Answer(new Body(), k, k + ":1", 0); var o2 = Answer(b, k, k + ":1", 0, atBoundary: false); var o3 = Answer(b, k, k + ":1", 0, atBoundary: false, emergency: true);
            Program.Check(o1 == AnswerOutcome.ApplyNow && (AIRunGate.IsShort(k) ? o2 == AnswerOutcome.ApplyNow : o2 == AnswerOutcome.Hold) && o3 == AnswerOutcome.ApplyNow,
                $"4. {k}: idle -> apply, mid-motion -> {(AIRunGate.IsShort(k) ? "apply at once" : "hold")}, emergency -> apply (got {o1}/{o2}/{o3})");
        }
        // 5. a duplicate answer (applied twice) starts nothing twice: the second sees its own key running
        b = new Body();
        Program.Check(Answer(b, "step", "step:left:2", 0) == AnswerOutcome.ApplyNow, "5a. first copy of an answer: apply");
        b.Start("step:left:2");
        Program.Check(Answer(b, "step", "step:left:2", 0) == AnswerOutcome.Continue, "5b. second copy of the same answer: continue (no second start)");
        Program.Check(AIRunGate.ContinueStale(0, false, 0, "x", "") == "계속할 기술이 이미 끝남", "5c. a continue with no run number (older build) keeps the old sentence");

        // HEAD review item 9: "hit again" while a swing of the same target runs is queued (a new run after it), never a
        // continue; with the swing already over it swings at once; a continue never makes a new swing
        b = new Body(); int h1 = b.Start("hit:7");
        Program.Check(Answer(b, "hit", "hitnext:7", 0) == AnswerOutcome.QueueAfter, "9a. hit again during the swing: queued after it");
        Program.Check(Answer(b, "continue", "hit:7", h1) == AnswerOutcome.Continue, "9b. continue of the swing: continue (no new swing)");
        b.End();
        Program.Check(Answer(b, "hit", "hitnext:7", 0) == AnswerOutcome.ApplyNow && AIRunGate.HitKeyOf("hitnext:7") == "hit:7", "9c. hit again after the swing ended: swing now (key hit:7)");
        b.Start("step:left:2");
        Program.Check(Answer(b, "hit", "hitnext:7", 0) == AnswerOutcome.ApplyNow, "9d. hit again while something else runs: applied at once as a short action");

        // item 4: one owner of the waiting switch — only a stale answer leaves it
        foreach (AnswerOutcome o in Enum.GetValues(typeof(AnswerOutcome)))
            Program.Check(AIRunGate.HeldSurvives(o) == (o == AnswerOutcome.StaleContinue), $"4b. waiting switch after a {o} answer: {(AIRunGate.HeldSurvives(o) ? "kept" : "replaced/dropped")}");

        // item 7: anticipations one after another do not drop every answer. Simulate: answers take 0.25 s; anticipations
        // of five brutes start every 0.1 s from t = 0.05; an anticipation drops the request in flight only per the rule.
        {
            float reqStart = 0f; bool inFlight = true, urgent = false; int answers = 0, aborts = 0; float tAnswer = -1f;
            var ants = new List<float> { 0.05f, 0.15f, 0.25f, 0.35f, 0.45f };
            bool need = false;
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                foreach (var a in ants) if (Math.Abs(a - t) < 0.005f)
                {
                    if (AIRunGate.AbortForAnticipation(inFlight, reqStart, urgent, a)) { aborts++; inFlight = false; need = true; }
                    else need = true;
                }
                if (inFlight && t - reqStart >= 0.25f) { answers++; if (tAnswer < 0f) tAnswer = t; inFlight = false; }
                if (!inFlight && need) { inFlight = true; reqStart = t; urgent = true; need = false; }
            }
            Program.Check(aborts == 1 && answers >= 2 && tAnswer > 0f && tAnswer < 0.4f, $"7. five anticipations 0.1 s apart: {aborts} drop, first answer at {tAnswer:F2}s, {answers} answers (the urgent re-ask is not dropped again)");
            Program.Check(!AIRunGate.AbortForAnticipation(true, 0.3f, false, 0.2f) && !AIRunGate.AbortForAnticipation(false, 0f, false, 0.2f), "7b. a request sent after the anticipation, or none in flight: nothing dropped");
        }

        // item 5: failures by cause
        Program.Check(AIRunGate.Classify(true, 200, false, true, false) == FailKind.Refusal && AIRunGate.Classify(true, 200, false, false, false) == FailKind.NoAnswer
            && AIRunGate.Classify(true, 429, false, false, false) == FailKind.RateLimit && AIRunGate.Classify(true, 503, false, false, false) == FailKind.Server
            && AIRunGate.Classify(false, 0, false, false, false) == FailKind.Network && AIRunGate.Classify(true, 401, false, false, false) == FailKind.Client
            && AIRunGate.Classify(true, 0, false, false, true) == FailKind.Timeout, "5d. failure causes: refusal, empty, rate limit, server, network, client, timeout");
        Program.Check(AIRunGate.RetryDelay(FailKind.Refusal, true, 0.25f) == 0.25f && AIRunGate.RetryDelay(FailKind.Server, true, 0.25f) == 1f && AIRunGate.RetryDelay(FailKind.RateLimit, true, 0.25f) == 3f
            && AIRunGate.RetryDelay(FailKind.Refusal, false, 0.25f) == 3f, "5e. retry waits: in combat refusal at the floor, server 1 s, rate limit 3 s; out of combat 3 s as before");
        Program.Check(!AIRunGate.CountsTowardStop(FailKind.Refusal, true) && AIRunGate.CountsTowardStop(FailKind.Refusal, false) && AIRunGate.CountsTowardStop(FailKind.Client, true)
            && AIRunGate.UrgentMayBypassRetry(FailKind.Refusal) && !AIRunGate.UrgentMayBypassRetry(FailKind.RateLimit), "5f. a refusal in combat does not stop the goal; an anticipation may ask again after a refusal, not after a rate limit");

        // item 8: how a move started
        Program.Check(MoveStart.Prior(0f, 0f, 1f, 0f) == "standstill" && MoveStart.Prior(5f, 0.2f, 1f, 0f) == "same" && MoveStart.Prior(0f, 5f, 1f, 0f) == "turn" && MoveStart.Prior(-5f, 0f, 1f, 0f) == "reverse",
            "8. prior motion: standstill / same / turn / reverse");
        Program.Check(MoveStart.Along(0.1f, 0.01f, 1f, 0f) && !MoveStart.Along(0f, 0.1f, 1f, 0f) && !MoveStart.Along(0.005f, 0f, 1f, 0f), "8b. moving along the new direction: the frame's displacement goes that way");
    }

    static void HitOnce()
    {
        // normal: press until the swing starts, release at once, done when the swing is over -> one press window
        var h = new HitOnceLogic(); int pressFrames = 0; bool releasedAfter = true; bool seenStart = false;
        for (float t = 0f; t < 1.2f && !h.Done; t += 0.02f)
        {
            bool started = t >= 0.10f;               // the game starts the swing at 0.10 s
            float left = started ? Math.Max(0f, 0.4f - (t - 0.10f)) : 0f;
            bool p = h.Step(t, true, started, left, true);
            if (p) { pressFrames++; if (seenStart) releasedAfter = false; }
            if (started) seenStart = true;
        }
        Program.Check(h.Done && !h.Failed && releasedAfter && pressFrames >= 4 && pressFrames <= 6, $"one swing: pressed {pressFrames} frames until it started, released after, done ({h.Reason})");
        // a previous swing still running: keep pressing until the new one starts (the cooldown), then one swing
        var h2 = new HitOnceLogic(); int presses2 = 0;
        for (float t = 0f; t < 1.2f && !h2.Done; t += 0.02f) { bool st = t >= 0.3f; if (h2.Step(t, true, st, st ? Math.Max(0f, 0.4f - (t - 0.3f)) : 0.2f, true)) presses2++; }
        Program.Check(h2.Done && !h2.Failed && presses2 >= 14, $"one swing after the previous one's cooldown: pressed {presses2} frames, done");
        // out of reach before it swings: fails with the reason, no press after
        var h3 = new HitOnceLogic();
        h3.Step(0f, true, false, 0f, true); bool p3 = h3.Step(0.04f, true, false, 0f, false);
        Program.Check(h3.Done && h3.Failed && !p3 && h3.Reason.Contains("거리 밖"), $"4. target out of reach before the swing: fails ({h3.Reason})");
        // no swing within 1 s: fails, does not press forever
        var h4 = new HitOnceLogic(); bool p4 = true;
        for (float t = 0f; t < 1.5f && !h4.Done; t += 0.05f) p4 = h4.Step(t, true, false, 0f, true);
        Program.Check(h4.Done && h4.Failed && !p4, $"5. no swing start within {HitOnceLogic.StartWait:F1}s: fails ({h4.Reason})");
        // timer unreadable: presses at most 0.35 s (< one 0.4 s cooldown: at most one swing), then done
        var h5 = new HitOnceLogic(); float lastPress = -1f;
        for (float t = 0f; t < 1.0f && !h5.Done; t += 0.02f) if (h5.Step(t, false, false, 0f, true)) lastPress = t;
        Program.Check(h5.Done && !h5.Failed && lastPress < HitOnceLogic.NoTimerPress && lastPress > 0.2f, $"5. timer unreadable: pressed until {lastPress:F2}s (< {HitOnceLogic.NoTimerPress:F2}), then done ({h5.Reason})");
        Program.Check(!h5.Step(2f, true, true, 0.3f, true), "5. after it is done it never presses again");
        // composite "hit then move": the hit stage ends 0.12 s after the swing started (one press window, then the move)
        var h6 = new HitOnceLogic { EndAfterStart = 0.12f }; int p6 = 0; float end6 = -1f;
        for (float t = 0f; t < 1.0f && !h6.Done; t += 0.02f) { bool st = t >= 0.1f; if (h6.Step(t, true, st, st ? Math.Max(0f, 0.4f - (t - 0.1f)) : 0f, true)) p6++; if (h6.Done) end6 = t; }
        Program.Check(h6.Done && !h6.Failed && end6 >= 0.21f && end6 <= 0.27f && p6 <= 6, $"composite hit stage: ends {end6:F2}s (swing seen at the first frame >= 0.10 s; 0.02 s frames), pressed {p6} frames ({h6.Reason})");
        Program.Check(AIRunGate.IsShort("combo") && AIRunGate.Decide("combo", "combo:hit-left:2", 0, true, 3, "attack:7", false, false) == AnswerOutcome.ApplyNow
            && AIRunGate.Decide("continue", "combo:hit-left:2", 4, true, 4, "combo:hit-left:2", false, false) == AnswerOutcome.Continue
            && AIRunGate.Decide("continue", "combo:hit-left:2", 4, true, 5, "combo:hit-left:2", false, false) == AnswerOutcome.StaleContinue,
            "composite: applied at once mid-motion; continue only for its own run");
    }

    static void MoveKind()
    {
        // the kinds the old text rule counted as moves (거리 벌리기, 물러나기, 탐험 이동, 파 들어가기, 기억한 목표, 동료/호스트 따라가기,
        // 호스트 쪽으로 가기) plus the new short moves; not attacks (the old rule counted "동료 돕기(공격)"), mining, pickups, eat, wait
        foreach (var k in new[] { "away", "retreat", "explore", "dig", "follow", "step", "approach" }) Program.Check(AIRunGate.IsMoveKind(k), $"move kind: {k}");
        foreach (var k in new[] { "attack", "hit", "hold", "mine", "pickup", "eat", "wait", "continue", "craft", "break" }) Program.Check(!AIRunGate.IsMoveKind(k), $"not a move kind: {k}");
    }
}
