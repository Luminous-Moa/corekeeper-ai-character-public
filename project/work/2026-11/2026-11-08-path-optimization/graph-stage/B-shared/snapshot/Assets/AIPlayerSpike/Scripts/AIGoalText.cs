// Goal display only; the execution contract lives with each feature.
public static class AIGoalText
{
    public static string Format(AIGoal g) => BaseFormat(g) + ((g.kind == GoalKind.Farm || g.kind == GoalKind.Cook || g.kind == GoalKind.Craft) ? Selectors(g) : "");
    private static string Selectors(AIGoal g) => (string.IsNullOrEmpty(g.mode) ? "" : " · mode=" + g.mode) + ((g.kind == GoalKind.Farm || g.kind == GoalKind.Cook) && g.item != ObjectID.None ? " · item=" + g.item : "") + (string.IsNullOrEmpty(g.second) ? "" : " · second=" + g.second) + (string.IsNullOrEmpty(g.where) ? "" : " · where=" + g.where) + ((g.kind == GoalKind.Farm || g.kind == GoalKind.Cook) && g.count > 0 ? " · count=" + g.count : "");
    private static string BaseFormat(AIGoal g) => g.kind == GoalKind.Maintenance ? $"장비 정비 {g.mode} 1회" + (g.item == ObjectID.None ? "" : $" · item={g.item}") + (g.slot < 0 ? "" : $" · slot={g.slot}") + (string.IsNullOrEmpty(g.where) ? "" : $" · where={g.where}")
        : AIGoalKinds.IsInventory(g.kind) ? InventoryText(g) : g.kind == GoalKind.Goto ? $"{g.where}(으)로 가기"
        : g.kind == GoalKind.Place ? $"{g.item}을(를) {g.where}에 놓기{(g.count > 1 ? $" {g.count}개" : "")}"
        : g.kind == GoalKind.Craft ? $"{g.itemName}({g.item}) {g.count}개 만들기" : g.kind == GoalKind.Gather ? $"{g.itemName}({g.item}) {g.count}개 모으기" + (g.maxCount >= 0 ? $" · 명시 확보 상한 {g.maxCount}개(원부탁#{g.maxOrder})" : "")
        : g.kind == GoalKind.Explore ? "탐험" : g.kind == GoalKind.Follow ? "호스트 따라가기"
        : g.kind == GoalKind.Store ? (g.item == ObjectID.None ? "기지 상자에 보관" : $"기지 상자에 {g.item} 보관") + (g.count > 0 ? $" {g.count}개" : "") + (g.keep >= 0 ? $"(가방에 {g.keep}개 남김)" : "") + (string.IsNullOrEmpty(g.where) ? "" : " " + g.where) : g.kind == GoalKind.Home ? "기지로 돌아가기"
        : g.kind == GoalKind.Farm ? "밭 가꾸기(일구기·심기·물 주기·거두기)" : g.kind == GoalKind.Cook ? "요리하기(내 솥에 재료 넣기·다 된 요리 꺼내기)"
        : g.kind == GoalKind.Free ? "자유 시간(정해진 목표 없음)" : "없음";

    private static string InventoryText(AIGoal g)
    {
        string verb = g.kind == GoalKind.Equip ? "장착·교체" : g.kind == GoalKind.Unequip ? "착용 해제" : g.kind == GoalKind.Hold ? "손에 들기"
            : g.kind == GoalKind.Take ? "상자에서 꺼내기" : g.kind == GoalKind.Drop ? "바닥에 내려놓기" : "건네기";
        return verb + (g.item == ObjectID.None ? "" : $" {g.item}") + (g.count > 0 ? $" {g.count}개" : "")
            + (string.IsNullOrEmpty(g.where) ? "" : " · " + g.where) + (g.slot >= 0 ? $" · 물품 칸 {g.slot}" : "");
    }
}
