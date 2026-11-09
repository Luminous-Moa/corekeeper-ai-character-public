// S6b: AICandidateAssembler against the OLD code's assembly records (refactor plan S6b, review F9: the only equivalence
// evidence). fixtures/asm-L1.log.gz = the ASM-IN / ASM-OUT lines of the L1 decision log (runs/L1/collect/
// decisions-010704.log, sha256 a79c028fd342fb2f…, written by rf-pre-L1 = the old BuildCandidates with the S6a recorder),
// CR removed. Every complete build is replayed: the final list (id, kind, key, value, desc), the set log, the left-out
// note and the body count must be the same, and the assembler must ask Doable / BodyCan for exactly the candidates the
// old code evaluated (it never reads a value the old code did not compute). Recursed builds: only the innermost is
// compared (the outer has no list). The L1 corpus never had BodyCan or Doable false, so hand records below cover those;
// their expected outputs are worked out from the old code (rf-S5 AIBrain.Loop.cs BuildCandidates, see each case).
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
    static readonly string[] ListNames = { "cont", "follow", "enemy", "combat", "craft", "life", "mine", "dig", "remember", "item", "explore", "digExplore", "other" };

    static List<object> Arr(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
    static bool B(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;

    // Replays one IN/OUT pair; returns null when equal, else the first difference.
    public static string Replay(Dictionary<string, object> inp, Dictionary<string, object> outp)
    {
        var byId = new Dictionary<int, AsmCand>();
        AsmCand C(Dictionary<string, object> j)
        {
            int id = ExpJson.GetI(j, "id");
            if (!byId.TryGetValue(id, out var c))
                byId[id] = c = new AsmCand { id = id, kind = ExpJson.GetS(j, "kind"), key = ExpJson.GetS(j, "key"), desc = ExpJson.GetS(j, "desc"), progress = B(j, "progress"), comesByItself = B(j, "comesByItself") };
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
            foodPicks = new HashSet<AsmCand>(Arr(inp, "foodPicks").Select(x => byId[(int)(double)x])),
        };
        var bodyRec = ExpJson.GetO(outp, "bodyCan"); var doRec = ExpJson.GetO(outp, "doable");
        var askedBody = new List<int>(); var askedDo = new List<int>();
        string missing = null;
        bool Rec(Dictionary<string, object> rec, List<int> asked, AsmCand c)
        {
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
        if (setLog != ExpJson.GetS(outp, "setLog")) return $"setLog '{setLog}' vs '{ExpJson.GetS(outp, "setLog")}'";
        if (noBody != ExpJson.GetI(outp, "noBody")) return $"noBody {noBody} vs {ExpJson.GetI(outp, "noBody")}";

        var fails = new Dictionary<string, (int count, float last, string reason)>();
        foreach (Dictionary<string, object> f in Arr(inp, "fails")) fails[ExpJson.GetS(f, "key")] = (ExpJson.GetI(f, "count"), (float)ExpJson.GetD(f, "last"), ExpJson.GetS(f, "reason"));
        var left = new List<(string key, float t)>();
        foreach (Dictionary<string, object> x in Arr(inp, "left")) left.Add((ExpJson.GetS(x, "key"), (float)ExpJson.GetD(x, "t")));
        var skipped = AICandidateAssembler.Mark(list, (float)ExpJson.GetD(outp, "tnow"), fails, (float)ExpJson.GetD(inp, "failMemory"), left);
        string skippedNote = skipped.Count == 0 ? "" : string.Join(", ", skipped);
        if (skippedNote != ExpJson.GetS(outp, "skipped")) return $"skipped '{skippedNote}' vs '{ExpJson.GetS(outp, "skipped")}'";

        var exp = Arr(outp, "list").Cast<Dictionary<string, object>>().ToList();
        var w = exp.Count > 0 ? exp[exp.Count - 1] : null;
        if (w == null || ExpJson.GetS(w, "kind") != "wait") return "the old list does not end with wait";
        var wait = new AsmCand { id = ExpJson.GetI(w, "id"), kind = "wait", key = "wait", desc = ExpJson.GetS(w, "desc") };   // the wait text comes from the settings (not an assembly input)
        AICandidateAssembler.Finish(list, wait);
        if (list.Count != exp.Count) return $"list length {list.Count} vs {exp.Count}";
        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i]; var e = exp[i];
            if (g.id != ExpJson.GetI(e, "id") || g.kind != ExpJson.GetS(e, "kind") || g.key != ExpJson.GetS(e, "key") || g.value != ExpJson.GetS(e, "value") || g.desc != ExpJson.GetS(e, "desc"))
                return $"item {i}: got #{g.id} {g.kind} {g.key} {g.value} '{g.desc}' vs #{ExpJson.GetI(e, "id")} {ExpJson.GetS(e, "kind")} {ExpJson.GetS(e, "key")} {ExpJson.GetS(e, "value")} '{ExpJson.GetS(e, "desc")}'";
        }
        return null;
    }

    static readonly Regex Line = new Regex(@"ASM-(IN|OUT) #(\d+) (.*)$");

    public static void Run()
    {
        // ---- the L1 corpus (old code)
        string path = Environment.GetEnvironmentVariable("ASM_CORPUS"); bool plain = path != null && !path.EndsWith(".gz"); if (path == null) path = Path.Combine(AppContext.BaseDirectory, "fixtures", "asm-L1.log.gz");
        if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "core-tests", "fixtures", "asm-L1.log.gz");
        var ins = new Dictionary<int, string>(); var outs = new Dictionary<int, string>();
        using (Stream gz = plain ? (Stream)File.OpenRead(path) : new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
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
        foreach (var kv in ins.OrderBy(k => k.Key))
        {
            if (!outs.TryGetValue(kv.Key, out var o)) { incomplete++; continue; }
            if (o == "recursed") { recursed++; continue; }
            var inp = ExpJson.Parse(kv.Value) as Dictionary<string, object>; var outp = ExpJson.Parse(o) as Dictionary<string, object>;
            if (inp == null || outp == null) { incomplete++; continue; }
            string r = Replay(inp, outp);
            if (r == null) same++; else { diff++; if (firstDiffs.Count < 3) firstDiffs.Add($"#{kv.Key}: {r}"); }
            if (ExpJson.GetS(outp, "setLog").Contains("close-fight")) closeSets++;
            if (Arr(outp, "list").Count == 15) over14++;
            if (Arr(inp, "fails").Count > 0) withFails++;
            if (Arr(inp, "left").Count > 0) withLeft++;
            if (ExpJson.GetS(inp, "goal") == "None") goalNone++;
        }
        Console.WriteLine("    L2 corpus " + path); Program.Check(incomplete == 0, $"L1 corpus: {ins.Count} builds, {recursed} recursed, {incomplete} incomplete (expected 864 / 10 / 0)");
        Program.Check(diff == 0, $"L1 corpus replay: {same} builds exactly the same as the old code, {diff} different{(firstDiffs.Count > 0 ? ": " + string.Join(" | ", firstDiffs) : "")}");
        Console.WriteLine($"    (covered: close-fight set {closeSets}, 15 items after the cut {over14}, failure memory {withFails}, recent switch {withLeft}, no goal {goalNone})");

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
