// AIBrain's decision loop (refactor S5, moved unchanged from AIDecisions.cs): per tick, motion boundaries,
// self-defence, request and response, candidates (BuildCandidates; the generator is AIBrain.Candidates.cs) and input,
// settings. The preparation path is in AIBrain.Prep.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;

public static partial class AIBrain
{
    // ------------------------------------------------------------------ per-tick

    public static void Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(player)) return;
        AITestSwitches.Refresh(now);
        try { AIChargeWatch.Frame(server); } catch (Exception e) { AIPlayerSpikeMod.LogError("charge watch failed", e); }   // once per frame (combat stage 2)
        try { AIBaseProbe.Frame(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("base probe failed", e); }
        try { AIEdge.Frame(server, player); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge frame failed", e); }   // 단계 2 G9: finished options   // test only, off unless /ai baseprobe (코어-안전지대 단계 1)
        try { AIMotion.Frame(server, player, AIBody.Current, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("motion: frame failed", e); }
        // remembered-frontier search running as a background job: a few hundred tiles per frame (~3-5 ms)
        if (S._fJob != null)
        {
            try
            {
                S._fJobFrames++;
                if (S._fJob.Step(FrontierTilesPerFrame))
                {
                    FrontierJobDone(S, now, $"in the background over {S._fJobFrames} frames");
                    if (S._fPending)
                    {
                        S._fPending = false;
                        if (Goal.kind != GoalKind.None)
                        {
                            _needCall = true; _needReason = "탐색 끝 찾기 끝남(기억한 탐색 끝 후보가 생김)";
                            // the 2 s placeholder wait ("only 'wait' is possible") would hold this call back by the
                            // running-skill gap until it ends (a new explore goal stood 2.0 s, ai2 polish 2): stop it
                            if (AIBody.Current is WaitSkill && AIBody.Current.Status == SkillStatus.Running) AIBody.Stop(server, player, now, "탐색 끝 찾기 끝남");
                        }
                    }
                }
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("brain: frontier job failed", e); S._fJob = null; S._fPending = false; }
        }
        try { FreeTime(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: free time check failed", e); }
        if (Goal.kind == GoalKind.None)
        {
            // HEAD decision 2026-11-04: with no goal, defend itself (self-defence episode) instead of standing still
            // while being hit; back to idle without calls once the threat is gone.
            if (!SelfDefence(server, player, now))
            {
                if (_req != null) AbortRequest("no goal");
                return;
            }
        }

        // Down (health 0, respawning at the core): no skill can act, so no calls until the AI is back up.
        int hpNow = em.HasComponent<HealthCD>(player) ? em.GetComponentData<HealthCD>(player).health : 1;
        if (hpNow <= 0)
        {
            if (!_wasDown)
            {
                _wasDown = true;
                AIPlayerSpikeMod.Log("BRAIN: AI is down (health 0); no decision calls until it is back up");
                AbortRequest("AI down");
                CombatTrack(now, false, "쓰러짐");
                if (Verbose) AIPlayerSpikeMod.Reply("AI: 쓰러졌습니다. 다시 일어날 때까지 판단을 쉽니다.", ChatKind.Status);
            }
            return;
        }
        if (_wasDown)
        {
            _wasDown = false;
            _seenEnemies.Clear();
            _hpAtLastCall = -1;
            _needCall = true;
            _needReason = "쓰러졌다가 코어에서 다시 일어남";
            AIPlayerSpikeMod.Log($"BRAIN: AI is back up (health {hpNow}); deciding again");
        }

        // motion boundaries: a held switch starts at the next boundary; an emergency asks again at once
        if (_inCombat && now - _lastEmergencyCheck >= 0.1f)
        {
            _lastEmergencyCheck = now;
            string emg = EmergencyNow(server, player, hpNow);
            if (emg != null)
            {
                if (_held != null) { HeldDropped++; AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch dropped (emergency: {emg}): {Trim(_held.desc, 60)}"); _held = null; }
                if (!_needCall || !EmergencyRule(_needReason, S._needEmergency)) { _needCall = true; _needReason = emg; S._needEmergency = true; }
            }
        }
        ChargeTrigger(server, player, now);
        // combat stage 2 (HEAD review item 4): the waiting switch and a newly arrived answer are settled in one place —
        // when an answer is ready this frame it goes first (it replaces, drops or leaves the waiting switch, AIRunGate)
        if (_held != null && !(_req != null && _req.isDone)) TryApplyHeld(server, player, now);

        // goal done (LLM follow 3 min, store/home/farm/cook, gather/craft count): AIGoals.CheckDone (refactor S3)
        if (AIGoals.CheckDone(server, player, now)) return;

        // combat stage 2: the swing a "hit again" waited for is over -> start the next swing now, also while a request is
        // in flight (in combat one always is; S2 live run: placed after the request block, none of 17 queued swings started)
        if (S._nextHitNow)
        {
            S._nextHitNow = false;
            ApplyNextHit(server, player, now);
        }

        if (_req != null)
        {
            if (_req.isDone)
            {
                HandleResponse(server, player, now);
            }
            else if (now - _reqStart > Settings().timeoutSeconds)
            {
                AIPlayerSpikeMod.Log($"DECISION #{_reqId} timeout after {now - _reqStart:F1}s");
                AbortRequest("timeout");
                CallFailed(server, player, now, "시간 초과", FailKind.Timeout);
            }
            else if (!_reflex && now - _reqStart > Settings().lateAfterSeconds)
            {
                // a prefetch is asked while the skill still runs: the reflex is only for a decision that is late
                // while nothing (valid) runs
                if (!(_reqPrefetch && !_prefetchSkillDone)) SetReflex(true, $"no response after {now - _reqStart:F1}s");
            }
            return;
        }

        if (_applyQueuedNow)
        {
            _applyQueuedNow = false;
            ApplyQueued(server, player, now);
            if (AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running) return;
        }

        bool running = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running;

        // prefetch: the running skill is about to end by itself -> ask now what to do after it
        if (running && !_needCall && _queued == null && _prefetchFor != AIBody.Current && now >= _retryAt && now - _lastEtaCheck >= 0.1f)
        {
            _lastEtaCheck = now;
            float eta = -1f;
            try { eta = AIBody.Current.EtaSeconds(server, player); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: eta failed", e); }
            if (eta >= 0f && eta <= PrefetchLeadSeconds && now - _lastCallStart >= Settings().idleGapSeconds && CallBudgetOk(now))
            {
                _prefetchFor = AIBody.Current;
                Send(server, player, now, $"곧 끝남: 지금 하는 '{AIBody.Current.Name}'({AIBody.Current.Key})이 약 {eta:F1}초 뒤 끝남. 끝난 뒤 이어서 할 행동을 고른다", true);
                return;
            }
        }
        if (!running && !_needCall) { _needCall = true; _needReason = "할 일 없음"; }

        // premise watch while a skill runs
        if (running && now - _lastWatch >= 0.5f)
        {
            _lastWatch = now;
            var o = AIObservation.Take(server, player, false);
            _inCombat = AIEdge.ObserveAnyThreat(server, player, o.enemies, Settings().combatRange);   // 단계 3: enemies that cannot come do not make a fight
            string curKeyW = AIBody.Current?.Key ?? "";
            foreach (var e in o.enemies)
            {
                if (!_seenEnemies.Contains(e.entity))
                {
                    _seenEnemies.Add(e.entity);
                    if (curKeyW != "attack:" + e.entity.Index) { _needCall = true; _needReason = $"새 적 보임({e.id} {e.dist:F1}칸{(e.dist > Settings().combatRange ? $", 아직 {Settings().combatRange:F0}칸 밖" : "")})"; }
                }
            }
            // an enemy that was in view at the last call is gone (dead or out of view)
            foreach (var kv in new List<KeyValuePair<Entity, ObjectID>>(_callEnemies))
            {
                if (o.enemies.Exists(x => x.entity == kv.Key && x.dist <= Settings().combatRange + 4f)) continue;
                _callEnemies.Remove(kv.Key);
                if (curKeyW == "attack:" + kv.Key.Index) continue; // the attack skill reports its own target
                _needCall = true; _needReason = $"적 사라짐({kv.Value})";
            }
            int drop = _inCombat ? Settings().combatHpDropTrigger : Settings().hpDropTrigger;
            if (_hpAtLastCall > 0 && AIVitals.LostSince(_hpAtLastCall, o.health) >= drop)   // 합치기 전 수정 6: no hit when the max came down
            {
                _needCall = true; _needReason = $"체력 감소({_hpAtLastCall}→{o.health})"; S._needEmergency = true;
            }
        }

        // nothing running: the watch above does not run, so look for enemies within combatRange here (light query) —
        // otherwise an idle AI keeps the combat state of its last call while enemies walk up to it
        if (!running && now - _lastWatch >= 0.25f)
        {
            _lastWatch = now;
            _inCombat = EnemiesWithin(server, player, Settings().combatRange).Exists(x => AIEdge.ObserveThreat(server, player, x.e, x.id, server.EntityManager.GetComponentData<LocalTransform>(x.e).Position, x.d, Settings().combatRange));   // 단계 3
        }

        // 코어-안전지대 단계 3: a newly broken part of the base edge (in view) is a trigger for the action model (repair
        // options come with the facts); checked once a second while a plan is set and a goal runs
        if (Goal.kind != GoalKind.None && AIEdge.PlanActive)
        {
            string br = null;
            try { br = AIEdge.NewBreach(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: breach check failed", e); }
            // review S4: a reason already waiting gets the breach added (it was dropped while the count moved on)
            if (br != null) { _needReason = _needCall ? AIBaseGuard.AddReason(_needReason, br) : br; S._needBreach = true; _needCall = true; }
        }

        // Combat (an enemy within combatRange): ask again as soon as the previous answer is in — one request in flight per
        // character, floor combatMinGapSeconds from the last call start, cap combatMaxCallsPerMinute (user decision 2026-11-04:
        // combat re-decision as fast as practical). Peaceful time keeps the triggers above.
        CombatTrack(now, _inCombat, "적이 범위 밖");
        if (running && _inCombat && !_needCall && Settings().combatContinuous) { _needCall = true; _needReason = "전투 중 계속 다시 판단"; }

        if (!_needCall || (now < _retryAt && !(S._urgentCharge && AIRunGate.UrgentMayBypassRetry(S._failKind)))) return;
        float gap = running ? (_inCombat ? Settings().combatMinGapSeconds : Settings().minGapSeconds) : Settings().idleGapSeconds;
        if (now - _lastCallStart < gap && !S._urgentCharge) return;   // combat stage 2: a charge anticipation aimed at me asks at once
        if (!CallRateOk(now)) return;
        var u = Usage();
        if (u.usd >= Settings().monthlyCapUsd)
        {
            AIPlayerSpikeMod.Log($"COST cap reached: ${u.usd:F4} >= ${Settings().monthlyCapUsd}; goal paused");
            AIPlayerSpikeMod.Reply($"AI: 이달 Decisions 비용 상한(${Settings().monthlyCapUsd:F0})에 닿아 판단을 멈춥니다. 제자리에 섭니다.", ChatKind.Error);
            ClearGoal(new GoalEnd("cost cap", "system"));
            AIBody.Stop(server, player, now, "비용 상한");
            return;
        }
        S._reqUrgentNext = S._urgentCharge;
        if (S._urgentCharge) AIPlayerSpikeMod.Log($"DECISION urgent send ({now - S._urgentAt:F2}s after the anticipation was seen, {now - _lastCallStart:F2}s after the last call start)");
        S._urgentCharge = false;
        Send(server, player, now, _needReason, false, S._needEmergency, S._needBreach);
    }

    // Combat stage 2 (work/2026-11/2026-11-05-전투-적용-계획.md "지연 경로"): a charging enemy started its anticipation aimed
    // at this character. The request in flight was built without it: drop it and ask again at once with the new
    // observation (one request per character stays; the floor between calls is bypassed for this one call). The
    // program does not move by itself: it only asks sooner (pol.combat-no-auto-dodge).
    // Review: only when the charger is within the close-fight range, where its observation and options are in the input
    // (FindBrute); from farther the anticipation waits while that charge is on and asks once it is in range
    // (AIRunGate.ChargeAsk). HEAD decision 2026-11-05 (1): the waiting switch (a model choice held for the motion
    // boundary) is not dropped here; the urgent answer settles it by the one-owner rule (ApplyOrHold).
    private static readonly List<AIChargeWatch.Anticipation> _antNew = new List<AIChargeWatch.Anticipation>();
    private static void ChargeTrigger(World server, Entity player, float now)
    {
        int seen = S._chargeSeen;
        _antNew.Clear();
        AIChargeWatch.NewAnticipations(player, ref seen, _antNew);
        S._chargeSeen = seen;
        foreach (var a in _antNew) S._chargePend.Add(a);
        if (S._chargePend.Count == 0) return;
        var em = server.EntityManager;
        float3 mp = em.GetComponentData<LocalTransform>(player).Position;
        V2 me = new V2(mp.x, mp.z);
        float range = Settings().combatRange;
        bool fire = false; int bIdx = 0, bN = 0; float bAt = 0f, bDist = 0f;
        for (int i = S._chargePend.Count - 1; i >= 0; i--)
        {
            var a = S._chargePend[i];
            bool on = AIChargeWatch.ChargeStillOn(a.brute, a.n, out V2 bp);
            float dist = (bp - me).Len;
            var what = AIRunGate.ChargeAsk(on, dist, range);
            bool fresh = _antNew.Contains(a);
            if (what == AIRunGate.ChargeAskNow.Wait)
            {
                if (fresh) AIPlayerSpikeMod.Log($"DECISION urgent waits: charge anticipation e={a.idx} #{a.n} aimed at me {dist:F1} tiles away (beyond the close-fight range {range:F0}: its facts are not in the input); asking when it is within range");
                continue;
            }
            S._chargePend.RemoveAt(i);
            if (what == AIRunGate.ChargeAskNow.Drop)
            {
                if (!fresh) AIPlayerSpikeMod.Log($"DECISION urgent dropped: charge e={a.idx} #{a.n} ended {(on ? "" : "or left the watch ")}while beyond the close-fight range ({dist:F1} tiles)");
                continue;
            }
            if (!fire || a.at > bAt) { fire = true; bIdx = a.idx; bN = a.n; bAt = a.at; bDist = dist; }
        }
        if (!fire) return;
        string why = $"긴급: 버섯 야수 돌진 준비 시작(나를 겨눔)";
        string aborted = "";
        // one anticipation = one event (brute + its start, AIChargeWatch): handled once. The request in flight is dropped
        // only when it was built before the anticipation and is not itself an urgent re-ask (AIRunGate.AbortForAnticipation)
        if (AIRunGate.AbortForAnticipation(_req != null, _reqStart, S._reqUrgent, bAt))
        {
            // the aborted call is billed although its answer is not read: counted with the last call's tokens (estimate)
            int est = S._lastTokens;
            if (est > 0) { AddUsage(est, est * Settings().usdPerMillionInputTokens / 1e6); TokensThisSession += est; }
            S.ChargeAborts++;
            aborted = $"; #{_reqId} in flight {now - _reqStart:F2}s dropped (usage counted ~{est} tokens, estimate)";
            AbortRequest(AIRunGate.AbortForCharge);   // keeps the waiting choices (re-review R1, HEAD decision 1)
        }
        else if (_req != null)
        {
            // kept (sent after it, or an urgent re-ask itself): the next call follows its answer at once with the new state
            _needCall = true; _needReason = why; S._needEmergency = true; S._urgentCharge = true; S._urgentAt = bAt; S.ChargeTriggers++;
            AIPlayerSpikeMod.Log($"DECISION urgent: charge anticipation e={bIdx} #{bN}; #{_reqId} {(_reqStart > bAt ? "was sent after it" : "is an urgent re-ask")}, kept; asking again right after its answer");
            return;
        }
        _needCall = true; _needReason = why; S._needEmergency = true; S._urgentCharge = true; S._urgentAt = bAt; S.ChargeTriggers++;
        AIPlayerSpikeMod.Log($"DECISION urgent: charge anticipation e={bIdx} #{bN} seen {now - bAt:F2}s ago{aborted}{(_held != null ? $"; held switch #{_heldId} kept (the answer settles it)" : "")}");
    }

    // One fight = from an enemy within combatRange until there is none (or the AI is down / self-defence is over).
    // Logged to measure the combat re-decision rate: calls/min, latency, cost/min (per character).
    private static void CombatTrack(float now, bool inCombat, string why)
    {
        var s = S;
        if (inCombat == s._cOn) return;
        if (inCombat)
        {
            s._cOn = true; s._cStart = now; s._cCalls0 = Calls; s._cLat0 = Latencies.Count; s._cTok0 = TokensThisSession; s._cN++;
            s._cSw0 = s.Switches; s._cSwB0 = s.SwitchBoundary; s._cSwE0 = s.SwitchEmergency; s._cSwM0 = s.SwitchMid; s._cSwS0 = s.SwitchShort; s._cSwI0 = s.SwitchIdle;
            s._cHeld0 = s.Held; s._cHeldDrop0 = s.HeldDropped; s._cCont0 = s.ContinueChosen; s._cKills0 = s.Kills; s._cDowns0 = AIVitals.Downs; s._cPrep0 = s.PrepMs.Count;
            s._cHp0 = AIVitals.LastHp; s._cRev0 = s.Reversals;
            AIPlayerSpikeMod.Log($"COMBAT #{s._cN} start: enemy within {Settings().combatRange:F0} tiles (re-decision floor {Settings().combatMinGapSeconds:F2}s, cap {Settings().combatMaxCallsPerMinute}/min, continuous {Settings().combatContinuous})");
            return;
        }
        s._cOn = false;
        float dur = now - s._cStart;
        int calls = Calls - s._cCalls0;
        var lat = new List<float>();
        for (int i = s._cLat0; i < Latencies.Count; i++) lat.Add(Latencies[i]);
        lat.Sort();
        long tok = TokensThisSession - s._cTok0;
        double usd = tok * Settings().usdPerMillionInputTokens / 1e6;
        float min = Mathf.Max(dur, 0.001f) / 60f;
        string L(float q) => lat.Count == 0 ? "-" : lat[Mathf.Clamp(Mathf.CeilToInt(q * lat.Count) - 1, 0, lat.Count - 1)].ToString("F2");
        AIPlayerSpikeMod.Log($"COMBAT #{s._cN} end ({why}): {dur:F1}s, calls {calls} ({calls / min:F0}/min), answers {lat.Count}, latency p50 {L(0.5f)}s p95 {L(0.95f)}s max {L(1f)}s, tokens {tok} (${usd:F5}, ${usd / min:F5}/min)");
        var prep = new List<float>();
        for (int i = s._cPrep0; i < s.PrepMs.Count; i++) prep.Add(s.PrepMs[i]);
        prep.Sort();
        string Pp(float q) => prep.Count == 0 ? "-" : prep[Mathf.Clamp(Mathf.CeilToInt(q * prep.Count) - 1, 0, prep.Count - 1)].ToString("F1");
        // similar-option switches: between the movement options (keep away, retreat, walk/dig somewhere)
        // review F4: the short close-fight actions applied at once by design are counted apart (they are not MID-MOTION and
        // not "between movement options", the indicators of switches that did not wait / flip-flopped)
        int similar = 0, shortMoves = 0;
        foreach (var w in s._switches) if (w.t >= s._cStart) { if (AIRunGate.SimilarMove(IsMove(w.from), w.toKind, w.how)) similar++; else if (AIRunGate.ShortMove(IsMove(w.from), w.toKind, w.how)) shortMoves++; }
        AIPlayerSpikeMod.Log($"COMBAT #{s._cN} result: switches {s.Switches - s._cSw0} (at boundary {s.SwitchBoundary - s._cSwB0}, emergency {s.SwitchEmergency - s._cSwE0}, MID-MOTION {s.SwitchMid - s._cSwM0}, short actions at once {s.SwitchShort - s._cSwS0}; reversals {s.Reversals - s._cRev0}; between movement options {similar}, between short moves {shortMoves}), started from idle {s.SwitchIdle - s._cSwI0}, held for boundary {s.Held - s._cHeld0} (dropped {s.HeldDropped - s._cHeldDrop0}), chose 'continue' {s.ContinueChosen - s._cCont0}; kills {s.Kills - s._cKills0}, hp {s._cHp0}->{AIVitals.LastHp}, downs {AIVitals.Downs - s._cDowns0}; prep ms p50 {Pp(0.5f)} p95 {Pp(0.95f)} max {Pp(1f)} (n {prep.Count}); {AIPerf.HitchNote(s._cStart, now)}");
        // combat stage 2: what happened to the answers (session totals; differences between two lines give one fight)
        AIPlayerSpikeMod.Log($"COMBAT #{s._cN} answers (session totals): received {s.AnsReceived}, started {s.AnsStarted}, continued {s.AnsContinued}, queued next swing {s.AnsQueued}, held {s.Held} (replaced {s.AnsHeldReplaced}, dropped {s.HeldDropped}), discarded {s.Discarded}, failed calls {s.Failed}; charge anticipation triggers {s.ChargeTriggers}, requests dropped for one {s.ChargeAborts}");
    }

    private static bool IsMove(string skillName) => skillName == "거리 벌리기" || skillName == "물러나기" || skillName == "걷기" || skillName == "파들어가기" || skillName == "짧은 이동" || skillName == "사거리까지 접근";
    // Combat stage 2: by the candidate kind, not by the first words of its text (IsMoveDesc read "거리 벌리기", "물러나기",
    // "탐험 이동", "파 들어가기", "기억한 목표", "동료", "호스트"). Same moves: away, retreat, explore, dig (dig toward
    // a target, remembered ore, explore dig, toward the host), follow; plus the new step and approach. Two differences from
    // the text rule: "동료 돕기(공격)" (an attack) is no longer counted as a move, and the close-fight break-off option
    // (its text starts "싸움을 끊고…") is counted when its kind is a move.
    private static bool IsMoveKind(string kind) => AIRunGate.IsMoveKind(kind);

    // per-minute cap and monthly cost cap, without side effects (the prefetch only asks when both allow it)
    private static bool CallBudgetOk(float now)
    {
        if (!CallRateOk(now)) return false;
        return Usage().usd < Settings().monthlyCapUsd;
    }

    // Per-minute caps: in combat all calls count against combatMaxCallsPerMinute; in peace only the calls made in peace
    // count against maxCallsPerMinute. (Before 2026-11-04 the peaceful cap counted every call of the last minute: right
    // after a fight (~200 calls/min) an AI that stood idle could not call for up to a minute, and since _inCombat is
    // only refreshed by a call or the running-skill watch, it then stood still while new enemies hit it — five arena
    // rounds in a row went down without one decision after the goal was set, ai2 combat polish 2.)
    private static bool CallRateOk(float now)
    {
        _callTimes.RemoveAll(t => now - t > 60f);
        _peaceCallTimes.RemoveAll(t => now - t > 60f);
        return _inCombat ? _callTimes.Count < Settings().combatMaxCallsPerMinute : _peaceCallTimes.Count < Settings().maxCallsPerMinute;
    }

    // ------------------------------------------------------------------ motion boundaries (HEAD decision 2026-11-04)
    // Decisions is asked as fast as before; a chosen switch to another action starts only at a natural breakpoint of the
    // running motion (AIMotion: the game's swing cooldown timer, a one-tile step, eating until eaten). Bypassed at once
    // by emergencies: a big health drop, a new enemy right next to the AI, the running skill ending (target dead or gone,
    // failure). Choosing the running action again ("continue") cancels a waiting switch.
    private const float MaxHoldSeconds = 2f;   // safety only: no motion of ours lasts longer (eating ~1.5 s)
    private const float AdjacentTiles = 1.5f;
    // "A new enemy right next to the AI" is an emergency only when the answer was made without knowing that enemy was
    // close: it was not within this many tiles in the request's input. In a swarm the same three or four enemies step
    // in and out of 1.5 tiles all the time; each re-entry counted as "new" (7 of 9 emergencies in Gemini's 26.7 s fight,
    // ai2 stab5), and each one switched at once. An enemy 4+ tiles away cannot reach 1.5 tiles within one ~0.3 s call.
    private const float KnownNearTiles = 4f;
    private static AICandidate _held { get => S._held; set => S._held = value; }
    private static int _heldId { get => S._heldId; set => S._heldId = value; }
    private static int _heldRev { get => S._heldRev; set => S._heldRev = value; }
    private static float _heldAt { get => S._heldAt; set => S._heldAt = value; }
    private static float _heldReqStart { get => S._heldReqStart; set => S._heldReqStart = value; }
    private static int _reqHp { get => S._reqHp; set => S._reqHp = value; }
    private static HashSet<Entity> _reqAdjacent => S._reqAdjacent;
    private static float _lastEmergencyCheck { get => S._lastEmergencyCheck; set => S._lastEmergencyCheck = value; }

    private static bool IsEmergencyTrigger(string t) => AIOutcomes.EmergencyText(t);
    // refactor S2b: the flag decides; with the test switch, the sentence is compared (EMERGENCY-MISMATCH)
    private static bool EmergencyRule(string trigger, bool flag)
    {
        if (AITestSwitches.Test)
        {
            string mm = AIOutcomes.Mismatch("emergency", IsEmergencyTrigger(trigger), flag, $"trigger='{Trim(trigger, 80)}'");
            if (mm != null) AIPlayerSpikeMod.Log("EMERGENCY-MISMATCH " + mm);
        }
        return flag;
    }

    // Enemies within `r` tiles (light query: enemies only, no tiles/items/objects).
    private static List<(Entity e, ObjectID id, float d)> EnemiesWithin(World server, Entity player, float r)
    {
        var res = new List<(Entity, ObjectID, float)>();
        var em = server.EntityManager;
        float3 me = em.GetComponentData<LocalTransform>(player).Position;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            foreach (var e in ents)
            {
                if (em.GetComponentData<HealthCD>(e).health <= 0) continue;
                float d = math.distance(em.GetComponentData<LocalTransform>(e).Position.xz, me.xz);
                if (d <= r) res.Add((e, em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None, d));
            }
        return res;
    }

    // An emergency since the last request was sent: a big health drop, or a new enemy right next to the AI. null if none.
    private static string EmergencyNow(World server, Entity player, int hpNow)
    {
        if (_reqHp > 0 && AIVitals.LostSince(_reqHp, hpNow) >= Settings().combatHpDropTrigger) return $"긴급: 체력 감소({_reqHp}→{hpNow})";
        foreach (var a in EnemiesWithin(server, player, AdjacentTiles))
            if (!_reqAdjacent.Contains(a.e)) return $"긴급: 새 적 {a.id}이(가) 바로 옆({a.d:F1}칸)";
        return null;
    }

    private static string Who(AICandidate c) => c.kind == "continue" ? "계속" : Trim(c.desc, 24);

    // An answer to apply now: the same action continues; a switch waits for the motion boundary unless the request was
    // about an emergency or nothing is in mid-motion.
    private static void ApplyOrHold(World server, Entity player, float now, AICandidate c, string head)
    {
        var cur = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current : null;
        // combat stage 2: the rule is AIRunGate (Unity-free, core-tests RunGateTests). The emergency flag is read only where
        // the old code read it (a switch of a long action in mid-motion); a short close-fight action applies at once.
        bool shortKind = AIRunGate.IsShort(c.kind);
        var outc = AIRunGate.Decide(c.kind, c.key, c.run, cur != null, cur?.Run ?? 0, cur?.Key ?? "", AIMotion.AtBoundary, false);
        if (outc == AnswerOutcome.Hold && EmergencyRule(_reqTrigger, S._reqEmergency)) outc = AIRunGate.Decide(c.kind, c.key, c.run, cur != null, cur?.Run ?? 0, cur?.Key ?? "", AIMotion.AtBoundary, true);
        if (outc == AnswerOutcome.StaleContinue)
        {
            // cannot happen after StaleReason (ContinueStale checks the run), kept so the rule's answer is always obeyed
            Discarded++;
            AIPlayerSpikeMod.Log($"{head} DISCARD: {AIRunGate.ContinueStale(c.run, cur != null, cur?.Run ?? 0, c.key, cur?.Key ?? "")}");
            _needCall = true; _needReason = "이전 답 버림(계속하라던 실행이 끝남)";
            return;
        }
        // an answer that is applied, continued, held or queued is newer than a queued next swing of another choice
        if (S._nextHit != null && !(outc == AnswerOutcome.Continue && c.kind == "continue"))
        { AIPlayerSpikeMod.Log($"DECISION #{S._nextHitId} next swing {(outc == AnswerOutcome.QueueAfter ? "replaced" : "dropped")} (#{_reqId} chose {c.key})"); S._nextHit = null; S._nextHitFor = null; }
        if (outc == AnswerOutcome.QueueAfter)
        {
            if (_held != null) { HeldDropped++; AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch replaced by #{_reqId} (next swing)"); _held = null; }
            S._nextHit = c; S._nextHitFor = cur; S._nextHitId = _reqId; S._nextHitReq = _reqStart; S._nextHitRev = _reqRev;
            S.AnsQueued++;
            AIPlayerSpikeMod.Log($"{head} QUEUED (next swing: starts as a new run when run #{cur?.Run} '{cur?.Key}' is over)");
            return;
        }
        if (outc == AnswerOutcome.Continue)
        {
            Applied++; S.AnsContinued++;
            ContinueChosen++;
            if (_held != null) { HeldDropped++; AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch dropped (#{_reqId} chose the running action again): {Trim(_held.desc, 60)}"); _held = null; }
            AIPlayerSpikeMod.Log($"{head} APPLY");
            Apply(server, player, now, c);
            return;
        }
        if (outc == AnswerOutcome.Hold)
        {
            if (_held != null) { HeldDropped++; S.AnsHeldReplaced++; AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch replaced by #{_reqId}"); }
            _held = c; _heldId = _reqId; _heldAt = now; _heldReqStart = _reqStart; _heldRev = _reqRev;
            Held++;
            AIPlayerSpikeMod.Log($"{head} HOLD until the motion boundary ({AIMotion.Mid})");
            return;
        }
        // one owner: an older waiting switch does not outlive a newer answer that is applied now (before, an emergency
        // answer applied mid-motion left it, and it replaced the newer action at the next boundary)
        if (_held != null) { HeldDropped++; S.AnsHeldReplaced++; AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch replaced by #{_reqId} (applied at once)"); _held = null; }
        Applied++; S.AnsStarted++;
        AIPlayerSpikeMod.Log($"{head} APPLY{(cur == null ? "" : shortKind && !AIMotion.AtBoundary && !S._reqEmergency ? $" (short action, at once: {AIMotion.Mid})" : S._reqEmergency ? $" (emergency: {_reqTrigger})" : " (at motion boundary)")}");
        // review F4: a short action applied at once mid-motion is "short" also when the request was an emergency (it would
        // have been applied at once anyway; counted apart from MID-MOTION and emergency)
        Switch(server, player, now, c, cur == null ? "idle" : shortKind && !AIMotion.AtBoundary ? "short" : S._reqEmergency ? "emergency" : "boundary");
    }

    private static void TryApplyHeld(World server, Entity player, float now)
    {
        var c = _held;
        string stale = _heldRev != Revision ? $"목표가 바뀜(rev {_heldRev}→{Revision})" : StaleReason(server, player, now, c, now - _heldReqStart, _heldRev);
        if (stale != null)
        {
            HeldDropped++;
            AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch dropped ({stale}): {Trim(c.desc, 60)}");
            _held = null;
            _needCall = true; _needReason = "기다리던 바꾸기 버림(" + stale + ")";
            return;
        }
        var cur = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current : null;
        string how = AIRunGate.HeldApplyNow(cur != null, AIMotion.AtBoundary, now - _heldAt, MaxHoldSeconds);
        if (how == null) return;
        _held = null;
        Applied++; S.AnsStarted++;
        AIPlayerSpikeMod.Log($"DECISION #{_heldId} held switch APPLY ({how}) {now - _heldAt:F2}s after the answer: {Trim(c.desc, 80)}");
        Switch(server, player, now, c, how);
    }

    // Starts the chosen other action and counts the switch (how: boundary | emergency | idle | timeout | short).
    private static void Switch(World server, Entity player, float now, AICandidate c, string how)
    {
        var cur = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current : null;
        if (cur != null && c.kind != "continue" && c.key != cur.Key)
        {
            // review F4: AIRunGate.SwitchClass — a short close-fight action applied at once by design is "short", not MID-MOTION
            string cls = AIRunGate.SwitchClass(how, AIMotion.AtBoundary);
            bool mid = cls == "mid";
            Switches++;
            S._left.RemoveAll(x => now - x.t > 3f);
            bool back = S._left.Exists(x => x.key == c.key);
            if (back) S.Reversals++;
            S._left.Add((cur.Key, now));
            if (cls == "emergency") SwitchEmergency++; else if (cls == "short") SwitchShort++; else if (mid) SwitchMid++; else SwitchBoundary++;
            S._switches.Add((now, cur.Name, Who(c), now - cur.StartTime, how, c.kind, AIRunGate.KindOfKey(cur.Key)));
            while (S._switches.Count > 0 && now - S._switches[0].t > 20f) S._switches.RemoveAt(0);
            AIPlayerSpikeMod.Log($"SWITCH {cur.Name}({cur.Key}, ran {now - cur.StartTime:F2}s) -> {c.kind}({c.key}) at {how}{(back ? " REVERSAL (back to an action left < 3 s ago)" : "")}{(mid ? " MID-MOTION: " + AIMotion.Mid : cls == "short" && !AIMotion.AtBoundary ? " (short action at once by design: " + AIMotion.Mid + ")" : "")}; swing left {AIMotion.SwingLeft:F2}s");
        }
        else if (cur == null) SwitchIdle++;
        Apply(server, player, now, c);
    }

    // Recent switches of this character (last 10 s) for the decision input: what was started, how long it ran.
    // In the close fight with a charger the switches between two short actions carry no restart sentence (HEAD decision
    // 2026-11-05 2, AIRunGate.SwitchHistoryText); every other input keeps the line as before.
    private static readonly List<(string from, string fromKind, string to, string toKind, float ran)> _swh = new List<(string, string, string, string, float)>();
    private static string SwitchHistory(float now, bool closeFightWithCharger)
    {
        _swh.Clear();
        foreach (var s in S._switches) if (now - s.t <= 10f) _swh.Add((s.from, s.fromKind, s.to, s.toKind, s.ran));
        return AIRunGate.SwitchHistoryText(_swh, closeFightWithCharger);
    }

    // Combat stage 2 (HEAD review item 9): the "hit again" answer queued while a swing ran — the swing is over: start the
    // next one as a new run (if the target is still there), so the next swing does not wait for another round trip.
    private static void ApplyNextHit(World server, Entity player, float now)
    {
        var c = S._nextHit; int id = S._nextHitId; float age = now - S._nextHitReq; int rev = S._nextHitRev;
        S._nextHit = null; S._nextHitFor = null;
        if (c == null) return;
        string stale = rev != Revision ? $"목표가 바뀜(rev {rev}→{Revision})" : StaleReason(server, player, now, c, age, rev);
        if (stale != null) { AIPlayerSpikeMod.Log($"DECISION #{id} next swing not started ({stale}); deciding again"); _needCall = true; _needReason = "다음 휘두르기 못 함(" + stale + ")"; return; }
        Applied++; S.AnsStarted++;
        AIPlayerSpikeMod.Log($"DECISION #{id} next swing APPLY at the end of the previous swing ({age:F2}s after asking)");
        Apply(server, player, now, c);
    }

    // The skill the prefetch was for ended as done: start the answer Decisions already gave, if still valid.
    private static void ApplyQueued(World server, Entity player, float now)
    {
        var c = _queued; int id = _queuedId; float age = now - _queuedReqStart; int rev = _queuedRev;
        _queued = null; _queuedFor = null;
        if (c == null) return;
        string stale = rev != Revision ? $"목표가 바뀜(rev {rev}→{Revision})" : StaleReason(server, player, now, c, age, rev);
        if (stale != null)
        {
            PrefetchDropped++;
            AIPlayerSpikeMod.Log($"DECISION #{id} queued answer not used ({stale}); deciding again");
            _needCall = true; _needReason = "미리 받은 답이 맞지 않음(" + stale + ")";
            return;
        }
        PrefetchUsed++;
        Applied++; S.AnsStarted++;
        AIPlayerSpikeMod.Log($"DECISION #{id} queued answer APPLY at skill end ({age:F2}s after asking): {Trim(c.desc, 80)}");
        _needCall = false;
        Apply(server, player, now, c);
    }

    // ------------------------------------------------------------------ self-defence (no goal)

    internal static bool _sd { get => S._sd; set => S._sd = value; }
    private static float _sdStart { get => S._sdStart; set => S._sdStart = value; }
    private static float _sdLastThreat { get => S._sdLastThreat; set => S._sdLastThreat = value; }
    private static float _sdLastCheck { get => S._sdLastCheck; set => S._sdLastCheck = value; }
    private static int _sdCalls0 { get => S._sdCalls0; set => S._sdCalls0 = value; }
    private static int _sdHp0 { get => S._sdHp0; set => S._sdHp0 = value; }
    private static int _sdDowns0 { get => S._sdDowns0; set => S._sdDowns0 = value; }
    private static int _sdEpisodes { get => S._sdEpisodes; set => S._sdEpisodes = value; }
    private static string _sdWhy { get => S._sdWhy; set => S._sdWhy = value; }

    // Returns true while a self-defence episode runs (the normal decision loop then runs with combat candidates only).
    private static bool SelfDefence(World server, Entity player, float now)
    {
        if (now - _sdLastCheck < 0.5f) return _sd;
        _sdLastCheck = now;
        var em = server.EntityManager;
        int hp = em.HasComponent<HealthCD>(player) ? em.GetComponentData<HealthCD>(player).health : 1;
        if (hp <= 0) return _sd;   // down: keep the episode state; the normal loop pauses while down
        var o = AIObservation.Take(server, player, false);
        // 코어-안전지대 단계 3: a threat is an enemy within range that can come (an open or unseen way; a flier over fences)
        // or can hit now; one behind walls or a closed door neither starts nor keeps the episode (단계 1 M4c: 287 calls
        // in 100 s against an enemy behind a fence). The episode rule is AIBaseGuard.DefenceStep.
        var near = o.enemies.Find(e => AIEdge.ObserveThreat(server, player, e.entity, e.id, e.pos, e.dist, Settings().combatRange));
        bool hurt = AIVitals.HurtRecently(AIBaseGuard.HurtSeconds, now);   // 단계 3 recheck R1: a lower max health (hunger) is no hit
        bool on = _sd; float lastThreat = _sdLastThreat;
        var ev = AIBaseGuard.DefenceStep(ref on, ref lastThreat, near != null, hurt, now);
        _sdLastThreat = lastThreat;
        if (ev == AIBaseGuard.DefenceEvent.None) return false;
        if (ev == AIBaseGuard.DefenceEvent.Started)
        {
            _sd = true; _sdStart = now; _sdCalls0 = Calls; _sdHp0 = hp; _sdDowns0 = AIVitals.Downs; _sdEpisodes++;
            _sdWhy = hurt ? $"체력이 줄어듦({AIVitals.HpChange(1.5f, now)})" : $"적 {near.id}이(가) {near.dist:F1}칸 안으로 옴";
            Revision++;
            _needCall = true; _needReason = "자기 방어: " + _sdWhy;
            _hpAtLastCall = -1;
            AIPlayerSpikeMod.Log($"SELF-DEFENCE #{_sdEpisodes} start: {_sdWhy}; hp {hp}/{o.maxHealth}; enemies in view {o.enemies.Count} (nearest {(o.enemies.Count > 0 ? $"{o.enemies[0].id} {o.enemies[0].dist:F1}" : "-")})");
            AIPlayerSpikeMod.Reply($"AI 자기 방어 시작: {_sdWhy}", ChatKind.Status);
            AIPlanner.AddEvent($"자기 방어({_sdWhy})");
            return true;
        }
        if (ev == AIBaseGuard.DefenceEvent.Ended)
        {
            _sd = false;
            Revision++;
            AbortRequest("self-defence over");
            _inCombat = false; CombatTrack(now, false, "자기 방어 끝");
            AIBody.Stop(server, player, now, "위협 없음, 제자리로");
            string res = $"{now - _sdStart:F1}s, calls {Calls - _sdCalls0}, hp {_sdHp0}->{hp}, downs {AIVitals.Downs - _sdDowns0}";
            AIPlayerSpikeMod.Log($"SELF-DEFENCE #{_sdEpisodes} end: no enemy within {Settings().combatRange:F0} tiles and no damage for 3s; {res}");
            AIPlayerSpikeMod.Reply($"AI 자기 방어 끝: 위협 없음({res}). 제자리에 섭니다.", ChatKind.Status);
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ request

    private static void Send(World server, Entity player, float now, string trigger, bool prefetch = false, bool emergency = false, bool breach = false)
    {
        string key = ApiKey();
        if (string.IsNullOrEmpty(key))
        {
            AIPlayerSpikeMod.Reply("AI: OPENAI_API_KEY 가 없어 판단을 멈춥니다.", ChatKind.Error);
            ClearGoal(new GoalEnd("no api key", "system"));
            return;
        }
        var prepSw = System.Diagnostics.Stopwatch.StartNew();
        _prepParts.Clear();
        try { AIMemory.Observe(server, player, now, true); } catch (Exception e) { AIPlayerSpikeMod.LogError("memory: observe before decision failed", e); }
        var o = AIObservation.Take(server, player, true);
        PrepPart("observe", prepSw);
        // recheck N4: a call for the breach trigger (or with its reason waiting): repairs first among the edge options
        bool breachCall = breach || AIEdge.PlanActive && AIEdge.BreachPending;
        List<AICandidate> cands;
        _building = _ctx;   // refactor S5: the preparation running now; PlanC (feature modules too) counts into it
        _msFrontier = _msPlans = 0; _nPlans = _nPlansCached = 0;
        int tunnelCalls0 = AITunnel.Calls, tunnelTwo0 = AITunnel.TwoPass;
        try { cands = BuildCandidates(server, player, o, prefetch, breachCall); } finally { _building = null; }
        PrepPart("candidates", prepSw);
        // 우회 사실: dig plans of this build — computed (n), cached, all Plan calls incl. skills (tunnel), two-pass searches (2pass)
        _prepParts.Add($"(frontier {_msFrontier:F1}, plans {_msPlans:F1} n{_nPlans} cached{_nPlansCached} tunnel{AITunnel.Calls - tunnelCalls0} 2pass{AITunnel.TwoPass - tunnelTwo0})");
        if (prefetch && (cands.Count < 2 || ((AIGoalKinds.Of(Goal.kind)?.NeedsProgress == true) && !cands.Exists(c => c.progress))
                         || (Goal.kind == GoalKind.Explore && _exploreOptions == 0)))
        {
            // nothing to choose from yet without the running action: decide normally when it ends (the end checks
            // such as "goal stalled" are made then, with the real situation)
            AIPlayerSpikeMod.Log($"BRAIN: prefetch skipped ({cands.Count} candidates without the running action); deciding when it ends");
            return;
        }
        if (!prefetch && _queued != null) DropQueued("새 판단 요청");
        if (AIGoals.CheckAfterBuild(server, player, now, new GoalBuildFacts
        {
            HasProgress = cands.Exists(c => c.progress), InCombat = _inCombat, FrontierPending = S._fPending,
            ExploreOptions = _exploreOptions, ExploreNoPath = _exploreNoPath, ExploreFrontierNote = _exploreFrontierNote,
            SkippedNote = _skippedNote, PrepNote = _prepNote, GatherFacts = S._gatherFacts,
            BlockNote = _blockNote, FailMemorySeconds = FailMemorySeconds
        })) return;
        if (cands.Count < 2)
        {
            // Only "wait" is left: a choice question needs at least two options (the API answers 400), and there is
            // nothing to choose anyway. Wait locally without a call; give up an explore goal after a few rounds.
            _lastCallStart = now;
            _needCall = false;
            _onlyWait++;
            AIPlayerSpikeMod.Log($"BRAIN: only 'wait' is possible ({trigger}); waiting without a call (#{_onlyWait}){(string.IsNullOrEmpty(_skippedNote) ? "" : "; left out: " + _skippedNote)}");
            if (_onlyWait >= 5 && Goal.IsReal)   // free time has nothing to stall: it waits until something can be chosen or a goal comes
            {
                string why = $"할 수 있는 행동이 기다리기뿐(5번 연속){(string.IsNullOrEmpty(_skippedNote) ? "" : " · 잠시 뺀 대상: " + Trim(_skippedNote, 120))}";
                AIPlayerSpikeMod.Log($"GOAL stalled rev={Revision}: {Goal.Text}: {why}");
                AIPlayerSpikeMod.Reply($"AI 목표 멈춤: {Goal.Text}. {why}", ChatKind.Status);
                string srcW = Goal.source, txtW = Goal.Text;
                EndGoal(server, player, now, new GoalEnd("stalled: " + why, "blocked", "only_wait"), EndOrder.ClearStopReport, "할 수 있는 행동 없음", "goal_stalled", txtW, why, srcW);
                _onlyWait = 0;
                return;
            }
            // review F2: a wait the program starts does not cut the closing tail of the skill before it
            if (AIBody.TailRunning) { AIPlayerSpikeMod.Log("BRAIN: only 'wait' while the closing tail runs; the close goes on"); return; }
            _applying = true;
            try { AIBody.StartSkill(server, player, new WaitSkill(Settings().waitSeconds), now); }
            finally { _applying = false; }
            return;
        }
        _onlyWait = 0;
        // live-run fix 7: a breach trigger overwritten by a later reason (skill end) still reaches this call
        if (AIEdge.PlanActive) trigger = AIEdge.WithPendingBreach(trigger);
        string input = BuildInput(server, player, o, trigger);
        string json = BuildJson(input, cands);
        float prepMs = (float)prepSw.Elapsed.TotalMilliseconds;
        S.PrepMs.Add(prepMs); while (S.PrepMs.Count > 2000) S.PrepMs.RemoveAt(0);

        _reqId++;
        S._reqUrgent = S._reqUrgentNext; S._reqUrgentNext = false;   // combat stage 2: this request is an urgent re-ask for a charge anticipation
        _reqStart = now;
        _reqRev = Revision;
        _reqTrigger = trigger;
        S._reqEmergency = emergency;
        _reqCands = cands;
        _reqPrefetch = prefetch;
        _prefetchSkillDone = false;
        if (prefetch) Prefetches++;
        _lastCallStart = now;
        _needCall = false;
        _hpAtLastCall = o.health;
        _reqHp = o.health;
        _reqAdjacent.Clear();
        foreach (var e in o.enemies) if (e.dist <= KnownNearTiles) _reqAdjacent.Add(e.entity);
        foreach (var e in o.enemies) _seenEnemies.Add(e.entity);
        _callEnemies.Clear();
        foreach (var e in o.enemies) if (e.dist <= Settings().combatRange && !AIEdge.LastAssessedCannotCome(e.entity)) _callEnemies[e.entity] = e.id;   // only enemies that matter for combat
        _callTimes.Add(now);
        if (!_inCombat) _peaceCallTimes.Add(now);
        Calls++;

        // the whole input and option texts once per 20 s per character in a fight (the SEND line cuts each option at
        // 120 characters, so the facts at the end of an option are otherwise not in the log)
        if (_inCombat && (!_fullLog.TryGetValue(AIAgent.Cur.Id, out float fl) || now - fl >= 20f))
        {
            _fullLog[AIAgent.Cur.Id] = now;
            var fsb = new StringBuilder();
            foreach (var c in cands) fsb.Append($" || {c.value} {c.desc}");
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} FULL input=[{input.Replace('\n', ' ')}] options:{fsb}");   // was #{_reqId + 1}, one too high (_reqId is already this request's)
        }
        if (AITestSwitches.DecisionLog)
        {
            // TEST ONLY: this request's whole input and every option, uncut (AIPlayers/decisionlog.on)
            var fsb = new StringBuilder();
            foreach (var c in cands) fsb.Append($" || {c.value} [{c.kind} {c.key}] {c.desc}");
            AITestSwitches.Write($"DECISION #{_reqId} INPUT{(prefetch ? " (prefetch)" : "")} rev={_reqRev} trigger='{trigger}'{_setLog} running={(AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current.Key : "-")} motion={(AIMotion.AtBoundary ? "boundary" : AIMotion.Mid)} input=[{input.Replace("\n", " ⏎ ")}] options({cands.Count}):{fsb}");
        }
        var sb = new StringBuilder();
        foreach (var c in cands) sb.Append($" | {c.value} {Trim(AIContinueText.ForShow(c.desc), 120)}");
        AIPlayerSpikeMod.Log($"DECISION #{_reqId} SEND{(prefetch ? " (prefetch)" : "")} rev={_reqRev} trigger='{trigger}' goal='{Goal.Text}' candidates={cands.Count} inputChars={input.Length} prep={prepMs:F1}ms[{string.Join(" ", _prepParts)}] motion={(AIMotion.AtBoundary ? "boundary" : AIMotion.Mid)}{_setLog}{(_swarmLog != "" ? $" swarm=[{Trim(_swarmLog, 200)}]" : "")}{(AITeammates.Last.Count > 0 ? $" players=[{Trim(AITeammates.Summary(AITeammates.Last), 240)}]" : "")} ({_objNote}){sb}");

        try
        {
            _req = AIDecisionsApi.Post(json, key, (int)math.ceil(Settings().timeoutSeconds) + 2);
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"DECISION #{_reqId} send failed", e);
            _req = null;
            CallFailed(server, player, now, "보내기 오류 " + e.GetType().Name, FailKind.Network);
        }
    }

    private static void HandleResponse(World server, Entity player, float now)
    {
        var req = _req;
        _req = null;
        float latency = now - _reqStart;
        var reply = AIDecisionsApi.Read(req);   // reads and disposes (refactor S5); logged below in the old order
        long code = reply.Code;
        string text = reply.Text;
        string netErr = reply.NetErr;
        if (_reflex) SetReflex(false, $"response arrived after {latency:F2}s");

        if (!reply.Ok)
        {
            string msg = reply.ErrorMessage;
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} HTTP {code} net='{netErr}' error='{Trim(msg, 200)}' after {latency:F2}s");
            CallFailed(server, player, now, $"HTTP {code} {Trim(msg, 60)}", AIRunGate.Classify(reply.NetOk, code, false, false, false));
            return;
        }
        if (reply.ParseError != null) AIPlayerSpikeMod.LogError($"DECISION #{_reqId} parse failed", reply.ParseError);
        DAnswer ans = reply.Answer;
        int tokens = reply.Tokens;
        int cached = reply.Cached;
        if (reply.Fallback)
        {
            // JsonUtility left the nested answer empty: the few fields we need were read directly from the text.
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} parsed with fallback reader (JsonUtility gave no answer): type={ans?.type} choice={ans?.choice} tokens={tokens} bodyChars={text.Length}");
        }
        double usd = tokens * Settings().usdPerMillionInputTokens / 1e6;
        AddUsage(tokens, usd);
        if (tokens > 0) S._lastTokens = tokens;
        Latencies.Add(latency);
        TokensThisSession += tokens;

        if (ans == null || ans.type != "choice" || string.IsNullOrEmpty(ans.choice))
        {
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} no usable answer (type={ans?.type}) after {latency:F2}s tokens={tokens}; body starts: {Trim(text.Replace('\n', ' '), 300)}");
            CallFailed(server, player, now, ans != null && ans.type == "refusal" ? "거절 답" : "답 없음", ans != null && ans.type == "refusal" ? FailKind.Refusal : FailKind.NoAnswer);
            return;
        }
        _consecutiveFails = 0;
        S.AnsReceived++;
        if (S._blockedSince >= 0f)
        {
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} unblocked: no usable answer for {now - S._blockedSince:F2}s before this one ({S._blockedFails} failed call(s), last cause {S._failKind}); meanwhile the running action went on or the body stood (minimal reflex)");
            S._blockedSince = -1f; S._blockedFails = 0;
        }
        S._failKind = FailKind.None;
        var probs = new List<string>();
        if (ans.probabilities != null)
        {
            var ps = new List<DProb>(ans.probabilities);
            ps.Sort((a, b) => b.probability.CompareTo(a.probability));
            for (int i = 0; i < ps.Count && i < 3; i++) probs.Add($"{ps[i].value}={ps[i].probability:F2}");
        }
        AICandidate chosen = _reqCands?.Find(c => c.value == ans.choice);
        string head = $"DECISION #{_reqId} RESULT choice={ans.choice} ({(chosen != null ? Trim(AIContinueText.ForShow(chosen.desc), 80) : "?")}) top=[{string.Join(", ", probs)}] confidence={ans.confidence:F2} latency={latency:F2}s tokens={tokens} cached={cached} cost=${usd:F6}";
        if (AITestSwitches.DecisionLog)
        {
            // TEST ONLY: the whole answer (every probability) and what is running when it arrives; what happens to it
            // (APPLY / HOLD / DISCARD / QUEUED) follows as the mirrored DECISION line
            var all = new List<string>();
            if (ans.probabilities != null) { var ps = new List<DProb>(ans.probabilities); ps.Sort((a, b) => b.probability.CompareTo(a.probability)); foreach (var p in ps) all.Add($"{p.value}={p.probability:F3}"); }
            AITestSwitches.Write($"DECISION #{_reqId} ANSWER choice={ans.choice} [{chosen?.kind} {chosen?.key}] probs=[{string.Join(", ", all)}] confidence={ans.confidence:F3} latency={latency:F2}s running={(AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current.Key : "-")}");
        }

        string stale = StaleReason(server, player, now, chosen, latency, _reqRev);
        if (stale == null && _reqPrefetch)
        {
            bool stillRunning = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running && AIBody.Current == _prefetchFor;
            if (stillRunning)
            {
                _queued = chosen; _queuedFor = _prefetchFor; _queuedReqStart = _reqStart; _queuedRev = _reqRev; _queuedId = _reqId;
                AIPlayerSpikeMod.Log($"{head} QUEUED (prefetch: starts when '{_prefetchFor.Name}' ends as done)");
                return;
            }
            if (!_prefetchSkillDone) stale = "미리 물은 행동이 정상으로 끝나지 않음";
            else
            {
                _prefetchSkillDone = false;
                PrefetchUsed++;
                Applied++; S.AnsStarted++;
                _needCall = false;
                AIPlayerSpikeMod.Log($"{head} APPLY (prefetch; arrived after '{_prefetchFor?.Name}' ended)");
                Apply(server, player, now, chosen);
                return;
            }
        }
        if (stale != null)
        {
            Discarded++;
            AIPlayerSpikeMod.Log($"{head} DISCARD: {stale}");
            // Combat stage 2 (HEAD review item 1): an answer that is no longer of use (late, already far enough, about a
            // run that ended, target gone) is dropped and asked again with a new observation; it is not an action failure.
            // Until now it was counted into the failure memory and a key discarded twice was left out for 30 s — a stable
            // option ("left 2", "keep away") could vanish only because answers were late. Only real execution failures
            // (the skill's own end: blocked, no item, ...) are remembered (OnSkillEnded).
            if (_reqPrefetch && !_prefetchSkillDone && AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running && AIBody.Current == _prefetchFor)
            {
                PrefetchDropped++;
                return;   // the running action goes on; the normal call follows when it ends
            }
            _prefetchSkillDone = false;
            _needCall = true;
            _needReason = "이전 답 버림(" + stale + ")";
            return;
        }
        ApplyOrHold(server, player, now, chosen, head);
    }

    private static void CallFailed(World server, Entity player, float now, string why, FailKind kind)
    {
        Failed++;
        // combat stage 2 (HEAD review item 5): the wait and the "3 in a row" rule by cause in combat (AIRunGate); out of
        // combat 3 s and the 3-in-a-row stop as before
        bool combat = _inCombat;
        if (AIRunGate.CountsTowardStop(kind, combat)) _consecutiveFails++;
        float wait = AIRunGate.RetryDelay(kind, combat, Settings().combatMinGapSeconds);
        _retryAt = now + wait;
        S._failKind = kind; S._blockedFails++;
        if (S._blockedSince < 0f) S._blockedSince = _reqStart;
        _needCall = true;
        _needReason = "재시도(" + why + ")";
        if (combat) AIPlayerSpikeMod.Log($"DECISION #{_reqId} failed ({kind}: {Trim(why, 60)}); in combat: ask again in {wait:F2}s{(AIRunGate.CountsTowardStop(kind, combat) ? $", failures in a row {_consecutiveFails}" : ", not counted toward the 3-in-a-row stop")}; no usable answer since {now - S._blockedSince:F2}s");
        if (_consecutiveFails >= 3)
        {
            AIPlayerSpikeMod.Reply($"AI: 판단 호출이 3번 연속 실패해 목표를 멈춥니다 ({why}).", ChatKind.Error);
            ClearGoal(new GoalEnd("3 consecutive failures: " + why, "system"));
            AIBody.Stop(server, player, now, "판단 호출 실패");
            _consecutiveFails = 0;
        }
        else if (_consecutiveFails == 1)
        {
            AIPlayerSpikeMod.Reply($"AI: 판단 호출 실패({why}), {wait:F1}초 뒤 다시 시도합니다.", ChatKind.Error, "decisions-fail");
        }
    }

    private static string StaleReason(World server, Entity player, float now, AICandidate c, float age, int rev)
    {
        if (c == null) return "모르는 선택값";
        if (rev != Revision) return $"목표가 바뀜(rev {rev}→{Revision})";
        if (age > Settings().maxResponseAgeSeconds) return $"응답이 너무 늦음({age:F1}초)";
        return AIActions.Stale(server, player, c);
    }

    private static void Apply(World server, Entity player, float now, AICandidate c)
    {
        AISkill s = AIActions.Make(c);
        if (s != null && c.kind == "explore") s.Tag = "explore:" + c.exploreDirection;
        AIVitals.NoteChoice(Trim(AIContinueText.ForShow(c.desc), 50));
        if (Verbose) AIPlayerSpikeMod.Reply($"AI 판단: {Trim(AIContinueText.ForShow(c.desc), 80)}", ChatKind.Status);
        if (s == null) return;
        // v2 §5: choosing the same action and target again continues it instead of restarting it
        if (AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running && !string.IsNullOrEmpty(s.Key) && s.Key == AIBody.Current.Key)
        {
            AIPlayerSpikeMod.Log($"BRAIN: chose the running action again ({s.Key}); continuing it");
            return;
        }
        // recheck N1: the close of the door the closing tail is closing now continues the tail (not a new close from the start)
        if (c.kind == "doorclose" && AIBody.TailRunning && AIEdge.TailContinues(c.key))
        {
            AIPlayerSpikeMod.Log($"BRAIN: chose {c.key} while the closing tail closes that door; continuing the tail");
            return;
        }
        if (_queued != null && _queued != c) DropQueued("다른 행동 시작");
        _applying = true;
        try { AIBody.StartSkill(server, player, s, now); }
        finally { _applying = false; }
        S._runKey = s.Key ?? ""; S._runProgress = c.progress;
        // combat stage 2: what was chosen against a charging enemy and what its option predicted (prediction vs result)
        if (!string.IsNullOrEmpty(c.pred))
        {
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} PICK {c.key} run #{s.Run} pred={c.pred}{MirrorNote(c)}");
            AIChargeWatch.NotePick(player, _reqId, c.key, c.pred, now);
        }   // its "continue" option later says whether it was a goal step
    }

    // Left/right record (② asks to record, not to change): the mirrored option in the same request and whether its predicted
    // outcome code is the same (tie) or not (the facts tell left from right).
    private static string MirrorNote(AICandidate c)
    {
        string k = c.key ?? "", m = null;
        if (k.Contains("left")) m = k.Replace("left", "#").Replace("right", "left").Replace("#", "right");
        else if (k.Contains("right")) m = k.Replace("right", "left");
        if (m == null) return "";
        var mc = _reqCands?.Find(x => x.key == m);
        if (mc == null) return $" lr=alone({m} not offered)";
        return mc.pred == c.pred ? $" lr=tie({m} pred={mc.pred})" : $" lr=differs({m} pred={mc.pred})";
    }

    // ------------------------------------------------------------------ candidates and input

    private static string _blockNote { get => S._blockNote; set => S._blockNote = value; }

    // The block note and whether it holds a tool-short block, always written together (refactor S2, review F5).
    internal static void SetBlockNote(string note, bool toolShort) { S._blockNote = note; S._blockToolShort = toolShort; }

    // refactor S2b: the flag decides; with the test switch, the sentence is compared (TOOLSHORT-MISMATCH)
    private static bool ToolShortRule(string note, bool flag, string where)
    {
        if (AITestSwitches.Test)
        {
            string mm = AIOutcomes.Mismatch("toolshort-" + where, AIOutcomes.ToolShortText(note), flag, $"note='{Trim(note, 120)}'");
            if (mm != null) AIPlayerSpikeMod.Log("TOOLSHORT-MISMATCH " + mm);
        }
        return flag;
    }

    private static bool _inCombat { get => S._inCombat; set => S._inCombat = value; }
    private static World _server { get => BC._server; set => BC._server = value; }

    // Observed (not game-data) estimate of hits and seconds to kill with this weapon, or "unknown".
    private static bool _weaponBroken { get => BC._weaponBroken; set => BC._weaponBroken = value; }   // set by BuildCandidates: the weapon the attack skill will use is broken (durability 0)
    private static string KillEstimate(ObjectID weapon, int enemyHp)
    {
        if (weapon == ObjectID.None) return "처치 시간 모름(무기 없음).";
        if (AIVitals.HitStats(AIVitals.HitKey(weapon, _weaponBroken), out float per, out float sec))
        {
            int hits = (int)math.ceil(enemyHp / math.max(1f, per));
            return $"관찰한 {weapon}{(_weaponBroken ? "(부서진 상태)" : "")} 피해 약 {per:F0}/타, 타격 간격 약 {sec:F1}초 → 약 {hits}타, {hits * sec:F1}초.";
        }
        if (_server != null && AIVitals.WeaponData(_server, weapon, out int dmg, out float cd))
        {
            int hits = (int)math.ceil(enemyHp / (float)dmg);
            return $"{weapon} 게임 자료 기본 피해 {dmg}{(cd > 0 ? $", 쿨다운 {cd:F2}초" : "")} → 대략 {hits}타(스킬·방어{(_weaponBroken ? "·부서진 상태" : "")} 반영 안 됨, 실제는 다를 수 있음).";
        }
        return $"처치 시간 모름({weapon} 피해를 아직 못 봄).";
    }

    // AITunnel.Plan for candidate descriptions, cached 1.5 s per target while the AI is within 2 tiles of where it was
    // planned (combat re-decides every ~0.3 s; the skills plan again themselves when they start).
    private const int FrontierTilesPerFrame = 400;
    private static void FrontierJobDone(State st, float now, string how)
    {
        var j = st._fJob; st._fJob = null;
        st._frontier = j.Result; st._frontierAt = now; st._frontierTile = j.Start; st._frontierExpanded = j.Expanded;
        AIPlayerSpikeMod.Log($"BRAIN: frontier search done {how}: expanded {j.Expanded} tiles, {j.Time.ElapsedMilliseconds} ms of work, found {j.Result.Count}");
    }

    // A walk got stuck on a tile the plans counted as open (AIObjects.NoteStuck): cached routes may go through it.
    internal static void ForgetPlans() => S._plans.Clear();
    // 제작대-놓기 검수 E3: one target's cached route (a dig stopped on edge walls a replan found)
    internal static void ForgetPlan(int2 target) => S._plans.Remove(((long)target.x << 32) ^ (uint)target.y);

    // detour (검수 H1): with the detour check (plan.alt) — a cached plan made without it does not serve such a call; one
    // made with it serves every call (its first way is the same). The cache's `seen` field holds the flag (1 = checked).
    internal static TunnelPlan PlanC(World server, Entity player, int2 target, out string why, bool detour = false)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float now = Time.realtimeSinceStartup;
        int2 me = AIEdge.StandTile(server, AIBody.Pos(server, player));   // 재실측 결함 2
        long k = ((long)target.x << 32) ^ (uint)target.y;
        var plans = S._plans;
        TunnelPlan plan;
        // tolerance grows with the route (a 90-tile retreat route is the same route one or two steps later).
        // Out of a fight a plan is kept 5 s (2 s in a fight): with four AIs the peaceful decisions every ~3 s missed the
        // 2 s cache and recomputed 8-10 plans (~16 ms each), 200-250 ms in one frame (2026-11-04 넷이 함께 플레이, Claude
        // gathering TinOre). The walk/dig skill plans its route again when it starts, so a 5 s old count only describes.
        // Chest, base and remembered-station routes (AIBase, craft) also come through here now (they were uncached).
        // A plan that found no way (tool-short walls all round, unseen tiles) searched every known tile in its box
        // (up to 120000 steps, 20-30 ms) and was kept only while the AI stayed within 2 tiles: in a fight Claude moved,
        // and two remembered TinOre spots behind temple walls were searched again on every 0.3 s call (10 plans,
        // 110-190 ms per decision, 2026-11-04 넷이 함께 플레이 2차 t=429-930). "No way" does not change by moving a few
        // tiles: kept 10 s and within an eighth of the distance to the target (at least 6 tiles).
        bool okC = plans.TryGetValue(k, out var c) && (!detour || c.seen == 1);
        if (okC && c.plan == null && now - c.at < 10f && math.cmax(math.abs(c.from - me)) <= math.max(6, (int)(math.distance((float2)me, (float2)target) / 8f)))
        {
            plan = null; why = c.why; _nPlansCached++;
        }
        else if (okC && now - c.at < (_inCombat ? 2f : 5f) &&math.cmax(math.abs(c.from - me)) <= math.max(2, (c.plan != null ? c.plan.path.Count : 0) / 10))
        {
            plan = c.plan; why = c.why; _nPlansCached++;
        }
        else
        {
            plan = AITunnel.Plan(server, player, target, out why, -1, false, detour);
            if (plans.Count > 64) plans.Clear();
            plans[k] = (now, me, detour ? 1 : 0, plan, why);
            _nPlans++;
        }
        _msPlans += sw.Elapsed.TotalMilliseconds;
        return plan;
    }

    // Seconds to kill an enemy with this much health (observed weapon stats), -1 if unknown.
    private static float KillSeconds(ObjectID weapon, int enemyHp)
    {
        if (weapon == ObjectID.None || !AIVitals.HitStats(AIVitals.HitKey(weapon, _weaponBroken), out float per, out float sec)) return -1f;
        return (int)math.ceil(enemyHp / math.max(1f, per)) * sec;
    }

    // What a fight with this enemy looks like from this character's own experience: time to kill it (observed damage
    // per hit and hit interval), the damage this kind did to it and how often, hence how many hits and seconds until it
    // would be down at its health now, and how often it went down near this kind. Unknowns are said to be unknown.
    // Facts only; the decision model weighs them (no program rule like "do not attack a strong enemy").
    private static string FightNote(ObjectID weapon, SeenEnemy e, int myHp)
    {
        var sb = new StringBuilder(KillEstimate(weapon, e.health));
        float kill = KillSeconds(weapon, e.health);
        float die = -1f;
        if (AIVitals.TakenStats(e.id, out float per, out int n))
        {
            int hits = (int)math.ceil(myHp / math.max(1f, per));
            sb.Append($" 이 종류에게 받은 피해(관찰 {n}번) 약 {per:F0}/번 → 내 체력 {myHp}이면 약 {hits}번 맞으면 쓰러짐");
            if (AIVitals.TakenGap(e.id, out float gap)) { die = hits * gap; sb.Append($", 연달아 맞을 때 간격 약 {gap:F1}초(관찰) → 붙어 있으면 약 {die:F1}초"); }
            sb.Append('.');
        }
        else sb.Append(" 이 종류에게 받는 피해는 모름(맞고 버틴 기록 없음).");
        int lethal = AIVitals.LethalHit(e.id);
        if (lethal > 0) sb.Append($" 이 종류에게 체력 {lethal}에서 한 번 맞고 바로 쓰러진 적 있음(한 번에 {lethal} 이상, 내 체력 지금 {myHp}).");
        int downs = AIVitals.DownsNear(e.id);
        if (downs > 0) sb.Append($" 이 종류가 근처에 있을 때 쓰러진 기록 {downs}번(내 기록).");
        if (kill > 0f && die > 0f) sb.Append($" 비교(관찰 기준, 다른 적은 뺌): 처치까지 약 {kill:F1}초 vs 내가 쓰러지기까지 약 {die:F1}초.");
        return sb.ToString();
    }

    private static string TakenNote(ObjectID enemy)
    {
        int lethal = AIVitals.LethalHit(enemy), downs = AIVitals.DownsNear(enemy);
        string extra = (lethal > 0 ? $" 한 번 맞고 체력 {lethal}에서 바로 쓰러진 적 있음." : "") + (downs > 0 ? $" 이 종류 근처에서 쓰러진 기록 {downs}번." : "");
        return (AIVitals.TakenStats(enemy, out float per, out int n) ? $"이 종류에게 받은 피해(관찰 {n}번) 약 {per:F0}/번." : "이 종류의 피해량 모름.") + extra;
    }
    private static readonly Dictionary<string, float> _fullLog = new Dictionary<string, float>();
    // An enemy within 4.5 tiles (keep away is offered) or two within 6 (the multi-enemy line): a close fight.
    // 단계 3: an enemy that cannot come (behind walls / a closed door, judged in this build: AIEdge.ObserveThreat) is not in the close fight
    private static bool CloseFight(AIObservation o) => o.enemies.Exists(x => x.dist <= 4.5f && !AIEdge.LastAssessedCannotCome(x.entity)) || o.enemies.FindAll(x => x.dist <= 6f && !AIEdge.LastAssessedCannotCome(x.entity)).Count >= 2;
    private static bool _closeSet { get => BC._closeSet; set => BC._closeSet = value; }   // the last candidate build used the close-fight set
    private static string _setLog { get => BC._setLog; set => BC._setLog = value; }   // for the SEND log: " set=close-fight", " set=all(close-fight off)" or ""
    private static string _swarmLog { get => BC._swarmLog; set => BC._swarmLog = value; }   // the last input's swarm line, for the SEND log (per call, so not per character state)
    private static string DirName(float2 v) => math.abs(v.x) >= math.abs(v.y) ? (v.x >= 0 ? "동" : "서") : (v.y >= 0 ? "북" : "남");

    // Where a movement option goes relative to the enemies in the fight (within combatRange), so that moves that look
    // alike (keep away, retreat, an explore walk, following someone) can be told apart by the decision model: toward
    // them, away from them, or sideways. Empty when no enemy is in combat range. A fact, not advice.
    // The running movement (keep away, retreat, an explore walk or dig, following) and the tile its next steps go to;
    // set by BuildCandidates. A movement option whose first steps go the same way gets a note: switching would not
    // change where the character goes now, only restart the action (keep away <-> retreat every 0.3-0.7 s at 1-2 tiles
    // from 2-3 enemies, Gemini, ai2 swarm3).
    private static string _curMoveName { get => BC._curMoveName; set => BC._curMoveName = value; }
    private static int2? _curMoveTo { get => BC._curMoveTo; set => BC._curMoveTo = value; }
    private static string SameWay(AIObservation o, int2 target)
    {
        if (!_curMoveTo.HasValue) return "";
        float2 me = o.pos.xz, a = (float2)_curMoveTo.Value - me, b = (float2)target - me;
        if (math.length(a) < 0.5f || math.length(b) < 0.5f) return "";
        return math.dot(math.normalize(a), math.normalize(b)) > 0.7f ? $" 지금 하는 {_curMoveName}과(와) 처음 가는 쪽이 같음({DirName(b)}쪽): 바꿔도 당장 움직이는 쪽은 거의 같고, 지금 행동은 멈추고 이 행동이 처음부터 시작됨." : "";
    }

    private static string Toward(AIObservation o, int2 target)
    {
        var fight = o.enemies.FindAll(x => x.dist <= Settings().combatRange);
        if (fight.Count == 0) return "";
        string same = SameWay(o, target);
        float2 me = o.pos.xz, mv = (float2)target - me;
        if (math.length(mv) < 0.5f) return "";
        float2 c = float2.zero;
        foreach (var e in fight) c += e.pos.xz;
        float2 ce = c / fight.Count - me;
        // enemies on more than one side (centroid close to the AI while they are not): no single "their side"
        float spread = 0f;
        foreach (var e in fight) spread = math.max(spread, math.length(e.pos.xz - me));
        if (fight.Count >= 2 && math.length(ce) < 0.35f * spread) return $" 가는 쪽: {DirName(mv)}쪽(적들이 여러 쪽에 있음).{same}";
        float cos = math.dot(math.normalize(mv), math.normalize(ce));
        string who = fight.Count == 1 ? $"적({DirName(ce)}쪽)" : $"적 {fight.Count}마리(대부분 {DirName(ce)}쪽)";
        return (cos > 0.5f ? $" 가는 쪽: {who} 쪽으로 감." : cos < -0.5f ? $" 가는 쪽: {who} 반대쪽으로 감." : $" 가는 쪽: {who}의 옆쪽({DirName(mv)}쪽).") + same;
    }

    // One line about the fight as a whole when several enemies are close (the per-enemy facts are on the attack options):
    // how many within 3 and 6 tiles, of which kinds and sides, the damage taken in the last 5 s and what that rate means
    // for this health. Facts only.
    private static string SwarmNote(AIObservation o)
    {
        var near = o.enemies.FindAll(x => x.dist <= 6f);
        if (near.Count < 2) return "";
        int c3 = near.FindAll(x => x.dist <= 3f).Count;
        var kinds = new Dictionary<ObjectID, int>(); var sides = new Dictionary<string, int>();
        foreach (var e in near)
        {
            kinds.TryGetValue(e.id, out int k); kinds[e.id] = k + 1;
            string s = DirName(e.pos.xz - o.pos.xz); sides.TryGetValue(s, out int n); sides[s] = n + 1;
        }
        var ks = new List<string>(); foreach (var kv in kinds) ks.Add($"{kv.Key} {kv.Value}");
        var ss = new List<string>(); foreach (var kv in sides) ss.Add($"{kv.Key} {kv.Value}");
        int lost = -AIVitals.HpChange(5f, Time.realtimeSinceStartup);
        string rate = lost > 0 ? $", 최근 5초에 받은 피해 {lost}(초당 약 {lost / 5f:F0}) → 이 속도면 체력 {o.health}이 약 {o.health / math.max(0.1f, lost / 5f):F0}초에 0" : ", 최근 5초에 받은 피해 없음";
        return $"여러 적과 싸우는 중: 6칸 안 적 {near.Count}마리({string.Join(", ", ks)}), 그중 3칸 안 {c3}마리; 방향 {string.Join(", ", ss)}{rate}.";
    }

    private static List<AICandidate> BuildCandidates(World server, Entity player, AIObservation o, bool prefetch, bool breachCall)
    {
        var b = new CandBuild(server, player, o, prefetch, breachCall);
        b.Prep();
        b.Continue();
        b.Enemies();
        b.CombatOptions();
        b.Brute();
        b.Food();
        b.Walls();
        b.DigOre();
        b.Breakables();
        b.Roots();
        b.RememberedOres();
        b.Items();
        b.Follow();
        b.Help();
        b.Explore();
        return b.Assemble();
    }

    // ---- refactor S6a: assembly recorder (test switch decisionlog.on only). What the final assembly of BuildCandidates
    // (regroup, set choice, body filter, failure marks, limit, wait, values) gets and gives, written to the decision log as
    // ASM-IN / ASM-OUT lines, so the Unity-free assembler (S6b) can be replayed offline against the old code. It only reads
    // values the build already has; the two RemoveAll predicates record what they evaluated (AsmNote) and call nothing more
    // (review F10: no extra PlanC). Candidate ids follow object identity (the same object in two lists has one id).
    private static int _asmBuilds;
    private sealed class AsmRec
    {
        public int Build, Depth; public float Tnow0, Tnow = -1f;
        public readonly Dictionary<AICandidate, int> Ids = new Dictionary<AICandidate, int>();
        public readonly Dictionary<int, bool> BodyCan = new Dictionary<int, bool>(), Doable = new Dictionary<int, bool>();
        public int Id(AICandidate c) { if (!Ids.TryGetValue(c, out int i)) { i = Ids.Count + 1; Ids[c] = i; } return i; }
    }
    private static bool AsmNote(AsmRec asm, Dictionary<int, bool> into, AICandidate c, bool value)
    {
        if (asm != null) into[asm.Id(c)] = value;
        return value;
    }
    private static string AsmF(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    private static string AsmList(AsmRec asm, List<AICandidate> l)
    {
        var parts = new List<string>();
        foreach (var c in l) parts.Add($"{{\"id\":{asm.Id(c)},\"kind\":{ExpJson.S(c.kind)},\"key\":{ExpJson.S(c.key)},\"desc\":{ExpJson.S(c.desc)},\"progress\":{(c.progress ? "true" : "false")},\"comesByItself\":{(c.comesByItself ? "true" : "false")}}}");
        return "[" + string.Join(",", parts) + "]";
    }
    private static void AsmIn(AsmRec asm, AIObservation o, AISkill cur, string curKey, bool free, bool canMine, bool canFight, string noMine, string noFight, HashSet<AICandidate> foodPicks,
        List<AICandidate> lCont, List<AICandidate> lFollow, List<AICandidate> lEnemy, List<AICandidate> lCombat, List<AICandidate> lCraft, List<AICandidate> lLife,
        List<AICandidate> lMine, List<AICandidate> lDig, List<AICandidate> lRemember, List<AICandidate> lItem, List<AICandidate> lExplore, List<AICandidate> lDigExplore, List<AICandidate> lOther, List<AICandidate> lBrute, List<AICandidate> lDoor, List<AICandidate> lEdge, bool combat, bool prefetch)
    {
        var sb = new StringBuilder();
        sb.Append($"{{\"b\":{asm.Build},\"depth\":{asm.Depth},\"goal\":{ExpJson.S(Goal.kind.ToString())},\"free\":{(free ? "true" : "false")},\"closeFight\":{(CloseFight(o) ? "true" : "false")},\"closeFightOff\":{(AITestSwitches.CloseFightOff ? "true" : "false")}");
        sb.Append($",\"cur\":{ExpJson.S(cur == null ? "" : cur.GetType().Name)},\"curKey\":{ExpJson.S(curKey)},\"prefetch\":{(prefetch ? "true" : "false")}");
        var near = o.enemies.FindAll(x => x.dist <= 6f);
        sb.Append($",\"near6\":{near.Count},\"enemy0\":{ExpJson.S(o.enemies.Count > 0 ? o.enemies[0].id.ToString() : "")},\"enemy0Dist\":{ExpJson.S(o.enemies.Count > 0 ? $"{o.enemies[0].dist:F1}" : "")}");
        sb.Append($",\"canMine\":{(canMine ? "true" : "false")},\"canFight\":{(canFight ? "true" : "false")},\"noMine\":{ExpJson.S(noMine)},\"noFight\":{ExpJson.S(noFight)},\"tnow0\":{AsmF(asm.Tnow0)},\"failMemory\":{AsmF(FailMemorySeconds)}");
        sb.Append(",\"lists\":{");
        sb.Append("\"cont\":").Append(AsmList(asm, lCont)).Append(",\"follow\":").Append(AsmList(asm, lFollow)).Append(",\"enemy\":").Append(AsmList(asm, lEnemy));
        sb.Append(",\"combat\":").Append(AsmList(asm, lCombat)).Append(",\"craft\":").Append(AsmList(asm, lCraft)).Append(",\"life\":").Append(AsmList(asm, lLife));
        sb.Append(",\"mine\":").Append(AsmList(asm, lMine)).Append(",\"dig\":").Append(AsmList(asm, lDig)).Append(",\"remember\":").Append(AsmList(asm, lRemember));
        sb.Append(",\"item\":").Append(AsmList(asm, lItem)).Append(",\"explore\":").Append(AsmList(asm, lExplore)).Append(",\"digExplore\":").Append(AsmList(asm, lDigExplore)).Append(",\"other\":").Append(AsmList(asm, lOther));
        sb.Append(",\"brute\":").Append(AsmList(asm, lBrute));   // combat stage 2 (absent in older records = empty)
        sb.Append(",\"door\":").Append(AsmList(asm, lDoor)).Append(",\"edge\":").Append(AsmList(asm, lEdge)).Append('}');   // review F1 / F4 (absent in older records = empty)
        sb.Append($",\"combat\":{(combat ? "true" : "false")},\"combatMax\":{Settings().combatMaxCandidates}");
        var fp = new List<string>(); foreach (var c in foodPicks) fp.Add(asm.Id(c).ToString());
        sb.Append(",\"foodPicks\":[").Append(string.Join(",", fp)).Append(']');
        var fl = new List<string>(); foreach (var kv in _fails) fl.Add($"{{\"key\":{ExpJson.S(kv.Key)},\"count\":{kv.Value.count},\"last\":{AsmF(kv.Value.last)},\"reason\":{ExpJson.S(kv.Value.reason)}}}");
        sb.Append(",\"fails\":[").Append(string.Join(",", fl)).Append(']');
        var lf = new List<string>(); foreach (var x in S._left) lf.Add($"{{\"key\":{ExpJson.S(x.key)},\"t\":{AsmF(x.t)}}}");
        sb.Append(",\"left\":[").Append(string.Join(",", lf)).Append("]}");
        AITestSwitches.Write($"ASM-IN #{asm.Build} {sb}");
    }
    private static void AsmOut(AsmRec asm, List<AICandidate> list, int noBody)
    {
        var sb = new StringBuilder();
        var items = new List<string>();
        foreach (var c in list) items.Add($"{{\"id\":{asm.Id(c)},\"kind\":{ExpJson.S(c.kind)},\"key\":{ExpJson.S(c.key)},\"value\":{ExpJson.S(c.value)},\"desc\":{ExpJson.S(c.desc)}}}");
        string Map(Dictionary<int, bool> m) { var p = new List<string>(); foreach (var kv in m) p.Add($"\"{kv.Key}\":{(kv.Value ? "true" : "false")}"); return "{" + string.Join(",", p) + "}"; }
        sb.Append($"{{\"b\":{asm.Build},\"tnow\":{AsmF(asm.Tnow)},\"setLog\":{ExpJson.S(_setLog)},\"skipped\":{ExpJson.S(_skippedNote)},\"noBody\":{noBody},\"blockNote\":{ExpJson.S(_blockNote)}");
        sb.Append(",\"bodyCan\":").Append(Map(asm.BodyCan)).Append(",\"doable\":").Append(Map(asm.Doable));
        sb.Append(",\"list\":[").Append(string.Join(",", items)).Append("]}");
        AITestSwitches.Write($"ASM-OUT #{asm.Build} {sb}");
    }

    // Walkable tiles in view connected to the AI's tile (objects such as doors are not seen by this flood).
    // Objects with a collider (the core, wires, plants, stations) block their footprint tiles (AIObjects).
    // BFS steps from the AI's tile for every tile of the last ReachableInView (walk length of an in-view walk target)
    private static Dictionary<int2, int> _reachDist => BC._reachDist;
    private static HashSet<int2> ReachableInView(AITileReader tr, AIObservation o, int2 me, HashSet<int2> objBlocked)
    {
        var seen = new HashSet<int2> { me };
        _reachDist.Clear(); _reachDist[me] = 0;
        var q = new Queue<int2>();
        q.Enqueue(me);
        int guard = 0;
        int2[] dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        while (q.Count > 0 && guard++ < 3000)
        {
            var p = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = p + d;
                if (seen.Contains(n) || !AIVision.CanSee(o.pos, n) || !tr.Walkable(n) || objBlocked.Contains(n)) continue;
                seen.Add(n);
                _reachDist[n] = _reachDist[p] + 1;
                q.Enqueue(n);
            }
        }
        return seen;
    }

    // Per direction (E/W/N/S quadrant), the reachable open tile with the most not-yet-seen area around it (memory);
    // tiles with nothing unseen nearby are not offered (this is what stops back-and-forth between two seen ends).
    private static List<(int2 t, string dir, int novelty)> ExploreTargets(HashSet<int2> reach, AIObservation o, int2 me)
    {
        var best = new Dictionary<string, (int2 t, int nov, float d)>();
        foreach (var t in reach)
        {
            float d = math.distance((float2)t, (float2)me);
            if (d < 3f) continue;
            int nov = AIMemory.Novelty(t);
            if (nov <= 0) continue;
            float tn = Time.realtimeSinceStartup;
            if (_noPathAreas.Exists(a => tn <= a.until && math.distance((float2)a.p, (float2)t) <= NoPathRadius)) { _exploreNoPath++; continue; }
            int2 v = t - me;
            string dir = math.abs(v.x) >= math.abs(v.y) ? (v.x > 0 ? "동" : "서") : (v.y > 0 ? "북" : "남");
            if (!best.TryGetValue(dir, out var b) || nov > b.nov || (nov == b.nov && d > b.d)) best[dir] = (t, nov, d);
        }
        var res = new List<(int2, string, int)>();
        foreach (var kv in best) res.Add((kv.Value.t, kv.Key, kv.Value.nov));
        res.Sort((a, b) => b.Item3.CompareTo(a.Item3));
        if (res.Count > 3) res.RemoveRange(3, res.Count - 3);
        return res;
    }

    private static string BuildInput(World server, Entity player, AIObservation o, string trigger)
    {
        var sb = new StringBuilder();
        sb.Append($"너는 Core Keeper(2D 탑뷰 채굴·생존 게임) 속 캐릭터 '{AIAgent.Cur.Name}'의 다음 행동 하나를 고른다. 목표를 이루는 데 지금 가장 알맞은 행동을 고르고, 적과 체력 같은 위험도 고려한다.\n");
        string prog = "";
        if (Goal.kind == GoalKind.Gather || Goal.kind == GoalKind.Craft)
        {
            AIBody.InventoryCounts(server, player).TryGetValue(Goal.item, out int c);
            prog = $" — 지금 {c - Goal.startCount}/{Goal.count}";
        }
        sb.Append($"목표: {Goal.Text}{prog}.\n");
        if (Goal.kind == GoalKind.Free) sb.Append($"자유 시간: 정해진 목표가 없음({AIGoals.FreeWhy}). 위쪽 LLM이나 호스트가 목표를 주면 그 목표로 바뀐다. 그때까지 할 일을 고른다 — 호스트·동료 따라가기, 가까운 곳 탐험, 동료 돕기, 기지에 가방 정리, 배고프면 먹기, 적이 오면 싸우기·피하기, 할 만한 것이 없으면 기다리기.\n");
        if (Goal.kind == GoalKind.None) sb.Append($"자기 방어 중: 목표는 없지만 {_sdWhy}. 싸우기·피하기·물러나기·먹기·기다리기 가운데 고른다. 위협이 3초 동안 없으면 프로그램이 판단을 멈추고 제자리에 선다.\n");
        sb.Append($"내 상태: 위치 ({o.pos.x:F0},{o.pos.z:F0}), 체력 {o.health}/{o.maxHealth}, 배고픔 {o.hunger}({AIFood.Meaning(o.hunger)}), 손에 든 것 {(o.equipped == ObjectID.None ? "맨손" : o.equipped.ToString())}.\n");
        sb.Append($"보이는 범위(사람 화면 크기 {o.half.x * 2:F0}x{o.half.y * 2:F0}칸, 어둠 미반영): 걸을 수 있는 칸 {o.walkable}, 벽 {o.walls}(광석 {o.ores}), 적 {o.enemies.Count}, 떨어진 아이템 {o.items.Count}.\n");
        var inv = AIBody.InventoryCounts(server, player);
        var invs = new List<string>();
        foreach (var kv in inv) if (invs.Count < 12) invs.Add($"{kv.Key} {kv.Value}");
        sb.Append($"가방: {(invs.Count == 0 ? "비어 있음" : string.Join(", ", invs))}.\n");
        var cur = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current : null;
        sb.Append($"지금 하는 일: {(cur == null ? "없음(서 있음)" : $"{cur.Name} {cur.Key} {Time.realtimeSinceStartup - cur.StartTime:F0}초째")}.{(cur != null ? AIBody.DoorStepNote : "")}\n");   // 검수 F7
        if (cur != null) sb.Append($"지금 동작: {(AIMotion.AtBoundary ? "끊기 좋은 때(휘두르기·한 칸 걸음 사이)" : AIMotion.Mid)}. 다른 행동을 고르면 프로그램은 지금 동작이 끝나는 때(게임의 휘두르기 쿨다운·한 칸 걸음·먹기 끝)에 바꾸고, 크게 맞거나 적이 바로 옆에 붙으면 바로 바꾼다.\n");
        if (_held != null) sb.Append($"바꾸기 대기 중: 앞 판단이 '{Trim(_held.desc, 50)}'을(를) 골라 지금 동작이 끝나기를 기다리는 중. 같은 것을 고르면 그대로 바꾸고, 계속하기를 고르면 바꾸지 않는다.\n");
        // combat stage 2 (step-1 part, 3-판단시험.md T4/T6): in a close fight with a charging enemy the history lines (downs this
        // session, damage per hit of this kind, the last four results) pulled the choice toward leaving the fight; there
        // the input says what matters now (the charge facts below, the damage of one charge) and only the latest result.
        // The memory itself is unchanged.
        bool bruteFight = !string.IsNullOrEmpty(BC._bruteObs);
        string swh = SwitchHistory(Time.realtimeSinceStartup, bruteFight);
        if (swh != "") sb.Append(swh + "\n");
        if (bruteFight) sb.Append($"직전 행동 결과: {(_recent.Count == 0 ? "없음" : _recent[_recent.Count - 1])}.\n");
        else sb.Append($"최근 결과: {(_recent.Count == 0 ? "없음" : string.Join(" / ", _recent))}.\n");
        sb.Append($"화면에 보이는 다른 플레이어: {AITeammates.Summary(AITeammates.Last)}. 다른 플레이어는 같은 편이라 공격 대상이 아니다.\n");
        if (o.enemies.Count > 0) sb.Append($"전투 상황: 보이는 적 {o.enemies.Count}, 가장 가까운 적 {o.enemies[0].id} {o.enemies[0].dist:F1}칸, 최근 5초 내 체력 변화 {AIVitals.HpChange(5f, Time.realtimeSinceStartup):+0;-0;0}{(bruteFight ? "" : $", 이번 접속 쓰러짐 {AIVitals.Downs}번")}. 프로그램은 스스로 피하거나 물러나지 않는다(고른 행동만 함).\n");
        string swarm = SwarmNote(o); _swarmLog = swarm;
        if (swarm != "") sb.Append(swarm + "\n");
        if (bruteFight) sb.Append(BC._bruteObs);
        if (!string.IsNullOrEmpty(_skippedNote)) sb.Append($"최근 두 번 실패해 잠시 후보에서 뺀 대상: {_skippedNote}.\n");
        sb.Append($"탐색 기억(이 월드): {AIMemory.Summary()}{(Goal.kind == GoalKind.Gather ? $", 그중 목표 품목 광석 {AIMemory.CountOres(Goal.item)}곳" : "")}. 기억은 마지막으로 본 때의 정보다.\n");
        if (!string.IsNullOrEmpty(_blockNote)) sb.Append($"갈 수 없는 대상: {_blockNote}.\n");
        if (!string.IsNullOrEmpty(_edgeMoreNote)) sb.Append(_edgeMoreNote + ".\n");   // recheck N4
        if (!string.IsNullOrEmpty(_prepNote)) sb.Append($"준비 길(제작): {_prepNote}.\n");
        string baseLine = AIBase.PromptLine(server, player, o);
        if (baseLine != "") sb.Append(baseLine + "\n");
        string farmLine = AIFarm.PromptLine(server, player, o);
        if (farmLine != "") sb.Append(farmLine + "\n");
        string cookLine = AICook.PromptLine(server, player, o);
        if (cookLine != "") sb.Append(cookLine + "\n");
        sb.Append($"지금 다시 판단하는 이유: {trigger}.");
        return sb.ToString();
    }

    private static string BuildJson(string input, List<AICandidate> cands)
    {
        var sb = new StringBuilder();
        sb.Append("{\"model\":").Append(Q(Settings().model));
        sb.Append(",\"input\":").Append(Q(input));
        sb.Append(",\"questions\":[{\"type\":\"choice\",\"name\":\"action\",\"instructions\":");
        sb.Append(Q($"목표와 지금 상황에서 {AIAgent.Cur.Name}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다)."));
        sb.Append(",\"choices\":[");
        for (int i = 0; i < cands.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"value\":").Append(Q(cands[i].value)).Append(",\"description\":").Append(Q(cands[i].desc)).Append('}');
        }
        sb.Append("]}]}");
        return sb.ToString();
    }

    private static string Q(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) sb.Append($"\\u{(int)ch:x4}"); else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    private static string Trim(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";

    // Ore overlay tile -> dropped object, via the same tile→object map the game uses (null when unknown).
    internal static bool TryOreObject(World server, int tileset, out ObjectID obj)
    {
        obj = ObjectID.None;
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileWithTilesetToObjectDataMapCD>()))
            {
                if (q.CalculateEntityCount() != 1) return false;
                var lookup = q.GetSingleton<TileWithTilesetToObjectDataMapCD>().lookup;
                if (lookup.TryGetValue((TileType.ore, (Tileset)tileset), out ObjectDataCD od)) { obj = od.objectID; return true; }
            }
        }
        catch { }
        return false;
    }
}
