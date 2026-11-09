using System.Reflection;
using Unity.Entities;

// All inventory and matching/binding/count policy is frozen production c4a0f5e source.
// ECS, queue and supplied post-transfer buffers are fixture boundaries.
var fixture = typeof(AIInventory).Assembly.GetType("ReviewFixture");
object Call(string name, params object[] args) => fixture.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
ContainedObjectsBuffer Item(ObjectID id, int amount, int variation = 0) => new() { objectData = new() { objectID = id, amount = amount, variation = variation } };
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }

foreach (bool store in new[] { false, true })
{
    var (world, player, bag) = ((World, Entity, DynamicBuffer<ContainedObjectsBuffer>))Call("Setup");
    var (chest, box) = ((Entity, DynamicBuffer<ContainedObjectsBuffer>))Call("Chest", world);
    var source = store ? bag : box;
    source[0] = Item(ObjectID.Wood, 10);
    source[1] = Item(ObjectID.CopperOre, 40, 2);
    var goal = new AIGoal { kind = store ? GoalKind.Store : GoalKind.Take, item = ObjectID.Wood, count = 3, where = "chest:1,0" };
    Call("Goal", world, player, goal);
    var observation = new AIObservation { pos = AIBody.Pos(world, player) };
    // No manual Goal/MatchesGoal/RequestedCount: production Resolve performs the initial empty-item binding.
    var selection = AILife.Resolve(new LifeSelection { Branch = "inventory", Mode = store ? "store" : "take", Where = "chest:1,0" });
    var inspect = AIInventory.LifeEntries(world, player, observation, "", selection).Single().Candidate;
    var inspecting = new InventorySkill(inspect.inventory);
    Check(inspecting.Start(world, player, 0), "inspect accepts unopened own chest");
    inspecting.Tick(world, player, 0);
    var entries = AIInventory.LifeEntries(world, player, observation, "", selection);
    Check(entries.Any(x => x.Id == "1" && x.Text.Contains("별도 행동") && x.Text.Contains("1개")), "actual Ore slot describes a separate one-unit action");
    selection.Slot = 1;
    var other = AIInventory.ExactCandidate(world, player, observation, "", selection);
    Check(other.itemId == ObjectID.CopperOre && other.itemVariation == 2 && other.itemCount == 1 && !other.life.MatchesGoal && other.life.Goal == null && other.inventory.goal == null, "actual Ore identity resolves to separate one-unit payload without original goal");
    var moving = new InventorySkill(other.inventory);
    Check(moving.Start(world, player, 1), "separate exact transfer starts");
    moving.Tick(world, player, 1);
    Check(AICraft.Requests.Last().mode == "move" && AICraft.Requests.Last().count == 1, "normal MoveAmount asks for one Ore");
    source[1] = Item(ObjectID.CopperOre, 39, 2);
    var target = store ? box : bag;
    target[other.inventory.to] = Item(ObjectID.CopperOre, 1, 2);
    moving.Tick(world, player, 2);
    Check(moving.Status == SkillStatus.Done && AIInventory.MovedForGoal(goal) == 0 && !AIInventory.GoalDone(world, player, out _), "observed Ore receipt leaves original Wood progress zero and incomplete");
    selection.Slot = 0;
    var wood = AIInventory.ExactCandidate(world, player, observation, "", selection);
    Check(wood.itemId == ObjectID.Wood && wood.itemCount == 3 && wood.life.MatchesGoal && ReferenceEquals(wood.inventory.goal, goal), "actual Wood identity resolves to original remaining three-unit payload");
    moving = new InventorySkill(wood.inventory);
    Check(moving.Start(world, player, 3), "matching Wood transfer starts");
    moving.Tick(world, player, 3);
    source[0] = Item(ObjectID.Wood, 7);
    target[wood.inventory.to] = Item(ObjectID.Wood, 3);
    moving.Tick(world, player, 4);
    Check(moving.Status == SkillStatus.Done && AIInventory.MovedForGoal(goal) == 3 && AIInventory.GoalDone(world, player, out _), "observed Wood receipt advances exactly three and satisfies original goal");
    Console.WriteLine($"FINAL mode={selection.Mode}; OreRequested={other.itemCount}; originalWoodMoved={AIInventory.MovedForGoal(goal)}; confirmedWoodWork={moving.ConfirmedWork}; confirmedWoodReceived={moving.ConfirmedReceived}");
}
Console.WriteLine("FINAL BOUNDARY PASS: take and store actual identity -> production Resolve -> separate Ore1/Wood0 -> matching Wood3/Wood3.");
