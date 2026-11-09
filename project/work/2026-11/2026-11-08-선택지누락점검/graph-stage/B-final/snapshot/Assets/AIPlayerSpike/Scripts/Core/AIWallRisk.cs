using System;

// The adapter checks the game's IsExplosiveCD, rather than guessing from an item's name.
// ExplosiveSystem.ExplodeJob creates the explosion after destruction; TileDamageSystem applies mining damage.
public static class AIWallRisk
{
    public const string Note = "폭발성 벽: 채굴로 폭발할 수 있어 가까이 있으면 피해 위험이 있음(피해량·범위 미확인). 이 캐기·파기 행동에는 폭발을 기다리며 물러나는 동작이 없음.";
    public static string Text(bool explosive) => explosive ? " " + Note : "";

    // A list of permitted edge tiles is not a route selector: ordinary home/goto/place routes have it too.
    // Only the explicit detour choice uses the alternative. No alternative means no fact about that route.
    public static string RouteText<T>(T primary, bool detour, Func<T, T> alternative, Func<T, string> text) where T : class
    {
        var chosen = primary == null ? null : detour ? alternative(primary) : primary;
        return chosen == null ? "" : text(chosen);
    }
}
