// AIPlanRules — the upper-LLM planner's decision rules and the plan queue, Unity-free so Tools/core-tests can check them
// (자연어 명령 검수 F8; work/2026-11/2026-11-05-자연어-명령/3-독립검수.md, 4-검수반영·계획.md). AIPlanner (Unity side)
// asks these and does the I/O.
//
// - Watching: when the upper LLM is asked by the 2-minute look and the triggers — auto on, a goal running, a plan step
//   about to start, or after any hold answer (검수 B1). A hold keeps the member watched until the next set or /ai stop,
//   on purpose (재검수 R4, user decision 3d "늘 스스로 이어 간다"): standing after a hold it is still asked every 2 minutes
//   and may come back to a put-off instruction or pick the next thing by itself.
// - Stale answers: an answer to the host speaking (host_said, host_goal) is never lost (검수 B2): keep / ask are applied
//   anyway; set / hold are asked again once with the host's line still new, and a second stale answer keeps its say and
//   order. Other triggers drop a stale answer as before.
// - Order: an answer changes the host instruction only if its report carried that same instruction (검수 F1), and only
//   by its "order" / "orders" fields (재검수 R2: no order, no change). AIHostOrders holds the instructions.
// - Events: the planner's own events (plan steps, plan end or stop, instruction changes, /ai stop) reach the next report
//   whatever the mode (재검수 R5); other events only while watching (EventKept).
// - Retry: a failed report is sent again only while watching, or for the host speaking up to HostTries (검수 F2).
// - Down: one down is told once, by back_up or by fight_end, whichever comes first (검수 F4).
// - AIPlanQueue: the steps of one answer (합의 3e). The planner sets step 1 as the goal; when a step ends well the next
//   one starts without asking; a failed step stops the rest, which the next report shows.
using System.Collections.Generic;
using System.Text;

public enum StaleAction { Apply, Drop, AskAgain, TalkOnly }

public static class AIPlanRules
{
    public const int HostTries = 3;

    public static bool IsHostTrigger(string t) => t == "host_said" || t == "host_goal";

    public static bool Watching(bool auto, bool goalReal, bool planNext, bool heldWatch) => auto || goalReal || planNext || heldWatch;

    // stale: the goal revision changed, a down came, or the answer is too old while the report was on the way.
    public static StaleAction OnStale(bool stale, string trigger, string decision, bool askedAgainAlready)
    {
        if (!stale) return StaleAction.Apply;
        if (!IsHostTrigger(trigger)) return StaleAction.Drop;
        if (decision == "keep" || decision == "ask") return StaleAction.Apply;
        return askedAgainAlready ? StaleAction.TalkOnly : StaleAction.AskAgain;
    }

    public static bool OrderApplies(int reportOrderSeq, int currentOrderSeq) => currentOrderSeq > 0 && reportOrderSeq == currentOrderSeq;

    // The goal revision changed while the report was on the way. Steps of the plan that was running when it was sent,
    // still running now, are that same plan going on, not a new state (실게임2 N1); any other change (the plan stopped
    // or ended, a host goal, no plan) is.
    public static bool RevStale(int reqRev, int nowRev, string reqPlanSource, string nowPlanSource)
        => reqRev != nowRev && !(reqPlanSource != null && reqPlanSource == nowPlanSource);

    // The plan the answer is checked against now (실게임3 M1): the running plan, or — no plan and no goal now — the plan
    // that ended (all steps done) while the report was on the way. A plan that ended well is that same plan's last
    // state, not a new one; a goal set since (host, another source) is.
    public static string PlanNow(bool planActive, string planSource, string endedSource, bool goalReal)
        => planActive ? planSource : goalReal ? null : endedSource;

    // A plan ended while a report about it was on the way (실게임3 M1): its plan_done / goal_done call is owed instead
    // of sent. After the answer: set / hold replace it (no call); keep / ask / talk-only leave nothing to go on, so the
    // owed call goes once — unless a goal or plan runs by then.
    public static bool OwedCallGoes(string decision, bool talkOnly, bool goalReal, bool planActive)
        => !goalReal && !planActive && (talkOnly || decision == "keep" || decision == "ask");

