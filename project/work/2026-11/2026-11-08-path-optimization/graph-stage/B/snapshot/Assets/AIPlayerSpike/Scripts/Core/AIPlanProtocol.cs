// AIPlanProtocol — reads the ai-bridge's reply to POST /v1/plan (refactor plan 2-계획.md S1). Unity-free on purpose:
// Tools/core-tests links Scripts/Core and checks it against the fixtures in Tools/ai-bridge/test/fixtures/plan.
// (If the game turns out not to compile Scripts/Core, this file moves flat to Scripts/AICorePlanProtocol.cs: a rename
// only, plus the core-tests link path.)
//
// Replaces AIPlanner's hand reader (Field/ObjectAt), which looked for the first "key": anywhere in the text (nested
// objects included) and cut strings at the next quote without undoing JSON escapes. This one parses the JSON
// (ExpJson.Parse) and reads top-level keys only. Accepted change (user, 2026-11-04): escaped and nested input now
// reads correctly. Everything else keeps the old values on purpose:
//   - *Raw strings are what the old Field() gave: a JSON string -> its (now decoded) text, JSON null -> "null" (so the
//     log still says error=null and a store item null is still the string "null"), a number -> its JSON text, true /
//     false -> "true" / "false", a missing key -> null. An object or array -> null (the old reader returned a cut
//     fragment there; the bridge never sends one in these keys).
//   - Integers (for.report, for.goal_rev, goal.count): an integral number in int range, or a string int.TryParse
//     accepts, else -1 (the old ToInt(Field(...))). A JSON 3.0 now reads 3 (old: -1); unreachable, the bridge
//     re-serialises with JSON.stringify.
//   - A body that is not a JSON object (cut, empty, HTML): every field null, Answer null. The old reader could still
//     pick keys out of a cut body; the bridge always sends a whole JSON body (UnityWebRequest gives the whole body).
using System.Collections.Generic;
using System.Globalization;

public sealed class PlanReply
{
    public bool Parsed;              // the body is a JSON object
    public string ErrorRaw;          // top-level "error"
    public string DetailRaw;         // top-level "detail"
    public string RetryAfterRaw;     // top-level "retry_after_s"
    public string RecallSummaryRaw;  // top-level "recall_summary"
    public PlanAnswer Answer;        // top-level "answer" when it is an object
}

public sealed class PlanAnswer
{
    public string Decision, Say, Why;            // *Raw rule (see above)
    public string Problem;                       // "problem": a problem the upper LLM reports (자연어 명령 decision 6)
    public string Order;                         // "order": what the answer does with the host's instruction (follow / defer / drop / done / answered), 자연어 명령
    // "orders": {"<id>": value} for earlier open host instructions (재검수 R3); null when missing or not an object. Keys
    // that are not integers and values that are not strings are left out.
    public Dictionary<int, string> Orders;
    public bool HasFor; public int ForReport = -1, ForGoalRev = -1;     // "for" when it is an object
    public bool HasGoal; public string GoalDo, GoalItem, GoalWhere, GoalMode, GoalSecond; public int GoalCount = -1;   // "goal" when it is an object (where: goto / place, 제작대-놓기)
    public int GoalKeep = -1, GoalSlot = -1;
    public int GoalMaxCount = -1;
    public int GoalMaxOrder = -1;
    // "plan": goal steps in order (합의 3e), each read like "goal"; null when the key is missing or not an array. A step
    // that is not an object is kept as an empty step (the planner rejects it with its number).
    public List<PlanStep> Plan;
}

