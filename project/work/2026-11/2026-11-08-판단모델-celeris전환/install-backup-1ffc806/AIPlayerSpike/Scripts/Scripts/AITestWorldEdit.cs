// AITestWorldEdit — TEST ONLY world editing for building test sites (치트명령-이식, 2026-11-08; session 코어키퍼-AI-캐릭터,
// work/2026-11/2026-11-08-cheat-commands). Chat commands behind AIPlayers/testcmds.on, like the other test commands:
//   /ai placetile <Tileset> <TileType> <x> <z>        /ai placetile <ObjectID> <x> <z>
//   /ai placetilearea <Tileset> <TileType> <x1> <z1> <x2> <z2>    /ai placetilearea <ObjectID> <x1> <z1> <x2> <z2>
//   /ai removetile <TileType> <x> <z>                 /ai removetile <ObjectID> <x> <z>
//   /ai removetilearea <TileType> <x1> <z1> <x2> <z2> /ai removetilearea <ObjectID> <x1> <z1> <x2> <z2>
//   /ai spawn <ObjectID> <x> <z> [variation]
// Coordinates are world tiles (as /ai hostto x z, /ai rmstation x z). "~N" is N tiles from the addressed AI's tile
// ("~" alone = the AI's tile), the original's relative form. Coordinates beyond ±1000000 are refused; area commands edit
// at most 4096 tiles. spawn drops non-prefab objects on the ground (no pull to anyone).
//
// The tile and spawn code below is adapted from ChatCommands by limoka (github.com/limoka/CoreKeeperMods,
// ChatCommands/Scripts/Commands: PlaceTileCommand, PlaceTileAreaCommand, RemoveTileCommand, RemoveTileAreaCommand,
// SpawnCommandHandler). The CoreLib command plumbing and helpers it used are not included; argument parsing here is
// this mod's own. Original license:
//
// MIT License
//
// Copyright (c) 2021 limoka
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System;
using System.Collections.Generic;
using PugTilemap;
using PugTilemap.Quads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class AITestWorldEdit
{
    // One area command edits at most this many tiles (the original had no limit; a typo could freeze the host).
    private const int MaxAreaTiles = 4096;
    // Coordinates (absolute or after "~N") beyond this are refused, so sums and loop counters never overflow.
    private const int MaxCoord = 1000000;

    public static void Command(World server, Entity player, string arg, string[] parts)
    {
        // parts[0] = "/ai", parts[1] = the command, the rest = its arguments
        var a = new List<string>();
        for (int i = 2; i < parts.Length; i++) a.Add(parts[i]);
        int2 me = AIBody.TileOf(AIBody.Pos(server, player));
        string result;
        switch (arg)
        {
            case "placetile": result = PlaceTile(server, a, me); break;
            case "placetilearea": result = PlaceTileArea(server, a, me); break;
            case "removetile": result = RemoveTile(server, a, me); break;
            case "removetilearea": result = RemoveTileArea(server, a, me); break;
            case "spawn": result = Spawn(server, a, me); break;
            default: return;
        }
        AIPlayerSpikeMod.Log($"TEST-ONLY {arg} [{string.Join(" ", a)}] from AI tile ({me.x},{me.y}): {result}");
        AIPlayerSpikeMod.Reply($"[시험용] {result}");
    }

    // ------------------------------------------------------------------ arguments

    // "12" = world tile 12, "~3" = 3 from the AI's tile on that axis, "~" = the AI's tile. False beyond ±MaxCoord.
    private static bool Coord(string s, int origin, out int v)
    {
        v = 0;
        long r;
        if (s.StartsWith("~"))
        {
            if (s.Length == 1) r = origin;
            else if (int.TryParse(s.Substring(1), out int d)) r = (long)origin + d;
            else return false;
        }
        else if (int.TryParse(s, out int abs)) r = abs;
        else return false;
        if (r > MaxCoord || r < -MaxCoord) return false;
        v = (int)r;
        return true;
    }

    // Tiles in the rectangle lo..hi, counted in long (coordinates are within ±MaxCoord, so this cannot overflow).
    private static long AreaTiles(int2 lo, int2 hi) => ((long)hi.x - lo.x + 1) * ((long)hi.y - lo.y + 1);

    // The two words at a[i], a[i+1] as an x z tile.
    internal static bool Pos(List<string> a, int i, int2 me, out int2 p)
    {
        p = default;
        if (i < 0 || i + 1 >= a.Count) return false;
        if (!Coord(a[i], me.x, out int x) || !Coord(a[i + 1], me.y, out int z)) return false;
        p = new int2(x, z);
        return true;
    }

    // An ObjectID by its enum name (case ignored) or a defined number; name words may be split by spaces.
    private static bool ObjectOf(List<string> a, int count, out ObjectID id)
    {
        id = ObjectID.None;
        if (count < 1) return false;
        string name = string.Join("", a.GetRange(0, count));
        return Enum.TryParse(name, true, out id) && id != ObjectID.None && Enum.IsDefined(typeof(ObjectID), id);
    }

    // ------------------------------------------------------------------ place

    private static string PlaceTile(World server, List<string> a, int2 me)
    {
        const string usage = "사용법: /ai placetile <Tileset> <TileType> <x> <z> · /ai placetile <ObjectID> <x> <z> (좌표 ~N = AI 기준, ±1000000 안)";
        if (a.Count < 3 || !Pos(a, a.Count - 2, me, out int2 p)) return usage;
        if (!TileOf(a, a.Count - 2, out int tileset, out TileType type, out string why)) return why ?? usage;
        string bad = CheckTile(tileset, type);
        if (bad != null) return bad;
        if (!PlaceOne(server, p, tileset, type)) return $"({p.x},{p.y}) 설치 거부(크리에이티브가 아니면 코어 주변·tileset 2 금지)";
        return $"{(Tileset)tileset} {type} 설치 ({p.x},{p.y})";
    }

    private static string PlaceTileArea(World server, List<string> a, int2 me)
    {
        const string usage = "사용법: /ai placetilearea <Tileset> <TileType> <x1> <z1> <x2> <z2> · /ai placetilearea <ObjectID> <x1> <z1> <x2> <z2>";
        if (a.Count < 5 || !Pos(a, a.Count - 4, me, out int2 s) || !Pos(a, a.Count - 2, me, out int2 e)) return usage;
        if (!TileOf(a, a.Count - 4, out int tileset, out TileType type, out string why)) return why ?? usage;
        string bad = CheckTile(tileset, type);
        if (bad != null) return bad;
        int2 lo = math.min(s, e), hi = math.max(s, e);
        long area = AreaTiles(lo, hi);
        if (area > MaxAreaTiles) return $"범위가 너무 큼: {area}칸(최대 {MaxAreaTiles})";
        int n = (int)area;
        int placed = 0;
        for (int x = lo.x; x <= hi.x; x++)
            for (int z = lo.y; z <= hi.y; z++)
                if (PlaceOne(server, new int2(x, z), tileset, type)) placed++;
        return $"{(Tileset)tileset} {type} 설치 ({lo.x},{lo.y})~({hi.x},{hi.y}) {placed}/{n}칸";
    }

    // The tile named by the first `count` words: "<Tileset> <TileType>" or an ObjectID that is a tile item.
    private static bool TileOf(List<string> a, int count, out int tileset, out TileType type, out string why)
    {
        tileset = 0; type = default; why = null;
        if (count == 2 && Enum.TryParse(a[0], true, out Tileset ts) && Enum.IsDefined(typeof(Tileset), ts)
            && Enum.TryParse(a[1], true, out TileType tt) && Enum.IsDefined(typeof(TileType), tt))
        {
            tileset = (int)ts; type = tt;
            return true;
        }
        if (!ObjectOf(a, count, out ObjectID id)) { why = $"타일셋·타일 종류나 ObjectID를 모름: {string.Join(" ", a.GetRange(0, Math.Max(0, count)))}"; return false; }
        if (!PugDatabase.HasComponent<TileCD>(id)) { why = $"{id}은(는) 타일이 아님"; return false; }
        TileCD t = PugDatabase.GetComponent<TileCD>(id);
        tileset = t.tileset; type = t.tileType;
        return true;
    }

    // From the original TryPlaceTile: the tileset must define that tile type. Applied to the ObjectID form too (the
    // original placed a tile item's TileCD unchecked). An unknown tileset number gives null data -> "없음", not an NRE.
    private static string CheckTile(int tileset, TileType type)
    {
        TilesetDataBlock data = TilesetDataBlock.Get(tileset);
        if (data == null) return $"타일셋 {tileset} 없음";
        var def = data.layerDefinition.Get();
        if (def == null) return $"타일셋 {(Tileset)tileset}의 층 정의 없음";
        LayerName layer = TileTypeToLayerName.GetLayerName(type);
        QuadGenerator gen = def.GetDef(layer);
        return gen == null ? $"타일셋 {(Tileset)tileset}에 {type}이(가) 없음" : null;
    }

    // From the original PlaceTile. False = refused by the original's guard (outside creative: tileset 2 and the tiles
    // around the core at (0,0)).
    private static bool PlaceOne(World server, int2 pos, int tileset, TileType type)
    {
        if (!Manager.saves.IsWorldModeEnabled(WorldMode.Creative) &&
            (tileset == 2 ||
             math.all(pos == new int2(0, 0)) ||
             math.all(pos == new int2(0, 1)) ||
             math.all(pos == new int2(-1, 1)) ||
             math.all(pos == new int2(1, 1))))
            return false;
        var buf = UpdateBuffer(server);
        buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileset = tileset, tileType = type } });
        if (type == TileType.wall)
        {
            buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.roofHole } });
        }
        else if (type == TileType.ground)
        {
            buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.pit } });
            buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.water } });
        }
        return true;
    }

    private static DynamicBuffer<TileUpdateBuffer> UpdateBuffer(World server)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadWrite<TileUpdateBuffer>()))
            return em.GetBuffer<TileUpdateBuffer>(q.GetSingletonEntity());
    }

    // ------------------------------------------------------------------ remove

    private static string RemoveTile(World server, List<string> a, int2 me)
    {
        const string usage = "사용법: /ai removetile <TileType> <x> <z> · /ai removetile <ObjectID> <x> <z> (좌표 ~N = AI 기준, ±1000000 안)";
        if (a.Count < 3 || !Pos(a, a.Count - 2, me, out int2 p)) return usage;
        if (!TileTypeOf(a, a.Count - 2, out TileType type, out string why)) return why ?? usage;
        RemoveOne(server, new AITileReader(server), p, type);
        return $"{type} 제거 ({p.x},{p.y})";
    }

    private static string RemoveTileArea(World server, List<string> a, int2 me)
    {
        const string usage = "사용법: /ai removetilearea <TileType> <x1> <z1> <x2> <z2> · /ai removetilearea <ObjectID> <x1> <z1> <x2> <z2>";
        if (a.Count < 5 || !Pos(a, a.Count - 4, me, out int2 s) || !Pos(a, a.Count - 2, me, out int2 e)) return usage;
        if (!TileTypeOf(a, a.Count - 4, out TileType type, out string why)) return why ?? usage;
        int2 lo = math.min(s, e), hi = math.max(s, e);
        long area = AreaTiles(lo, hi);
        if (area > MaxAreaTiles) return $"범위가 너무 큼: {area}칸(최대 {MaxAreaTiles})";
        int n = (int)area;
        var tr = new AITileReader(server);
        for (int x = lo.x; x <= hi.x; x++)
            for (int z = lo.y; z <= hi.y; z++)
                RemoveOne(server, tr, new int2(x, z), type);
        return $"{type} 제거 ({lo.x},{lo.y})~({hi.x},{hi.y}) {n}칸";
    }

    // "<TileType>" or an ObjectID that is a tile item (its tile type).
    private static bool TileTypeOf(List<string> a, int count, out TileType type, out string why)
    {
        type = default; why = null;
        if (count == 1 && Enum.TryParse(a[0], true, out TileType tt) && Enum.IsDefined(typeof(TileType), tt)) { type = tt; return true; }
        if (!ObjectOf(a, count, out ObjectID id)) { why = $"타일 종류나 ObjectID를 모름: {string.Join(" ", a.GetRange(0, Math.Max(0, count)))}"; return false; }
        if (!PugDatabase.HasComponent<TileCD>(id)) { why = $"{id}은(는) 타일이 아님"; return false; }
        type = PugDatabase.GetComponent<TileCD>(id).tileType;
        return true;
    }

    // From the original RemoveTile: remove the type, and every tile at that spot that needs it (e.g. what stands on
    // the ground); removing ground or water leaves a pit. The tiles at the spot are read with AITileReader (the same
    // map data as the original's TileAccessor).
    private static void RemoveOne(World server, AITileReader tr, int2 pos, TileType type)
    {
        var buf = UpdateBuffer(server);
        buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = type } });
        var needed = new NativeList<TileType>(4, Allocator.Temp);
        foreach (TileCD t in tr.Layers(pos))
        {
            needed.Clear();
            t.tileType.GetNeededTile(ref needed);
            for (int i = 0; i < needed.Length; i++)
                if (needed[i] == type)
                {
                    buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = t.tileType } });
                    break;
                }
        }
        needed.Dispose();
        if (type == TileType.ground || type == TileType.water)
            buf.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.pit } });
    }

    // ------------------------------------------------------------------ spawn

    // From the original SpawnID: an object with a prefab is created as an entity (enemy, station, chest, plant, ...);
    // anything else is dropped as an item on the ground.
    private static string Spawn(World server, List<string> a, int2 me)
    {
        const string usage = "사용법: /ai spawn <ObjectID> <x> <z> [variation] (좌표 ~N = AI 기준, ±1000000 안)";
        int variation = 0;
        int end = a.Count;
        // optional last number = variation when three coordinates-like words end the line
        if (a.Count >= 4 && int.TryParse(a[a.Count - 1], out int v) && Pos(a, a.Count - 3, me, out _)) { variation = v; end = a.Count - 1; }
        var b = a.GetRange(0, end);
        if (b.Count < 3 || !Pos(b, b.Count - 2, me, out int2 p)) return usage;
        if (!ObjectOf(b, b.Count - 2, out ObjectID id)) return $"ObjectID를 모름: {string.Join(" ", b.GetRange(0, b.Count - 2))}";
        if (id == ObjectID.Player) return "Player는 생성하지 않음";
        ObjectInfo info = PugDatabase.GetObjectInfo(id);
        if (info == null) return $"{id} 정보 없음";
        var em = server.EntityManager;
        BlobAssetReference<PugDatabase.PugDatabaseBank> bank;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
        var at = new float3(p.x, 0f, p.y);
        if (!info.prefabInfo.graphicalRef.hasAddress)
        {
            var item = new ContainedObjectsBuffer { objectData = new ObjectDataCD { objectID = info.objectID, amount = 1, variation = variation } };
            // Entity.Null = no pull target: the item stays on the ground at (x,z). Passing the AI here would make it a
            // ForcePickUp into the AI's bag (the original pulled it to the sender); /ai give is the way to fill the bag.
            EntityUtility.DropNewEntity(server, item, at, bank, Entity.Null);
            return $"{id} 아이템 떨굼 ({p.x},{p.y})";
        }
        Entity e = EntityUtility.CreateEntity(server, at, id, 1, bank, variation);
        return e == Entity.Null ? $"{id} 생성 실패 ({p.x},{p.y})" : $"{id} 생성 ({p.x},{p.y}){(variation != 0 ? $" variation {variation}" : "")}";
    }
}
