internal static class ChatScreenTests
{
    internal static void Run()
    {
        Program.Check(AIChatScreenMath.ScreenX(.5f,0,1,3840) == 1920 && AIChatScreenMath.ScreenY(.5f,0,1,2160) == 1080,
            "Projection uses actual screen dimensions, independent of the low-resolution camera texture");
        Program.Check(AIChatScreenMath.ScreenX(0,.25f,.5f,3840) == 960 && AIChatScreenMath.ScreenX(1,.25f,.5f,3840) == 2880,
            "Partial camera viewport keeps its original horizontal location and width");
        Program.Check(AIChatScreenMath.ScreenY(0,.25f,.5f,2160) == 1620 && AIChatScreenMath.ScreenY(1,.25f,.5f,2160) == 540,
            "Viewport vertical bounds convert correctly to top-origin GUI pixels");
        Program.Check(AIChatScreenMath.NativePixelsPerDot(2160,1,15,1) == 4.5f,
            "Stable pixel measurement uses parent scale and does not depend on the chat root loading fade");
        Program.Check(AIChatScreenMath.NativePixelsPerDot(2160,.5f,15,1) == 2.25f && AIChatScreenMath.NativePixelsPerDot(2160,1,30,1) == 2.25f,
            "Changed viewport or orthographic projection updates metrics even at the same screen resolution");
        int scale = AIChatScreenMath.PixelScale(4.4f, 15, 10);
        float ppu = AIChatScreenMath.PixelsPerUnit(4.4f, scale);
        Program.Check(scale == 3 && System.Math.Abs(10f / ppu * 16f * 4.4f - 30f) < 0.001f,
            "Full-screen pixel metrics: original Korean 66px becomes 30px, preserving every bitmap row");
        Program.Check(AIChatScreenMath.PixelScale(4.4f,12,10) == 3, "12px original cell also selects nearest integer scale to half");
        Program.Check(AIChatScreenMath.PixelScale(4f,15,10) == 3, "60px original becomes exactly 30px");
        Program.Check(AIChatScreenMath.PixelScale(1f,15,10) == 1, "Small screen never downsamples the 10px bitmap below one pixel per dot");
        Program.Check(AIChatScreenMath.PixelScale(6.6f,10,10) == 3, "Current 4K compact 66px becomes 30px (44px to 20px in a two-thirds capture)");
        Program.Check(AIChatScreenMath.PixelScale(float.NaN,15,10) == 1 && AIChatScreenMath.PixelsPerUnit(float.NaN,1) == 16,
            "Invalid camera measurements safely keep normal metrics");
        Program.Check(AIChatScreenMath.PixelsPerUnit(0,1) == 16 && AIChatScreenMath.PixelsPerUnit(4,0) == 16,
            "Missing projection cannot create zero/infinite font ppu");
    }
}
