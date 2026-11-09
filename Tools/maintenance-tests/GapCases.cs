using Inventory;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// 2026-11-08 선택지 누락 점검 정비 M1~M3: repair beside a forge pending, the walk back to an unseen forge, and the
// give-up choice for a pending that cannot be recovered (with the upper LLM told what happened).
public static class GapCases
{
    static void Stop(Fixture f)=>AIBrain.ApplyChosen(f.World,f.Player,new AICandidate{kind="stop_action"});
    static AICandidate Recover(Fixture f)=>AIMaintenance.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});
    static AICandidate Repair13(Fixture f)=>AIMaintenance.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="repair",Slot=13,Where=f.RepairId});
    static AICandidate Upgrade13(Fixture f)=>AIMaintenance.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="upgrade",Slot=13,Where=f.ForgeId});
    // The upgraded item stays in the forge after a stop: put and level change observed, take not started.
    static Fixture UpgradedPending()
    {
        var f=Fixture.Create();f.Plan("upgrade");f.Bag[13]=Fixture.Item(ObjectID.CookingPotHelm,30,0,18,0);
        var skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();Stop(f);
        return f;
    }
    static AICandidate AbandonAtRoot(Fixture f)=>AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"}).Select(x=>x.Candidate).FirstOrDefault(x=>x?.maintenance?.Mode=="abandon");
    static Entity Other(Fixture f,int index,string name,float x)
    {var e=new Entity{Index=index,Version=1};f.World.EntityManager.Put(e,new PlayerGhost{onlineName=name+"-online"});f.World.EntityManager.Put(e,new PlayerCustomizationCD{customization=new PlayerCustomization{name=name}});f.World.EntityManager.Put(e,new LocalTransform{Position=new float3(x,0,0)});return e;}

    public static void RepairBesidePending()
    {
        var f=UpgradedPending();
        Program.Check(Recover(f)!=null,"fixture: upgraded item waits in the forge for recovery");
        var repair=Repair13(f);
        Program.Check(repair!=null&&repair.maintenance.Mode=="repair","M3: a repair of another item is offered while an upgraded forge pending waits (no new item input)");
        Program.Check(Upgrade13(f)==null,"M3: a new upgrade input stays blocked by the forge pending");
        var skill=f.Start(repair);f.Tick(skill);
        Program.Check(Repair13(f)==null,"M3: a second repair waits for the posted repair's processing");
        f.ConsumeNext();f.Tick(skill);f.End(skill);
        Program.Check(skill.Status==SkillStatus.Done&&((ILifeResult)skill).ConfirmedWork==1&&f.Bag[13].amount==90,"M3: the repair beside the pending posts one normal request and confirms the real durability change");
        var recover=Recover(f);
        Program.Check(recover!=null&&recover.maintenance.Mode=="recover","M3: the forge pending is kept intact by the repair and stays recoverable");
        var r=f.Start(recover);f.Tick(r);f.ConsumeNext();f.Tick(r);f.End(r);
        Program.Check(r.Status==SkillStatus.Done&&f.Bag.Any(x=>x.objectID==ObjectID.CookingPotHelm&&x.variation==7&&x.auxDataIndex==17)&&f.CraftsApplied==2,"M3: recovery after the repair receives the exact upgraded item with no second upgrade request");

        f=Fixture.Create();f.Plan("upgrade");f.Bag[13]=Fixture.Item(ObjectID.CookingPotHelm,30,0,18,0);
        skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);Stop(f);
        Program.Check(Repair13(f)==null&&AIMaintenance.Report(f.World,f.Player).Contains("처리 확인 뒤 일반 수리 가능"),"M3: while the forge pending's own upgrade request is unprocessed a repair waits (one request in flight)");
        f.ConsumeNext();
        Program.Check(Repair13(f)!=null,"M3: the repair is offered once that request's processing is observed");
        f.Plan("repair",13);
        Program.Check(Upgrade13(f)==null&&AIMaintenance.Report(f.World,f.Player).Contains("투입 없는 일반 수리는 가능")&&f.Exact()!=null,"M3: under a new repair goal the old forge pending blocks only item input, named in the fact, and the goal's repair is offered");
    }

    public static void WalkToUnseenForge()
    {
        var f=UpgradedPending();Recover(f);   // the level change is observed while the forge is in sight
        AIVision.Visible=false;var walk=Recover(f);
        Program.Check(walk!=null&&walk.kind=="walk"&&walk.desc.Contains("가서 자기 pending 확인")&&!walk.progress&&walk.life!=null,"M1: an own pending at an unseen forge offers the walk to its exact place instead of nothing");
        Program.Check(walk.life.Mode=="upgrade"&&ReferenceEquals(walk.life.Goal,AIGoals.Goal),"M1: the walk keeps the original upgrade binding of a same-goal upgraded pending");
        var root=AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"});
        Program.Check(root.Any(x=>x.Op=="mode"&&x.Id=="recover")&&!root.Any(x=>x.Candidate?.maintenance?.Mode=="abandon"),"M1: out of sight the pending is not judged lost (no give-up choice)");
        AIVision.Visible=true;var recover=Recover(f);
        Program.Check(recover!=null&&recover.kind=="maintain"&&recover.maintenance.Mode=="recover","M1: back in sight the normal exact recovery is offered");
        var entries=AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});
        AIVision.Visible=false;entries=AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});
        Program.Check(entries.Any(x=>x.Candidate?.kind=="walk"),"M1: the navigated recover leaf carries the walk candidate when the forge is unseen");
        AIVision.Visible=true;
    }

    // Review-5 G5 (F4): the recovered item keeps its aux identity; a level different from the one the pending last saw
    // still counts as the arrival.
    public static void AuxArrival()
    {
        var f=UpgradedPending();var take=f.Start(Recover(f));f.Tick(take);f.ConsumeNext();Stop(f);
        for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.CookingPotHelm&&f.Bag[i].auxDataIndex==17){var x=f.Bag[i];var d=x.objectData;d.variation=9;x.objectData=d;f.Bag[41]=x;f.Bag[i]=default;}
        Program.Check(Upgrade13(f)!=null&&AIMaintenance.Report(f.World,f.Player).Contains("들어온 것을 봄"),"review-5 G5/F4: the same aux item back at another level is counted as the own arrival");
    }
    public static void GiveUpLostPending()
    {
        // Taken by another player: the players near the forge while the item was seen and when the loss was seen.
        var f=UpgradedPending();var host=Other(f,30,"호스트",3);
        Program.Check(Recover(f)!=null&&AbandonAtRoot(f)==null,"M2: an intact pending has no give-up choice");
        f.World.EntityManager.Put(host,new LocalTransform{Position=new float3(40,0,0)});Other(f,31,"멀리있는사람",50);
        f.ForgeItems[5]=default;
        var abandon=AbandonAtRoot(f);
        Program.Check(Recover(f)==null&&AIMaintenance.Report(f.World,f.Player).Contains("대장간 입력 칸이 비어 있음"),"M2: an emptied forge input is told as the fact that the item cannot be recovered");
        Program.Check(abandon!=null&&abandon.kind=="maintain"&&!abandon.progress&&abandon.desc.Contains("호스트")&&abandon.desc.Contains("추정")&&!abandon.desc.Contains("멀리있는사람"),"M2: the give-up choice names the player who was near the forge while the item was there, marked as a guess");
        var leaf=AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});
        Program.Check(leaf.Any(x=>x.Candidate?.maintenance?.Mode=="abandon"),"M2: the recover leaf also offers the give-up choice beside the fact");
        Program.Check(Upgrade13(f)==null,"M2: before giving up, the lost pending still blocks a new upgrade input");
        int replans=AIPlanner.Replans;AIBrain.ApplyChosen(f.World,f.Player,abandon);var skill=AIBody.Current;f.Tick(skill);
        Program.Check(skill is MaintenanceAbandonSkill&&skill.Status==SkillStatus.Done,"M2: the chosen give-up runs through the production maintain dispatch and ends at once");
        Program.Check(AIPlanner.Replans==replans+1&&AIPlanner.LastReplan.Contains("호스트")&&AIPlanner.LastReplan.Contains("잃음")&&!AIPlanner.LastReplan.Contains("따지"),"M2: the upper LLM is told what happened through the existing action_replan path, with no instruction what to do");
        Program.Check(AIMaintenance.Report(f.World,f.Player).Contains("자기 pending 없음")&&Upgrade13(f)!=null&&AbandonAtRoot(f)==null,"M2: after giving up the pending is cleared and a new upgrade can be chosen");

        // The forge itself is gone (seen): unknown who or where, no names.
        f=UpgradedPending();Other(f,30,"호스트",3);Recover(f);f.World.EntityManager.Remove(f.Forge);
        Program.Check(AbandonAtRoot(f)==null,"review I2: a forge missing at its seen tile is not judged gone at once (a reloading chunk shows the tile first)");
        f.ServerTick=new(f.ServerTick.Value+121);abandon=AbandonAtRoot(f);
        Program.Check(abandon!=null&&abandon.desc.Contains("대장간이 없음")&&abandon.desc.Contains("모름")&&!abandon.desc.Contains("호스트"),"M2: a removed forge is told as unknown who/where, without guessing a name");

        // Review I2: the chunk was unloaded and loaded again, the same forge is a new entity at the same tile.
        f=UpgradedPending();Recover(f);var oldItems=f.ForgeItems;f.World.EntityManager.Remove(f.Forge);
        var again=new Entity{Index=33,Version=1};var em=f.World.EntityManager;
        em.Put(again,new ObjectDataCD{objectID=ObjectID.UpgradeForge});em.Put(again,new LocalTransform{Position=new float3(2,0,0)});em.Put(again,new CraftingCD{outputSlotIndex=6});
        var items=Fixture.Buffer<ContainedObjectsBuffer>(8);items[5]=oldItems[5];em.PutBuffer(again,items);var inv=Fixture.Buffer<InventoryBuffer>();inv.Add(new(){startIndex=5,size=1,sizeX=1,sizeY=1,extraInventorySizeSlot=-1});em.PutBuffer(again,inv);
        var back=Recover(f);
        Program.Check(back!=null&&back.kind=="maintain"&&back.maintenance.Mode=="recover"&&back.entity==again&&AbandonAtRoot(f)==null,"review I2: the pending finds the reloaded forge at its tile again and offers the exact recovery, not a give-up");
        f.ServerTick=new(f.ServerTick.Value+500);
        Program.Check(AbandonAtRoot(f)==null&&!AIMaintenance.Report(f.World,f.Player).Contains("대장간이 없음"),"review I2: a reloaded forge with the item is never told as gone");

        // Nobody near: told as unknown.
        f=UpgradedPending();f.ForgeItems[5]=default;abandon=AbandonAtRoot(f);
        Program.Check(abandon!=null&&abandon.desc.Contains("다른 플레이어가 없었음")&&abandon.desc.Contains("그 사이는 보지 못함"),"M2: with nobody near the forge the taker is told as unknown, limited to the two seen moments (review L7)");
        // Review L6: the own recovered item worn right after the take is not a loss.
        f=UpgradedPending();Other(f,30,"호스트",3);var take=f.Start(Recover(f));f.Tick(take);f.ConsumeNext();Stop(f);
        int landed=-1;for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.CookingPotHelm&&f.Bag[i].auxDataIndex==17)landed=i;
        if(landed>=0){f.Bag[41]=f.Bag[landed];f.Bag[landed]=default;}
        Program.Check(landed>=0&&AbandonAtRoot(f)==null&&!AIMaintenance.Report(f.World,f.Player).Contains("호스트"),"review L6: the own recovered item worn right after the take is not judged taken by someone near");
        f=UpgradedPending();Other(f,30,"호스트",3);take=f.Start(Recover(f));f.Tick(take);f.ConsumeNext();Stop(f);
        for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.CookingPotHelm&&f.Bag[i].auxDataIndex==17){f.Bag[41]=f.Bag[i];f.Bag[i]=default;}
        Recover(f);   // the arrival is observed once (worn right away: one more of the exact item on the player, not in the receipt slot)
        f.Bag[41]=default;
        for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.CookingPotHelm&&f.Bag[i].auxDataIndex==17)f.Bag[i]=default;
        Program.Check(AbandonAtRoot(f)==null,"review L6: after the own processed take was seen arriving, a later move of the item names nobody");
        Program.Check(Upgrade13(f)!=null&&(AIMaintenance.Report(f.World,f.Player).Contains("들어온 것을 봄")||AIMaintenance.Report(f.World,f.Player).Contains("들어온 것을 봤고")),"review-2 N4/review-3 R2: that pending ends as received (arrival seen, moved since) and a new upgrade can be chosen");
        f=UpgradedPending();take=f.Start(Recover(f));f.Tick(take);f.ConsumeNext();Stop(f);
        for(int i=0;i<40;i++)if(f.Bag[i].objectID==ObjectID.CookingPotHelm&&f.Bag[i].auxDataIndex==17){f.Bag[41]=f.Bag[i];f.Bag[i]=default;}
        Program.Check(Upgrade13(f)!=null&&AIMaintenance.Report(f.World,f.Player).Contains("지금 지님"),"review-2 N4: the own recovered item worn right after the take ends the pending as received");
        // Review-3 R2: someone takes the item first; the own take is processed on an empty input and nothing arrives.
        f=UpgradedPending();Other(f,30,"호스트",3);Recover(f);take=f.Start(Recover(f));f.Tick(take);f.ForgeItems[5]=default;f.ConsumeNext();Stop(f);
        abandon=AbandonAtRoot(f);
        Program.Check(abandon!=null&&abandon.desc.Contains("들어온 것을 보지 못함")&&abandon.desc.Contains("호스트")&&abandon.desc.Contains("추정"),"review-3 R2: an own take that brought nothing is the loss flow (give-up choice naming the near player as a guess), not a receipt");
        Program.Check(!AIMaintenance.Report(f.World,f.Player).Contains("회수 완료로 기록"),"review-3 R2: the pending is not recorded as recovered");
        replans=AIPlanner.Replans;AIBrain.ApplyChosen(f.World,f.Player,abandon);f.Tick(AIBody.Current);
        Program.Check(AIPlanner.Replans==replans+1&&AIPlanner.LastReplan.Contains("들어온 것을 보지 못함"),"review-3 R2: giving it up tells the upper LLM what happened");
        // Review-2 N5: an absence seen, then out of sight, does not carry its start into a later look.
        f=UpgradedPending();Recover(f);var forgeKeep=f.Forge;f.World.EntityManager.Remove(f.Forge);
        Program.Check(AbandonAtRoot(f)==null,"fixture: the first missing look only starts the wait");
        AIVision.Visible=false;AbandonAtRoot(f);f.ServerTick=new(f.ServerTick.Value+500);AIVision.Visible=true;
        Program.Check(AbandonAtRoot(f)==null,"review-2 N5: after an unseen gap the first missing look restarts the 120-tick wait instead of judging the forge gone");
        f.ServerTick=new(f.ServerTick.Value+121);
        Program.Check(AbandonAtRoot(f)!=null,"review-2 N5: seen missing for 120 ticks again, the give-up is offered");

        // Unseen: never judged lost. Reappearing: the loss is withdrawn and a stale give-up refuses.
        f=UpgradedPending();var kept=f.ForgeItems[5];f.ForgeItems[5]=default;AIVision.Visible=false;
        Program.Check(AbandonAtRoot(f)==null,"M2: a forge out of sight is not judged lost");
        AIVision.Visible=true;abandon=AbandonAtRoot(f);f.ForgeItems[5]=kept;
        Program.Check(AbandonAtRoot(f)==null&&Recover(f)!=null,"M2: the item seen again withdraws the loss and recovery is offered again");
        Program.Check(AIMaintenance.Stale(f.World,f.Player,abandon)!=null,"M2: a give-up chosen before the item was seen again is refused as stale");

        // A different item in the input counts as taken too.
        f=UpgradedPending();f.ForgeItems[5]=Fixture.Item(ObjectID.Wood,3);
        Program.Check(AbandonAtRoot(f)?.desc.Contains("다른 물건")==true,"M2: a different object in the forge input is told as the own item gone");
    }
}
