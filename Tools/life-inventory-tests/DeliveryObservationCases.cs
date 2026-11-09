using Unity.Entities;
using Unity.Transforms;

static partial class Program
{
    static (World world, Entity sender, DynamicBuffer<ContainedObjectsBuffer> bag,
        Entity recipient, DynamicBuffer<ContainedObjectsBuffer> receiverBag, InventorySkill skill) DeliverySetup()
    {
        var (world, sender, bag) = Setup();
        bag[12] = Item(ObjectID.CopperBreastArmor, 1, 75);
        var recipient = new Entity { Index = 3 };
        var receiverBag = new DynamicBuffer<ContainedObjectsBuffer>();
        for (int i = 0; i < 70; i++) receiverBag.Add(default);
        world.EntityManager.PutBuffer(recipient, receiverBag);
        world.EntityManager.Put(recipient, new LocalTransform());
        AITeammates.Seen.Add(new SeenPlayer { entity = recipient, name = "Friend" });
        Goal(world, sender, new AIGoal { kind = GoalKind.Give, item = ObjectID.CopperBreastArmor, count = 1, where = "Friend" });
        var move = AIInventory.Candidates(world, sender, Observe(world, sender), "").First().inventory;
        return (world, sender, bag, recipient, receiverBag, Start(world, sender, move));
    }

    static Entity ArmorFloor(World world, int index, Entity recipient, bool captureSource = true, int dropper = 1)
    {
        var floor = new Entity { Index = index };
        var contents = new DynamicBuffer<ContainedObjectsBuffer>();
        contents.Add(Item(ObjectID.CopperBreastArmor, 1, 75));
        world.EntityManager.PutBuffer(floor, contents);
        world.EntityManager.Put(floor, new LocalTransform());
        world.EntityManager.Put(floor, new PickUpItemCD { targetEntity = new Entity { Index = dropper }, state = PickUpItemState.BlockPickupUntilReEnterStart });
        if (captureSource)
        {
            world.GetOrCreateSystemManaged<InventoryDeliverySourceObservationSystem>().TestUpdate();
            world.EntityManager.Put(floor, new PickUpItemCD { targetEntity = recipient, state = PickUpItemState.HasBeenPickedUp });
        }
        return floor;
    }

    static void DeliveryObservationChecks()
    {
        var (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        receiverBag[3] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 7);
        Check(skill.Status == SkillStatus.Running && !AIInventory.GoalDone(world, sender, out _),
            "missing floor plus matching two-sided counts alone does not assert delivery");
        Check(skill.Progress(world, sender).Contains("전 0, 현재 1, 증가 1"),
            "receiver quantity is measured even when floor was never bound");
        skill.Tick(world, sender, 30);
        Check(AICraft.Requests.Count == 1, "pending receipt observes without sending another drop");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        var floor = ArmorFloor(world, 300, recipient);
        // Real system callback at the inventory consumer boundary, before the next render-frame skill Tick.
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        world.EntityManager.Remove(floor);
        receiverBag[41] = Item(ObjectID.CopperBreastArmor, 1, 74);
        skill.Tick(world, sender, 1);
        Check(skill.Status == SkillStatus.Done && AIInventory.GoalDone(world, sender, out _),
            "server consumer observation survives a floor created and consumed between skill ticks");
        Check(skill.ConfirmedReceived == 1 && AICraft.Requests.Count == 1,
            "received armor moved into worn slot with durability change counts once");
        skill.Cleanup(world);
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        floor = ArmorFloor(world, 301, new Entity { Index = 8 });
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        world.EntityManager.Remove(floor);
        receiverBag[0] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 1);
        Check(skill.Status == SkillStatus.Failed && !AIInventory.GoalDone(world, sender, out _),
            "other player's captured pickup cannot be credited to recipient's incidental gain");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        ArmorFloor(world, 302, recipient, false);
        ArmorFloor(world, 303, recipient, false);
        world.GetOrCreateSystemManaged<InventoryDeliverySourceObservationSystem>().TestUpdate();
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        receiverBag[0] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 1);
        Check(skill.Status != SkillStatus.Done && !AIInventory.GoalDone(world, sender, out _),
            "simultaneous identical floor objects keep source attribution ambiguous");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        floor = ArmorFloor(world, 305, recipient, true, 8);
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        world.EntityManager.Remove(floor);
        receiverBag[0] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 7);
        Check(skill.Status == SkillStatus.Running && !AIInventory.GoalDone(world, sender, out _),
            "another sender's identical floor seen first cannot be attributed to this drop");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        skill.Cleanup(world);
        floor = ArmorFloor(world, 306, recipient);
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        world.EntityManager.Remove(floor);
        receiverBag[0] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 7);
        Check(skill.Status == SkillStatus.Running && !AIInventory.GoalDone(world, sender, out _),
            "cleanup unregisters source and pickup observers");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        world.EntityManager.Remove(recipient);
        skill.Tick(world, sender, 7);
        Check(skill.Progress(world, sender).Contains("수령자 전체 가방·착용 미관측"),
            "unavailable recipient inventory is unobserved rather than zero received");

        (world, sender, bag, recipient, receiverBag, skill) = DeliverySetup();
        bag[12] = default;
        floor = ArmorFloor(world, 304, recipient);
        world.GetOrCreateSystemManaged<InventoryDeliveryObservationSystem>().TestUpdate();
        receiverBag[0] = Item(ObjectID.CopperBreastArmor, 1, 75);
        skill.Tick(world, sender, 1);
        Check(skill.Status == SkillStatus.Running && !AIInventory.GoalDone(world, sender, out _),
            "captured pickup intent with unconsumed floor does not prove receipt");
    }
}
