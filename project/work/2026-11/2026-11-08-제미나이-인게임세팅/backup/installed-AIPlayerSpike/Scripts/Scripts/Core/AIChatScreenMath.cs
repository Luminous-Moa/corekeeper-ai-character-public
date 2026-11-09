using System;

public static class AIChatScreenMath
{
    public static float ScreenX(float x, float viewportX, float viewportWidth, int screenWidth) => (viewportX + x * viewportWidth) * screenWidth;
    public static float ScreenY(float y, float viewportY, float viewportHeight, int screenHeight) => (1f - viewportY - y * viewportHeight) * screenHeight;
    public static float NativePixelsPerDot(int screenHeight, float viewportHeight, float orthographicSize, float stableParentScale)
    {
        if (screenHeight <= 0 || viewportHeight <= 0 || orthographicSize <= 0 || stableParentScale <= 0) return 0;
        return screenHeight * viewportHeight / (2f * orthographicSize) * stableParentScale / 16f;
    }
    // Preserve every bitmap row. Pick the closest integer screen-pixel size to half the original cell.
    public static int PixelScale(float screenPixelsPerDot, int originalCell, int compactCell)
    {
        if (float.IsNaN(screenPixelsPerDot) || float.IsInfinity(screenPixelsPerDot) || screenPixelsPerDot <= 0 || originalCell <= 0 || compactCell <= 0) return 1;
        return Math.Max(1, (int)Math.Floor(screenPixelsPerDot * originalCell / compactCell * 0.5f + 0.5f));
    }

    public static float PixelsPerUnit(float screenPixelsPerDot, int pixelScale)
    {
        if (float.IsNaN(screenPixelsPerDot) || float.IsInfinity(screenPixelsPerDot) || screenPixelsPerDot <= 0 || pixelScale <= 0) return 16f;
        return 16f * screenPixelsPerDot / pixelScale;
    }
}
