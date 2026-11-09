using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Inventory;

namespace Unity.NetCode
{
    public struct NetworkTick : IEquatable<NetworkTick>
    {
        public uint Value;
        public NetworkTick(uint value){Value=value;}
        public bool IsValid=>Value!=0;
        public static NetworkTick Invalid=>default;
        public uint TickIndexForValidTick=>Value;
        public uint SerializedData=>Value;
        public void Add(uint n){Value+=n;}
        public void Increment(){Value++;}
        public bool IsNewerThan(NetworkTick tick)=>IsValid&&(!tick.IsValid||unchecked((int)(Value-tick.Value))>0);
        public int TicksSince(NetworkTick tick)=>unchecked((int)(Value-tick.Value));
        public bool Equals(NetworkTick other)=>Value==other.Value;
        public override bool Equals(object other)=>other is NetworkTick tick&&Equals(tick);
        public override int GetHashCode()=>(int)Value;
        public static bool operator==(NetworkTick a,NetworkTick b)=>a.Equals(b);
        public static bool operator!=(NetworkTick a,NetworkTick b)=>!a.Equals(b);
        public override string ToString()=>Value.ToString();
    }
    public struct NetworkTime {public NetworkTick ServerTick;}
}
namespace Unity.Collections
{
    public struct NativeList<T>:IDisposable
    {
        private List<T> items;
        public NativeList(Allocator allocator){items=new();Boundary.NativeCreated++;}
        public NativeList(int capacity,Allocator allocator){items=new(capacity);Boundary.NativeCreated++;}
        public bool IsCreated=>items!=null;
        public int Length=>items?.Count??0;
        public T this[int i]{get=>items[i];set=>items[i]=value;}
        public void Add(T item)=>items.Add(item);
        public void Dispose(){if(items!=null){items=null;Boundary.NativeDisposed++;}}
    }
}
namespace Unity.Entities
{
    public readonly struct BufferLookup<T>
    {
        public readonly EntityManager Manager;
        public BufferLookup(EntityManager manager){Manager=manager;}
        public bool HasBuffer(Entity entity)=>Manager.HasBuffer<T>(entity);
        public bool HasComponent(Entity entity)=>HasBuffer(entity);
        public DynamicBuffer<T> this[Entity entity]=>Manager.GetBuffer<T>(entity,true);
        public bool TryGetBuffer(Entity entity,out DynamicBuffer<T> buffer){buffer=null;if(!HasBuffer(entity))return false;buffer=this[entity];return true;}
    }
    public readonly struct ComponentLookup<T>
    {
        public readonly EntityManager Manager;
        public ComponentLookup(EntityManager manager){Manager=manager;}
        public bool HasComponent(Entity entity)=>Manager.HasComponent<T>(entity);
        public T this[Entity entity]=>Manager.GetComponentData<T>(entity);
        public bool TryGetComponent(Entity entity,out T component){component=default;if(!HasComponent(entity))return false;component=this[entity];return true;}
    }
}
public sealed class PugQuerySystem
{
    public readonly EntityManager Manager;
    public PugQuerySystem(EntityManager em){Manager=em;}
    public BufferLookup<T> GetBufferLookup<T>(bool isReadOnly=true)=>new(Manager);
    public ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly=true)=>new(Manager);
    public bool TryGetSingleton<T>(out T singleton)
    {using var q=Manager.CreateEntityQuery(ComponentType.ReadOnly<T>());singleton=default;if(q.CalculateEntityCount()!=1)return false;singleton=q.GetSingleton<T>();return true;}
    public void CompleteDependency()=>Manager.CompleteAllTrackedJobs();
}
public struct UIActionsCD {public NetworkTick lastActionTick;}
public struct DurabilityCD {public int maxDurability;}
public struct PrioritizedRepairMaterialCD {public ObjectID objectID;}
public struct UpgradeCostsTableCD {public int Available;}
public struct CraftBuffer {public Entity playerEntity;public CraftActionData craftActionData;}
namespace Inventory
{
    public enum CraftAction {Craft,RepairOrReinforce,Upgrade}
    public enum InventoryAction {MoveAmount,MoveOrDropItem,Swap,DropItem,QuickStack,Sort,QuickStackToNearbyChests}
    public struct CraftActionData
    {
        public CraftAction craftAction;public ObjectID objectId;public int amount,additionalFreeAmount;
        public Entity playerEntity,craftingEntity,mainInventoryEntity,targetInventoryEntity;
        public int int0,int1;public bool bool0,bool1;
    }
    public struct InventoryChangeBuffer {public Entity playerEntity;public InventoryChangeData inventoryChangeData;}
}

