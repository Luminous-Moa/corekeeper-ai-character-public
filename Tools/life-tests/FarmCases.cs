using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PugTilemap;

public sealed class FarmFixture
{
    public World World;
    public Entity Player;
    bool pressed;
    public int ActualInputs;
    public static ContainedObjectsBuffer Item(ObjectID id,int amount,int variation=0)=>new ContainedObjectsBuffer{objectData=new ObjectDataCD{objectID=id,amount=amount,variation=variation}};
    public static FarmFixture Create(int plots,int seeds,int goalCount)
    {
        AIAgent.Cur=new AIAgent();FarmWorld.Tiles.Clear();FarmWorld.Hidden.Clear();FarmWorld.Enemies.Clear();PugDatabase.Prefabs.Clear();
        var fixture=new FarmFixture{World=new World()};var em=fixture.World.EntityManager;fixture.Player=em.New();
        em.Put(fixture.Player,new LocalTransform{Position=new float3(3,0,0)});em.Put(fixture.Player,new HealthCD{health=100});em.Put(fixture.Player,new CraftingCD{outputSlotIndex=40});
        var bag=new DynamicBuffer<ContainedObjectsBuffer>();for(int i=0;i<41;i++)bag.Add(default);bag[0]=Item(ObjectID.SeedA,seeds);bag[1]=Item(ObjectID.SeedB,seeds);bag[2]=Item(ObjectID.WoodHoe,100);bag[3]=Item(ObjectID.WaterCan,100);em.PutBuffer(fixture.Player,bag);
        var bank=em.New();em.Put(bank,new PugDatabase.DatabaseBankCD{databaseBankBlob=new object()});
        foreach(var id in new[]{ObjectID.SeedA,ObjectID.SeedB})
        {
            var prefab=em.New();PugDatabase.Prefabs[(id,0)]=prefab;em.Put(prefab,new GrowingCD());em.Put(prefab,new PlantCD{objectToDropWhenHarvested=id==ObjectID.SeedA?ObjectID.FoodA:ObjectID.FoodB,numberOfPlantsToDrop=1});em.Put(prefab,new ObjectPropertiesCD{Stages=3,Time=100});
        }
        var water=em.New();PugDatabase.Prefabs[(ObjectID.WaterCan,0)]=water;em.Put(water,new FullnessCD{maxFullness=100});
        for(int i=0;i<plots;i++){var tile=new int2(3+i,0);FarmWorld.Tiles[tile]=new TileLayer{tileType=TileType.dugUpGround};AIFarm.NotePlot(tile);}
        AIGoals.Goal=new AIGoal{kind=GoalKind.Farm,item=ObjectID.SeedA,mode="plant",where=$"rect:3,0,{3+plots-1},0",count=goalCount,source="test"};
        AILife.OnGoalChanged();return fixture;
    }
    public AIObservation Observation=>AIObservation.Take(World,Player,false);
    // Physical game boundary: consume a rising secondary-use press aimed at a free dug tile using the held seed.
    bool swung;
    public void Consume(BodyInput input)
    {
        // Attack boundary (2026-11-08 gap M6): a rising primary swing aimed along face hits the ripe crop on the faced tile.
        if(!input.primary)swung=false;
        else if(!swung)
        {
            swung=true;var em0=World.EntityManager;var me=AIBody.Pos(World,Player);var hit=new int2((int)Math.Round(me.x+input.face.x),(int)Math.Round(me.z+input.face.y));
            using(var q=em0.CreateEntityQuery(ComponentType.ReadOnly<GrowingCD>(),ComponentType.ReadOnly<LocalTransform>()))
            using(var crops=q.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach(var crop in crops)
                    if(input.aim&&AIBody.TileOf(em0.GetComponentData<LocalTransform>(crop).Position)==hit&&em0.GetComponentData<GrowingCD>(crop).HasFinishedGrowing(em0.GetComponentData<ObjectPropertiesCD>(crop))){em0.Remove(crop);ActualInputs++;break;}
        }
        if(!input.secondary){pressed=false;return;}if(pressed)return;pressed=true;
        if(!input.aim||!input.hasPoint||input.slot<0)return;
        var em=World.EntityManager;var bag=em.GetBuffer<ContainedObjectsBuffer>(Player);var held=bag[input.slot];
        if(held.objectID==ObjectID.WaterCan)
        {
            var wetTile=new int2((int)input.point.x,(int)input.point.y);
            if(held.amount>=5&&FarmWorld.Tiles.TryGetValue(wetTile,out var dug)&&dug.tileType==TileType.dugUpGround){FarmWorld.Tiles[wetTile]=new TileLayer{tileType=TileType.wateredGround};var wd=held.objectData;wd.amount-=5;held.objectData=wd;bag[input.slot]=held;ActualInputs++;}
            return;
        }
        if(held.objectID==ObjectID.StoneSeeder)
        {
            // SeederSlot.PlaceItem: the first plantable seed of the inventory, over the tool's area from the aimed tile (2x2 here)
            int seedSlot=-1;for(int i=0;i<bag.Length;i++)if((bag[i].objectID==ObjectID.SeedA||bag[i].objectID==ObjectID.SeedB)&&bag[i].amount>0){seedSlot=i;break;}
            if(seedSlot<0)return;var anchor=new int2((int)input.point.x,(int)input.point.y);
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++){var t=anchor+new int2(dx,dy);var seed=bag[seedSlot];if(seed.amount<=0)break;if(PlantAt(t,seed.objectID)){var sd=seed.objectData;sd.amount--;seed.objectData=sd;bag[seedSlot]=seed;ActualInputs++;}}
            return;
        }
        if(held.objectID==ObjectID.WoodHoe)
        {
            var aimed=new int2((int)input.point.x,(int)input.point.y);
            using(var q=em.CreateEntityQuery(ComponentType.ReadOnly<GrowingCD>(),ComponentType.ReadOnly<LocalTransform>()))
            using(var crops=q.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach(var crop in crops)
                    if(AIBody.TileOf(em.GetComponentData<LocalTransform>(crop).Position)==aimed&&em.GetComponentData<GrowingCD>(crop).HasFinishedGrowing(em.GetComponentData<ObjectPropertiesCD>(crop)))
                    {em.Remove(crop);ActualInputs++;return;} // harvested drop remains outside main inventory
            return;
        }
        if(held.objectID!=ObjectID.SeedA&&held.objectID!=ObjectID.SeedB||held.amount<=0)return;
        var tile=new int2((int)input.point.x,(int)input.point.y);
        if(!FarmWorld.Tiles.TryGetValue(tile,out var ground)||ground.tileType!=TileType.dugUpGround&&ground.tileType!=TileType.wateredGround)return;
        using(var q=em.CreateEntityQuery(ComponentType.ReadOnly<GrowingCD>(),ComponentType.ReadOnly<LocalTransform>()))
        using(var crops=q.ToEntityArray(Unity.Collections.Allocator.Temp))foreach(var crop in crops)if(AIBody.TileOf(em.GetComponentData<LocalTransform>(crop).Position)==tile)return;
        var entity=em.New();em.Put(entity,new LocalTransform{Position=new float3(tile.x,0,tile.y)});em.Put(entity,new ObjectDataCD{objectID=held.objectID==ObjectID.SeedA?ObjectID.CropA:ObjectID.CropB});em.Put(entity,new GrowingCD());em.Put(entity,new PlantCD{objectToDropWhenHarvested=held.objectID==ObjectID.SeedA?ObjectID.FoodA:ObjectID.FoodB,numberOfPlantsToDrop=1});em.Put(entity,new ObjectPropertiesCD{Stages=3,Time=100});
        var data=held.objectData;data.amount--;held.objectData=data;bag[input.slot]=held;ActualInputs++;
    }
    bool PlantAt(int2 tile,ObjectID seed)
    {
        var em=World.EntityManager;
        if(!FarmWorld.Tiles.TryGetValue(tile,out var ground)||ground.tileType!=TileType.dugUpGround&&ground.tileType!=TileType.wateredGround)return false;
        using(var q=em.CreateEntityQuery(ComponentType.ReadOnly<GrowingCD>(),ComponentType.ReadOnly<LocalTransform>()))
        using(var crops=q.ToEntityArray(Unity.Collections.Allocator.Temp))foreach(var crop in crops)if(AIBody.TileOf(em.GetComponentData<LocalTransform>(crop).Position)==tile)return false;
        var entity=em.New();em.Put(entity,new LocalTransform{Position=new float3(tile.x,0,tile.y)});em.Put(entity,new ObjectDataCD{objectID=seed==ObjectID.SeedA?ObjectID.CropA:ObjectID.CropB});em.Put(entity,new GrowingCD());em.Put(entity,new PlantCD{objectToDropWhenHarvested=seed==ObjectID.SeedA?ObjectID.FoodA:ObjectID.FoodB,numberOfPlantsToDrop=1});em.Put(entity,new ObjectPropertiesCD{Stages=3,Time=100});
        return true;
    }
    public Entity Ripe(int2 tile)
    {
        var em=World.EntityManager;var ripe=em.New();em.Put(ripe,new LocalTransform{Position=new float3(tile.x,0,tile.y)});em.Put(ripe,new ObjectDataCD{objectID=ObjectID.CropA});em.Put(ripe,new GrowingCD{currentStage=2});em.Put(ripe,new ObjectPropertiesCD{Stages=3,Time=100});em.Put(ripe,new PlantCD{objectToDropWhenHarvested=ObjectID.FoodA,numberOfPlantsToDrop=1});return ripe;
    }
    public void Drive(AISkill skill)
    {
        for(int i=0;i<300&&skill.Status==SkillStatus.Running;i++){float now=UnityEngine.Time.realtimeSinceStartup+=0.1f;Consume(skill.Tick(World,Player,now));}
        if(skill.Status==SkillStatus.Running)throw new Exception("production FarmSkill did not reach observed endpoint");
    }
}

public static class FarmCases
{
    static void Check(bool condition,string message){Program.Check(condition,"farm: "+message);if(!condition)throw new Exception(message);}
    public static void Run()
    {
        var f=FarmFixture.Create(9,20,12);var q=AILife.ForGoal(AIGoals.Goal);
        var first=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",q);Check(first!=null&&first.itemCount==6&&first.life.RequestedCount==12,"count12 offers first six without reducing total goal");
        var skill=AIFarm.Make(first);Check(skill.Key==first.key&&skill.Start(f.World,f.Player,0),"candidate and exact FarmSkill share identity and start validator");
        skill.Run=1;AILife.Starting(first,skill);f.Drive(skill);AILife.Ended(skill);
        Check(((ILifeResult)skill).ConfirmedWork==6&&f.ActualInputs==6,"six normal aimed inputs produce six confirmed tiles");
        Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==6,"remaining work after first batch is six");
        var next=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));Check(next!=null&&next.itemCount==3,"next batch exposes three remaining valid tiles");
        var nextSkill=AIFarm.Make(next);Check(nextSkill.Start(f.World,f.Player,UnityEngine.Time.realtimeSinceStartup),"second exact batch starts");nextSkill.Run=2;AILife.Starting(next,nextSkill);f.Drive(nextSkill);AILife.Ended(nextSkill);
        Check(AILife.ForGoal(AIGoals.Goal).RequestedCount==3&&f.ActualInputs==9,"nine observed changes leave unmet three of twelve");
        Check(AILife.Report().Contains("잔량 3"),"reported work keeps unmet quantity");
        f=FarmFixture.Create(4,20,6);q=AILife.ForGoal(AIGoals.Goal);var a=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",q);Check(a.itemCount==4&&a.desc.Contains("부족 2"),"request six exposes actual four and shortage two");
        var other=q.Copy();other.Item="SeedB";var b=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",other);Check(a.key!=b.key,"different seeds on identical tiles have different execution identities");
        var buffer=f.World.EntityManager.GetBuffer<ContainedObjectsBuffer>(f.Player);buffer[0]=FarmFixture.Item(ObjectID.SeedB,20);Check(AIFarm.Stale(f.World,f.Player,a)!=null&&!AIFarm.Make(a).Start(f.World,f.Player,0),"replaced seed slot rejected by stale and Start without substitution");
        f=FarmFixture.Create(4,20,6);a=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));FarmWorld.Tiles[a.tiles[0]]=new TileLayer{tileType=TileType.ground};Check(AIFarm.Stale(f.World,f.Player,a)!=null,"changed target tile rejected");
        var entries=AIFarm.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="plant",Item="SeedA",Where="tiles"});Check(entries.Exists(e=>e.Id.StartsWith("tile:",StringComparison.Ordinal)),"individual tile depth reachable");
        f=FarmFixture.Create(4,1,6);buffer=f.World.EntityManager.GetBuffer<ContainedObjectsBuffer>(f.Player);for(int i=4;i<7;i++)buffer[i]=FarmFixture.Item(ObjectID.SeedA,1);
        a=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));Check(a.itemCount==4&&a.farm.SourceSlots.Count==4,"same selected seed variation uses all four fixed source stacks");
        skill=AIFarm.Make(a);Check(skill.Start(f.World,f.Player,0),"multi-stack exact request starts");f.Drive(skill);Check(((ILifeResult)skill).ConfirmedWork==4&&f.ActualInputs==4,"fixed seed stacks consumed through normal inputs without replacing choice");
        f=FarmFixture.Create(1,20,1);var em=f.World.EntityManager;var ripe=em.New();em.Put(ripe,new LocalTransform{Position=new float3(3,0,0)});em.Put(ripe,new ObjectDataCD{objectID=ObjectID.CropA});em.Put(ripe,new GrowingCD{currentStage=2});em.Put(ripe,new ObjectPropertiesCD{Stages=3,Time=100});em.Put(ripe,new PlantCD{objectToDropWhenHarvested=ObjectID.FoodA,numberOfPlantsToDrop=1});
        AIGoals.Goal=new AIGoal{kind=GoalKind.Farm,mode="harvest",count=1,where="tile:3,0"};a=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));
        skill=AIFarm.Make(a);Check(skill.Start(f.World,f.Player,0),"exact ripe crop and selected hoe start");
        var original=AIGoals.Goal;AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.FoodA,count=10,maxCount=10};
        Check(a.itemId==ObjectID.WoodHoe&&AIGathering.HarvestCandidateLimit(f.World,f.Player,a)!=null,"actual harvest candidate rechecks crop output despite hoe metadata");
        var blocked=AIFarm.Make(a);Check(!blocked.Start(f.World,f.Player,0)&&blocked.Reason.Contains("PlantCD"),"actual harvest Start reports observed crop source and uncertain bonus-inclusive maximum");
        var blockedInput=skill.Tick(f.World,f.Player,1);f.Consume(blockedInput);
        Check(!blockedInput.secondary&&skill.Status==SkillStatus.Failed&&f.ActualInputs==0,"new ceiling before actual harvest press suppresses secondary input with no crop mutation");
        Check(AIGathering.HarvestCandidateLimit(f.World,f.Player,new AICandidate{kind="farm",actionMode="plant"})==null,"non-harvest farm actions remain outside destructive harvest guard");
        AIGoals.Goal=original;skill=AIFarm.Make(a);Check(skill.Start(f.World,f.Player,0),"ordinary harvest resumes with no explicit ceiling");
        f.Drive(skill);Check(((ILifeResult)skill).ConfirmedWork==1&&((ILifeResult)skill).ConfirmedReceived==0,"harvested tile with floor-only drop is work, not main inventory receipt");
        Gaps();
    }
    // 2026-11-08 selection gaps: water empty dug ground, harvest without a hoe, plant with a seeder.
    static void Gaps()
    {
        // M5: dug plots with nothing on them are water targets ("ground" or "all"), crops are not under "ground"
        var f=FarmFixture.Create(3,20,1);var em=f.World.EntityManager;
        AIGoals.Goal=new AIGoal{kind=GoalKind.None};AILife.OnGoalChanged();
        var items=AIFarm.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="water"});Check(items.Exists(e=>e.Id=="ground"),"water item axis offers empty dug ground");
        var w=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="water",Item="ground",Where="farm"});Check(w!=null&&w.itemCount==3,"three empty dug plots are water targets");
        var skill=AIFarm.Make(w);Check(skill.Start(f.World,f.Player,0),"pre-watering starts with the selected can");f.Drive(skill);
        Check(((ILifeResult)skill).ConfirmedWork==3&&FarmWorld.Tiles[new int2(3,0)].tileType==TileType.wateredGround,"normal can inputs wet three empty plots and count them");
        Check(AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="water",Item="ground",Where="farm"})==null,"already watered empty plots are no longer offered");
        f=FarmFixture.Create(2,20,1);f.Ripe(new int2(3,0));AIGoals.Goal=new AIGoal{kind=GoalKind.None};AILife.OnGoalChanged();
        w=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="water",Item="ground",Where="farm"});Check(w!=null&&w.itemCount==1&&w.tiles[0].Equals(new int2(4,0)),"ground filter skips a tile with a crop");
        // M6: no hoe -> hit harvest through the attack path; the work counts toward the harvest goal
        f=FarmFixture.Create(2,20,2);em=f.World.EntityManager;var bag=em.GetBuffer<ContainedObjectsBuffer>(f.Player);bag[2]=default;f.Ripe(new int2(3,0));f.Ripe(new int2(4,0));
        AIGoals.Goal=new AIGoal{kind=GoalKind.Farm,mode="harvest",count=2,where="farm"};AILife.OnGoalChanged();
        var h=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",AILife.ForGoal(AIGoals.Goal));Check(h!=null&&h.farm.Attack&&h.itemCount==2&&h.desc.Contains("괭이 없음"),"no hoe offers a hit harvest of both ripe crops");
        Check(AIFarm.ToolTargets(f.World,f.Player).TrueForAll(x=>x.id!=ObjectID.WoodHoe),"harvest goal no longer prepares a hoe it does not need");
        skill=AIFarm.Make(h);Check(skill.Start(f.World,f.Player,0),"hit harvest starts with the weapon slot");skill.Run=11;AILife.Starting(h,skill);f.Drive(skill);AILife.Ended(skill);
        Check(((ILifeResult)skill).ConfirmedWork==2&&f.ActualInputs==2&&AILife.ForGoal(AIGoals.Goal).RequestedCount==0,"two primary swings harvest both crops and close the harvest goal quantity");
        // M7: seeder second axis, the seed it will use, and an area press planting several targets
        f=FarmFixture.Create(4,20,4);em=f.World.EntityManager;bag=em.GetBuffer<ContainedObjectsBuffer>(f.Player);bag[5]=FarmFixture.Item(ObjectID.StoneSeeder,100);
        for(int i=0;i<4;i++)FarmWorld.Tiles[new int2(3+i,1)]=new TileLayer{tileType=TileType.ground};
        var seconds=AIFarm.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="farm",Mode="plant",Item="SeedA"});Check(seconds.Exists(e=>e.Id=="hand")&&seconds.Exists(e=>e.Id=="seeder:StoneSeeder"&&e.Text.Contains("SeedA")),"plant offers hand or seeder and names the seed the seeder will use");
        var q=AILife.ForGoal(AIGoals.Goal);q.Second="seeder:StoneSeeder";var sd=AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",q);Check(sd!=null&&sd.farm.Seeder&&sd.itemCount==4&&sd.life.MatchesGoal,"seeder candidate binds the plant goal and four empty plots");
        var qb=q.Copy();qb.Item="SeedB";Check(AIFarm.ExactCandidate(f.World,f.Player,f.Observation,"",qb)==null,"seeder is refused when the game would plant another seed first");
        skill=AIFarm.Make(sd);Check(skill.Start(f.World,f.Player,0),"seeder plant starts with the tool slot");skill.Run=12;AILife.Starting(sd,skill);f.Drive(skill);AILife.Ended(skill);
        Check(((ILifeResult)skill).ConfirmedWork==4&&AILife.ForGoal(AIGoals.Goal).RequestedCount==0,"area presses plant all four targets and the plant goal counts them: work "+((ILifeResult)skill).ConfirmedWork+" left "+AILife.ForGoal(AIGoals.Goal).RequestedCount+" status "+skill.Status+" "+skill.Reason+" inputs "+f.ActualInputs);
        Check(em.GetBuffer<ContainedObjectsBuffer>(f.Player)[0].amount==16&&em.GetBuffer<ContainedObjectsBuffer>(f.Player)[5].objectID==ObjectID.StoneSeeder,"the first seed stack is consumed and the seeder stays in the bag");
    }
}
