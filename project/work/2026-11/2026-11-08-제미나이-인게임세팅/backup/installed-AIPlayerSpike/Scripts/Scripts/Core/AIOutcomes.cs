// AIOutcomes — what a skill end, a goal end and a rejected upper-LLM answer MEAN, as values next to the sentences
// that say it (refactor plan 2-계획.md S2). Unity-free (Tools/core-tests). Until now the program re-read the sentences
// ("길 없음…", "AI가 쓰러짐…", "…처치…", "오래된 답…", "긴급…") to decide; a reworded sentence changed behaviour.
// S2 (transition): every place still decides by the sentence, exactly as before, and also computes the value; when the
// two disagree it logs OUTCOME-MISMATCH / REJECT-MISMATCH (callers: AIBrain, AIPlanner, AIExplore, ExpRecorder).
// S2b (after L1 showed no mismatch): the values decide; the sentence comparisons stay, logged only with the test switch
// (testcmds.on, review F7). The skill no-path rule still decides by the sentence until L2 (no path failure in L1).
// (If Scripts/Core is ever flattened, this file becomes Scripts/AICoreOutcomes.cs: a rename only.)

// How a skill ended, where the brain cares (AIBrain.OnSkillEnded, TunnelSkill). None = nothing special.
public enum SkillEnd { None, NoPath, Stuck, Timeout, Down, Killed }

// A rejected upper-LLM answer: Stale (asked again later, AIPlanner.Apply) or Other (asked again at once).
public enum RejectKind { None, Stale, Other }

// A goal that ends: Why is the sentence AIBrain.ClearGoal always took (logs and the experience record keep it).
// Outcome: completed | blocked | exhausted | cancelled | system | unknown (ExpRun.Classify's words).
// Cls: the block class when the ending place alone decides it (only_wait, no_place, cannot_craft), null when the
// sentence's content decides it (tool_weak / no_source: gather, farm and cook stalls; HEAD decision, review F6).
public struct GoalEnd
{
    public string Why, Outcome, Cls;
    public GoalEnd(string why, string outcome, string cls = null) { Why = why; Outcome = outcome; Cls = cls; }
}

public static class AIOutcomes
{
    // ---- the sentence rules as they were (AIDecisions.cs:453, :466, :473; AIExplore.cs:1053; AIPlanner.cs:417)
    public static bool NoPathText(string reason) => reason != null && (reason.StartsWith("길 없음") || reason.StartsWith("막힘") || reason.StartsWith("시간 초과"));
    public static bool DownText(string reason) => reason != null && reason.StartsWith("AI가 쓰러짐");
    public static bool KillText(string reason) => reason != null && reason.Contains("처치");
    public static bool StaleRejectText(string reject) => reject != null && reject.StartsWith("오래된 답");
    public static bool EmergencyText(string trigger) => trigger != null && (trigger.StartsWith("긴급") || trigger.StartsWith("체력 감소"));
    public static bool ToolShortText(string note) => note != null && note.Contains("도구 부족");

    // ---- the same questions from the values
    public static bool NoPath(SkillEnd e) => e == SkillEnd.NoPath || e == SkillEnd.Stuck || e == SkillEnd.Timeout;

    // "rule text=… code=…" when the sentence and the value disagree, else null (the caller logs it).
    public static string Mismatch(string rule, bool byText, bool byValue, string detail)
        => byText == byValue ? null : $"{rule} text={(byText ? 1 : 0)} code={(byValue ? 1 : 0)} {detail}";

    // 실측결함수정3 N8: what hurt the AI when no enemy was in view — the damage-over-time conditions on it and the
    // projectiles near it at that moment (the game side reads them, AIVitals). "" when neither.
    // dots = condition names with their value; shots = projectile object ids within reach of the AI
    public static string HurtWithoutEnemy(System.Collections.Generic.IList<string> dots, System.Collections.Generic.IList<string> shots)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (dots != null && dots.Count > 0) parts.Add($"지속 피해 상태 {string.Join(", ", dots)}");
        if (shots != null && shots.Count > 0) parts.Add($"가까운 투사체 {string.Join(", ", shots)}(보이지 않는 곳에서 쏜 것일 수 있음)");
        return string.Join("; ", parts);
    }
    // 실측결함수정3 N8: the cause on a down record. lastEnemies = the enemies of the last sample; hurtWhy = the last
    // HurtWithoutEnemy within the last seconds ("" none); hurtLooked = the drops without an enemy were looked at
    public static string DownCause(string lastEnemies, int hunger, string hurtWhy, bool hurtLooked)
    {
        if (!string.IsNullOrEmpty(lastEnemies)) return $"적 근처(마지막으로 본 적: {lastEnemies})";
        if (!string.IsNullOrEmpty(hurtWhy)) return $"보이는 적 없음; 체력이 줄 때 {hurtWhy}";
        if (hunger == 0) return "배고픔 0(최대 체력·공격력 약 25% 감소 상태)";
        return hurtLooked ? "보이는 적 없음, 원인 모름(체력이 줄 때 지속 피해 상태도 가까운 투사체도 없었음)" : "보이는 적 없음, 원인 모름";
    }
}
