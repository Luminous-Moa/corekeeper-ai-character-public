// Offline checks of the experience memory (AIExpCore.cs) without the game.
//  S1-S6  scenarios from the direction review (work/2026-11/2026-11-04-LLM-기억방향-검토/review.md, "최소 검증 사례")
//  T1-T7  cases from the independent check (work/2026-11/2026-11-04-LLM-기억/2-검수.md) and its notes R3, R6, R8
//  N1-N3  cases from the re-check (2-검수.md "재검수": G1 latest block only, G2 repeats with a growing wait, G3 craft
//         blocks undone only by that item's recipe)
//  S5     a 100-hour synthetic play of one character with the real gather set: file sizes, load time at every
//         restart, report part size and time, and whether the right experience is picked
// dotnet run -c Release -- [work dir]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public static class Program
{
    static int _fail;
    static long _clock = 1_800_000_000;
    static string _root;
    static void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {what}"); if (!ok) _fail++; }

    const string TOOL = "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 0개 · 보이는 목표 광석 (3,4): 도구 부족(추정 피해 10 ≤ 감소 12)";
    const string NOSRC = "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 0개(닿는 것), 파 들어갈 수 있는 목표 광석 0개, 기억한 목표 광석 0곳(갈 길 없음), 탐험할 새 구역 없음";
    const string WAIT = "stalled: 할 수 있는 행동이 기다리기뿐(5번 연속) · 잠시 뺀 대상: mine:3,4";
    const string CRAFT = "stalled: 만들 방법이 없음 — 구리 작업대에서만 만들 수 있음, 보이는 곳에 없음";
    const string CRAFT_TOOL = "stalled: 만들 방법이 없음 — 재료 TinOre: 보이는 목표 광석 (1,2): 도구 부족";

    static ExpRecorder NewRec(string dir, string ch = "claude")
    {
        var r = new ExpRecorder(dir, "w1", ch) { Clock = () => _clock };
        r.Load();
        return r;
    }
    static ExpRecorder Rec(string name) => NewRec(Path.Combine(_root, name));
    static ExpCond C(int lv, string area, int hp = -1) => new ExpCond { toolLv = lv, weaponLv = lv, armorLv = lv, hpMax = hp > 0 ? hp : 100 + 10 * lv, area = area, cell = "0,0", tool = "Pick" + lv, weapon = "Sword" + lv, seen = 100, stations = 2 };
    static ExpCond G(int tool, int wpn, int arm, string area, int hp = 100, int seen = 100, int st = 2) => new ExpCond { toolLv = tool, weaponLv = wpn, armorLv = arm, hpMax = hp, area = area, cell = "0,0", tool = "P" + tool, weapon = "W" + wpn, seen = seen, stations = st };
    static ExpCond With(ExpCond c, int ore) { c.ore = ore; return c; }
    static string Note(string report, string key)
    {
        int i = report.IndexOf($"\"key\":\"{key}\"", StringComparison.Ordinal);
        if (i < 0) return "(key not in report)";
        int n = report.IndexOf("\"note\":\"", i, StringComparison.Ordinal);
        int e = report.IndexOf("\"}", n, StringComparison.Ordinal);
        return report.Substring(n + 8, e - n - 8);
    }
    static void Run(ExpRecorder r, string key, string why, ExpCond c0, ExpCond c1, double secs = 120, int cmiss = -1, int cst = -1)
    {
        var g = new object(); string kind = key.Split(':')[0]; string item = key.Contains(":") ? key.Split(':')[1] : "";
        r.GoalStart(g, null, key, kind, item, 3, "llm:claude#1", c0); r.Play += secs; _clock += (long)secs;
        r.Open.craftMissing = cmiss; r.Open.craftStation = cst;
        r.GoalEnd(g, why, 0, c1, c1.area);
    }
    static List<Dictionary<string, object>> Events(string dir) =>
        Directory.GetFiles(dir, "events*.jsonl").SelectMany(f => File.ReadAllLines(f)).Where(l => l.Length > 1).Select(l => (Dictionary<string, object>)ExpJson.Parse(l)).ToList();
    static bool IdsOk(string dir) { var ids = Events(dir).Select(e => ExpJson.GetL(e, "id")).ToList(); return ids.Distinct().Count() == ids.Count && ids.SequenceEqual(ids.OrderBy(x => x)); }

    public static int Main(string[] args)
    {
        _root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "exp-sim");
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        Directory.CreateDirectory(_root);
        Console.WriteLine($"work dir {_root}");

        Console.WriteLine("S1 one goal, two downs, done");
        {
            string d = Path.Combine(_root, "s1");
            var r = NewRec(d);
            var g = new object();
            r.GoalStart(g, null, "gather:TinOre", "gather", "TinOre", 3, "llm:claude#1", C(2, "Slime"));
            r.Play += 30; r.Down(new[] { "Caveling" }, "적 근처", C(2, "Slime"));
            r.Play += 20; r.Down(new[] { "Caveling", "Slime" }, "적 근처", C(2, "Slime"));
            r.Play += 40;
            Check(r.GoalEnd(g, "goal done", 3, C(2, "Slime")), "goal end accepted");
            Check(r.Book.Rows.Count == 1 && r.Book.Rows[0].outcome == "completed", "1 run, completed");
            Check(r.Book.Rows[0].downs == 2, $"downs inside the run = 2 (got {r.Book.Rows[0].downs})");
            var ev = Events(d);
            Check(ev.Count(e => ExpJson.GetS(e, "kind") == "down" && ExpJson.GetS(e, "run") == r.Book.Rows[0].run) == 2, "2 down events carry the run id");
            Check(ev.Count(e => ExpJson.GetS(e, "kind") == "goal_end") == 1, "1 goal_end event");

            Console.WriteLine("S2 repeats and restarts");
            Check(!r.GoalEnd(g, "goal done", 3, C(2, "Slime")), "second end of the same goal ignored");
            var g2 = new object();
            r.GoalStart(g2, null, "gather:CopperOre", "gather", "CopperOre", 5, "host", C(2, "Slime"));
            r.Play += 10;
            var r2 = NewRec(d);
            Check(r2.Book.Rows.Count == 2 && r2.Book.Rows[1].outcome == "unknown", "restart: the open run is closed once as unknown");
            var r3 = NewRec(d);
            Check(r3.Book.Rows.Count == 2, "another restart: still 2 runs");
            var g3 = new object();
            r3.GoalStart(g3, null, "craft:WoodPickaxe", "craft", "WoodPickaxe", 1, "llm:claude#2", C(2, "Slime"));
            string stateBefore = File.ReadAllText(r3.StatePath);
            r3.Play += 15; r3.GoalEnd(g3, "goal done", 1, C(2, "Slime"));
            File.WriteAllText(r3.StatePath, stateBefore);
            var lines = File.ReadAllLines(r3.GoalsPath).ToList(); lines.RemoveAt(lines.Count - 1); File.WriteAllLines(r3.GoalsPath, lines);
            var r4 = NewRec(d);
            Check(r4.Book.Rows.Count == 3 && r4.Book.Rows[2].outcome == "completed", "end found in the events after a stop before the state save (no unknown added)");
            Check(IdsOk(d), $"event ids unique and increasing over {Events(d).Select(e => ExpJson.GetS(e, "sess")).Distinct().Count()} sessions");
            File.Delete(r4.GoalsPath);
            var r5 = NewRec(d);
            Check(r5.Book.Rows.Select(x => x.run + x.outcome).SequenceEqual(r4.Book.Rows.Select(x => x.run + x.outcome)), "goals.jsonl deleted: rebuilt from the events, same runs");
        }

        Console.WriteLine("S3 blocked with a weak pick, then a better pick");
        {
            var r = Rec("s3");
            for (int i = 0; i < 2; i++) Run(r, "gather:TinOre", TOOL, C(1, "Slime"), C(1, "Slime"), 60);
            string n1 = Note(r.Report(C(1, "Slime"), "gather:TinOre", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(n1.Contains("아직 풀리지 않은 막힘(도구 부족: 곡괭이 레벨이 그대로 1; 바뀐 것 없이 연속 2번)"), $"same pick: '{n1}'");
            string n2 = Note(r.Report(C(2, "Slime"), "gather:TinOre", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(!n2.Contains("아직 풀리지") && n2.Contains("다시 해 볼 만함") && n2.Contains("곡괭이 레벨 1→2"), $"better pick: '{n2}'");
            Run(r, "gather:TinOre", "goal done", C(2, "Slime"), C(2, "Slime"), 90);
            string n3 = Note(r.Report(C(2, "Slime"), "gather:TinOre", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(n3.Contains("막힘 2번 뒤에 완료함") && !n3.Contains("아직 풀리지"), $"done after: '{n3}'");
        }

        Console.WriteLine("S4 not failures, other character");
        {
            var r = Rec("s4");
            var a = new object(); r.GoalStart(a, null, "gather:Wood", "gather", "Wood", 5, "llm:claude#1", C(1, "Slime")); r.Play += 20; r.GoalEnd(a, "/ai stop", 1, C(1, "Slime"));
            var b = new object(); r.GoalStart(b, null, "gather:Wood", "gather", "Wood", 5, "llm:claude#2", C(1, "Slime")); r.Play += 20;
            var c = new object(); r.GoalStart(c, b, "follow", "follow", "", 0, "host", C(1, "Slime")); r.Play += 20; r.GoalEnd(c, "/ai goal 없음", -1, C(1, "Slime"));
            var e = new object(); r.GoalStart(e, null, "gather:Wood", "gather", "Wood", 5, "llm:claude#3", C(1, "Slime")); r.Play += 20; r.GoalEnd(e, "3 consecutive failures: 시간 초과", 0, C(1, "Slime"));
            var f = new object(); r.GoalStart(f, null, "gather:Wood", "gather", "Wood", 5, "llm:claude#4", C(1, "Slime")); r.Play += 20;
            var r2 = Rec("s4");
            var outs = r2.Book.ByKey["gather:Wood"].Select(x => x.outcome).ToList();
            Check(outs.SequenceEqual(new[] { "cancelled", "replaced", "system", "unknown" }), $"outcomes {string.Join(",", outs)}");
            string rep = r2.Report(C(1, "Slime"), "gather:Wood", new[] { "gather:Wood" });
            Check(rep.Contains("\"key\":\"gather:Wood\",\"runs\":4,\"done\":0,\"blocked\":0,\"other\":4,\"block\":\"없음\"") && !Note(rep, "gather:Wood").Contains("막힘"), "4 runs, blocked 0");
            var other = NewRec(Path.Combine(_root, "s4-gpt"), "gpt");
            Run(other, "gather:CopperOre", TOOL, C(1, "Slime"), C(1, "Slime"));
            Check(!r2.Report(C(1, "Slime"), "gather:CopperOre", new[] { "gather:CopperOre" }).Contains("CopperOre"), "gpt's CopperOre run is not in Claude's report");
        }

        Console.WriteLine("S6 events file rotation");
        {
            string d = Path.Combine(_root, "s6");
            var r = NewRec(d); r.RotateBytes = 32 * 1024;
            for (int i = 0; i < 300; i++)
            {
                var g = new object(); r.GoalStart(g, null, "gather:Wood", "gather", "Wood", 2, "llm:claude#" + i, C(1, "Slime"));
                for (int k = 0; k < 3; k++) r.Event("fight", r.Open.run, new[] { "enemy:Slime" }, "game", "{\"secs\":5}");
                r.Play += 60; r.GoalEnd(g, i % 5 == 0 ? NOSRC : "goal done", 2, C(1, "Slime"));
                if (i == 150) { r = NewRec(d); r.RotateBytes = 32 * 1024; }
            }
            int files = Directory.GetFiles(d, "events*.jsonl").Length;
            var ids = Events(d).Select(e => ExpJson.GetL(e, "id")).OrderBy(x => x).ToList();
            Check(files > 3, $"rotated into {files} files");
            Check(ids.Distinct().Count() == ids.Count && ids[ids.Count - 1] == ids.Count, $"ids 1..{ids.Count} once each across files and a restart");
            File.Delete(r.GoalsPath);
            Check(NewRec(d).Book.Rows.Count == 300, $"rebuilt 300 runs from {files} files");
        }

        // ---------------------------------------------------------------- the independent check's cases
        Console.WriteLine("T1 (F1) pick made inside the run, still blocked: judged by the end conditions");
        {
            var r = Rec("t1");
            Run(r, "gather:TinOre", TOOL, G(1, 1, 0, "Slime"), G(2, 1, 0, "Slime"));
            string n = Note(r.Report(G(2, 1, 0, "Slime"), "", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(n.Contains("아직 풀리지 않은 막힘(도구 부족: 곡괭이 레벨이 그대로 2)"),$"now = the pick it failed with: '{n}'");
            string n2 = Note(r.Report(G(3, 1, 0, "Slime"), "", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(n2.Contains("다시 해 볼 만함") && n2.Contains("곡괭이 레벨 2→3"), $"a better pick than at the end: '{n2}'");
        }

        Console.WriteLine("T2 (F2) unrelated changes do not undo a block; the area is where the goal was worked");
        {
            var r = Rec("t2");
            Run(r, "gather:TinOre", TOOL, G(1, 1, 0, "Larva"), G(1, 1, 0, "Larva"));
            string a = Note(r.Report(G(1, 1, 1, "Larva"), "", new[] { "gather:TinOre" }), "gather:TinOre");
            string b = Note(r.Report(G(1, 1, 0, "Slime", 150), "", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(a.Contains("아직 풀리지 않은 막힘(") && !a.Contains("다시 해 볼"), $"armor 0→1: '{a}'");
            Check(b.Contains("아직 풀리지 않은 막힘(") &&!b.Contains("다시 해 볼") && b.Contains("Larva에서"), $"reported at the core (Slime), more health: '{b}'");
            var g = new object(); r.GoalStart(g, null, "gather:CopperOre", "gather", "CopperOre", 3, "llm:claude#2", G(1, 1, 0, "Larva"));
            r.Play += 60; r.Down(new[] { "Caveling" }, "x", G(1, 1, 0, "Larva")); r.Play += 60; r.GoalEnd(g, "goal done", 3, G(1, 1, 0, "Larva"), "Larva");
            string c = Note(r.Report(G(1, 1, 0, "Slime"), "", new[] { "gather:CopperOre" }), "gather:CopperOre");
            string d = Note(r.Report(G(1, 3, 0, "Slime"), "", new[] { "gather:CopperOre" }), "gather:CopperOre");
            Check(c.Contains("마지막 쓰러짐 때와 무기·방어구·체력 같음"), $"down, same weapon: '{c}'");
            Check(d.Contains("달라짐: 무기 레벨 1→3"), $"down, better weapon: '{d}'");
        }

        Console.WriteLine("T3 (B1) blocks a better pick cannot undo: undone by new finds or by time; explore never stays blocked");
        {
            var r = Rec("t3");
            Run(r, "gather:CopperOre", NOSRC, G(1, 1, 0, "Slime"), With(G(1, 1, 0, "Slime", seen: 100), 0));
            Run(r, "craft:CopperWorkbench", CRAFT, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"), 120, 0, 0);
            Run(r, "explore", WAIT, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime", seen: 100));
            var g = new object(); r.GoalStart(g, null, "explore", "explore", "", 0, "llm:claude#9", G(1, 1, 0, "Slime")); r.Play += 120; r.GoalEnd(g, "nothing left to explore", -1, G(1, 1, 0, "Slime", seen: 100), "Slime");
            var cands = new[] { "gather:WallDirtBlock", "gather:CopperOre", "gather:TinOre", "gather:Wood", "craft:CopperWorkbench", "explore", "follow" };
            Func<string, int> ore0 = _ => 0, ore2 = n => n == "CopperOre" ? 2 : 0;
            Func<string, (int, int)> craft00 = _ => (0, 0), craft01 = _ => (0, 1);
            r.Play += 300;
            string rep = r.Report(G(1, 1, 0, "Slime"), "", cands, ore0, craft00);
            Check(Note(rep, "gather:CopperOre").Contains("아직 풀리지 않은 막힘(모을 곳 못 찾음"), $"5 min later, nothing new: {Note(rep, "gather:CopperOre")}");
            Check(Note(rep, "craft:CopperWorkbench").Contains("아직 풀리지 않은 막힘(만들 방법 없음"), $"5 min later: {Note(rep, "craft:CopperWorkbench")}");
            Check(Note(rep, "explore").Contains("아직 풀리지 않은 막힘(탐험할 곳 없음") && Note(rep, "explore").Contains("마지막 완료 뒤 막힘 2번"), $"5 min later: {Note(rep, "explore")}");
            string ns = Note(r.Report(G(1, 1, 0, "Slime", seen: 106), "", cands, ore0, craft00), "gather:CopperOre");
            Check(ns.Contains("아직 풀리지 않은 막힘(모을 곳 못 찾음: 새 구역 6칸을 봤지만 CopperOre은(는) 더 기억하지 못함"), $"an ore, 6 new cells but no more copper remembered (weak hint, P1): '{ns}'");
            Run(r, "gather:Wood", NOSRC, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime", seen: 100));   // not an ore: ore unknown (-1)
            string nw = Note(r.Report(G(1, 1, 0, "Slime", seen: 106), "", new[] { "gather:Wood" }, ore0, craft00), "gather:Wood");
            Check(nw.Contains("다시 해 볼 만함") && nw.Contains("새 구역 6칸"), $"not an ore, 6 new cells explored: '{nw}'");
            string no = Note(r.Report(G(1, 1, 0, "Slime"), "", cands, ore2, craft00), "gather:CopperOre");
            Check(no.Contains("다시 해 볼 만함") && no.Contains("CopperOre을(를) 2곳 더 기억함"), $"2 more copper ores remembered: '{no}'");
            string nc = Note(r.Report(G(1, 1, 0, "Slime"), "", cands, ore0, craft01), "craft:CopperWorkbench");
            Check(nc.Contains("다시 해 볼 만함") && nc.Contains("제작대를 기억함"), $"its station now remembered: '{nc}'");
            string ne = Note(r.Report(G(2, 1, 0, "Slime"), "", cands, ore0, craft00), "explore");
            Check(ne.Contains("다시 해 볼 만함") && ne.Contains("곡괭이 레벨 1→2"), $"explore after a better pick: '{ne}'");
            Run(r, "gather:Wood", "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"), 20 * 3600);
            rep = r.Report(G(1, 1, 0, "Slime"), "gather:Wood", cands, ore0, craft00);
            foreach (var k in new[] { "gather:CopperOre", "craft:CopperWorkbench", "explore" })
                Check(!Note(rep, k).Contains("아직 풀리지") && !Note(rep, k).Contains("다시 해 볼") && Note(rep, k).Contains("오래돼 지금은 모름"), $"20 h later, nothing measured, {k}: '{Note(rep, k)}'");
        }

        Console.WriteLine("N1 (G1) only the latest block is judged: no note says both");
        {
            var r = Rec("n1");
            Run(r, "gather:TinOre", TOOL, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"));
            Run(r, "gather:TinOre", TOOL, G(2, 1, 0, "Slime"), G(2, 1, 0, "Slime"));   // retried with a better pick, blocked again
            string a = Note(r.Report(G(2, 1, 0, "Slime"), "", new[] { "gather:TinOre" }), "gather:TinOre");
            Check(a.Contains("아직 풀리지 않은 막힘(도구 부족: 곡괭이 레벨이 그대로 2)") && !a.Contains("다시 해 볼") && !a.Contains("연속") && a.Contains("마지막 완료 뒤 막힘 2번"), $"pick 1 then pick 2, blocked both: '{a}'");
            Run(r, "gather:CopperOre", NOSRC, G(1, 1, 0, "Slime"), With(G(1, 1, 0, "Slime"), 0));
            r.Play += 33 * 60;
            string b = Note(r.Report(G(1, 1, 0, "Slime"), "", new[] { "gather:CopperOre" }, _ => 0), "gather:CopperOre");
            Check(b.Contains("오래돼 지금은 모름") && !b.Contains("변화가 있음"), $"33 min, nothing measured: '{b}'");
            Run(r, "gather:CopperOre", NOSRC, G(1, 1, 0, "Slime"), With(G(1, 1, 0, "Slime"), 0));   // tried again, blocked again
            string c = Note(r.Report(G(1, 1, 0, "Slime"), "", new[] { "gather:CopperOre" }, _ => 0), "gather:CopperOre");
            Check(c.Contains("아직 풀리지 않은 막힘(모을 곳 못 찾음") && c.Contains("연속 2번") && c.Contains("60분 뒤") && !c.Contains("다시 해 볼") && !c.Contains("오래돼"), $"blocked again: wait doubled: '{c}'");
            Run(r, "gather:TinOre", NOSRC, G(3, 1, 0, "Slime"), With(G(3, 1, 0, "Slime"), 0));   // another cause after the tool blocks
            string d = Note(r.Report(G(3, 1, 0, "Slime"), "", new[] { "gather:TinOre" }, _ => 0), "gather:TinOre");
            Check(d.Contains("아직 풀리지 않은 막힘(모을 곳 못 찾음") && !d.Contains("도구 부족") && !d.Contains("다시 해 볼"), $"older tool blocks, newer no-source block: '{d}'");
        }

        Console.WriteLine("N2 (G2) nothing changes; the goal is picked whenever it is not standing; 10 play hours");
        {
            int Picks(bool backoff, string name)
            {
                ExpResolve.Backoff = backoff;
                var r = Rec(name);
                int picks = 0;
                var now = G(1, 1, 0, "Slime");
                while (r.Play < 10 * 3600)
                {
                    var v = r.Book.ByKey.ContainsKey("gather:CopperOre") ? ExpReport.BlockState(r.Book.ByKey["gather:CopperOre"], now, r.Play, _ => 0, null) : ExpVerdict.None;
                    if (v != ExpVerdict.Standing) { picks++; Run(r, "gather:CopperOre", NOSRC, now, With(G(1, 1, 0, "Slime"), 0), 120); }
                    else Run(r, "gather:Wood", "goal done", now, now, 300);
                }
                ExpResolve.Backoff = true;
                return picks;
            }
            int off = Picks(false, "n2-off"), on = Picks(true, "n2-on");
            Check(off >= 15 && on >= 4 && on <= 6, $"the same failure picked {off} times with a fixed 30 min wait, {on} times with the growing wait (30 min, 1 h, 2 h, 4 h, 8 h)");
        }

        Console.WriteLine("N3 (G3) a craft block is undone only by that item's materials or station");
        {
            var r = Rec("n3");
            Run(r, "craft:CopperWorkbench", CRAFT, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"), 120, 2, 0);
            Run(r, "gather:WallDirtBlock", "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"), 120);   // the bag changes, the recipe does not
            var cands = new[] { "craft:CopperWorkbench" };
            string a = Note(r.Report(G(1, 1, 0, "Slime"), "", cands, null, _ => (2, 0)), "craft:CopperWorkbench");
            Check(a.Contains("아직 풀리지 않은 막힘(만들 방법 없음: 모자란 재료 2개·제작대 모름 그대로"), $"dirt gathered, recipe the same: '{a}'");
            string b = Note(r.Report(G(1, 1, 0, "Slime"), "", cands, null, _ => (1, 0)), "craft:CopperWorkbench");
            Check(b.Contains("다시 해 볼 만함") && b.Contains("모자란 재료 2→1개"), $"one material unit less missing: '{b}'");
            string c = Note(r.Report(G(1, 1, 0, "Slime"), "", cands, null, _ => (2, 2)), "craft:CopperWorkbench");
            Check(c.Contains("다시 해 볼 만함") && c.Contains("제작대가 가까이 있음"), $"its station in view: '{c}'");
        }

        // An upper LLM that follows the brief: a "풀리지 않음" goal is not chosen (another 3-minute goal instead), anything
        // else is chosen again and blocked a minute later by why(i). Tries in 10 play hours (the re-check 2's loop).
        int Loop(string name, string key, Func<int, string> why, Func<ExpCond> cond, Func<string, (int, int)> craft = null, Action between = null, int cmiss = -1, int cst = -1)
        {
            var r = Rec(name);
            int tries = 0;
            void Try() { var c = cond(); Run(r, key, why(tries), c, c, 60, cmiss, cst); tries++; }
            Try();
            while (r.Play < 10 * 3600)
            {
                if (ExpReport.BlockState(r.Book.ByKey[key], cond(), r.Play, _ => 0, craft) != ExpVerdict.Standing) Try();
                else { between?.Invoke(); Run(r, "follow", "goal done", cond(), cond(), 180); }
            }
            string n = Note(r.Report(cond(), "", new[] { key }, _ => 0, craft), key);
            Console.WriteLine($"    {name}: {tries} tries; last note: {n}");
            return tries;
        }
        ExpCond OreC(int seen = 100) => With(G(1, 1, 0, "Slime", seen: seen), 0);
        Console.WriteLine("A1-A5 (re-check 2) causes alternating or only new cells, nothing measured changes, 10 play hours");
        {
            int a1 = Loop("a1", "gather:TinOre", i => i % 2 == 0 ? NOSRC : TOOL, () => OreC());
            int a2 = Loop("a2", "explore", i => i % 2 == 0 ? "nothing left to explore" : WAIT, () => G(1, 1, 0, "Slime"));
            int a2x = Loop("a2-exh", "explore", _ => "nothing left to explore", () => G(1, 1, 0, "Slime"));
            int a2w = Loop("a2-wait", "explore", _ => WAIT, () => G(1, 1, 0, "Slime"));
            int a3 = Loop("a3", "gather:CopperOre", i => i % 2 == 0 ? NOSRC : WAIT, () => OreC());
            int seen = 100;
            int a4 = Loop("a4", "gather:CopperOre", _ => NOSRC, () => OreC(seen), null, () => seen += 5);
            int a5 = Loop("a5", "craft:CopperWorkbench", i => i % 2 == 0 ? CRAFT : WAIT, () => G(1, 1, 0, "Slime"), _ => (2, 0), null, 2, 0);
            Check(a1 <= 2, $"A1 no_source <-> tool_weak: {a1} tries");
            Check(a2 <= 8 && a2x <= 6 && a2w <= 8, $"A2 explore nothing left <-> only wait: {a2} tries (only nothing left {a2x}, only only-wait {a2w}); was 27");
            Check(a3 <= 8, $"A3 gather no_source <-> only_wait: {a3} tries; was 27");
            Check(a4 <= 6, $"A4 ore, other goals see 5 new cells each time, the ore never remembered: {a4} tries; was 150");
            Check(a5 <= 8, $"A5 craft cannot_craft <-> only_wait: {a5} tries; was 27");
        }

        Console.WriteLine("T4 (R2) the running goal and its downs are in the report");
        {
            var r = Rec("t4"); var g = new object();
            r.GoalStart(g, null, "gather:TinOre", "gather", "TinOre", 3, "llm:claude#1", G(1, 1, 0, "Slime"));
            r.Play += 60; r.Down(new[] { "Caveling" }, "x", G(1, 1, 0, "Slime")); r.Play += 60; r.Down(new[] { "Caveling" }, "x", G(1, 1, 0, "Slime"));
            string rep = r.Report(G(1, 1, 0, "Slime"), "gather:TinOre", new[] { "gather:TinOre" });
            Check(rep.Contains("\"running\":\"gather:TinOre×3(LLM) 2분째, 그동안 쓰러짐 2(Caveling)\""), $"running: {rep.Substring(rep.IndexOf("\"running\""), 70)}");
        }

        Console.WriteLine("T5 (F3) exp_state.json cut off after 50 h");
        {
            string d = Path.Combine(_root, "t5");
            var r = NewRec(d);
            r.Items.Add("CopperOre"); r.Event("item_first", null, new[] { "item:CopperOre" }, "game", "{\"item\":\"CopperOre\",\"count\":1}");
            r.Areas.Add("Larva"); r.Event("area_enter", null, new[] { "area:Larva" }, "game", "{\"area\":\"Larva\",\"first\":true}");
            for (int i = 0; i < 50; i++) Run(r, "gather:TinOre", i == 49 ? TOOL : "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"), 3600);
            double play = r.Play; long next = r.NextId;
            File.WriteAllText(r.StatePath, "{\"v\":1,\"next\":20");
            File.Delete(r.StateBak);
            var r2 = NewRec(d);
            Check(Math.Abs(r2.Play - play) < 1 && r2.NextId > next, $"play {r2.Play / 3600:F1} h (was {play / 3600:F1}), next id {r2.NextId} (was {next})");
            Check(r2.Items.Contains("CopperOre") && r2.Areas.Contains("Larva"), "seen items/areas rebuilt from the events");
            string rep = r2.Report(G(1, 1, 0, "Slime"), "", new[] { "gather:TinOre" });
            Check(rep.Contains("\"recent\":[\"방금 gather:TinOre×3(LLM) → 막힘") && rep.Contains("\"60분 전 gather:TinOre") && rep.Contains("\"2시간 전 gather:TinOre"), "recent lines keep their times: " + rep.Substring(rep.IndexOf("\"recent\""), 160));
            Check(IdsOk(d), "event ids unique");
            // the open run lost with the state
            var g = new object(); r2.GoalStart(g, null, "gather:Wood", "gather", "Wood", 2, "llm:claude#5", G(1, 1, 0, "Slime")); r2.Play += 30;
            File.WriteAllText(r2.StatePath, "{broken"); File.Delete(r2.StateBak);
            var r3 = NewRec(d);
            Check(r3.Book.Rows.Last().key == "gather:Wood" && r3.Book.Rows.Last().outcome == "unknown", "an open run lost with the state is closed as unknown from the events");
            // .bak used when the main file is damaged
            File.WriteAllText(r3.StatePath, "{broken");
            var r4 = NewRec(d);
            Check(r4.LoadNote.Contains(".bak") && IdsOk(d), $"damaged state, .bak used: '{r4.LoadNote}'");
        }

        Console.WriteLine("T6 (F3) exp_state.json locked at load");
        {
            string d = Path.Combine(_root, "t6");
            var r = NewRec(d); for (int i = 0; i < 3; i++) Run(r, "gather:Wood", "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"));
            double playBefore = r.Play;
            var r2 = new ExpRecorder(d, "w1", "claude") { Clock = () => _clock };
            bool threw = false;
            using (var lockf = new FileStream(r.StatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            { try { r2.Load(); } catch (Exception) { threw = true; } }
            Check(threw && !r2.Loaded, "load throws and the recorder is not loaded (AIExperience turns the memory off for this join)");
            bool wrote = true; try { r2.Event("fight", null, null, "game", "{}"); } catch (InvalidOperationException) { wrote = false; }
            Check(!wrote, "a not-loaded recorder refuses to write");
            var r3 = NewRec(d);
            Run(r3, "gather:Wood", "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"));
            Check(IdsOk(d) && r3.Play >= playBefore && r3.Book.Rows.Count == 4, $"next join: ids unique, play {r3.Play:F0} s (was {playBefore:F0}), runs {r3.Book.Rows.Count}");
            Check(File.Exists(r3.StateBak) && !File.Exists(r3.StatePath + ".tmp"), "state swapped in from a temporary file, previous kept as .bak");
        }

        Console.WriteLine("T7 report length with real key names, 6 recent lines with downs");
        {
            var r = Rec("t7"); var keys = new[] { "craft:CopperMiningPick", "craft:WoodenWorkBench", "gather:WallDirtBlock", "craft:CopperWorkbench", "gather:CopperOre", "gather:TinOre", "craft:Furnace", "craft:TinMiningPick" };
            foreach (var k in keys) for (int j = 0; j < 3; j++)
                {
                    var g = new object(); r.GoalStart(g, null, k, "x", "y", 10, "llm:claude#1", G(1, 1, 0, "Larva"));
                    r.Play += 400; r.Down(new[] { "CavelingShaman" }, "x", null); r.Down(new[] { "MushroomBrute" }, "x", null); r.Down(new[] { "BigLarva" }, "x", null);
                    r.GoalEnd(g, j == 0 ? TOOL : j == 1 ? NOSRC : "goal done", 3, G(1, 1, 0, "Larva"), "Larva");
                }
            var og = new object(); r.GoalStart(og, null, "gather:TinOre", "gather", "TinOre", 5, "llm:claude#2", G(1, 1, 0, "Larva")); r.Play += 100; r.Down(new[] { "Caveling" }, "x", null);
            string rep = r.Report(G(2, 2, 2, "Slime", 150), "craft:TinMiningPick", keys.Concat(new[] { "explore", "follow" }).ToList());
            Check(rep.Length > 2200 && rep.Split(new[] { "\"key\":" }, StringSplitOptions.None).Length - 1 == keys.Length, $"full report {rep.Length} chars retains all {keys.Length} goal keys");
        }

        Console.WriteLine("R3 the same goal set again keeps one run; R8 a craft block mentioning a tool stays cannot_craft; R6 a damaged goals.jsonl");
        {
            string d = Path.Combine(_root, "r3");
            var r = NewRec(d);
            var a = new object(); r.GoalStart(a, null, "gather:TinOre", "gather", "TinOre", 3, "llm:claude#1", G(1, 1, 0, "Slime"), 4);
            r.Play += 60; r.Down(new[] { "Caveling" }, "x", G(1, 1, 0, "Slime"));
            var b = new object(); r.GoalStart(b, a, "gather:TinOre", "gather", "TinOre", 3, "llm:claude#2", G(1, 1, 0, "Slime"), 6);
            r.Play += 60; r.GoalEnd(b, "goal done", 3, G(1, 1, 0, "Slime"));
            var row = r.Book.Rows.Single();
            Check(row.resets == 1 && row.downs == 1 && row.start == 4 && row.outcome == "completed", $"one run (resets {row.resets}, downs {row.downs}, start {row.start})");
            var h1 = new object(); r.GoalStart(h1, null, "gather:Wood", "gather", "Wood", 3, "llm:claude#3", G(1, 1, 0, "Slime"), 0);
            var h2 = new object(); r.GoalStart(h2, h1, "gather:Wood", "gather", "Wood", 20, "host", G(1, 1, 0, "Slime"), 0);
            r.Play += 30; r.GoalEnd(h2, "goal done", 20, G(1, 1, 0, "Slime"));
            Check(r.Book.Rows.Last().source == "host" && r.Book.Rows.Last().count == 20, "the host set the same goal again: shown as the host's goal (Q2)");
            Run(r, "craft:TinMiningPick", CRAFT_TOOL, G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"));
            Check(r.Book.Rows.Last().cls == "cannot_craft", $"craft block class {r.Book.Rows.Last().cls}");
            ExpRun.Classify("stalled: 넣을 방법이 없음 — 기지 코어(0,0) 3칸, 기지 상자 0개, 만들 수 있는 상자 없음, 준비: 도구 부족", out var oS, out var cS, out _);
            ExpRun.Classify("stalled: 기지로 갈 방법이 없음 — 기지 (-32,32) 71칸", out var oH, out var cH, out _);
            Check(oS == "blocked" && cS == "no_place" && cH == "no_place", $"store/home stalls: own class ({cS}, {cH}), not tool_weak");
            File.AppendAllText(r.GoalsPath, "{\"run\":\"cut");   // a line cut off
            Run(r, "gather:Wood", "goal done", G(1, 1, 0, "Slime"), G(1, 1, 0, "Slime"));   // appended after the cut line (unreadable)
            var r2 = NewRec(d);
            Check(r2.Book.Rows.Count == 4 && r2.LoadNote.Contains("rebuilt"), $"bad line: rebuilt, runs {r2.Book.Rows.Count} ('{r2.LoadNote}')");
        }

        Console.WriteLine("S5 100-hour synthetic play (real gather set)");
        Sim(Path.Combine(_root, "s5"));

        Console.WriteLine(_fail == 0 ? "ALL CHECKS OK" : $"{_fail} CHECK(S) FAILED");
        return _fail == 0 ? 0 : 1;
    }

    static void Sim(string d)
    {
        var rnd = new Random(42);
        // the upper LLM can gather only these (AIPlanner can_do.gather and its answer check); other ores only as host goals
        string[] llmGather = { "WallDirtBlock", "CopperOre", "TinOre", "Wood" };
        var need = new Dictionary<string, int> { ["WallDirtBlock"] = 0, ["Wood"] = 0, ["CopperOre"] = 1, ["TinOre"] = 2, ["IronOre"] = 3, ["ScarletOre"] = 4, ["OctarineOre"] = 5, ["GalaxiteOre"] = 6, ["AncientOre"] = 3, ["SolariteOre"] = 7 };
        string[] hostOres = { "IronOre", "ScarletOre", "OctarineOre", "GalaxiteOre" };
        string[] areas = { "Slime", "Larva", "Stone", "Nature", "Sea", "Desert" };
        string[] enemies = { "Slime", "Caveling", "Larva", "MushroomBrute", "BigLarva", "CavelingShaman", "Mold", "Crab", "Scarab" };
        var crafts = Enumerable.Range(0, 40).Select(i => $"Item{i:00}").ToList();
        double[] tierAt = { 0, 8, 20, 35, 55, 75 };   // hours
        var ores = need.Keys.ToDictionary(k => k, k => 0);
        int seen = 50, stations = 1;
        Func<string, int> oreNow = n => ores.TryGetValue(n, out int v) ? v : 0;
        var craftSt = crafts.ToDictionary(c => c, c => (1 + int.Parse(c.Substring(4)) % 3, 0));   // (missing units, station)
        Func<string, (int, int)> craftNow = n => craftSt.TryGetValue(n, out var v) ? v : (-1, -1);
        var r = new ExpRecorder(d, "w1", "claude") { Clock = () => _clock };
        var loadMs = new List<double>(); var repMs = new List<double>(); var repChars = new List<int>();
        var sw = Stopwatch.StartNew(); r.Load(); loadMs.Add(sw.Elapsed.TotalMilliseconds);
        double nextRestart = 3 * 3600;
        int Tier(double play) => tierAt.Count(h => play / 3600 >= h);
        string AreaAt(double play) => areas[Math.Min(areas.Length - 1, (int)(play / 3600 / 17))];
        ExpCond Now(double play, bool atCore = false) { int t = Tier(play); return new ExpCond { toolLv = t, weaponLv = t, armorLv = t, hpMax = 100 + 10 * t, area = atCore ? "Slime" : AreaAt(play), cell = "0,0", tool = "Pick" + t, weapon = "Sword" + t, seen = seen, stations = stations }; }
        string lastKey = "";
        object replacing = null;
        int forcedEarly = 0, forcedLate = 0, goals = 0, staleWeak = 0, staleChecks = 0, mixedNotes = 0;
        while (r.Play < 100 * 3600)
        {
            if (r.Play >= nextRestart)
            {
                if (rnd.NextDouble() >= 0.5 && r.Open != null) r.End("unknown", "", "test", -1, Now(r.Play));
                sw.Restart(); r = new ExpRecorder(d, "w1", "claude") { Clock = () => _clock }; r.Load(Now(r.Play)); loadMs.Add(sw.Elapsed.TotalMilliseconds);
                nextRestart += 3 * 3600; replacing = null;
            }
            int tier = Tier(r.Play);
            string key, kind, item = ""; int count = 0; string src = "llm:claude#" + goals;
            if (r.Play > 2 * 3600 && forcedEarly < 3) { item = "AncientOre"; kind = "gather"; count = 3; src = "host"; forcedEarly++; }
            else if (r.Play > 99.5 * 3600 && forcedLate < 2) { item = "SolariteOre"; kind = "gather"; count = 3; src = "host"; forcedLate++; }
            else
            {
                double x = rnd.NextDouble();
                if (x < 0.40) { item = llmGather[rnd.Next(llmGather.Length)]; kind = "gather"; count = 1 + rnd.Next(10); }
                else if (x < 0.45) { item = hostOres[Math.Min(hostOres.Length - 1, rnd.Next(Math.Max(1, tier - 1)))]; kind = "gather"; count = 1 + rnd.Next(10); src = "host"; }
                else if (x < 0.7) { item = crafts[rnd.Next(Math.Min(40, 8 + tier * 6))]; kind = "craft"; count = 1 + rnd.Next(3); }
                else if (x < 0.88) kind = "explore";
                else kind = "follow";
            }
            key = item != "" ? kind + ":" + item : kind;
            var cond = Now(r.Play);
            // the report before choosing; half of them are made at the core (back up after a down, hold recheck)
            var cands = llmGather.Select(o => "gather:" + o).Concat(crafts.Take(16).Select(c => "craft:" + c)).Concat(new[] { "explore", "follow", "gather:AncientOre" }).ToList();
            var repCond = Now(r.Play, rnd.NextDouble() < 0.5);
            sw.Restart(); string rep = r.Report(repCond, lastKey, cands, oreNow, craftNow); repMs.Add(sw.Elapsed.TotalMilliseconds); repChars.Add(rep.Length);
            // invariants: a block that only new finds or time can undo never stands past the longest wait (8 h), and no
            // note carries two verdicts
            if (goals % 5 == 0)
                foreach (var kv in r.Book.ByKey)
                {
                    var bl = ExpReport.BlocksSinceDone(kv.Value);
                    if (bl.Count == 0 || ExpResolve.Cls(bl[bl.Count - 1]) == "tool_weak") continue;
                    staleChecks++;
                    if (ExpResolve.Judge(bl, repCond, r.Play, oreNow, craftNow, out _, out _) == ExpVerdict.Standing && r.Play - bl[bl.Count - 1].play1 > ExpResolve.WeakRetrySec * 16 + 1) staleWeak++;
                }
            foreach (var part in rep.Split(new[] { "\"note\":\"" }, StringSplitOptions.None).Skip(1))
            {
                string nt = part.Substring(0, part.IndexOf('"'));
                int tags = (nt.Contains("아직 풀리지 않은 막힘") ? 1 : 0) + (nt.Contains("다시 해 볼 만함") ? 1 : 0) + (nt.Contains("오래돼 지금은 모름") ? 1 : 0);
                if (tags > 1) mixedNotes++;
            }
            r.Event("plan_result", null, null, "llm", "{\"result\":\"accepted\",\"why\":\"synthetic\"}");
            var g = new object();
            r.GoalStart(g, replacing, key, kind, item, count, src, cond, 0);
            goals++; replacing = null;
            double dur = 60 + rnd.NextDouble() * 420;
            int needLv = need.TryGetValue(item, out int nl) ? nl : 0;
            int fights = rnd.Next(4);
            for (int i = 0; i < fights; i++)
            {
                r.Play += dur / (fights + 1);
                var en = new List<string> { enemies[rnd.Next(enemies.Length)] };
                bool together = rnd.NextDouble() < 0.5;
                r.Event("fight", r.Open?.run, en.Select(e => "enemy:" + e), "game", $"{{\"secs\":{rnd.Next(3, 40)},\"enemies\":{{\"{en[0]}\":{1 + rnd.Next(3)}}},\"with\":{(together ? "[\"호스트(호스트)\"]" : "[]")},\"together\":{(together ? "true" : "false")},\"downs\":0,\"boss\":\"\"}}");
                if (rnd.NextDouble() < (needLv > tier ? 0.25 : 0.06)) r.Down(en, "적 근처", Now(r.Play));
            }
            if (kind == "explore" || rnd.NextDouble() < 0.3) seen += rnd.Next(0, kind == "explore" ? 12 : 3);
            if (rnd.NextDouble() < 0.15) { var o = need.Keys.ElementAt(rnd.Next(need.Count)); if (o != "AncientOre" && o != "SolariteOre") ores[o]++; }
            if (rnd.NextDouble() < 0.02) stations++;
            if (rnd.NextDouble() < 0.1) { var ci = crafts[rnd.Next(crafts.Count)]; var cs = craftSt[ci]; craftSt[ci] = (Math.Max(0, cs.Item1 - 1), cs.Item2); }
            if (rnd.NextDouble() < 0.02) { var ci = crafts[rnd.Next(crafts.Count)]; craftSt[ci] = (craftSt[ci].Item1, 1); }
            if (rnd.NextDouble() < 0.1) { r.Items.Add("New" + goals); r.Event("item_first", r.Open?.run, new[] { "item:New" + goals }, "game", $"{{\"item\":\"New{goals}\",\"count\":1}}"); }
            if (rnd.NextDouble() < 0.005) r.Event("boss_kill", r.Open?.run, new[] { "enemy:Glurch" }, "game", "{\"boss\":\"Glurch\",\"with\":[\"호스트(호스트)\"]}");
            r.Play += dur / (fights + 1);
            _clock += (long)dur;
            string why; int got = count; double y = rnd.NextDouble();
            if (kind == "gather" && needLv > tier) { why = TOOL; got = 0; }
            else if (y < 0.03) why = "/ai stop";
            else if (y < 0.04) why = "3 consecutive failures: 시간 초과";
            else if (kind == "explore") why = y < 0.4 ? "nothing left to explore" : y < 0.45 ? WAIT : "replace";
            else if (kind == "gather" && y < 0.14) { why = NOSRC; got = rnd.Next(count); }
            else if (kind == "craft" && y < 0.24) { why = CRAFT; got = 0; }
            else why = "goal done";
            if (why == "replace") { lastKey = key; replacing = g; continue; }
            var end = Now(r.Play); end.ore = item.EndsWith("Ore") ? oreNow(item) : -1;   // as AIExperience: only ores are measured directly
            if (kind == "craft" && r.Open != null) { var cs = craftNow(item); r.Open.craftMissing = cs.Item1; r.Open.craftStation = cs.Item2; }
            r.GoalEnd(g, why, kind == "explore" || kind == "follow" ? -1 : got, end, AreaAt(r.Play));
            lastKey = key;
        }
        var endCond = Now(r.Play, true);
        var cands2 = llmGather.Select(o => "gather:" + o).Concat(new[] { "gather:AncientOre", "gather:SolariteOre", "explore", "follow" }).ToList();
        sw.Restart(); var r9 = new ExpRecorder(d, "w1", "claude") { Clock = () => _clock }; r9.Load(endCond); double lastLoad = sw.Elapsed.TotalMilliseconds;
        string final = r9.Report(endCond, "gather:SolariteOre", cands2, oreNow, craftNow);
        File.WriteAllText(Path.Combine(d, "final_report_part.json"), final);
        repMs.Sort(); repChars.Sort();
        long evBytes = Directory.GetFiles(d, "events*.jsonl").Sum(f => new FileInfo(f).Length);
        int evCount = Directory.GetFiles(d, "events*.jsonl").Sum(f => File.ReadLines(f).Count());
        Console.WriteLine($"  play {r9.Play / 3600:F1} h, goal runs {r9.Book.Rows.Count}, events {evCount} in {Directory.GetFiles(d, "events*.jsonl").Length} file(s) ({evBytes / 1024 / 1024.0:F1} MB), goals.jsonl {new FileInfo(r9.GoalsPath).Length / 1024} KB, exp_state.json {new FileInfo(r9.StatePath).Length} B");
        Console.WriteLine($"  load at restart: first {loadMs[0]:F1} ms, max {loadMs.Max():F1} ms, at 100 h {lastLoad:F1} ms ({loadMs.Count} restarts)");
        Console.WriteLine($"  report part: chars median {repChars[repChars.Count / 2]}, p95 {repChars[(int)(repChars.Count * 0.95)]}, max {repChars[repChars.Count - 1]}; build ms median {repMs[repMs.Count / 2]:F2}, p95 {repMs[(int)(repMs.Count * 0.95)]:F2}, max {repMs[repMs.Count - 1]:F2}");
        Console.WriteLine($"  outcomes: {string.Join(", ", r9.Book.Rows.GroupBy(x => x.outcome + (x.cls != "" ? "/" + x.cls : "")).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}");
        Console.WriteLine($"  final report part ({final.Length} chars):\n    {final}");
        string na = Note(final, "gather:AncientOre"), ns = Note(final, "gather:SolariteOre");
        Check(na.Contains("다시 해 볼 만함") && na.Contains("곡괭이 레벨 1→6") && !na.Contains("아직 풀리지"), $"AncientOre blocked at hours 2-3 with a weak pick: retry now: '{na}'");
        Check(ns.Contains("아직 풀리지 않은 막힘(도구 부족: 곡괭이 레벨이 그대로 6; 바뀐 것 없이 연속 2번)"), $"SolariteOre blocked twice now (report made at the core): '{ns}'");
        Check(staleWeak == 0, $"no block other than tool_weak stood past the longest wait ({staleChecks} checks over the play)");
        Check(mixedNotes == 0, $"no note carries two verdicts (every report of the play, {repChars.Count} reports)");
        Check(repChars[repChars.Count - 1] > 2200, "long history report is no longer truncated at 2200 characters");
        Check(r9.Book.Rows.Count == r9.Book.Rows.Select(x => x.run).Distinct().Count(), "no run twice after all restarts");
        Check(IdsOk(d), "event ids unique and increasing");
    }
}
