using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class ChoiceFactsTests
{
    sealed class Route
    {
        public bool explosive;
        public Route alt;
        public Route(bool explosive, Route alt = null) { this.explosive = explosive; this.alt = alt; }
    }
    static string Risk(Route route, bool detour) => AIWallRisk.RouteText(route, detour, p => p.alt, p => AIWallRisk.Text(p.explosive));
    sealed class Pick
    {
        public string key;
        public bool goal, food, automatic, eligible = true;
        public Pick(string key, bool goal = false, bool food = false, bool automatic = false, bool eligible = true)
        { this.key = key; this.goal = goal; this.food = food; this.automatic = automatic; this.eligible = eligible; }
    }
    static string Select(bool free, params Pick[] picks) => string.Join(",", AIPickupOptions.Select(picks,
        x => x.goal, x => x.food, x => x.automatic, x => x.key, x => x.eligible, free).Select(x => x.key));

    public static void Run()
    {
        Program.Check(Select(false, new Pick("dirt"), new Pick("seed"), new Pick("wood", goal:true), new Pick("berry", food:true)) == "wood,berry,dirt,seed",
            "goal item and hunger food stay first without cutting the other viable pickups");
        Program.Check(Select(true, new Pick("dirt"), new Pick("seed"), new Pick("berry", food:true)) == "berry",
            "free time still offers hunger food only, even after two filler drops");
        Program.Check(Select(false, new Pick("bad", goal:true, eligible:false), new Pick("wood", goal:true), new Pick("berry", food:true)) == "wood,berry",
            "a failed/ineligible goal pickup cannot occupy the reserved slot");
        Program.Check(Select(false, new Pick("auto-goal", goal:true, automatic:true), new Pick("filler")) == "auto-goal,filler",
            "goal need stays ahead of ordinary actionable filler even when the goal drop arrives automatically");
        Program.Check(Select(false, new Pick("auto-goal", goal:true, automatic:true), new Pick("walk-goal", goal:true), new Pick("berry", food:true)) == "walk-goal,berry,auto-goal",
            "within the same need, prefer the item requiring an approach; no forced pickup execution");
        Program.Check(Select(false, new Pick("both", goal:true, food:true), new Pick("other", food:true)) == "both,other",
            "one item can satisfy both needs without duplicate actions");
        Program.Check(Select(false, new Pick("tile", goal:true), new Pick("tile", goal:true), new Pick("next", goal:true)) == "tile,next",
            "multiple drops sharing an action key use one slot");
        Program.Check(Select(false, new Pick("auto", automatic:true), new Pick("walk"), new Pick("walk2")) == "walk,walk2,auto"
            && Select(false, new Pick("auto", automatic:true)) == "auto", "automatic drops stay available when there is room");
        Program.Check(Select(true, new Pick("not-food")) == "" && Select(false, new Pick("bad", eligible:false)) == "",
            "no admissible pickup means no candidate");
        var manyPicks = Enumerable.Range(0, 24).Select(i => new Pick("drop:" + i)).ToArray();
        Program.Check(Select(false, manyPicks).Split(',').SequenceEqual(manyPicks.Select(x => x.key)),
            "all twenty-four distinct viable pickup targets survive selection without a count quota");

        float retreat = AIMoveEstimate.Seconds(14), explore = AIMoveEstimate.Seconds(13);
        Program.Check(retreat > explore && retreat < 2.5f && explore > 2.2f,
            "logged #30: 14 walking tiles cost slightly more than 13, under the same speed");
        Program.Check(Math.Abs(AIMoveEstimate.Seconds(13, 4) - explore - 2f) < 0.001f,
            "mining adds its own half-second-per-hit cost; walking speed changes only walking");
        Program.Check(Math.Abs(AITunnelSearch.EstTiles(10, 5) - 16f) < 0.001f && AITunnelSearch.OfferDetour(25f, 15f),
            "legacy route choice score and detour threshold are unchanged");
        var note = AIBaseGuard.DetourFact(false, 14, retreat, 13, explore, null);
        Program.Check(note.Contains("14칸·약 2초") && note.Contains("13칸·약 2초"),
            "detour display consumes seconds without applying the old 2.4 conversion");

        string scripts = Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts");
        string cand = File.ReadAllText(Path.Combine(scripts, "AIBrain.Candidates.cs"));
        string mine = File.ReadAllText(Path.Combine(scripts, "AIMineSkill.cs"));
        string tunnel = File.ReadAllText(Path.Combine(scripts, "AITunnel.cs"));
        string wall = File.ReadAllText(Path.Combine(scripts, "AITileReader.cs"));
        // 2026-11-08 이동·전달 개선 3/4 (source-level): object count/position changes no longer cancel a prepared batch.
        string tunnelLf = tunnel.Replace("\r\n", "\n");
        int validation = tunnelLf.IndexOf("public bool StepValidation(int workBudget)");
        int validationEnd = validation < 0 ? -1 : tunnelLf.IndexOf("ValidationCompleted = true;\n                        break;", validation);
        string validationBody = validationEnd < 0 ? "" : tunnelLf.Substring(validation, validationEnd - validation);
        Program.Check(validationEnd >= 0 && !tunnelLf.Contains("MarkChanged(\"장애물 범위\")") && !tunnelLf.Contains("MarkChanged(\"장애물 위치\")")
            && !tunnelLf.Contains("MarkChanged(\"장애물 재관측 실패\")")
            && validationBody.Contains("ReadFact(t, objBlocked, false)") && validationBody.Contains("MarkChanged(\"타일 (\"")
            && validationBody.Contains("MarkChanged(\"검증 중 위치\")"),
            "batch validation ignores moving objects (re-reads read tiles with the captured object set) but still cancels on terrain, position and tool change");
        // 2026-11-08 item 5 (source-level): call pacing in Tick and Send.
        string loop = File.ReadAllText(Path.Combine(scripts, "AIBrain.Loop.cs")).Replace("\r\n", "\n");
        Program.Check(loop.Contains("float gap = _inCombat || _treeStepNext ? 0f : running && !standing ? Settings().minGapSeconds : Settings().idleGapSeconds;")
            && loop.Contains("bool standing = AIBody.Current is TravelSkill run && !run.Moving;")
            && !loop.Contains("CallRateOk") && !loop.Contains("Settings().maxCallsPerMinute") && !loop.Contains("Settings().combatMaxCallsPerMinute") && !loop.Contains("Settings().combatMinGapSeconds")
            && !loop.Contains("now + ExecutionReviewSeconds") && loop.Contains("_treeStepNext = false;")
            && System.Text.RegularExpressions.Regex.Matches(loop, "_treeStepNext = true").Count == 4,
            "peaceful floors kept; no floor in combat or after a tree step (4 navigation points); no per-minute caps; no 5 s hold after a rejected completion");
        // 토큰 절감 ② (2026-11-08): one shared description, places listed, a different target in full
        string g = AIGatheringRules.CompactGroup(new List<(string, int, int, float)> {
            ("캐기 — 벽 WallDirtBlock (14,6) 3.2칸, 벽 체력 40. 부수면 WallDirtBlock 이(가) 나올 것으로 봄.", 14, 6, 3.2f),
            ("캐기 — 벽 WallDirtBlock (15,7) 4.0칸, 벽 체력 40. 부수면 WallDirtBlock 이(가) 나올 것으로 봄.", 15, 7, 4.0f),
            ("캐기 — 벽 WallDirtBlock (16,7) 5.1칸, 벽 체력 80. 부수면 WallDirtBlock 이(가) 나올 것으로 봄.", 16, 7, 5.1f) });
        Program.Check(g.Contains("공통 설명: 캐기 — 벽 WallDirtBlock (위치), 벽 체력 40.") && g.Contains("대상: (14,6) 3.2칸, (15,7) 4.0칸")
            && g.Contains("조건이 다른 대상: [캐기 — 벽 WallDirtBlock (16,7) 5.1칸, 벽 체력 80.") && System.Text.RegularExpressions.Regex.Matches(g, "나올 것으로 봄").Count == 2,
            "gather group: the shared sentence once, places as a list, a target with other facts in full");
        // 재검수 R2: a coordinate that is not the target's own place (a base door) is kept and keeps targets apart
        string door = AIGatheringRules.CompactGroup(new List<(string, int, int, float)> {
            ("캐기 — 벽 WallDirtBlock (3,4) 2.0칸. 기지 경계 벽 (11,5) 옆.", 3, 4, 2f),
            ("캐기 — 벽 WallDirtBlock (3,5) 2.5칸. 기지 경계 벽 (99,99) 옆.", 3, 5, 2.5f) });
        Program.Check(door.Contains("(11,5)") && door.Contains("(99,99)") && !door.Contains("기지 경계 벽 (위치)"),
            "gather group: other coordinates are not merged into the place list (재검수 R2)");
        // 재검수 R1·R3·R4·R5 (source-level)
        string candSrc = cand.Replace("\r\n", "\n"), gatherSrc = File.ReadAllText(Path.Combine(scripts, "AIGathering.cs"));
        Program.Check(loop.Contains("            SelfDefence(server, player, now);\n        }") && !loop.Contains("AbortRequest(\"no goal\")")
            && loop.Contains("if (AIEdge.PlanActive)   // 재검수 R1") && loop.Contains("if (Goal.kind == GoalKind.None) NoGoalWatch(server, player, hpNow);")
            && loop.Contains("MarkIssue(\"새 적 보임\"); _needCall = true; _needReason = $\"새 적 보임({enemy.id}") && loop.Contains("MarkIssue(\"체력 감소\"); _needCall = true; _needReason = $\"체력 감소({_hpAtLastCall}→{hpNow}, 목표 없음)")
            && loop.Contains("위협이 보이지 않으면 자기 방어는 저절로 끝나고") && loop.Contains("AIBody.Stop(server, player, now, \"자기 방어 끝: 위협이 보이지 않음(목표 없음)\")")
            && candSrc.Contains("if (gather && candidate.itemId == ObjectID.None) candidate.itemId = AIGathering.ProductOf(server, candidate);")
            && gatherSrc.Contains("public static ObjectID ProductOf(World server, AICandidate candidate)"),
            "no-goal issue watches run (base edge, new enemy, health drop, back up); self-defence text and end stop; catalog items filled");
        // 토큰 절감 ③: the last result keeps the last 3 observations, the repeated discovery tail once, ≤240 chars
        var obs = new List<string>();
        for (int i = 0; i < 5; i++) obs.Add("새로 보이는 물체 Worm @" + i + ",0." + AIFieldText.DiscoveryTail);
        string r3 = AIFieldText.Result("묶음 채집", "Done", obs);
        Program.Check(r3.Contains("앞선 관측 2건 생략") && r3.Contains("@4,0") && !r3.Contains("@1,0")
            && System.Text.RegularExpressions.Regex.Matches(r3, "원래 작업을 계속하면서").Count == 1 && r3.Length <= AIFieldText.ResultChars + 1
            && AIFieldText.Choice("gather_group", "WallDirtBlock", 1, 2, 12) == "묶음 채집 WallDirtBlock 12곳" && AIFieldText.Choice("mine", "CopperOre", 3, 4, 1) == "캐기 CopperOre (3,4)",
            "expedition record: short chosen-work name, last results bounded with the repeated tail once");
        // 토큰 절감 ④: the last 20 results in full, earlier ones counted per kind
        var recent = new RecentResults();
        for (int i = 0; i < 25; i++) recent.Add(i < 3 ? "걷기 실패" : "통로 탐색 완료", "결과 " + i);
        string rt = recent.Text();
        Program.Check(recent.Count == 25 && rt.StartsWith("(그 앞 5건: 걷기 실패 3, 통로 탐색 완료 2)") && rt.Contains("결과 5") && rt.Contains("결과 24")
            && !rt.Contains("결과 4 ") && !rt.EndsWith("결과 4") && recent.Last == "결과 24",
            "recent results: last 20 in full, the earlier 5 as counts per kind");
        // valuable-kind file (user decision 2026-11-08): round trip; missing, broken or other-version files read as defaults
        var saved = ValuableStore.Write(new Dictionary<string, bool> { ["TinOre"] = true, ["CopperOre"] = false });
        Program.Check(saved == "{\"version\":1,\"kinds\":{\"CopperOre\":false,\"TinOre\":true}}" && ValuableStore.TryRead(saved, out var back) && back.Count == 2 && !back["CopperOre"] && back["TinOre"]
            && ValuableStore.TryRead("{\"version\":1,\"kinds\":{}}", out var empty) && empty.Count == 0
            && !ValuableStore.TryRead("", out _) && !ValuableStore.TryRead("{\"version\":1,\"kinds\":{\"CopperOre\":fal", out var torn) && torn.Count == 0
            && !ValuableStore.TryRead("{\"version\":2,\"kinds\":{\"CopperOre\":false}}", out _) && !ValuableStore.TryRead("{\"version\":1,\"kinds\":{\"CopperOre\":\"no\"}}", out _),
            "valuable-kind file: round trip; empty, torn, other-version and bad-value files are not used");
        // 2026-11-08 항목6 (source-level): no-goal idle asks only for issues; moving runs pace resource sightings.
        Program.Check(loop.Contains("if (AIRunGate.IdleHold(running, Goal.kind == GoalKind.None, _inCombat,")
            && loop.Contains("_issuePending, _treeStepNext, ChoiceViewOpen))")   // merge review G-1: one open-view judgment, the open chest included
            && loop.Contains("private static bool ChoiceViewOpen => AIInventoryMenu.Active || AILife.Cursor.Open || AIExpedition.Active || AIChestMode.Open;")
            && !loop.Contains("IsIdleIssue(_needReason)") && !loop.Contains("GoalKind.Free")
            && loop.IndexOf("TravelWatch(server, AIObservation.Take(server, player, true), now);") < loop.IndexOf("        if (_req != null)\n")
            && loop.Contains("AIExpedition.LowerSent(cands);") && loop.Contains("AIExpedition.LowerAnswered(chosen, now);")
            && loop.Contains("_building?.Query, AIBody.Current is TravelSkill run && run.Moving)"),
            "idle no-goal gate before sending; travel sightings, offered kinds and answers wired into the brain loop");
        Program.Check(!cand.Contains("i < o.items.Count && lItem.Count < 2") && cand.Contains("c => c.key, Doable, false")
            && cand.IndexOf("BagHasRoomFor", cand.IndexOf("public IEnumerable<int> ItemsSteps()")) < cand.IndexOf("AIPickupOptions.Select"),
            "production scans all drops, rejects bag-full before selection, applies failed/body filter before reserving slots");
        Program.Check(wall.Contains("HasComponent<IsExplosiveCD>") && wall.Contains("ignoreExploding")
            && cand.Contains("AIWallRisk.RouteText(plan, c.digDetour") && cand.Contains("AIWallData.RiskText(server, obj)")
            && mine.Contains("AIWallData.RiskText(server, _wallObj)") && tunnel.Contains("남은 벽 {plan.walls.Count}개{plan.RiskText(server)}"),
            "known explosive prefab fact is wired into direct/route candidates and both continue descriptions");
        Program.Check(AIWallRisk.Text(false) == "" && AIWallRisk.Text(true).Contains("피해량·범위 미확인")
            && AIWallRisk.Text(true).Contains("물러나는 동작이 없음"),
            "hazard text states uncertainty and actual execution limitation without asserting damage numbers");
        Program.Check(Risk(new Route(true), false) == AIWallRisk.Text(true),
            "R1: ordinary route still warns when there is no alternative (home/goto/place can have edge permissions)");
        var dangerThenSafe = new Route(true, new Route(false));
        Program.Check(Risk(dangerThenSafe, false) == AIWallRisk.Text(true) && Risk(dangerThenSafe, true) == "",
            "R1: dangerous main route / safe alternative each describe their own risk");
        var safeThenDanger = new Route(false, new Route(true));
        Program.Check(Risk(safeThenDanger, false) == "" && Risk(safeThenDanger, true) == AIWallRisk.Text(true),
            "R1: safe main route / dangerous alternative do not contaminate each other's facts");
        Program.Check(Risk(null, false) == "" && Risk(new Route(true), true) == "",
            "R1: missing selected route does not borrow another route's hazard fact");
        Program.Check(cand.Contains("PlanC(server, player, c.tile, out _, c.digDetour)")
            && !cand.Contains("PlanC(server, player, c.tile, out _, c.digEdgeOk != null)"),
            "R1: production route fact selects with digDetour, never the permitted-edge list");
        Program.Check(!Directory.GetFiles(scripts, "*.cs").Any(p => File.ReadAllText(p).Contains("/ 2.4f")),
            "all game-facing walking ETA producers have left the old speed behind");
    }
}
