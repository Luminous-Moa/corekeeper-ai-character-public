// S4: PlanInbox keeps the old _pendingTrigger rule (one waiting report, the latest wins; null = none).
public static class InboxTests
{
    public static void Run()
    {
        var b = new PlanInbox();
        Program.Check(!b.HasPending && !b.TryTake(out _), "empty: nothing waiting");
        b.Put(new PlanTrigger("goal_done")); b.Put(new PlanTrigger("back_up"));
        Program.Check(b.HasPending && b.Pending.Kind == "back_up", "the latest wins");
        Program.Check(b.TryTake(out var t) && t.Kind == "back_up" && !b.HasPending, "take empties it");
        b.Put(new PlanTrigger("retry")); b.Put(new PlanTrigger(null));
        Program.Check(b.HasPending && b.Pending.Kind == "retry", "a null kind is ignored (it is not a report)");
        b.Put(new PlanTrigger(""));
        Program.Check(b.HasPending && b.Pending.Kind == "", "an empty kind is a report, as the old string was (non-null)");
        b.Clear();
        Program.Check(!b.HasPending && b.Pending.Kind == null, "clear: nothing waiting");
        b.Put(new PlanTrigger("host_said", "Claude, 나무 좀 모아 줘"));
        Program.Check(b.TryTake(out var h) && h.Text == "Claude, 나무 좀 모아 줘", "the text travels with the kind (for feature work 3)");
    }
}
