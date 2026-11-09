using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Inventory;

public sealed class Fixture
{
    public World World=new();
    public Entity Player=new(){Index=1,Version=1},Repair=new(){Index=2,Version=3},Forge=new(){Index=3,Version=5},Clock=new(){Index=4},Queues=new(){Index=5},Bank=new(){Index=6},Primary=new(){Index=7};
    public DynamicBuffer<ContainedObjectsBuffer> Bag,ForgeItems;
    public DynamicBuffer<UIActionBuffer> UI=>World.EntityManager.GetBuffer<UIActionBuffer>(Player);
    public DynamicBuffer<CraftBuffer> Crafts=>World.EntityManager.GetBuffer<CraftBuffer>(Queues);
    public DynamicBuffer<InventoryChangeBuffer> Moves=>World.EntityManager.GetBuffer<InventoryChangeBuffer>(Queues);
    public int CraftsApplied,MovesApplied;
    public string RepairId=>"station:SalvageAndRepairStation@1,0";
    public string ForgeId=>"station:UpgradeForge@2,0";
    public static ContainedObjectsBuffer Item(ObjectID id,int amount,int variation=0,int aux=0,int stamp=0)=>new(){objectData=new(){objectID=id,amount=amount,variation=variation,variationUpdateCount=stamp},auxDataIndex=aux};
    public static DynamicBuffer<T> Buffer<T>(int n=0){var result=new DynamicBuffer<T>();for(int i=0;i<n;i++)result.Add(default);return result;}
    public static EquipmentCD Gear(int head=41)=>new(){helmSlotIndex=head,breastSlotIndex=42,pantsSlotIndex=43,necklaceSlotIndex=44,ring1SlotIndex=45,ring2SlotIndex=46,offHandIndex=47,bagIndex=48,lanternIndex=49,pouch1Index=51};
    public static Fixture Create()
    {
        Boundary.Reset();AIBrain.ResetAdapter();AIAgent.Cur=new();AIVision.Visible=true;AINavigator.Blocked=false;PugDatabase.Prefabs.Clear();AIObservation.ObservedEnemies.Clear();AIMemory.Stations.Clear();AIBase.Memories.Clear();AITeammates.Seen.Clear();
        var f=new Fixture();var em=f.World.EntityManager;Boundary.QuerySystem=new(em);
        f.Bag=Buffer<ContainedObjectsBuffer>(56);f.Bag[12]=Item(ObjectID.CookingPotHelm,30,0,17,2);f.Bag[0]=Item(ObjectID.Wood,20);f.Bag[1]=Item(ObjectID.IronBar,20);f.Bag[2]=Item(ObjectID.AncientCoin,264);
        em.PutBuffer(f.Player,f.Bag);em.Put(f.Player,new LocalTransform{Position=new float3(0,0,0)});em.Put(f.Player,new HealthCD{health=100});em.Put(f.Player,new CraftingCD{outputSlotIndex=50});em.Put(f.Player,Gear());em.Put(f.Player,new UIActionsCD{lastActionTick=new(99)});em.PutBuffer(f.Player,Buffer<UIActionBuffer>());
        var main=Buffer<InventoryBuffer>();main.Add(new(){startIndex=0,size=40,sizeX=10,sizeY=4,maxSize=40,extraInventorySizeSlot=-1});var gear=Buffer<InventoryBuffer>();
        main.Add(new(){startIndex=40,size=16,sizeX=16,sizeY=1,maxSize=16,extraInventorySizeSlot=-1});em.PutBuffer(f.Player,main);
        em.Put(f.Repair,new ObjectDataCD{objectID=ObjectID.SalvageAndRepairStation});em.Put(f.Repair,new LocalTransform{Position=new float3(1,0,0)});em.Put(f.Repair,new CraftingCD{outputSlotIndex=1});em.PutBuffer(f.Repair,Buffer<ContainedObjectsBuffer>(2));var repairInventory=Buffer<InventoryBuffer>();repairInventory.Add(new(){startIndex=0,size=2,sizeX=2,sizeY=1,extraInventorySizeSlot=-1});em.PutBuffer(f.Repair,repairInventory);
        em.Put(f.Forge,new ObjectDataCD{objectID=ObjectID.UpgradeForge});em.Put(f.Forge,new LocalTransform{Position=new float3(2,0,0)});em.Put(f.Forge,new CraftingCD{outputSlotIndex=6});f.ForgeItems=Buffer<ContainedObjectsBuffer>(8);em.PutBuffer(f.Forge,f.ForgeItems);var forgeInventory=Buffer<InventoryBuffer>();forgeInventory.Add(new(){startIndex=5,size=1,sizeX=1,sizeY=1,extraInventorySizeSlot=-1});em.PutBuffer(f.Forge,forgeInventory);
        em.Put(f.Clock,new NetworkTime{ServerTick=new(100)});em.Put(f.Bank,new PugDatabase.DatabaseBankCD{databaseBankBlob=new object()});em.Put(f.Bank,new UpgradeCostsTableCD{Available=1});em.PutBuffer(f.Queues,Buffer<CraftBuffer>());em.PutBuffer(f.Queues,Buffer<InventoryChangeBuffer>());
        PugDatabase.Prefabs[(ObjectID.CookingPotHelm,0)]=f.Primary;for(int i=1;i<=20;i++)PugDatabase.Prefabs[(ObjectID.CookingPotHelm,i)]=f.Primary;
        em.Put(f.Primary,new DurabilityCD{maxDurability=90});em.Put(f.Primary,new LevelCD{level=6});em.Put(f.Primary,new ObjectCategoryTagsCD{tagsBitMask=34});var levels=Buffer<LevelEntitiesBuffer>();for(int i=0;i<=20;i++)levels.Add(new(){entity=f.Primary});em.PutBuffer(f.Primary,levels);
        AIInventory.Reset();AIMaintenance.Reset();AILife.Reset();AIGoals.Goal=new AIGoal();return f;
    }
    public AIObservation Observation=>AIObservation.Take(World,Player,false);
    public void Plan(string mode,int slot=12,string where=null,string item="CookingPotHelm")
    {
        where??=mode=="repair"?RepairId:ForgeId;
        string json="{\"answer\":{\"plan\":[{\"do\":\"maintain\",\"mode\":\""+mode+"\",\"item\":\""+item+"\",\"count\":1,\"slot\":"+slot+",\"where\":\""+where+"\"}]}}";
        var step=AIPlanProtocol.Read(json).Answer.Plan[0];AIPlanner.SetStep(World,Player,step);
    }
    public AICandidate Exact(string mode=null)
    {
        var selection=AILife.ForGoal(AIGoals.Goal);if(mode!=null)selection.Mode=mode;
        return AIMaintenance.ExactCandidate(World,Player,Observation,"",selection);
    }
    public AISkill Start(AICandidate candidate)
    {if(candidate==null)throw new InvalidOperationException("No candidate: "+AIMaintenance.Report(World,Player));AIBrain.ApplyChosen(World,Player,candidate);return AIBody.Current;}
    public void Tick(AISkill skill)
    {UnityEngine.Time.realtimeSinceStartup+=0.1f;skill.Tick(World,Player,UnityEngine.Time.realtimeSinceStartup);}
    public NetworkTick ServerTick {get=>World.EntityManager.GetComponentData<NetworkTime>(Clock).ServerTick;set=>World.EntityManager.Put(Clock,new NetworkTime{ServerTick=value});}
    public void ApplyTick(NetworkTick tick,bool updateLast=true)
    {ServerTick=tick;if(updateLast)World.EntityManager.Put(Player,new UIActionsCD{lastActionTick=tick});}
    // Game UI processing boundary. Historical UI entries stay; matching consumer buffers model in-flight work.
    public UIActionBuffer PostToConsumer()
    {
        var last=World.EntityManager.GetComponentData<UIActionsCD>(Player).lastActionTick;
        var action=UI.First(x=>x.tick.IsNewerThan(last));ApplyTick(action.tick);
        if(action.actionData.action==UIInputAction.Craft)Crafts.Add(new(){playerEntity=Player,craftActionData=action.actionData.craftActionData});else Moves.Add(new(){playerEntity=Player,inventoryChangeData=action.actionData.inventoryChangeData});
        return action;
    }
    public void Consume(UIActionBuffer action,bool changeTarget=true,bool changeMaterials=true)
    {
        if(action.actionData.action==UIInputAction.InventoryChange)
        {
            var m=action.actionData.inventoryChangeData;var from=World.EntityManager.GetBuffer<ContainedObjectsBuffer>(m.inventory1);var to=World.EntityManager.GetBuffer<ContainedObjectsBuffer>(m.entityOrInventory2);
            if(changeTarget)
            {
                // InventoryUtility.TryMove preserves the destination's variationUpdateCount; it copies only these data fields.
                var source=from[m.index1];var destination=to[m.index2];var sourceData=source.objectData;var destinationData=destination.objectData;
                int moved=AIInventory.Info(source)?.isStackable==true?Math.Min(m.amount,source.amount):source.amount;
                sourceData.amount-=moved;destinationData.objectID=source.objectID;destinationData.variation=source.variation;destinationData.amount+=moved;destination.auxDataIndex=source.auxDataIndex;destination.objectData=destinationData;source.objectData=sourceData;
                from[m.index1]=sourceData.amount==0?default:source;to[m.index2]=destination;
            }
            Moves.Clear();MovesApplied++;
        }
        else
        {
            var c=action.actionData.craftActionData;var target=World.EntityManager.GetBuffer<ContainedObjectsBuffer>(c.targetInventoryEntity);var before=target[c.int0];
            if(changeTarget){var data=before.objectData;if(c.craftAction==CraftAction.RepairOrReinforce)data.amount=90;else data.variation=(data.variation>0?data.variation:6)+1;data.variationUpdateCount++;before.objectData=data;target[c.int0]=before;}
            if(changeMaterials)foreach(var cost in c.craftAction==CraftAction.RepairOrReinforce?Boundary.RepairCosts:Boundary.UpgradeCosts){for(int i=0;i<40;i++)if(Bag[i].objectID==cost.id){var x=Bag[i];var d=x.objectData;d.amount-=cost.need;x.objectData=d;Bag[i]=x;break;}}
            Crafts.Clear();CraftsApplied++;
        }
        var next=action.tick;next.Add(1);ApplyTick(next);
    }
    public void ConsumeNext(bool changeTarget=true,bool changeMaterials=true)=>Consume(PostToConsumer(),changeTarget,changeMaterials);
    public void End(AISkill skill)=>AIBrain.OnSkillEnded(skill,UnityEngine.Time.realtimeSinceStartup);
}
