// The game starts a primary use while held and starts a windup-secondary use on release (EquipmentSlot.UpdateEquipment).
// This is input scheduling, not a damage or projectile simulator. The game owns costs, cooldown and outcomes.
public sealed class AIWeaponUseControl
{
    private bool _released, _releaseFramePassed;
    public struct Input { public bool Primary, Secondary, Done, MissedIndividualWindow; }
    public Input Step(bool visible, bool handReady, bool secondary, bool atMaxWindup, bool winding,
        bool individualSummon = false, int tier = 0, int minimumTier = 0)
    {
        if (!visible || !handReady) return default;
        if (!secondary) return new Input { Primary = true };
        if (!_released)
        {
            if (individualSummon)
            {
                // Max charge is a different game action: dismiss/replace, never silently substitute it for spawn.
                if (atMaxWindup) return new Input { Secondary = true, MissedIndividualWindow = true };
                if (!winding || tier < minimumTier) return new Input { Secondary = true };
            }
            else if (!atMaxWindup) return new Input { Secondary = true };
        }
        _released = true;
        bool done = _releaseFramePassed && !winding;
        _releaseFramePassed = true;
        return new Input { Done = done };
    }
}
