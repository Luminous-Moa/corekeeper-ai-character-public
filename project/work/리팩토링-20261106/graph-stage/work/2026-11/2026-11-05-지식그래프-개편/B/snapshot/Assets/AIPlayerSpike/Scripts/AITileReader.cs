// AITileReader — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// ------------------------------------------------------------------------------------------ tiles

// Reads the server's tile map through the EntityManager (SubMapRegistry + SubMapLayerBuffer), same data as the
// game's TileAccessor. Unloaded areas read as the game's default tile (wall, tileset 2), as in TileAccessor.
public class AITileReader
{
    private readonly EntityManager _em;
    private NativeParallelHashMap<int2, Entity> _map;
    public readonly bool Ok;

    public AITileReader(World server)
    {
        _em = server.EntityManager;
        using (var q = _em.CreateEntityQuery(ComponentType.ReadOnly<SubMapRegistry>()))
        {
            if (q.CalculateEntityCount() == 1)
            {
                _map = q.GetSingleton<SubMapRegistry>().IndexToEntity;
                Ok = _map.IsCreated;
            }
        }
    }

    public List<TileCD> Layers(int2 p)
    {
        var list = new List<TileCD>(4);
        int2 key = (p & -64) >> 6;
        if (!Ok || !_map.TryGetValue(key, out Entity sub) || !_em.Exists(sub) || !_em.HasBuffer<SubMapLayerBuffer>(sub))
        {
            list.Add(TileAccessor.DefaultTile);
            return list;
        }
        var buf = _em.GetBuffer<SubMapLayerBuffer>(sub, true);
        int2 local = p - key * 64;
        for (int i = 0; i < buf.Length; i++)
        {
            SubMapLayer l = buf[i].data;
            if (l.GetByRef(local)) list.Add(l.layer);
        }
        if (list.Count == 0) list.Add(TileAccessor.DefaultTile);
        return list;
    }

    public TileCD Top(int2 p)
    {
        TileCD best = default;
        foreach (var t in Layers(p))
        {
            if (t.tileType.GetSurfacePriority() > best.tileType.GetSurfacePriority()) best = t;
        }
        return best;
    }

    public bool Walkable(int2 p)
    {
        return Top(p).tileType.IsWalkableTile();
    }

    public bool HasWall(int2 p, out TileCD wall, out bool hasOre, out int oreTileset)
    {
        wall = default;
        hasOre = false;
        oreTileset = -1;
        bool found = false;
        foreach (var t in Layers(p))
        {
            if (t.tileType == TileType.wall) { wall = t; found = true; }
            if (t.tileType == TileType.ore) { hasOre = true; oreTileset = t.tileset; }
        }
        return found;
    }
}

// Wall durability from game data (same method as PathFindSystem.OnStartRunning): wall tileset -> object -> prefab
// -> HealthCD.maxHealth, DamageReductionCD.reduction.
public static class AIWallData
{
    public static bool TryGet(World server, int tileset, out int health, out int reduction, out ObjectID obj)
    {
        health = 0; reduction = 0; obj = ObjectID.None;
        var em = server.EntityManager;
        try
        {
            NativeHashMap<TileTypeTileSetTuple, ObjectDataCD> lookup;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileWithTilesetToObjectDataMapCD>()))
            {
                if (q.CalculateEntityCount() != 1) return false;
                lookup = q.GetSingleton<TileWithTilesetToObjectDataMapCD>().lookup;
            }
            BlobAssetReference<PugDatabase.PugDatabaseBank> bank;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() != 1) return false;
                bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
            }
            if (!lookup.TryGetValue((TileType.wall, (Tileset)tileset), out ObjectDataCD od)) return false;
            obj = od.objectID;
            Entity prefab = PugDatabase.GetPrimaryPrefabEntity(od.objectID, bank, od.variation);
            if (prefab == Entity.Null || !em.HasComponent<HealthCD>(prefab)) return false;
            health = em.GetComponentData<HealthCD>(prefab).maxHealth;
            if (em.HasComponent<DamageReductionCD>(prefab)) reduction = em.GetComponentData<DamageReductionCD>(prefab).reduction;
            return true;
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"wall data for tileset {tileset} failed", e);
            return false;
        }
    }

    // Damage estimate for the AI's next hit on a tile: base 20 (Constants.playerBaseMiningDamage) + the AI's
    // summarized Mining condition effect (granted e.g. by an equipped pick). Estimate only; the game's
    // CalculateDamage has more terms.
    public static int EstimateMiningDamage(World server, Entity player, out int miningEffect)
    {
        miningEffect = 0;
        try { miningEffect = EntityUtility.GetConditionEffectValue(ConditionEffect.Mining, player, server); } catch { }
        return 20 + miningEffect;
    }

    // Diagnosis while swinging: the AI's player state bits, pending tile-damage entries for p, and entities tagged as
    // damaged tiles near p (with health). Read-only.
    public static string DamageProbe(World server, Entity player, int2 p)
    {
        var em = server.EntityManager;
        string state = "?";
        try
        {
            if (em.HasComponent<PlayerState.PlayerStateCD>(player))
            {
                var s = em.GetComponentData<PlayerState.PlayerStateCD>(player);
                state = $"{s.AllStates()} next={s.nextState} locked={s.isStateLocked}";
            }
        }
        catch { }
        int bufHits = 0, bufDamage = 0, bufEntities = 0;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileDamageBuffer>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                bufEntities++;
                var b = em.GetBuffer<TileDamageBuffer>(e, true);
                for (int i = 0; i < b.Length; i++) if (math.all(b[i].position == p)) { bufHits++; bufDamage += b[i].damage; }
            }
        }
        var tags = new List<string>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileDamageTagCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                float3 pos = em.GetComponentData<LocalTransform>(e).Position;
                if (math.distance(pos.xz, (float2)p) > 1.5f) continue;
                string hp = em.HasComponent<HealthCD>(e) ? em.GetComponentData<HealthCD>(e).health.ToString() : "-";
                tags.Add($"({pos.x:F1},{pos.z:F1}) hp={hp} on={em.IsComponentEnabled<TileDamageTagCD>(e)}");
            }
        }
        return $"state=[{state}] damageBuffer(target)={bufHits} entries/{bufDamage} dmg (buffers {bufEntities}); damagedTileEntities near=[{string.Join(" ", tags)}]";
    }

    // Health of the live tile entity at p (tile entities exist around players), -1 if none found.
    public static int TileEntityHealth(World server, int2 p)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                var t = em.GetComponentData<TileCD>(e);
                if (t.tileType != TileType.wall) continue;
                if (math.all(AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position) == p))
                {
                    return em.GetComponentData<HealthCD>(e).health;
                }
            }
        }
        return -1;
    }
}
