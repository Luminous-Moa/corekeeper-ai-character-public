// AIExpCore — the upper LLM's experience memory, step 1 (session 코어키퍼-AI-캐릭터, branch LLM-기억;
// work/2026-11/2026-11-04-LLM-기억/1-구현.md). Plain C# without Unity so Tools/exp-sim runs the same code offline.
//
// Files per character and world (AIPlayers/<world>/<member>/, next to memory.json):
//   events.jsonl   L1, append only, the source. One event per line (ExpRecorder.Event). Rotated to
//                  events-<yyyyMMddHHmmss>.jsonl past 8 MB; never read in normal play (only the tail at load).
//   goals.jsonl    L2, one line per goal run that ended (the goal_end event's payload). Derived: rebuilt from the
//                  events when missing or damaged. Read once at load, then kept in memory.
//   exp_state.json next event id, play seconds, the goal run still open, items/bosses/areas seen. Swapped in from a
//                  temporary file; the previous one stays as exp_state.json.bak.
//
// A goal run is one goal adopted by AIBrain.SetGoal until it ends; its id ("<session>.<event id>") is not the goal
// revision (that also changes on self-defence start/end). The same goal set again keeps its run. Downs are events
// inside a run, not its end. Only completed and blocked are judged results; cancelled (host), replaced, system
// (Decisions call trouble), unknown (the end was not seen) and exhausted (nothing left to explore) are not failures.
// Whether a goal's latest block still stands is judged by its cause only (ExpResolve), from the conditions at the run's
// end; without a measured change it turns "too old to judge" after a time that grows with each repeat.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class ExpJson
{
    public static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: if (c < 0x20) sb.Append(' '); else sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
    public static string S(string s) => "\"" + Esc(s) + "\"";
    public static string N(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
    public static string Arr(IEnumerable<string> items) => "[" + string.Join(",", (items ?? Enumerable.Empty<string>()).Select(S)) + "]";

    public static object Parse(string t)
    {
        int i = 0;
        try { return Val(t, ref i); } catch { return null; }
    }
    private static void Ws(string t, ref int i) { while (i < t.Length && char.IsWhiteSpace(t[i])) i++; }
    private static object Val(string t, ref int i)
    {
        Ws(t, ref i);
        char c = t[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++;
            Ws(t, ref i);
            if (t[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(t, ref i); string k = Str(t, ref i); Ws(t, ref i); i++; // ':'
                d[k] = Val(t, ref i); Ws(t, ref i);
                if (t[i] == ',') { i++; continue; }
                i++; return d;
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            Ws(t, ref i);
            if (t[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Val(t, ref i)); Ws(t, ref i);
                if (t[i] == ',') { i++; continue; }
                i++; return l;
            }
        }
        if (c == '"') return Str(t, ref i);
        if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (t.Length - i >= 5 && string.CompareOrdinal(t, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "null", 0, 4) == 0) { i += 4; return null; }
        int s = i;
        while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
        return double.Parse(t.Substring(s, i - s), CultureInfo.InvariantCulture);
    }
    private static string Str(string t, ref int i)
    {
        var sb = new StringBuilder(); i++;
        while (t[i] != '"')
        {
            char c = t[i++];
            if (c != '\\') { sb.Append(c); continue; }
            char e = t[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u': sb.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        i++;
        return sb.ToString();
    }

    public static string GetS(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is string s ? s : "";
    public static double GetD(Dictionary<string, object> d, string k, double def = 0) => d != null && d.TryGetValue(k, out var v) && v is double x ? x : def;
    public static int GetI(Dictionary<string, object> d, string k, int def = 0) => (int)GetD(d, k, def);
    public static long GetL(Dictionary<string, object> d, string k, long def = 0) => (long)GetD(d, k, def);
    public static Dictionary<string, object> GetO(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
    public static List<string> GetSA(Dictionary<string, object> d, string k)
    {
        var r = new List<string>();
        if (d != null && d.TryGetValue(k, out var v) && v is List<object> l) foreach (var x in l) if (x is string s) r.Add(s);
        return r;
    }
}

// What can explain a result and what can undo a block: gear levels (the game's ObjectInfo.level of the best working
// pick / melee weapon / armor piece in the inventory), max health, where, and what has been found so far (explored
// cells, station kinds, remembered ores of the goal's item), all from the mod's existing records (AIMemory). Unknown
// stays empty or -1.
public sealed class ExpCond
{
    public int hpMax = -1;
    public string hand = "";
    public string tool = ""; public int toolLv = -1;
    public string weapon = ""; public int weaponLv = -1;
    public int armorLv = -1;
    public string area = "";      // biome where the character stands (Slime, Larva, Stone, Nature, Sea, Desert, ...)
    public string cell = "";      // 16-tile cell "cx,cy"
    public int coreDist = -1;
    public int mates;             // other players on screen
    public int hunger = -1;
    public int seen = -1;         // explored cells (AIMemory.SeenCount)
    public int stations = -1;     // kinds of crafting stations remembered
    public int ore = -1;          // remembered ores of the goal's item (set at a goal's end)

    public string ToJson() =>
        $"{{\"hp\":{hpMax},\"hand\":{ExpJson.S(hand)},\"tool\":{ExpJson.S(tool)},\"tlv\":{toolLv},\"wpn\":{ExpJson.S(weapon)},\"wlv\":{weaponLv},\"alv\":{armorLv},\"area\":{ExpJson.S(area)},\"cell\":{ExpJson.S(cell)},\"core\":{coreDist},\"mates\":{mates},\"hunger\":{hunger},\"seen\":{seen},\"st\":{stations},\"ore\":{ore}}}";

    public static ExpCond From(Dictionary<string, object> d)
    {
        var c = new ExpCond();
        if (d == null) return c;
        c.hpMax = ExpJson.GetI(d, "hp", -1); c.hand = ExpJson.GetS(d, "hand");
        c.tool = ExpJson.GetS(d, "tool"); c.toolLv = ExpJson.GetI(d, "tlv", -1);
        c.weapon = ExpJson.GetS(d, "wpn"); c.weaponLv = ExpJson.GetI(d, "wlv", -1);
        c.armorLv = ExpJson.GetI(d, "alv", -1); c.area = ExpJson.GetS(d, "area"); c.cell = ExpJson.GetS(d, "cell");
        c.coreDist = ExpJson.GetI(d, "core", -1); c.mates = ExpJson.GetI(d, "mates"); c.hunger = ExpJson.GetI(d, "hunger", -1);
        c.seen = ExpJson.GetI(d, "seen", -1); c.stations = ExpJson.GetI(d, "st", -1); c.ore = ExpJson.GetI(d, "ore", -1);
        return c;
    }

    public string Short() =>
        $"곡괭이 {(tool == "" ? "없음" : tool)}(레벨 {toolLv}), 무기 {(weapon == "" ? "없음" : weapon)}(레벨 {weaponLv}), 방어구 레벨 {armorLv}, 최대 체력 {hpMax}, 지역 {(area == "" ? "모름" : area)}";
}

// One goal run (L2 line).
public sealed class ExpRun
{
    public string run = "", sess = "", key = "", kind = "", item = "", source = "", sourceRaw = "";
    public int count;
    public int start = -1;        // the item's inventory count when the run started (gather/craft)
    public int resets;            // the same goal set again while it ran (one run, not two)
    public string outcome = "";   // completed | blocked | exhausted | cancelled | replaced | system | unknown
    public string cls = "";       // blocked cause class: tool_weak | no_source | cannot_craft | only_wait
    public string cause = "";     // the game's own words (trimmed)
    public int got = -1;          // observed progress (gather/craft)
    public double secs;           // play seconds from start to end
    public int downs;
    public List<string> downBy = new List<string>();
    public string area = "";      // where the goal was worked (the biome seen most during the run)
    public ExpCond c0 = new ExpCond(), c1 = new ExpCond();
    public ExpCond cDown;         // conditions at the last down in the run (null: none)
    public int craftMissing = -1; // craft goals at the end: material units still missing for one (AICraft.Materials)
    public int craftStation = -1; // ... a station that makes it: 0 none known, 1 remembered, 2 at hand (in view or by hand)
    public long t0, t1;           // unix seconds
    public double play0, play1;   // play seconds of this character in this world
    public long ev0, ev1;         // goal_start / goal_end event ids

    public bool Judged => outcome == "completed" || outcome == "blocked";
    // Conditions to judge a block by: the end of the run (gear made during the run counts), else the start.
    public ExpCond EndCond => c1 != null && (c1.toolLv >= 0 || c1.hpMax > 0) ? c1 : c0;

    public string ToJson() =>
        $"{{\"run\":{ExpJson.S(run)},\"sess\":{ExpJson.S(sess)},\"key\":{ExpJson.S(key)},\"kind\":{ExpJson.S(kind)},\"item\":{ExpJson.S(item)},\"count\":{count},\"start\":{start},\"resets\":{resets},\"source\":{ExpJson.S(source)},\"source_raw\":{ExpJson.S(sourceRaw)}," +
        $"\"outcome\":{ExpJson.S(outcome)},\"cls\":{ExpJson.S(cls)},\"cause\":{ExpJson.S(cause)},\"got\":{got},\"secs\":{ExpJson.N(secs)},\"downs\":{downs},\"down_by\":{ExpJson.Arr(downBy)},\"area\":{ExpJson.S(area)}," +
        $"\"c0\":{c0.ToJson()},\"c1\":{c1.ToJson()},\"cdown\":{(cDown == null ? "null" : cDown.ToJson())},\"cmiss\":{craftMissing},\"cst\":{craftStation},\"t0\":{t0},\"t1\":{t1},\"play0\":{ExpJson.N(play0)},\"play1\":{ExpJson.N(play1)},\"ev0\":{ev0},\"ev1\":{ev1}}}";

    public static ExpRun From(Dictionary<string, object> d)
    {
        if (d == null) return null;
        var cd = ExpJson.GetO(d, "cdown");
        var r = new ExpRun
        {
            run = ExpJson.GetS(d, "run"), sess = ExpJson.GetS(d, "sess"), key = ExpJson.GetS(d, "key"), kind = ExpJson.GetS(d, "kind"),
            item = ExpJson.GetS(d, "item"), count = ExpJson.GetI(d, "count"), start = ExpJson.GetI(d, "start", -1), resets = ExpJson.GetI(d, "resets"),
            source = ExpJson.GetS(d, "source"), sourceRaw = ExpJson.GetS(d, "source_raw"),
            outcome = ExpJson.GetS(d, "outcome"), cls = ExpJson.GetS(d, "cls"), cause = ExpJson.GetS(d, "cause"), got = ExpJson.GetI(d, "got", -1),
            secs = ExpJson.GetD(d, "secs"), downs = ExpJson.GetI(d, "downs"), downBy = ExpJson.GetSA(d, "down_by"), area = ExpJson.GetS(d, "area"),
            c0 = ExpCond.From(ExpJson.GetO(d, "c0")), c1 = ExpCond.From(ExpJson.GetO(d, "c1")), cDown = cd == null ? null : ExpCond.From(cd), craftMissing = ExpJson.GetI(d, "cmiss", -1), craftStation = ExpJson.GetI(d, "cst", -1),
            t0 = ExpJson.GetL(d, "t0"), t1 = ExpJson.GetL(d, "t1"), play0 = ExpJson.GetD(d, "play0"), play1 = ExpJson.GetD(d, "play1"),
            ev0 = ExpJson.GetL(d, "ev0"), ev1 = ExpJson.GetL(d, "ev1"),
        };
        return string.IsNullOrEmpty(r.run) || string.IsNullOrEmpty(r.key) ? null : r;
    }

    // How AIBrain.ClearGoal's reason maps to a result.
    public static void Classify(string why, out string outcome, out string cls, out string cause)
    {
        why = why ?? "";
        cls = ""; cause = "";
        if (why == "goal done") { outcome = "completed"; return; }
        if (why.StartsWith("stalled:"))
        {
            outcome = "blocked";
            cause = Trim(why.Substring(8).Trim(), 160);
            // the craft text comes first: its preparation note can mention a tool too (one cause, one class)
            cls = why.Contains("기지로 갈 방법이 없음") || why.Contains("넣을 방법이 없음") ? "no_place"   // store / home (AIBase); its note can mention a tool too
                : why.Contains("기다리기뿐") ? "only_wait"
                : why.Contains("만들 방법이 없음") ? "cannot_craft"
                : why.Contains("도구 부족") ? "tool_weak"
                : "no_source";
            return;
        }
        if (why == "nothing left to explore") { outcome = "exhausted"; return; }
        if (why == "/ai stop" || why.StartsWith("/ai goal")) { outcome = "cancelled"; cause = why; return; }
        if (why == "cost cap" || why == "no api key" || why.StartsWith("3 consecutive failures")) { outcome = "system"; cause = Trim(why, 120); return; }
        outcome = "unknown"; cause = Trim(why, 120);
    }

    public static string Trim(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s ?? "" : s.Substring(0, n) + "…";
}

// Whether a goal key's block still stands now. Only the key's latest block after its last completion is judged (a later
// block means the earlier one was already tried again). It is judged by what can undo its cause, from the conditions
// at the end of its run:
//   tool_weak    -> a better pick (pick level up); with known levels nothing else, and no time limit
//   no_source    -> an ore (remembered ores known): that ore remembered in more places; new cells alone are only a
//                   weak hint (shown, no retry, the wait keeps growing). Other items: new cells explored
//   cannot_craft -> for that item: fewer missing materials (AICraft.Materials) or a station that makes it now known
//                   (remembered) or at hand (in view / hand recipe)
//   no_place     -> (store / home) new cells explored (a way to base or a chest spot may open), else time
//   only_wait    -> any new cell explored
//   exhausted    -> (explore) a better pick (digs further), or new cells explored by other goals
// Verdicts: Changed (a measured change: worth a retry), Standing, Stale (no measured change but long ago: not known now).
// The time to Stale doubles with each block since the last completion that came back without a measured change in
// between, whatever its cause (30 min, 1 h, 2 h, 4 h, 8 h; from 10 min when the latest is only_wait). Downs are
// compared by weapon, armor and max health.
public enum ExpVerdict { None, Stale, Changed, Standing }

public static class ExpResolve
{
    public const double WeakRetrySec = 30 * 60, WaitRetrySec = 10 * 60;
    public const int SeenStep = 4, MaxDoublings = 4;
    public static bool Backoff = true;   // test switch (exp-sim compares the repeats with it off)

    private static int Delta(int then, int now) => then >= 0 && now >= 0 ? now - then : 0;
    public static string Cls(ExpRun r) => r.outcome == "exhausted" ? "exhausted" : r.cls;
    public static bool IsBlock(ExpRun r) => r.outcome == "blocked" || r.outcome == "exhausted";

    public static double Wait(string cls, int streak)
    {
        double b = cls == "only_wait" ? WaitRetrySec : WeakRetrySec;
        int k = Backoff ? Math.Min(MaxDoublings, Math.Max(0, streak - 1)) : 0;
        return b * (1 << k);
    }

    // A change that can undo this block, measured from the block's end to `later` (ore: remembered ores of the item
    // later; craft: missing material units and station 0 none / 1 remembered / 2 at hand, -1 unknown).
    public static bool Measured(ExpRun r, ExpCond later, int oreLater, int craftMissLater, int craftStLater, out string why)
    {
        var e = r.EndCond;
        int ds = Delta(e.seen, later.seen);
        switch (Cls(r))
        {
            case "tool_weak":
                if (e.toolLv >= 0 && later.toolLv > e.toolLv) { why = $"곡괭이 레벨 {e.toolLv}→{later.toolLv}"; return true; }
                break;
            case "no_source":
                if (e.ore >= 0) { if (oreLater > e.ore) { why = $"그 뒤 {r.item}을(를) {oreLater - e.ore}곳 더 기억함"; return true; } break; }   // an ore: measured directly
                if (ds >= SeenStep) { why = $"그 뒤 새 구역 {ds}칸을 봄"; return true; }
                break;
            case "cannot_craft":
                if (r.craftStation >= 0 && craftStLater > r.craftStation) { why = craftStLater == 2 ? "그 뒤 이 품목을 만드는 제작대가 가까이 있음" : "그 뒤 이 품목을 만드는 제작대를 기억함"; return true; }
                if (r.craftMissing > 0 && craftMissLater >= 0 && craftMissLater < r.craftMissing) { why = $"그 뒤 모자란 재료 {r.craftMissing}→{craftMissLater}개"; return true; }
                break;
            case "no_place":
                if (ds >= SeenStep) { why = $"그 뒤 새 구역 {ds}칸을 봄"; return true; }
                break;
            case "only_wait":
                if (ds >= 1) { why = $"그 뒤 새 구역 {ds}칸을 봄"; return true; }
                break;
            case "exhausted":
                if (e.toolLv >= 0 && later.toolLv > e.toolLv) { why = $"곡괭이 레벨 {e.toolLv}→{later.toolLv}(더 파 들어갈 수 있음)"; return true; }
                if (ds >= SeenStep) { why = $"그 뒤 새 구역 {ds}칸을 봄"; return true; }
                break;
        }
        why = ""; return false;
    }

    private static string Unchanged(ExpRun r, ExpCond now, int craftMiss, int craftSt)
    {
        switch (Cls(r))
        {
            case "tool_weak": return $"곡괭이 레벨이 그대로 {now.toolLv}";
            case "no_source":
                {
                    int ds = Delta(r.EndCond.seen, now.seen);
                    return r.EndCond.ore >= 0 && ds > 0 ? $"새 구역 {ds}칸을 봤지만 {r.item}은(는) 더 기억하지 못함" : "그 뒤 새로 찾은 구역·광석 없음";
                }
            case "cannot_craft": return $"모자란 재료 {(craftMiss >= 0 ? craftMiss + "개" : "모름")}·제작대 {(craftSt == 2 ? "가까이 있음" : craftSt == 1 ? "기억함" : craftSt == 0 ? "모름" : "?")} 그대로";
            case "only_wait": return "잠깐 할 행동이 없던 상태";
            default: return "그 뒤 새로 본 구역 없음";
        }
    }

    // The latest blocks (any cause), back to one that came after a measured change (judged by the earlier block's cause).
    public static int Streak(List<ExpRun> blocks)
    {
        int n = 1;
        for (int i = blocks.Count - 2; i >= 0; i--)
        {
            var b = blocks[i];
            var nx = blocks[i + 1];
            if (Measured(b, nx.EndCond, nx.EndCond.ore, nx.craftMissing, nx.craftStation, out _)) break;
            n++;
        }
        return n;
    }

    // blocks: the key's blocks after its last completion, end order (the latest is judged).
    public static ExpVerdict Judge(List<ExpRun> blocks, ExpCond now, double playNow, Func<string, int> oreNow, Func<string, (int miss, int st)> craftNow, out string why, out int streak)
    {
        var r = blocks[blocks.Count - 1];
        streak = Streak(blocks);
        int oreL = r.item != "" && oreNow != null ? oreNow(r.item) : -1;
        var cr = r.item != "" && craftNow != null && Cls(r) == "cannot_craft" ? craftNow(r.item) : (-1, -1);
        if (Measured(r, now, oreL, cr.Item1, cr.Item2, out why)) return ExpVerdict.Changed;
        string cls = Cls(r);
        if (cls == "tool_weak" && r.EndCond.toolLv >= 0 && now.toolLv >= 0) { why = Unchanged(r, now, cr.Item1, cr.Item2); return ExpVerdict.Standing; }
        double since = Math.Max(0, playNow - r.play1), wait = Wait(cls, streak);
        if (since >= wait) { string u = Unchanged(r, now, cr.Item1, cr.Item2); why = $"{ExpReport.Dur(since)} 지남, {(u.StartsWith("새 구역") ? u : "바뀐 것은 못 봄")}"; return ExpVerdict.Stale; }
        why = $"{Unchanged(r, now, cr.Item1, cr.Item2)}, {ExpReport.Dur(wait - since)} 뒤 다시 볼 수 있음"; return ExpVerdict.Standing;
    }

    public static List<string> FightDiff(ExpCond then, ExpCond now)
    {
        var r = new List<string>();
        if (then == null || now == null) return r;
        if (then.weaponLv >= 0 && now.weaponLv >= 0 && then.weaponLv != now.weaponLv) r.Add($"무기 레벨 {then.weaponLv}→{now.weaponLv}");
        if (then.armorLv >= 0 && now.armorLv >= 0 && then.armorLv != now.armorLv) r.Add($"방어구 레벨 {then.armorLv}→{now.armorLv}");
        if (then.hpMax > 0 && now.hpMax > 0 && Math.Abs(now.hpMax - then.hpMax) >= Math.Max(10, then.hpMax / 10)) r.Add($"최대 체력 {then.hpMax}→{now.hpMax}");
        return r;
    }
}

// All ended runs of one character in one world, in end order, no run twice.
public sealed class ExpBook
{
    public readonly List<ExpRun> Rows = new List<ExpRun>();
    public readonly Dictionary<string, List<ExpRun>> ByKey = new Dictionary<string, List<ExpRun>>();
    private readonly HashSet<string> _runs = new HashSet<string>();
    public bool Has(string run) => _runs.Contains(run);
    public bool Add(ExpRun r)
    {
        if (r == null || !_runs.Add(r.run)) return false;
        Rows.Add(r);
        if (!ByKey.TryGetValue(r.key, out var l)) ByKey[r.key] = l = new List<ExpRun>();
        l.Add(r);
        return true;
    }
    // end order (event ids only grow, across sessions too)
    public void Sort()
    {
        Rows.Sort((a, b) => a.ev1.CompareTo(b.ev1));
        foreach (var l in ByKey.Values) l.Sort((a, b) => a.ev1.CompareTo(b.ev1));
    }
}

// The "experience" part of the upper LLM's report: the goal runs that matter now and whether their blocks still stand.
// Bounded: the running goal, at most 6 recent runs and 6 goal keys (each over its last 12 runs), 2200 characters.
public static class ExpReport
{
    public const int RecentN = 6, MaxKeys = 6, Window = 12, MaxChars = 2200, StreakLook = 24;
    public const double DangerPlaySec = 3 * 3600;

    public static string OutcomeKo(string o) => o == "completed" ? "완료" : o == "blocked" ? "막힘" : o == "exhausted" ? "탐험할 곳 없음"
        : o == "cancelled" ? "호스트가 취소" : o == "replaced" ? "다른 목표로 바뀜" : o == "system" ? "실행 장애" : "결과 모름";
    public static string ClsKo(string c) => c == "tool_weak" ? "도구 부족" : c == "no_source" ? "모을 곳 못 찾음" : c == "cannot_craft" ? "만들 방법 없음" : c == "only_wait" ? "할 행동 없음" : c == "no_place" ? "기지·상자에 갈 곳/넣을 곳 없음" : c == "exhausted" ? "탐험할 곳 없음" : "";
    public static string VerdictKo(ExpVerdict v) => v == ExpVerdict.Standing ? "풀리지 않음" : v == ExpVerdict.Changed ? "다시 해 볼 만함" : v == ExpVerdict.Stale ? "오래돼 모름" : "없음";

    public static string Ago(double playNow, double play)
    {
        double s = Math.Max(0, playNow - play);
        if (s < 90) return "방금";
        if (s < 5400) return $"{Math.Round(s / 60)}분 전";
        return $"{(s / 3600).ToString("0.#", CultureInfo.InvariantCulture)}시간 전";
    }
    public static string Dur(double s) => s < 90 ? $"{Math.Round(s)}초" : s < 5400 ? $"{Math.Round(s / 60)}분" : $"{(s / 3600).ToString("0.#", CultureInfo.InvariantCulture)}시간";

    private static string Line(ExpRun r, double playNow)
    {
        var sb = new StringBuilder();
        sb.Append($"{Ago(playNow, r.play1)} {r.key}{(r.count > 0 ? "×" + r.count : "")}({(r.source == "host" ? "호스트" : "LLM")}) → {OutcomeKo(r.outcome)}");
        string ck = ClsKo(r.cls); if (ck != "") sb.Append($"({ck})");
        if (r.got >= 0 && r.count > 0) sb.Append($" {r.got}/{r.count}");
        sb.Append($", {Dur(r.secs)}");
        if (r.area != "") sb.Append($", {r.area}에서");
        if (r.downs > 0) sb.Append($", 쓰러짐 {r.downs}({string.Join("·", r.downBy.Distinct().Take(3))})");
        return sb.ToString();
    }

    private static string Counts(IEnumerable<string> xs) =>
        string.Join(", ", xs.Where(x => !string.IsNullOrEmpty(x)).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Count() > 1 ? $"{g.Key} {g.Count()}" : g.Key));

    // The key's blocks after its last completion (at most the last StreakLook), end order.
    public static List<ExpRun> BlocksSinceDone(List<ExpRun> all)
    {
        int lastDone = all.FindLastIndex(r => r.outcome == "completed");
        var l = new List<ExpRun>();
        for (int i = Math.Max(lastDone + 1, all.Count - StreakLook); i < all.Count; i++) if (ExpResolve.IsBlock(all[i])) l.Add(all[i]);
        return l;
    }

    public static ExpVerdict BlockState(List<ExpRun> all, ExpCond now, double playNow, Func<string, int> oreNow, Func<string, (int, int)> craftNow)
    {
        var bl = BlocksSinceDone(all);
        return bl.Count == 0 ? ExpVerdict.None : ExpResolve.Judge(bl, now, playNow, oreNow, craftNow, out _, out _);
    }

    public static string KeyPart(List<ExpRun> all, ExpCond now, double playNow, Func<string, int> oreNow, Func<string, (int, int)> craftNow)
    {
        var win = all.Skip(Math.Max(0, all.Count - Window)).ToList();
        int done = win.Count(r => r.outcome == "completed"), blocked = win.Count(r => r.outcome == "blocked");
        int other = win.Count - done - blocked;
        int lastDone = all.FindLastIndex(r => r.outcome == "completed");
        int superseded = win.Count(r => r.outcome == "blocked" && all.IndexOf(r) < lastDone);
        var bl = BlocksSinceDone(all);
        var verdict = ExpVerdict.None;
        var parts = new List<string>();
        if (bl.Count > 0)
        {
            verdict = ExpResolve.Judge(bl, now, playNow, oreNow, craftNow, out string why, out int streak);
            string ck = ClsKo(ExpResolve.Cls(bl[bl.Count - 1]));
            string rep = streak > 1 ? $"; 바뀐 것 없이 연속 {streak}번" : "";
            if (verdict == ExpVerdict.Standing) parts.Add($"아직 풀리지 않은 막힘({ck}: {why}{rep})");
            else if (verdict == ExpVerdict.Changed) parts.Add($"막힘({ck}) 뒤 풀릴 만한 변화가 있음({why}) — 다시 해 볼 만함");
            else parts.Add($"막힘({ck})은 오래돼 지금은 모름({why}{rep})");
            if (bl.Count > 1) parts.Add($"마지막 완료 뒤 막힘 {bl.Count}번");
        }
        if (superseded > 0) parts.Add($"막힘 {superseded}번 뒤에 완료함");
        var withDowns = win.Where(r => r.downs > 0).ToList();
        if (withDowns.Count > 0)
        {
            int downs = withDowns.Sum(r => r.downs);
            var lastD = withDowns[withDowns.Count - 1];
            var fd = ExpResolve.FightDiff(lastD.cDown ?? lastD.EndCond, now);
            parts.Add($"이 목표 중 쓰러짐 {downs}번({Counts(win.SelectMany(r => r.downBy))}{(lastD.area != "" ? ", " + lastD.area : "")}; 마지막 쓰러짐 때와 {(fd.Count == 0 ? "무기·방어구·체력 같음" : "달라짐: " + string.Join(", ", fd))})");
        }
        var dsecs = win.Where(r => r.outcome == "completed").Select(r => r.secs).OrderBy(x => x).ToList();
        if (dsecs.Count > 0) parts.Add($"완료 때 보통 {Dur(dsecs[dsecs.Count / 2])}");
        var last = all[all.Count - 1];
        parts.Add($"마지막 {Ago(playNow, last.play1)} {OutcomeKo(last.outcome)}{(last.area != "" ? $"({last.area}에서)" : "")}");
        string note = ExpRun.Trim(string.Join("; ", parts), 320);
        return $"{{\"key\":{ExpJson.S(all[0].key)},\"runs\":{win.Count},\"done\":{done},\"blocked\":{blocked},\"other\":{other},\"block\":{ExpJson.S(VerdictKo(verdict))},\"note\":{ExpJson.S(note)}}}";
    }

    // 1. the goal just done or running, 2. goals that can be chosen now whose block still stands, 3. ... worth a retry,
    // 4. goals blocked or with downs in the last 3 play hours, 5. ... with a block too old to judge, 6. the rest, latest first
    public static List<string> PickKeys(ExpBook book, string lastKey, IList<string> candidates, double playNow, ExpCond now, Func<string, int> oreNow, Func<string, (int, int)> craftNow)
    {
        var keys = new List<string>();
        void Add(string k) { if (!string.IsNullOrEmpty(k) && keys.Count < MaxKeys && book.ByKey.ContainsKey(k) && !keys.Contains(k)) keys.Add(k); }
        Add(lastKey);
        var known = candidates == null ? new List<string>() : candidates.Where(k => book.ByKey.ContainsKey(k)).Distinct().OrderByDescending(k => book.ByKey[k][book.ByKey[k].Count - 1].play1).ToList();
        var state = known.ToDictionary(k => k, k => BlockState(book.ByKey[k], now, playNow, oreNow, craftNow));
        foreach (var k in known) if (state[k] == ExpVerdict.Standing) Add(k);
        foreach (var k in known) if (state[k] == ExpVerdict.Changed) Add(k);
        for (int i = book.Rows.Count - 1; i >= 0 && playNow - book.Rows[i].play1 <= DangerPlaySec; i--)
            if (book.Rows[i].outcome == "blocked" || book.Rows[i].downs > 0) Add(book.Rows[i].key);
        foreach (var k in known) if (state[k] == ExpVerdict.Stale) Add(k);
        foreach (var k in known) Add(k);
        return keys;
    }

    public static string Build(ExpBook book, ExpCond now, double playNow, string lastKey, IList<string> candidates, Func<string, int> oreNow = null, ExpRun open = null, Func<string, (int, int)> craftNow = null)
    {
        var recent = new List<string>();
        for (int i = book.Rows.Count - 1; i >= 0 && recent.Count < RecentN; i--) recent.Add(ExpJson.S(Line(book.Rows[i], playNow)));
        var keys = PickKeys(book, lastKey, candidates, playNow, now, oreNow, craftNow);
        var parts = keys.Select(k => KeyPart(book.ByKey[k], now, playNow, oreNow, craftNow)).ToList();
        string running = open == null ? "null" : ExpJson.S($"{open.key}{(open.count > 0 ? "×" + open.count : "")}({(open.source == "host" ? "호스트" : "LLM")}) {Dur(Math.Max(0, playNow - open.play0))}째{(open.downs > 0 ? $", 그동안 쓰러짐 {open.downs}({string.Join("·", open.downBy.Distinct().Take(3))})" : "")}");
        string Make() => $"{{\"now\":{ExpJson.S(now.Short())},\"play_hours\":{ExpJson.N(Math.Round(playNow / 360.0) / 10.0)},\"goal_runs\":{book.Rows.Count},\"running\":{running},\"recent\":[{string.Join(",", recent)}],\"by_goal\":[{string.Join(",", parts)}]}}";
        string s = Make();
        while (s.Length > MaxChars && (parts.Count > 1 || recent.Count > 2))
        {
            if (parts.Count > 1) parts.RemoveAt(parts.Count - 1); else recent.RemoveAt(recent.Count - 1);
            s = Make();
        }
        return s;
    }
}

// Writes the files and keeps the goal-run bookkeeping for one character in one world.
public sealed class ExpRecorder
{
    public readonly string Dir, World, Char;
    public string Sess { get; private set; } = "";
    public bool Loaded { get; private set; }    // false: Load did not finish; nothing may be written
    public double Play;                         // play seconds (only while this character is in this world)
    public readonly ExpBook Book = new ExpBook();
    public ExpRun Open;                         // the run going on now
    public object OpenToken;                    // the AIGoal object of that run (not saved)
    public readonly HashSet<string> Items = new HashSet<string>(), Bosses = new HashSet<string>(), Areas = new HashSet<string>();
    public Func<long> Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public Action<string> Log = _ => { };
    public long RotateBytes = 8L << 20;
    public long NextId => _next;
    public int Rotations;
    public string LoadNote = "";                // what Load had to repair
    private long _next = 1;
    private int _appends;

    public ExpRecorder(string dir, string world, string character) { Dir = dir; World = world; Char = character; }
    private string P(string n) => Path.Combine(Dir, n);
    public string EventsPath => P("events.jsonl");
    public string GoalsPath => P("goals.jsonl");
    public string StatePath => P("exp_state.json");
    public string StateBak => P("exp_state.json.bak");

    private IEnumerable<string> EventFiles() =>
        Directory.Exists(Dir) ? Directory.GetFiles(Dir, "events*.jsonl").OrderBy(f => Path.GetFileName(f) == "events.jsonl" ? "~" : Path.GetFileName(f), StringComparer.Ordinal) : Enumerable.Empty<string>();

    private static string Tail(string path, int bytes)
    {
        if (!File.Exists(path)) return "";
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            long start = Math.Max(0, fs.Length - bytes);
            fs.Seek(start, SeekOrigin.Begin);
            var buf = new byte[fs.Length - start];
            int n = 0; while (n < buf.Length) { int k = fs.Read(buf, n, buf.Length - n); if (k <= 0) break; n += k; }
            string s = Encoding.UTF8.GetString(buf, 0, n);
            if (start > 0) { int nl = s.IndexOf('\n'); s = nl >= 0 ? s.Substring(nl + 1) : ""; }
            return s;
        }
    }

    private static Dictionary<string, object> ReadState(string path)
    {
        if (!File.Exists(path)) return null;
        return ExpJson.Parse(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;   // IO errors go up
    }

    private static IEnumerable<Dictionary<string, object>> Lines(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Length < 2) continue;
            if (ExpJson.Parse(line) is Dictionary<string, object> e) yield return e;
        }
    }

    // At join (or a world change). Reads everything first; any read error leaves the recorder not Loaded (the caller
    // turns the memory off for this join) so nothing half-read is ever written on top. Repairs:
    // - exp_state.json unreadable -> exp_state.json.bak; play seconds and the next id are never below what the newest
    //   events show; lost seen-lists and a lost open run are rebuilt from the events
    // - goals.jsonl missing or with bad lines -> rebuilt from every goal_end; goal_end events in the newest events that
    //   goals.jsonl lacks are added
    // - a run left open by a session that ended without its end -> closed once as "unknown"
    public void Load(ExpCond staleCond = null)
    {
        Directory.CreateDirectory(Dir);
        var notes = new List<string>();
        ExpRun open = null;
        long next = 1; double play = 0;
        var items = new HashSet<string>(); var bosses = new HashSet<string>(); var areas = new HashSet<string>();
        var st = ReadState(StatePath);
        if (st == null && File.Exists(StatePath)) { notes.Add("exp_state.json unreadable"); st = ReadState(StateBak); if (st != null) notes.Add("used exp_state.json.bak"); }
        else if (st == null && File.Exists(StateBak)) { st = ReadState(StateBak); if (st != null) notes.Add("exp_state.json missing, used .bak"); }
        bool stateOk = st != null;
        if (stateOk)
        {
            next = ExpJson.GetL(st, "next", 1); play = ExpJson.GetD(st, "play");
            open = ExpRun.From(ExpJson.GetO(st, "open"));
            foreach (var x in ExpJson.GetSA(st, "items")) items.Add(x);
            foreach (var x in ExpJson.GetSA(st, "bosses")) bosses.Add(x);
            foreach (var x in ExpJson.GetSA(st, "areas")) areas.Add(x);
        }
        var files = EventFiles().ToList();
        var tailEv = new List<Dictionary<string, object>>();
        foreach (var f in files.Skip(Math.Max(0, files.Count - 2))) tailEv.AddRange(Lines(Tail(f, 256 * 1024)));
        foreach (var e in tailEv) { next = Math.Max(next, ExpJson.GetL(e, "id") + 1); play = Math.Max(play, ExpJson.GetD(e, "play")); }
        var book = new ExpBook();
        int bad = 0;
        if (File.Exists(GoalsPath))
            foreach (var line in File.ReadLines(GoalsPath, Encoding.UTF8))
            {
                if (line.Length < 2) continue;
                var r = ExpRun.From(ExpJson.Parse(line) as Dictionary<string, object>);
                if (r == null) bad++; else book.Add(r);
            }
        bool rebuild = files.Count > 0 && (!File.Exists(GoalsPath) || bad > 0);
        if (rebuild) { int n = AddEnds(book, files.SelectMany(f => Lines(File.ReadAllText(f, Encoding.UTF8)))); notes.Add($"goals.jsonl {(bad > 0 ? bad + " bad line(s)" : "missing")}: rebuilt, {n} run(s) from {files.Count} event file(s)"); }
        int fromTail = AddEnds(book, tailEv);
        if (fromTail > 0) { rebuild = true; notes.Add($"{fromTail} ended run(s) found only in the events"); }
        if (!stateOk && files.Count > 0)
        {
            // seen-lists and the open run from the events
            Dictionary<string, object> lastStart = null;
            foreach (var e in files.SelectMany(f => Lines(File.ReadAllText(f, Encoding.UTF8))))
            {
                var d = ExpJson.GetO(e, "d"); string k = ExpJson.GetS(e, "kind");
                if (k == "item_first") items.Add(ExpJson.GetS(d, "item"));
                else if (k == "boss_seen") bosses.Add(ExpJson.GetS(d, "boss"));
                else if (k == "area_enter") areas.Add(ExpJson.GetS(d, "area"));
                else if (k == "goal_start") lastStart = e;
            }
            if (lastStart != null && !book.Has(ExpJson.GetS(lastStart, "run"))) open = FromStart(lastStart);
            notes.Add("state rebuilt from the events");
        }
        // everything read: commit
        _next = next; Play = play;
        foreach (var x in items) Items.Add(x); foreach (var x in bosses) Bosses.Add(x); foreach (var x in areas) Areas.Add(x);
        book.Sort();
        foreach (var r in book.Rows) Book.Add(r);
        Sess = Guid.NewGuid().ToString("N").Substring(0, 8);
        Loaded = true;
        if (rebuild) WriteGoals();
        if (open != null && !Book.Has(open.run))
        {
            Open = open; OpenToken = null;
            End("unknown", "", "세션이 끝나 목표의 끝을 보지 못함", -1, staleCond ?? open.c0);
        }
        LoadNote = string.Join("; ", notes);
        if (LoadNote != "") Log("EXP: load repaired: " + LoadNote);
        Event("session_start", null, null, "game", $"{{\"goal_runs\":{Book.Rows.Count},\"play\":{ExpJson.N(Play)},\"repaired\":{ExpJson.S(LoadNote)}}}");
        SaveState();
    }

    private static ExpRun FromStart(Dictionary<string, object> e)
    {
        var d = ExpJson.GetO(e, "d");
        string key = ExpJson.GetS(d, "key"), item = "";
        foreach (var x in ExpJson.GetSA(e, "ents")) if (x.StartsWith("item:")) item = x.Substring(5);
        string raw = ExpJson.GetS(d, "source");
        return new ExpRun
        {
            run = ExpJson.GetS(e, "run"), sess = ExpJson.GetS(e, "sess"), key = key, kind = key.Split(':')[0], item = item,
            count = ExpJson.GetI(d, "count"), start = ExpJson.GetI(d, "start", -1), source = raw.StartsWith("llm") ? "llm" : "host", sourceRaw = raw,
            c0 = ExpCond.From(ExpJson.GetO(d, "c")), t0 = ExpJson.GetL(e, "t"), play0 = ExpJson.GetD(e, "play"), ev0 = ExpJson.GetL(e, "id"),
        };
    }

    private static int AddEnds(ExpBook book, IEnumerable<Dictionary<string, object>> events)
    {
        int n = 0;
        foreach (var e in events)
            if (ExpJson.GetS(e, "kind") == "goal_end" && book.Add(ExpRun.From(ExpJson.GetO(e, "d")))) n++;
        return n;
    }

    private void WriteGoals()
    {
        var sb = new StringBuilder();
        foreach (var r in Book.Rows) sb.Append(r.ToJson()).Append('\n');
        string tmp = GoalsPath + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
        Swap(tmp, GoalsPath, null);
    }

    // tmp -> path in one step where the runtime allows it (File.Replace), else delete+move (the old file stays as .bak)
    private static void Swap(string tmp, string path, string bak)
    {
        if (!File.Exists(path)) { File.Move(tmp, path); return; }
        try { File.Replace(tmp, path, bak, true); }
        catch (Exception)
        {
            if (bak != null) File.Copy(path, bak, true);
            File.Delete(path);
            File.Move(tmp, path);
        }
    }

    public long Event(string kind, string run, IEnumerable<string> ents, string src, string dJson)
    {
        if (!Loaded) throw new InvalidOperationException("experience memory not loaded");
        long id = _next++;
        string line = $"{{\"id\":{id},\"sess\":{ExpJson.S(Sess)},\"t\":{Clock()},\"play\":{ExpJson.N(Math.Round(Play, 1))},\"world\":{ExpJson.S(World)},\"char\":{ExpJson.S(Char)},\"kind\":{ExpJson.S(kind)},\"run\":{(run == null ? "null" : ExpJson.S(run))},\"ents\":{ExpJson.Arr(ents)},\"src\":{ExpJson.S(src ?? "game")},\"d\":{(string.IsNullOrEmpty(dJson) ? "{}" : dJson)}}}";
        File.AppendAllText(EventsPath, line + "\n", new UTF8Encoding(false));
        if (++_appends % 200 == 0)
        {
            var fi = new FileInfo(EventsPath);
            if (fi.Exists && fi.Length > RotateBytes)
            {
                string to = P($"events-{DateTime.UtcNow:yyyyMMddHHmmss}-{id}.jsonl");
                File.Move(EventsPath, to);
                Rotations++;
                Log($"EXP: events.jsonl rotated to {Path.GetFileName(to)} ({fi.Length / 1024} KB)");
            }
        }
        return id;
    }

    public static List<string> Ents(ExpRun r, ExpCond c)
    {
        var l = new List<string>();
        if (!string.IsNullOrEmpty(r?.item)) l.Add("item:" + r.item);
        if (c != null && c.cell != "") l.Add("cell:" + c.cell);
        if (c != null && c.area != "") l.Add("area:" + c.area);
        return l;
    }

    // A goal was adopted. prevToken is the goal object that was running. The same goal set again (same key) keeps the
    // run going; another goal ends the open run as replaced; when the open run is not prevToken's (e.g. after a reset)
    // it ends as unknown.
    public ExpRun GoalStart(object token, object prevToken, string key, string kind, string item, int count, string sourceRaw, ExpCond c, int startCount = -1)
    {
        string src = (sourceRaw ?? "").StartsWith("llm") ? "llm" : "host";
        if (Open != null)
        {
            bool fromOpen = prevToken != null && ReferenceEquals(prevToken, OpenToken);
            if (fromOpen && Open.key == key)
            {
                Open.resets++; Open.count = count; OpenToken = token;
                if (src != Open.source) { Open.source = src; Open.sourceRaw = sourceRaw ?? ""; }   // the latest setter (a host goal stays a host goal)
                Event("goal_continue", Open.run, Ents(Open, c), src, $"{{\"key\":{ExpJson.S(key)},\"count\":{count},\"source\":{ExpJson.S(sourceRaw)}}}");
                SaveState();
                return Open;
            }
            if (fromOpen) End("replaced", "", $"새 목표({src}: {key})", -1, c);
            else End("unknown", "", "목표 추적이 끊김(초기화 뒤 새 목표)", -1, c);
        }
        var r = new ExpRun { sess = Sess, key = key, kind = kind, item = item ?? "", count = count, start = startCount, source = src, sourceRaw = sourceRaw ?? "", c0 = c ?? new ExpCond(), t0 = Clock(), play0 = Play };
        long id = _next;   // the goal_start event's id names the run
        r.run = $"{Sess}.{id}";
        r.ev0 = Event("goal_start", r.run, Ents(r, c), src == "llm" ? "llm" : "host",
            $"{{\"key\":{ExpJson.S(key)},\"count\":{count},\"start\":{startCount},\"source\":{ExpJson.S(sourceRaw)},\"c\":{(c ?? new ExpCond()).ToJson()}}}");
        Open = r; OpenToken = token;
        SaveState();
        return r;
    }

    // AIBrain.ClearGoal: the goal object and its reason. Another object than the open run's goal, or nothing open: no-op.
    public bool GoalEnd(object token, string why, int got, ExpCond c, string workArea = null)
    {
        if (Open == null || (token != null && OpenToken != null && !ReferenceEquals(token, OpenToken))) return false;
        ExpRun.Classify(why, out string outcome, out string cls, out string cause);
        End(outcome, cls, cause, got, c, workArea);
        return true;
    }

    // The same, with the result the ending place stated (refactor S2b): a stated outcome / cls (null = not stated)
    // decides. A cls left null (gather, farm and cook stalls: the sentence's content decides, review F6) and the cause
    // still come from the classified sentence. With compare (the test switch), a stated value that differs from the
    // sentence's is logged as OUTCOME-MISMATCH (kept for the live runs, review F7).
    public bool GoalEnd(object token, string why, string statedOutcome, string statedCls, int got, ExpCond c, string workArea = null, bool compare = false)
    {
        if (Open == null || (token != null && OpenToken != null && !ReferenceEquals(token, OpenToken))) return false;
        ExpRun.Classify(why, out string outcome, out string cls, out string cause);
        if (compare && ((statedOutcome != null && statedOutcome != outcome) || (statedCls != null && statedCls != cls)))
            Log($"OUTCOME-MISMATCH goal text={outcome}/{cls} code={statedOutcome}/{statedCls ?? "-"} why='{ExpRun.Trim(why, 160)}'");
        End(statedOutcome ?? outcome, statedCls ?? cls, cause, got, c, workArea);
        return true;
    }

    public void End(string outcome, string cls, string cause, int got, ExpCond c, string workArea = null)
    {
        var r = Open;
        if (r == null) return;
        Open = null; OpenToken = null;
        r.outcome = outcome; r.cls = cls ?? ""; r.cause = cause ?? ""; r.got = got; r.c1 = c ?? new ExpCond();
        r.area = !string.IsNullOrEmpty(workArea) ? workArea : r.area != "" ? r.area : r.c1.area != "" ? r.c1.area : r.c0.area;
        r.t1 = Clock(); r.play1 = Play; r.secs = Math.Max(0, Play - r.play0);
        r.ev1 = _next;
        Event("goal_end", r.run, Ents(r, c), "game", r.ToJson());
        if (Book.Add(r)) File.AppendAllText(GoalsPath, r.ToJson() + "\n", new UTF8Encoding(false));
        SaveState();
    }

    // A down: an event inside the open run (the run keeps going).
    public long Down(IList<string> enemies, string cause, ExpCond c)
    {
        string first = enemies != null && enemies.Count > 0 ? enemies[0] : "";
        if (Open != null) { Open.downs++; Open.downBy.Add(first != "" ? first : "원인 모름"); Open.cDown = c; }
        var ents = new List<string>();
        foreach (var e in enemies ?? new List<string>()) ents.Add("enemy:" + e);
        if (c != null && c.cell != "") ents.Add("cell:" + c.cell);
        if (c != null && c.area != "") ents.Add("area:" + c.area);
        long id = Event("down", Open?.run, ents, "game", $"{{\"cause\":{ExpJson.S(cause)},\"enemies\":{ExpJson.Arr(enemies)},\"c\":{(c ?? new ExpCond()).ToJson()}}}");
        SaveState();
        return id;
    }

    // Written to a temporary file and swapped in; the previous state stays as exp_state.json.bak.
    public void SaveState()
    {
        if (!Loaded) return;
        string s = $"{{\"v\":1,\"next\":{_next},\"play\":{ExpJson.N(Math.Round(Play, 1))},\"open\":{(Open == null ? "null" : Open.ToJson())},\"items\":{ExpJson.Arr(Items)},\"bosses\":{ExpJson.Arr(Bosses)},\"areas\":{ExpJson.Arr(Areas)}}}";
        string tmp = StatePath + ".tmp";
        File.WriteAllText(tmp, s, new UTF8Encoding(false));
        Swap(tmp, StatePath, StateBak);
    }

    public string Report(ExpCond now, string lastKey, IList<string> candidates, Func<string, int> oreNow = null, Func<string, (int, int)> craftNow = null)
    {
        now = now ?? new ExpCond();
        return ExpReport.Build(Book, now, Play, lastKey, candidates, oreNow, Open, craftNow);
    }
}
