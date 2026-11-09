// Maintenance request and result rules. Game lookups and NetworkTick comparisons stay in AIMaintenance.
public static class AIMaintenanceRules
{
    public static bool Mode(string mode) => mode == "repair" || mode == "upgrade" || mode == "recover";
    public static int CountFor(int count) => count == -1 ? 1 : count;

    public static string Check(PlanStep step)
    {
        if (step == null || step.Do != "maintain") return "장비 정비 목표가 아님";
        string mode = AILifeGoalForm.Text(step.Mode), item = AILifeGoalForm.Text(step.Item), where = AILifeGoalForm.Text(step.Where);
        if (!Mode(mode)) return "정비 mode는 repair/upgrade/recover만 가능";
        if (step.Count != -1 && step.Count != 1) return "정비 count는 한 번에 1만 가능";
        if (step.Keep != -1 || AILifeGoalForm.Text(step.Second) != null) return "정비는 keep/second를 받지 않음";
        if (step.Slot < -1) return "정비 slot은 정확 절대 슬롯의 0 이상 정수";
        if (item == AILifeGoalForm.InvalidSelector || where == AILifeGoalForm.InvalidSelector) return "정비 item/where는 문자열이어야 함";
        if (where != null && !AILifeGoalForm.Facility(where, "station")) return "정비 where는 station:<ObjectID>@x,y";
        return null;
    }

    public static int RepairConfirmed(bool sameIdentity, int beforeAmount, int afterAmount, int expectedMax)
        => sameIdentity && expectedMax > 0 && beforeAmount < expectedMax && afterAmount == expectedMax ? 1 : 0;

    public static int UpgradeConfirmed(bool sameIdentity, int observedLevel, int expectedLevel, bool receivedMain)
        => sameIdentity && expectedLevel > 0 && observedLevel == expectedLevel && receivedMain ? 1 : 0;

    // Old UIActionBuffer history is not evidence of an unconsumed request. The caller checks the matching consumer queue.
    public static string RecoveryReady(bool currentTickNewer, bool appliedTickAtOrAfter, bool matchingQueuePending, bool observedExactInput)
        => currentTickNewer && appliedTickAtOrAfter && !matchingQueuePending && observedExactInput
            ? null : "요청 처리 미확인; 회수 대기";

    public static bool RecoveryCompletesUpgrade(bool observedUpgrade, bool sameGoalReference, int pendingGoalRevision, int currentGoalRevision)
        => observedUpgrade && sameGoalReference && pendingGoalRevision == currentGoalRevision;
}
