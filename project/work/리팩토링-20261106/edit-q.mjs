import fs from 'node:fs';
import {randomUUID} from 'node:crypto';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106/Assets/AIPlayerSpike/Scripts/';
const read=p=>fs.readFileSync(root+p,'utf8').replaceAll('\r\n','\n');
function write(p,s){fs.writeFileSync(root+p,s);if(!fs.existsSync(root+p+'.meta'))fs.writeFileSync(root+p+'.meta','fileFormatVersion: 2\nguid: '+randomUUID().replaceAll('-','')+'\n');}
function rep(s,a,b){if(!s.includes(a))throw Error('missing '+a);return s.replace(a,b);}
function method(s,name){const re=new RegExp('^    (?:public|private|internal) static [^\\n]*\\b'+name+'\\([^\\n]*\\)\\n    \\{','m');const m=re.exec(s);if(!m)throw Error('method '+name);const end=s.indexOf('\n    }',m.index)+6;return s.slice(m.index,end);}
let reach=read('AIEdge.Reach.cs'),edge=read('AIEdge.cs');
const doors=method(reach,'DoorsNear');
reach=rep(reach,doors+'\n','');
const at=method(edge,'DoorAt');
const first=at.replace('public static DoorView DoorAt(World server, int2 tile, float3 me)','public static DoorFact? FirstAt(World server, int2 tile)').replace('!(em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e))','!IsDoorLike(em, e)').replace('new DoorView { e = e, id = od.objectID, tile = tile, variation = od.variation, kind =','new DoorFact { Entity = e, Id = od.objectID, Tile = tile, Variation = od.variation, Kind =').replace(', dist = math.distance(me.xz, (float2)tile)','');
edge=rep(edge,at,`    public static DoorView DoorAt(World server, int2 tile, float3 me)
    {
        var fact = AIDoorFacts.FirstAt(server, tile);
        if (!fact.HasValue) return null;
        var d = fact.Value;
        return new DoorView { e = d.Entity, id = d.Id, tile = d.Tile, variation = d.Variation, kind = d.Kind, dist = math.distance(me.xz, (float2)tile) };
    }`);
const a=edge.indexOf('        var openDoors = new HashSet<int2>();'),b=edge.indexOf('        for (int x = 0; x < n; x++)',a);
const openBlock=edge.slice(a,b);
edge=edge.slice(0,a)+'        var openDoors = AIDoorFacts.OpenTilesExcept(server, seed, Window, t => S.doors.ContainsKey(t));\n'+edge.slice(b);
let open=openBlock.replaceAll('seed.x','center.x').replaceAll('seed.y','center.y').replaceAll('> Window','> radius').replace('S.doors.ContainsKey(dt)','excluded(dt)').replace('!(em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e))','!IsDoorLike(em, e)');
write('AIDoorFacts.cs',`using System;
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

${doors.replace('private static','public static').replace('DoorsNear(World server, int2 c, int r)','InWindowLastByTile(World server, int2 center, int radius)').replaceAll('c.x','center.x').replaceAll('c.y','center.y').replaceAll('> r)', '> radius)').replaceAll('> r ||','> radius ||')}

${first}

    public static HashSet<int2> OpenTilesExcept(World server, int2 center, int radius, Func<int2, bool> excluded)
    {
${open}        return openDoors;
    }
}
`);
write('AIEdge.Reach.cs',reach);write('AIEdge.cs',edge);
for(const p of ['AIEdge.Reach.cs','AIEdge.Guard.cs','AIEdge.Place.cs'])write(p,read(p).replace(/\bDoorsNear\(/g,'AIDoorFacts.InWindowLastByTile('));
write('AIBaseProbe.cs',rep(read('AIBaseProbe.cs'),'private static bool IsDoorLike(EntityManager em, Entity e) => em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e);','private static bool IsDoorLike(EntityManager em, Entity e) => AIDoorFacts.IsDoorLike(em, e);'));
write('AIWorldFacts.cs',`using Unity.Entities;
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
`);
for(const p of ['AIEdge.cs','AIPlace.cs'])write(p,rep(read(p),'bool known = AIVision.CanSee(me, (float2)t) || AIMemory.SeenTile(t);','bool known = AIWorldFacts.IsKnown(me, t);'));
for(const p of ['AIEdge.cs','AIEdge.Guard.cs'])write(p,read(p).replaceAll('if (em.HasComponent<EnemyCD>(e) || em.HasComponent<PlayerGhost>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<PickUpItemCD>(e) || em.HasComponent<CritterCD>(e)) continue;','if (AIWorldFacts.IsExcludedPlacedEntity(em, e)) continue;'));
write('AIEdge.Guard.cs',read('AIEdge.Guard.cs').replaceAll('ObjectInfo info = PugDatabase.GetObjectInfo(od.objectID, 0);\n                if (info == null || info.objectType != ObjectType.PlaceablePrefab && !em.HasComponent<DoorCD>(e) && !em.HasComponent<GateCD>(e)) continue;\n                if (od.objectID == ObjectID.TheCore) continue;','if (!AIWorldFacts.IsPlacedObjectOrDoor(em, e, od.objectID)) continue;'));
