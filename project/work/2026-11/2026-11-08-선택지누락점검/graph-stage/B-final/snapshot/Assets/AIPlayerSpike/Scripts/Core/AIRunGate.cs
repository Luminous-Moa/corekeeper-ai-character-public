// AIRunGate — the asynchronous contract between a decision answer and the action that runs (combat stage 2, original
// plan 2-개선계획.md 단계 2 "비동기 처리의 약속"; HEAD review 2026-11-05 items 1, 4, 5, 7, 9). Unity-free (Tools/core-tests
// CombatS2Tests replays the five conditions and the rules below).
// Identifiers: action key (what: "hit:123", "step:left:2"), candidate value (the choice in one request), decision id
// (the request), execution id = run number (one started action, AIBody.StartSkill).
// - A request remembers the run that was going when it was built; its "continue" answer applies to that run only. A run
//   that ended, or another run with the same key that started since, is not continued by it: the answer is stale
//   (dropped and asked again; a stale answer is not an action failure and never sets a key aside).
// - The short close-fight actions (step, hit once, approach, hold) are applied at once: no motion-boundary wait, no 2 s
//   forced apply. The other kinds keep the boundary rule as before.
// - Composite actions (user decision 2026-11-05: hit then move, approach then hit, move then hit) are one run with numbered
//   stages in a fixed order, no condition inside (no "dodge if a charge comes"); a new choice may cut in at any stage.
// - Choosing what already runs (same key) continues it; it never starts a second copy. A "hit again" choice made while a
//   swing of the same target runs is queued and starts as a new run when that swing is over (never from a continue).
// - One owner for the waiting switch: an answer that is applied, continued or queued replaces / drops the older waiting
//   switch; only a stale answer leaves it.
// (If Scripts/Core is ever flattened, this file becomes Scripts/AICoreRunGate.cs: a rename only.)

using System;
using System.Collections.Generic;

public enum AnswerOutcome { StaleContinue, Continue, ApplyNow, Hold, QueueAfter }

// Why a decision call gave nothing usable (HEAD review item 5): the waits differ.
public enum FailKind { None, RateLimit, Server, Network, Timeout, Client, Refusal, NoAnswer }

// 토큰 절감 ④ (2026-11-08, user decision): the action model sees the last Keep results in full; earlier ones only as
// counts per kind ("<skill> <status>"). Consecutive repeats are not merged.
public sealed class RecentResults
{
    public const int Keep = 20;
    private readonly List<(string kind, string text)> last = new List<(string, string)>();
    private readonly SortedDictionary<string, int> older = new SortedDictionary<string, int>(StringComparer.Ordinal);
    private int olderTotal;
    public int Count => last.Count + olderTotal;
    public string Last => last.Count == 0 ? null : last[last.Count - 1].text;
    public void Add(string kind, string text)
    {
        last.Add((kind, text));
        while (last.Count > Keep)
        {
            older.TryGetValue(last[0].kind, out int n); older[last[0].kind] = n + 1; olderTotal++;
            last.RemoveAt(0);
        }
    }
    public void Clear() { last.Clear(); older.Clear(); olderTotal = 0; }
    public string Text()
    {
        if (Count == 0) return "없음";
        var parts = new List<string>();
        if (olderTotal > 0) { var o = new List<string>(); foreach (var kv in older) o.Add(kv.Key + " " + kv.Value); parts.Add($"(그 앞 {olderTotal}건: {string.Join(", ", o)})"); }
        foreach (var x in last) parts.Add(x.text);
        return string.Join(" / ", parts);
    }
}

