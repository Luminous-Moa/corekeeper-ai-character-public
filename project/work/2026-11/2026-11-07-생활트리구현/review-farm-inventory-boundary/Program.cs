using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;

// Compiles frozen 24c61c2 production AIInventory / InventorySkill, with their existing ECS boundary adapter.
// No production policy is copied here. MatchesGoal=true is independently traced to production AILife.Matches
// accepting an empty cursor Item while inventory navigation selects only Mode/Where/Slot.
var assembly = typeof(AIInventory).Assembly;
var fixture = assembly.GetType("ReviewFixture");
object Call(string name, params object[] args) => fixture.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
ContainedObjectsBuffer Item(ObjectID id, int amount) => new() { objectData = new() { objectID = id, amount = amount } };
var setup = ((World, Entity, DynamicBuffer<ContainedObjectsBuffer>))Call("Setup");
var (world, player, bag) = setup;
var (chest, box) = ((Entity, DynamicBuffer<ContainedObjectsBuffer>))Call("Chest", world);
box[0] = Item(ObjectID.Wood, 5);
box[1] = Item(ObjectID.CopperSword, 100);
var goal = new AIGoal { kind = GoalKind.Take, item = ObjectID.Wood, count = 1, where = "chest:1,0" };
Call("Goal", world, player, goal);
var observation = new AIObservation { pos = AIBody.Pos(world, player) };
var selection = new LifeSelection { Branch = "inventory", Mode = "take", Where = "chest:1,0", Item = "", Slot = -1, RequestedCount = 1, Goal = goal, MatchesGoal = true };
var inspect = AIInventory.LifeEntries(world, player, observation, "", selection)[0].Candidate;
var inspectSkill = new InventorySkill(inspect.inventory);
if (!inspectSkill.Start(world, player, 0)) throw new Exception(inspectSkill.Reason);
inspectSkill.Tick(world, player, 0);
var slots = AIInventory.LifeEntries(world, player, observation, "", selection);
Console.WriteLine("goal=" + goal.item + "; empty cursor item lists: " + string.Join(" | ", slots.Select(x => x.Text)));
if (!slots.Any(x => x.Id == "1" && x.Text.Contains("CopperSword"))) throw new Exception("Expected wrong-item source slot was filtered");
selection.Slot = 1;
var leaf = AIInventory.ExactCandidate(world, player, observation, "", selection);
Console.WriteLine($"selected={leaf.itemId}; bound_original_goal={ReferenceEquals(leaf.inventory.goal, goal)}; MatchesGoal={leaf.life.MatchesGoal}; count={leaf.inventory.count}");
var transfer = new InventorySkill(leaf.inventory);
if (!transfer.Start(world, player, 1)) throw new Exception(transfer.Reason);
transfer.Tick(world, player, 1);
box[1] = default;
bag[leaf.inventory.to] = Item(ObjectID.CopperSword, 100);
transfer.Tick(world, player, 2);
var done = AIInventory.GoalDone(world, player, out var note);
Console.WriteLine($"actual_received={transfer.ConfirmedReceived}; received_item={bag[leaf.inventory.to].objectID}; Wood_goal_moved={AIInventory.MovedForGoal(goal)}; Wood_goal_done={done}; result={note}");
if (!done || AIInventory.MovedForGoal(goal) != 1 || bag[leaf.inventory.to].objectID != ObjectID.CopperSword) throw new Exception("Boundary did not reproduce");
Console.WriteLine("REPRODUCED: wrong-item receipt advances and satisfies the original Wood goal.");
