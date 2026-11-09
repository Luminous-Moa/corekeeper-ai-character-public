// Character-local read-only life navigation. Concrete observations and actions stay with their feature owners.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static class AILife
{
    public sealed class Entry
    {
        public string Op = "", Id = "", Text = "";
        public AICandidate Candidate;
        public Func<Entry> Observe; // only the current page evaluates a counterpart prediction/route
    }
    private sealed class State
    {
        public LifeCursor Cursor;
        public AIGoal Goal;
        public readonly LifeProgress Progress = new LifeProgress();
        public readonly Dictionary<string, LifeNav> Offered = new Dictionary<string, LifeNav>(StringComparer.Ordinal);
        public readonly Dictionary<AISkill, LifeSelection> Runs = new Dictionary<AISkill, LifeSelection>();
        public string Facts = "", Result = "";
    }
    private static State S => AIAgent.Cur.Get<State>(AISlot.LifeTree);
    private static State Sync()
    {
        var s = S;
        if (s.Cursor == null) s.Cursor = new LifeCursor(AIAgent.Cur.Id, AIGoals.GoalRevision);
        if (!ReferenceEquals(s.Goal, AIGoals.Goal) || s.Cursor.GoalRevision != AIGoals.GoalRevision || s.Cursor.Owner != AIAgent.Cur.Id)
        {
            s.Goal = AIGoals.Goal;
            s.Cursor.Reset(AIAgent.Cur.Id, AIGoals.GoalRevision);
            s.Progress.Reset(s.Goal, AIGoals.GoalRevision);
            s.Offered.Clear(); s.Facts = s.Result = "";
        }
        return s;
    }
    public static void Reset() { AIAgent.Cur.Slots[AISlot.LifeTree] = null; }
    public static void OnGoalChanged() { Sync(); }
    public static LifeCursor Cursor => Sync().Cursor;
    private static string Clean(string x) => AILifeGoalForm.Text(x) ?? "";
    private static string ItemName(string x) { x = Clean(x); int at = x.IndexOf("#v", StringComparison.Ordinal); return at < 0 ? x : x.Substring(0, at); }
    private static string Branch(AIGoal g) => g.kind == GoalKind.Farm ? "farm" : g.kind == GoalKind.Cook ? "cook" : g.kind == GoalKind.Craft ? "craft" : g.kind == GoalKind.Store || g.kind == GoalKind.Take ? "inventory" : "";
    private static bool Selected(string selected, string specified) => Clean(specified) == "" || Clean(selected) == "" || ItemName(selected) == ItemName(specified);
    private static bool Matches(LifeSelection q, AIGoal g)
    {
        if (g == null || Branch(g) == "" || q.Branch != Branch(g)) return false;
        string gm = g.kind == GoalKind.Store ? "store" : g.kind == GoalKind.Take ? "take" : g.mode;
        if (!Selected(q.Mode, gm) || !Selected(q.Item, g.item == ObjectID.None ? "" : g.item.ToString()) || !Selected(q.Second, g.second)) return false;
        if (g.slot >= 0 && q.Slot >= 0 && g.slot != q.Slot) return false;
        if (q.Branch == "farm")
        {
            // The farm producer intersects every current tile/region with the original goal bounds.
            if (AILifeGoalForm.TryFarmBounds(g.where, out _, out _, out _, out _) && q.Where.StartsWith("tile:", StringComparison.Ordinal))
            {
                if (!AILifeGoalForm.TryFarmBounds(q.Where, out int x, out int y, out _, out _) || !AILifeGoalForm.InFarmWhere(g.where, x, y)) return false;
            }
            return true;
        }
        if (q.Branch == "inventory" && (Clean(g.where) == "" || g.where == "base"))
        {
            if (Clean(q.Where) == "") return true;
            if (!q.Where.StartsWith("chest:", StringComparison.Ordinal) || !AILifeGoalForm.TryFarmBounds("tile:" + q.Where.Substring(6), out int x, out int y, out _, out _)) return false;
            return math.distance((float2)new int2(x, y), (float2)AIBase.BaseTile) <= AIBase.BaseRadius;
        }
        return Clean(g.where) == "" || Clean(q.Where) == "" || g.where == q.Where;
    }
    private static LifeSelection Bind(LifeSelection q)
    {
        var s = Sync(); q.GoalRevision = AIGoals.GoalRevision;
        q.MatchesGoal = Matches(q, s.Goal); q.Goal = q.MatchesGoal ? s.Goal : null;
        q.RequestedCount = q.MatchesGoal ? Remaining(q) : 1;
        return q;
    }
    // Producers call this after an actual slot/item identity resolves a cursor's unspecified fields.
    public static LifeSelection Resolve(LifeSelection selection) => Bind(selection.Copy());
    public static LifeSelection ForGoal(AIGoal g)
    {
        var q = new LifeSelection { Branch = Branch(g), Mode = g.kind == GoalKind.Store ? "store" : g.kind == GoalKind.Take ? "take" : Clean(g.mode),
            Item = g.item == ObjectID.None ? "" : g.item.ToString(), Second = Clean(g.second), Where = Clean(g.where), Slot = g.slot,
            Goal = g, GoalRevision = AIGoals.GoalRevision, MatchesGoal = ReferenceEquals(g, AIGoals.Goal) };
        q.RequestedCount = q.MatchesGoal ? Remaining(q) : 1;
        return q;
    }
    private static LifeSelection ForCursor()
    {
        var c = Cursor;
        return Bind(new LifeSelection { Branch = c.Branch, Mode = c.Mode, Item = c.Item, Second = c.Second, Where = c.Where, Slot = c.Slot });
    }
    // A narrowed choice still belongs to the original explicit quantity. Separate selections never share this key.
    private static LifeSelection ProgressKey(LifeSelection selection)
    {
        var q = selection.Copy(); var g = q.Goal as AIGoal;
        if (g == null) return q;
        q.Mode = Clean(g.mode);
        // An unspecified goal selector is a wildcard over the requested work, not a fresh quota per local choice.
        q.Item = g.item == ObjectID.None ? "" : g.item.ToString();
        q.Second = Clean(g.second);
        q.Where = Clean(g.where);
        if (g.slot >= 0) q.Slot = g.slot; else q.Slot = -1;
        return q;
    }
    public static int Remaining(LifeSelection selection)
    {
        var s = Sync(); var g = selection?.Goal as AIGoal;
        if (g == null || !selection.MatchesGoal || !ReferenceEquals(g, s.Goal)) return 1;
        if (g.count <= 0) return 0;
        if (selection.Branch == "inventory") return Math.Max(0, g.count - AIInventory.MovedForGoal(g));
        return s.Progress.Remaining(ProgressKey(selection), g.count);
    }
    public static bool RequestSatisfied(LifeSelection selection)
        => selection?.MatchesGoal == true && selection.Goal is AIGoal g && g.count > 0 && Remaining(selection) == 0;
}
