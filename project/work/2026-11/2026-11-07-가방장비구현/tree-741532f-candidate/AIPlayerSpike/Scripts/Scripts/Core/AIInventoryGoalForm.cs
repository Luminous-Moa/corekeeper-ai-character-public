// Inventory plan shape only. World checks, concrete slots and actual transfer results belong to AIInventory.
public static class AIInventoryGoalForm
{
    public static int CountFor(GoalKind kind, int count) => count < 0 && AIGoalKinds.IsInventory(kind) ? 1 : count < 0 && kind == GoalKind.Store ? 0 : count;

    public static string Check(PlanStep s)
    {
        if (!AIGoalKinds.TryWire(s.Do, out var kind) || (!AIGoalKinds.IsInventory(kind) && kind != GoalKind.Store)) return "가방·장비 목표가 아님";
        bool hasItem = !string.IsNullOrEmpty(s.Item) && s.Item != "null" && s.Item != "none";
        if (kind != GoalKind.Unequip && kind != GoalKind.Store && !hasItem) return "품목을 지정해야 함";
        if (kind == GoalKind.Unequip && !hasItem && string.IsNullOrEmpty(s.Where) && s.Slot < 0) return "벗을 품목이나 착용 슬롯을 지정해야 함";
        if (s.Keep < -1 || s.Keep > 9999 || s.Slot < -1 || s.Slot > 9999) return "keep/slot 범위 밖";
        if (s.Keep >= 0 && kind != GoalKind.Store) return "keep은 보관 목표에서만 사용";
        if (s.Count < -1 || s.Count > 9999) return "개수 범위 밖";
        if ((kind == GoalKind.Take || kind == GoalKind.Drop || kind == GoalKind.Give) && s.Count == 0) return "옮길 개수는 1 이상";
        if ((kind == GoalKind.Equip || kind == GoalKind.Unequip || kind == GoalKind.Hold) && s.Count != -1 && s.Count != 1) return "장비 선택은 한 번에 한 개";
        if (kind == GoalKind.Give && string.IsNullOrEmpty(s.Where)) return "수령자를 where에 지정해야 함";
        return null;
    }
}
