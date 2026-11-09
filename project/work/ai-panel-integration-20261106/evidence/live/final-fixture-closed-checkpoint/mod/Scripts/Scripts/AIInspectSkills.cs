// AIInspectSkills — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// ------------------------------------------------------------------------------------------ skills

public class LookSkill : AISkill
{
    public LookSkill() { Name = "보기"; }

    public override bool Start(World server, Entity player, float now)
    {
        var o = AIObservation.Take(server, player, true);
        float3 host = AIBody.HostPosition(server, player);
        var sb = new StringBuilder();
        sb.Append($"보기: 범위 {o.half.x * 2:F0}x{o.half.y * 2:F0}칸(사람 화면, 빛 미반영) · {AIAgent.Cur.Name} ({o.pos.x:F1},{o.pos.z:F1}) · 호스트 ({host.x:F1},{host.z:F1}) · ");
        sb.Append($"벽 {o.walls}(광석 {o.ores}) · 적 {o.enemies.Count}");
        if (o.enemies.Count > 0) sb.Append($"(가까운 {o.enemies[0].id} {o.enemies[0].dist:F1}칸 {o.enemies[0].health}/{o.enemies[0].maxHealth})");
        sb.Append($" · 아이템 {o.items.Count} · 물체 {o.objects.Count}");
        AIPlayerSpikeMod.Reply(sb.ToString());

        var wallKinds = new Dictionary<int, int>();
        foreach (var w in o.wallTiles) { wallKinds.TryGetValue(w.tileset, out int n); wallKinds[w.tileset] = n + 1; }
        var kinds = new List<string>();
        foreach (var kv in wallKinds)
        {
            string dur = AIWallData.TryGet(server, kv.Key, out int hp, out int red, out ObjectID obj) ? $"{obj} hp={hp} reduction={red}" : "data?";
            kinds.Add($"tileset {(Tileset)kv.Key}({kv.Key}) x{kv.Value} [{dur}]");
        }
        var ores = new List<string>();
        foreach (var w in o.wallTiles) if (w.ore && ores.Count < 20) ores.Add($"({w.p.x},{w.p.y}) ore={(Tileset)w.oreTileset} wall={(Tileset)w.tileset}");
        var enemies = new List<string>();
        foreach (var e in o.enemies) enemies.Add($"{e.id} {e.entity} ({e.pos.x:F1},{e.pos.z:F1}) d={e.dist:F1} hp={e.health}/{e.maxHealth}");
        var items = new List<string>();
        foreach (var i in o.items) if (items.Count < 20) items.Add($"{i.id}x{i.amount} ({i.pos.x:F1},{i.pos.z:F1}) d={i.dist:F1}");
        Facts.Add($"rule='{AIVision.RuleName}' half=({o.half.x:F1},{o.half.y:F1}) {AIVision.MeasureNote}");
        Facts.Add($"ai=({o.pos.x:F2},{o.pos.z:F2}) hp={o.health}/{o.maxHealth} hunger={o.hunger} equipped slot={o.equippedSlot} {o.equipped} host=({host.x:F2},{host.z:F2})");
        Facts.Add($"walkable={o.walkable} walls={o.walls} ores={o.ores} wallKinds=[{string.Join(" | ", kinds)}]");
        Facts.Add($"oreTiles=[{string.Join(" | ", ores)}]");
        Facts.Add($"enemies=[{string.Join(" | ", enemies)}]");
        Facts.Add($"items=[{string.Join(" | ", items)}]");
        var objs = new List<string>();
        foreach (var ob in o.objects) if (objs.Count < 15) objs.Add($"{ob.id} ({ob.pos.x:F1},{ob.pos.z:F1}) d={ob.dist:F1}");
        Facts.Add($"objects={o.objects.Count} nearest=[{string.Join(" | ", objs)}]");
        Status = SkillStatus.Done;
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now) => BodyInput.Idle;
}

public class InvSkill : AISkill
{
    public InvSkill() { Name = "가방"; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        var parts = new List<string>();
        int empty = 0;
        if (em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            int bag = math.min(buf.Length, 40);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].objectID == ObjectID.None) { if (i < bag) empty++; continue; }
                parts.Add($"{i}:{buf[i].objectID}x{buf[i].amount}");
            }
            Facts.Add($"slots={buf.Length} emptyInFirst40={empty}");
        }
        string eq = "?";
        if (em.HasComponent<EquippedObjectCD>(player))
        {
            var e = em.GetComponentData<EquippedObjectCD>(player);
            eq = $"{e.equippedSlotIndex}번 칸 {(e.containedObject.objectID == ObjectID.None ? "빈손" : e.containedObject.objectID.ToString())}";
        }
        Facts.Add($"items=[{string.Join(", ", parts)}] equipped={eq}");
        AIPlayerSpikeMod.Reply($"가방: {(parts.Count == 0 ? "비어 있음" : string.Join(", ", parts))} · 든 것 {eq} · 앞 40칸 중 빈 칸 {empty}");
        Status = SkillStatus.Done;
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now) => BodyInput.Idle;
}
