using Unity.Entities;
using Unity.Mathematics;

// Both menu owners, maintenance, transfers and the public filtering/apply entry points are production code.
// The fixture supplies only observed enemies/stations, ECS data and normal request consumption.
public static class IntegrationCases
{
    static List<AICandidate> Life(Fixture f,bool combat=false,params AICandidate[] actual)
        => AIBrain.AssembleLifePage(f.World,f.Player,f.Observation,AIBody.Current?.Key??"",combat,new(actual));
    static List<AICandidate> Surface(Fixture f,List<AICandidate> actual)
        => AIBrain.Surface(f.World,f.Player,f.Observation,actual);
    static List<AICandidate> Bag(Fixture f)
        => AIInventoryMenu.Candidates(f.World,f.Player,f.Observation,AIBody.Current?.Key??"");
    static void Choose(Fixture f,AICandidate candidate)
        => AIBrain.ApplyChosen(f.World,f.Player,candidate);
    static void BagNav(Fixture f,string action)
        => Choose(f,Bag(f).Single(x=>x.kind=="inventory_menu"&&x.inventoryMenu.action==action));
    static void LifeNav(Fixture f,string op,string value=null)
        => Choose(f,Life(f).Single(x=>x.lifeNav?.Op==op&&(value==null||x.lifeNav.Value==value)));
    static void Danger(bool enabled)
    {
        AIObservation.ObservedEnemies.Clear();
        if(enabled)AIObservation.ObservedEnemies.Add(new(){entity=new Entity{Index=90,Version=1},dist=1});
    }
    static AICandidate Recover(Fixture f)
        => AIMaintenance.ExactCandidate(f.World,f.Player,f.Observation,"",new(){Branch="maintenance",Mode="recover",Slot=12,Where=f.ForgeId});
    public static void RenderedOwner()
    {
        var f=Fixture.Create();f.Plan("repair");var request=f.Exact();var skill=f.Start(request);f.Tick(skill);
        AIBrain.SeedPending(skill);var held=AIBrain.HeldChoice;var queued=AIBrain.QueuedChoice;var goal=AIGoals.Goal;
        var oldBag=Bag(f).Single(x=>x.kind=="inventory_menu");LifeNav(f,"open");
        var oldLife=Life(f).Single(x=>x.lifeNav?.Op=="branch"&&x.lifeNav.Value=="maintenance");
        Program.Check(AILife.BlocksInventoryView(f.World,f.Player)&&AIActions.Stale(f.World,f.Player,oldBag)!=null,
            "integration: rendered maintenance life view blocks a previously offered bag entry");
        Program.Check(ReferenceEquals(skill,AIBody.Current)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice)&&ReferenceEquals(goal,AIGoals.Goal)&&f.UI.Length==1,
            "integration: opening life navigation preserves running repair, held/queued, goal and posted request");
        Danger(true);var combat=Life(f,true,request,new(){kind="attack",key="fight",entity=AIObservation.ObservedEnemies[0].entity},new(){kind="continue",key=skill.Key});
        Program.Check(AILife.Cursor.Open&&AILife.BlocksInventoryView(f.World,f.Player)&&combat.Any(x=>x.lifeNav!=null)&&combat.Any(x=>x.kind=="attack")&&AIActions.Stale(f.World,f.Player,oldLife)==null,
            "integration: combat retains rendered life ownership and executable read-only navigation beside attack");
        LifeNav(f,"close");
        var options=Surface(f,Life(f,true,combat.Where(x=>x.lifeNav==null).ToArray()));var enter=options.Single(x=>x.kind=="inventory_menu");
        Program.Check(options.Any(x=>x.kind=="attack")&&AIActions.Stale(f.World,f.Player,enter)==null,
            "integration: explicit closing life in combat preserves attack and exposes executable bag entry");
        Choose(f,enter);
        Program.Check(AIInventoryMenu.Active&&!AILife.Cursor.Open&&AILife.NavStale(oldLife.lifeNav)!=null&&ReferenceEquals(skill,AIBody.Current)&&ReferenceEquals(goal,AIGoals.Goal)&&AIMaintenance.Report(f.World,f.Player).Contains("게시"),
            "integration: actual bag Apply transfers view ownership and revokes old life navigation while repair/pending/goal remain");
        Program.Check(AIBrain.HeldChoice==null&&AIBrain.QueuedChoice==null,
            "integration: bag navigation revokes old waiting choice authority without stopping the running body");
        var menuToken=Bag(f).First(x=>x.kind=="inventory_menu"&&x.inventoryMenu.action.StartsWith("category:"));
        BagNav(f,"cancel");Danger(false);LifeNav(f,"open");
        Program.Check(!AIInventoryMenu.Active&&AILife.BlocksInventoryView(f.World,f.Player)&&AIActions.Stale(f.World,f.Player,menuToken)!=null,
            "integration: cancel then reopening life restores one rendered owner and rejects the previous bag token");
        f.ConsumeNext();f.Tick(skill);f.End(skill);
        Program.Check(((ILifeResult)skill).ConfirmedWork==1&&AILife.RequestSatisfied(AILife.ForGoal(goal))&&f.UI.Length==1,
            "integration: menu/combat transitions do not lose the original repair receipt or duplicate its request");
    }
    public static void DirectAndFallback()
    {
        var f=Fixture.Create();f.Plan("upgrade");var direct=f.Exact();
        var initial=Surface(f,Life(f,false,direct));
        Program.Check(initial.Contains(direct)&&initial.Any(x=>x.kind=="inventory_menu"),
            "integration: specified maintenance stays directly executable beside closed-menu root choices");
        Choose(f,initial.Single(x=>x.kind=="inventory_menu"));
        var active=Surface(f,Life(f,false,direct));
        Program.Check(AIInventoryMenu.Active&&!AILife.Cursor.Open&&!active.Any(x=>x.kind=="maintain"||x.lifeNav!=null)&&active.Any(x=>x.kind=="inventory_menu"),
            "integration: active bag publishes its own children and suppresses maintenance direct/life navigation");
        BagNav(f,"cancel");
        Program.Check(Surface(f,Life(f,false,direct)).Contains(direct),
            "integration: canceling the bag restores the exact maintenance direct candidate");
        AIMemory.Stations.Add(new(){id=ObjectID.UpgradeForge,tile=new int2(2,0)});AIVision.Visible=false;
        var walk=f.Exact();
        Program.Check(walk?.kind=="walk"&&walk.life?.Branch=="maintenance"&&walk.life.MatchesGoal&&walk.maintenance==null,
            "integration: remembered station fallback retains maintenance life binding and does not post a maintenance action");
        Choose(f,Surface(f,Life(f,false,walk)).Single(x=>x.kind=="inventory_menu"));
        Program.Check(!Surface(f,Life(f,false,walk)).Contains(walk)&&f.UI.Length==0,
            "integration: active bag suppresses the remembered-maintenance fallback without side effects");
        BagNav(f,"cancel");Danger(true);
        Program.Check(Life(f,true,walk).Contains(walk)&&AIActions.Stale(f.World,f.Player,walk)==null,
            "integration: remembered-maintenance fallback remains selectable through actual Stale in combat");
        f=Fixture.Create();f.Plan("repair");Danger(true);var fightingRepair=f.Exact();Program.Check(AIActions.Stale(f.World,f.Player,fightingRepair)==null,"integration: fresh repair remains valid through actual Stale in combat");var repairing=f.Start(fightingRepair);f.Tick(repairing);f.ConsumeNext();f.Tick(repairing);f.End(repairing);Program.Check(f.UI.Length==1&&((ILifeResult)repairing).ConfirmedWork==1,"integration: combat choice dispatch posts one normal repair request and confirms real receipt");Danger(false);
    }
    public static void InventoryBarriers()
    {
        var f=Fixture.Create();f.Plan("repair");f.Bag[10]=Fixture.Item(ObjectID.MiningPick,30);var skill=f.Start(f.Exact());f.Tick(skill);
        BagNav(f,"enter");BagNav(f,"category:착용 장비");BagNav(f,"item:12");
        Program.Check(!Bag(f).Any(x=>x.inventoryMenu?.action=="operation:equip"),
            "integration: actual bag operations hide an equip that would move the pending repair source");
        Program.Check(AIInventory.MenuMoves(f.World,f.Player,f.Player,12,"equip",Entity.Null,Entity.Null,"",1).Count==0&&AIInventory.MenuMoves(f.World,f.Player,f.Player,10,"hold",Entity.Null,Entity.Null,"",1).Any(),
            "integration: production MenuMoves applies the repair endpoint barrier while unrelated slots remain available");
        f.ConsumeNext();
        BagNav(f,"cancel");BagNav(f,"enter");BagNav(f,"category:착용 장비");BagNav(f,"item:12");
        Program.Check(Bag(f).Any(x=>x.inventoryMenu?.action=="operation:equip"),
            "integration: consumed repair releases the exact slot for the actual bag operation list");
        f=Fixture.Create();f.Plan("upgrade");f.Bag[10]=Fixture.Item(ObjectID.MiningPick,30);skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();f.Tick(skill);
        int destination=f.UI.Last().actionData.inventoryChangeData.index2;
        var moves=AIInventory.MenuMoves(f.World,f.Player,f.Player,10,"hold",Entity.Null,Entity.Null,"",1);
        Program.Check(!moves.Any(x=>x.to==destination)&&moves.Any(x=>x.to!=destination),
            "integration: posted maintenance take protects its actual main destination from bag moves without reserving other destinations");
        f.ConsumeNext();
        Program.Check(AIInventory.MenuMoves(f.World,f.Player,f.Player,10,"hold",Entity.Null,Entity.Null,"",1).Any(x=>x.to==destination),
            "integration: consumed take releases the main destination through the production bag Stale path");
    }
    public static void RecoveryAndLifecycle()
    {
        var f=Fixture.Create();f.Plan("upgrade");var goal=AIGoals.Goal;var skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();
        Choose(f,new(){kind="stop_action"});LifeNav(f,"open");Danger(true);Life(f,true);LifeNav(f,"close");
        BagNav(f,"enter");BagNav(f,"cancel");Danger(false);
        var recover=Recover(f);
        Program.Check(recover?.maintenance.Mode=="recover"&&recover.life.Mode=="upgrade"&&ReferenceEquals(goal,recover.life.Goal)&&f.UI.Length==2,
            "integration: stopped upgraded pending survives combat and actual menu ownership changes with its original upgrade binding");
        var recovery=f.Start(recover);f.Tick(recovery);f.ConsumeNext();f.Tick(recovery);f.End(recovery);f.End(recovery);
        Program.Check(((ILifeResult)recovery).ConfirmedWork==1&&((ILifeResult)recovery).ConfirmedReceived==1&&AILife.RequestSatisfied(AILife.ForGoal(goal))&&f.CraftsApplied==1,
            "integration: post-menu exact recovery completes the original upgrade once without another upgrade request");
        f=Fixture.Create();f.Plan("upgrade");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.ConsumeNext();Choose(f,new(){kind="stop_action"});
        LifeNav(f,"open");Danger(true);Life(f,true);LifeNav(f,"close");BagNav(f,"enter");f.Plan("upgrade");Danger(false);recover=Recover(f);
        Surface(f,Life(f,false,recover)); // The actual public build refreshes the menu's goal/world/player binding.
        Program.Check(!AIInventoryMenu.Active&&!AILife.Cursor.Open&&recover!=null&&!recover.life.MatchesGoal&&AILife.ForGoal(AIGoals.Goal).RequestedCount==1,
            "integration: goal change closes both old views but retains pending recovery outside the new upgrade quota");
        // Reopen the production core cursor without another Compose, so a redraw cannot mask stale suspension.
        var cursor=AILife.Cursor;cursor.Apply(cursor.Token("open"),AIAgent.Cur.Id,AIGoals.GoalRevision);
        Program.Check(AILife.BlocksInventoryView(f.World,f.Player),
            "integration: goal lifecycle reset clears suspended ownership before the next rendered Compose");
        Life(f,true);AILife.LeaveNavigation();cursor=AILife.Cursor;cursor.Apply(cursor.Token("open"),AIAgent.Cur.Id,AIGoals.GoalRevision);
        Program.Check(AILife.BlocksInventoryView(f.World,f.Player)&&Recover(f)!=null,
            "integration: LeaveNavigation clears suspension without clearing maintenance pending evidence");
        AIMaintenance.Reset();AILife.Reset();AIInventory.Reset();
        Program.Check(Recover(f)==null&&!AIInventoryMenu.Active&&!AILife.Cursor.Open&&f.ForgeItems[5].variation==7,
            "integration: character/world reset drops both views and temporary pending rather than adopting forge contents");
    }
}

