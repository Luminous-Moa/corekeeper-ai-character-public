// Historical L1 inputs/outputs remain unchanged. This replay projects the new no-count-cut / all-qualified-goal
// contract onto fields the old one-goal/14-cut execution actually recorded. Newly retained goal-side physical
// feasibility is not inferred from the old log; explicit current-contract cases below check it independently.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class AssemblerReplayTests
{
    const int HistoricalLimit = 14;
    static readonly string[] ListNames = { "cont", "follow", "enemy", "combat", "craft", "life", "mine", "dig", "remember", "item", "explore", "digExplore", "other", "brute", "door", "edge" };   // brute: combat stage 2 (absent in the L1 records = empty)

    static List<object> Arr(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
    static bool B(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;

    // Combat (an enemy within combat range 8, the setting the L1 run had) as the build saw it: the record's own flag
    // (combat stage 2 records) or, for the L1 records, the nearest enemy's distance as written ({dist:F1}).
    public static bool Combat(Dictionary<string, object> inp)
    {
        if (inp.TryGetValue("combat", out var v) && v is bool b) return b;
        string d = ExpJson.GetS(inp, "enemy0Dist");
        return !string.IsNullOrEmpty(d) && float.Parse(d, CultureInfo.InvariantCulture) <= 8f;
    }

    // Old ASM logs have no role field. Decode only historical fixtures, never production candidates.
    static EdgeCandidateRole LegacyEdgeRole(string kind, string desc)
    {
        if (kind != "place") return EdgeCandidateRole.None;
        string d = desc ?? "";
        return d.StartsWith("기지 문 다시 달기") ? EdgeCandidateRole.DoorRepair
            : d.StartsWith("기지 문 달기") ? EdgeCandidateRole.DoorPlace
            : d.StartsWith("기지 안 횃불") ? EdgeCandidateRole.TorchPlace : EdgeCandidateRole.None;
    }
    // Replays one IN/OUT pair; returns null when equal, else the first difference. limit: the cut given to Finish
    // (14 = the old code; LimitFor(...) now returns no cut). An old build whose
    // list was cut by the old code is compared as "the old list is a prefix of the new one (before wait)".
    public static string Replay(Dictionary<string, object> inp, Dictionary<string, object> outp) => Replay(inp, outp, AICandidateAssembler.Limit, out _);
    public static string Replay(Dictionary<string, object> inp, Dictionary<string, object> outp, int limit, out int longer)
    {
        longer = 0;
        var byId = new Dictionary<int, AsmCand>();
        AsmCand C(Dictionary<string, object> j)
        {
            int id = ExpJson.GetI(j, "id");
            if (!byId.TryGetValue(id, out var c))
                byId[id] = c = new AsmCand { id = id, kind = ExpJson.GetS(j, "kind"), key = ExpJson.GetS(j, "key"), desc = ExpJson.GetS(j, "desc"), edgeRole = LegacyEdgeRole(ExpJson.GetS(j, "kind"), ExpJson.GetS(j, "desc")), progress = B(j, "progress"), comesByItself = B(j, "comesByItself") };
            return c;
        }
        var lists = ExpJson.GetO(inp, "lists");
        var L = new Dictionary<string, List<AsmCand>>();
        foreach (var n in ListNames) L[n] = Arr(lists, n).Select(x => C((Dictionary<string, object>)x)).ToList();
        string cur = ExpJson.GetS(inp, "cur");
        var a = new AsmInput
        {
            goal = (GoalKind)Enum.Parse(typeof(GoalKind), ExpJson.GetS(inp, "goal")),
            free = B(inp, "free"), closeFight = B(inp, "closeFight"), closeFightOff = B(inp, "closeFightOff"),
            // the old test was `cur is AttackSkill || cur is KeepAwaySkill || cur is RetreatSkill || cur is EatSkill`; none has a subclass
            curCombat = cur == "AttackSkill" || cur == "KeepAwaySkill" || cur == "RetreatSkill" || cur == "EatSkill",
            curKey = ExpJson.GetS(inp, "curKey"),
            near6 = ExpJson.GetI(inp, "near6"), enemy0 = ExpJson.GetS(inp, "enemy0"), enemy0Dist = ExpJson.GetS(inp, "enemy0Dist"),
            lCont = L["cont"], lFollow = L["follow"], lEnemy = L["enemy"], lCombat = L["combat"], lCraft = L["craft"], lLife = L["life"],
            lMine = L["mine"], lDig = L["dig"], lRemember = L["remember"], lItem = L["item"], lExplore = L["explore"], lDigExplore = L["digExplore"], lOther = L["other"],
            lBrute = L["brute"], lDoor = L["door"], lEdge = L["edge"], combat = Combat(inp),
            foodPicks = new HashSet<AsmCand>(Arr(inp, "foodPicks").Select(x => byId[(int)(double)x])),
        };
        var expectedProjection = Arr(outp, "list").Cast<Dictionary<string, object>>().ToList();
        var bodyRec = ExpJson.GetO(outp, "bodyCan"); var doRec = ExpJson.GetO(outp, "doable");
        var askedBody = new List<int>(); var askedDo = new List<int>();
        string missing = null;
        bool Rec(Dictionary<string, object> rec, List<int> asked, AsmCand c)
        {
            // A valid current execution is now retained even when the old close-fight set hid it.
            if (c.kind == "continue" && !rec.ContainsKey(c.id.ToString())) return true;
            // Old one-goal close-fight records never evaluated the extra goal-side actions now retained. Their
            // physical eligibility is covered by current-contract tests below; this projection compares old fields only.
            if (c.progress && c.desc.StartsWith("싸움을 끊고") && !rec.ContainsKey(c.id.ToString())) return true;
            // Historical records did not evaluate bodies of actions hidden by the former policy.
            // Their actual eligibility is checked by current-contract tests, not invented old evidence.
            if (!rec.ContainsKey(c.id.ToString()) && (ReferenceEquals(rec, bodyRec) || !c.progress)) return true;
            asked.Add(c.id);
            if (rec.TryGetValue(c.id.ToString(), out var v) && v is bool b) return b;
            missing = missing ?? $"asked for a value the old code did not evaluate: id {c.id}";
            return true;
        }
        var list = AICandidateAssembler.Select(a, c => Rec(doRec, askedDo, c), c => Rec(bodyRec, askedBody, c), out string setLog, out bool closeSet, out int noBody);
        if (missing != null) return missing;
        string Ids(IEnumerable<int> s) => string.Join(",", s.OrderBy(x => x));
        if (Ids(askedDo) != Ids(doRec.Keys.Select(int.Parse)) || askedDo.Count != doRec.Count) return $"Doable asked for [{Ids(askedDo)}], the old code evaluated [{Ids(doRec.Keys.Select(int.Parse))}]";
        if (Ids(askedBody) != Ids(bodyRec.Keys.Select(int.Parse)) || askedBody.Count != bodyRec.Count) return $"BodyCan asked for [{Ids(askedBody)}], the old code evaluated [{Ids(bodyRec.Keys.Select(int.Parse))}]";
        if (setLog != ExpJson.GetS(outp, "setLog") && !(setLog.StartsWith(" set=close-fight") && ExpJson.GetS(outp, "setLog").StartsWith(" set=close-fight"))) return $"setLog '{setLog}' vs '{ExpJson.GetS(outp, "setLog")}'";
        if (noBody != ExpJson.GetI(outp, "noBody")) return $"noBody {noBody} vs {ExpJson.GetI(outp, "noBody")}";

        // Compare only the old output surface; newly eligible alternatives are covered separately below.
        list.RemoveAll(c => !expectedProjection.Exists(e => ExpJson.GetI(e,"id") == c.id && ExpJson.GetS(e,"key") == c.key)
            && !Arr(inp,"fails").Cast<Dictionary<string,object>>().Any(f => ExpJson.GetS(f,"key") == c.key)
            && !(c.progress && c.desc.StartsWith("싸움을 끊고")) && c.kind != "continue");
        var fails = new Dictionary<string, (int count, float last, string reason)>();
        foreach (Dictionary<string, object> f in Arr(inp, "fails")) fails[ExpJson.GetS(f, "key")] = (ExpJson.GetI(f, "count"), (float)ExpJson.GetD(f, "last"), ExpJson.GetS(f, "reason"));
        var left = new List<(string key, float t)>();
        foreach (Dictionary<string, object> x in Arr(inp, "left")) left.Add((ExpJson.GetS(x, "key"), (float)ExpJson.GetD(x, "t")));
        // Failure policy changed intentionally. Verify the production policy keeps every viable candidate,
        // then normalize only the historical failure presentation to replay the unchanged assembly ordering.
        var beforeFailureMark = list.ConvertAll(c => new AsmCand { id = c.id, kind = c.kind, key = c.key, desc = c.desc, value = c.value, edgeRole = c.edgeRole, progress = c.progress, comesByItself = c.comesByItself, edge = c.edge, keep = c.keep, detour = c.detour });
        AICandidateAssembler.Mark(list, (float)ExpJson.GetD(outp, "tnow"), fails, (float)ExpJson.GetD(inp, "failMemory"), left);
        if (list.Count != beforeFailureMark.Count || list.Where((c, i) => c.key != beforeFailureMark[i].key).Any())
            return "failure history revoked a candidate";
        foreach (var c in list)
            if (c.kind != "continue" && fails.TryGetValue(c.key, out var fail) && fail.count >= 2 && (float)ExpJson.GetD(outp, "tnow") - fail.last <= (float)ExpJson.GetD(inp, "failMemory") && !c.desc.Contains(fail.reason))
                return "retry candidate lost its failure reason";
        list = beforeFailureMark;
        var skipped = HistoricalFailurePresentation(list, (float)ExpJson.GetD(outp, "tnow"), fails, (float)ExpJson.GetD(inp, "failMemory"), left);
        string skippedNote = skipped.Count == 0 ? "" : string.Join(", ", skipped);
        if (skippedNote != ExpJson.GetS(outp, "skipped")) return $"skipped '{skippedNote}' vs '{ExpJson.GetS(outp, "skipped")}'";

        var exp = Arr(outp, "list").Cast<Dictionary<string, object>>().ToList();
        var w = exp.Count > 0 ? exp[exp.Count - 1] : null;
        if (w == null || ExpJson.GetS(w, "kind") != "wait") return "the old list does not end with wait";
        var wait = new AsmCand { id = ExpJson.GetI(w, "id"), kind = "wait", key = "wait", desc = ExpJson.GetS(w, "desc") };   // the wait text comes from the settings (not an assembly input)
        AICandidateAssembler.Finish(list, wait, limit);
        // Preserve the old non-current ordering while explicitly checking the new continue contract below.
        list.RemoveAll(c => c.kind == "continue" && !exp.Exists(e => ExpJson.GetI(e, "id") == c.id));
        list.RemoveAll(c => c.progress && c.desc.StartsWith("싸움을 끊고") && !exp.Exists(e => ExpJson.GetI(e, "id") == c.id));
        if (list.Count > exp.Count && exp.Count == HistoricalLimit + 1)
        {
            // the old code cut this list at 14: the first 14 are the same, the new one goes on, wait is last
            if (list[list.Count - 1].kind != "wait") return "the new list does not end with wait";
            longer = list.Count - exp.Count;
            var head = list.GetRange(0, HistoricalLimit); head.Add(list[list.Count - 1]);
            list = head;
        }
        if (list.Count != exp.Count) return $"list length {list.Count} vs {exp.Count}";
        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i]; var e = exp[i];
            if (g.id != ExpJson.GetI(e, "id") || g.kind != ExpJson.GetS(e, "kind") || g.key != ExpJson.GetS(e, "key") || g.value != ExpJson.GetS(e, "value") || g.desc != ExpJson.GetS(e, "desc"))
                return $"item {i}: got #{g.id} {g.kind} {g.key} {g.value} '{g.desc}' vs #{ExpJson.GetI(e, "id")} {ExpJson.GetS(e, "kind")} {ExpJson.GetS(e, "key")} {ExpJson.GetS(e, "value")} '{ExpJson.GetS(e, "desc")}'";
        }
        return null;
    }

    static void ContinuingNonGoalAction()
    {
        var current = new AsmCand { id = 1, kind = "continue", key = "edgerepair:2", desc = "계속 보수", progress = false };
        var enemy = new AsmCand { id = 2, kind = "attack", key = "attack:3", desc = "공격" };
        AsmInput Input() => new AsmInput
        {
            goal = GoalKind.Gather, closeFight = true, near6 = 1, enemy0 = "Slime", enemy0Dist = "2.0",
            curKey = current.key, lCont = new List<AsmCand> { current }, lEnemy = new List<AsmCand> { enemy },
            lFollow = new List<AsmCand>(), lCombat = new List<AsmCand>(), lCraft = new List<AsmCand>(),
            lLife = new List<AsmCand>(), lMine = new List<AsmCand>(), lDig = new List<AsmCand>(),
            lRemember = new List<AsmCand>(), lItem = new List<AsmCand>(), lExplore = new List<AsmCand>(),
            lDigExplore = new List<AsmCand>(), lOther = new List<AsmCand>(), foodPicks = new HashSet<AsmCand>()
        };
        var list = AICandidateAssembler.Select(Input(), c => true, c => true, out _, out _, out _);
        Program.Check(list.Contains(current) && (current.desc.Contains("주변 적") || current.desc.Contains("적 1마리")), "non-goal repair keeps an explicit continue with danger facts in close combat");
        AICandidateAssembler.Finish(list, new AsmCand { kind = "wait", key = "wait", desc = "기다림" }, 1);
        Program.Check(list.Contains(current) && !string.IsNullOrEmpty(current.value), "candidate cap preserves current continue and assigns a real model choice value");
        var unavailable = AICandidateAssembler.Select(Input(), c => true, c => c != current, out _, out _, out _);
        Program.Check(!unavailable.Contains(current), "an actually unavailable body action is not restored as a viable continue");
    }

    // Fixture adapter for the old policy only. Production Mark above is checked independently.
    static List<string> HistoricalFailurePresentation(List<AsmCand> list, float now, IDictionary<string, (int count, float last, string reason)> fails, float seconds, IList<(string key, float t)> left)
    {
        var skipped = new List<string>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            if (c.kind == "continue" || !fails.TryGetValue(c.key, out var f) || now - f.last > seconds) continue;
            if (f.count >= 2) { skipped.Add($"{c.key}({f.reason})"); list.RemoveAt(i); }
            else c.desc += $" 최근 이 대상에서 실패 1회: {f.reason}.";
        }
        foreach (var c in list)
        {
            if (c.kind == "continue") continue;
            for (int i = left.Count - 1; i >= 0; i--)
                if (left[i].key == c.key)
                {
                    if (now - left[i].t <= 3f) c.desc += $" 이 행동은 {now - left[i].t:F1}초 전에 하다가 그만둔 것(다시 고르면 처음부터).";
                    break;
                }
        }
        return skipped;
    }

    static readonly Regex Line = new Regex(@"ASM-(IN|OUT) #(\d+) (.*)$");

    static void AllCloseFightGoalChoices()
    {
        AsmCand C(string key, bool progress = true) => new AsmCand { key = key, kind = "mine", desc = key, progress = progress };
        var current = C("mine:running"); current.kind = "continue";
        var first = C("mine:first"); var second = C("mine:second"); var blocked = C("mine:blocked");
        var automatic = C("pickup:auto"); automatic.kind = "pickup"; automatic.comesByItself = true;
        var filler = C("mine:filler", false); var enemy = C("attack:1", false); enemy.kind = "attack";
        var a = new AsmInput
        {
            goal = GoalKind.Gather, closeFight = true, curKey = current.key, near6 = 1,
            lCont = new List<AsmCand> { current }, lEnemy = new List<AsmCand> { enemy },
            lMine = new List<AsmCand> { first, second, blocked, filler }, lItem = new List<AsmCand> { automatic },
            lFollow = new List<AsmCand>(), lCombat = new List<AsmCand>(), lCraft = new List<AsmCand>(),
            lLife = new List<AsmCand>(), lDig = new List<AsmCand>(), lRemember = new List<AsmCand>(),
            lExplore = new List<AsmCand>(), lDigExplore = new List<AsmCand>(), lOther = new List<AsmCand>(),
            foodPicks = new HashSet<AsmCand>()
        };
        var list = AICandidateAssembler.Select(a, c => true, c => c != blocked, out _, out bool close, out int noBody);
        Program.Check(close && list.Contains(current) && list.Contains(first) && list.Contains(second)
            && list.IndexOf(enemy) < list.IndexOf(current) && list.IndexOf(current) < list.IndexOf(first),
            "close-fight retains every qualified goal-side action after combat, with current goal action first");
        Program.Check(noBody == 1 && !list.Contains(blocked) && list.Contains(automatic) && list.Contains(filler),
            "removing the close-fight one-goal quota preserves physical eligibility while exposing optional and automatic pickups");
    }

    public static void Run()
    {
        ContinuingNonGoalAction();
        AllCloseFightGoalChoices();
        // ---- the L1 corpus (old code)
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "asm-L1.log.gz");
        if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "core-tests", "fixtures", "asm-L1.log.gz");
        var ins = new Dictionary<int, string>(); var outs = new Dictionary<int, string>();
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var rd = new StreamReader(gz, Encoding.UTF8))
        {
            string l;
            while ((l = rd.ReadLine()) != null)
            {
                var m = Line.Match(l);
                if (!m.Success) continue;
                (m.Groups[1].Value == "IN" ? ins : outs)[int.Parse(m.Groups[2].Value)] = m.Groups[3].Value;
            }
        }
        int same = 0, diff = 0, recursed = 0, incomplete = 0; var firstDiffs = new List<string>();
        int closeSets = 0, over14 = 0, withFails = 0, withLeft = 0, goalNone = 0;
        int peaceSame = 0, peaceDiff = 0, combatOk = 0, combatBad = 0, combatLonger = 0, maxLonger = 0;
        foreach (var kv in ins.OrderBy(k => k.Key))
        {
            if (!outs.TryGetValue(kv.Key, out var o)) { incomplete++; continue; }
            if (o == "recursed") { recursed++; continue; }
            var inp = ExpJson.Parse(kv.Value) as Dictionary<string, object>; var outp = ExpJson.Parse(o) as Dictionary<string, object>;
            if (inp == null || outp == null) { incomplete++; continue; }
            string r = Replay(inp, outp);
            if (r == null) same++; else { diff++; if (firstDiffs.Count < 3) firstDiffs.Add($"#{kv.Key}: {r}"); }
            // combat stage 2: the default cut (none in combat, 14 out of combat)
            bool combat = Combat(inp);
            string r2 = Replay(ExpJson.Parse(kv.Value) as Dictionary<string, object>, ExpJson.Parse(o) as Dictionary<string, object>, AICandidateAssembler.LimitFor(combat, 0), out int longer);
            if (!combat) { if (r2 == null) peaceSame++; else { peaceDiff++; if (firstDiffs.Count < 3) firstDiffs.Add($"#{kv.Key} (no cut setting, peace): {r2}"); } }
            else { if (r2 == null) { combatOk++; if (longer > 0) { combatLonger++; maxLonger = Math.Max(maxLonger, longer); } } else { combatBad++; if (firstDiffs.Count < 3) firstDiffs.Add($"#{kv.Key} (no cut setting, combat): {r2}"); } }
            if (ExpJson.GetS(outp, "setLog").Contains("close-fight")) closeSets++;
            if (Arr(outp, "list").Count == 15) over14++;
            if (Arr(inp, "fails").Count > 0) withFails++;
            if (Arr(inp, "left").Count > 0) withLeft++;
            if (ExpJson.GetS(inp, "goal") == "None") goalNone++;
        }
        Program.Check(ins.Count == 864 && recursed == 10 && incomplete == 0, $"L1 corpus: {ins.Count} builds, {recursed} recursed, {incomplete} incomplete (expected 864 / 10 / 0)");
        Program.Check(diff == 0 && same == 854, $"L1 historical projection: {same} recorded builds retain the recorded choices and fields, {diff} different{(firstDiffs.Count > 0 ? ": " + string.Join(" | ", firstDiffs) : "")}");
        Console.WriteLine($"    (covered: close-fight set {closeSets}, 15 items after the cut {over14}, failure memory {withFails}, recent switch {withLeft}, no goal {goalNone})");
        // combat stage 2 (user decision 2026-11-05): the replay above passes the old cut 14 (setting combatMaxCandidates =
        // 14 gives the old behaviour); with the default (no cut in combat) the out-of-combat builds are exactly the old
        // ones and the combat builds keep the old list as their first 14 items
        Program.Check(peaceDiff == 0 && peaceSame > 0, $"L1 corpus, default cut: {peaceSame} out-of-combat historical projections retain the old recorded fields, {peaceDiff} different");
        Program.Check(combatBad == 0 && combatOk > 0, $"L1 corpus, default cut: {combatOk} combat builds keep the old list as their head ({combatLonger} longer than the old cut, up to +{maxLonger}), {combatBad} different");
        Program.Check(AICandidateAssembler.LimitFor(true, 0) == 0 && AICandidateAssembler.LimitFor(true, 14) == 0 && AICandidateAssembler.LimitFor(false, 0) == 0 && AICandidateAssembler.LimitFor(false, 30) == 0, "LimitFor: old combat and peace candidate settings no longer cut choices");
        {
            var l = new List<AsmCand>(); for (int i = 0; i < 25; i++) l.Add(new AsmCand { id = i + 1, kind = "step", key = "step:x:" + i, desc = "d" });
            var w = new AsmCand { id = 99, kind = "wait", key = "wait", desc = "w" };
            var l2 = new List<AsmCand>(l);
            AICandidateAssembler.Finish(l, w, 0); AICandidateAssembler.Finish(l2, w, 14);
            Program.Check(l.Count == 26 && l[25].kind == "wait" && l2.Count == 26 && l2[25].kind == "wait"
                && l.Select(c => c.key).SequenceEqual(l2.Select(c => c.key)) && l2.Select(c => c.value).Distinct().Count() == 26,
                $"Finish: all 25 choices plus wait survive even a legacy limit14 argument ({l.Count}/{l2.Count}), with distinct model values");
        }

        // ---- hand records: BodyCan / Doable false (never in L1). Expected OUT worked out from the old code:
        // H1 gather, no close fight: list = cont, follow, enemy, combat, craft, life, then mine, dig, remember, item, explore,
        //    digExplore, other = [4 attack, 1 mine, 2 dig, 3 pickup]; RemoveAll(!BodyCan) with 1, 2 false (no pick, no free
        //    hand slot) drops 2 -> noBody 2, [4, 3]; no failures; + wait; values KeyLabel(key).
        Hand("H1 BodyCan false drops mine and dig",
            "{\"b\":1,\"depth\":0,\"goal\":\"Gather\",\"free\":false,\"closeFight\":false,\"closeFightOff\":false,\"cur\":\"\",\"curKey\":\"\",\"prefetch\":false,\"near6\":0,\"enemy0\":\"\",\"enemy0Dist\":\"\",\"canMine\":false,\"canFight\":true,\"noMine\":\"곡괭이 없음\",\"noFight\":\"\",\"tnow0\":100,\"failMemory\":30,"
            + "\"lists\":{\"cont\":[],\"follow\":[],\"enemy\":[" + J(4, "attack", "attack:100", "공격 A") + "],\"combat\":[],\"craft\":[],\"life\":[],\"mine\":[" + J(1, "mine", "mine:3,4", "캐기 B", true) + "],\"dig\":[" + J(2, "dig", "dig:5,6", "파기 C", true) + "],\"remember\":[],\"item\":[" + J(3, "pickup", "pickup:7,8", "줍기 D", true) + "],\"explore\":[],\"digExplore\":[],\"other\":[]},\"foodPicks\":[],\"fails\":[],\"left\":[]}",
            "{\"b\":1,\"tnow\":100,\"setLog\":\"\",\"skipped\":\"\",\"noBody\":2,\"blockNote\":\"\",\"bodyCan\":{\"4\":true,\"1\":false,\"2\":false,\"3\":true},\"doable\":{},"
            + "\"list\":[" + O(4, "attack", "attack:100", "attack_100", "공격 A") + "," + O(3, "pickup", "pickup:7,8", "pickup_7_8", "줍기 D") + "," + O(5, "wait", "wait", "wait", "기다리기 — 3초") + "]}");
        // H2 gather, close fight: goalSide = cont, craft, mine [3], dig, remember, item not by itself [4], explore,
        //    digExplore, follow, other; RemoveAll(!progress || !Doable) asks Doable(3) = false (left out), Doable(4) = true
        //    -> keep = 4 (no continue); list = enemy [1], combat [2], keep [4] with the break-off text (near6 2, Slime 3.2);
        //    set=close-fight; BodyCan 1, 2, 4 true. Marks at tnow 105: attack:50 failed once at 100 (5 s <= 30) -> one
        //    failure note; away:0 left at 103.5 (1.5 s <= 3) -> reversal note "1.5초"; + wait; values.
        string keepDesc = "싸움을 끊고 목표 계속하기 — 줍기 D 이것을 고르면 싸우지도 피하지도 않고 목표 일을 함: 가는 동안 공격하지 않고, 6칸 안 적 2마리(가장 가까운 Slime 3.2칸)는 따라오거나 칠 수 있음.";
        Hand("H2 Doable false leaves the next goal step as the break-off option",
            "{\"b\":2,\"depth\":0,\"goal\":\"Gather\",\"free\":false,\"closeFight\":true,\"closeFightOff\":false,\"cur\":\"AttackSkill\",\"curKey\":\"\",\"prefetch\":false,\"near6\":2,\"enemy0\":\"Slime\",\"enemy0Dist\":\"3.2\",\"canMine\":true,\"canFight\":true,\"noMine\":\"\",\"noFight\":\"\",\"tnow0\":105,\"failMemory\":30,"
            + "\"lists\":{\"cont\":[],\"follow\":[],\"enemy\":[" + J(1, "attack", "attack:50", "공격 A") + "],\"combat\":[" + J(2, "away", "away:0", "거리 벌리기 B") + "],\"craft\":[],\"life\":[],\"mine\":[" + J(3, "mine", "mine:1,1", "캐기 C", true) + "],\"dig\":[],\"remember\":[],\"item\":[" + J(4, "pickup", "pickup:2,2", "줍기 D", true) + "],\"explore\":[],\"digExplore\":[],\"other\":[]},\"foodPicks\":[],"
            + "\"fails\":[{\"key\":\"attack:50\",\"count\":1,\"last\":100,\"reason\":\"막힘\"}],\"left\":[{\"key\":\"away:0\",\"t\":103.5}]}",
            "{\"b\":2,\"tnow\":105,\"setLog\":\" set=close-fight\",\"skipped\":\"\",\"noBody\":0,\"blockNote\":\"\",\"bodyCan\":{\"1\":true,\"2\":true,\"4\":true},\"doable\":{\"3\":false,\"4\":true},"
            + "\"list\":[" + O(1, "attack", "attack:50", "attack_50", "공격 A 최근 이 대상에서 실패 1회: 막힘.") + "," + O(2, "away", "away:0", "away_0", "거리 벌리기 B 이 행동은 " + 1.5f.ToString("F1") + "초 전에 하다가 그만둔 것(다시 고르면 처음부터).") + ","
            + O(4, "pickup", "pickup:2,2", "pickup_2_2", keepDesc) + "," + O(5, "wait", "wait", "wait", "기다리기 — 3초") + "]}");
        // H3 explore, close fight, the only goal step not doable: goalSide [3] -> Doable(3) = false -> keep null ->
        //    "set=close-fight(no goal step to break off for)"; list = enemy [1]; BodyCan(1) true; a second failure of
        //    attack:50 within 30 s leaves it out -> list empty, skipped "attack:50(막힘)"; + wait.
        Hand("H3 Doable false everywhere: no break-off option, then a twice-failed attack left out",
            "{\"b\":3,\"depth\":0,\"goal\":\"Explore\",\"free\":false,\"closeFight\":true,\"closeFightOff\":false,\"cur\":\"\",\"curKey\":\"\",\"prefetch\":false,\"near6\":1,\"enemy0\":\"Slime\",\"enemy0Dist\":\"2.0\",\"canMine\":true,\"canFight\":true,\"noMine\":\"\",\"noFight\":\"\",\"tnow0\":110,\"failMemory\":30,"
            + "\"lists\":{\"cont\":[],\"follow\":[],\"enemy\":[" + J(1, "attack", "attack:50", "공격 A") + "],\"combat\":[],\"craft\":[],\"life\":[],\"mine\":[],\"dig\":[],\"remember\":[],\"item\":[],\"explore\":[" + J(3, "explore", "explore:9,9", "탐험 C", true) + "],\"digExplore\":[],\"other\":[]},\"foodPicks\":[],"
            + "\"fails\":[{\"key\":\"attack:50\",\"count\":2,\"last\":100,\"reason\":\"막힘\"}],\"left\":[]}",
            "{\"b\":3,\"tnow\":110,\"setLog\":\" set=close-fight(no goal step to break off for)\",\"skipped\":\"attack:50(막힘)\",\"noBody\":0,\"blockNote\":\"\",\"bodyCan\":{\"1\":true},\"doable\":{\"3\":false},"
            + "\"list\":[" + O(4, "wait", "wait", "wait", "기다리기 — 3초") + "]}");
        // the check itself: a record whose old Doable value is missing must fail the replay (the assembler may only read
        // what the old code evaluated)
        var badIn = ExpJson.Parse("{\"b\":4,\"depth\":0,\"goal\":\"Explore\",\"free\":false,\"closeFight\":true,\"closeFightOff\":false,\"cur\":\"\",\"curKey\":\"\",\"near6\":1,\"enemy0\":\"Slime\",\"enemy0Dist\":\"2.0\",\"failMemory\":30,\"lists\":{\"enemy\":[" + J(1, "attack", "attack:50", "공격 A") + "],\"explore\":[" + J(3, "explore", "explore:9,9", "탐험 C", true) + "]},\"foodPicks\":[],\"fails\":[],\"left\":[]}") as Dictionary<string, object>;
        var badOut = ExpJson.Parse("{\"b\":4,\"tnow\":110,\"setLog\":\" set=close-fight(no goal step to break off for)\",\"skipped\":\"\",\"noBody\":0,\"bodyCan\":{\"1\":true},\"doable\":{},\"list\":[" + O(1, "attack", "attack:50", "attack_50", "공격 A") + "," + O(4, "wait", "wait", "wait", "w") + "]}") as Dictionary<string, object>;
        string bad = Replay(badIn, badOut);
        Program.Check(bad != null && bad.StartsWith("asked for a value the old code did not evaluate"), $"replay refuses a value the old code did not evaluate ({bad})");
    }

    static string J(int id, string kind, string key, string desc, bool progress = false, bool byItself = false)
        => $"{{\"id\":{id},\"kind\":{ExpJson.S(kind)},\"key\":{ExpJson.S(key)},\"desc\":{ExpJson.S(desc)},\"progress\":{(progress ? "true" : "false")},\"comesByItself\":{(byItself ? "true" : "false")}}}";
    static string O(int id, string kind, string key, string value, string desc)
        => $"{{\"id\":{id},\"kind\":{ExpJson.S(kind)},\"key\":{ExpJson.S(key)},\"value\":{ExpJson.S(value)},\"desc\":{ExpJson.S(desc)}}}";

    static void Hand(string name, string inJson, string outJson)
    {
        var inp = ExpJson.Parse(inJson) as Dictionary<string, object>; var outp = ExpJson.Parse(outJson) as Dictionary<string, object>;
        string r = inp == null || outp == null ? "hand record does not parse" : Replay(inp, outp);
        Program.Check(r == null, $"hand record {name}{(r == null ? "" : ": " + r)}");
    }
}
