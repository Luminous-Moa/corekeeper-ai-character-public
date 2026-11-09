// 채팅창 개선 — fade only (user decision 2026-11-06 '7초 선명 → 짧게 사라짐'): the numbers AIChatWindow puts on each line.
public static class ChatFadeTests
{
    public static void Run()
    {
        float hold = AIChatFade.DefaultFadeSeconds;
        Program.Check(hold == 7f && AIChatFade.FadeSeconds(0f) == 7f && AIChatFade.FadeSeconds(float.NaN) == 7f, "fade: 7 s by default and for an absent or broken value");
        Program.Check(AIChatFade.FadeSeconds(0.1f) == 0.5f && AIChatFade.FadeSeconds(1000f) == 60f, "fadeSeconds is pulled into 0.5..60");
        Program.Check(AIChatFade.FadeOutTime(hold) == 8f && System.Math.Abs(AIChatFade.HoldFraction(hold) - 7f / 8f) < 1e-6, "7 s hold: fadeOutTime 8, curve holds 1 up to 7/8");
        Program.Check(AIChatFade.Alpha(0f, hold) == 1f && AIChatFade.Alpha(3.5f, hold) == 1f && AIChatFade.Alpha(6.99f, hold) == 1f && AIChatFade.Alpha(7f, hold) == 1f, "fully clear for the whole 7 s");
        Program.Check(System.Math.Abs(AIChatFade.Alpha(7.5f, hold) - 0.5f) < 1e-4, "half way through the 1 s vanish: alpha 0.5");
        Program.Check(AIChatFade.Alpha(8f, hold) == 0f && AIChatFade.Alpha(9f, hold) == 0f, "gone at 8 s and after");
        Program.Check(AIChatFade.Alpha(3f, 3f) == 1f && System.Math.Abs(AIChatFade.Alpha(3.25f, 3f) - 0.75f) < 1e-4 && AIChatFade.Alpha(4f, 3f) == 0f, "another hold (3 s): the vanish is still 1 s");
    }
}
