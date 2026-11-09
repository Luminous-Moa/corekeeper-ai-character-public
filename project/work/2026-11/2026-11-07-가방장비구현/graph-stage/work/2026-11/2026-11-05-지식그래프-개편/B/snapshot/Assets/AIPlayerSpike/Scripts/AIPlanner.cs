// AIPlanner — the upper LLM (subscription CLI through the local ai-bridge) sets the next goal; OpenAI Decisions
// still picks every action. One planner state per AI character (AIAgents.cs); the bridge picks the member's CLI. Design: work/2026-11/2026-11-04-위쪽LLM-구독CLI-설계.md;
// report: work/2026-11/2026-11-04-위쪽LLM-첫단계.md (session 코어키퍼-AI-캐릭터).
//
// - Auto (the upper LLM picks goals by itself) is on after "/ai goal 알아서"; "/ai stop" or "/ai goal 없음" turns it off.
// - Watching (자연어 명령 2026-11-05, user decisions): the upper LLM is asked whenever auto is on OR a goal is running
//   (a host /ai goal or a goal from a mention included), and on every host mention whatever the mode. With neither a
//   goal nor auto there is no periodic look (not decided; left out). A goal ending is reported too (the goal was real).
// - Reports at: auto turned on, the host speaking to it (host_said: a chat line with its @mention or @모두; host_goal:
//   a /ai goal), goal done, goal stalled, nothing left to explore, back up after a down (also under a host goal now), a
//   fight that ended below half health or with a down (fight_end), and the periodic look every upperPeriodSeconds
//   (120 s; the clock restarts at every report sent and every answer) while watching (합의 3). One request at a
//   time; a waiting report is not replaced by a less urgent one (PlanInbox.Priority). While waiting, the current goal
//   keeps running (Decisions), or the member stands / self-defends.
// - The host's instruction is a priority the upper LLM may put off or give up by its own judgement (합의 2): the report
//   carries it (host_order) with its state, the answer may say "order": follow / defer / drop / done (with the reason
//   in say), and the change is logged (ORDER) and kept as an event. A host goal no longer blocks the upper LLM.
// - The answer is applied only if: it is for the last report and the current goal revision (a host /ai goal or
//   anything else that changed the goal makes it stale), younger than 180 s, a known goal kind, a gather item from
//   the report's list, count 1..30, follow only with the host in the world. Then AIBrain.SetGoal(…, "llm:<member>#n").
//   keep = go on with the current goal / plan; hold = put the current goal / plan down and stand, still watched (the
//   host's instruction stays as put off, 검수 B1) — every hold keeps the 2-minute look going until the next set or /ai
//   stop, on purpose (재검수 R4, user decision 3d). An answer to the host speaking is never lost: AIPlanRules.OnStale.
// - The host's instructions (AIHostOrders, 재검수 R1·R2·R3): the latest and up to two earlier open ones are in the report;
//   only the answer's "order" / "orders" fields change them; /ai stop closes the open ones as 멈춤 (AIPlanStop); a goal or
//   plan step that gets stuck leaves the instructions it carried open as 막힘 (실게임4 K2).
// - A plan (합의 3e): a set answer may give "plan" (up to upperPlanMaxSteps goal steps). Step 1 starts now; when a step
//   ends well (AIPlanRules.StepDone) the next one starts on the next tick without a call; a failed step, or one that
//   cannot start, stops the rest and reports (the report's "plan" shows the running plan or the one that stopped). The
//   action model (Decisions) only ever sees the step running now. A step that cannot be taken when the answer comes
//   (an unknown item, a recipe of a station not placed) cuts the plan there instead of throwing the answer away
//   (실게임4 K1): the say and order are applied, the steps before it are set (none: the goal stays), the why goes in the
//   next report's last_answer_rejected, and a re-ask goes at once only when nothing runs, once (AIPlanRules.PlanCut).
// - Failures: bridge not reachable -> told once, asked again after 2 min. timeout/spawn -> retry after 30/60/120 s
//   (3 per report, then again after 5 min). parse/check failed -> asked again once with the reason. limit/auth/login ->
//   told once, asked again after the bridge's retry_after_s (limit) or 10 min. Auto stays on through all of these;
//   meanwhile a member with no goal has free time (FreeTimeReason, AIBrain; 2026-11-04 막힘-멈춤-정리).
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class PlanGoalJ { public string @do; public string item; public string mode, second; public int count; public string @where; public int keep = -1, slot = -1; }
[Serializable] public class PlanForJ { public int report; public int goal_rev; }
[Serializable] public class PlanAnswerJ { public string decision; public PlanForJ @for; public PlanGoalJ goal; public string say; public string why; public string order; public string problem; [NonSerialized] public Dictionary<int, string> orders; }

