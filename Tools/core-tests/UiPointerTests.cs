internal static class UiPointerTests
{
    internal static void Run()
    {
        var capture = new AIUiPointerCapture();
        Program.Check(!capture.Evaluate(true, true, 0, 1), "UI hover leaves game inputs untouched");
        Program.Check(capture.Evaluate(true, true, 1, 2), "First UI mouse down is captured before OnGUI");
        Program.Check(capture.Evaluate(true, true, 1, 2), "Repeated queries in one frame remain captured");
        Program.Check(capture.Evaluate(true, false, 1, 3), "Dragging beyond UI keeps game input blocked");
        Program.Check(capture.Evaluate(true, false, 0, 4), "Release outside UI cannot fire a game release action");
        Program.Check(capture.Evaluate(true, false, 0, 4), "Release remains captured for every consumer in frame");
        Program.Check(!capture.Evaluate(true, false, 0, 5), "Next frame restores ordinary control");
        Program.Check(!capture.Evaluate(true, false, 1, 6), "Game click outside UI is untouched");
        Program.Check(!capture.Evaluate(true, true, 1, 7), "Entering panel with a game drag does not steal ownership");
        capture.Evaluate(true, false, 0, 8);
        capture.Evaluate(true, true, 1, 9);
        Program.Check(!capture.Evaluate(false, true, 1, 10), "World/menu hide immediately clears capture");
        Program.Check(!capture.Evaluate(true, false, 0, 11), "Reopening leaves no stale release capture");
        capture.Evaluate(true, false, 2, 12);
        Program.Check(!capture.Evaluate(true, true, 2, 13), "Held world right button entering UI remains unowned");
        Program.Check(capture.Evaluate(true, true, 3, 14), "New left UI click is captured while world right button remains held");
        Program.Check(capture.Evaluate(true, true, 3, 14), "Mixed-button same-frame queries remain captured");
        Program.Check(capture.Evaluate(true, false, 2, 15), "Owned left release is blocked while unowned right stays held");
        Program.Check(!capture.Evaluate(true, false, 2, 16), "Unowned right hold returns to game next frame");
        capture.Reset();
        Program.Check(capture.Evaluate(true, true, 0, 17, 1), "Press and release between polls still capture the click frame");
        Program.Check(capture.Evaluate(true, false, 0, 17, 1), "Short-click release remains captured outside UI in the same frame");
        Program.Check(!capture.Evaluate(true, false, 0, 18), "Short-click capture clears next frame");
    }
}
