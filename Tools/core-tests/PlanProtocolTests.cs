// S1: AIPlanProtocol.Read against Tools/ai-bridge/test/fixtures/plan/cases.expect.json (refactor plan 2-계획.md S1).
// Each case: the new reader must give exactly `expect`; the old hand reader (a copy of AIPlanner.Field/ObjectAt/ToInt
// at 3c3adbe, LegacyPlanReader below) must give the same values when the case says legacy "same", and must differ when
// it says escape / nested / malformed / unreachable. The differences are printed as a table (for the step report).
// Fixtures are read with System.Text.Json (not ExpJson, which is under test).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class PlanProtocolTests
{
    public static readonly List<string> DiffTable = new List<string>();

    public static void Run()
    {
        string file = Path.Combine(Repo.Root, "Tools", "ai-bridge", "test", "fixtures", "plan", "cases.expect.json");
        Program.Check(File.Exists(file), "fixtures/plan/cases.expect.json exists");
        if (!File.Exists(file)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        int n = 0; var byCat = new Dictionary<string, int>();
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            n++;
            string name = c.GetProperty("name").GetString(), legacy = c.GetProperty("legacy").GetString();
            int status = c.GetProperty("status").GetInt32();
            string body = c.GetProperty("body").GetString();
            byCat.TryGetValue(legacy, out int k); byCat[legacy] = k + 1;
            var got = Flat(AIPlanProtocol.Read(body));
            var want = FlatExpect(c.GetProperty("expect"));
            var bad = new List<string>();
            foreach (var kv in want) if (!got.TryGetValue(kv.Key, out var g) || g != kv.Value) bad.Add($"{kv.Key}: want {Show(kv.Value)} got {Show(g)}");
            Program.Check(bad.Count == 0, $"{name}: reads as expected{(bad.Count > 0 ? " — " + string.Join("; ", bad) : "")}");

            var old = LegacyPlanReader.Read(body);
            bool applies = status == 200 && (IsNone(got["ErrorRaw"]) || IsNone(old["ErrorRaw"]));
            var diffs = new List<string>();
            foreach (var kv in got)
            {
                if (kv.Key.StartsWith("Answer") && !applies) continue;
                if (kv.Key == "Parsed") continue;   // the old reader had no such flag
                old.TryGetValue(kv.Key, out var o);
                if (o != kv.Value) diffs.Add($"{kv.Key} {Show(o)} -> {Show(kv.Value)}");
            }
            if (legacy == "same") Program.Check(diffs.Count == 0, $"{name}: same as the old reader{(diffs.Count > 0 ? " — " + string.Join("; ", diffs) : "")}");
            else
            {
                Program.Check(diffs.Count > 0, $"{name}: differs from the old reader ({legacy})");
                DiffTable.Add($"| {name} | {legacy} | {string.Join("; ", diffs).Replace("|", "\\|")} |");
            }
        }
        Program.Check(n >= 30, $"{n} cases");
        Console.WriteLine("  cases by legacy class: " + string.Join(", ", ByKey(byCat)));
        Console.WriteLine("  differences from the old reader:");
        Console.WriteLine("  | case | class | field old -> new |");
        foreach (var l in DiffTable) Console.WriteLine("  " + l);
        // 2026-11-08: the valuable-kind field (booleans only; a non-object is absent)
        var v = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"keep\",\"valuable\":{\"CopperOre\":false,\"TinOre\":true,\"Bad\":\"yes\"}}}").Answer;
        var none = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"keep\",\"valuable\":[1]}}").Answer;
        Program.Check(v?.Valuable != null && v.Valuable.Count == 2 && v.Valuable["CopperOre"] == false && v.Valuable["TinOre"] && none?.Valuable == null,
            "plan answer reads boolean valuable-kind changes only");
    }

    static IEnumerable<string> ByKey(Dictionary<string, int> d) { var l = new List<string>(); foreach (var kv in d) l.Add($"{kv.Key} {kv.Value}"); l.Sort(); return l; }
    static bool IsNone(string s) => s == null || s == "<null>" || s == "null";
    static string Show(string s) => s == null ? "(missing)" : s.Replace("\n", "\\n");

    // Every PlanReply field as a string ("<null>" for null) so new, expected and old compare the same way.
    static string S(string s) => s ?? "<null>";
    public static Dictionary<string, string> Flat(PlanReply r)
    {
        var d = new Dictionary<string, string>
        {
            ["Parsed"] = r.Parsed ? "true" : "false", ["ErrorRaw"] = S(r.ErrorRaw), ["DetailRaw"] = S(r.DetailRaw),
            ["RetryAfterRaw"] = S(r.RetryAfterRaw), ["RecallSummaryRaw"] = S(r.RecallSummaryRaw), ["Answer"] = r.Answer == null ? "null" : "object",
        };
        var a = r.Answer ?? new PlanAnswer();
        d["Answer.Decision"] = r.Answer == null ? "-" : S(a.Decision); d["Answer.Say"] = r.Answer == null ? "-" : S(a.Say); d["Answer.Why"] = r.Answer == null ? "-" : S(a.Why);
        d["Answer.For"] = r.Answer == null ? "-" : a.HasFor ? $"{a.ForReport}/{a.ForGoalRev}" : "none";
        d["Answer.Goal"] = r.Answer == null ? "-" : a.HasGoal ? $"{S(a.GoalDo)}|{S(a.GoalItem)}|{a.GoalCount}" : "none";
        return d;
    }

    static Dictionary<string, string> FlatExpect(JsonElement e)
    {
        string Str(JsonElement o, string k) => o.GetProperty(k).ValueKind == JsonValueKind.Null ? null : o.GetProperty(k).GetString();
        var r = new PlanReply
        {
            Parsed = e.GetProperty("Parsed").GetBoolean(), ErrorRaw = Str(e, "ErrorRaw"), DetailRaw = Str(e, "DetailRaw"),
            RetryAfterRaw = Str(e, "RetryAfterRaw"), RecallSummaryRaw = Str(e, "RecallSummaryRaw"),
        };
        var a = e.GetProperty("Answer");
        if (a.ValueKind != JsonValueKind.Null)
            r.Answer = new PlanAnswer
            {
                Decision = Str(a, "Decision"), Say = Str(a, "Say"), Why = Str(a, "Why"),
                HasFor = a.GetProperty("HasFor").GetBoolean(), ForReport = a.GetProperty("ForReport").GetInt32(), ForGoalRev = a.GetProperty("ForGoalRev").GetInt32(),
                HasGoal = a.GetProperty("HasGoal").GetBoolean(), GoalDo = Str(a, "GoalDo"), GoalItem = Str(a, "GoalItem"), GoalCount = a.GetProperty("GoalCount").GetInt32(),
            };
        return Flat(r);
    }
}

