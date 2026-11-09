using Inventory;
using Unity.Entities;
using Unity.NetCode;

public static class Cases
{
    public static void Repair()
    {
        var f=Fixture.Create();f.Plan("repair");
        Program.Check(AIGoals.Goal.kind==GoalKind.Maintenance&&AIGoals.Goal.mode=="repair"&&AIGoals.Goal.slot==12&&AIGoals.Goal.count==1,"actual Protocol -> PlanStep -> SetGoalFrom -> AIGoals preserves maintain selectors");
        var direct=f.Exact();if(direct==null)throw new Exception(AIMaintenance.Report(f.World,f.Player));
        var tree=AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal)).Single(x=>x.Candidate!=null).Candidate;
        Program.Check(direct.key==tree.key&&direct.maintenance.BeforeItem.EqualsExact(tree.maintenance.BeforeItem),"repair tree and direct ExactCandidate share frozen slot identity and request key");
        Program.Check(Boundary.RepairFlag&&!Boundary.ReinforceFlag&&Boundary.RepairRecipe.objectID==ObjectID.CookingPotHelm&&Boundary.RepairRecipe.amount==30&&Boundary.RepairStation==f.Repair&&Boundary.RepairPlayer==f.Player&&Boundary.ChestStart==1&&Boundary.Inventories.SequenceEqual(new[]{f.Player}),"repair game cost helper receives selected amount, real AI player/station and main-only inventory arguments");
        Program.Check(Boundary.NativeCreated==Boundary.NativeDisposed,"game helper NativeLists are disposed after copied cost rows");
        var skill=f.Start(direct);f.Tick(skill);var request=f.UI.Single();var action=request.actionData.craftActionData;
        Program.Check(skill is MaintenanceSkill&&request.actionData.action==UIInputAction.Craft&&action.craftAction==CraftAction.RepairOrReinforce&&action.targetInventoryEntity==f.Player&&action.craftingEntity==f.Repair&&action.playerEntity==f.Player&&action.int0==12&&action.int1==1&&!action.bool0&&!action.bool1,"production Make/Start/Tick posts normal single repair Create payload with reinforce/free false");
        Program.Check(((ILifeResult)skill).ConfirmedWork==0&&f.Bag[12].amount==30,"queue acceptance is not repair work or target mutation");
        f.Tick(skill);Program.Check(f.UI.Length==1&&skill.Status==SkillStatus.Running,"unapplied repair request waits without duplicate posting");
        f.ConsumeNext();f.Tick(skill);f.End(skill);
        Program.Check(skill.Status==SkillStatus.Done&&((ILifeResult)skill).ConfirmedWork==1&&((ILifeResult)skill).ConfirmedReceived==0&&f.Bag[12].amount==90&&f.Bag[0].amount==18,"consumed request confirms actual target max durability and separate main material decrease");
        Program.Check(AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal))&&AIBrain.ExecutionObservation.Contains("확인된 작업량 1"),"production OnSkillEnded records one repair and supplies next decision observation");
    }
    public static void Upgrade()
    {
        var f=Fixture.Create();f.Plan("upgrade");var direct=f.Exact();if(direct==null)throw new Exception(AIMaintenance.Report(f.World,f.Player));
        Program.Check(direct.maintenance.BeforeLevel==6&&direct.maintenance.ExpectedLevel==7&&Boundary.UpgradeLevel==7&&direct.maintenance.Costs.Any(x=>x.Item==ObjectID.IronBar&&x.Needed==10)&&direct.maintenance.Costs.Any(x=>x.Item==ObjectID.AncientCoin&&x.Needed==132),"source 8555 fixture selects prefab level six -> seven and the fixed source cost rows without a fixture formula");
        var skill=f.Start(direct);f.Tick(skill);var put=f.UI.Single();var change=put.actionData.inventoryChangeData;
        Program.Check(put.actionData.action==UIInputAction.InventoryChange&&change.inventoryAction==InventoryAction.MoveAmount&&change.inventory1==f.Player&&change.entityOrInventory2==f.Forge&&change.index1==12&&change.index2==5&&change.amount==1,"upgrade first posts normal one-item MoveAmount to forge local0 absolute slot five");
        Program.Check(!f.UI.Any(x=>x.actionData.action==UIInputAction.Craft),"no Upgrade request targets an item that remains in the player bag");
        f.ConsumeNext();f.Tick(skill);
        var upgrade=f.UI.Last();var action=upgrade.actionData.craftActionData;
        Program.Check(f.Bag[12].objectID==ObjectID.None&&f.ForgeItems[5].auxDataIndex==17&&upgrade.actionData.action==UIInputAction.Craft&&action.craftAction==CraftAction.Upgrade&&action.targetInventoryEntity==f.Forge&&action.craftingEntity==f.Forge&&action.playerEntity==f.Player&&action.int0==5&&!action.bool0,"observed two-sided put precedes exact normal Upgrade payload with forge inventory and crafting entity");
        Program.Check(direct.maintenance.BeforeItem.variationUpdateCount==2&&f.ForgeItems[5].variationUpdateCount==0,"normal TryMove consumer preserves forge destination stamp instead of copying the source slot stamp");
        f.ConsumeNext();f.Tick(skill);
        Program.Check(f.ForgeItems[5].variation==7&&((ILifeResult)skill).ConfirmedWork==0&&((ILifeResult)skill).ConfirmedReceived==0,"actual level change in forge remains zero completed work until bag recovery");
        var take=f.UI.Last();Program.Check(take.actionData.action==UIInputAction.InventoryChange&&take.actionData.inventoryChangeData.inventory1==f.Forge&&take.actionData.inventoryChangeData.entityOrInventory2==f.Player,"upgrade recovery uses normal MoveAmount back to main inventory");
        f.ConsumeNext();f.Tick(skill);f.End(skill);
        Program.Check(skill.Status==SkillStatus.Done&&((ILifeResult)skill).ConfirmedWork==1&&((ILifeResult)skill).ConfirmedReceived==1&&f.ForgeItems[5].objectID==ObjectID.None&&f.Bag.Any(x=>x.objectID==ObjectID.CookingPotHelm&&x.variation==7&&x.auxDataIndex==17),"upgrade completes exactly one stage only after forge decrease and exact upgraded main receipt");
        Program.Check(f.Bag.Any(x=>x.objectID==ObjectID.CookingPotHelm&&x.variation==7&&x.variationUpdateCount==0),"normal TryMove keeps empty main destination stamp zero when the upgraded source stamp was one");
        Program.Check(f.CraftsApplied==1&&f.MovesApplied==2&&f.Bag[1].amount==10&&f.Bag[2].amount==132&&AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"one upgrade request and two normal transfers preserve actual cost and single-goal progress");
    }
}
