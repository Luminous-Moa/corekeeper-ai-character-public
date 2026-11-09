// AIEdge — the base edge in the game (코어-안전지대 단계 2; the logic is Core/AIBaseEdge.cs, Core/AIDoorPass.cs):
// - Options: the window around the base as an EdgeGrid (known/walkable/objects/water/spawn pad), AIBaseEdgeCore.Options,
//   with the bag's blocks in the facts. Chosen with the test command "/ai edge pick N [door]" until the upper LLM can
//   choose (단계 4).
// - Plan and memory: the chosen option's gaps, door tile and inside; placed edge blocks and doors. Kept per world and
//   character in memory.json with the base (AIMemory.Data edge*), the reference for 단계 3 (broken parts).
// - Actions: EdgeFillSkill (one batch of at most 7 blocks along a gap; the closing tile from inside), placing the door
//   and torches (PlaceSkill on a tile with a stand), DoorPassSkill (open -> through -> close), DoorCloseSkill (an own
//   door left open). Offered to the action model while a plan is set and a goal runs; the test commands start them.
// - The AI's walk treats an own closed door as a tile it opens and passes (AINavigator), see DoorStep.
using System;
using System.Collections.Generic;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static partial class AIEdge
{
    public const int Window = 30;   // tiles around the base looked at for the edge

    private sealed class State
    {
        public List<EdgeOption> lastOptions = new List<EdgeOption>();
        public EdgeGrid lastGrid;
        public int gridFrame = -1; public int2 gridSeed; public EdgeGrid grid; public int otherOpenDoors;   // review G9: one grid per frame
        // review G9: the options are computed off the game thread (the grid is read on it), picked up by Frame
        public System.Threading.Tasks.Task<EdgeOptions> pending; public EdgeGrid pendingGrid; public long pendingGridMs;
        public System.Diagnostics.Stopwatch pendingWatch; public bool pendingReply;
        public string lastKind = "options";   // the last listing: options (nested sizes) or around (holds given things)
        public List<MapObject> objects = new List<MapObject>();
        // the plan (chosen option)
        public bool planSet; public int2 seed;
        public readonly List<List<int2>> gaps = new List<List<int2>>();
        public int2? door;
        public readonly HashSet<int2> inside = new HashSet<int2>();
        // what was placed for the edge
        public readonly Dictionary<int2, ObjectID> walls = new Dictionary<int2, ObjectID>();
        public readonly Dictionary<int2, ObjectID> doors = new Dictionary<int2, ObjectID>();
        public readonly Dictionary<int2, ObjectID> lights = new Dictionary<int2, ObjectID>();
        // 단계 3 (AIEdge.Reach.cs): the walk grid around the AI and its reach map (kept 0.4 s on the same tile); the
        // enemies judged unable to come at the last look (close-fight set)
        public ReachMap reach; public int2 reachAt; public float reachTime = -1f;
        public readonly AIThreatHistory<Entity> threatHistory = new AIThreatHistory<Entity>();
        // 검수 F2 (HEAD decision): each edge tile as the AI saw it at its last look (out of view it is that)
        public readonly EdgeLastSeen edgeSeen = new EdgeLastSeen();
        public float breachLogAt = -1f; public string breachLog = "";
        public float breachCheckAt = -1f; public readonly AIBaseGuard.BreachWatch breachWatch = new AIBaseGuard.BreachWatch();
        public int planVersion;   // review S4: changes with every plan (Pick, ClearMemory / Import)
        public bool? doorSidesEW; public BaseShape shape; public int shapeFrame = -1;   // recheck R2: the door's side-wall axis; the shape once per frame
        // live-run fix 3: the own door the AI went into, owed a close (the closing tail at a skill's end)
        public readonly DoorOwe owe = new DoorOwe(); public float oweCheckAt = -1f; public int oweUse;
        // user decision B: doors the AI remembers placing ("my doors"; the list of 제작대-놓기 comes later, RememberedDoor)
        public readonly HashSet<int2> myDoors = new HashSet<int2>();
        public float myDoorLookAt = -1f;   // 내문-판정: the last look at my doors (LookMyDoors, every 0.5 s)
        public readonly AIBaseGuard.MyDoorLook myDoorLook = new AIBaseGuard.MyDoorLook();   // 32-검수 M1: the looks that saw no door
        public int2 offDoor; public bool offDoorSet;   // 33-재검수 N3: the last tile the AI stood on off its doors (TrackDoor)
    }
    private static State S => AIAgent.Cur.Get<State>(AISlot.BaseEdge);

    public static bool PlanActive => S.planSet;
    public static bool IsInside(int2 t) => S.inside.Contains(t);
    public static IEnumerable<int2> OwnDoorTiles => S.doors.Keys;
    public static bool IsOwnDoorTile(int2 t) => S.doors.ContainsKey(t);
    // user decision B: the 제작대-놓기 list of doors the AI placed plugs in here (null until then); the test command
    // "/ai edge mydoor x z" marks one for this session
    public static Func<int2, bool> RememberedDoor;
    public static bool IsMyDoorTile(int2 t) => !S.doors.ContainsKey(t) && (S.myDoors.Contains(t) || RememberedDoor != null && RememberedDoor(t));

    // ------------------------------------------------------------------ grid and options

    // Wall-tile items in the bag (ObjectInfo.tileType wall: dirt/stone/wood wall blocks ...), with counts
    // Review G11: the block for a tile (on ground first one that cannot go on water), or None
    public static ObjectID BlockFor(World server, Entity player, bool waterTile)
    {
        var blocks = WallBlocks(server, player);
        int i = AIBaseEdgeCore.PickBlock(blocks.ConvertAll(b => (b.n, b.water)), waterTile);
        return i < 0 ? ObjectID.None : blocks[i].id;
    }

    public static List<(ObjectID id, int n, bool water)> WallBlocks(World server, Entity player)
    {
        var res = new List<(ObjectID, int, bool)>();
        foreach (var kv in AIBody.InventoryCounts(server, player))
        {
            if (kv.Value <= 0) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(kv.Key, 0);
            if (info == null || info.tileType != TileType.wall) continue;
            res.Add((kv.Key, kv.Value, AIPlaceSpot.CanGoOnWater(kv.Key)));
        }
        res.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return res;
    }

    // Objects near the base (단계 2, for the map tool of 단계 4 and the "left outside" facts): name, kind, the tiles they
    // occupy (position + prefab corner offset and size); creatures, players, items on the ground and tiles are not objects.
    public static List<MapObject> ObjectsInWindow(World server, int2 seed)
    {
        var res = new List<MapObject>();
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (AIWorldFacts.IsExcludedPlacedEntity(em, e)) continue;
                int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                if (math.abs(t.x - seed.x) > Window || math.abs(t.y - seed.y) > Window) continue;
                var od = em.GetComponentData<ObjectDataCD>(e);
                ObjectInfo info = PugDatabase.GetObjectInfo(od.objectID, od.variation) ?? PugDatabase.GetObjectInfo(od.objectID, 0);
                // live run: boss statues are not PlaceablePrefab: an object with a collider counts too
                if (info == null || info.objectType != ObjectType.PlaceablePrefab && od.objectID != ObjectID.TheCore && !em.HasComponent<DoorCD>(e) && !em.HasComponent<GateCD>(e) && !em.HasComponent<Unity.Physics.PhysicsCollider>(e)) continue;
                var mo = new MapObject { name = od.objectID.ToString(), kind = KindOf(server, e, od.objectID) };
                int2 size = new int2(math.max(1, info.prefabTileSize.x), math.max(1, info.prefabTileSize.y));
                int2 corner = new int2(info.prefabCornerOffset.x, info.prefabCornerOffset.y);
                for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) mo.tiles.Add((t.x + corner.x + x, t.y + corner.y + y));
                res.Add(mo);
            }
        // the spawn pad and the respawn point (measured, 5-단계1-측정.md M1) when the core is in the window
        if (math.abs(seed.x) <= Window && math.abs(seed.y) <= Window)
        {
            var pad = new MapObject { name = "스폰 받침(3x3)", kind = "spawnpad" };
            for (int x = -AIPlaceGrid.SpawnPadHalf; x <= AIPlaceGrid.SpawnPadHalf; x++) for (int y = -AIPlaceGrid.SpawnPadHalf; y <= AIPlaceGrid.SpawnPadHalf; y++) pad.tiles.Add((x, y));
            res.Add(pad);
            var rs = new MapObject { name = "부활 자리(0,0)", kind = "respawn" }; rs.tiles.Add((0, 0)); res.Add(rs);
        }
        return res;
    }

    private static string KindOf(World server, Entity e, ObjectID id)
    {
        var em = server.EntityManager;
        string n = id.ToString();
        if (id == ObjectID.TheCore) return "core";
        if (em.HasComponent<DoorCD>(e) || em.HasComponent<GateCD>(e)) return S.doors.ContainsKey(AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position)) ? "owndoor" : "door";
        if (AIBase.IsChestItem(server, id) || n.Contains("Chest")) return "chest";
        if (AICook.IsPotItem(server, id)) return "pot";
        if (n.Contains("Statue") || n.Contains("Altar") || n.Contains("Shrine")) return "statue";
        if (n.Contains("Torch") || n.Contains("Lamp") || n.Contains("Lantern")) return "light";
        if (n.Contains("WorkBench") || n.Contains("Workbench") || n.Contains("Furnace") || n.Contains("Anvil") || n.Contains("Table") || n.Contains("Station") || n.Contains("Cooking")) return "station";
        return "object";
    }

    // the kinds whose side of the edge the options tell
    private static bool Landmark(string kind) => kind == "core" || kind == "spawnpad" || kind == "respawn" || kind == "station" || kind == "chest" || kind == "pot" || kind == "statue";

    public static EdgeGrid BuildGrid(World server, Entity player, int2 seed)
    {
        var s = S;
        if (s.grid != null && s.gridFrame == Time.frameCount && math.all(s.gridSeed == seed)) return s.grid;
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        int n = 2 * Window + 1;
        var g = new EdgeGrid { n = n, loX = seed.x - Window, loY = seed.y - Window, pass = new bool[n * n], water = new bool[n * n], known = new bool[n * n], noPlace = new bool[n * n], wall = new bool[n * n], myDoor = new bool[n * n], seedX = seed.x, seedY = seed.y, windowRange = Window };
        // review G13: another player's open door lets enemies through: a walkable tile for the edge
        var openDoors = AIDoorFacts.OpenTilesExcept(server, seed, Window, t => S.doors.ContainsKey(t));
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int2 t = new int2(g.loX + x, g.loY + y);
                int i = x * n + y;
                bool known = AIWorldFacts.IsKnown(me, t);
                g.known[i] = known;
                if (!known) continue;
                bool water = AIFarm.IsWater(tr, t);
                g.water[i] = water && !AIPlaceSpot.IsLava(tr, t) && !blocked.Contains(t);
                // user decision B: a door the AI remembers placing (not yet edge memory) is a way for a new plan's cut (taken as its door)
                g.pass[i] = !water && tr.Walkable(t) && (!blocked.Contains(t) || openDoors.Contains(t) || IsMyDoorTile(t)) && !S.doors.ContainsKey(t);
                g.myDoor[i] = g.pass[i] && IsMyDoorTile(t);   // 재실측: a cut through my door needs no block
                g.noPlace[i] = AIPlaceGrid.SpawnPad(t.x, t.y) || AIPlaceSpot.IsImmune(tr, t);
                g.wall[i] = !g.pass[i] && !g.water[i] && DoorWall(tr, t);   // review F5: the same reading as SidesReady
            }
        s.objects = ObjectsInWindow(server, seed);
        foreach (var mo in s.objects) if (Landmark(mo.kind)) g.landmarks.Add((mo.name, mo.tiles));
        s.grid = g; s.gridFrame = Time.frameCount; s.gridSeed = seed; s.otherOpenDoors = openDoors.Count;
        return g;
    }

    // Starts the options of the base edge: the grid now (game thread), the cuts on a worker thread; Frame lists them.
    public static bool RequestOptions(World server, Entity player, bool reply)
    {
        var s = S;
        if (s.pending != null) return false;
        int2 seed = AIBase.BaseTile;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = BuildGrid(server, player, seed);
        s.pendingGridMs = sw.ElapsedMilliseconds;
        s.pendingGrid = g; s.pendingWatch = sw; s.pendingReply = reply;
        s.pending = System.Threading.Tasks.Task.Run(() => AIBaseEdgeCore.Options(g));
        return true;
    }

    // once per character tick (AIBrain.Tick): a finished options computation is listed
    public static void Frame(World server, Entity player)
    {
        var s = S;
        if (s.pending == null || !s.pending.IsCompleted) return;
        var task = s.pending; s.pending = null;
        if (task.IsFaulted) { AIPlayerSpikeMod.LogError("edge: options failed", task.Exception); return; }
        long totalMs = s.pendingWatch.ElapsedMilliseconds;
        List(server, player, task.Result, s.pendingGrid, s.pendingGridMs, totalMs - s.pendingGridMs, out string text);
        if (s.pendingReply) foreach (var line in text.Split('\n')) AIPlayerSpikeMod.Reply("[시험용] " + line);
        s.lastKind = "options";
    }

    // The edge that holds given things (단계 2, user decision: what the base holds is the model's call). spec words:
    // "core" (TheCore, the spawn pad), an object name (every one in the window), "x,y" a tile, "rect:x1,y1,x2,y2" an area.
    public static string Around(World server, Entity player, string[] spec)
    {
        var s = S;
        int2 seed = AIBase.BaseTile;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = BuildGrid(server, player, seed);
        var hold = new List<(int x, int y)>(); var named = new List<string>();
        void AddObj(MapObject mo)
        {
            named.Add(mo.name);
            var own = new HashSet<(int, int)>(mo.tiles);
            foreach (var t in mo.tiles)
            {
                hold.Add(t);
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { var u = (t.x + d.Item1, t.y + d.Item2); if (!own.Contains(u)) hold.Add(u); }
            }
        }
        foreach (var w in spec)
        {
            if (w == "core") { foreach (var mo in s.objects) if (mo.kind == "core" || mo.kind == "spawnpad") AddObj(mo); continue; }
            if (w.StartsWith("rect:"))
            {
                var a = w.Substring(5).Split(',');
                if (a.Length == 4 && int.TryParse(a[0], out int x1) && int.TryParse(a[1], out int y1) && int.TryParse(a[2], out int x2) && int.TryParse(a[3], out int y2))
                { for (int x = math.min(x1, x2); x <= math.max(x1, x2); x++) for (int y = math.min(y1, y2); y <= math.max(y1, y2); y++) hold.Add((x, y)); named.Add($"영역 ({x1},{y1})~({x2},{y2})"); }
                continue;
            }
            var xy = w.Split(',');
            if (xy.Length == 2 && int.TryParse(xy[0], out int tx) && int.TryParse(xy[1], out int ty)) { hold.Add((tx, ty)); named.Add($"칸 ({tx},{ty})"); continue; }
            int found = 0;
            foreach (var mo in s.objects) if (mo.name.Equals(w, StringComparison.OrdinalIgnoreCase)) { AddObj(mo); found++; }
            if (found == 0) return $"품을 것 '{w}'을(를) 기지 둘레 {Window}칸 안에서 찾지 못함";
        }
        if (hold.Count == 0) return "품을 것을 주지 않음(core | 물체 이름 | x,y | rect:x1,y1,x2,y2)";
        var res = AIBaseEdgeCore.Around(g, hold);
        long ms = sw.ElapsedMilliseconds;
        List(server, player, res, g, 0, ms, out string text);
        s.lastKind = "around";
        string head = $"품을 것 {string.Join(", ", named)} → 둘레 후보 {res.list.Count}개 ({ms} ms)";
        AIPlayerSpikeMod.Log("EDGE around: " + head);
        return head + (res.list.Count > 0 ? "\n" + text : "");
    }

    // ---------------------------------------------------------------- the map for the 단계 4 map tool

    // the base map and the plans as data: written to the world's AI folder (basemap.json) and returned
    public static string WriteMap(World server, Entity player)
    {
        var s = S;
        int2 seed = s.planSet ? s.seed : AIBase.BaseTile;
        var g = BuildGrid(server, player, seed);
        var plans = new List<EdgePlanData>();
        var lights = new List<(int x, int y)>(); foreach (var l in s.lights.Keys) lights.Add((l.x, l.y));
        if (s.lastGrid != null && s.lastGrid.seedX == g.seedX && s.lastGrid.seedY == g.seedY)
            for (int i = 0; i < s.lastOptions.Count; i++)
                plans.Add(AIBaseMap.Plan(g, s.lastOptions[i], null, "preview", $"[{i + 1}] ({s.lastKind}) {s.lastOptions[i].Facts(0, 0, Window)}", lights));
        if (s.planSet)
        {
            var o = new EdgeOption();
            foreach (var t in s.inside) o.inside.Add(EdgeGrid.Key(t.x, t.y));
            foreach (var gp in s.gaps) { var eg = new EdgeGap(); foreach (var t in gp) eg.tiles.Add((t.x, t.y)); o.gaps.Add(eg); }
            AIBaseEdgeCore.Annotate(g, o);
            (int, int)? door = s.door.HasValue ? (s.door.Value.x, s.door.Value.y) : ((int, int)?)null;
            var cp = AIBaseMap.Plan(g, o, door, "confirmed", $"고른 둘레: 틈 {o.gaps.Count}곳·막을 칸 {o.Blocks}칸·안쪽 {o.inside.Count}칸; 놓은 벽 {s.walls.Count}·문 {s.doors.Count}·횃불 {s.lights.Count}", lights);
            plans.Add(cp);
        }
        string json = AIBaseMap.Json(g, s.objects, plans, AIPlayerSpikeMod.WorldId, AIAgent.Cur.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        string path = System.IO.Path.Combine(AIMemory.CharDir(AIPlayerSpikeMod.WorldId), "basemap.json");
        System.IO.File.WriteAllText(path, json);
        AIPlayerSpikeMod.Log($"EDGE map: {path} ({json.Length} chars, {s.objects.Count} objects, {plans.Count} plans: {string.Join(", ", plans.ConvertAll(x => x.state))})");
        return path;
    }

    private static List<EdgeOption> List(World server, Entity player, EdgeOptions res, EdgeGrid g, long gridMs, long optMs, out string text)
    {
        int2 seed = new int2(g.seedX, g.seedY);
        var opts = res.list;
        S.lastOptions = opts; S.lastGrid = g;
        var blocks = WallBlocks(server, player);
        int have = 0, haveW = 0; foreach (var b in blocks) { have += b.n; if (b.water) haveW += b.n; }
        var sb = new StringBuilder();
        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            var ad = AIBaseEdgeCore.AdoptDoors(o, t => IsMyDoorTile(new int2(t.x, t.y)));   // user decision B
            string adText = ad.Count == 0 ? "" : "; 이미 놓은 내 문 " + string.Join(" ", ad.ConvertAll(a => $"({a.tile.x},{a.tile.y})")) + "은 둘레 문으로 받아들임(그 칸은 막지 않음)";
            sb.Append($"[{i + 1}] {o.Facts(have, haveW, Window)}{(o.gaps.Count > 0 ? "; 틈: " + string.Join(" / ", o.gaps.ConvertAll(gp => $"{gp.tiles.Count}칸 ({gp.tiles[0].x},{gp.tiles[0].y})~({gp.tiles[gp.tiles.Count - 1].x},{gp.tiles[gp.tiles.Count - 1].y})")) : "")}{adText}\n");
        }
        if (res.largerLeftOut) sb.Append($"(더 큰 둘레는 블록 {AIBaseEdgeCore.MaxBlocksOffered}개를 넘어 뺐음)\n");
        if (S.otherOpenDoors > 0) sb.Append($"(열린 남의 문 {S.otherOpenDoors}개는 지나갈 수 있는 칸으로 셈 — 적도 지나감)\n");
        text = opts.Count == 0 ? $"기지 {AIBase.BaseText} 둘레: 막을 수 있는 둘레 후보 없음(창 {Window}칸, 블록 {AIBaseEdgeCore.MaxBlocksOffered}개 넘거나 받침을 지나야 함)" : sb.ToString().TrimEnd();
        AIPlayerSpikeMod.Log($"EDGE options: base ({seed.x},{seed.y}), {opts.Count}; grid {gridMs} ms on the game thread, {res.cutsComputed} cuts done {optMs} ms later on a worker thread: {text.Replace("\n", " | ")}");
        return opts;
    }

    // choose option k (1-based) of the last listing; door = gap index (1-based) whose middle tile is the door, 0 none
    public static string Pick(int k, int doorGap, World server = null, Entity player = default)
    {
        var s = S;
        if (k < 1 || k > s.lastOptions.Count) return $"둘레 후보 {k}번이 없음(먼저 /ai edge)";
        var o = s.lastOptions[k - 1];
        s.planSet = true; s.seed = new int2(s.lastGrid.seedX, s.lastGrid.seedY);
        s.gaps.Clear(); s.inside.Clear(); s.door = null; s.doorSidesEW = null;
        foreach (var gp in o.gaps) s.gaps.Add(gp.tiles.ConvertAll(t => new int2(t.x, t.y)));
        foreach (var key in o.inside) s.inside.Add(new int2((int)(key >> 32), (int)(uint)key));
        string adoptNote = RepickOldDoors(o, server, player);   // 내문-판정 결함 3: before the adoption below (it takes my doors on gap tiles)
        // user decision B: a door the AI remembers placing on a gap tile is the edge's door (no second door, no wall there)
        var adopted = AIBaseEdgeCore.AdoptDoors(o, t => IsMyDoorTile(new int2(t.x, t.y)) && (server == null || DoorAt(server, new int2(t.x, t.y), float3.zero) != null));
        foreach (var (gi, t) in adopted)
        {
            int2 at = new int2(t.x, t.y);
            var dv = server != null ? DoorAt(server, at, float3.zero) : null;
            s.doors[at] = dv != null ? dv.id : ObjectID.WoodDoor;
            if (!s.door.HasValue || gi == doorGap - 1) { s.door = at; s.doorSidesEW = dv != null ? !AIDoorPass.PassageEastWest(dv.variation) : (bool?)null; }
            adoptNote += $", 틈 {gi + 1}의 ({t.x},{t.y})는 이미 놓은 내 문 — 둘레 문으로 받아들임(그 칸에 문·벽을 더 놓지 않음)";
        }
        // review S7: a chosen door gap that is not where the adopted door is (it is not ignored without a word)
        adoptNote += AIBaseEdgeCore.AdoptGapNote(adopted, doorGap, s.door.HasValue ? (s.door.Value.x, s.door.Value.y) : ((int, int)?)null);
        if (!s.door.HasValue && doorGap >= 1 && doorGap <= o.gaps.Count) { var d = AIBaseEdgeCore.DoorTileOf(s.lastGrid, o.gaps[doorGap - 1]); s.door = new int2(d.x, d.y); s.doorSidesEW = AIBaseEdgeCore.DoorSides(s.lastGrid, o.gaps[doorGap - 1], d).eastWest; }
        s.planVersion++;   // review S4: the breach count starts again with a new plan
        s.breachWatch.pending = "";   // review S5: the old plan's breach reason is no fact of this one
        AIMemory.MarkDirty(); AIMemory.Save(true);
        string r = $"{(s.lastKind == "around" ? "품을 것 둘레" : "둘레")} {k}번을 고름: 틈 {o.gaps.Count}곳·막을 칸 {o.Blocks}칸·안쪽 {o.inside.Count}칸{(s.door.HasValue ? $", 문 자리 ({s.door.Value.x},{s.door.Value.y})" : ", 문 자리 없음")}{adoptNote}";
        AIPlayerSpikeMod.Log("EDGE plan: " + r);
        return r;
    }

    // 내문-판정 결함 3 (HEAD decision): the old plan's edge doors off the new plan's gap tiles leave the edge doors; one
    // still standing as the AI knows it (in view: a door there now; out of view: unless its tile was seen open at the last
    // look — AIBaseGuard.OwnDoorStands) becomes my door, one gone is forgotten (AIBaseEdgeCore.RepickDoors). An old edge
    // door on a new gap tile stays an edge door. Returns the plan sentence's part ("" none).
    private static string RepickOldDoors(EdgeOption o, World server, Entity player)
    {
        var s = S;
        if (s.doors.Count == 0) return "";
        bool look = server != null && player != Entity.Null && server.EntityManager.Exists(player);
        float3 me = look ? AIBody.Pos(server, player) : float3.zero;
        var old = new List<(int x, int y)>(); foreach (var t in s.doors.Keys) old.Add((t.x, t.y));
        AIBaseEdgeCore.RepickDoors(old, o, t =>
        {
            var v = new int2(t.x, t.y);
            bool inView = look && AIVision.CanSee(me, (float2)v);
            return AIBaseGuard.OwnDoorStands(inView, inView && DoorAt(server, v, me) != null, s.edgeSeen.Last(t.x, t.y));
        }, out _, out var toMine, out var drop);
        string note = "";
        foreach (var t in toMine)
        {
            var v = new int2(t.x, t.y);
            s.doors.Remove(v); s.myDoors.Add(v); s.edgeSeen.Forget(t.x, t.y);   // 33-재검수 N2: it stands (as known): no old "seen open" on it
            note += $", 옛 둘레 문 ({t.x},{t.y})은 새 둘레의 틈 위가 아니라 둘레 문에서 빼고 내 문으로 둠(지나가기·닫기는 그대로, 보수·부서짐 확인은 안 함)";
        }
        foreach (var t in drop)
        {
            s.doors.Remove(new int2(t.x, t.y));
            ForgetMyDoor(new int2(t.x, t.y), "옛 둘레 문, 없어진 것으로 봄(둘레를 다시 고름)");   // 32-검수 L2: an adopted my door is in both lists
            note +=$", 옛 둘레 문 ({t.x},{t.y})은 새 둘레의 틈 위가 아니고 없어진 것으로 보여 잊음";
        }
        if (toMine.Count + drop.Count > 0) AIPlayerSpikeMod.Log($"EDGE memory: re-picked edge: old edge doors to my doors {string.Join(" ", toMine.ConvertAll(t => $"({t.x},{t.y})"))}, forgotten {string.Join(" ", drop.ConvertAll(t => $"({t.x},{t.y})"))} (edge doors {s.doors.Count}, my doors {s.myDoors.Count})");
        return note;
    }

    // the plan's remaining batches (tiles still open), recomputed from the plan and the ground now
    public static List<EdgeBatch> Batches(World server, Entity player)
    {
        var s = S;
        var res = new List<EdgeBatch>();
        if (!s.planSet) return res;
        var tr = new AITileReader(server);
        var g = BuildGrid(server, player, s.seed);
        var o = new EdgeOption();
        foreach (var t in s.inside) o.inside.Add(EdgeGrid.Key(t.x, t.y));
        for (int gi = 0; gi < s.gaps.Count; gi++)
        {
            var gap = new EdgeGap();
            // 단계 3: a placed wall that is gone again is a repair (GuardCandidates), not filling
            // (user decision B: any own door on a gap tile — an adopted one too — is no tile to fill)
            foreach (var t in s.gaps[gi]) if (!tr.HasWall(t, out _, out _, out _) && !s.doors.ContainsKey(t) && !s.walls.ContainsKey(t)) gap.tiles.Add((t.x, t.y));
            o.gaps.Add(gap);
        }
        (int, int)? door = s.door.HasValue ? (s.door.Value.x, s.door.Value.y) : ((int, int)?)null;
        // recheck R2: before the door is placed, its side-wall axis is the one DoorSides counts now
        if (door.HasValue && !s.doors.ContainsKey(s.door.Value)) { var dg = o.gaps.Find(gp => gp.tiles.Contains(door.Value)); if (dg != null) s.doorSidesEW = AIBaseEdgeCore.DoorSides(g, dg, door.Value).eastWest; }
        // review F5: the door spot's missing side walls off the plan are filled in these batches (a placed wall gone
        // there is a repair instead)
        foreach (var b in AIBaseEdgeCore.Batches(g, o, door, t => RepairCovers(new int2(t.x, t.y), server))) if (b.tiles.Count > 0) res.Add(b);
        return res;
    }

    // Review G2/G3: is t the last open tile of plan gap gi right now (others walled; the gap's door placed)?
    // exceptDoor (review F3, the outside option of a door gap's last batch): the door spot not hung yet does not count
    // (it stays open as the way in); t must be one of the gap's other tiles
    public static bool IsLastOpen(World server, Entity player, int gi, int2 t, bool exceptDoor = false)
    {
        var s = S;
        if (gi < 0 || gi >= s.gaps.Count) return false;
        var tr = new AITileReader(server);
        HashSet<int2> blocked;
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        var tiles = s.gaps[gi].ConvertAll(u => (u.x, u.y));
        if (exceptDoor && s.door.HasValue && !s.doors.ContainsKey(s.door.Value) && tiles.Remove((s.door.Value.x, s.door.Value.y)) && !tiles.Contains((t.x, t.y))) return false;
        // recheck N3: an object on a gap tile (a chest, a station) closes it too
        return AIBaseEdgeCore.IsLastOpen(tiles, (t.x, t.y), u => { var v = new int2(u.x, u.y); return !tr.HasWall(v, out _, out _, out _) && !s.doors.ContainsKey(v) && !blocked.Contains(v); });
    }

    // ------------------------------------------------------------------ memory (memory.json via AIMemory)

    public static void NotePlaced(World server, ObjectID item, int2 tile)
    {
        var s = S;
        ObjectInfo info = PugDatabase.GetObjectInfo(item, 0);
        // 단계 3: a repaired natural edge tile is part of the edge memory too
        bool plan = s.planSet && (s.inside.Contains(tile) || IsEdgeTile(tile));
        // 제작대-놓기 검수 S4: a wall or a door is edge memory on an edge tile (a gap, the door spot, a remembered edge
        // part) or beside the door spot, not on any inside tile (place goals put them anywhere; an inside door is my
        // door, an inside wall is no edge part). Torches keep the inside rule (the edge's torches are inside).
        bool edgeT = s.planSet && (IsEdgeTile(tile) || s.door.HasValue && math.all(s.door.Value == tile));
        // review F5 follow-up: a wall beside the door spot (off the cut) is edge memory too, so a broken one is repaired
        if (info != null && info.tileType == TileType.wall && (edgeT || IsDoorSideTile(tile, server))) s.walls[tile] = item;
        else if (IsDoorItem(server, item) && edgeT) s.doors[tile] = item;
        else if (item.ToString().Contains("Torch") && plan) s.lights[tile] = item;
        else if (NoteMyDoor(server, item, tile)) return;   // 제작대-놓기 §6: a door off the plan is my door
        else return;
        AIPlayerSpikeMod.Log($"EDGE memory: {item} at ({tile.x},{tile.y}) (walls {s.walls.Count}, doors {s.doors.Count}, lights {s.lights.Count})");
        AIMemory.MarkDirty();
    }

    public static bool IsDoorItem(World server, ObjectID item)
    {
        var em = server.EntityManager;
        Entity prefab = AIGameData.PrimaryPrefab(em, item, 0);
        return prefab != Entity.Null && em.Exists(prefab) && (em.HasComponent<DoorCD>(prefab) || em.HasComponent<GateCD>(prefab));
    }

    public static void ClearMemory() { var s = S; s.planVersion++; s.breachWatch.pending = ""; s.doorSidesEW = null; s.owe.door = null; s.owe.tail = false; s.planSet = false; s.gaps.Clear(); s.inside.Clear(); s.door = null; s.walls.Clear(); s.doors.Clear(); s.lights.Clear(); s.edgeSeen.Clear(); s.reach = null; }

    public static void Import(AIMemory.Data d)
    {
        var s = S;
        ClearMemory();
        s.planSet = d.edgeSet != 0;
        if (d.edgeSeed != null && d.edgeSeed.Length == 2) s.seed = new int2(d.edgeSeed[0], d.edgeSeed[1]);
        for (int i = 0; d.edgeIn != null && i + 1 < d.edgeIn.Length; i += 2) s.inside.Add(new int2(d.edgeIn[i], d.edgeIn[i + 1]));
        for (int i = 0; d.edgePlan != null && i + 2 < d.edgePlan.Length; i += 3)
        {
            int gi = d.edgePlan[i];
            while (s.gaps.Count <= gi) s.gaps.Add(new List<int2>());
            s.gaps[gi].Add(new int2(d.edgePlan[i + 1], d.edgePlan[i + 2]));
        }
        if (d.edgeDoorAt != null && d.edgeDoorAt.Length == 2) s.door = new int2(d.edgeDoorAt[0], d.edgeDoorAt[1]);
        void Read(int[] a, Dictionary<int2, ObjectID> to) { for (int i = 0; a != null && i + 2 < a.Length; i += 3) to[new int2(a[i + 1], a[i + 2])] = (ObjectID)a[i]; }
        Read(d.edgeWalls, s.walls); Read(d.edgeDoors, s.doors); Read(d.edgeLights, s.lights);
        s.edgeSeen.Load(d.edgeSeen);   // 재검수 R3: the last look of each edge tile survives a restart
        ImportMyDoors(d);   // 제작대-놓기 §6
    }

    public static void Export(AIMemory.Data d)
    {
        var s = S;
        d.edgeSet = s.planSet ? 1 : 0;
        d.edgeSeed = new[] { s.seed.x, s.seed.y };
        var ins = new List<int>(); foreach (var t in s.inside) { ins.Add(t.x); ins.Add(t.y); }
        d.edgeIn = ins.ToArray();
        var pl = new List<int>(); for (int gi = 0; gi < s.gaps.Count; gi++) foreach (var t in s.gaps[gi]) { pl.Add(gi); pl.Add(t.x); pl.Add(t.y); }
        d.edgePlan = pl.ToArray();
        d.edgeDoorAt = s.door.HasValue ? new[] { s.door.Value.x, s.door.Value.y } : new int[0];
        int[] Write(Dictionary<int2, ObjectID> m) { var l = new List<int>(); foreach (var kv in m) { l.Add((int)kv.Value); l.Add(kv.Key.x); l.Add(kv.Key.y); } return l.ToArray(); }
        d.edgeWalls = Write(s.walls); d.edgeDoors = Write(s.doors); d.edgeLights = Write(s.lights);
        d.edgeSeen = s.edgeSeen.ToArray();
        ExportMyDoors(d);   // 제작대-놓기 §6
    }

    // ------------------------------------------------------------------ doors in the world

    public sealed class DoorView { public Entity e; public ObjectID id; public int2 tile; public int variation; public DoorKind kind; public float dist; }

    public static DoorView DoorAt(World server, int2 tile, float3 me)
    {
        var fact = AIDoorFacts.FirstAt(server, tile);
        if (!fact.HasValue) return null;
        var d = fact.Value;
        return new DoorView { e = d.Entity, id = d.Id, tile = d.Tile, variation = d.Variation, kind = d.Kind, dist = math.distance(me.xz, (float2)tile) };
    }

    // ------------------------------------------------------------------ options for the action model (plan set)

    // blocked (optional): what cannot be done now and why, for the input's blocked-target notes (review F3)
    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey, List<string> blocked = null)
    {
        var res = new List<AICandidate>();
        var s = S;
        if (!s.planSet) return res;
        float3 me = o.pos;
        int2 meT = StandTile(server, me);   // live-run fix 2: pressed against a shut door is not "on the door tile"
        bool meInside = s.inside.Contains(meT);
        var blocks = WallBlocks(server, player);
        int have = 0; foreach (var b in blocks) have += b.n;
        var trB = new AITileReader(server);
        List<(int x, int y)> openNow = null;   // review F3: the edge's open tiles now (for the outside options' facts), once
        // batches
        foreach (var b in Batches(server, player))
        {
            var first = b.tiles[0]; var last = b.tiles[b.tiles.Count - 1];
            string closing = b.closesGap ? $" 마지막 칸 ({last.x},{last.y})은 안쪽 ({b.stands[b.stands.Count - 1].x},{b.stands[b.stands.Count - 1].y})에서 놓아 이 틈을 닫음."
                : b.closesToDoor && s.door.HasValue ? $" 이 묶음 뒤에는 이 틈의 문 자리 ({s.door.Value.x},{s.door.Value.y})만 열려 있음(문을 달면 닫힘)." : " 이 묶음 뒤에도 틈이 남음.";
            // review F5: side walls of the door spot off the plan (the door goes in only between two walls)
            string sidesText = "";
            if (b.doorSides.Count > 0 && s.door.HasValue)
                sidesText = $" 그중 {string.Join(" ", b.doorSides.ConvertAll(t => $"({t.x},{t.y}){(s.inside.Contains(new int2(t.x, t.y)) ? "(기지 안쪽 칸)" : "")}"))}은 계획 밖 칸으로, 문 자리 ({s.door.Value.x},{s.door.Value.y}) 양옆 벽(문은 양옆이 벽일 때만 놓임, 규칙 door-between-walls).";
            res.Add(new AICandidate
            {
                kind = "edgefill", key = $"edgefill:{b.gap}:{first.x},{first.y}", tiles = b.tiles.ConvertAll(t => new int2(t.x, t.y)), edgeGap = b.gap, progress = false,
                desc = $"기지 둘레 틈 {b.gap + 1} 메우기 — 블록 {b.tiles.Count}개를 ({first.x},{first.y})부터 차례로 놓기. 가진 블록 {have}개{(have < b.tiles.Count ? $"(모자라면 놓은 데까지 하고 멈춤)" : "")}.{closing}{sidesText} 지금 AI는 {(meInside ? "안쪽" : "바깥쪽")}.",
            });
            // user decision A: the batch that closes the gap, closing it from outside (G2's "inside only" lifted)
            // review F3: also the last batch of a gap whose door spot is still open (closesToDoor: its last gap tile from
            // outside, the door spot stays the way in); the fact names the open places that stay (HEAD decision)
            var ct = b.closesGap ? last : b.closesToDoor && b.closeTile.HasValue ? b.closeTile.Value : last;
            var os = b.closesGap || b.closesToDoor && b.closeTile.HasValue ? OutsideStandOf(trB, ct) : null;
            if (os.HasValue)
            {
                if (openNow == null) openNow = OpenEdgeNow(server, player, null);
                var shut = new List<(int x, int y)>();
                if (b.gap >= 0 && b.gap < s.gaps.Count) foreach (var t in s.gaps[b.gap]) if (!(b.closesToDoor && s.door.HasValue && math.all(t == s.door.Value))) shut.Add((t.x, t.y));
                string fact = AIBaseGuard.OutsideCloseText(EdgeDoorNow(server), AIBaseGuard.OpenAfter(openNow, null, shut), os.Value);
                string what = b.closesGap ? "이 틈을 닫음" : $"이 틈의 벽을 다 채움(문 자리 ({s.door.Value.x},{s.door.Value.y})는 열린 채)";
                string lastWord = b.closesGap ? "마지막 칸" : "이 틈의 마지막 칸";
                res.Add(new AICandidate
                {
                    kind = "edgefill", key = $"edgefill:{b.gap}:{first.x},{first.y}:out", tiles = b.tiles.ConvertAll(t => new int2(t.x, t.y)), edgeGap = b.gap, progress = false,
                    desc = $"기지 둘레 틈 {b.gap + 1} 메우기(바깥에서 닫기) — 블록 {b.tiles.Count}개를 ({first.x},{first.y})부터 차례로 놓기. 가진 블록 {have}개{(have < b.tiles.Count ? $"(모자라면 놓은 데까지 하고 멈춤)" : "")}. {lastWord} ({ct.x},{ct.y})은 바깥 ({os.Value.x},{os.Value.y})에서 놓아 {what} — {fact}.{sidesText} 지금 AI는 {(meInside ? "안쪽" : "바깥쪽")}.",
                });
            }
        }
        // the door
        if (s.door.HasValue && !s.doors.ContainsKey(s.door.Value))
        {
            ObjectID doorItem = ObjectID.None; int haveDoor = 0;
            foreach (var kv in AIBody.InventoryCounts(server, player)) if (kv.Value > 0 && IsDoorItem(server, kv.Key)) { doorItem = kv.Key; haveDoor = kv.Value; break; }
            int2 dt = s.door.Value;
            var g = BuildGrid(server, player, s.seed);
            var opt = new EdgeOption(); foreach (var t in s.inside) opt.inside.Add(EdgeGrid.Key(t.x, t.y));
            var st = AIBaseEdgeCore.InsideStand(g, opt, (dt.x, dt.y));
            var ost = AIBaseEdgeCore.OutsideStand(g, opt, (dt.x, dt.y));   // user decision A: G3's "inside only" lifted
            // live run: the game takes a door only between two walls (E+W or N+S); before that the door cannot go in
            var trD = new AITileReader(server);
            bool sidesReady = SidesReady(trD, dt);
            // review F3: why the door cannot go in now goes to the action model (blocked-target notes), not only the log
            if (!sidesReady || doorItem == ObjectID.None || !st.HasValue && !ost.HasValue)
            {
                string why = !sidesReady ? DoorSidesText(server, player, dt) : "";
                if (doorItem == ObjectID.None) why = (why != "" ? why + "; " : "") + "가방에 문 없음(문을 만들거나 가져와야 함)";
                if (!st.HasValue && !ost.HasValue) why = (why != "" ? why + "; " : "") + "문 칸 옆에 설 칸이 없음";
                AIPlayerSpikeMod.Log($"EDGE door: ({dt.x},{dt.y}) not offered: {why}");
                blocked?.Add($"기지 문 달기 ({dt.x},{dt.y}): {why} — 문을 달 때까지 이 틈은 열려 있음");
            }
            if (doorItem != ObjectID.None && sidesReady)
            {
                if (st.HasValue)
                    res.Add(new AICandidate
                    {
                        kind = "place", edgeRole = EdgeCandidateRole.DoorPlace, key = $"place:{doorItem}@{dt.x},{dt.y}", itemId = doorItem, tile = dt, placeAt = true, placeStand = new int2(st.Value.x, st.Value.y), placeStandOnly = true, progress = false,
                        desc = $"기지 문 달기 — 가방의 {doorItem}를 문 자리 ({dt.x},{dt.y})에 안쪽 ({st.Value.x},{st.Value.y})에서 놓기(가진 문 {haveDoor}개). 놓으면 그 틈이 닫히고, 지나갈 때는 열고 닫아야 함.",
                    });
                if (ost.HasValue)
                {
                    if (openNow == null) openNow = OpenEdgeNow(server, player, null);
                    string fact = AIBaseGuard.OutsideCloseText((dt.x, dt.y), AIBaseGuard.OpenAfter(openNow, null, new[] { (dt.x, dt.y) }), ost.Value);   // review F3
                    res.Add(new AICandidate
                    {
                        kind = "place", edgeRole = EdgeCandidateRole.DoorPlace, key = $"place:{doorItem}@{dt.x},{dt.y}:out", itemId = doorItem, tile = dt, placeAt = true, placeStand = new int2(ost.Value.x, ost.Value.y), placeStandOnly = true, progress = false,
                        desc = $"기지 문 달기(바깥에서) — 가방의 {doorItem}를 문 자리 ({dt.x},{dt.y})에 바깥 ({ost.Value.x},{ost.Value.y})에서 놓기(가진 문 {haveDoor}개) — {fact}. 놓으면 그 틈이 닫히고, 지나갈 때는 열고 닫아야 함.",
                    });
                }
            }
        }
        // own doors near that stand open: close — review F1: built by AIBrain for every set (lDoor), not here
        // torches inside
        var torch = ObjectID.None;
        foreach (var kv in AIBody.InventoryCounts(server, player)) if (kv.Value > 0 && kv.Key.ToString().Contains("Torch")) { torch = kv.Key; break; }
        if (torch != ObjectID.None && meInside)
        {
            var all = AIPlaceSpot.AllOptions(server, player, torch, null, out _);
            var lights = new List<int2>(s.lights.Keys);
            all.RemoveAll(x => !s.inside.Contains(new int2(x.ax, x.ay)));
            float Far(PlaceOption x) { float m = 99f; foreach (var l in lights) m = math.min(m, math.distance((float2)l, new float2(x.ax, x.ay))); return m; }
            all.Sort((a, b) => Far(b).CompareTo(Far(a)));
            for (int i = 0; i < all.Count; i++)
            {
                var x = all[i]; float far = Far(x);
                res.Add(new AICandidate
                {
                    kind = "place", edgeRole = EdgeCandidateRole.TorchPlace, key = $"place:{torch}@{x.ax},{x.ay}", itemId = torch, tile = new int2(x.ax, x.ay), placeAt = true, placeStand = new int2(x.standX, x.standY), progress = false,
                    desc = $"기지 안 횃불 놓기 — 가방의 {torch}를 ({x.ax},{x.ay})에 놓기(설 칸 ({x.standX},{x.standY})). 기지 안쪽, {(lights.Count == 0 ? "아직 놓은 횃불 없음" : $"가장 가까운 놓은 횃불에서 {far:F0}칸")}. 그 자리: {x.Facts}.",
                });
            }
        }
        return res;
    }

    // ------------------------------------------------------------------ test command (behind testcmds.on)

    public static void Command(World server, Entity player, string[] parts)
    {
        string sub = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        float now = Time.realtimeSinceStartup;
        if (GuardCommand(server, player, sub, parts, now)) return;   // 단계 3: breaks | repair K | pickup x z [nx nz] | reach | guard
        int Arg(int i, int def) => parts.Length > i && int.TryParse(parts[i], out int v) ? v : def;
        switch (sub)
        {
            case "":
                AIPlayerSpikeMod.Reply(RequestOptions(server, player, true) ? "[시험용] 둘레 후보 계산 중" : "[시험용] 둘레 후보 계산이 이미 돌고 있음");
                break;
            case "around":
                foreach (var line in Around(server, player, parts.Length > 3 ? new List<string>(parts).GetRange(3, parts.Length - 3).ToArray() : new string[0]).Split('\n')) AIPlayerSpikeMod.Reply("[시험용] " + line);
                break;
            case "mydoor":
            {
                // TEST ONLY (user decision B): mark a door as one the AI remembers placing ("my door") for this session —
                // the stand-in for the 제작대-놓기 list (RememberedDoor); the next plan takes it as its door when it is on a gap tile
                int2 at = new int2(Arg(3, 0), Arg(4, 0));
                var dv = DoorAt(server, at, AIBody.Pos(server, player));
                if (dv == null) { AIPlayerSpikeMod.Reply($"[시험용] ({at.x},{at.y})에 문이 없음"); break; }
                S.myDoors.Add(at);
                AIPlayerSpikeMod.Log($"EDGE my door: ({at.x},{at.y}) {dv.id} marked as placed by the AI (test)");
                AIPlayerSpikeMod.Reply($"[시험용] ({at.x},{at.y}) 문을 '내 문'으로 표시(다음 둘레 계획의 틈 위에 있으면 둘레 문으로 받아들임)");
                break;
            }
            case "adopt":
            {
                // TEST ONLY (단계 2 live run): record an existing door / wall on a tile as an own edge part
                int2 at = new int2(Arg(3, 0), Arg(4, 0));
                var dv = DoorAt(server, at, AIBody.Pos(server, player));
                if (dv != null) S.doors[at] = dv.id;
                else if (new AITileReader(server).HasWall(at, out _, out _, out _)) S.walls[at] = ObjectID.WallDirtBlock;
                else { AIPlayerSpikeMod.Reply($"[시험용] ({at.x},{at.y})에 문·벽이 없음"); break; }
                AIMemory.MarkDirty(); AIMemory.Save(true);
                AIPlayerSpikeMod.Log($"EDGE adopt: ({at.x},{at.y}) {(dv != null ? dv.id.ToString() : "wall")} recorded as own (test)");
                AIPlayerSpikeMod.Reply($"[시험용] ({at.x},{at.y}) {(dv != null ? "문" : "벽")}을 둘레 기억에 넣음");
                break;
            }
            case "map":
                AIPlayerSpikeMod.Reply("[시험용] 기지 지도 자료: " + WriteMap(server, player));
                break;
            case "pick":
                AIPlayerSpikeMod.Reply("[시험용] " + Pick(Arg(3, 1), Arg(4, 0), server, player));
                try { WriteMap(server, player); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: map after pick failed", e); }
                break;
            case "batches":
            {
                var bs = Batches(server, player);
                AIPlayerSpikeMod.Reply($"[시험용] 남은 묶음 {bs.Count}: " + string.Join(" / ", bs.ConvertAll(b => $"틈{b.gap + 1} {b.tiles.Count}칸({b.tiles[0].x},{b.tiles[0].y}~){(b.closesGap ? " 닫음" : "")}")));
                AIPlayerSpikeMod.Log("EDGE batches: " + string.Join(" | ", bs.ConvertAll(b => $"gap {b.gap} tiles [{string.Join(" ", b.tiles.ConvertAll(t => $"({t.x},{t.y})"))}] stands [{string.Join(" ", b.stands.ConvertAll(t => $"({t.x},{t.y})"))}] closes={b.closesGap}")));
                break;
            }
            case "fill":
            {
                var bs = Batches(server, player);
                int k = Arg(3, 1);
                if (k < 1 || k > bs.Count) { AIPlayerSpikeMod.Reply($"[시험용] 묶음 {k}번이 없음(남은 {bs.Count})"); break; }
                AIBody.StartSkill(server, player, new EdgeFillSkill(bs[k - 1].gap, bs[k - 1].tiles.ConvertAll(t => new int2(t.x, t.y)), null, parts.Length > 4 && parts[4] == "out"), now);
                break;
            }
            case "door":
            {
                bool outside = parts.Length > 3 && parts[3] == "out";   // user decision A: "/ai edge door out" = the outside option
                var obs = AIObservation.Take(server, player, false);
                var c = Candidates(server, player, obs, "").Find(x => x.kind == "place" && x.edgeRole == EdgeCandidateRole.DoorPlace && x.key.EndsWith(":out") == outside)
                     ?? GuardCandidates(server, player, obs, "").Find(x => x.kind == "place" && x.edgeRole == EdgeCandidateRole.DoorRepair && x.key.EndsWith(":out") == outside);   // live run ⑦: a gone door too
                if (c == null) { AIPlayerSpikeMod.Reply("[시험용] 문 달기 후보 없음(문 자리·가방의 문·안쪽 설 칸·문 양옆 벽 확인)"); break; }
                AIBody.StartSkill(server, player, new PlaceSkill(c.itemId, c.tile, false, c.placeStand, true), now);   // review G3: from inside only
                break;
            }
            case "torch":
            {
                var c = Candidates(server, player, AIObservation.Take(server, player, false), "").Find(x => x.kind == "place" && x.edgeRole == EdgeCandidateRole.TorchPlace);
                if (c == null) { AIPlayerSpikeMod.Reply("[시험용] 횃불 후보 없음(기지 안쪽에 있어야 하고 가방에 횃불)"); break; }
                AIBody.StartSkill(server, player, new PlaceSkill(c.itemId, c.tile, false, c.placeStand), now);
                break;
            }
            case "pass":
            case "close":
            {
                int2 t;
                if (parts.Length >= 5) t = new int2(Arg(3, 0), Arg(4, 0));
                else { t = default; float bd = 1e9f; float3 me = AIBody.Pos(server, player); foreach (var d in S.doors.Keys) { float dd = math.distance(me.xz, (float2)d); if (dd < bd) { bd = dd; t = d; } } if (bd > 1e8f) { AIPlayerSpikeMod.Reply("[시험용] 기억한 문 없음"); break; } }
                AIBody.StartSkill(server, player, sub == "pass" ? (AISkill)new DoorPassSkill(t) : new DoorCloseSkill(t), now);
                break;
            }
            case "mem":
            {
                var s = S;
                string r = $"계획 {(s.planSet ? "있음" : "없음")}, 틈 {s.gaps.Count}, 안쪽 {s.inside.Count}칸, 문 자리 {(s.door.HasValue ? $"({s.door.Value.x},{s.door.Value.y})" : "없음")}; 놓은 벽 {s.walls.Count}, 문 {s.doors.Count}, 횃불 {s.lights.Count}";
                AIPlayerSpikeMod.Reply("[시험용] " + r); AIPlayerSpikeMod.Log("EDGE mem: " + r);
                break;
            }
            case "forget":
                ClearMemory(); AIMemory.MarkDirty(); AIMemory.Save(true); AIPlayerSpikeMod.Reply("[시험용] 둘레 기억을 지움");
                break;
            default:
                AIPlayerSpikeMod.Reply("사용법: /ai edge | around core|물체이름|x,y|rect:x1,y1,x2,y2 … | pick N [문틈] | mydoor x z | map | batches | fill K [out] | door [out] | torch | pass [x z] | close [x z] | mem | forget | breaks | repair K [out] | pickup x z [nx nz] | reach | guard");
                break;
        }
    }
}
