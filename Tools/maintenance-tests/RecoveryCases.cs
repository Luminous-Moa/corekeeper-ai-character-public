using Inventory;
using Unity.Entities;
using Unity.NetCode;

public static class RecoveryCases
{
    static void Stop(Fixture f)=>AIBrain.ApplyChosen(f.World,f.Player,new AICandidate{kind="stop_action"});
    static AICandidate Recover(Fixture f)
    {return AIMaintenance.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});}
    static void DriveRecovery(Fixture f,AICandidate candidate)
    {var skill=f.Start(candidate);f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.End(skill);}
    public static void Stops()
    {
        var f=Fixture.Create();f.Plan("upgrade");var skill=f.Start(f.Exact());Stop(f);
        Program.Check(f.UI.Length==0&&f.Exact()!=null,"stop before Begin/posting leaves the original exact upgrade available");
        f=Fixture.Create();f.Plan("repair");skill=f.Start(f.Exact());f.World.EntityManager.RemoveComponent<UIActionsCD>(f.Player);f.Tick(skill);Stop(f);f.World.EntityManager.Put(f.Player,new UIActionsCD{lastActionTick=new(99)});
        Program.Check(f.UI.Length==0&&f.Exact()!=null,"unposted and unconfirmed Begin intent is released on explicit stop without a pending lock");
        f=Fixture.Create();f.Plan("repair");skill=f.Start(f.Exact());f.Tick(skill);Stop(f);
        Program.Check(f.Exact()==null&&AIMaintenance.Report(f.World,f.Player).Contains("게시"),"posted repair intent survives stop and blocks another repair until consumption is observed");
        f.ConsumeNext();AIMaintenance.Report(f.World,f.Player);f.Bag[12]=Fixture.Item(ObjectID.CookingPotHelm,30,0,17,5);
        Program.Check(f.Exact()!=null,"stopped repair intent retires after actual processing and target/material observation");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);Stop(f);f.ConsumeNext();
        var recover=Recover(f);Program.Check(recover!=null&&recover.maintenance.Mode=="recover"&&recover.life.Mode=="recover","observed own put after stop permits explicit recovery of the unupgraded original");
        DriveRecovery(f,recover);Program.Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==1&&f.CraftsApplied==0,"unupgraded original recovery never completes the upgrade goal or sends an upgrade");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();Stop(f);recover=Recover(f);
        Program.Check(recover!=null&&recover.maintenance.Mode=="recover"&&recover.life.Mode=="upgrade"&&ReferenceEquals(recover.life.Goal,AIGoals.Goal),"same-goal recovery of an observed upgraded pending retains the original upgrade binding");
        DriveRecovery(f,recover);Program.Check(f.CraftsApplied==1&&AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"recovered already-upgraded pending completes the original goal exactly once without re-upgrading");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();Stop(f);var old=AIGoals.Goal;f.Plan("upgrade");recover=Recover(f);
        Program.Check(recover!=null&&!ReferenceEquals(recover.life.Goal,old)&&!recover.life.MatchesGoal,"goal change preserves own pending while recovery does not bind its upgraded result to the new upgrade goal");
        DriveRecovery(f,recover);Program.Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==1&&f.CraftsApplied==1,"recovery after a changed goal leaves the new upgrade count unchanged");
    }
    public static void Barriers()
    {
        var f=Fixture.Create();f.Plan("upgrade");var skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);
        Program.Check(Recover(f)==null&&f.UI.Length==2,"posted but unapplied upgrade blocks recovery without resending the request");
        var posted=f.PostToConsumer();var snapshot=f.ForgeItems[5];var data=snapshot.objectData;data.variation=7;data.variationUpdateCount++;snapshot.objectData=data;f.ForgeItems[5]=snapshot;var next=posted.tick;next.Add(1);f.ApplyTick(next);
        Program.Check(Recover(f)==null&&f.Crafts.Length==1,"a newer applied tick with the matching CraftBuffer still pending cannot permit recovery");
        f.Crafts.Clear();var recover=Recover(f);
        Program.Check(recover!=null&&f.UI.Length==2,"only newer tick plus last-action receipt plus absent matching consumer buffer permits recovery");
        DriveRecovery(f,recover);Program.Check(f.MovesApplied==2&&f.UI.Count(x=>x.actionData.action==UIInputAction.Craft)==1,"recovery posts one exact transfer and never repeats the completed level request");
        f=Fixture.Create();f.Plan("repair");skill=f.Start(f.Exact());f.Tick(skill);
        var move=new AIInventoryMove{mode="hold",fromEntity=f.Player,toEntity=f.Player,from=12,to=13,beforeFrom=f.Bag[12],beforeTo=f.Bag[13]};
        Program.Check(AIMaintenance.SlotBarrier(f.World,f.Player,move)!=null,"unprocessed own repair protects only its exact source slot from another inventory move");move.from=20;
        Program.Check(AIMaintenance.SlotBarrier(f.World,f.Player,move)==null,"repair processing barrier does not reserve unrelated player slots");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);Stop(f);var put=f.PostToConsumer();f.ForgeItems[5]=f.Bag[12];f.Bag[12]=default;next=put.tick;next.Add(1);f.ApplyTick(next);
        Program.Check(Recover(f)==null&&f.Moves.Length==1,"two-sided put changes do not establish recoverable ownership while its InventoryChangeBuffer entry remains");
        f.Moves.Clear();Program.Check(Recover(f)!=null,"put ownership becomes recoverable only after matching inventory consumer entry disappears");
    }
    public static void Observation()
    {
        var f=Fixture.Create();f.Plan("upgrade");var skill=f.Start(f.Exact());f.Tick(skill);AIVision.Visible=false;Boundary.BufferReads.Clear();f.Tick(skill);AIMaintenance.Report(f.World,f.Player);
        Program.Check(skill.Status==SkillStatus.Failed&&!Boundary.BufferReads.Any(x=>x.entity==f.Forge&&x.type==typeof(ContainedObjectsBuffer)),"post-transfer observation failure retains intent without reading an out-of-view forge item buffer");AIVision.Visible=true;
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext(changeTarget:false);f.Tick(skill);
        Program.Check(skill.Status==SkillStatus.Done&&((ILifeResult)skill).ConfirmedWork==0&&f.UI.Length==2&&Recover(f)!=null,"consumed upgrade with no level change remains work zero and offers explicit original recovery without retry");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.None)f.Bag[i]=Fixture.Item(ObjectID.Wood,1);f.Tick(skill);
        Program.Check(skill.Status==SkillStatus.Running&&((ILifeResult)skill).ConfirmedWork==0&&f.UI.Length==2&&f.ForgeItems[5].variation==7,"lost recovery space after actual upgrade leaves upgraded own pending in forge without false completion or dropping");
        Stop(f);f.Bag[3]=default;var recover=Recover(f);DriveRecovery(f,recover);
        Program.Check(f.CraftsApplied==1&&AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"freeing a permitted main slot allows exact pending recovery to finish the same upgrade once");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);var posted=f.PostToConsumer();var current=f.ForgeItems[5];var d=current.objectData;d.variation=8;current.objectData=d;f.ForgeItems[5]=current;f.Crafts.Clear();var next=posted.tick;next.Add(1);f.ApplyTick(next);Stop(f);
        Program.Check(Recover(f)==null&&f.UI.Length==2&&AIMaintenance.Report(f.World,f.Player).Contains("예상"),"unexpected level change remains unconfirmed and cannot silently recover or re-upgrade a substituted state");
    }
    public static void IdentityRegression()
    {
        var f=Fixture.Create();f.Plan("upgrade");var candidate=f.Exact();var original=candidate.maintenance.BeforeItem;var skill=f.Start(candidate);f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);f.ConsumeNext();AIMaintenance.Report(f.World,f.Player);
        Program.Check(f.ForgeItems[5].variation==7&&Recover(f)!=null,"legitimate observed expected-level pending remains recoverable before identity replacement");
        f.ForgeItems[5]=original;int requests=f.UI.Length;var reverted=Recover(f);
        bool rejected=reverted==null||AIMaintenance.Stale(f.World,f.Player,reverted)!=null;
        if(reverted!=null){AIBrain.ApplyChosen(f.World,f.Player,reverted);if(AIBody.Current?.Status==SkillStatus.Running)f.Tick(AIBody.Current);}
        Program.Check(rejected&&f.UI.Length==requests&&!AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"M2: after expected-level snapshot confirmation, a same-ID/aux original-level replacement cannot be recovered or satisfy upgrade");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);f.ConsumeNext();AIMaintenance.Report(f.World,f.Player);var replaced=f.ForgeItems[5];var replacementData=replaced.objectData;replacementData.variationUpdateCount++;replaced.objectData=replacementData;f.ForgeItems[5]=replaced;requests=f.UI.Length;reverted=Recover(f);
        Program.Check((reverted==null||AIMaintenance.Stale(f.World,f.Player,reverted)!=null)&&f.UI.Length==requests&&!AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"M2: even unchanged expected variation with a replaced confirmed state stamp is not silently adopted for recovery");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);
        int destination=f.UI.Last().actionData.inventoryChangeData.index2;var touching=new AIInventoryMove{mode="hold",fromEntity=f.Player,toEntity=f.Player,from=destination,to=15};
        Program.Check(AIMaintenance.SlotBarrier(f.World,f.Player,touching)!=null,"posted take protects its exact actual main destination slot from a competing inventory move");touching.from=20;touching.to=21;
        Program.Check(AIMaintenance.SlotBarrier(f.World,f.Player,touching)==null,"posted take leaves unrelated main slots unreserved");
        Program.Check(Recover(f)==null,"already-posted recovery transfer survives stop and stays blocked until its processing receipt");
        f.ConsumeNext();touching.from=destination;Program.Check(AIMaintenance.SlotBarrier(f.World,f.Player,touching)==null,"take destination barrier retires only after actual posted request processing proof");var completedRecovery=Recover(f);requests=f.UI.Length;var receiving=f.Start(completedRecovery);f.Tick(receiving);f.End(receiving);f.End(receiving);
        Program.Check(((ILifeResult)receiving).ConfirmedWork==1&&((ILifeResult)receiving).ConfirmedReceived==1&&f.UI.Length==requests&&AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"processed interrupted take is observed once without another move and completes same upgrade only once");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);f.Plan("upgrade");f.ConsumeNext();var changedGoalRecovery=Recover(f);DriveRecovery(f,changedGoalRecovery);
        Program.Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==1&&f.CraftsApplied==1,"goal change after upgrade posting and before consumption keeps its pending result outside the new goal quota");
    }
}