public static class AIRunGate
{
    // No goal and nothing running (2026-11-08 user decision, 항목6): the action model is asked only for these issue
    // triggers (the reason texts the existing triggers write), besides combat, an emergency and a charge anticipation
    // (flags). Draft list for the user's review — one place.
    public static readonly string[] IdleIssueTriggers =
    {
        "자기 방어",            // an enemy came within range or the character was hit (SelfDefence)
        "새 적 보임",           // a new enemy in view (running-skill watch)
        "체력 감소",            // health dropped by hpDropTrigger since the last call
        "긴급",                // big health drop / a new enemy right next to the AI / a charge aimed at me
        "쓰러졌다가",           // back up after being down
        "기지 둘레 부서짐",      // a newly broken part of the base edge
    };
    // Trigger names (not reason sentences): each trigger site calls AIBrain.MarkIssue(name); removing a name here turns it off.
    public static bool IsIdleIssue(string trigger) => System.Array.IndexOf(IdleIssueTriggers, trigger) >= 0;
    // The no-goal idle gate (B1): hold the call only when there is no goal, nothing runs,
    // and none of: combat, emergency, charge, a pending issue (kept through retries/discarded answers), a tree step,
    // an open bag/life/expedition menu.
    public static bool IdleHold(bool running, bool noGoal, bool inCombat, bool emergency, bool urgent,
        bool issuePending, bool treeStep, bool menuOpen)
        => !running && noGoal && !inCombat && !emergency && !urgent && !issuePending && !treeStep && !menuOpen;

    public static bool IsShort(string kind) => kind == "stop_action" || kind == "finish_goal" || kind == "request_plan" || kind == "step" || kind == "hit" || kind == "approach" || kind == "hold" || kind == "combo";

    // Movement options for the combat "switches between movement options" count (AIBrain.CombatTrack), by kind (it read
    // the first words of the option text before, IsMoveDesc).
    public static bool IsMoveKind(string kind) => kind == "away" || kind == "retreat" || kind == "explore" || kind == "dig" || kind == "follow" || kind == "step" || kind == "approach";

    public const string HitAgainPrefix = "hitnext:";   // "hit again when this swing is over" (key hitnext:<entity>)
    public static string HitKeyOf(string key) => key != null && key.StartsWith(HitAgainPrefix) ? "hit:" + key.Substring(HitAgainPrefix.Length) : key;

    // A "continue" answer built while run `answerRun` was going: null when it still applies, else why not.
    public static string ContinueStale(int answerRun, bool curRunning, int curRun, string answerKey, string curKey)
    {
        if (!curRunning) return answerRun > 0 ? $"계속하라던 실행 #{answerRun}이 이미 끝남" : "계속할 기술이 이미 끝남";
        if (answerRun > 0 && curRun != answerRun) return $"계속하라던 실행이 이미 바뀜(#{answerRun}→#{curRun}{(answerKey == curKey ? ", 같은 키의 새 실행" : "")})";
        return null;
    }

    // What to do with an answer that passed the stale checks. emergency: the request was about an emergency.
    public static AnswerOutcome Decide(string kind, string key, int answerRun, bool curRunning, int curRun, string curKey, bool atBoundary, bool emergency)
    {
        if (kind == "continue") return ContinueStale(answerRun, curRunning, curRun, key, curKey) == null ? AnswerOutcome.Continue : AnswerOutcome.StaleContinue;
        if (kind == "hit" && key != null && key.StartsWith(HitAgainPrefix))
            return curRunning && curKey == HitKeyOf(key) ? AnswerOutcome.QueueAfter : AnswerOutcome.ApplyNow;   // the swing already ended: swing now
        if (curRunning && !string.IsNullOrEmpty(key) && key == curKey) return AnswerOutcome.Continue;   // the same action: keep it, no restart
        if (!curRunning) return AnswerOutcome.ApplyNow;
        if (IsShort(kind)) return AnswerOutcome.ApplyNow;
        if (emergency || atBoundary) return AnswerOutcome.ApplyNow;
        return AnswerOutcome.Hold;
    }

    // Does an older waiting switch survive this answer? (only a stale answer leaves it; Hold replaces it)
    public static bool HeldSurvives(AnswerOutcome o) => o == AnswerOutcome.StaleContinue;

    // A charge anticipation aimed at the character started at `antAt`. Drop the request in flight and ask again only when
    // that request was built before it and is not itself such an urgent re-ask (a second anticipation right after the first
    // would otherwise drop every answer: the urgent request is kept and the next one sees both). One abort per anticipation.
    public static bool AbortForAnticipation(bool inFlight, float reqStart, bool reqUrgent, float antAt) => inFlight && reqStart <= antAt && !reqUrgent;

