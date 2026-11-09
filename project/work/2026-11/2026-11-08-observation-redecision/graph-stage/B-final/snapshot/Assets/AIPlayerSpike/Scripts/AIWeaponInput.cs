using Unity.Mathematics;

// The single translation of observed target / held selection into normal BodyInput for distance and charged use.
public static class AIWeaponInput
{
    public static BodyInput Build(AIWeaponUseControl control, float2 face, float2 target, int slot, bool visible, bool ready,
        bool secondary, bool atMaxWindup, bool winding, out bool done, bool individualSummon = false, int tier = 0, int minimumTier = 0)
    {
        var step = control.Step(visible, ready, secondary, atMaxWindup, winding, individualSummon, tier, minimumTier);
        done = step.Done;
        return new BodyInput { slot = slot, face = visible ? face : float2.zero, aim = visible, hasPoint = visible,
            point = visible ? target : float2.zero, primary = step.Primary, secondary = step.Secondary };
    }
}
