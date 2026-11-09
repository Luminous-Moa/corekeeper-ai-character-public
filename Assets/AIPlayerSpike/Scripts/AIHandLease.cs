using Unity.Entities;

// Per-action identity and actual-equipped observation. It never chooses a replacement or changes inventory state.
internal sealed class AIHandLease
{
    private readonly int _slot;
    private readonly InventoryStamp _expected;
    private readonly bool _explicit, _mining;

    internal AIHandLease(World world, Entity player, int slot, bool mining)
    {
        _slot = slot; _mining = mining;
        _explicit = AIInventory.TryChosenHand(world, player, mining, out _, out _, out _);
        AIInventory.Read(world, player, slot, out var item);
        _expected = AIInventory.Stamp(item);
    }

    internal string Check(World world, Entity player, out bool ready)
    {
        ready = false;
        if (!AIInventory.Read(world, player, _slot, out var current)) return "선택한 손 물품 슬롯을 더 이상 관측할 수 없음";
        if (_explicit && AIInventory.TryChosenHand(world, player, _mining, out int selected, out _, out string why) && selected != _slot)
            return string.IsNullOrEmpty(why) ? "현재 명시 손 선택이 이 동작의 물품과 달라짐" : why;
        var em = world.EntityManager;
        var equipped = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player) : default;
        var state = AIHeldChoice.Check(_expected, AIInventory.Stamp(current), _slot, AIInventory.Stamp(equipped.containedObject),
            em.HasComponent<EquippedObjectCD>(player) ? equipped.equippedSlotIndex : -1, _explicit, PlayerController.ItemIsBroken(current.objectData));
        if (state == HeldChoiceState.Changed) return "선택한 손 물품의 ID·변형·개별 상태가 바뀜(다른 물품에는 입력하지 않음)";
        if (state == HeldChoiceState.Broken) return "명시 선택한 손 물품이 고장 남";
        ready = state == HeldChoiceState.Ready;
        return null;
    }
}