// A copy of the hand reader AIPlanner used until S1 (3c3adbe AIPlanner.cs: Tick :360-366, Apply :390-410,
// ToInt :492, Field :496-505, ObjectAt :508-521), giving the same flat fields. Test only.
public static class LegacyPlanReader
{
    public static Dictionary<string, string> Read(string text)
    {
        var r = new PlanReply { ErrorRaw = Field(text, "error"), DetailRaw = Field(text, "detail"), RetryAfterRaw = Field(text, "retry_after_s"), RecallSummaryRaw = Field(text, "recall_summary") };
        int ai = text.IndexOf("\"answer\":", StringComparison.Ordinal);
        try
        {
            string inner = ai >= 0 ? ObjectAt(text, text.IndexOf('{', ai)) : null;
            if (inner != null)
            {
                var a = new PlanAnswer { Decision = Field(inner, "decision"), Say = Field(inner, "say"), Why = Field(inner, "why") };
                int fi = inner.IndexOf("\"for\":", StringComparison.Ordinal);
                string fo = fi >= 0 ? ObjectAt(inner, inner.IndexOf('{', fi)) : null;
                if (fo != null) { a.HasFor = true; a.ForReport = ToInt(Field(fo, "report")); a.ForGoalRev = ToInt(Field(fo, "goal_rev")); }
                int gi = inner.IndexOf("\"goal\":", StringComparison.Ordinal);
                string go = gi >= 0 ? ObjectAt(inner, inner.IndexOf('{', gi)) : null;
                if (go != null) { a.HasGoal = true; a.GoalDo = Field(go, "do"); a.GoalItem = Field(go, "item"); a.GoalCount = ToInt(Field(go, "count")); }
                r.Answer = a;
            }
        }
        catch { r.Answer = null; }
        return PlanProtocolTests.Flat(r);
    }

    static int ToInt(string s) => int.TryParse(s, out int v) ? v : -1;

    static string Field(string t, string key)
    {
        int i = t.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (i < 0) return null;
        int s = i + key.Length + 3;
        if (s >= t.Length) return null;
        if (t[s] == '"') { int e = t.IndexOf('"', s + 1); return e > s ? t.Substring(s + 1, e - s - 1) : null; }
        int e2 = s; while (e2 < t.Length && t[e2] != ',' && t[e2] != '}') e2++;
        return t.Substring(s, e2 - s).Trim();
    }

    static string ObjectAt(string t, int i)
    {
        if (i < 0) return null;
        int depth = 0; bool inStr = false, esc = false;
        for (int k = i; k < t.Length; k++)
        {
            char c = t[k];
            if (inStr) { if (esc) esc = false; else if (c == '\\') esc = true; else if (c == '"') inStr = false; continue; }
            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return t.Substring(i, k - i + 1);
        }
        return null;
    }
}

// The repository root (the folder that has Tools/ai-bridge), found upward from the working folder or the binary.
public static class Repo
{
    static string _root;
    public static string Root => _root ?? (_root = Find());
    static string Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "Tools", "ai-bridge"))) return d.FullName;
        return Directory.GetCurrentDirectory();
    }
}