    // Review (memo "전조 계기에 거리 조건이 없다"): the urgent re-ask is made only when the charger is within the range where
    // its observation and options are in the input (the close-fight range, AIBrain.Candidates FindBrute). An anticipation
    // aimed at me from farther waits while that charge is still under way (anticipation / charging) and asks as soon as
    // the charger is within range; when the charge is over it is dropped. Before, it asked at once and the re-ask went out
    // without the charger's facts (the non-combat set) when it was beyond 8 tiles.
    public enum ChargeAskNow { Ask, Wait, Drop }
    public static ChargeAskNow ChargeAsk(bool chargeStillOn, float dist, float range)
        => !chargeStillOn ? ChargeAskNow.Drop : dist <= range ? ChargeAskNow.Ask : ChargeAskNow.Wait;

    // HEAD decision 2026-11-05 (5-검수반영 결정 1): an anticipation does not drop the waiting switch (a model choice that
    // waits for the motion boundary; pol.combat-choice-design "모델의 선택을 버리지 않음"). It still applies at its boundary
    // while the urgent re-ask is in flight; the re-ask's answer settles it by the one-owner rule (HeldSurvives: applied,
    // continued, held or queued replaces it; a stale answer leaves it). AIBrain.ChargeTrigger; core-tests C2 checks the source.

    // A transport timeout or a new observation does not revoke an already chosen waiting action.
    // Clear it when its goal/run context is replaced, the player is down, or a direct reset occurs.
    public const string AbortForCharge = "charge anticipation";
    public static bool AbortDropsWaiting(string why) => why == "new goal" || why == "goal cleared" || why == "AI down" || why == "reset" || why == "AI stopped self-defence";

    // When a waiting (held) switch starts by itself: nothing runs -> "idle", at the motion boundary -> "boundary", after
    // maxHold seconds -> "timeout", else it keeps waiting (null). AIBrain.TryApplyHeld.
    public static string HeldApplyNow(bool running, bool atBoundary, float heldFor, float maxHold)
        => !running ? "idle" : atBoundary ? "boundary" : heldFor > maxHold ? "timeout" : null;

    // Review F4: how a switch of the running action is counted. A short close-fight action is applied at once by design
    // ("short"); before, it was counted as MID-MOTION, the indicator of switches that did not wait for the boundary.
    public static string SwitchClass(string how, bool atBoundary)
        => how == "emergency" ? "emergency" : how == "short" ? "short" : !atBoundary ? "mid" : "boundary";

    // "Switches between movement options" (the old indicator of flip-flopping between similar moves) counts the switches
    // that are not short ones; the short ones between moves (step -> step, step -> approach ...) are counted apart.
    public static bool SimilarMove(bool fromMove, string toKind, string how) => fromMove && IsMoveKind(toKind) && how != "short";
    public static bool ShortMove(bool fromMove, string toKind, string how) => fromMove && IsMoveKind(toKind) && how == "short";

    // The kind of a running action from its key ("step:left:2" -> step, "hit:12" -> hit, "hold" -> hold).
    public static string KindOfKey(string key) => string.IsNullOrEmpty(key) ? "" : key.IndexOf(':') < 0 ? key : key.Substring(0, key.IndexOf(':'));

