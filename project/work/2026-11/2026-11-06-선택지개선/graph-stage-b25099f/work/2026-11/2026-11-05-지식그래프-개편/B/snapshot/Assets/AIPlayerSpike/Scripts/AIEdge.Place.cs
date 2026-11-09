// AIEdge (제작대-놓기) — what the place goal needs from the base edge (1-설계.md §2.10, §6):
// - MarkPlaceGrid: the plan's marks on the place options' window (gap tiles and how many are open, the door spot and its
//   sides, the inside, an own edge door to come in by), so AIPlaceGrid states what placing there does to the edge. The
//   edge plan itself is not made or changed here.
// - My doors (§6 (b)(c), user decision B): a door the AI placed off the plan is remembered as "my door" (kept per world
//   in memory.json), apart from the edge doors. The walk passes my doors as it passes edge doors (open -> through ->
//   close), and an open one gets the close option; repair and breach checks stay with the edge doors only.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static partial class AIEdge
{
    // the walk opens and passes these (edge doors and my doors)
    // 내문-판정: the one "is it the AI's own door" judgement — the walk, the dig route's door cost and the reach grid
    // (an own closed door the AI opens and passes) all ask this
    public static bool IsWalkDoorTile(int2 t) => S.doors.ContainsKey(t) || IsMyDoorTile(t);
    public static IEnumerable<int2> MyDoorTiles => S.myDoors;
    public static bool HasMyDoors => S.myDoors.Count > 0;

    // 검수 Q5: the walk-door tiles as the AI knows them now (edge doors as OwnDoorTilesNow, and my doors the same way: in
    // view where a door stands, out of view as remembered unless the tile was seen open at the last look) — the place
    // grid's door passage
    public static HashSet<int2> WalkDoorTilesNow(World server, Unity.Mathematics.float3 me, int2 c, int r)
    {
        var res = OwnDoorTilesNow(server, me, c, r);
        var s = S;
        if (s.myDoors.Count == 0) return res;
        var doors = AIDoorFacts.InWindowLastByTile(server, c, r);
        foreach (var t in s.myDoors)
        {
            if (s.doors.ContainsKey(t) || math.abs(t.x - c.x) > r || math.abs(t.y - c.y) > r) continue;
            if (AIBaseGuard.OwnDoorStands(AIVision.CanSee(me, (float2)t), doors.ContainsKey(t), s.edgeSeen.Last(t.x, t.y))) res.Add(t);
        }
        return res;
    }

    // 내문-판정 결함 1: the one look at my doors — a my-door tile in view with no door standing (AIBaseGuard.OwnDoorStands)
    // is forgotten: seen gone, it is the tile as it is from then on (open ground in the reach grid, in view and out of
    // it), while a my door last seen standing stays the door out of view (AIBaseGuard.DoorCell). Every 0.5 s from
    // TrackDoor (with a plan or without). 32-검수 M1: only looks that can see the door count (the AI's area loaded, the
    // tile read as ground, or as water / lava: 33-재검수 N1) and only "no door" in every such look for a second or more
    // (AIBaseGuard.MyDoorLook.LookTile).
    public static void LookMyDoors(World server, Entity player, float now)
    {
        var s = S;
        if (s.myDoors.Count == 0) return;
        var em = server.EntityManager;
        Unity.Mathematics.float3 me = AIBody.Pos(server, player);
        bool areaLoaded = em.Exists(player) && em.HasComponent<AreaLoadedCD>(player);   // the game adds it once the area round the player is loaded
        int2 c = AIBody.TileOf(me);
        var seen = new List<int2>(); int r = 0;
        foreach (var t in s.myDoors) if (AIVision.CanSee(me, (float2)t)) { seen.Add(t); r = math.max(r, math.max(math.abs(t.x - c.x), math.abs(t.y - c.y))); }
        Dictionary<int2, (Entity e, ObjectID id, int variation)> doors = seen.Count > 0 && areaLoaded ? AIDoorFacts.InWindowLastByTile(server, c, r) : null;   // every my door in view lies within r
        var tr = doors != null ? new AITileReader(server) : null;
        var gone = new List<int2>();
        foreach (var t in new List<int2>(s.myDoors))
        {
            bool inView = seen.Contains(t), read = doors != null;
            // 33-재검수 N1: a tile turned to water or lava is a look with no door (doors do not stand on it)
            bool liquid = read && (AIFarm.IsWater(tr, t) || AIPlaceSpot.IsLava(tr, t));
            if (s.myDoorLook.LookTile(t.x, t.y, inView, read, read && tr.Walkable(t), liquid, read && doors.ContainsKey(t), now)) gone.Add(t);
        }
        foreach (var t in gone) ForgetMyDoor(t, $"보이는데 문이 없음({AIBaseGuard.MyDoorLook.ForgetAfter:F0}초 넘게 볼 때마다)");
    }

    // a door placed off the plan (or with no plan): my door (AIEdge.NotePlaced)
    private static bool NoteMyDoor(World server, ObjectID item, int2 tile)
    {
        if (!IsDoorItem(server, item)) return false;
        S.myDoors.Add(tile); S.myDoorLook.Drop(tile.x, tile.y);
        S.edgeSeen.Forget(tile.x, tile.y);   // 33-재검수 N2: a door stands there now (an old plan's "seen open" would read it gone out of view)
        AIPlayerSpikeMod.Log($"EDGE memory: {item} at ({tile.x},{tile.y}) is my door (placed off the edge plan; my doors {S.myDoors.Count})");
        AIMemory.MarkDirty();
        return true;
    }

    // a my-door tile seen without a door: forgotten (the walk would try to open nothing)
    public static void ForgetMyDoor(int2 t, string why)
    {
        S.myDoorLook.Drop(t.x, t.y);
        if (!S.myDoors.Remove(t)) return;
        AIPlayerSpikeMod.Log($"EDGE memory: my door ({t.x},{t.y}) forgotten: {why}");
        AIMemory.MarkDirty();
    }

    private static void ImportMyDoors(AIMemory.Data d)
    {
        S.myDoors.Clear(); S.myDoorLook.Clear();
        for (int i = 0; d.edgeMyDoors != null && i + 1 < d.edgeMyDoors.Length; i += 2) S.myDoors.Add(new int2(d.edgeMyDoors[i], d.edgeMyDoors[i + 1]));
    }

    private static void ExportMyDoors(AIMemory.Data d)
    {
        var l = new List<int>();
        foreach (var t in S.myDoors) { l.Add(t.x); l.Add(t.y); }
        d.edgeMyDoors = l.ToArray();
    }

    // §2.11: the edge tiles among a rect / line's tiles, in one sentence ("" none): "그중 3칸이 둘레 틈 1을 메움, 1칸이 문 자리"
    public static string ShapeEdgeNote(List<int2> tiles)
    {
        var s = S;
        if (!s.planSet || tiles == null) return "";
        var perGap = new SortedDictionary<int, int>(); int door = 0, inside = 0;
        foreach (var t in tiles)
        {
            if (s.door.HasValue && math.all(s.door.Value == t)) { door++; continue; }
            int gi = s.gaps.FindIndex(g => g.Contains(t));
            if (gi >= 0) { perGap.TryGetValue(gi + 1, out int c); perGap[gi + 1] = c + 1; }
            else if (s.inside.Contains(t)) inside++;
        }
        return AIPlaces.ShapeEdgeText(perGap, door, inside);   // 실게임2 S-b
    }

    // §2.10: the plan's marks on a place grid (nothing when no plan is set)
    public static void MarkPlaceGrid(World server, Entity player, PlaceGridInput g, ObjectID item)
    {
        var s = S;
        if (!s.planSet) return;
        int n = g.n;
        bool In(int2 t) => t.x >= g.loX && t.y >= g.loY && t.x < g.loX + n && t.y < g.loY + n;
        int Idx(int2 t) => (t.x - g.loX) * n + (t.y - g.loY);
        g.edgeGap = new int[n * n]; g.edgeDoorSpot = new bool[n * n]; g.edgeDoorSide = new bool[n * n]; g.edgeInside = new bool[n * n];
        var tr = new AITileReader(server);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        for (int gi = 0; gi < s.gaps.Count; gi++)
        {
            int open = 0;
            foreach (var t in s.gaps[gi])
            {
                if (!tr.HasWall(t, out _, out _, out _) && !s.doors.ContainsKey(t) && !blocked.Contains(t)) open++;
                if (In(t)) g.edgeGap[Idx(t)] = gi + 1;
            }
            g.gapOpen[gi + 1] = open;
        }
        if (s.door.HasValue && !s.doors.ContainsKey(s.door.Value) && In(s.door.Value)) { g.edgeDoorSpot[Idx(s.door.Value)] = true; g.edgeGap[Idx(s.door.Value)] = 0; }
        if (s.door.HasValue)
            foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
            {
                int2 t = s.door.Value + d;
                if (In(t) && IsDoorSideTile(t, server)) g.edgeDoorSide[Idx(t)] = true;
            }
        foreach (var t in s.inside) if (In(t)) g.edgeInside[Idx(t)] = true;
        g.itemIsDoor = IsDoorItem(server, item);
        var dn = EdgeDoorNow(server);
        g.edgeDoorNow = dn.HasValue ? $"({dn.Value.x},{dn.Value.y})" : null;
    }
}
