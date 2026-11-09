using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Text.Json;

static class Program
{
    static int checks, failures, decisions, navigation;
    static World world; static Entity player; static DynamicBuffer<ContainedObjectsBuffer> bag;
    static AIGoal originalGoal; static readonly List<object> trace = new();
    static void Check(bool ok,string what) { checks++; if(!ok)failures++; Console.WriteLine((ok?"PASS ":"FAIL ")+what); }
    static ContainedObjectsBuffer Item(ObjectID id,int state=100,int variation=0)=>new(){objectData=new(){objectID=id,amount=state,variation=variation}};
    static void Setup(AIGoal goal)
    {
        AIAgent.Cur=new(); world=new(); player=new(){Index=1};bag=new();for(int i=0;i<90;i++)bag.Add(default);
        AIObservation.CurrentEnemies.Clear();
        world.EntityManager.PutBuffer(player,bag);var inv=new DynamicBuffer<InventoryBuffer>();inv.Add(new(){startIndex=0,size=40,sizeX=10,sizeY=4,maxSize=40,extraInventorySizeSlot=-1});inv.Add(new(){startIndex=40,size=50,sizeX=1,sizeY=50,maxSize=50,extraInventorySizeSlot=-1});
        world.EntityManager.PutBuffer(player,inv);world.EntityManager.PutBuffer(player,new DynamicBuffer<UIActionBuffer>());world.EntityManager.Put(player,new LocalTransform());
        world.EntityManager.Put(player,new EquipmentCD{helmSlotIndex=40,breastSlotIndex=41,pantsSlotIndex=42,necklaceSlotIndex=43,ring1SlotIndex=44,ring2SlotIndex=45,offHandIndex=46,bagIndex=47,lanternIndex=48,pouch1Index=49});
        AIGoals.Goal=originalGoal=goal;AIInventory.Reset();AIInventory.GoalSet(world,player,goal);AIBase.Memories.Clear();AITeammates.Seen.Clear();AICraft.Requests.Clear();
        AIBody.Current=new ExistingAction();AIBody.Starts=AIBody.Stops=0;AIGoals.Finishes=AIPlanner.Replans=0;AIPlanner.Orders=new();AIPlanner.Orders.Add(new(){Id=9,Text="정확한 장비를 갖추고 원래 보급 부탁을 이어가",State="따름",Source="llm:offline#9"},null);
    }
    static AICandidate Decide(string action)
    {
        var before=AIBody.Current;
        var normal=new List<AICandidate>{new(){kind="continue",key=before?.Key??"",desc="현재 몸 동작 계속",run=before?.Run??0},new(){kind="stop_action",key="control-stop",desc="몸 동작만 중단"},new(){kind="finish_goal",key="control-finish",desc="현재 단계 완료"},new(){kind="request_plan",key="control-plan",desc="상위 재계획"}};
        var cs=AIBrain.Choices(world,player,new(){pos=AIBody.Pos(world,player)},normal);
        string input=$"원래 부탁: {AIPlanner.Orders.Latest.Text}. 현재 목표: {originalGoal.kind} {originalGoal.item}. 현재 몸: {before?.Key}. "+AIInventoryMenu.PromptLine();
        string json=AIBrain.Request(input,cs);
        using var parsed=JsonDocument.Parse(json);
        var c=cs.Find(x=>x.inventoryMenu?.action==action)??throw new Exception("No "+action+" at "+AIInventoryMenu.PromptLine());
        string value=c.value;var selected=cs.Find(x=>x.value==value);Check(ReferenceEquals(c,selected),"the selected cN resolves to the same actual menu candidate "+action);
        Check(parsed.RootElement.GetProperty("questions")[0].GetProperty("choices").EnumerateArray().Any(x=>x.GetProperty("value").GetString()==value),"actual model JSON contains the selected node "+action);
        Check(AIActions.Stale(world,player,selected)==null,"actual action-entry stale check accepts current menu token "+action);
        int requests=AICraft.Requests.Count,starts=AIBody.Starts;
        AIBrain.Select(world,player,decisions+1,selected);decisions++;
        if(c.kind=="inventory_menu")
        {
            navigation++;
            Check(ReferenceEquals(before,AIBody.Current)&&AIBody.Starts==starts&&AIBody.Stops==0&&AICraft.Requests.Count==requests,"actual Loop.Apply navigation changes no body/queue "+action);
        }
        Check(ReferenceEquals(originalGoal,AIGoals.Goal)&&AIPlanner.Orders.Latest.Open&&AIGoals.Finishes==0&&AIPlanner.Replans==0,"original goal and actual host-order record stay open "+action);
        trace.Add(new{decision=decisions,action,value,input,requestJson=json,navigation=c.kind=="inventory_menu",queuedInventoryRequests=AICraft.Requests.Count,body=AIBody.Current?.Key,goalUnchanged=ReferenceEquals(originalGoal,AIGoals.Goal),orderState=AIPlanner.Orders.Latest.State});
        return c;
    }
    static int Main()
    {
        Setup(new(){kind=GoalKind.Hold,item=ObjectID.CopperSword,slot=13,where="hotbar:5",count=1});bag[12]=Item(ObjectID.CopperSword,100,1);bag[13]=Item(ObjectID.CopperSword,300,2);bag[5]=Item(ObjectID.Wood,20);
        var existing=AIBody.Current;
        Decide("enter");Decide("category:무기");Decide("item:12");Decide("back");Decide("item:13");Decide("operation:hold");Decide("destination:1:5");
        Check(AICraft.Requests.Count==0&&AIBody.Current==existing,"entire menu/back/other-item/argument traversal has no queued inventory effect");
        var leaf=Decide("execute");
        Check(AIBody.Current is InventorySkill&&AIBody.Starts==1&&AICraft.Requests.Count==0,"actual registry and Apply start one InventorySkill only at the final leaf");
        var physical=AIBody.Current;physical.Tick(world,player,20);
        Check(AICraft.Requests.Count==1&&AICraft.Requests[0].mode=="swap"&&AICraft.Requests[0].from==13&&AICraft.Requests[0].to==5,"actual InventorySkill emits exactly the selected normal Swap request");
        physical.Tick(world,player,21);Check(AICraft.Requests.Count==1&&physical.Status==SkillStatus.Running,"unchanged fake consumer state neither repeats the request nor claims success");
        (bag[13],bag[5])=(bag[5],bag[13]);world.EntityManager.Put(player,new EquippedObjectCD{equippedSlotIndex=5,containedObject=bag[5]});physical.Tick(world,player,22);
        Check(physical.Status==SkillStatus.Done&&bag[13].objectID==ObjectID.Wood&&bag[5].variation==2&&AIInventory.GoalDone(world,player,out _),"same connected flow observes both exact swapped objects and actual held slot before receipt");
        Check(ReferenceEquals(originalGoal,AIGoals.Goal)&&AIPlanner.Orders.Latest.Open&&AIGoals.Finishes==0,"observed inventory leaf leaves the real original host order and goal for later explicit completion");
        trace.Add(new{eventName="manual consumer buffer updates",requestCount=AICraft.Requests.Count,fromSlot=13,fromObject=bag[13].objectID.ToString(),toSlot=5,toObject=bag[5].objectID.ToString(),variation=bag[5].variation,skill=physical.Status.ToString(),goalUnchanged=ReferenceEquals(originalGoal,AIGoals.Goal),order=AIPlanner.Orders.Latest.State});

        Setup(new(){kind=GoalKind.Equip,item=ObjectID.CopperHelm,count=1});bag[12]=Item(ObjectID.CopperSword);
        existing=AIBody.Current;Decide("enter");Decide("category:무기");Decide("item:12");Decide("cancel");
        Check(!AIInventoryMenu.Active&&AIBody.Current==existing&&AICraft.Requests.Count==0&&AIBody.Stops==0,"real menu cancel through actual Apply retains body and original request with no effect");

        Setup(new(){kind=GoalKind.Store,item=ObjectID.Wood,count=5});bag[0]=Item(ObjectID.Wood,10);var chest=new Entity{Index=2};var box=new DynamicBuffer<ContainedObjectsBuffer>();var ci=new DynamicBuffer<InventoryBuffer>();ci.Add(new(){size=8,sizeX=8,sizeY=1,maxSize=8,extraInventorySizeSlot=-1});for(int i=0;i<8;i++)box.Add(default);world.EntityManager.PutBuffer(chest,box);world.EntityManager.PutBuffer(chest,ci);world.EntityManager.Put(chest,new LocalTransform{Position=new(1,0,0)});world.EntityManager.Put(chest,new ObjectDataCD{objectID=ObjectID.Chest});world.EntityManager.Put(chest,new InventoryAutoTransferEnabledCD());AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new(1,0),slots=8,free=8});
        Decide("enter");Decide("category:재료·기타");Decide("item:0");Decide("operation:store");Decide("chest:1:0");Decide("execute");
        var inspection=AIBody.Current;Check(inspection is InventorySkill&&inspection.Status==SkillStatus.Running,"explicit inspect leaf starts a real still-pending inventory skill");Decide("cancel");
        Check(!AIInventoryMenu.Active&&AIBody.Current==inspection&&inspection.Status==SkillStatus.Running&&AIBody.Stops==0&&AICraft.Requests.Count==0,"await-inspection cancellation does not stop the previously selected inspect skill or alter items");
        inspection.Tick(world,player,50);
        originalGoal.slot=0;originalGoal.where="chest:1,0";
        var direct=new List<AICandidate>();AIGoalFeatures.AddCandidates(world,player,new(),"",direct);
        var exact=direct.Single(c=>c.inventory?.mode=="store"&&c.life!=null);
        Check(exact.inventory.count==5&&ReferenceEquals(exact.life.Goal,originalGoal),"actual goal-feature entry preserves exact life storage and requested work quantity");
        var composed=AILife.Compose(world,player,new(),"",false,direct);
        var publicRoot=AIBrain.Choices(world,player,new(),composed);
        Check(publicRoot.Contains(exact)&&publicRoot.Any(c=>c.inventoryMenu?.action=="enter"),"actual life exact storage and inventory browsing coexist at the ordinary root");
        AIBrain.Select(world,player,51,exact);var directSkill=AIBody.Current;
        directSkill.Tick(world,player,52);
        Check(directSkill is InventorySkill&&AICraft.Requests.Count==1&&AICraft.Requests[0].count==5,"life direct leaf starts the same normal MoveAmount inventory primitive");
        bag[0]=Item(ObjectID.Wood,5);box[0]=Item(ObjectID.Wood,5);directSkill.Tick(world,player,53);AILife.Ended(directSkill);
        Check(directSkill.Status==SkillStatus.Done&&AIInventory.MovedForGoal(originalGoal)==5&&AIInventory.GoalDone(world,player,out _),"life direct storage observes actual source/destination deltas and shared goal receipt");
        Check(ReferenceEquals(originalGoal,AIGoals.Goal)&&AIPlanner.Orders.Latest.Open,"direct life inventory receipt leaves the original order open");
        CrossDomain();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"..","..","..","connected-trace.json"),JsonSerializer.Serialize(trace,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"CONNECTED {checks-failures}/{checks}; {decisions} constructed Decisions requests, {navigation} navigation decisions; external model/game consumer calls=0");
        return failures==0?0:1;
    }
    static void CrossDomain()
    {
        Setup(new(){kind=GoalKind.Hold,item=ObjectID.CopperSword,slot=12,where="hotbar:5",count=1});bag[12]=Item(ObjectID.CopperSword);
        var validBody=AIBody.Current;
        Decide("enter");Decide("category:무기");Decide("item:12");Decide("operation:hold");Decide("destination:1:5");
        var pendingLeaf=AIInventoryMenu.Candidates(world,player,new(),"").Single(c=>c.inventoryMenu?.IsLeaf==true);
        Decide("cancel");
        var lifeOpen=AILife.Compose(world,player,new(),"",false,new()).Single(c=>c.lifeNav?.Op=="open");
        AIBrain.Select(world,player,90,lifeOpen);
        Check(AIInventory.Stale(world,player,pendingLeaf.inventory)==null&&AIActions.Stale(world,player,pendingLeaf)!=null,"cross-view late inventory leaf keeps valid item snapshot but loses menu authority");
        AIBrain.Select(world,player,91,pendingLeaf);
        Check(ReferenceEquals(validBody,AIBody.Current)&&AICraft.Requests.Count==0&&AIBody.Starts==0,"actual Apply rejects a pending inventory leaf after explicit life entry");
        Setup(new(){kind=GoalKind.Farm,count=6,mode="plant"});bag[12]=Item(ObjectID.CopperSword);
        var body=AIBody.Current;
        var root=AILife.Compose(world,player,new(),"",false,new());
        var oldLifeOpen=root.Single(c=>c.lifeNav?.Op=="open");
        var oldInventoryOpen=AIInventoryMenu.Candidates(world,player,new(),"").Single(c=>c.inventoryMenu?.action=="enter");
        AIBrain.Select(world,player,100,oldLifeOpen);
        Check(AILife.Cursor.Open&&!AIInventoryMenu.Active&&ReferenceEquals(body,AIBody.Current),"actual life opening owns the view without changing body");
        Check(AIActions.Stale(world,player,oldInventoryOpen)!=null,"pre-life inventory opening token is revoked");
        var lifeChildren=AILife.Compose(world,player,new(),"",false,new());
        Check(!AIBrain.Choices(world,player,new(),lifeChildren).Any(c=>c.inventoryMenu!=null),"life current children do not acquire an inventory root beside them");
        var close=lifeChildren.Single(c=>c.lifeNav?.Op=="close");
        AIBrain.Select(world,player,101,close);
        Check(!AILife.Cursor.Open&&AIActions.Stale(world,player,oldInventoryOpen)!=null,"closing life never revives a superseded inventory root reply");
        Check(AIActions.Stale(world,player,oldLifeOpen)!=null,"life close never revives its old open reply");
        var beforeInventory=AILife.Compose(world,player,new(),"",false,new()).Single(c=>c.lifeNav?.Op=="open");
        Decide("enter");
        Check(AIActions.Stale(world,player,beforeInventory)!=null,"inventory entry revokes previous life navigation authority");
        var bagChildren=AIInventoryMenu.Candidates(world,player,new(),"");
        Check(ReferenceEquals(bagChildren,AILife.Compose(world,player,new(),"",false,bagChildren)),"life composer leaves active inventory children intact");
        Decide("cancel");
        Check(AIActions.Stale(world,player,beforeInventory)!=null&&ReferenceEquals(body,AIBody.Current),"inventory cancel keeps expired life replies expired and body valid");
        var lifeStore=new AICandidate{kind="inventory",key="life-bound",life=new(){Branch="inventory"}};
        var legacy=new AICandidate{kind="inventory",key="legacy"};
        var surface=AIBrain.Choices(world,player,new(),new(){lifeStore,legacy});
        Check(surface.Contains(lifeStore)&&!surface.Contains(legacy),"public root preserves approved life-bound storage and hides legacy flat inventory");
        var selected=AILife.ForGoal(originalGoal);
        var run=new WorkResult();AILife.Starting(new(){life=selected},run);AILife.Ended(run);
        Check(AILife.Remaining(selected)==4,"actual life progress records two of six requested work units");
        Decide("enter");Decide("cancel");
        Check(AILife.Remaining(selected)==4,"inventory view changes retain actual life progress and remaining work quantity");
        var run2=new WorkResult{Run=78};AILife.Starting(new(){life=selected},run2);
        Decide("enter");Decide("cancel");AILife.Ended(run2);
        Check(AILife.Remaining(selected)==2,"inventory view changes retain in-flight life run bindings");
        var reopen=AILife.Compose(world,player,new(),"",false,new()).Single(c=>c.lifeNav?.Op=="open");
        AIBrain.Select(world,player,120,reopen);
        AIObservation.CurrentEnemies.Add(new(){entity=new(){Index=99},dist=1});
        var combat=new AIObservation{enemies=AIObservation.CurrentEnemies};
        var stillVisible=AILife.Compose(world,player,combat,"",false,new());
        Check(!AIBrain.Choices(world,player,combat,stillVisible).Any(c=>c.inventoryMenu!=null),"a nearby enemy does not replace an actually rendered life view when combat eligibility is false");
        var combatActual=new List<AICandidate>{new(){kind="attack",key="enemy99"}};
        var combatLife=AILife.Compose(world,player,combat,"",true,combatActual);
        var combatSurface=AIBrain.Choices(world,player,combat,combatLife);
        var combatEntry=combatSurface.Single(c=>c.inventoryMenu?.action=="enter");
        Check(AILife.Cursor.Open&&!combatSurface.Any(c=>c.lifeNav!=null),"combat suspends the life view while preserving its cursor until a new view is chosen");
        Check(AIActions.Stale(world,player,combatEntry)==null,"combat inventory entry passes the actual shared stale guard");
        AIBrain.Select(world,player,121,combatEntry);
        Check(AIInventoryMenu.Active&&!AILife.Cursor.Open&&ReferenceEquals(body,AIBody.Current)&&AICraft.Requests.Count==0,"visible combat inventory entry applies and relinquishes only the life view");
        Check(AILife.Remaining(selected)==2&&ReferenceEquals(originalGoal,AIGoals.Goal),"combat view switching preserves original goal and actual remaining life quota");
        Decide("cancel");
        Check(!AIInventoryMenu.Active&&!AILife.Cursor.Open&&ReferenceEquals(body,AIBody.Current)&&AILife.Remaining(selected)==2&&AICraft.Requests.Count==0,"combat inventory cancellation preserves body, goal, work progress and zero execution effects");
        trace.Add(new{eventName="cross-domain ownership",expiredRepliesRevived=false,lifeRemaining=AILife.Remaining(selected),bodyUnchanged=ReferenceEquals(body,AIBody.Current),externalModelCalls=0});
    }
    sealed class WorkResult: AISkill,ILifeResult
    {
        public WorkResult(){Run=77;}public int ConfirmedWork=>2;public int ConfirmedReceived=>0;public int RequestedWork=>2;public string ResultStage=>"controlled result";
        public override bool Start(World w,Entity p,float n)=>true;public override BodyInput Tick(World w,Entity p,float n)=>default;public override string Progress(World w,Entity p)=>"";
    }
}
