using System;

// One integer pixel scale for drawing, button geometry and mouse-coordinate conversion.
public readonly struct AIUiLayout
{
    public const int Width = 292, BarHeight = 28, PanelHeight = 246, Margin = 12, Gap = 4;
    public readonly int Scale, BarX, BarY, PanelY;
    public readonly bool Fits;

    public AIUiLayout(int screenWidth, int screenHeight)
    {
        int fit = Math.Min(screenWidth / (Width + Margin * 2), screenHeight / (PanelHeight + Gap + BarHeight + Margin * 2));
        int desired = Math.Max(1, (screenHeight + 360) / 720);
        Fits = fit >= 1;
        Scale = Math.Max(1, Math.Min(desired, fit));
        BarX = Math.Max(0, screenWidth / Scale - Width - Margin);
        BarY = Math.Max(0, screenHeight / Scale - BarHeight - Margin);
        PanelY = Math.Max(0, BarY - PanelHeight - Gap);
    }
}
