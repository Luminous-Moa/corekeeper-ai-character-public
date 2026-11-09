public static class HeldChoiceTests
{
    public static void Run()
    {
        var sword = new InventoryStamp(8900, 2, 31, 900, 0, false);
        var worn = new InventoryStamp(8900, 2, 31, 899, 0, false);
        var otherVariation = new InventoryStamp(8900, 3, 31, 900, 0, false);
        var otherInstance = new InventoryStamp(8900, 2, 99, 900, 0, false);
        var empty = new InventoryStamp(0, 0, 0, 0, 0, false);
        Program.Check(AIHeldChoice.Check(sword, worn, 5, worn, 5, true, false) == HeldChoiceState.Ready, "using the chosen sword consumes durability without losing its selection");
        Program.Check(AIHeldChoice.Check(sword, otherVariation, 5, otherVariation, 5, true, false) == HeldChoiceState.Changed, "same ObjectID with another variation cannot receive the saved attack input");
        Program.Check(AIHeldChoice.Check(sword, otherInstance, 5, otherInstance, 5, true, false) == HeldChoiceState.Changed, "same type and variation with another aux item cannot receive attack input");
        Program.Check(AIHeldChoice.Check(sword, empty, 5, empty, 5, true, false) == HeldChoiceState.Changed, "removed chosen equipment cannot become a bare-handed attack");
        Program.Check(AIHeldChoice.Check(sword, sword, 5, sword, 0, true, false) == HeldChoiceState.WaitingForEquip, "the held slot must catch up before primary input is sent");
        Program.Check(AIHeldChoice.Check(sword, sword, 5, otherVariation, 5, true, false) == HeldChoiceState.WaitingForEquip, "a stale EquippedObject snapshot does not authorize an attack with another variation");
        Program.Check(AIHeldChoice.Check(sword, sword, 5, sword, 5, true, true) == HeldChoiceState.Broken, "explicitly chosen broken gear is reported rather than automatically substituted");
        Program.Check(AIHeldChoice.Check(sword, sword, 5, sword, 5, false, true) == HeldChoiceState.Ready, "legacy non-explicit broken-weapon behavior remains with its existing skill rule");
        var pick = new InventoryStamp(10, 0, 7, 100, 0, false);
        Program.Check(AIHeldChoice.Check(pick, pick, 2, sword, 5, true, false) == HeldChoiceState.WaitingForEquip, "switching to the chosen pick waits for the actual hand before mining primary");
        Program.Check(AIHeldChoice.Check(empty, empty, 0, empty, 0, false, false) == HeldChoiceState.Ready, "an explicitly observed empty legacy hand stays valid");
    }
}