    // What an answer's order value makes of a host instruction (합의 2). Only the field (재검수 R2): an answer to host_said
    // must carry order; without one (or with an unknown value) nothing changes — not 답함, not 따름 — and the planner logs it.
    public static string OrderState(string order)
    {
        string o = (order ?? "").Trim().ToLowerInvariant();
        if (o == "follow") return "따름";
        if (o == "defer") return "미룸";
        if (o == "drop") return "포기";
        if (o == "done") return "끝남";
        if (o == "answered") return "답함";
        return null;
    }

    // An "orders" entry for an earlier open instruction applies if that instruction existed when the report was sent.
    public static bool EarlierOrderApplies(int id, int reportOrderSeq) => id > 0 && id <= reportOrderSeq;

    // An event reaches the next report while watching, or always when it is the planner's own (재검수 R5).
    public static bool EventKept(bool watching, bool force) => watching || force;

    // A plan is left running without its goal (the goal was cleared outside the plan: three failed decisions, the cost
    // cap, no API key — 재검수 작은 지적 3): stop it like a failed step.
    public static bool PlanOrphaned(bool planActive, bool planNext, bool goalReal, string goalSource, string planSource)
        => planActive && !planNext && !(goalReal && goalSource == planSource);

    // An answer's text field: JSON null (read as the string "null") is no text (재검수 작은 지적 5).
    public static string Text(string raw) => raw == "null" ? null : raw;

    // A hold puts the host's instruction off unless the answer says it follows it (the host asked to wait: 실게임2 N2).
    public static bool HoldPutsOff(string order) => (order ?? "").Trim().ToLowerInvariant() != "follow";

    public static bool MayRetry(bool watching, string trigger, int hostTries) => watching || (IsHostTrigger(trigger) && hostTries < HostTries);

    // downsNow: the member's downs so far; downsTold: the downs count when a back_up or fight_end about a down was sent
    public static bool DownTold(int downsNow, int downsTold) => downsNow <= downsTold;

    // The goal ends that let a plan go on (the step is done); every other end stops the plan.
    public static bool StepDone(string trigger) => trigger == "goal_done" || trigger == "completed" || trigger == "explore_ended";

    // 실게임4 K1: a step of a set answer that cannot be taken (an unknown item, a recipe of a station not placed) no longer
    // throws the whole answer away. stepErrors[i]: why step i cannot be taken now, or null. The cut is the first such
    // step, or -1 when every step can be taken. The steps before the cut are set; the say and order are applied anyway.
    public static int PlanCut(IList<string> stepErrors)
    {
        if (stepErrors == null) return -1;
        for (int i = 0; i < stepErrors.Count; i++) if (stepErrors[i] != null) return i;
        return -1;
    }

    // No step could be taken (cut 0): ask again at once only when nothing runs (no goal, no plan, no step about to
    // start) and not asked again already since the last answer that was taken — once, so a model that keeps naming
    // the same impossible step does not loop. Otherwise the next report (the 2-minute look, a trigger) carries the why.
    public static bool RefusedAskAgain(int cut, bool goalReal, bool planActive, bool planNext, bool askedAgainAlready)
        => cut == 0 && !goalReal && !planActive && !planNext && !askedAgainAlready;

    // The next report's last_answer_rejected for a cut plan: which steps were not taken and why.
    public static string RefusedNote(IList<PlanStep> steps, int cut, string why)
    {
        if (steps == null || cut < 0 || cut >= steps.Count) return null;
        var left = new List<string>();
        for (int i = cut; i < steps.Count; i++) left.Add(steps[i].Text);
        string at = $"{cut + 1}단계 '{steps[cut].Text}'";
        return cut == 0
            ? $"계획을 받지 않음(목표는 그대로): {at} — {why}. 받지 않은 단계: {string.Join(", ", left)}. say와 order는 적용됨"
            : $"계획을 {cut}단계까지만 받음: {at}부터 받지 않음 — {why}. 받지 않은 단계: {string.Join(", ", left)}";
    }
}

