// S2b: the two stalls no live run has shown yet (L0, L1): the wait-only stall and a stall of a goal the upper LLM set.
// Offline, as far as the Unity-free code carries them: the values the ending place states (AIBrain.Send's stall
// branches, AIDecisions.cs "stalled: " + why with GoalEnd(…, "blocked", cls)) now decide the experience record, and the
// record reaches the experience part of the next report to the upper LLM (ExpReport.Build). The PLANNER send itself
// (trigger=goal_stalled, AIPlanner.OnGoalEnded) is Unity code and is checked live (L2).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class StallTests
{
    // the sentences as AIDecisions.cs makes them (rf-S2b: home/store/farm/cook :984, gather/craft :1006, explore :1017, only-wait :1034)
    const string WAIT = "stalled: 할 수 있는 행동이 기다리기뿐(5번 연속) · 잠시 뺀 대상: mine:3,4(길 없음)";
    const string GATHER_TOOL = "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 1개(닿는 것, 그중 도구 부족 1개)";
    const string GATHER_NOSRC = "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 0개(닿는 것), 탐험할 새 구역 없음(걷기·파기·기억한 탐색 끝 모두)";
    const string CRAFT_WAITNOTE = "stalled: 만들 방법이 없음 — 되풀이 실패로 뺀 행동: x(할 수 있는 행동이 기다리기뿐)";

    static long _clock = 1_800_000_000;
    static ExpCond C() => new ExpCond { toolLv = 1, weaponLv = 1, armorLv = 1, hpMax = 110, area = "Slime", cell = "0,0", seen = 100 };

    static (ExpRecorder r, List<string> logs) NewRec(string root, string name)
    {
        string dir = Path.Combine(root, name);
        var logs = new List<string>();
        var r = new ExpRecorder(dir, "w1", "claude") { Clock = () => _clock };
        r.Log = s => logs.Add(s);
        r.Load();
        return (r, logs);
    }

    // one goal run of the upper LLM ("llm:claude#4") that ends with the stated values; returns the recorded row
    static ExpRun End(ExpRecorder r, string key, string why, string outcome, string cls, bool compare)
    {
        var g = new object(); string kind = key.Split(':')[0]; string item = key.Contains(":") ? key.Split(':')[1] : "";
        r.GoalStart(g, null, key, kind, item, 5, "llm:claude#4", C());
        r.Play += 60; _clock += 60;
        bool ok = r.GoalEnd(g, why, outcome, cls, 0, C(), null, compare);
        return ok ? r.Book.Rows[r.Book.Rows.Count - 1] : null;
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "core-tests-stall");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);

        // ---- the wait-only stall of an LLM goal: blocked / only_wait, the sentence kept as the cause, the source kept
        {
            var (r, logs) = NewRec(root, "wait");
            var row = End(r, "gather:Wood", WAIT, "blocked", "only_wait", true);
            Program.Check(row != null && row.outcome == "blocked" && row.cls == "only_wait", $"wait-only stall recorded blocked/only_wait (got {row?.outcome}/{row?.cls})");
            Program.Check(row != null && row.cause.StartsWith("할 수 있는 행동이 기다리기뿐") && row.sourceRaw == "llm:claude#4", "wait-only stall: cause is the sentence after 'stalled:', LLM source kept");
            Program.Check(!logs.Any(l => l.Contains("MISMATCH")), "wait-only stall: the stated values agree with the sentence (no OUTCOME-MISMATCH)");
            string rep = ExpReport.Build(r.Book, C(), r.Play, "gather:Wood", new[] { "gather:Wood" });
            Program.Check(rep.Contains("\"key\":\"gather:Wood\"") && rep.Contains("\"blocked\":1") && rep.Contains("할 행동 없음"),
                $"the next report's experience part names the wait-only block of gather:Wood: {ExpRun.Trim(rep, 400)}");
            Program.Check(ExpResolve.Wait("only_wait", 1) == ExpResolve.WaitRetrySec, "a wait-only block is judged again after the short wait (10 min)");
        }

        // ---- an LLM gather stall: no stated cls, the sentence's content decides (tool_weak / no_source, review F6)
        {
            var (r, logs) = NewRec(root, "gather");
            var a = End(r, "gather:TinOre", GATHER_TOOL, "blocked", null, true);
            var b = End(r, "gather:Wood", GATHER_NOSRC, "blocked", null, true);
            Program.Check(a?.cls == "tool_weak" && b?.cls == "no_source", $"LLM gather stalls: cls from the sentence (got {a?.cls}, {b?.cls})");
            Program.Check(!logs.Any(l => l.Contains("MISMATCH")), "LLM gather stalls: no OUTCOME-MISMATCH");
            string rep = ExpReport.Build(r.Book, C(), r.Play, "gather:TinOre", new[] { "gather:TinOre", "gather:Wood" });
            Program.Check(rep.Contains("도구 부족") && rep.Contains("모을 곳 못 찾음"), "the next report names both blocks (도구 부족, 모을 곳 못 찾음)");
        }

        // ---- S2b: the stated values decide. The known edge (a craft note that itself says "기다리기뿐") is recorded as
        // the craft stall's cannot_craft, not the sentence's only_wait; with the test switch it is logged, without not.
        {
            var (r, logs) = NewRec(root, "edge");
            var on = End(r, "craft:TinMiningPick", CRAFT_WAITNOTE, "blocked", "cannot_craft", true);
            Program.Check(on?.outcome == "blocked" && on?.cls == "cannot_craft", $"stated cannot_craft decides over the sentence's only_wait (got {on?.cls})");
            Program.Check(logs.Count(l => l.StartsWith("OUTCOME-MISMATCH goal text=blocked/only_wait code=blocked/cannot_craft")) == 1, "with the test switch the disagreement is logged once");
            int before = logs.Count;
            var off = End(r, "craft:TinMiningPick", CRAFT_WAITNOTE, "blocked", "cannot_craft", false);
            Program.Check(off?.cls == "cannot_craft" && logs.Count == before, "without the test switch: same record, nothing logged");
            // an outcome stated by the place wins too (the cause still comes from the sentence)
            var ex = End(r, "explore", "nothing left to explore", "exhausted", "", false);
            Program.Check(ex?.outcome == "exhausted" && ex.cls == "" && ex.cause == "", "explore end: exhausted, empty cls and cause");
            var sys = End(r, "gather:Wood", "3 consecutive failures: HTTP 500 ", "system", null, true);
            Program.Check(sys?.outcome == "system" && sys.cause == "3 consecutive failures: HTTP 500 " && sys.cls == "", "system end: stated outcome, cause from the sentence");
        }
        Directory.Delete(root, true);
    }
}
