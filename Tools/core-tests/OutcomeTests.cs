// S2: AIOutcomes — the sentence rules give the same answers as before for the sentences the mod makes, the values set
// next to them agree, and the goal-end values each ending place states agree with ExpRun.Classify of its sentence
// (refactor plan 2-계획.md S2). Sentences are copied from the producers (file:line at 3c3adbe in the comments).
using System;

public static class OutcomeTests
{
    public static void Run()
    {
        // ---- skill ends (AIDecisions.cs:453/:466/:473 rules; producers AIBodySkills.cs nav :1561/:1589/:1661/:1714 via
        // GotoSkill :1982, AIBody.Tick :114, AttackSkill :2281, TunnelSkill AIExplore.cs :1053 + Summary :1066-1071)
        var nav = new (string reason, SkillEnd code)[]
        {
            ("시간 초과 (길찾기 game-bfs)", SkillEnd.Timeout),
            ("막힘(-61,52에서 세 번 못 움직임) (길찾기 game-bfs)", SkillEnd.Stuck),
            ("길 없음(게임 길찾기: 타일·물체 고려) (길찾기 game)", SkillEnd.NoPath),
            ("길 없음(막힌 칸 2개를 피하면) (길찾기 tile)", SkillEnd.NoPath),
            ("길 없음 (길찾기 tile)", SkillEnd.NoPath),
        };
        foreach (var n in nav)
            Program.Check(AIOutcomes.NoPathText(n.reason) && AIOutcomes.NoPath(n.code), $"nav failure '{n.reason}': no-path rule by sentence and by value");
        foreach (var other in new[] { "목표 칸이 걸을 수 없는 곳(wall)", "오류: NullReferenceException", "새 기술 공격(으)로 바뀜", "도착 (3,4), 길찾기 game", "AI가 쓰러짐(체력 0)" })
            Program.Check(!AIOutcomes.NoPathText(other) && !AIOutcomes.NoPath(SkillEnd.None), $"other walk end '{other}': no no-path rule");
        Program.Check(AIOutcomes.DownText("AI가 쓰러짐(체력 0)") && AIOutcomes.DownText("AI가 쓰러짐 · 벽 2개 뚫음 · 가방 +Wood 3"), "down sentences (AIBody.Tick, TunnelSkill summary)");
        Program.Check(!AIOutcomes.DownText("다가가기 실패: 길 없음") && !AIOutcomes.DownText("대상이 안 보임 (체력 50→20)"), "other failures are not downs");
        Program.Check(AIOutcomes.KillText("MushroomEnemy 처치 (체력 120→0)"), "the attack's kill sentence");
        foreach (var notKill in new[] { "대상이 안 보임 (체력 50→20)", "시간 초과 (체력 50→20)", "대상에게 갈 길 없음 (길 없음, 체력 50→50)" })
            Program.Check(!AIOutcomes.KillText(notKill), $"attack end '{notKill}' is not a kill");
        // the attack's own timeout sentence starts like a nav timeout: the no-path rule is only read for walks, and the
        // value for it stays None (no mismatch: the brain compares only where it reads the rule)
        Program.Check(AIOutcomes.NoPathText("시간 초과 (체력 50→20)"), "(the attack's timeout sentence would match the walk rule; the brain reads that rule only for walks)");

        // ---- emergencies (AIDecisions.cs:694 EmergencyNow, :826 health drop) and other triggers
        foreach (var t in new[] { "긴급: 체력 감소(100→80)", "긴급: 새 적 MushroomEnemy이(가) 바로 옆(1.2칸)", "체력 감소(100→85)" })
            Program.Check(AIOutcomes.EmergencyText(t), $"emergency trigger '{t}'");
        foreach (var t in new[] { "새 적 보임(Slime 6.0칸)", "전투 중 계속 다시 판단", "기술 끝(공격 완료)", "재시도(HTTP 500 )", "곧 끝남: 지금 하는 '걷기'(goto:3,4)이 약 0.3초 뒤 끝남. 끝난 뒤 이어서 할 행동을 고른다", "자기 방어: 체력이 줄어듦(-5)" })
            Program.Check(!AIOutcomes.EmergencyText(t), $"not an emergency '{t}'");

        // ---- rejected upper-LLM answers (AIPlanner.Check)
        foreach (var r in new[] { "오래된 답: 목표 수정 번호가 바뀜(2→3)", "오래된 답: 기다리는 동안 쓰러짐", "오래된 답: 190초" })
            Program.Check(AIOutcomes.StaleRejectText(r), $"stale reject '{r}'");
        foreach (var r in new[] { "답 JSON이 없음", "답의 for가 보낸 보고와 다름(report -1/7, goal_rev -1/3)", "모르는 decision 'go'", "보고의 can_do.gather에 없는 품목 'IronOre'", "개수 범위 밖(40)" })
            Program.Check(!AIOutcomes.StaleRejectText(r), $"not stale '{r}'");

        // ---- tool-short block note: the note is the pieces joined with " / " (AIDecisions.cs:2634)
        var pieces = new[] { "보이는 목표 품목 벽 WallStoneBlock+TinOre (3,4): 도구 부족(추정 피해 20 ≤ 감소 30, 손에 든 것)", "코어로 물러나기: 열린 길 없음(벽 3개)" };
        Program.Check(AIOutcomes.ToolShortText(string.Join(" / ", pieces)) == Array.Exists(pieces, AIOutcomes.ToolShortText), "tool-short: the joined note and the pieces agree");
        var none = new[] { "떨어진 물건 줍기: 가방(손 칸 포함)에 빈 칸이 없고", "코어로 물러나기: 열린 길 없음(벽 3개)" };
        Program.Check(!AIOutcomes.ToolShortText(string.Join(" / ", none)) && !Array.Exists(none, AIOutcomes.ToolShortText), "no tool-short piece: neither");

        // ---- goal ends: what each ending place states vs ExpRun.Classify of its sentence (AIDecisions.cs ClearGoal callers)
        var goals = new (string where, string why, string outcome, string cls)[]
        {
            ("goal done :705/:722/:748", "goal done", "completed", null),
            ("/ai stop (AIBodySkills :308)", "/ai stop", "cancelled", null),
            ("/ai goal 없음 :498", "/ai goal 없음", "cancelled", null),
            ("cost cap :853", "cost cap", "system", null),
            ("no api key :1125", "no api key", "system", null),
            ("3 failures :1465", "3 consecutive failures: HTTP 500 ", "system", null),
            ("free time over :439", "free time over", "unknown", null),
            ("explore ended :1196", "nothing left to explore", "exhausted", ""),
            ("home stall :1159", "stalled: 기지로 갈 방법이 없음 — 기지까지 아는 길 없음(도구 부족 벽 2개)", "blocked", "no_place"),
            ("store stall :1159", "stalled: 넣을 방법이 없음 — 기지에 자리 있는 상자 없음, 만들 재료 Wood 2 더", "blocked", "no_place"),
            ("craft stall :1183", "stalled: 만들 방법이 없음 — TinMiningPick: Furnace에서 만듦 · 재료 모으기: 보이는 목표 품목 벽 1개(닿는 것, 그중 도구 부족 1개)", "blocked", "cannot_craft"),
            ("craft stall with left-out :1183", "stalled: 만들 방법이 없음 — 되풀이 실패로 30초 동안 뺀 행동: dig:3,4(막힘(3,4에서 세 번 못 움직임)) / Furnace(…)", "blocked", "cannot_craft"),
            ("only wait :1209", "stalled: 할 수 있는 행동이 기다리기뿐(5번 연속) · 잠시 뺀 대상: mine:3,4(길 없음)", "blocked", "only_wait"),
            ("gather stall, tool (content decides) :1183", "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 1개(닿는 것, 그중 도구 부족 1개)", "blocked", null),
            ("gather stall, no source (content decides) :1183", "stalled: 모을 방법이 없음 — 보이는 목표 품목 벽 0개(닿는 것), 탐험할 새 구역 없음(걷기·파기·기억한 탐색 끝 모두)", "blocked", null),
            ("farm stall (content decides) :1159", "stalled: 밭 일을 할 방법이 없음 — 씨앗 없음, 괭이 없음", "blocked", null),
        };
        foreach (var g in goals)
        {
            ExpRun.Classify(g.why, out string o, out string c, out _);
            bool ok = o == g.outcome && (g.cls == null || g.cls == c);
            Program.Check(ok, $"goal end {g.where}: stated {g.outcome}/{g.cls ?? "(sentence)"} = classified {o}/{c}");
        }
        // a stated cannot_craft would disagree with the sentence only if the craft note itself said "기다리기뿐" or a store /
        // home phrase (Classify checks those first): shown here, logged as OUTCOME-MISMATCH in play if it ever happens
        ExpRun.Classify("stalled: 만들 방법이 없음 — 되풀이 실패로 뺀 행동: x(할 수 있는 행동이 기다리기뿐)", out _, out string odd, out _);
        Program.Check(odd == "only_wait", $"(known edge: a craft note with '기다리기뿐' classifies as {odd}; S2b records the stated cannot_craft, StallTests)");
    }
}
