// AIObservation — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.Text;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

// ------------------------------------------------------------------------------------------ observation

public class SeenEnemy
{
    public Entity entity;
    public ObjectID id;
    public float3 pos;
    public int health, maxHealth;
    public float dist;
}

public class AIObservation
{
    public float time;
    public float3 pos;
    public int health, maxHealth, hunger;
    public int equippedSlot;
    public ObjectID equipped;
    public float2 half;
    public int walls, walkable, ores;
    public readonly List<(int2 p, int tileset, bool ore, int oreTileset)> wallTiles = new List<(int2, int, bool, int)>();
    public readonly List<SeenEnemy> enemies = new List<SeenEnemy>();
    // pull: 0 = lying, 1 = being pulled into the AI (the game's auto pickup started; a player sees it fly in), 2 = pulled
    // toward another player
    public readonly List<(ObjectID id, int amount, float3 pos, float dist, int pull)> items = new List<(ObjectID, int, float3, float, int)>();
    public float pickupDistance = 1.4f;   // the AI's auto-pickup radius (AutoPickUpItemCD.pickupDistance, base 1.4 tiles)
    public readonly List<(ObjectID id, float3 pos, float dist)> objects = new List<(ObjectID, float3, float)>();

    public static AIObservation Take(World server, Entity player, bool withTiles)
    {
        var em = server.EntityManager;
        var o = new AIObservation();
        o.time = Time.realtimeSinceStartup;
        o.pos = em.GetComponentData<LocalTransform>(player).Position;
        if (em.HasComponent<HealthCD>(player)) { var h = em.GetComponentData<HealthCD>(player); o.health = h.health; o.maxHealth = h.maxHealth; }
        if (em.HasComponent<HungerCD>(player)) o.hunger = em.GetComponentData<HungerCD>(player).hunger;
        if (em.HasComponent<EquippedObjectCD>(player)) { var eq = em.GetComponentData<EquippedObjectCD>(player); o.equippedSlot = eq.equippedSlotIndex; o.equipped = eq.containedObject.objectID; }
        o.half = AIVision.HalfExtents();

        if (withTiles)
        {
            var tr = new AITileReader(server);
            int2 c = AIBody.TileOf(o.pos);
            int hx = (int)math.floor(o.half.x), hz = (int)math.floor(o.half.y);
            for (int x = c.x - hx; x <= c.x + hx; x++)
            {
                for (int z = c.y - hz; z <= c.y + hz; z++)
                {
                    var p = new int2(x, z);
                    if (!AIVision.CanSee(o.pos, p)) continue;
                    if (tr.HasWall(p, out TileCD wall, out bool ore, out int oreTs))
                    {
                        o.walls++;
                        if (ore) o.ores++;
                        o.wallTiles.Add((p, wall.tileset, ore, oreTs));
                    }
                    else if (tr.Walkable(p)) o.walkable++;
                }
            }
        }

        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                var h = em.GetComponentData<HealthCD>(e);
                if (h.health <= 0) continue;
                if (em.HasComponent<EntityDestroyedCD>(e) && em.IsComponentEnabled<EntityDestroyedCD>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(o.pos, p.xz)) continue;
                o.enemies.Add(new SeenEnemy
                {
                    entity = e,
                    id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None,
                    pos = p, health = h.health, maxHealth = h.maxHealth, dist = math.distance(p.xz, o.pos.xz),
                });
            }
        }
        o.enemies.Sort((a, b) => a.dist.CompareTo(b.dist));

        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PickUpItemCD>(), ComponentType.ReadOnly<ContainedObjectsBuffer>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(o.pos, p.xz)) continue;
                var b = em.GetBuffer<ContainedObjectsBuffer>(e, true);
                if (b.Length == 0 || b[0].objectID == ObjectID.None) continue;
                var pu = em.GetComponentData<PickUpItemCD>(e);
                if (pu.state == PickUpItemState.HasBeenPickedUp) continue;   // already taken (vanishing into a bag)
                int pull = pu.state == PickUpItemState.IsBeingPickedUp || pu.state == PickUpItemState.ForcePickUp ? (pu.targetEntity == player ? 1 : 2) : 0;
                o.items.Add((b[0].objectID, b[0].amount, p, math.distance(p.xz, o.pos.xz), pull));
            }
        }
        o.items.Sort((a, b) => a.dist.CompareTo(b.dist));
        if (em.HasComponent<AutoPickUpItemCD>(player)) o.pickupDistance = em.GetComponentData<AutoPickUpItemCD>(player).pickupDistance;

        // Placed/natural objects with a physics collider (things a player bumps into), excluding players, enemies,
        // dropped items and tiles.
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsCollider>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<PickUpItemCD>(e) || em.HasComponent<TileCD>(e)) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(o.pos, p.xz)) continue;
                o.objects.Add((em.GetComponentData<ObjectDataCD>(e).objectID, p, math.distance(p.xz, o.pos.xz)));
            }
        }
        o.objects.Sort((a, b) => a.dist.CompareTo(b.dist));
        return o;
    }
}
