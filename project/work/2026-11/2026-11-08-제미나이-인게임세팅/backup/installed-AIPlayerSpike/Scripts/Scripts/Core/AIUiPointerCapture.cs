// No game/UI types: one owner for mouse capture across frame/order changes and releases outside the UI.
public sealed class AIUiPointerCapture
{
    private int _ownedButtons;
    private int _previousButtons;
    private int _releaseFrame = -1;

    public bool Evaluate(bool visible, bool inside, int buttons, int frame, int pressed = 0)
    {
        if (!visible) { Reset(); return false; }
        int started = (buttons & ~_previousButtons) | pressed;
        if (inside) _ownedButtons |= started;
        if ((_ownedButtons & ~buttons) != 0) _releaseFrame = frame;
        _ownedButtons &= buttons;
        _previousButtons = buttons;
        return _ownedButtons != 0 || _releaseFrame == frame;
    }

    public void Reset() { _ownedButtons = 0; _previousButtons = 0; _releaseFrame = -1; }
}
