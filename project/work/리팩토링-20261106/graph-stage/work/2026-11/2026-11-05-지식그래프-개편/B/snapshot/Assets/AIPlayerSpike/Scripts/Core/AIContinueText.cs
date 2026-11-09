// AIContinueText — the close-fight "continue" option of a short action that ends before this request's answer comes
// (work 2026-11-06-계속하기-경고-문구.md, session 코어키퍼-AI-캐릭터). Unity-free (Tools/core-tests, group CW).
//
// The fact was already in the option ("— 답이 오기 전에 끝남: 이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서
// 있음"), at its end, after "이어 하기" and a switching-cost sentence that argue for continuing; the model chose
// "continue" in 19 of 20 such logged requests. Policy unchanged (combat stage 2: facts in, the model chooses; the option
// stays, nothing is chosen for the model). Only the wording and the place of this option's text change, and only when the
// action ends first: the warning comes first with both times, the head says the action ends before the answer instead of
// "이어 하기", the switching-cost sentence (moot when nothing is left to carry on) is left out, the instruction's "keep the
// current action if it still fits" is said not to apply, and the forecast is labelled as what happens when the action ends.
// Offline (59 logged close-fight requests, variant V9): continue chosen 19/20 -> 6/20; the 39 others unchanged.
// An action that does not end first keeps the old text (RemainingNote, ForecastLabel).
public static class AIContinueText
{
    public const string ForecastLabel = " 계속하면: ";
    public const string ForecastLabelEndsFirst = " 이 행동이 끝날 때(이 답이 오기 전): ";

    // rem = seconds the running action still takes (-1 unknown), ans = seconds until this request's answer comes
    public static bool EndsFirst(float rem, float ans) => rem >= 0f && rem < ans;

    // The old remaining-time sentence, for an action that does not end before the answer (unchanged wording).
    public static string RemainingNote(float rem, float ans) => $" 남은 시간: 이 행동은 약 {F2(rem)}초 뒤 끝남(이 판단의 답은 약 {F2(ans)}초 뒤 옴).";

    // The whole text up to the progress part, for an action that ends first. since = "방금(0.6초 전) 시작한" | "3초째 하고
    // 있는"; name = the skill name; key = the run key; explore = the explore-walk note or ""; prog = the skill's progress note or
    // ""; motion = " 지금 동작: …." plus the door-step note, or "" (as the old head had them).
    public static string EndsFirstHead(string since, string name, string key, string explore, string prog, string motion, float rem, float ans)
        => $"계속하기 — 답이 오기 전에 끝남: 이 행동은 약 {F2(rem)}초 뒤 끝나고 이 판단의 답은 약 {F2(ans)}초 뒤 옴. "
         + "그래서 이 답이 올 때는 이어 갈 행동이 남아 있지 않음: 이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서 있음. "
         + "이어 갈 행동이 없으므로 '지금 하는 행동이 여전히 알맞으면 이어 간다'는 이 선택지에 해당하지 않음. "
         + $"{since} '{name}'({key}){explore} — 이 답이 오기 전에 끝남{(string.IsNullOrEmpty(prog) ? "" : " (" + prog + ")")}.{motion}";

    // For the cut-down places people read (SEND/RESULT log, "AI 판단:" chat, the down record's last choices; code review S1):
    // the ends-first text starts with the warning, so a cut kept no action name. There the action part comes first again,
    // as in the old text, and the warning follows. Every other text is returned unchanged. The model gets the text as built.
    public const string EndsFirstStart = "계속하기 — 답이 오기 전에 끝남: ";
    const string WarnEnd = "'지금 하는 행동이 여전히 알맞으면 이어 간다'는 이 선택지에 해당하지 않음. ";
    public static string ForShow(string desc)
    {
        if (desc == null || !desc.StartsWith(EndsFirstStart)) return desc;
        int i = desc.IndexOf(WarnEnd);
        if (i < 0) return desc;
        string warn = desc.Substring("계속하기 — ".Length, i + WarnEnd.Length - "계속하기 — ".Length).TrimEnd();
        return "계속하기 — " + desc.Substring(i + WarnEnd.Length) + " " + warn;
    }

    static string F2(float f) => f.ToString("F2");
}
