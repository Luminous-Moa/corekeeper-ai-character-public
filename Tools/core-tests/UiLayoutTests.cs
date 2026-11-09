internal static class UiLayoutTests
{
    internal static void Run()
    {
        Program.Check(new AIUiLayout(2560, 1440).Scale == 2, "1440p uses 20px bitmap text");
        Program.Check(new AIUiLayout(1920, 1080).Scale == 2, "1080p uses readable 20px text");
        Program.Check(new AIUiLayout(1280, 720).Scale == 1, "720p preserves compact 10px layout");
        Program.Check(new AIUiLayout(3840, 2160).Scale == 3, "4K uses integer 30px text");
        foreach (var viewport in new[] { (640,480), (1280,720), (1920,1080), (2560,1440), (3840,2160), (3440,1440), (360,800), (1001,601) })
        {
            var layout = new AIUiLayout(viewport.Item1, viewport.Item2);
            int s = layout.Scale;
            Program.Check(layout.Fits && layout.BarX >= 0 && layout.PanelY >= 0 &&
                (layout.BarX + AIUiLayout.Width) * s <= viewport.Item1 &&
                (layout.BarY + AIUiLayout.BarHeight) * s <= viewport.Item2 &&
                layout.PanelY + AIUiLayout.PanelHeight + AIUiLayout.Gap == layout.BarY,
                "Selection bar and expanded panel fit " + viewport);
            int physicalX = (layout.BarX + 4 + 2 * 71 + 35) * s;
            int physicalY = (layout.BarY + 3 + 11) * s;
            Program.Check(physicalX / s == layout.BarX + 181 && physicalY / s == layout.BarY + 14,
                "Drawn member button uses the same input coordinate scale " + viewport);
        }
        Program.Check(!new AIUiLayout(300,240).Fits, "Unsupported tiny viewport cannot draw off-screen UI");
        var before = new AIUiLayout(2560,1440);
        var after = new AIUiLayout(640,480);
        Program.Check(before.Scale == 2 && after.Scale == 1 && after.Fits, "Viewport shrink recomputes scale and anchors without cached dimensions");
    }
}
