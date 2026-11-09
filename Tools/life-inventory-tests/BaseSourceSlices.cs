// Verbatim method slices extracted from frozen AIBase.cs; supporting calls are stubs.
using Unity.Entities;
using ChestMem = AIBase.ChestMem;
using ChestView = AIBase.ChestView;
using Unity.Mathematics;
public static class ReviewBase {
public static List<(int slot, ObjectID id, int amount)> Storable(World server, Entity player, ObjectID filter)
    {
        var res = new List<(int, ObjectID, int)>();
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return res;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = AIBody.MainEnd(em, player, buf.Length);
        for (int i = 0; i < end; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None || (filter != ObjectID.None && id != filter)) continue;
            // food stays in the bag unless the store goal names it (보관 Mushroom: put that food away)
            if (filter == ObjectID.None && !StorableKind(server, id, buf[i].variation)) continue;
            var info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            if (info == null) continue;
            res.Add((i, id, info.isStackable ? buf[i].amount : 1));
        }
        return res;
    }
public static ObjectID ChestTarget(World server, Entity player)
    {
        if (AIBrain.Goal.kind != GoalKind.Store) return ObjectID.None;
        if (Storable(server, player, AIBrain.Goal.item).Count == 0) return ObjectID.None;
        if (BaseChests().Exists(c => c.openedUnix == 0 || c.free > 0)) return ObjectID.None;
        ObjectID ci = ChestItem(server, player);
        if (ci == ObjectID.None) return ObjectID.None;
        AIBody.InventoryCounts(server, player).TryGetValue(ci, out int have);
        return have > 0 ? ObjectID.None : ci;
    }
public static bool MayStackStored(World server, Entity player, ChestMem memory, (int slot, ObjectID id, int amount) item)
    {
        if (!memory.items.ContainsKey(item.id)) return false;
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var bag = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return item.slot >= 0 && item.slot < bag.Length && PugDatabase.GetObjectInfo(item.id, bag[item.slot].variation)?.isStackable == true;
    }
public static bool MayStoreKnownChest(World server, Entity player, ChestMem memory,
        List<(int slot, ObjectID id, int amount)> bag, List<ChestView> inView)
    {
        var visible = inView.Find(c => c.id == memory.id && math.all(AIBody.TileOf(c.pos) == memory.tile));
        if (visible == null || memory.openedUnix == 0)
            return memory.openedUnix == 0 || memory.free > 0 || bag.Exists(x => MayStackStored(server, player, memory, x));
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var actual = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return bag.Exists(x => x.slot >= 0 && x.slot < actual.Length && actual[x.slot].objectID == x.id &&
            AIInventory.HasStoreRoom(server, visible.entity, actual[x.slot]));
    }
private static bool StorableKind(World w,ObjectID id,int variation)=>AIBase.StorableKind(w,id,variation);
private static ObjectID ChestItem(World w,Entity p)=>ObjectID.Chest;
private static List<AIBase.ChestMem> BaseChests()=>new();
}
public static class AIBrain {public static AIGoal Goal=>AIGoals.Goal;}
