using Unity.Entities;
using Unity.Mathematics;

public static class AIWorldFacts
{
    public static bool IsKnown(float3 me, int2 tile) => AIVision.CanSee(me, (float2)tile) || AIMemory.SeenTile(tile);

    public static bool IsExcludedPlacedEntity(EntityManager em, Entity e) =>
        em.HasComponent<EnemyCD>(e) || em.HasComponent<PlayerGhost>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<PickUpItemCD>(e) || em.HasComponent<CritterCD>(e);

    // Creature/tile exclusion remains a separate caller step, before this prefab/door fact.
    public static bool IsPlacedObjectOrDoor(EntityManager em, Entity e, ObjectID id)
    {
        ObjectInfo info = PugDatabase.GetObjectInfo(id, 0);
        return info != null && (info.objectType == ObjectType.PlaceablePrefab || em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e)) && id != ObjectID.TheCore;
    }
}