public sealed class PlanStep
{
    public string Do, Item;
    public int Count;
    public int Keep, Slot; // inventory reserve and exact source slot; -1 when unspecified
    public string Where;   // 제작대-놓기: goto / place (a place identifier, Core/AIPlaces); null when none
    public PlanStep(string d, string item, int count, string where = null, int keep = -1, int slot = -1) { Do = d; Item = item; Count = count; Where = string.IsNullOrEmpty(where) || where == "null" ? null : where; Keep = keep; Slot = slot; }
    public string Text => (string.IsNullOrEmpty(Item) || Item == "null" ? Do : Count > 0 ? $"{Do} {Item} {Count}" : $"{Do} {Item}") + (Where != null ? " " + Where : "") + (Keep >= 0 ? $" keep={Keep}" : "") + (Slot >= 0 ? $" slot={Slot}" : "");
}

public sealed class AIPlanQueue
{
    private readonly List<PlanStep> _steps = new List<PlanStep>();
    public string Source { get; private set; }      // the goal source every step is set with ("llm:<id>#<report>")
    public int Index { get; private set; } = -1;     // the step running now (0-based); -1 none
    public int Count => _steps.Count;
    public bool Active => Source != null;
    public PlanStep Current => Active && Index >= 0 && Index < _steps.Count ? _steps[Index] : null;
    public bool HasNext => Active && Index + 1 < _steps.Count;
    // the plan that stopped last (failed step, hold, a new goal from outside): shown once in the next report
    public string StoppedWhy { get; private set; }
    public List<PlanStep> StoppedRest { get; private set; }
    public PlanStep StoppedAt { get; private set; }

    public void Set(string source, IList<PlanStep> steps)
    {
        _steps.Clear(); _steps.AddRange(steps);
        Source = steps.Count > 0 ? source : null; Index = steps.Count > 0 ? 0 : -1;
        StoppedWhy = null; StoppedRest = null; StoppedAt = null;
    }

    public bool Owns(string goalSource) => Active && goalSource == Source;

    // The next step (Index moves on), or null when the plan is over (then it is cleared).
    public PlanStep Advance()
    {
        if (!HasNext) { Clear(); return null; }
        Index++;
        return _steps[Index];
    }

    public List<PlanStep> Rest()
    {
        var r = new List<PlanStep>();
        for (int i = Index + 1; i < _steps.Count; i++) r.Add(_steps[i]);
        return r;
    }

    // The plan stops at the step running now: what is left is kept for the next report.
    public void Stop(string why)
    {
        if (!Active) return;
        StoppedAt = Current; StoppedRest = Rest(); StoppedWhy = why;
        _steps.Clear(); Source = null; Index = -1;
    }

    public void Clear() { _steps.Clear(); Source = null; Index = -1; }
    public void ForgetStopped() { StoppedWhy = null; StoppedRest = null; StoppedAt = null; }

    // The report's "plan": the running plan (step k of n, the steps after it) and the one that stopped last, or null.
    public string ReportJson(System.Func<string, string> esc)
    {
        if (!Active && StoppedWhy == null) return "null";
        var sb = new StringBuilder("{");
        if (Active)
        {
            sb.Append($"\"step\":{Index + 1},\"of\":{_steps.Count},\"now\":\"{esc(Current?.Text ?? "")}\",\"next\":[");
            var rest = Rest();
            for (int i = 0; i < rest.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(esc(rest[i].Text)).Append('"'); }
            sb.Append(']');
        }
        if (StoppedWhy != null)
        {
            if (Active) sb.Append(',');
            sb.Append($"\"stopped\":{{\"at\":\"{esc(StoppedAt?.Text ?? "")}\",\"why\":\"{esc(StoppedWhy)}\",\"left\":[");
            for (int i = 0; i < StoppedRest.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(esc(StoppedRest[i].Text)).Append('"'); }
            sb.Append("]}");
        }
        sb.Append('}');
        return sb.ToString();
    }
}
