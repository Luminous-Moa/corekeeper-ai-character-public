// A saved hotbar index is not an item identity. Durability may change while the same item is used.
public enum HeldChoiceState { Ready, WaitingForEquip, Changed, Broken }

public static class AIHeldChoice
{
    public static HeldChoiceState Check(InventoryStamp expected, InventoryStamp current, int requestedSlot,
        InventoryStamp actuallyHeld, int actuallyHeldSlot, bool explicitChoice, bool broken)
    {
        if (!expected.SameObject(current)) return HeldChoiceState.Changed;
        if (explicitChoice && broken) return HeldChoiceState.Broken;
        if (actuallyHeldSlot != requestedSlot || !expected.SameObject(actuallyHeld)) return HeldChoiceState.WaitingForEquip;
        return HeldChoiceState.Ready;
    }
}