    // The decision input's line about recent switches (last 10 s). Out of the close fight with a charger: as before, every
    // switch and "the new action started from the beginning each time". In it (HEAD decision 2026-11-05 2): the short
    // actions are made to be chosen again and again and start at once with nothing to prepare, so a switch between two
    // short actions is only counted, without the restart sentence; the switches that leave or start a long action (attack,
    // keep away, eat, ...) are listed as before with the restart fact.
    public static string SwitchHistoryText(System.Collections.Generic.IList<(string from, string fromKind, string to, string toKind, float ran)> sw, bool closeFightWithCharger)
    {
        if (sw == null || sw.Count == 0) return "";
        var all = new System.Collections.Generic.List<string>();
        foreach (var s in sw) all.Add($"{s.from}({s.ran:F1}초 만에)→{s.to}");
        if (!closeFightWithCharger) return $"최근 10초 동안 행동을 {all.Count}번 바꿈: {string.Join(", ", all)}. 바꿀 때마다 새 행동은 처음부터 시작했다.";
        var longs = new System.Collections.Generic.List<string>(); int shorts = 0;
        foreach (var s in sw)
        {
            if (IsShort(s.fromKind) && IsShort(s.toKind)) shorts++;
            else longs.Add($"{s.from}({s.ran:F1}초 만에)→{s.to}");
        }
        var parts = new System.Collections.Generic.List<string>();
        if (longs.Count > 0) parts.Add($"최근 10초 동안 긴 동작(공격(계속)·거리 벌리기·먹기 등)이 걸린 전환 {longs.Count}번: {string.Join(", ", longs)}. 긴 동작은 바꾸면 처음부터 다시 시작한다.");
        if (shorts > 0) parts.Add($"최근 10초 동안 짧은 동작(짧은 이동·한 번 치기·접근·제자리·복합)끼리 바꾼 것 {shorts}번(짧은 동작은 고르면 바로 시작하고 끝나면 바로 다시 판단하게 되어 있다).");
        return string.Join(" ", parts);
    }

    // Review (memo AITargetEnd.How): a kill only when the death is seen — the health component is read and at or below 0.
    // A target that vanished, was disabled, is being destroyed without a readable health (hp -1 before), is not a kill.
    public static bool KilledSeen(bool exists, bool hasHealth, int hp) => exists && hasHealth && hp <= 0;

    public static FailKind Classify(bool netOk, long code, bool hasAnswer, bool refusal, bool timeout)
    {
        if (timeout) return FailKind.Timeout;
        if (!netOk && code == 0) return FailKind.Network;
        if (code == 429) return FailKind.RateLimit;
        if (code >= 500) return FailKind.Server;
        if (code != 200) return FailKind.Client;
        if (refusal) return FailKind.Refusal;
        return hasAnswer ? FailKind.None : FailKind.NoAnswer;
    }

    // Seconds before asking again. Out of combat 3 s for everything (as before). In combat: a refusal / empty answer is a
    // normal exchange that gave no usable choice — ask again with the next observation after the combat floor; network,
    // server and timeout 1 s; rate limit and client errors (auth, request format) keep 3 s.
    public static float RetryDelay(FailKind k, bool combat, float combatFloor)
    {
        if (!combat) return 3f;
        switch (k)
        {
            case FailKind.Refusal: case FailKind.NoAnswer: return combatFloor;
            case FailKind.Network: case FailKind.Server: case FailKind.Timeout: return 1f;
            default: return 3f;
        }
    }

    // Does it count toward "3 failures in a row stop the goal"? In combat a refusal / empty answer does not (the input
    // changes every call; stopping the body would be an action the model did not choose).
    public static bool CountsTowardStop(FailKind k, bool combat) => !(combat && (k == FailKind.Refusal || k == FailKind.NoAnswer));

    // A new charge anticipation may ask again before the retry time when the failure was not about the service limits.
    public static bool UrgentMayBypassRetry(FailKind k) => k == FailKind.Refusal || k == FailKind.NoAnswer || k == FailKind.Network || k == FailKind.Server || k == FailKind.Timeout;
}

// One swing for the "hit once" action: press until the game starts a swing, release, end when that swing is over.
// Unity-free so the press/end rule is tested offline (a second swing or an endless hold must not happen).
public sealed class HitOnceLogic
{
    public const float NoTimerPress = 0.35f;   // timer unreadable: press at most this long (< one 0.4 s cooldown) then stop
    public const float StartWait = 1.0f;       // no swing start within this: the game did not swing
    // >= 0: the action ends this long after the swing started (the hit lands ~0.07 s after the start; a composite action
    // goes on to its move then, the game lets the body move while the swing finishes); < 0: when the swing is over
    public float EndAfterStart = -1f;
    private bool _started;
    private float _startedAt = -1f;
    public bool Started => _started;
    public float StartedAt => _startedAt;
    public bool Done { get; private set; }
    public string Reason { get; private set; } = "";
    public bool Failed { get; private set; }