public static class AIPlanner
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    internal sealed class State
    {
        public bool Auto;
        public UnityWebRequest _req;
        public float _reqStart;
        public int _reqReport;
        public int _reqRev;
        public int _reqDowns;   // AIVitals.Downs when the report was sent (a down while waiting makes the answer stale)
        public string _reqTrigger = "";
        public int _reportId;
        public int _tries;
        public bool _askedAgain;
        public float _retryAt = -1f;
        public readonly PlanInbox Inbox = new PlanInbox();   // the report waiting to be sent (one, the latest wins; refactor S4)
        public string _lastGoalText = "";
        public string _lastWhy = "";
        public string _lastSource = "";
        public float _lastGoalMinutes;
        public string _rejected;
        public readonly List<string> _events = new List<string>();
        public float _holdUntil = -1f;
        // the upper LLM cannot answer for now (usage over the bridge's limit, login/auth, bridge down, three failures):
        // auto stays on, the report is sent again at _retryAt; meanwhile a member with no goal has free time (AIBrain)
        public string _down; public bool _downTold;
        public int _hpSeen = -1;
        public float _hpSince = -1f;
        public int Calls;
        public int Applied;
        public int Dropped;
        public int Failed;
        public readonly List<float> _lat = new List<float>();
        public readonly List<ObjectID> _gatherFood = new List<ObjectID>();   // food items offered in the last report's can_do.gather
        // 자연어 명령 (2026-11-05): the periodic look, the chat lines already reported, the last applied decision and the
        // host's instructions (AIHostOrders: the latest and the earlier open ones, 재검수 R3)
        public float _lastPlanAt = -1000f;
        public int _chatSeen;
        public string _lastDecision; public float _lastDecisionAt;
        public readonly AIHostOrders Orders = new AIHostOrders();
        public int FightCalls;   // fight_end reports (user decision 3)
        // 검수 반영·계획 (2026-11-05, 4-검수반영·계획.md)
        public readonly AIPlanQueue Plan = new AIPlanQueue();   // the steps of the last set answer (합의 3e)
        public bool _planNext;          // a step ended well: start the next one on the next tick (no call)
        public bool HeldWatch;          // after a hold answer: still watched until the next set or /ai stop (B1, R4 on purpose)
        public int HostOrderSeq;        // +1 for every new host instruction (mention or /ai goal)
        public int _reqOrderSeq;        // HostOrderSeq when the report on the way was sent (F1)
        public int _reqChatSeenBefore;  // _chatSeen before the report on the way (put back when a host report goes again, B2)
        public bool _hostAskedAgain;    // a stale answer to the host was asked again once (B2)
        public int _hostTries;          // failed sends of the host's latest words (F2)
        public int _downsTold;          // AIVitals.Downs when a down was last told (back_up or fight_end, F4)
        public string _reqPlanSource;   // the plan running when the report on the way was sent (실게임2 N1), or null
        // 실게임3 M1: a plan that ended while a report about it was on the way owes its plan_done / goal_done call
        public string _owedPlanTrigger; // the owed call's trigger, or null
        public string _planEndedSource; // the source of that plan (the answer on the way is checked against it)
        public string _ansEnded, _ansOwed; public bool _ansOwedHandled;   // the same, for the answer being handled now
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Planner);
    private static State S => StateOf(AIAgent.Cur);
    internal static State Cur => S;   // AIPlanReport reads the report state through this

    // Puts a report into a NAMED character's inbox; safe outside a character scope (it does not use AIAgent.Cur).
    // The planner sends it from that character's tick like any waiting report. Nothing calls it yet (feature work 3).
    public static void Post(AIAgent owner, PlanTrigger t) => StateOf(owner).Inbox.Put(t);

    public const string BridgeUrl = "http://127.0.0.1:8331";
    // The world's recent talk (host mentions and every member's say), shared by all characters (AIChatLog).
    public static readonly AIChatLog Chat = new AIChatLog();
    private static float Period => AIBrain.Settings().upperPeriodSeconds;
    // Asked now: auto is on, a goal is running (free time is not a goal), a plan step is about to start, or a hold put a
    // goal or the host's instruction down (asked again later, 검수 B1). AIPlanRules.Watching.
    private static bool Watching => AIPlanRules.Watching(Auto, AIBrain.Goal.IsReal, S._planNext, S.HeldWatch);
    public static bool IsWatching => Watching;
    public static bool Auto { get => S.Auto; set => S.Auto = value; }
    private static UnityWebRequest _req { get => S._req; set => S._req = value; }
    private static float _reqStart { get => S._reqStart; set => S._reqStart = value; }
    private static int _reqReport { get => S._reqReport; set => S._reqReport = value; }
    private static int _reqRev { get => S._reqRev; set => S._reqRev = value; }
    private static int _reqDowns { get => S._reqDowns; set => S._reqDowns = value; }
    private static string _reqTrigger { get => S._reqTrigger; set => S._reqTrigger = value; }
    private static int _reportId { get => S._reportId; set => S._reportId = value; }
    private static int _tries { get => S._tries; set => S._tries = value; }               // per report (timeouts/spawn)
    private static bool _askedAgain { get => S._askedAgain; set => S._askedAgain = value; }         // one re-ask for parse/check failures
    private static float _retryAt { get => S._retryAt; set => S._retryAt = value; }
    private static string _pendingTrigger { get => S.Inbox.HasPending ? S.Inbox.Pending.Kind : null; set { if (value == null) S.Inbox.Clear(); else S.Inbox.Put(new PlanTrigger(value)); } }   // a report to send when the retry time comes
    private static string _lastGoalText { get => S._lastGoalText; set => S._lastGoalText = value; }
    private static string _lastWhy { get => S._lastWhy; set => S._lastWhy = value; }
    private static string _lastSource { get => S._lastSource; set => S._lastSource = value; }
    private static float _lastGoalMinutes { get => S._lastGoalMinutes; set => S._lastGoalMinutes = value; }
    private static string _rejected { get => S._rejected; set => S._rejected = value; }
    private static List<string> _events => S._events;
    private static float _holdUntil { get => S._holdUntil; set => S._holdUntil = value; }
    public static int Calls { get => S.Calls; set => S.Calls = value; }
    public static int Applied { get => S.Applied; set => S.Applied = value; }
    public static int Dropped { get => S.Dropped; set => S.Dropped = value; }
    public static int Failed { get => S.Failed; set => S.Failed = value; }
    private static List<float> _lat => S._lat;
    private static UnityWebRequest _health;

    public static void Reset()
    {
        AIExperience.OnReset();
        if (_req != null) { try { _req.Abort(); _req.Dispose(); } catch { } _req = null; }
        Auto = false; _pendingTrigger = null; _retryAt = -1f; _events.Clear(); _rejected = null; _holdUntil = -1f; S._down = null; S._downTold = false;
        S._lastPlanAt = -1000f; S._lastDecision = null; S.Orders.Clear();
        S.Plan.Clear(); S.Plan.ForgetStopped(); S._planNext = false; S.HeldWatch = false;
        S._hostAskedAgain = false; S._hostTries = 0; S._downsTold = AIVitals.Downs;
        S._owedPlanTrigger = null; S._planEndedSource = null; S._ansEnded = null; S._ansOwed = null; S._ansOwedHandled = false;
    }

    public static string StatusText()
    {
        var l = new List<float>(_lat); l.Sort();
        string med = l.Count > 0 ? $"{l[l.Count / 2]:F1}초" : "-";
        float now = Time.realtimeSinceStartup;
        string mode = Auto ? "켬(알아서)" : Watching ? "지켜보는 중(목표·계획·미룬 지시)" : "끔";
        string down = S._down != null ? $"(지금 쓸 수 없음: {S._down}, {math.max(0f, _retryAt - now):F0}초 뒤 다시 물음)" : "";
        string pend = _pendingTrigger != null ? $", 보류 보고 {_pendingTrigger}({math.max(0f, _retryAt - now):F0}초 뒤)" : "";
        string plan = S.Plan.Active ? $", 계획 {S.Plan.Index + 1}/{S.Plan.Count}단계" : "";
        return $"위쪽 LLM {mode}{down}(호출 {Calls}, 적용 {Applied}, 버림 {Dropped}, 실패 {Failed}, 지연 중앙값 {med}{(_req != null ? ", 답 기다리는 중" : "")}{pend}{plan})";
    }

    // ---- free time (2026-11-04 막힘-멈춤-정리, HEAD decision): a member whose host asked it to decide by itself
    // ("/ai goal 알아서") but whose upper LLM cannot give a goal now stood idle once a goal ended (ChatGPT and Grok:
    // usage 97% / 99% > 85%, the bridge refuses; four-player run 1: Grok 396 s, ChatGPT 293 s without a decision).
    // While this says why, AIBrain gives the decision model the free-time set; any goal from the upper LLM (or the
    // host) replaces it. Not when the host turned auto off (/ai stop, /ai goal 없음) or never turned it on: then the
    // host decides and the member stands. Not after a "hold" answer either (the upper LLM chose to wait).
    public const float FreeAfterSeconds = 20f;   // an answer pending longer than this (Gemini answers in 10-17 s)

    public static string FreeTimeReason(float now)
    {
        if (!Auto) return null;
        if (S._down != null) return $"위쪽 LLM을 지금 쓸 수 없음({S._down}; {math.max(0f, _retryAt - now):F0}초 뒤 다시 물음)";
        if (_req != null && now - _reqStart >= FreeAfterSeconds) return $"위쪽 LLM 답을 {now - _reqStart:F0}초째 기다리는 중";
        if (_req == null && _pendingTrigger != null && _retryAt > now + 1f) return $"위쪽 LLM에 {_retryAt - now:F0}초 뒤 다시 물음(앞 답을 쓰지 못함)";
        return null;
    }

    // Free time goes on while a goal may still come (a report on the way or due again); it ends when the upper LLM is
    // available and has nothing pending (after a "hold" answer) or auto is off.
    public static bool KeepsFreeTime => Auto && (S._down != null || _req != null || _pendingTrigger != null);

    private static void Down(float now, string why, float retryIn, string trigger)
    {
        bool first = S._down == null;
        S._down = why; _retryAt = now + retryIn;
        if (KeepForRetry(trigger ?? "retry", why)) _pendingTrigger = trigger ?? "retry";
        AIPlayerSpikeMod.Log($"PLANNER: upper LLM unavailable ({why}); auto stays on, asking again in {retryIn:F0}s");
        if (!S._downTold || first) { S._downTold = true; AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] 위쪽 LLM을 지금 쓸 수 없음({Trim(why, 80)}). {retryIn / 60f:F0}분 뒤 다시 묻고, 그동안 목표가 없으면 자유 시간으로 행동 판단기가 고릅니다.", ChatKind.Error); }
    }

    // A failed report goes again only while watching, or the host's words up to AIPlanRules.HostTries sends (검수 F2):
    // with no goal, no plan and auto off a failing CLI no longer loops. The host's line is put back as new (B2).
    private static bool KeepForRetry(string trigger, string why)
    {
        bool host = AIPlanRules.IsHostTrigger(trigger);
        if (host) { S._hostTries++; S._chatSeen = S._reqChatSeenBefore; }
        if (AIPlanRules.MayRetry(Watching, trigger, S._hostTries)) return true;
        AIPlayerSpikeMod.Log($"PLANNER: report '{trigger}' not sent again (not watching{(host ? $", host words tried {S._hostTries}" : "")}): {why}");
        if (host) AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] 호스트 말에 답하지 못했습니다({Trim(why, 80)}). 다시 말해 주세요.", ChatKind.Error);
        return false;
    }

    // A host instruction changed state (합의 2; AIHostOrders): logged (ORDER), an event for the next report whatever the
    // mode (재검수 R5) and an "order" event in the experience record (검수 작은 지적 4).
    private static void OnOrderChanged(HostOrder o, string was, string why)
    {
        float now = Time.realtimeSinceStartup;
        AIPlayerSpikeMod.Log($"ORDER {AIAgent.Cur.Name}: host instruction #{o.Id} '{Trim(o.Text, 100)}' {was} -> {o.State} (report #{_reqReport}, {now - o.At:F0}s after it was given): {Trim(why, 160)}");
        if (o.State == "미룸" || o.State == "막힘" || o.State == "포기" || o.State == "멈춤" || o.State == "밀려남") AddEvent($"호스트 지시 #{o.Id}을 {o.State}({Trim(why, 60)})", true);
        AIExperience.Order(o.Text, was, o.State, why, _reqReport, _reqTrigger);
    }

    private static void SetOrder(HostOrder o, string st, string note, string why) => AIHostOrders.Change(o, st, Trim(note, 120), why, OnOrderChanged);

    // force: the planner's own events (plan, instructions, /ai stop) go into the next report whatever the mode (재검수 R5)
    public static void AddEvent(string e, bool force = false)
    {
        if (!AIPlanRules.EventKept(Watching, force)) return;
        // same kind merged by count (v2 §8)
        for (int i = 0; i < _events.Count; i++)
        {
            string head = e.Split('(')[0];
            if (_events[i].StartsWith(head))
            {
                int n = 1;
                int x = _events[i].LastIndexOf(" ×");
                if (x > 0 && int.TryParse(_events[i].Substring(x + 2), out int k)) n = k;
                _events[i] = e + $" ×{n + 1}";
                return;
            }
        }
        _events.Add(e);
        while (_events.Count > 6) _events.RemoveAt(0);
    }

    public static void Enable(World server, Entity player)
    {
        TurnOn("/ai goal 알아서");
        AIPlayerSpikeMod.Reply($"AI 알아서 모드: {AIAgent.Cur.Name}(구독 CLI)가 다음 목표를 정하고, 행동은 OpenAI Decisions가 고릅니다. /ai stop 으로 끔.");
        Report(server, player, "auto_on");   // also under a goal now: the upper LLM looks at it (합의 2·3)
    }

    private static void TurnOn(string why)
    {
        Auto = true; _rejected = null; _events.Clear(); S._down = null; S._downTold = false; _retryAt = -1f;
        AIPlayerSpikeMod.Log($"PLANNER: auto on ({why})");
    }

    // The host spoke to this member in chat (a line with its @mention or @모두; AIPlayerSpikeMod.TryHandleMention, which
    // put the line into Chat). Reported now whatever the mode (auto stays as it is); a goal the answer sets keeps the
    // upper LLM watching while it runs.
    public static void OnHostSaid(World server, Entity player, string text)
    {
        float now = Time.realtimeSinceStartup;
        S.HostOrderSeq++; S._hostAskedAgain = false; S._hostTries = 0;
        // a new instruction does not overwrite an earlier open one (재검수 R3)
        S.Orders.Add(new HostOrder { Id = S.HostOrderSeq, Text = text, At = now, State = "받음" }, OnOrderChanged);
        AIPlayerSpikeMod.Log($"PLANNER: host said '{Trim(text, 160)}' (instruction #{S.HostOrderSeq}, open {S.Orders.OpenAll().Count}){(_req != null ? $" (report #{_reqReport} on the way; this one waits)" : "")}");
        // the upper LLM cannot answer for now: the host hears it (검수 F7); the words wait for the retry time
        if (S._down != null && now < _retryAt)
            AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] 지금 위쪽 LLM을 쓸 수 없음({Trim(S._down, 80)}). 약 {math.max(1f, math.ceil((_retryAt - now) / 60f)):F0}분 뒤 이 말에 답합니다.", ChatKind.Error);
        Report(server, player, "host_said");
    }

    // A member's say goes into the shared talk (and the chat window).
    private static void Say(string say)
    {
        string t = Trim(say, 80);
        Chat.Add(Time.realtimeSinceStartup, AIAgent.Cur.Id, AIAgent.Cur.Name, null, false, t);
        AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] {t}", ChatKind.Say);
    }

    // /ai stop, /ai goal 없음 (재검수 R1): nothing is watched after it, the owed plan call goes, and the open host
    // instructions close as 멈춤 with an event, so the next mention's answer does not set the stopped work again.
    public static void Disable(string why)
    {
        S._down = null; S._downTold = false;
        _pendingTrigger = null;   // a waiting report (a mention with auto off too) goes as well
        var st = S;
        string ev = AIPlanStop.Apply(st.Plan, st.Orders, why, OnOrderChanged, ref st._owedPlanTrigger, ref st._planEndedSource, ref st._planNext, ref st.HeldWatch);
        if (ev != null) { AddEvent(ev, true); AIPlayerSpikeMod.Log($"PLANNER: {AIAgent.Cur.Name} stopped by the host ({why}): {ev}"); }
        if (!Auto && _req == null) return;
        Auto = false;
        if (_req != null) { try { _req.Abort(); _req.Dispose(); } catch { } _req = null; Dropped++; AIPlayerSpikeMod.Log($"PLANNER: request dropped ({why})"); }
        _pendingTrigger = null;
        AIPlayerSpikeMod.Log($"PLANNER: auto off ({why})");
    }

    // A host /ai goal (AIGoals.SetGoal source "host"): it is the host's instruction now; with the upper LLM on it is
    // reported (host_goal) so the LLM answers and keeps looking at it (it may put it off, 합의 2).
    public static void OnHostGoal(World server, Entity player, string text)
    {
        if (_req != null) AIPlayerSpikeMod.Log($"PLANNER: host set a goal ('{text}') while report #{_reqReport} is in flight; its answer will be stale");
        S.HostOrderSeq++; S._hostAskedAgain = false; S._hostTries = 0; S.HeldWatch = false;
        // the instructions the plan put down was following are put off (still open in the report); the host goal is the
        // latest now
        if (S.Plan.Active) S.Orders.PutOff(S.Plan.Source, null, "호스트가 /ai goal로 다른 목표를 걸어 미뤄짐", "host /ai goal", OnOrderChanged);
        foreach (var o in S.Orders.Carried("host")) SetOrder(o, "멈춤", "호스트가 새 /ai goal로 바꿈", "host /ai goal");   // the host goal it replaces
        S.Orders.Add(new HostOrder { Id = S.HostOrderSeq, Text = "/ai goal " + text, At = Time.realtimeSinceStartup, State = "따름", Note = "호스트가 직접 목표로 걸었음", Source = "host" }, OnOrderChanged);
        if (S.Plan.Active) { S.Plan.Stop("호스트가 /ai goal로 목표를 걸었음"); S._planNext = false; }
        AddEvent($"호스트가 목표를 줌({text})");
        Report(server, player, "host_goal");   // a goal is running now: watched whatever the mode
    }

    public static void OnGoalEnded(World server, Entity player, string trigger, string goalText, string why, string source)
    {
        bool done = AIPlanRules.StepDone(trigger);
        // a plan (합의 3e): a step that ended well starts the next one on the next tick without a call
        if (S.Plan.Owns(source))
        {
            if (done && S.Plan.HasNext)
            {
                AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: step {S.Plan.Index + 1}/{S.Plan.Count} '{S.Plan.Current?.Text}' ended ({trigger}); next '{S.Plan.Rest()[0].Text}' without a call");
                AddEvent($"계획 {S.Plan.Index + 1}/{S.Plan.Count}단계 끝: {goalText}", true);
                S._planNext = true;
                return;
            }
            if (done)
            {
                if (S.Plan.Count > 1) trigger = "plan_done";
                // a report sent while this plan ran is on the way (실게임3 M1): its answer is not stale for this end, and the
                // plan_done / goal_done call is owed instead of sent — the answer decides (AIPlanRules.OwedCallGoes)
                if (_req != null && S._reqPlanSource != null && S._reqPlanSource == S.Plan.Source) { S._planEndedSource = S.Plan.Source; S._owedPlanTrigger = trigger; }
                AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: plan of {S.Plan.Count} steps done{(S._owedPlanTrigger != null ? $" (report #{_reqReport} about it on the way: {trigger} owed, not sent)" : "")}");
                if (S.Plan.Count > 1) AddEvent($"계획 {S.Plan.Count}단계를 모두 끝냄(마지막: {goalText})", true);
                else if (S._owedPlanTrigger != null) AddEvent($"목표 끝: {goalText}", true);
                S.Plan.Clear();
            }
            else
            {
                AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: step {S.Plan.Index + 1}/{S.Plan.Count} '{S.Plan.Current?.Text}' ended {trigger}; the rest stops ({S.Plan.Rest().Count} steps)");
                AddEvent($"계획 {S.Plan.Index + 1}/{S.Plan.Count}단계에서 멈춤: {goalText}", true);
                S.Plan.Stop($"{goalText}: {why}");
            }
        }
        // the host's instruction carried by this goal (a host /ai goal, or the LLM goal or plan that follows it)
        S.Orders.GoalEnded(source, done, Trim($"{goalText}: {why}", 120), OnOrderChanged);
        if (!Auto && string.IsNullOrEmpty(source)) return;   // a real goal ended (the goal is already cleared here): reported
        _lastGoalText = goalText; _lastWhy = why; _lastSource = source;
        if (S._owedPlanTrigger != null && trigger == S._owedPlanTrigger && _req != null) return;   // owed (M1): the answer on the way decides
        Report(server, player, trigger);
    }

    // The next step of the plan (from Tick, the frame after the step before ended). It is checked like a goal of an
    // answer now; one that cannot start stops the plan and asks at once (plan_step_failed). The host instructions the plan
    // carried are stuck (막힘, still open: 실게임4 K2), not left followed by a plan that is gone.
    private static void StartNextStep(World server, Entity player, float now)
    {
        if (!S.Plan.Active || AIBrain.Goal.IsReal) return;   // stopped meanwhile, or another goal came
        var st = S.Plan.Advance();
        if (st == null) return;
        string bad = CheckGoal(server, player, st.Do, st.Item, st.Count, st.Where, st.Keep, st.Slot, st.Mode, st.Second);
        if (bad != null)
        {
            AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: step {S.Plan.Index + 1}/{S.Plan.Count} '{st.Text}' cannot start: {bad}");
            S.Orders.GoalEnded(S.Plan.Source, false, Trim($"{st.Text}: 시작할 수 없음 — {bad}", 120), OnOrderChanged);
            S.Plan.Stop($"{st.Text}: 시작할 수 없음 — {bad}");
            AddEvent($"계획 {S.Plan.Index + 1}단계를 시작할 수 없어 멈춤: {st.Text}", true);
            Report(server, player, "plan_step_failed");
            return;
        }
        AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: step {S.Plan.Index + 1}/{S.Plan.Count} '{st.Text}' starts");
        SetGoalFrom(server, player, st.Do, st.Item, st.Count, S.Plan.Source, st.Where, st.Keep, st.Slot, st.Mode, st.Second);
    }

    // Back up after a down: report (the goal keeps running meanwhile). A host goal is questioned too now: the upper LLM
    // may put the host's instruction off after dying on it (합의 2). One down is told once (검수 F4): not again when its
    // fight already called (fight_end).
    public static void OnBackUp(World server, Entity player)
    {
        if (!Watching) return;
        if (AIPlanRules.DownTold(AIVitals.Downs, S._downsTold)) { AIPlayerSpikeMod.Log($"PLANNER: back_up not reported (down #{AIVitals.Downs} already told by fight_end)"); return; }
        S._downsTold = AIVitals.Downs;
        var g = AIBrain.Goal;
        _lastGoalText = !g.IsReal ? _lastGoalText : g.Text;
        _lastWhy = !g.IsReal ? _lastWhy : "이 목표를 하다가 쓰러져 코어에서 다시 일어남(목표는 아직 진행 중)";
        _lastSource = !g.IsReal ? _lastSource : g.source;
        Report(server, player, "back_up");
    }

    // A fight AIExperience recorded ended (no enemy within 7 tiles on screen for 5 s). It is reported once when it ended
    // below half health or with a down in it (AIFightLog.ShouldReport); other fights are not (they reach the next
    // report as recent_fights). The fight itself is the action model's; the upper LLM decides what to do after it.
    public static void OnFightEnded(World server, Entity player, FightRecord f)
    {
        bool downTold = f.Downs > 0 && AIPlanRules.DownTold(AIVitals.Downs, S._downsTold);
        if (!Watching || !AIFightLog.ShouldReport(f, downTold)) { if (Watching && downTold) AIPlayerSpikeMod.Log("PLANNER: fight_end not reported (its down was told by back_up)"); return; }
        if (f.Downs > 0) S._downsTold = AIVitals.Downs;
        S.FightCalls++;
        AIPlayerSpikeMod.Log($"PLANNER: fight_end trigger #{S.FightCalls} (downs {f.Downs}, health {f.EndHp}/{f.MaxHp}, damage {f.Damage}){(_req != null ? $" (report #{_reqReport} on the way; this one waits)" : "")}");
        Report(server, player, "fight_end");
    }

    // ------------------------------------------------------------------ report

    // The report JSON is built in AIPlanReport (refactor S4); the planner decides when to send it.
    private static string ReportJson(World server, Entity player, string trigger, out int reportId) => AIPlanReport.ReportJson(server, player, trigger, out reportId);

    public static bool ReplanPending => _req != null || _pendingTrigger != null;
    public static void RequestReplan(World server, Entity player, string reason)
    {
        AddEvent(reason, true);
        if (_req != null) { if (_pendingTrigger == null) _pendingTrigger = "action_replan"; return; }
        if (_pendingTrigger != null) return;
        Report(server, player, "action_replan");
    }

    private static void Report(World server, Entity player, string trigger)
    {
        if (_req != null) { _pendingTrigger = trigger; return; }
        // unavailable for now (limit, bridge down …): keep the newest trigger for the retry time instead of asking again
        if (S._down != null && Time.realtimeSinceStartup < _retryAt) { _pendingTrigger = trigger; AIPlayerSpikeMod.Log($"PLANNER: report '{trigger}' kept for the retry in {_retryAt - Time.realtimeSinceStartup:F0}s ({S._down})"); return; }
        // a report still waiting (a retry time not reached yet) goes with this one, under the more urgent trigger (검수
        // 작은 지적 7: it was sent once more afterwards)
        if (S.Inbox.HasPending)
        {
            string w = S.Inbox.Pending.Kind;
            if (PlanInbox.Priority(w) > PlanInbox.Priority(trigger)) trigger = w;
            S.Inbox.Clear();
        }
        // timed: the report reads the bag, recipes of every station in view, remembered stations and the experience
        // memory in one frame (a 258 ms mod frame of Gemini in the four-player run 2 had no decision preparation behind
        // it; its report #11 was built in that window — measured from now on)
        var rsw = System.Diagnostics.Stopwatch.StartNew();
        string report = ReportJson(server, player, trigger, out int rid);
        float reportMs = (float)rsw.Elapsed.TotalMilliseconds;
        string body = $"{{\"member\":\"{AIAgent.Cur.Id}\",\"world\":\"{AIPlayerSpikeMod.WorldId}\",\"report_id\":{rid},\"goal_rev\":{AIBrain.GoalRevision},\"report\":{report}}}";
        _reqReport = rid; _reqRev = AIBrain.GoalRevision; _reqTrigger = trigger; _reqStart = Time.realtimeSinceStartup; _reqDowns = AIVitals.Downs;
        _req = new UnityWebRequest(BridgeUrl + "/v1/plan", "POST");
        _req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        _req.downloadHandler = new DownloadHandlerBuffer();
        _req.SetRequestHeader("Content-Type", "application/json");
        _req.timeout = 150;   // the bridge answers within 120 s (memory searches included); 150 < the 180 s stale limit
        _req.SendWebRequest();
        Calls++;
        _events.Clear();
        S._lastPlanAt = Time.realtimeSinceStartup;
        S._reqChatSeenBefore = S._chatSeen;
        S._chatSeen = Chat.LastSeq;
        S._reqOrderSeq = S.HostOrderSeq;
        S._reqPlanSource = S.Plan.Active ? S.Plan.Source : null;
        AIPlayerSpikeMod.Log($"PLANNER: report #{rid} sent trigger={trigger} rev={_reqRev} reportChars={report.Length} buildMs={reportMs:F1}: {report}");
    }

    // ------------------------------------------------------------------ per tick

    public static void Tick(World server, Entity player, float now)
    {
        try { AIExperience.Tick(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("exp: tick failed", e); }
        if (_health != null && _health.isDone)
        {
            AIPlayerSpikeMod.Log($"PLANNER: health GET {BridgeUrl}/v1/health -> result={_health.result} code={_health.responseCode} error='{_health.error}' body={(_health.downloadHandler?.text ?? "").Substring(0, Math.Min(400, (_health.downloadHandler?.text ?? "").Length))}");
            AIPlayerSpikeMod.Reply($"[시험용] 브리지 health: {_health.result} {_health.responseCode} {_health.error}");
            _health.Dispose(); _health = null;
        }
        // a report waiting or on the way is handled whatever the mode (a mention with auto off, an answer that arrives
        // after the goal ended); new reports only while watching
        if (S._planNext) { S._planNext = false; StartNextStep(server, player, now); }
        // a plan whose goal was cleared outside it (three failed decisions, the cost cap, no API key): stopped like a failed
        // step (재검수 작은 지적 3)
        if (AIPlanRules.PlanOrphaned(S.Plan.Active, S._planNext, AIBrain.Goal.IsReal, AIBrain.Goal.source, S.Plan.Source))
        {
            string at = S.Plan.Current?.Text ?? "";
            AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: step {S.Plan.Index + 1}/{S.Plan.Count} '{at}' lost its goal outside the plan; the rest stops");
            S.Orders.GoalEnded(S.Plan.Source, false, Trim($"{at}: 목표가 계획 밖에서 지워짐", 120), OnOrderChanged);
            AddEvent($"계획 {S.Plan.Index + 1}/{S.Plan.Count}단계에서 멈춤: {at}(목표가 지워짐)", true);
            S.Plan.Stop($"{at}: 목표가 계획 밖에서 지워짐");
            Report(server, player, "plan_step_failed");
        }
        bool watching = Watching;
        if (_req == null)
        {
            if (_pendingTrigger != null && now >= _retryAt) { string t = _pendingTrigger; _pendingTrigger = null; Report(server, player, t); }
            // the periodic look (합의 3) while watching; it replaces the old 2-minute re-ask after a hold
            else if (watching && PlanInbox.PeriodicDue(now, S._lastPlanAt, Period, _pendingTrigger != null, S._down != null)) Report(server, player, "periodic");
            return;
        }
        if (!_req.isDone)
        {
            if (now - _reqStart > 10f && now - _reqStart < 10.05f + Time.deltaTime) AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] (다음 할 일을 생각하는 중…)", ChatKind.Status);
            return;
        }
        var req = _req; _req = null;
        S._lastPlanAt = now;   // the next periodic look counts from the answer
        // the owed plan call (M1) belongs to this answer now; whatever path below ends it, OwedAfter settles it
        S._ansEnded = S._planEndedSource; S._ansOwed = S._owedPlanTrigger; S._ansOwedHandled = false;
        S._planEndedSource = null; S._owedPlanTrigger = null;
        try { Answer(server, player, now, req); }
        finally { OwedAfter(now); S._ansEnded = null; S._ansOwed = null; S._ansOwedHandled = false; }
    }

    // The answer (or failure) of the report that was on the way.
    private static void Answer(World server, Entity player, float now, UnityWebRequest req)
    {
        float ms = (now - _reqStart);
        string text = req.downloadHandler != null ? req.downloadHandler.text : "";
        var result = req.result; long code = req.responseCode; string err = req.error;
        req.Dispose();
        bool timedOut = result == UnityWebRequest.Result.ConnectionError && code == 0 && (err ?? "").IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        if (result == UnityWebRequest.Result.ConnectionError && code == 0 && !timedOut)
        {
            // bridge down: auto stays on and asks again in 2 minutes (was: auto off for good); free time meanwhile
            Failed++;
            AIPlayerSpikeMod.Log($"PLANNER: bridge not reachable ({err}) after {ms:F1}s");
            Down(now, "브리지에 닿지 않음(꺼져 있음?)", 120f, _reqTrigger);
            return;
        }
        // the bridge's reply, read once (AIPlanProtocol: top-level keys, JSON escapes undone; the *Raw values are what
        // the old hand reader gave, e.g. "null" for a JSON null)
        var reply = AIPlanProtocol.Read(text);
        string error = timedOut ? "timeout" : reply.ErrorRaw;
        AIPlayerSpikeMod.Log($"PLANNER: report #{_reqReport} answer after {ms:F1}s code={code} error={error ?? "none"} body={text.Substring(0, Math.Min(900, text.Length))}");
        if (code == 409) { if (AIPlanRules.IsHostTrigger(_reqTrigger)) S._chatSeen = S._reqChatSeenBefore; _pendingTrigger = _reqTrigger; _retryAt = now + 5f; return; }
        if (!string.IsNullOrEmpty(error) && error != "null")
        {
            Failed++;
            AIExperience.PlanResult("failed", _reqReport, _reqTrigger, "", "", "", "", error + ": " + reply.DetailRaw, ms, reply.RecallSummaryRaw);
            if (error == "limit" || error == "auth" || error == "login")
            {
                // usage over the bridge's limit (it re-reads usage every 10 min and says retry_after_s), or login/auth
                // that needs a person: auto stays on and asks again later (was: auto off for good)
                float after = error == "limit" && float.TryParse(reply.RetryAfterRaw, out float ra) && ra > 0f ? math.clamp(ra, 120f, 3600f) : 600f;
                Down(now, $"{error}: {reply.DetailRaw}", after, _reqTrigger);
                return;
            }
            if (error == "parse" && !_askedAgain)
            {
                _askedAgain = true; _rejected = "답 형식 오류: " + reply.DetailRaw; AskAgainNow(now); return;
            }
            _tries++;
            if (_tries >= 3) { _tries = 0; Down(now, $"세 번 실패({error})", 300f, _reqTrigger); return; }
            if (KeepForRetry(_reqTrigger, error)) { _pendingTrigger = _reqTrigger; _retryAt = now + (30f * (1 << (_tries - 1))); }
            return;
        }
        _lat.Add(ms);
        _tries = 0;
        if (S._down != null) { AIPlayerSpikeMod.Log($"PLANNER: upper LLM answers again (was: {S._down})"); S._down = null; S._downTold = false; }
        Apply(server, player, now, reply, ms);
    }

    private static void Apply(World server, Entity player, float now, PlanReply reply, float secs)
    {
        // the answer object inside the bridge's reply (AIPlanProtocol; JsonUtility leaves nested custom classes empty in
        // mod-compiled code, U.json-fallback)
        PlanAnswerJ a = null;
        var an = reply.Answer;
        if (an != null)
        {
            a = new PlanAnswerJ { decision = an.Decision, say = AIPlanRules.Text(an.Say), why = AIPlanRules.Text(an.Why), order = AIPlanRules.Text(an.Order), problem = AIPlanRules.Text(an.Problem), orders = an.Orders };
            if (an.HasFor) a.@for = new PlanForJ { report = an.ForReport, goal_rev = an.ForGoalRev };
            if (an.HasGoal) a.goal = new PlanGoalJ { @do = an.GoalDo, item = an.GoalItem, count = an.GoalCount, @where = an.GoalWhere, keep = an.GoalKeep, slot = an.GoalSlot, mode = an.GoalMode, second = an.GoalSecond };
        }
        // the steps of a set answer: "plan" (합의 3e) or the one "goal"
        List<PlanStep> steps = null;
        if (a != null && a.decision == "set")
        {
            if (an.Plan != null && an.Plan.Count > 0) steps = new List<PlanStep>(an.Plan);
            else if (a.goal != null) steps = new List<PlanStep> { new PlanStep(a.goal.@do, a.goal.item, a.goal.count, a.goal.@where, a.goal.keep, a.goal.slot, a.goal.mode, a.goal.second) };
        }
        string reject = CheckReason(server, player, a, steps, secs, out string staleWhy, out int cut, out string cutWhy);
        // a stale answer (검수 B2): the host's words are never lost — see AIPlanRules.OnStale
        var act = reject == null ? AIPlanRules.OnStale(staleWhy != null, _reqTrigger, a?.decision, S._hostAskedAgain) : StaleAction.Apply;
        if (reject != null || act == StaleAction.Drop || act == StaleAction.AskAgain)
        {
            string why = reject ?? staleWhy;
            bool stale = reject == null;
            Dropped++;
            AIPlayerSpikeMod.Log($"PLANNER: report #{_reqReport} answer DROPPED: {why}{(act == StaleAction.AskAgain ? " (the host's words: asked again now)" : "")}");
            AIExperience.PlanResult("dropped", _reqReport, _reqTrigger, a?.decision ?? "", steps == null || steps.Count == 0 ? "" : steps[0].Text, a?.why, a?.say, why, secs, reply.RecallSummaryRaw);
            // refactor S2b: the kind decides; with the test switch, the sentence is compared (REJECT-MISMATCH)
            if (AITestSwitches.Test)
            {
                string mm = AIOutcomes.Mismatch("reject-stale", AIOutcomes.StaleRejectText(why), stale, $"reject='{Trim(why, 120)}'");
                if (mm != null) AIPlayerSpikeMod.Log("REJECT-MISMATCH " + mm);
            }
            if (act == StaleAction.AskAgain) { S._hostAskedAgain = true; S._chatSeen = S._reqChatSeenBefore; _pendingTrigger = _reqTrigger; _retryAt = now; return; }
            if (stale) { if (Auto && !AIBrain.Goal.IsReal && _pendingTrigger == null) { _pendingTrigger = "retry"; _retryAt = now + 30f; } return; }
            if (!_askedAgain) { _askedAgain = true; _rejected = why; AskAgainNow(now); }
            return;
        }
        bool askedBefore = _askedAgain;   // a re-ask already went since the last answer that was taken (K1: once)
        _askedAgain = false; _rejected = null;
        if (AIPlanRules.IsHostTrigger(_reqTrigger)) { S._hostAskedAgain = false; S._hostTries = 0; }
        S.Plan.ForgetStopped();   // a stopped plan was shown in this answer's report
        if (!string.IsNullOrEmpty(a.say)) Say(a.say);
        AIPlayerSpikeMod.Log($"PLANNER: report #{_reqReport} APPLY trigger={_reqTrigger} decision={a.decision}{(steps != null ? $" plan=[{string.Join(" → ", steps.ConvertAll(x => x.Text))}]" : "")} order={a.order ?? "-"}{(act == StaleAction.TalkOnly ? " (stale twice: say and order only)" : staleWhy != null ? $" (stale but kept: {staleWhy})" : "")} why='{Trim(a.why, 120)}' after {secs:F1}s");
        // 실게임4 K1: a set answer whose step cannot be taken is not thrown away. No step can be taken (cut 0): the say and
        // order are kept, the goal is left as it is (a follow is not taken: nothing carries it). Some can: those are set.
        bool refusedAll = act != StaleAction.TalkOnly && a.decision == "set" && cut == 0;
        string cutNote = a.decision == "set" && cut >= 0 ? AIPlanRules.RefusedNote(steps, cut, cutWhy) : null;
        if (cutNote != null) AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: report #{_reqReport} step {cut + 1}/{steps.Count} '{steps[cut].Text}' not taken: {cutWhy}{(cut > 0 ? $"; steps 1-{cut} are set" : "; the goal stays as it is")}");
        var followed = OnOrder(a, act == StaleAction.TalkOnly || refusedAll);
        // problem report (user decision 6): the answer's "problem" field is the one path to the record; the host hears it
        // through say. Empty or the string "null": none.
        if (!string.IsNullOrWhiteSpace(a.problem))
        {
            AIPlayerSpikeMod.Log($"PROBLEM {AIAgent.Cur.Name} (report #{_reqReport}, trigger {_reqTrigger}): {Trim(a.problem, 200)} | say: {Trim(a.say, 100)}");
            AIExperience.Problem(a.problem, a.say, _reqReport, _reqTrigger);
        }
        S._lastDecision = $"{a.decision}{(steps != null ? " " + string.Join(" → ", steps.ConvertAll(x => x.Text)) : "")}{(string.IsNullOrEmpty(a.order) ? "" : $", 호스트 지시 {a.order}")} — {Trim(a.why, 120)}";
        S._lastDecisionAt = now;
        if (act == StaleAction.TalkOnly)
        {
            // asked again once already and still stale: the say and order are kept, the goal is left as it is now
            OwedSettle(a.decision, true, now);
            Applied++; S._hostAskedAgain = false;
            AIExperience.PlanResult("accepted", _reqReport, _reqTrigger, "talk-only", "", a.why, a.say, staleWhy, secs, reply.RecallSummaryRaw);
            return;
        }
        if (refusedAll)
        {
            // 실게임4 K1: no step can be taken. The goal / plan goes on as it is, the why goes in the next report
            // (last_answer_rejected). Asked again at once only when nothing runs, and once (AIPlanRules.RefusedAskAgain).
            OwedSettle("keep", false, now);
            Applied++;
            _rejected = cutNote;
            _askedAgain = true;   // until an answer is taken, no second re-ask for this
            AIExperience.PlanResult("accepted", _reqReport, _reqTrigger, "set-refused", string.Join(" → ", steps.ConvertAll(x => x.Text)), a.why, a.say, cutNote, secs, reply.RecallSummaryRaw);
            if (AIPlanRules.RefusedAskAgain(cut, AIBrain.Goal.IsReal, S.Plan.Active, S._planNext, askedBefore))
            {
                if (_pendingTrigger == null) { _pendingTrigger = "retry"; _retryAt = now; }
                AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: nothing runs and no step was taken: asked again now ({_pendingTrigger})");
            }
            return;
        }
        if (a.decision == "keep" || a.decision == "ask")
        {
            // keep: go on with the current goal / plan (or stay as you are); ask: a question in say, the goal goes on
            OwedSettle(a.decision, false, now);
            Applied++;
            AIExperience.PlanResult("accepted", _reqReport, _reqTrigger, a.decision, "", a.why, a.say, "", secs, reply.RecallSummaryRaw);
            return;
        }
        if (a.decision == "hold")
        {
            // hold (검수 B1): put the current goal / plan down and stand. The host's instruction is not wiped: it stays as
            // put off (미룸), and the member stays watched (2-minute look and triggers) so it can come back to it.
            OwedSettle(a.decision, false, now);
            Applied++;
            AIExperience.PlanResult("accepted", _reqReport, _reqTrigger, a.decision, "", a.why, a.say, "", secs, reply.RecallSummaryRaw);
            string planSrc = S.Plan.Active ? S.Plan.Source : null, goalSrc = AIBrain.Goal.IsReal ? AIBrain.Goal.source : null;
            if (S.Plan.Active) S.Plan.Stop($"hold: {Trim(a.why, 80)}");
            if (AIBrain.Goal.kind != GoalKind.None)
            {
                AIBrain.ClearGoal(new GoalEnd($"upper LLM hold: {Trim(a.why, 80)}", "cancelled"));
            }
            AIBrain.CancelPendingChoices("upper LLM hold");
            AIBody.Stop(server, player, now, "위쪽 LLM이 멈춤", true);
            if (AIBrain._sd) { AIBrain._sd = false; AIBrain.Revision++; AIBrain.AbortRequest("AI stopped self-defence"); }
            var lo = S.Orders.Latest;
            bool loFollows = lo != null && AIPlanRules.OrderApplies(S._reqOrderSeq, lo.Id) && !AIPlanRules.HoldPutsOff(a.order);
            if (lo != null && AIPlanRules.OrderApplies(S._reqOrderSeq, lo.Id) && AIPlanRules.HoldPutsOff(a.order) && (lo.State == "받음" || lo.State == "따름"))
                SetOrder(lo, "미룸", string.IsNullOrEmpty(a.say) ? a.why : a.say, "hold: " + a.why);
            // the other followed instructions the goal / plan put down carried are put off too (they stay open to come back to)
            string pnote = Trim("hold로 내려놓음: " + (string.IsNullOrEmpty(a.say) ? a.why : a.say), 120);
            S.Orders.PutOff(planSrc, loFollows ? lo : null, pnote, "hold", OnOrderChanged);
            S.Orders.PutOff(goalSrc, loFollows ? lo : null, pnote, "hold", OnOrderChanged);
            S.HeldWatch = true;   // every hold, on purpose (재검수 R4, user decision 3d): asked again every 2 minutes until a set or /ai stop
            return;
        }
        // set: a goal, or a plan of steps (합의 3e). Step 1 starts now; the others start one by one without a call.
        // A step that cannot be taken (실게임4 K1): the steps before it are set, the rest and why go in the next report.
        if (cut > 0) { steps.RemoveRange(cut, steps.Count - cut); _rejected = cutNote; AddEvent($"계획을 {cut}단계까지만 받음(그 뒤 단계는 last_answer_rejected)", true); }
        int max = math.max(1, AIBrain.Settings().upperPlanMaxSteps);
        if (steps.Count > max) { AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: answer had {steps.Count} steps, the first {max} are taken"); AddEvent($"계획이 {steps.Count}단계라 앞 {max}단계만 받음", true); steps.RemoveRange(max, steps.Count - max); }
        string src = $"llm:{AIAgent.Cur.Id}#{_reqReport}";
        OwedSettle(a.decision, false, now);
        S.HeldWatch = false;
        // the instructions this answer follows, and those the replaced plan was carrying, are carried by the new plan
        var carry = new List<HostOrder>(followed);
        if (S.Plan.Active) foreach (var o in S.Orders.Carried(S.Plan.Source)) if (!carry.Contains(o)) carry.Add(o);
        S.Plan.Set(src, steps);
        foreach (var o in carry) o.Source = src;
        SetGoalFrom(server, player, steps[0].Do, steps[0].Item, steps[0].Count, src, steps[0].Where, steps[0].Keep, steps[0].Slot, steps[0].Mode, steps[0].Second);
        Applied++;
        AIExperience.PlanResult("accepted", _reqReport, _reqTrigger, a.decision, string.Join(" → ", steps.ConvertAll(x => x.Text)), a.why, a.say, cutNote ?? "", secs, reply.RecallSummaryRaw);
        AIPlayerSpikeMod.Reply($"AI 목표(위쪽 LLM): {AIBrain.Goal.Text}{(steps.Count > 1 ? $" (계획 1/{steps.Count})" : "")}", ChatKind.Status);
    }

    // The owed plan call (M1) after an applied answer: set / hold replace it; keep / ask / talk-only send it once now
    // when nothing runs (AIPlanRules.OwedCallGoes).
    private static void OwedSettle(string decision, bool talkOnly, float now)
    {
        if (S._ansOwed == null) return;
        S._ansOwedHandled = true;
        bool goes = AIPlanRules.OwedCallGoes(decision, talkOnly, AIBrain.Goal.IsReal || S._planNext, S.Plan.Active);
        AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: owed {S._ansOwed} after report #{_reqReport} ({(talkOnly ? "talk-only" : decision)}): {(goes ? "sent now" : "not needed")}");
        if (!goes) return;
        if (_pendingTrigger == null) { _pendingTrigger = S._ansOwed; _retryAt = now; }
        else OwedRides(S._ansOwed);
    }

    // The owed plan call when the answer was not applied (dropped, failed, asked again): it goes as the next report, or
    // rides on the one already waiting (that answer settles it).
    private static void OwedAfter(float now)
    {
        if (S._ansOwed == null || S._ansOwedHandled) return;
        if (_pendingTrigger == null) { _pendingTrigger = S._ansOwed; if (_retryAt < now) _retryAt = now; AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: owed {S._ansOwed} goes next (report #{_reqReport} not applied)"); }
        else if (_pendingTrigger != S._ansOwed) OwedRides(S._ansOwed);
    }

    // The owed call meets a report already waiting (재검수 작은 지적 2): that report is built when it is sent, after the plan
    // ended, so it already shows the end; it goes under the more urgent of the two triggers and nothing stays owed.
    private static void OwedRides(string owed)
    {
        string w = _pendingTrigger;
        if (PlanInbox.Priority(owed) > PlanInbox.Priority(w)) _pendingTrigger = owed;
        AIPlayerSpikeMod.Log($"PLAN {AIAgent.Cur.Name}: owed {owed} goes with the waiting report ({w} -> {_pendingTrigger})");
    }

    // A re-ask after a rejected answer goes now. The host's words keep their trigger and come back as new (검수 B2);
    // other reports go as "retry".
    private static void AskAgainNow(float now)
    {
        if (AIPlanRules.IsHostTrigger(_reqTrigger)) { S._chatSeen = S._reqChatSeenBefore; _pendingTrigger = _reqTrigger; }
        else _pendingTrigger = "retry";
        _retryAt = now;
    }

    private static void SetGoalFrom(World server, Entity player, string d, string item, int count, string source, string where = null, int keep = -1, int slot = -1, string mode = null, string second = null)
    {
        if (!AIGoalKinds.TryWire(d, out GoalKind kind)) kind = GoalKind.Follow;
        ObjectID id = ObjectID.None; string nm = "";
        if (kind == GoalKind.Gather || kind == GoalKind.Craft || kind == GoalKind.Place || kind == GoalKind.Farm || kind == GoalKind.Cook || AIGoalKinds.IsInventory(kind) || (kind == GoalKind.Store && !string.IsNullOrEmpty(item) && item != "null")) AIBrain.TryItemPublic(item ?? "", out id, out nm);
        count = AILifeGoalForm.CountFor(d, AIInventoryGoalForm.CountFor(kind, count));
        AIGoals.SetGoal(server, player, kind, id, nm, kind == GoalKind.Place && count < 1 ? 1 : count, source, where, keep, slot, mode, second);
    }

    // What the answer does with the host's instructions (합의 2; AIHostOrders.ApplyAnswer): "order" for the latest, if the
    // answer's report carried it (검수 F1), "orders" for earlier open ones (재검수 R3). Fields only (재검수 R2): no order, no
    // change. Returns the instructions this answer follows (a set answer's source carries them).
    private static List<HostOrder> OnOrder(PlanAnswerJ a, bool talkOnly)
    {
        return S.Orders.ApplyAnswer(a.order, a.orders, _reqTrigger, S._reqOrderSeq, talkOnly,
            Trim(string.IsNullOrEmpty(a.say) ? a.why : a.say, 120), a.why, OnOrderChanged,
            m => AIPlayerSpikeMod.Log($"ORDER {AIAgent.Cur.Name} (report #{_reqReport}): {m}"));
    }

    // null when the answer can be applied. staleWhy (not a rejection by itself): the answer came too late or for another
    // state; AIPlanRules.OnStale decides what that means for this trigger. cut / cutWhy (실게임4 K1, not a rejection
    // either): the first step of a set answer that cannot be taken now and why, -1 when all can; the steps before it are
    // set, the say and order are applied anyway (AIPlanRules.PlanCut).
    private static string CheckReason(World server, Entity player, PlanAnswerJ a, List<PlanStep> steps, float secs, out string staleWhy, out int cut, out string cutWhy)
    {
        staleWhy = null; cut = -1; cutWhy = null;
        if (a == null || string.IsNullOrEmpty(a.decision)) return "답 JSON이 없음";
        if (a.@for == null || a.@for.report != _reqReport || a.@for.goal_rev != _reqRev) return $"답의 for가 보낸 보고와 다름(report {a.@for?.report}/{_reqReport}, goal_rev {a.@for?.goal_rev}/{_reqRev})";
        // the goal revision moves with every plan step too; steps of the same plan going on, or that plan ending well while
        // nothing else came, are not a new state (실게임2 N1, 실게임3 M1)
        if (AIPlanRules.RevStale(_reqRev, AIBrain.GoalRevision, S._reqPlanSource, AIPlanRules.PlanNow(S.Plan.Active, S.Plan.Source, S._ansEnded, AIBrain.Goal.IsReal))) staleWhy = $"오래된 답: 목표 수정 번호가 바뀜({_reqRev}→{AIBrain.GoalRevision})";
        else if (AIVitals.Downs != _reqDowns) staleWhy = "오래된 답: 기다리는 동안 쓰러짐";
        // A slow answer still applies when the goal, down state and current conditions agree.
        if (a.decision == "hold" || a.decision == "ask" || a.decision == "keep") return null;
        if (a.decision != "set") return $"모르는 decision '{a.decision}'";
        if (steps == null || steps.Count == 0) return "set에 goal이나 plan이 없음";
        // step 1 starts now: checked against the world now; later steps only for their form (the world will differ)
        int max = math.max(1, AIBrain.Settings().upperPlanMaxSteps);
        var errs = new List<string> { CheckGoal(server, player, steps[0].Do, steps[0].Item, steps[0].Count, steps[0].Where, steps[0].Keep, steps[0].Slot, steps[0].Mode, steps[0].Second) };
        for (int i = 1; i < steps.Count && i < max; i++) errs.Add(StepForm(steps[i]));
        cut = AIPlanRules.PlanCut(errs);
        if (cut >= 0) cutWhy = errs[cut];
        return null;
    }

    // A later plan step: a known kind, a known item where one is needed, a count in range.
    private static string StepForm(PlanStep s)
    {
        string d = s.Do;
        string lifeForm = AILifeGoalForm.Check(s);
        if (lifeForm != null) return lifeForm;
        if (d == "farm" || d == "cook")
        {
            if (AILifeGoalForm.Text(s.Item) != null && !AIBrain.TryItemPublic(s.Item, out _, out _)) return $"모르는 품목 '{s.Item}'";
            if (AILifeGoalForm.Text(s.Second) != null && !AIBrain.TryItemPublic(s.Second, out _, out _)) return $"모르는 품목 '{s.Second}'";
            return null;
        }
        if (AIGoalKinds.TryWire(d, out var ik) && (AIGoalKinds.IsInventory(ik) || ik == GoalKind.Store))
        {
            string form = AIInventoryGoalForm.Check(s);
            if (form != null) return form;
            return string.IsNullOrEmpty(s.Item) || s.Item == "null" || s.Item == "none" || AIBrain.TryItemPublic(s.Item, out _, out _) ? null : $"모르는 품목 '{s.Item}'";
        }
        if (d == AIGoalKinds.Of(GoalKind.Gather).WireName || d == AIGoalKinds.Of(GoalKind.Craft).WireName)
        {
            if (!AIBrain.TryItemPublic(s.Item ?? "", out _, out _)) return $"모르는 품목 '{s.Item}'";
            int hi = AIGoalKinds.Of(d == AIGoalKinds.Of(GoalKind.Gather).WireName ? GoalKind.Gather : GoalKind.Craft).PlannerMaxCount;
            if (s.Count < 1 || s.Count > hi) return $"개수 범위 밖({s.Count})";
            return null;
        }
        if (d == AIGoalKinds.Of(GoalKind.Store).WireName) return string.IsNullOrEmpty(s.Item) || s.Item == "null" || AIBrain.TryItemPublic(s.Item, out _, out _) ? null : $"모르는 품목 '{s.Item}'";
        if (d == AIGoalKinds.Of(GoalKind.Home).WireName || d == AIGoalKinds.Of(GoalKind.Farm).WireName || d == AIGoalKinds.Of(GoalKind.Cook).WireName || d == AIGoalKinds.Of(GoalKind.Explore).WireName || d == AIGoalKinds.Of(GoalKind.Follow).WireName) return null;
        // 제작대-놓기 §2.3: the where's form; place also a known item and a count in range
        if (d == AIGoalKinds.Of(GoalKind.Goto).WireName) return AIPlaces.WhereForm(d, s.Where);
        if (d == AIGoalKinds.Of(GoalKind.Place).WireName)
        {
            if (!AIBrain.TryItemPublic(s.Item ?? "", out _, out _)) return $"모르는 품목 '{s.Item}'";
            if (s.Count > AIGoalKinds.Of(GoalKind.Place).PlannerMaxCount) return $"개수 범위 밖({s.Count})";
            return AIPlaces.WhereForm(d, s.Where);
        }
        return $"모르는 goal.do '{d}'";
    }

    // One goal against the world now (an answer's goal, or a plan step when it starts).
    private static string CheckGoal(World server, Entity player, string d, string item, int count, string where = null, int keep = -1, int slot = -1, string mode = null, string second = null)
    {
        string lifeForm = AILifeGoalForm.Check(new PlanStep(d, item, count, where, keep, slot, mode, second));
        if (lifeForm != null) return lifeForm;
        if (d == "farm" || d == "cook")
        {
            string form = StepForm(new PlanStep(d, item, count, where, keep, slot, mode, second));
            if (form != null) return form;
            // Explicit selectors are narrowed in the tree; physical validity is checked at execution.
            if (AILifeGoalForm.Text(mode) != null || AILifeGoalForm.Text(item) != null || AILifeGoalForm.Text(second) != null || AILifeGoalForm.Text(where) != null) return null;
        }
        if (AIGoalKinds.TryWire(d, out var ik) && (AIGoalKinds.IsInventory(ik) || ik == GoalKind.Store && (!string.IsNullOrEmpty(item) && item != "null" || keep >= 0 || slot >= 0 || count > 0 || !string.IsNullOrEmpty(where))))
        {
            string form = StepForm(new PlanStep(d, item, count, where, keep, slot));
            if (form != null) return form;
            ObjectID iid = ObjectID.None;
            if (!string.IsNullOrEmpty(item) && item != "null" && item != "none") AIBrain.TryItemPublic(item, out iid, out _);
            return AIInventory.CheckGoal(server, player, ik, iid, AIInventoryGoalForm.CountFor(ik, count), where, keep, slot);
        }
        if (d == AIGoalKinds.Of(GoalKind.Gather).WireName)
        {
            if (!AIBrain.TryItemPublic(item ?? "", out ObjectID id, out _)) return $"모르는 품목 '{item}'";
            if (id != ObjectID.WallDirtBlock && id != ObjectID.CopperOre && id != ObjectID.TinOre && id != ObjectID.Wood && !S._gatherFood.Contains(id)) return $"보고의 can_do.gather에 없는 품목 '{item}'";
            if (count < 1 || count > AIGoalKinds.Of(GoalKind.Gather).PlannerMaxCount) return $"개수 범위 밖({count})";
            return null;
        }
        if (d == AIGoalKinds.Of(GoalKind.Craft).WireName)
        {
            if (!AIBrain.TryItemPublic(item ?? "", out ObjectID cid, out _)) return $"모르는 품목 '{item}'";
            if (AICook.IsCookedFood(server, cid, 0)) return $"요리한 음식 '{item}'은 craft가 아니라 cook 목표로 만든다";
            if (!AICraft.KnownRecipe(server, player, cid, out _)) return AICraft.CraftFact(server, player, cid);   // 제작대-놓기 §2.5: the way to it, not only "unknown"
            if (count < 1 || count > AIGoalKinds.Of(GoalKind.Craft).PlannerMaxCount) return $"개수 범위 밖({count})";
            return null;
        }
        if (d == AIGoalKinds.Of(GoalKind.Store).WireName)
        {
            ObjectID sid = ObjectID.None;
            if (!string.IsNullOrEmpty(item) && item != "null" && !AIBrain.TryItemPublic(item, out sid, out _)) return $"모르는 품목 '{item}'";
            if (AIBase.Storable(server, player, sid).Count == 0) return $"가방에 넣을 것이 없음(bag_storable 참고){(sid != ObjectID.None ? $": {sid}" : "")}";
            return null;
        }
        if (d == AIGoalKinds.Of(GoalKind.Home).WireName) return AIBase.BaseDist(AIBody.Pos(server, player)) > AIBase.HomeArrive ? null : "이미 기지에 있음";
        if (d == AIGoalKinds.Of(GoalKind.Farm).WireName) return AIFarm.CanFarm(server, player);
        if (d == AIGoalKinds.Of(GoalKind.Cook).WireName) return AICook.CanCook(server, player);
        if (d == AIGoalKinds.Of(GoalKind.Explore).WireName) return null;
        if (d == AIGoalKinds.Of(GoalKind.Follow).WireName) return math.lengthsq(AIBody.HostPosition(server, player)) >= 0f ? null : "호스트가 없음";
        if (d == AIGoalKinds.Of(GoalKind.Goto).WireName) return AIPlaceGoal.CheckGoto(server, player, where);   // 제작대-놓기 §2.3
        if (d == AIGoalKinds.Of(GoalKind.Place).WireName) return AIPlaceGoal.CheckPlace(server, player, item, count, where);
        return $"모르는 goal.do '{d}'";
    }

    // TEST ONLY: one GET /v1/health from inside the game (does the game allow plain http to localhost?).
    public static void HealthProbe()
    {
        _health = UnityWebRequest.Get(BridgeUrl + "/v1/health");
        _health.timeout = 10;
        _health.SendWebRequest();
        AIPlayerSpikeMod.Log($"PLANNER: health probe sent to {BridgeUrl}/v1/health");
    }

    // cut at n characters without splitting a surrogate pair (an emoji, 검수 작은 지적 8)
    private static string Trim(string s, int n)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= n) return s;
        if (n > 0 && char.IsHighSurrogate(s[n - 1])) n--;
        return s.Substring(0, n) + "…";
    }
}
