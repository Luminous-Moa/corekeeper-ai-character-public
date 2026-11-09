public static class InventoryFlowTests
{
    public static void Run()
    {
        var oldArmor = new InventoryStamp(8520, 0, 17, 230, 0, false);
        var newArmor = new InventoryStamp(8521, 0, 21, 400, 0, false);
        var empty = new InventoryStamp(0, 0, 0, 0, 0, false);
        var exchange = new InventorySwapProgress(newArmor, oldArmor);
        Program.Check(exchange.MayRequest(newArmor, oldArmor), "inventory: selected exact source/destination can request Swap");
        Program.Check(!exchange.MayRequest(new InventoryStamp(8521, 0, 22, 400, 0, false), oldArmor), "inventory: same item ID with another individual state is stale before sending");
        exchange.RequestedOnce();
        Program.Check(!exchange.MayRequest(newArmor, oldArmor), "inventory: pending request is not issued again");
        Program.Check(!exchange.Observe(newArmor, oldArmor), "inventory: queue acceptance does not imply a swap");
        Program.Check(!exchange.Observe(oldArmor, oldArmor), "inventory: only one changed slot does not prove preservation");
        Program.Check(exchange.Observe(oldArmor, newArmor), "inventory: actual two-slot exchange proves old armor returned and new armor arrived");
        var unequip = new InventorySwapProgress(newArmor, empty); unequip.RequestedOnce();
        Program.Check(unequip.Observe(empty, newArmor), "inventory: unequip is observed in both equipment and bag slots");
        Program.Check(newArmor.Count == 1 && newArmor.State == 400, "inventory: nonstack equipment state is not a count of 400 items");
        Program.Check(AIInventoryRules.MoveCount(30, 20, 0, 10) == 20, "inventory: reserve ten wood while storing remaining twenty");
        Program.Check(AIInventoryRules.MoveCount(30, 20, 5, 10) == 5, "inventory: explicit five-item request limits one stack operation");
        Program.Check(AIInventoryRules.MoveCount(10, 10, 5, 10) == 0, "inventory: reserve already reached means nothing may be removed");
        var wood = new InventoryStamp(1, 0, 0, 20, 0, true);
        var woodAfter = new InventoryStamp(1, 0, 0, 13, 0, true);
        Program.Check(AIInventoryRules.ObservedTransfer(wood, woodAfter, 9, 16, 10) == 7, "inventory: partial transfer reports exactly seven observed on both sides");
        Program.Check(AIInventoryRules.ObservedTransfer(wood, wood, 9, 16, 10) == 0, "inventory: unrelated destination growth is not a transfer receipt");
        Program.Check(AIInventoryRules.ObservedTransfer(wood, woodAfter, 9, 9, 10) == 0, "inventory: source loss without destination gain is not success");
        Program.Check(AIInventoryRules.ObservedTransfer(wood, new InventoryStamp(2, 0, 0, 13, 0, true), 9, 16, 10) == 0, "inventory: another source item cannot satisfy the old request");
        Program.Check(AIInventoryRules.ObservedTransfer(newArmor, empty, 0, 1, 1) == 1, "inventory: one nonstack equipment item transfers with its whole state");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 5, 5) == 5, "inventory: source loss, bound floor consumption and matching receiver gain complete delivery");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, false, 5, 5) == 0, "inventory: floor disappearance and receiver gain without exact-target pickup request are not delivery");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, false, true, 5, 5) == 0, "inventory: ambiguous floor association cannot prove delivery");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 0, 5) == 0, "inventory: HasBeenPickedUp queue marker and coincidental receiver gain cannot consume unchanged floor");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 5, 0) == 0, "inventory: consumed floor without real receiver gain is not success");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 2, 2) == 2, "inventory: matching actual floor decrease and receiver gain prove exactly two partial units");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 5, 2) == 0, "inventory: original floor gone with only partial receiver gain leaves overflow attribution unconfirmed");
        Program.Check(AIInventoryRules.ObservedDelivery(5, 5, true, true, 2, 5) == 0, "inventory: receiver growth beyond observed floor decrease is not promoted to delivery");
    }
}
