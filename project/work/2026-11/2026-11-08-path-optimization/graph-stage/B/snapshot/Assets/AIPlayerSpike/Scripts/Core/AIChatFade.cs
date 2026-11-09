// AIChatFade — how long a chat line stays (채팅창 개선, 2026-11-05 합의 work/2026-11/2026-11-05-채팅창-개선/0-합의.md;
// user decision 2026-11-06 '7초 선명 → 짧게 사라짐'; letter size and line count are left for later).
// Unity-free (Tools/core-tests). The Unity side is AIChatWindow.cs.
// A line stays fully clear for fadeSeconds, then vanishes over VanishSeconds. The game's PugTextEffectMaxFade runs one
// fade of fadeOutTime and multiplies alpha by fadeOutCurve(elapsed / fadeOutTime) (the game: 4 s, curve 1 up to 0.8,
// i.e. 3.2 s clear and 0.8 s to vanish); so fadeOutTime = hold + vanish and the curve is 1 up to HoldFraction, then a
// straight line down to 0 at 1.
public static class AIChatFade
{
    public const float DefaultFadeSeconds = 7f;      // user decision 2026-11-05: "글 남는건 7초로"
    public const float VanishSeconds = 1f;

    // A value out of 0.5..60 is pulled into it; NaN, infinity or not above zero (absent or broken) gives the default.
    public static float FadeSeconds(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0f) return DefaultFadeSeconds;
        return v < 0.5f ? 0.5f : v > 60f ? 60f : v;
    }

    public static float FadeOutTime(float holdSeconds) => holdSeconds + VanishSeconds;
    public static float HoldFraction(float holdSeconds) => holdSeconds / FadeOutTime(holdSeconds);

    // The curve value at x = elapsed / fadeOutTime (0..1): what the Unity curve built in AIChatWindow evaluates to.
    public static float Curve(float x, float holdSeconds)
    {
        float h = HoldFraction(holdSeconds);
        if (x <= h) return 1f;
        if (x >= 1f) return 0f;
        return (1f - x) / (1f - h);
    }

    // Alpha factor after `elapsed` seconds of fading.
    public static float Alpha(float elapsed, float holdSeconds) => Curve(elapsed / FadeOutTime(holdSeconds), holdSeconds);
}
