// AIPlanInbox — what is waiting to be reported to the upper LLM, per character (refactor plan 2-계획.md S4). Unity-free
// (Tools/core-tests). Today's rule is kept: one waiting report, the latest one wins (AIPlanner's former string
// _pendingTrigger; the planner still reads and writes it through a property of that name). Kind is the trigger word
// the report sends (auto_on, host_said, host_goal, goal_done, goal_stalled, explore_ended, back_up, fight_end, plan_done, plan_step_failed, periodic, retry); Text is for the
// natural-language input of feature work 3 and is unused now. AIPlanner.Post(owner, trigger) puts into a named
// character's inbox from outside a character scope (nothing calls it yet).
// (If Scripts/Core is ever flattened, this file becomes Scripts/AICorePlanInbox.cs: a rename only.)
public struct PlanTrigger
{
    public string Kind;
    public string Text;
    public PlanTrigger(string kind, string text = null) { Kind = kind; Text = text; }
}

public sealed class PlanInbox
{
    public bool HasPending { get; private set; }
    public PlanTrigger Pending { get; private set; }

    // The latest wins, except that a waiting report is not replaced by a less urgent one (자연어 명령 2026-11-05): the
    // host speaking (host_said, host_goal) beats the goal events (goal_done, back_up, retry, …), which beat the periodic
    // look (periodic). A null kind is not a report (the old "_pendingTrigger = null" is Clear), so it is ignored.
    public void Put(PlanTrigger t)
    {
        if (t.Kind == null) return;
        if (HasPending && Priority(t.Kind) < Priority(Pending.Kind)) return;
        Pending = t; HasPending = true;
    }

    public static int Priority(string kind)
    {
        switch (kind)
        {
            case "host_said": case "host_goal": return 3;
            case "periodic": return 1;
            default: return 2;
        }
    }

    // The periodic look (합의 3): due when nothing is on the way or waiting, the upper LLM is usable, and period
    // seconds passed since the last report was sent or answered (the later of the two). period <= 0: off.
    public static bool PeriodicDue(float now, float lastAt, float period, bool busy, bool down)
        => period > 0f && !busy && !down && now - lastAt >= period;

    public bool TryTake(out PlanTrigger t)
    {
        t = Pending;
        if (!HasPending) return false;
        Clear();
        return true;
    }

    public void Clear() { Pending = default; HasPending = false; }
}