    // t: seconds since the action started. timerOk: the game's swing timer could be read this frame. newSwing: a swing
    // start was seen since the action started. swingLeft: seconds left of the current swing. inReach: the target is in
    // reach now. Returns whether to hold the primary button this frame.
    public bool Step(float t, bool timerOk, bool newSwing, float swingLeft, bool inReach)
    {
        if (Done) return false;
        if (!_started && newSwing) { _started = true; _startedAt = t; }
        if (!timerOk)
        {
            // review: once a swing started, never press again (before: pressed up to 0.35 s after it, room for a second
            // swing when the attack speed is higher than the 0.4 s wood sword cooldown, pol.motion-real-speed)
            if (t < NoTimerPress) return inReach && !_started;
            Finish(false, $"휘두르기 1회(게임 타이머를 못 읽어 {NoTimerPress:F2}초만 누름)");
            return false;
        }
        if (_started)
        {
            if (EndAfterStart >= 0f && t - _startedAt >= EndAfterStart) Finish(false, $"휘두르기 시작 {EndAfterStart:F2}초 뒤 다음 단계로");
            else if (swingLeft <= 0f) Finish(false, "휘두르기 1회 끝");
            return false;   // released at once: the game swings once
        }
        if (!inReach) { Finish(true, "휘두르기 전에 대상이 휘두르는 거리 밖으로 벗어남"); return false; }
        if (t > StartWait) { Finish(true, $"{StartWait:F1}초 안에 휘두르기가 시작되지 않음"); return false; }
        return true;
    }

    private void Finish(bool failed, string reason) { Done = true; Failed = failed; Reason = reason; }

    // Review (asynchronous condition 4, weapon broken / replaced during the action): the weapon slot chosen at the start is
    // read every frame. sameItem: the slot still holds the weapon it started with; brokenNow / brokenAtStart: its
    // durability is 0 now / was at the start. The item gone or replaced: the action ends with that reason (before the
    // swing: failed, nothing pressed after; after it: the one swing is over) — it never swings with whatever is in the
    // hand. It broke on the way: the chosen one swing goes on (the game swings a broken weapon, with less damage); returns
    // true once so the caller records the hits under the broken key (AIVitals.HitKey).
    public bool WeaponCheck(bool sameItem, bool brokenNow, bool brokenAtStart)
    {
        if (Done) return false;
        if (!sameItem)
        {
            if (_started) Finish(false, "휘두르기 뒤 손의 무기가 바뀌거나 없어짐(휘두르기 1회로 끝)");
            else Finish(true, "휘두르기 전에 손의 무기가 바뀌거나 없어짐");
            return false;
        }
        if (brokenNow && !brokenAtStart && !_brokeSeen) { _brokeSeen = true; return true; }
        return false;
    }
    private bool _brokeSeen;
}

// How a short move started relative to what the body was doing (HEAD review item 8): moving already the same way, turning,
// reversing or from standing. Unity-free.
public static class MoveStart
{
    public static string Prior(float vx, float vy, float dx, float dy)
    {
        float sp = (float)System.Math.Sqrt(vx * vx + vy * vy), dl = (float)System.Math.Sqrt(dx * dx + dy * dy);
        if (sp < 0.5f || dl < 1e-4f) return "standstill";
        float cos = (vx * dx + vy * dy) / (sp * dl);
        return cos > 0.7f ? "same" : cos < -0.7f ? "reverse" : "turn";
    }

    // The body moves along the new direction: this frame's displacement goes that way (cos > 0.7, > 0.01 tile).
    public static bool Along(float mx, float my, float dx, float dy)
    {
        float ml = (float)System.Math.Sqrt(mx * mx + my * my), dl = (float)System.Math.Sqrt(dx * dx + dy * dy);
        return ml > 0.01f && dl > 1e-4f && (mx * dx + my * dy) / (ml * dl) > 0.7f;
    }
}
