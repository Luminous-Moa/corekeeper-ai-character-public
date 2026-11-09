// Inventory observations as values. The adapter reads the game; these rules never create inventory state.
using System;

public readonly struct InventoryStamp
{
    public readonly int Id, Variation, Aux, State, Version;
    public readonly bool Stackable;
    public InventoryStamp(int id, int variation, int aux, int state, int version, bool stackable)
    { Id = id; Variation = variation; Aux = aux; State = state; Version = version; Stackable = stackable; }
    public int Count => Id == 0 ? 0 : Stackable ? Math.Max(0, State) : 1;
    public bool SameObject(InventoryStamp other) => Id == other.Id && Variation == other.Variation && Aux == other.Aux;
    public bool Exact(InventoryStamp other) => SameObject(other) && State == other.State && Version == other.Version;
}

public sealed class InventorySwapProgress
{
    public readonly InventoryStamp BeforeFrom, BeforeTo;
    public bool Requested { get; private set; }
    public bool Observed { get; private set; }
    public InventorySwapProgress(InventoryStamp from, InventoryStamp to) { BeforeFrom = from; BeforeTo = to; }
    public bool MayRequest(InventoryStamp currentFrom, InventoryStamp currentTo) => !Requested && currentFrom.Exact(BeforeFrom) && currentTo.Exact(BeforeTo);
    public void RequestedOnce() { Requested = true; }
    public bool Observe(InventoryStamp from, InventoryStamp to)
    {
        Observed = Requested && from.Exact(BeforeTo) && to.Exact(BeforeFrom);
        return Observed;
    }
}

public static class AIInventoryRules
{
    // requestedRemaining <= 0 means all available beyond reserve. A stack is a single chosen body operation.
    public static int MoveCount(int available, int sourceCount, int requestedRemaining, int keep)
    {
        int movable = Math.Max(0, available - Math.Max(0, keep));
        if (requestedRemaining > 0) movable = Math.Min(movable, requestedRemaining);
        return Math.Min(Math.Max(0, sourceCount), movable);
    }

    // A processed MoveAmount is not a receipt. Both actual sides must agree on the same positive amount.
    public static int ObservedTransfer(InventoryStamp beforeSource, InventoryStamp afterSource, int beforeDestinationCount, int afterDestinationCount, int requested)
    {
        if (requested <= 0 || beforeSource.Id == 0) return 0;
        int remaining = afterSource.Id == 0 ? 0 : beforeSource.SameObject(afterSource) ? afterSource.Count : -1;
        if (remaining < 0) return 0;
        if (!beforeSource.Stackable && afterSource.Id != 0) return 0;
        int removed = beforeSource.Count - remaining;
        int added = afterDestinationCount - beforeDestinationCount;
        return removed > 0 && removed <= requested && removed == added ? removed : 0;
    }

    // HasBeenPickedUp means the move was queued, not consumed. The bound floor's actual decrease must agree with
    // the receiver's actual gain; disappearance/overflow cannot promote an unrelated or differently-sized gain.
    public static int ObservedDelivery(int sourceRemoved, int requested, bool uniqueFloorBound, bool pickupRequestedForRecipient, int floorRemoved, int receiverGain)
    {
        if (requested <= 0 || sourceRemoved != requested || !uniqueFloorBound || !pickupRequestedForRecipient || floorRemoved <= 0 || floorRemoved > requested || receiverGain != floorRemoved) return 0;
        return receiverGain;
    }
}