public static class AIPlanProtocol
{
    public static PlanReply Read(string body)
    {
        var r = new PlanReply();
        var d = string.IsNullOrEmpty(body) ? null : ExpJson.Parse(body) as Dictionary<string, object>;
        if (d == null) return r;
        r.Parsed = true;
        r.ErrorRaw = Raw(d, "error");
        r.DetailRaw = Raw(d, "detail");
        r.RetryAfterRaw = Raw(d, "retry_after_s");
        r.RecallSummaryRaw = Raw(d, "recall_summary");
        if (d.TryGetValue("answer", out var av) && av is Dictionary<string, object> a)
        {
            var p = new PlanAnswer { Decision = Raw(a, "decision"), Say = Raw(a, "say"), Why = Raw(a, "why"), Order = Raw(a, "order"), Problem = Raw(a, "problem") };
            if (a.TryGetValue("for", out var fv) && fv is Dictionary<string, object> f)
            {
                p.HasFor = true;
                p.ForReport = Int(f, "report");
                p.ForGoalRev = Int(f, "goal_rev");
            }
            if (a.TryGetValue("goal", out var gv) && gv is Dictionary<string, object> g)
            {
                p.HasGoal = true;
                p.GoalDo = Raw(g, "do");
                p.GoalMode = Selector(g, "mode");
                p.GoalSecond = Selector(g, "second");
                p.GoalItem = GoalText(g, "item");
                p.GoalCount = GoalCount(g);
                p.GoalWhere = GoalText(g, "where");
                p.GoalKeep = GoalOptionalInt(g, "keep");
                p.GoalSlot = GoalOptionalInt(g, "slot");
                p.GoalMaxCount = ExplicitMaxCount(g);
                p.GoalMaxOrder = ExplicitPositive(g, "max_order");
            }
            if (a.TryGetValue("orders", out var ov) && ov is Dictionary<string, object> om)
            {
                p.Orders = new Dictionary<int, string>();
                foreach (var kv in om) if (int.TryParse(kv.Key, out int id) && kv.Value is string sv) p.Orders[id] = sv;
            }
            if (a.TryGetValue("plan", out var pv) && pv is List<object> pl)
            {
                p.Plan = new List<PlanStep>();
                foreach (var x in pl)
                {
                    var st = x as Dictionary<string, object>;
                    p.Plan.Add(st == null ? new PlanStep(null, null, -1) : new PlanStep(Raw(st, "do"), GoalText(st, "item"), GoalCount(st), GoalText(st, "where"), GoalOptionalInt(st, "keep"), GoalOptionalInt(st, "slot"), Selector(st, "mode"), Selector(st, "second"), ExplicitMaxCount(st), ExplicitPositive(st, "max_order")));
                }
            }
            r.Answer = p;
        }
        return r;
    }

    // New life fields have a strict string contract. Keep invalid input distinguishable from omission.
    private static int ExplicitMaxCount(Dictionary<string, object> goal) => ExplicitPositive(goal, "max_count");
    private static int ExplicitPositive(Dictionary<string, object> goal, string key)
    {
        if (!goal.TryGetValue(key, out var value) || value == null) return -1;
        return value is double n && n >= 1 && n <= int.MaxValue && n == System.Math.Floor(n) ? (int)n : -2;
    }

    private static string Selector(Dictionary<string, object> d, string key)
    {
        if (!d.TryGetValue(key, out var value) || value == null) return null;
        return value is string text ? AILifeGoalForm.Text(text) : AILifeGoalForm.InvalidSelector;
    }

    private static int GoalCount(Dictionary<string, object> d)
    {
        string action = Raw(d, "do");
        if (action == "maintain")
        {
            if (!d.TryGetValue("count", out var maintenanceCount)) return -1;
            return maintenanceCount is double single && single == 1 ? 1 : -2;
        }
        if (action != "farm" && action != "cook") return Int(d, "count");
        if (!d.TryGetValue("count", out var value) || value == null) return -1;
        return value is double n && n >= 1 && n <= int.MaxValue && n == System.Math.Floor(n) ? (int)n : -2;
    }

    private static string GoalText(Dictionary<string, object> d, string key) => Raw(d, "do") == "maintain" ? Selector(d, key) : Raw(d, key);

    private static int GoalOptionalInt(Dictionary<string, object> d, string key)
    {
        if (Raw(d, "do") != "maintain") return Int(d, key);
        if (!d.TryGetValue(key, out var value) || value == null) return -1;
        return value is double n && n >= 0 && n <= int.MaxValue && n == System.Math.Floor(n) ? (int)n : -2;
    }

    // What the old Field(text, key) gave for this key (see the header).
    public static string Raw(Dictionary<string, object> d, string key)
    {
        if (d == null || !d.TryGetValue(key, out var v)) return null;
        if (v == null) return "null";
        if (v is string s) return s;
        if (v is bool b) return b ? "true" : "false";
        if (v is double x) return x.ToString("R", CultureInfo.InvariantCulture);
        return null;
    }

    // The old ToInt(Field(...)): -1 unless an int.
    public static int Int(Dictionary<string, object> d, string key)
    {
        if (d == null || !d.TryGetValue(key, out var v) || v == null) return -1;
        if (v is double x) return x >= int.MinValue && x <= int.MaxValue && x == System.Math.Floor(x) ? (int)x : -1;
        if (v is string s) return int.TryParse(s, out int i) ? i : -1;
        return -1;
    }
}