public static class Boundary
{
    public static PugQuerySystem QuerySystem;
    public static List<(Entity entity,Type type)> BufferReads=new();
    public static int NativeCreated,NativeDisposed;
    public static int RepairCalls,UpgradeCalls;
    public static bool AllowRepair=true,AllowUpgrade=true;
    public static bool ThrowCosts;
    public static ObjectWithAmount RepairRecipe;
    public static Entity RepairStation,RepairPlayer;
    public static bool RepairFlag,ReinforceFlag;
    public static int ChestStart,UpgradeLevel;
    public static Entity[] Inventories;
    public static List<(ObjectID id,int need)> RepairCosts=new(){(ObjectID.Wood,2)};
    public static List<(ObjectID id,int need)> UpgradeCosts=new(){(ObjectID.IronBar,10),(ObjectID.AncientCoin,132)};
    public static void Reset()
    {BufferReads.Clear();NativeCreated=NativeDisposed=RepairCalls=UpgradeCalls=0;AllowRepair=AllowUpgrade=true;ThrowCosts=false;Inventories=null;RepairCosts=new(){(ObjectID.Wood,2)};UpgradeCosts=new(){(ObjectID.IronBar,10),(ObjectID.AncientCoin,132)};}
}

// These public game helpers are a boundary. Fixed returned cost rows are not an implementation of the game cost formula.
public static class InventoryUtility
{
    public static bool CanBeRepaired(Entity primary,ContainedObjectsBuffer item,BufferLookup<LevelEntitiesBuffer> levels,ComponentLookup<DurabilityCD> durability,bool isReinforcing)
    {Boundary.ReinforceFlag=isReinforcing;return Boundary.AllowRepair;}
    public static bool CanBeUpgraded(BufferLookup<LevelEntitiesBuffer> levels,ComponentLookup<ObjectCategoryTagsCD> tags,Entity primary)=>Boundary.AllowUpgrade;
    public static NativeList<PugDatabase.MaterialInfoData> GetCraftingMaterialInfosForRecipe(PugDatabase.DatabaseBankCD bank,BufferLookup<ContainedObjectsBuffer> objects,BufferLookup<InventoryBuffer> inventories,ComponentLookup<AnvilCD> anvil,ComponentLookup<ObjectDataCD> objectData,BufferLookup<SummarizedConditionsBuffer> conditions,ComponentLookup<DurabilityCD> durability,ComponentLookup<PrioritizedRepairMaterialCD> priority,ComponentLookup<LevelCD> level,ObjectWithAmount recipe,NativeList<ObjectWithAmount> cooking,NativeList<Entity> entities,int chestStart,bool repairing,bool reinforcing,Entity station,Entity player,Allocator allocator)
    {if(Boundary.ThrowCosts)throw new InvalidOperationException("cost lookup unavailable");Boundary.RepairCalls++;Boundary.RepairRecipe=recipe;Boundary.RepairStation=station;Boundary.RepairPlayer=player;Boundary.RepairFlag=repairing;Boundary.ReinforceFlag=reinforcing;Boundary.ChestStart=chestStart;Boundary.Inventories=Enumerable.Range(0,entities.Length).Select(i=>entities[i]).ToArray();var result=new NativeList<PugDatabase.MaterialInfoData>(allocator);foreach(var cost in Boundary.RepairCosts)result.Add(new(){objectID=cost.id,amountNeeded=cost.need});return result;}
    public static NativeList<PugDatabase.MaterialInfoData> GetCraftingMaterialInfosForUpgrade(BufferLookup<ContainedObjectsBuffer> objects,BufferLookup<InventoryBuffer> inventories,PugDatabase.DatabaseBankCD bank,UpgradeCostsTableCD table,int level,NativeList<Entity> entities,int chestStart,Allocator allocator)
    {if(Boundary.ThrowCosts)throw new InvalidOperationException("cost lookup unavailable");Boundary.UpgradeCalls++;Boundary.UpgradeLevel=level;Boundary.ChestStart=chestStart;Boundary.Inventories=Enumerable.Range(0,entities.Length).Select(i=>entities[i]).ToArray();var result=new NativeList<PugDatabase.MaterialInfoData>(allocator);foreach(var cost in Boundary.UpgradeCosts)result.Add(new(){objectID=cost.id,amountNeeded=cost.need});return result;}
}
