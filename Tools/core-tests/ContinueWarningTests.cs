// CW: the close-fight "continue" of an action that ends before this request's answer (AIContinueText, work
// 2026-11-06-계속하기-경고-문구.md). The text sent offline as variant V9 (59 logged close-fight requests) is pinned here,
// so the C# text and the measured one stay the same; the old remaining-time sentence is kept for the other actions.
using System;
using System.IO;

public static class ContinueWarningTests
{
    public static void Run()
    {
        Program.Check(AIContinueText.EndsFirst(0.05f, 0.30f) && !AIContinueText.EndsFirst(0.40f, 0.30f) && !AIContinueText.EndsFirst(-1f, 0.30f) && !AIContinueText.EndsFirst(0.30f, 0.30f),
            "EndsFirst: only a known remaining time below the answer time (unknown -1 and equal are not)");
        Program.Check(AIContinueText.RemainingNote(0.73f, 0.30f) == " 남은 시간: 이 행동은 약 0.73초 뒤 끝남(이 판단의 답은 약 0.30초 뒤 옴).",
            "RemainingNote: the old sentence for an action that does not end first");
        // E232 (decisions-182701.log): a short move ending in 0.05 s, answer in 0.30 s
        string head = AIContinueText.EndsFirstHead("방금(0.6초 전) 시작한", "짧은 이동", "step:frontright:1", "", "적 기준 오른쪽 앞(대각) 1칸: 지금까지 1.3칸 옴, 도착지 (-4.6,7.2)까지 0.3칸 남음", "", 0.05f, 0.30f);
        Program.Check(head == "계속하기 — 답이 오기 전에 끝남: 이 행동은 약 0.05초 뒤 끝나고 이 판단의 답은 약 0.30초 뒤 옴. 그래서 이 답이 올 때는 이어 갈 행동이 남아 있지 않음: 이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서 있음. 이어 갈 행동이 없으므로 '지금 하는 행동이 여전히 알맞으면 이어 간다'는 이 선택지에 해당하지 않음. 방금(0.6초 전) 시작한 '짧은 이동'(step:frontright:1) — 이 답이 오기 전에 끝남 (적 기준 오른쪽 앞(대각) 1칸: 지금까지 1.3칸 옴, 도착지 (-4.6,7.2)까지 0.3칸 남음).",
            "EndsFirstHead: the measured text (V9) for a short move: warning first, no '이어 하기', no switching cost");
        // no progress note, a motion note (a swing in progress)
        string head2 = AIContinueText.EndsFirstHead("방금(0.3초 전) 시작한", "한 번 치기", "hit:31042", "", "", " 지금 동작: 휘두르는 중.", 0.05f, 0.32f);
        Program.Check(head2.EndsWith("방금(0.3초 전) 시작한 '한 번 치기'(hit:31042) — 이 답이 오기 전에 끝남. 지금 동작: 휘두르는 중.") && !head2.Contains("바꾸는 비용") && !head2.Contains("이어 하기"),
            "EndsFirstHead: no progress -> no parentheses; the motion note follows as before");
        Program.Check(AIContinueText.ForecastLabel == " 계속하면: " && AIContinueText.ForecastLabelEndsFirst == " 이 행동이 끝날 때(이 답이 오기 전): ",
            "forecast labels: the old one, and the one when the action ends first");
        // the brain uses it (source check: the old inline warning is gone, the new calls are there)
        string cands = File.ReadAllText(Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIBrain.Candidates.cs"));
        Program.Check(!cands.Contains("— 답이 오기 전에 끝남: 이 '계속하기'를 고르면") && cands.Contains("AIContinueText.EndsFirstHead(") && cands.Contains("AIContinueText.RemainingNote(")
            && cands.Contains("endsFirst ? AIContinueText.ForecastLabelEndsFirst : AIContinueText.ForecastLabel"),
            "Candidates.cs: the close-fight continue text comes from AIContinueText");
        // S2 (code review): the continue head every other case gets (non-close-fight, or a close-fight action that does not end
        // first) is the inline text of Continue(), unchanged since 0869165
        Program.Check(cands.Contains("desc = $\"계속하기 — {since} '{cur.Name}'({curKey}){cExplore} 이어 하기{(string.IsNullOrEmpty(prog) ? \"\" : \" (\" + prog + \")\")}.{motion} 바꾸는 비용: 다른 행동을 고르면 지금 동작이 끝나는 대로 이 일은 멈추고, 지금까지의 진행은 이어지지 않으며 새 행동은 처음부터(다가가기·무기 들기·길 찾기부터) 시작함.\" });")
            && cands.Contains("cExplore = cur.Tag.StartsWith(\"explore:\") ? $\"({cur.Tag.Substring(8)}쪽 탐험 걷기)\" : \"\";")
            && cands.Contains("string since = ran < 1f ? $\"방금({ran:F1}초 전) 시작한\" : $\"{ran:F0}초째 하고 있는\";"),
            "Candidates.cs: the ordinary continue head (since, name, key, explore, '이어 하기', progress, motion, switching cost) is the old text");
        // S1 (code review): the cut-down places people read show the action first again; every other text unchanged
        string full = head + " 진행: 지금까지 1.3칸 옴, 남은 0.3칸(약 0.05초)." + AIContinueText.ForecastLabelEndsFirst + "시간: 끝날 때까지 약 0.05초";
        string shown = AIContinueText.ForShow(full);
        Program.Check(shown.StartsWith("계속하기 — 방금(0.6초 전) 시작한 '짧은 이동'(step:frontright:1) — 이 답이 오기 전에 끝남 (적 기준 오른쪽 앞(대각) 1칸")
            && shown.EndsWith("이 선택지에 해당하지 않음.") && shown.Contains("답이 오기 전에 끝남: 이 행동은 약 0.05초 뒤 끝나고") && shown.Length == full.Length,
            "ForShow: the ends-first text shows the action first, the warning after (same characters, moved)");
        string ordinary = "계속하기 — 방금(0.2초 전) 시작한 '짧은 이동'(step:away:1) 이어 하기 (적 기준 뒤(멀어지기) 1칸: …). 바꾸는 비용: … 남은 시간: 이 행동은 약 0.40초 뒤 끝남(이 판단의 답은 약 0.30초 뒤 옴).";
        Program.Check(AIContinueText.ForShow(ordinary) == ordinary && AIContinueText.ForShow("공격 — 적 MushroomBrute") == "공격 — 적 MushroomBrute" && AIContinueText.ForShow(null) == null,
            "ForShow: any other text (ordinary continue, other options, null) is returned unchanged");
        string loop = File.ReadAllText(Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIBrain.Loop.cs"));
        Program.Check(loop.Contains("options={cands.Count} (full facts in request; decisionlog.on records them)") && loop.Contains("Trim(AIContinueText.ForShow(chosen.desc), 80)")
            && loop.Contains("AIVitals.NoteChoice(Trim(AIContinueText.ForShow(c.desc), 50));") && loop.Contains("$\"AI 판단: {Trim(AIContinueText.ForShow(c.desc), 80)}\""),
            "Loop.cs: SEND summarizes count; RESULT, down record and AI chat use ForShow while request retains every fact");
        Program.Check(loop.Contains("Append(Q(cands[i].desc))") && !loop.Contains("Q(AIContinueText.ForShow"),
            "Loop.cs: the request JSON is built from the text as made (not ForShow)");
    }

}
