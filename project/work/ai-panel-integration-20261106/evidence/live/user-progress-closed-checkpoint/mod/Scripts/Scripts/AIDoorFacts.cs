using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct DoorFact
{
    public Entity Entity;
    public ObjectID Id;
    public int2 Tile;
    public int Variation;
    public DoorKind Kind;
}

// Query order is part of each contract: last per tile, first at a tile, or any open door.
public static class AIDoorFacts
{
    public static bool IsDoorLike(EntityManager em, Entity e) => em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e);

    // doors (any owner) in a window: tile -> (entity, item, variation)
    public static Dictionary<int2, (Entity e, ObjectID id, int variation)> InWindowLastByTile(World server, int2 center, int radius)
    {
        var res = new Dictionary<int2, (Entity, ObjectID, int)>();
        var em = server.EntityManager;
        foreach (var ct in new[] { ComponentType.ReadOnly<DoorCD>(), ComponentType.ReadOnly<GateCD>() })
            using (var q = em.CreateEntityQuery(ct, ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
            using (var ents = q.ToEntityArray(Allocator.Temp))
                foreach (var e in ents)
                {
                    int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                    if (math.abs(t.x - center.x) > radius || math.abs(t.y - center.y) > radius) continue;
                    var od = em.GetComponentData<ObjectDataCD>(e);
                    res[t] = (e, od.objectID, od.variation);
                }
        return res;
    }

    public static DoorFact? FirstAt(World server, int2 tile)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (!IsDoorLike(em, e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!math.all(AIBody.TileOf(p) == tile)) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                return new DoorFact { Entity = e, Id = od.objectID, Tile = tile, Variation = od.variation, Kind = em.HasComponent<ActivatedByElectricityStateCD>(e) ? DoorKind.Auto : DoorKind.Manual };
            }
        return null;
    }

    public static HashSet<int2> OpenTilesExcept(World server, int2 center, int radius, Func<int2, bool> excluded)
    {
        var openDoors = new HashSet<int2>();
        var em = server.EntityManager;
        using (var dq = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var dents = dq.ToEntityArray(Allocator.Temp))
            foreach (var e in dents)
            {
                if (!IsDoorLike(em, e)) continue;
                int2 dt = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                if (math.abs(dt.x - center.x) > radius || math.abs(dt.y - center.y) > radius || excluded(dt)) continue;
                if (AIDoorPass.IsOpen(em.GetComponentData<ObjectDataCD>(e).variation)) openDoors.Add(dt);
            }
        return openDoors;
    }
}
