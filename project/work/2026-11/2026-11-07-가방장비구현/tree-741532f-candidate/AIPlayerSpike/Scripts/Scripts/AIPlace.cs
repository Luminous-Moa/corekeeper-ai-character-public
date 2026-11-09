// AIPlace — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using Inventory;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Where to put a placeable (a chest, pot or crafting station) so it does not cut a passage (먹거리 자급 마무리;
// work/2026-11/2026-11-04-먹거리-자급/4-실기.md): an AI chest and pot in a one-tile-wide cave cut the base off from
// the field (ai2 2026-11-04 F6). Looks at known, walkable, free tiles up to SearchR tiles from the AI. A footprint is
// fine when, with it blocked, the walkable tiles next to it stay in one piece with the AI (4-neighbour moves inside a
// window around it) and every own plot, known water and object next to a reachable tile keeps a reachable neighbour
// (8 neighbours, the stand rule of AIFarm.StandFor). A small dead-end pocket that does not lead out of the window may be
// closed off (cost added). Not on dug-up ground or own plots; base items (chest, pot) inside the base when the AI is
// there. The AI then stands next to it and aims there (ClientInput.wasAiming + mouseOrJoystickWorldPoint, as AIFarm):
// PlacementHandler.FindPlaceablePositionFromMouseOrJoystick takes the aimed point minus ((w-1)/2,(h-1)/2), rounded, as
// the corner tile when it is inside a box of (size + 1.5) around the player.
public static class AIPlaceSpot
{
    public const int SearchR = 5;
    public sealed class Spot { public int2 anchor, size, stand; public float score; public bool pocket; public string why = ""; }

    public static int2 SizeOf(ObjectID id)
    {
        ObjectInfo info = PugDatabase.GetObjectInfo(id, 0);
        return info != null ? new int2(math.max(1, info.prefabTileSize.x), math.max(1, info.prefabTileSize.y)) : new int2(1, 1);
    }

    public static float2 AimPoint(Spot s) => (float2)s.anchor + new float2((s.size.x - 1) / 2f, (s.size.y - 1) / 2f);

    // inside the placement box around the player (with a margin) and not standing on the footprint.
    // 코어-안전지대 단계 1 live run: the margins were 0.15 (box) and 0.45 (body), which left a 0.15-tile window on a
    // 1-wide corridor tile (stand next to it in the corridor) that the full-speed adjust step jumped over every time
    // (fence (12,5), door (-12,5) from the west); now 0.05 and 0.35 (~0.35-tile window).
    // 실측결함수정3 N7: one rule with the stand pick (AIPlaceGrid.PressFrom: a side of the footprint, 1.1 at most)
    public static bool InReach(Spot s, float3 me) => AIPlaceGrid.PressFrom(s.anchor.x, s.anchor.y, s.size.x, s.size.y, me.x, me.z);

    // The window around the AI as AIPlaceGrid sees it (shared by the options and the stand of a given tile). The
    // things that must stay reachable (own plots, known water, objects in the window) are named in the facts.
    private static PlaceGridInput Grid(World server, Entity player, ObjectID item, int extra, out HashSet<int2> blocked, PlaceArea area = null)
    {
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        int2 meT = AIEdge.StandTile(server, me);   // 재실측 결함 2
        int2 size = SizeOf(item);
        try { blocked = AIObjects.BlockedInView(server, player, out _); } catch { blocked = new HashSet<int2>(); }
        HashSet<int2> occupied;   // live run: torches etc. occupy a tile without a blocking collider
        try { occupied = AIObjects.OccupiedInView(server, player); } catch { occupied = blocked; }
        bool baseItem = AIBase.IsChestItem(server, item) || AICook.IsPotItem(server, item);
        int m = SearchR + math.max(size.x, size.y) + 4 + extra;
        int2 lo = meT - m;
        var ownDoors = AIEdge.WalkDoorTilesNow(server, me, meT, m);   // 실측결함수정3 N4/N6: a door seen gone is ground; 검수 Q5: my doors too
        int n = 2 * m + 1;
        var g = new PlaceGridInput
        {
            n = n, loX = lo.x, loY = lo.y, pass = new bool[n * n], noPlace = new bool[n * n], plot = new bool[n * n], dug = new bool[n * n],
            meX = meT.x, meY = meT.y, meFx = me.x, meFy = me.z, sizeX = size.x, sizeY = size.y, searchR = SearchR,
            baseItem = baseItem && AIBase.AtBase(me), baseX = AIBase.BaseTile.x, baseY = AIBase.BaseTile.y, baseRadius = AIBase.BaseRadius,
            baseWord = AIBase.IsChestItem(server, item) ? "기지 상자: 기지" : "기지",
            water = new bool[n * n], onWater = CanGoOnWater(item), onLand = CanGoOnLand(item), noStand = new bool[n * n], door = new bool[n * n],
            footprintWalkable = PugDatabase.GetObjectInfo(item, 0) is ObjectInfo fi && fi.tileType != TileType.none && fi.tileType.IsWalkableTile(),
        };
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int2 t = lo + new int2(x, y);
                int i = x * n + y;
                bool known = AIWorldFacts.IsKnown(me, t);
                g.pass[i] = known && tr.Walkable(t) && !occupied.Contains(t) && !AIFarm.IsWater(tr, t);
                g.noPlace[i] = AIPlaceGrid.SpawnPad(t.x, t.y) || known && IsImmune(tr, t);
                g.noStand[i] = AIPlaceGrid.SpawnPad(t.x, t.y);   // recheck R1
                g.door[i] = known && ownDoors.Contains(t);    // 재실측 N-f: the walk to a stand passes own doors
                g.plot[i] = AIFarm.IsOwnPlot(t);
                g.dug[i] = known && AIFarm.IsDug(tr, t);
                g.water[i] = known && AIFarm.IsWater(tr, t) && !IsLava(tr, t) && !blocked.Contains(t);
            }
        bool In(int2 t) => t.x >= lo.x && t.y >= lo.y && t.x < lo.x + n && t.y < lo.y + n;
        // 제작대-놓기 §2.3: a place goal's area (its centre and radius) instead of the base radius
        if (area != null) { g.baseItem = area.radius > 0f; g.baseX = area.center.x; g.baseY = area.center.y; g.baseRadius = area.radius; g.areaWord = area.word; }
        // 제작대-놓기 §2.10: the base edge plan's marks (facts on the options, never a reason to drop one)
        try { AIEdge.MarkPlaceGrid(server, player, g, item); } catch (Exception e) { AIPlayerSpikeMod.LogError("place: edge marks failed", e); g.edgeGap = null; }
        foreach (var t in AIFarm.OwnPlotTiles) if (In(t)) g.targets.Add((t.x, t.y, "내 밭"));
        foreach (var t in AIFarm.KnownWaterTiles) if (In(t)) g.targets.Add((t.x, t.y, "물"));
        foreach (var t in blocked)
            if (In(t)) g.targets.Add((t.x, t.y, AIObjects.ObjectAt(player, t, out ObjectID oid) && oid != ObjectID.None ? oid.ToString() : "물건"));
        return g;
    }

    // Every physically possible spot around the AI with what placing there does (AIPlaceGrid).
    public static List<PlaceOption> AllOptions(World server, Entity player, ObjectID item, HashSet<int2> extraRefused, out string note, PlaceArea area = null)
    {
        var g = Grid(server, player, item, 0, out _, area);
        // Previous failed presses are history. Current placement facts decide applicability.
        if (extraRefused != null) foreach (var a in extraRefused) g.refused.Add(PlaceGridInput.Key(a.x, a.y));
        var all = AIPlaceGrid.All(g, out int looked, out int impossible);
        note = $"{item} 놓을 수 있는 자리 {all.Count}곳({SearchR}칸 안 {looked}곳 중 {impossible}곳은 놓을 수 없음: 막힘·물건·물·코어 스폰 받침·내 칸·못 감·게임이 잠시 거절)";
        if (!g.onLand) { string wn = AIPlaces.WaterOnlyNote(item.ToString(), all.Count, SearchR); if (wn != "") note += "; " + wn; }   // 실게임3 R-b
        return all;
    }

    public static Spot ToSpot(PlaceOption o) => new Spot
    {
        anchor = new int2(o.ax, o.ay), size = new int2(o.sx, o.sy), stand = new int2(o.standX, o.standY), score = o.score, pocket = o.pocket > 0, why = o.Facts,
    };

    // A spot without a decision (PlaceSkill without a tile, e.g. the test command): AIPlaceGrid.Best — a clean spot
    // when there is one; a spot with a consequence is not left out, its facts are logged.
    public static bool Choose(World server, Entity player, ObjectID item, HashSet<int2> badAnchors, out Spot spot, out string note)
    {
        spot = null;
        var all = AllOptions(server, player, item, badAnchors, out string allNote);
        var best = AIPlaceGrid.Best(all);
        if (best == null) { note = $"{item} 놓을 자리 없음: {allNote}"; return false; }
        spot = ToSpot(best);
        note = $"{item} 놓을 자리 ({spot.anchor.x},{spot.anchor.y}) {spot.size.x}x{spot.size.y}: {best.Facts}; 설 칸 ({spot.stand.x},{spot.stand.y}); {allNote}";
        return true;
    }

    // The place options for the decision model (one candidate per kind of consequence, AIPlaceGrid.Pick): each places
    // `item` on its tile from the stand it states (PlaceSkill with tile and stand, review F1) and states what placing
    // there does. `head` names the action ("상자 놓기"), `why` is the reason sentence of the caller. When no spot is
    // physically possible: no option, `none` says why (the caller puts it where the model reads its preparation,
    // review F3) and the same sentence goes to this character's blocked targets of the build (TakeNoSpot).
    public static List<AICandidate> Candidates(World server, Entity player, ObjectID item, string head, string why, bool progress, out string none, bool toBlocks = true, PlaceArea area = null)
    {
        var res = new List<AICandidate>();
        none = null;
        var all = AllOptions(server, player, item, null, out string note, area);
        var pick = AIPlaceGrid.Pick(all);
        if (pick.Count == 0)
        {
            none = $"{head}: 가방에 {item}이(가) 있으나 지금 둘레 {SearchR}칸 안에 놓을 수 있는 자리가 없음({note})";
            if (toBlocks) NoteNoSpot(player, none);   // recheck R3: a caller with its own note keeps it to one sentence
            LogOnce(player, item, $"PLACE options: none for {item}: {note}");
            return res;
        }
        foreach (var o in pick)
        {
            string dist = o.dist < 0.5f ? "지금 선 자리에서" : $"약 {o.dist:F0}칸";
            res.Add(new AICandidate
            {
                kind = "place", key = $"place:{item}@{o.ax},{o.ay}", itemId = item, tile = new int2(o.ax, o.ay), placeAt = true, progress = progress,
                placeStand = new int2(o.standX, o.standY),
                desc = $"{head} — 가방의 {item}를 ({o.ax},{o.ay})에 놓기({o.sx}x{o.sy}, 설 칸 ({o.standX},{o.standY}), {dist}). 그 자리: {o.Facts}. {why}",
            });
        }
        LogOnce(player, item, $"PLACE options: {item} {pick.Count} of {all.Count} ({string.Join(" | ", pick.ConvertAll(o => $"({o.ax},{o.ay}) {o.EffectKey}"))}); {note}");
        return res;
    }

    // Adds the options whose key is not in the list yet (two goals needing the same station: one set, review F8).
    public static void AddUnique(List<AICandidate> to, List<AICandidate> add)
    {
        foreach (var c in add) if (!to.Exists(x => x.key == c.key)) to.Add(c);
    }

    // "no spot" sentences of this character in this frame, for the blocked targets of the candidate build
    private static readonly Dictionary<Entity, (int frame, List<string> l)> _noSpot = new Dictionary<Entity, (int, List<string>)>();
    private static void NoteNoSpot(Entity player, string s)
    {
        int f = Time.frameCount;
        if (!_noSpot.TryGetValue(player, out var v) || v.frame != f) _noSpot[player] = v = (f, new List<string>());
        if (!v.l.Contains(s)) v.l.Add(s);
    }
    public static List<string> TakeNoSpot(Entity player)
    {
        if (!_noSpot.TryGetValue(player, out var v) || v.frame != Time.frameCount) return new List<string>();
        _noSpot.Remove(player);
        return v.l;
    }

    // the options are rebuilt at every candidate build: log them when they change
    private static readonly Dictionary<(Entity, ObjectID), string> _lastLog = new Dictionary<(Entity, ObjectID), string>();
    private static void LogOnce(Entity player, ObjectID item, string line)
    {
        if (_lastLog.TryGetValue((player, item), out var old) && old == line) return;
        _lastLog[(player, item)] = line;
        AIPlayerSpikeMod.Log(line);
    }

    // Spots the game refused for a while (3 presses did not place) per character and item, for RefusedSeconds: left
    // out of the options for now. Only temporary causes belong here: the spawn pad is permanent (AIPlaceGrid.SpawnPad,
    // review F2) and is never noted; a refusal while a player or creature body was on or next to the tile is not noted
    // either (it says nothing about the tile).
    public const float RefusedSeconds = 300f;
    private static readonly Dictionary<(Entity, ObjectID), Dictionary<int2, float>> _refused = new Dictionary<(Entity, ObjectID), Dictionary<int2, float>>();
    public static void NoteRefused(World server, Entity player, ObjectID item, int2 anchor, float now)
    {
        if (AIPlaceGrid.SpawnPad(anchor.x, anchor.y)) { AIPlayerSpikeMod.Log($"PLACE: ({anchor.x},{anchor.y}) refused for {item}: the spawn pad (permanent, not noted)"); return; }
        bool body = false;
        try { body = AIObjects.BodyNear(server, player, anchor); } catch { }
        if (body) { AIPlayerSpikeMod.Log($"PLACE: ({anchor.x},{anchor.y}) refused for {item} with a body on or next to it; not noted"); return; }
        if (!_refused.TryGetValue((player, item), out var d)) _refused[(player, item)] = d = new Dictionary<int2, float>();
        d[anchor] = now + RefusedSeconds;
        AIPlayerSpikeMod.Log($"PLACE: ({anchor.x},{anchor.y}) refused by the game for {item}; left out for {RefusedSeconds:F0}s");
    }
    private static List<int2> Refused(Entity player, ObjectID item)
    {
        var l = new List<int2>();
        if (!_refused.TryGetValue((player, item), out var d)) return l;
        float now = Time.realtimeSinceStartup;
        foreach (var kv in d) if (now < kv.Value) l.Add(kv.Key);
        return l;
    }

    // why the game never takes this tile (the spawn pad, an immune tile), or null
    public static string NoPlaceText(AITileReader tr, int2 t) =>
        AIPlaceGrid.SpawnPad(t.x, t.y) ? "코어 스폰 받침(3x3)이라 게임이 놓기를 받지 않음(실측)" : IsImmune(tr, t) ? "보호 구역(immune)이라 게임이 놓기를 받지 않음" : null;

    // 코어-안전지대 단계 1: a spot on a given tile (the caller chose it, e.g. a gap of the base edge, so the passage
    // checks of Choose do not apply). The footprint must not be a tile the game never takes (the spawn pad, immune),
    // hold a wall or an object, or be water for an item that cannot go there. The stand (review F1): `preferStand` (the
    // stand the chosen option stated, so the facts and the walk agree) while the AI still walks to it without crossing
    // the footprint, else the tile it reaches that way with the shortest walk (AIPlaceGrid.StandFor), skipping
    // `badStands`.
    public static bool ForTile(World server, Entity player, ObjectID item, int2 anchor, bool allowImmune, HashSet<int2> badStands, int2? preferStand, out Spot spot, out string note, bool standOnly = false)
    {
        spot = null;
        var tr = new AITileReader(server);
        int2 size = SizeOf(item);
        int2 meT = AIEdge.StandTile(server, AIBody.Pos(server, player));   // 재실측 결함 2
        int far = math.max(math.abs(anchor.x - meT.x), math.abs(anchor.y - meT.y));
        var g = Grid(server, player, item, math.max(0, far - SearchR), out var blocked);
        HashSet<int2> occupiedF;
        try { occupiedF = AIObjects.OccupiedInView(server, player); } catch { occupiedF = blocked; }
        for (int fx = 0; fx < size.x; fx++)
            for (int fy = 0; fy < size.y; fy++)
            {
                int2 f = anchor + new int2(fx, fy);
                string np = NoPlaceText(tr, f);
                if (!allowImmune && np != null) { note = $"{item} 놓을 칸 ({f.x},{f.y})은 {np}"; return false; }
                if (tr.HasWall(f, out _, out _, out _)) { note = $"{item} 놓을 칸 ({f.x},{f.y})에 이미 벽이 있음"; return false; }
                if (blocked.Contains(f) || occupiedF.Contains(f)) { note = $"{item} 놓을 칸 ({f.x},{f.y})에 이미 물건이 있음"; return false; }
                bool water = AIFarm.IsWater(tr, f) && !IsLava(tr, f);
                if (water && !CanGoOnWater(item)) { note = $"{item}은(는) 물 위에 놓을 수 없음(게임 설정) — ({f.x},{f.y})"; return false; }
                if (!water && !CanGoOnLand(item)) { note = $"{item}은(는) 물 위에만 놓임 — ({f.x},{f.y})는 물 칸이 아님"; return false; }
                if (!tr.Walkable(f) && !water) { note = $"{item} 놓을 칸 ({f.x},{f.y})이 걷는 칸이 아님({tr.Top(f).tileType})"; return false; }
                // 합치기 전 수정 1: an object whose body overlaps the tile's 0.9 box (a grave next to it) — the game refuses it
                if (OverlapAt(server, player, f, out string ov)) { note = $"{item} {ov}"; return false; }
            }
        var bad = new HashSet<long>();
        if (badStands != null) foreach (var b in badStands) bad.Add(PlaceGridInput.Key(b.x, b.y));
        long? prefer = preferStand.HasValue ? PlaceGridInput.Key(preferStand.Value.x, preferStand.Value.y) : (long?)null;
        if (!AIPlaceGrid.StandFor(g, anchor.x, anchor.y, size.x, size.y, bad, prefer, out int sX, out int sY))
        {
            // 합치기 전 수정 1: the stand of a "from this side only" option reached only through the tile to place (an AI
            // outside a fallen door with the outside stand taken): the same through-walk as below, not "no stand"
            // (22-짧은확인 t=666.0 "no stand reached" from (-4,-9))
            if (standOnly && preferStand.HasValue && AIPlaceGrid.StandFor(g, anchor.x, anchor.y, size.x, size.y, bad, prefer, out int tx, out int ty, true) && tx == preferStand.Value.x && ty == preferStand.Value.y)
            {
                bool outsideP = AIEdge.PlanActive && !AIEdge.IsInside(preferStand.Value);
                spot = new Spot { anchor = anchor, size = size, stand = preferStand.Value, score = 0f, why = "지정한 칸" };
                note = $"{item} 지정한 칸 ({anchor.x},{anchor.y}) {size.x}x{size.y}; 설 칸 ({tx},{ty}) ({AIPlaceGrid.ThroughStandNote(outsideP)})";
                return true;
            }
            // 검수 D5: a diagonal the walk reaches is left out by the rule, not because it cannot be reached — said so
            string diag = AIPlaceGrid.DiagonalStand(g, anchor.x, anchor.y, size.x, size.y, bad, out int dx, out int dy) ? "; " + AIPlaceGrid.DiagonalLeftOutNote(dx, dy) : "";
            note = $"{item} 놓을 칸 ({anchor.x},{anchor.y}) 옆(4방향)에 놓을 칸을 지나지 않고 갈 수 있는 설 칸이 없음{(bad.Count > 0 ? $"(못 간 설 칸 {bad.Count}개 뺌)" : "")}{diag}";
            return false;
        }
        int2 stand = new int2(sX, sY);
        bool asOption = preferStand.HasValue && math.all(preferStand.Value == stand);
        if (standOnly && !asOption)
        {
            // recheck N2: an AI on the other side reaches the chosen stand only through the tile to close (still open):
            // allowed — it walks through it and places from there. 재실측 N-e: the side is the stand's (an outside
            // stand of a ':out' option is no "inside")
            bool outsideStand = AIEdge.PlanActive && !AIEdge.IsInside(preferStand.Value);
            string sideW = outsideStand ? "바깥" : "안쪽";
            if (AIPlaceGrid.StandFor(g, anchor.x, anchor.y, size.x, size.y, bad, prefer, out int cx, out int cy, true) && cx == preferStand.Value.x && cy == preferStand.Value.y)
            {
                spot = new Spot { anchor = anchor, size = size, stand = preferStand.Value, score = 0f, why = "지정한 칸" };
                note = $"{item} 지정한 칸 ({anchor.x},{anchor.y}) {size.x}x{size.y}; 설 칸 ({cx},{cy}) ({AIPlaceGrid.ThroughStandNote(outsideStand)})";
                return true;
            }
            note = $"{item} 놓을 칸 ({anchor.x},{anchor.y})은 {sideW} ({preferStand.Value.x},{preferStand.Value.y})에서만 놓는데 그 칸으로 갈 수 없음"; spot = null; return false;
        }
        spot = new Spot { anchor = anchor, size = size, stand = stand, score = 0f, why = "지정한 칸" };
        note = $"{item} 지정한 칸 ({anchor.x},{anchor.y}) {size.x}x{size.y}; 설 칸 ({stand.x},{stand.y}){(preferStand.HasValue ? (asOption ? " (후보의 설 칸)" : $" (후보의 설 칸 ({preferStand.Value.x},{preferStand.Value.y})은 지금 못 감)") : " (길로 가장 가까운 칸)")}";
        return true;
    }

    // 합치기 전 수정 1: an object whose collider box overlaps the 0.9 box at the tile's centre (AIPlaceGrid.OverlapsPlaceBox,
    // the game's PlacementHandler check). Left out as the game does: tiles, pets, minions, critters; also players and
    // enemies (they move: not a lasting reason) and dropped items (whether the game's cast hits them is not read).
    public static bool OverlapAt(World server, Entity player, int2 t, out string note)
    {
        note = "";
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<Unity.Physics.PhysicsCollider>()))
            using (var ents = q.ToEntityArray(Allocator.Temp))
                foreach (var e in ents)
                {
                    if (e == player || em.HasComponent<PlayerGhost>(e) || em.HasComponent<EnemyCD>(e) || em.HasComponent<TileCD>(e) || em.HasComponent<CritterCD>(e)
                        || em.HasComponent<PetCD>(e) || em.HasComponent<MinionCD>(e) || em.HasComponent<PickUpItemCD>(e)) continue;
                    var lt = em.GetComponentData<LocalTransform>(e);
                    if (math.distance(lt.Position.xz, (float2)t) > 3f) continue;
                    var pc = em.GetComponentData<Unity.Physics.PhysicsCollider>(e);
                    if (!pc.IsValid) continue;
                    var box = pc.Value.Value.CalculateAabb(new Unity.Mathematics.RigidTransform(lt.Rotation, lt.Position));
                    if (!AIPlaceGrid.OverlapsPlaceBox(t.x, t.y, box.Min.x, box.Min.z, box.Max.x, box.Max.z)) continue;
                    note = AIPlaceGrid.OverlapNote(em.GetComponentData<ObjectDataCD>(e).objectID.ToString(), lt.Position.x, lt.Position.z, t.x, t.y);
                    return true;
                }
        }
        catch (Exception ex) { AIPlayerSpikeMod.Log($"PLACE: overlap check at ({t.x},{t.y}) failed ({ex.GetType().Name}); not counted"); }
        return false;
    }

    // Can this item go on a water tile? The game decides per item: PlacementHandler.Activate sets canPlaceOnWater from
    // the placement prefab's ObjectPropertiesCD (a placement-settings flag -975748197, then -1324171664), and
    // ShouldCheckPlaceObjectOnTile accepts a water tile (not lava, tileset 3) only then. Same lookup as AIFarm's stage times.
    private static readonly Dictionary<ObjectID, bool> _onWater = new Dictionary<ObjectID, bool>();
    public static bool CanGoOnWater(ObjectID item)
    {
        if (_onWater.TryGetValue(item, out bool v)) return v;
        v = false;
        try
        {
            var pr = PugDatabase.GetComponent<ObjectPropertiesCD>(item, 0);
            v = pr.IsValid && pr.Has(-975748197) && pr.Has(-1324171664);
            // 제작대-놓기 실게임2 P3: a bridge (a tile item, tileType bridge) goes on water in the game, but its prefab has no
            // canPlaceOnWater flag (live run: WoodBridge "cannot"). The game's other way onto water is the "can be placed
            // on" object lists (PlacementHandler.ObjectCanBePlacedOnObject, properties 1757427560 / -789473209 on the
            // water tile's object) — [추정] that is how bridges are taken; not read here, the tile type decides.
            if (!v && PugDatabase.GetObjectInfo(item, 0) is ObjectInfo bi && AIPlaceGoal.TileGoesOnWater(bi.tileType)) v = true;
        }
        catch (Exception e) { AIPlayerSpikeMod.Log($"PLACE: water rule of {item} unreadable ({e.GetType().Name}); not on water"); }
        _onWater[item] = v;
        AIPlayerSpikeMod.Log($"PLACE: {item} {(v ? "can" : "cannot")} go on water (game placement flag)");
        return v;
    }

    // 실게임3 R-b: may this item go on a ground (walkable) tile? The game's canPlaceOnWalkableTiles (PlacementHandler.
    // Activate: the placement-settings flag -975748197, then 1497889171; true without settings), and a bridge tile is
    // water only (AIPlaces.LandOk). One rule for the place options, a given tile and rect / line.
    private static readonly Dictionary<ObjectID, bool> _onLand = new Dictionary<ObjectID, bool>();
    public static bool CanGoOnLand(ObjectID item)
    {
        if (_onLand.TryGetValue(item, out bool v)) return v;
        v = true;
        bool settings = false, land = false; string tt = "none";
        try
        {
            var pr = PugDatabase.GetComponent<ObjectPropertiesCD>(item, 0);
            settings = pr.IsValid && pr.Has(-975748197);
            land = settings && pr.Has(1497889171);
            if (PugDatabase.GetObjectInfo(item, 0) is ObjectInfo li) tt = li.tileType.ToString();
            v = AIPlaces.LandOk(settings, land, tt);
        }
        catch (Exception e) { AIPlayerSpikeMod.Log($"PLACE: ground rule of {item} unreadable ({e.GetType().Name}); ground allowed"); }
        _onLand[item] = v;
        AIPlayerSpikeMod.Log($"PLACE: {item} {(v ? "can" : "cannot")} go on ground (settings {settings}, land flag {land}, tile {tt})");
        return v;
    }

    public static bool IsLava(AITileReader tr, int2 t) { var top = tr.Top(t); return top.tileType == TileType.water && top.tileset == 3; }

    public static bool IsImmune(AITileReader tr, int2 t)
    {
        foreach (var l in tr.Layers(t)) if (l.tileType == TileType.immune) return true;
        return false;
    }

    // 코어-안전지대 단계 1: is `item` placed on the footprint (anchor, size)? A tile item (ObjectInfo.tileType set: wall
    // blocks, wood wall, fence, floor, rug, bridge) by a layer of that type on a footprint tile that was not there before
    // placing (`layersBefore`, LayersOn at the pick; review F5); an object (door, fence gate, torch, ...) by an entity of
    // that id on the footprint that was not in `before` (review F4: the game puts it on the aimed tiles,
    // PlacementHandler.cs:220-223, so no tile around them counts).
    public static bool PlacedAt(World server, ObjectID item, int2 anchor, int2 size, HashSet<Entity> before, HashSet<(int2, TileType, int)> layersBefore, out string what)
    {
        what = "";
        ObjectInfo info = PugDatabase.GetObjectInfo(item, 0);
        if (info != null && info.tileType != TileType.none)
        {
            var tr = new AITileReader(server);
            for (int fx = 0; fx < size.x; fx++)
                for (int fy = 0; fy < size.y; fy++)
                {
                    int2 f = anchor + new int2(fx, fy);
                    foreach (var l in tr.Layers(f))
                        if (l.tileType == info.tileType && (layersBefore == null || !layersBefore.Contains((f, l.tileType, l.tileset))))
                        {
                            what = $"타일 {l.tileType} tileset={l.tileset}{(l.tileset != info.tileset ? $"(아이템 tileset {info.tileset}와 다름)" : "")} ({f.x},{f.y})";
                            return true;
                        }
                }
            return false;
        }
        var e = EntityOn(server, item, anchor, size, before);
        if (e == Entity.Null) return false;
        var em = server.EntityManager;
        float3 p = em.GetComponentData<LocalTransform>(e).Position;
        what = $"물체 {item} ({p.x:F1},{p.z:F1}) variation={em.GetComponentData<ObjectDataCD>(e).variation}";
        return true;
    }

    // the tile layers on the footprint now (taken when the spot is picked, compared at the end)
    public static HashSet<(int2, TileType, int)> LayersOn(World server, int2 anchor, int2 size)
    {
        var res = new HashSet<(int2, TileType, int)>();
        var tr = new AITileReader(server);
        for (int fx = 0; fx < size.x; fx++)
            for (int fy = 0; fy < size.y; fy++)
            {
                int2 f = anchor + new int2(fx, fy);
                foreach (var l in tr.Layers(f)) res.Add((f, l.tileType, l.tileset));
            }
        return res;
    }

    // an entity of `item` whose tile is on the footprint, not in `skip`
    public static Entity EntityOn(World server, ObjectID item, int2 anchor, int2 size, HashSet<Entity> skip)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (em.GetComponentData<ObjectDataCD>(e).objectID != item || (skip != null && skip.Contains(e))) continue;
                if (em.HasComponent<ContainedObjectsBuffer>(e) && em.HasComponent<PlayerGhost>(e)) continue;
                int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                if (t.x >= anchor.x && t.y >= anchor.y && t.x < anchor.x + size.x && t.y < anchor.y + size.y) return e;
            }
        return Entity.Null;
    }

    public static HashSet<Entity> EntitiesNear(World server, ObjectID item, int2 anchor, int2 size)
    {
        var res = new HashSet<Entity>();
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (em.GetComponentData<ObjectDataCD>(e).objectID != item) continue;
                int2 t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
                if (t.x >= anchor.x - 2 && t.y >= anchor.y - 2 && t.x <= anchor.x + size.x + 1 && t.y <= anchor.y + size.y + 1) res.Add(e);
            }
        return res;
    }

}

// Places a crafting station / chest / pot from the bag the way a client does with a placeable item in hand: hold it in
// the hotbar slot, aim at a tile and hold the secondary use (PlaceObjectSlot.UpdateEquipment: secondInteractHeld ->
// PlaceItem at PlacementCD.bestPositionToPlaceAt; the server checks that the spot is free). The tile is the
// option the decision model chose (AIPlaceSpot.Candidates: each spot states what placing there cuts; 코어-안전지대 단계 1)
// or, without a tile, AIPlaceSpot.Choose; the AI walks next to it first. A given tile that does not take after 3 presses
// fails and is left out of the options for a while (a chosen spot: the next one, 3 spots). Done when the bag has one
// less and the chest / pot / station is in view within 3 tiles, or the tile layer / new object is on the footprint
// (wall blocks, wood wall, fence, floor, door, fence gate, torch: AIPlaceSpot.PlacedAt).
public class PlaceSkill : AISkill
{
    private readonly ObjectID _item;
    public bool TileFault { get; private set; }   // 제작대-놓기 재검수 N2: Start failed because of the tile (no spot / stand), not the bag or the hand slot
    private int _slot, _before, _presses, _spots;
    private float _pressAt = -1f, _lastLog;
    private bool _settleOn;          // 합치기 전 수정 1: at the stand, standing still for the press (AIPlaceGrid.PressStep)
    private PlacePressState _press;
    private readonly HashSet<Entity> _chestsBefore = new HashSet<Entity>();   // chests in view before placing (a chest: the new one becomes this character's)
    private readonly HashSet<int2> _badAnchors = new HashSet<int2>();
    private AIPlaceSpot.Spot _spot;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn;
    // 코어-안전지대 단계 1: a given tile (AIPlaceSpot.ForTile) instead of a chosen spot; a failed stand is dropped and the
    // next one is tried (3 stands); the tile itself is not changed.
    private readonly int2? _target;
    private readonly bool _allowImmune;
    private readonly HashSet<int2> _badStands = new HashSet<int2>();
    private HashSet<Entity> _sameBefore = new HashSet<Entity>();   // entities of this item around the spot before placing (an object: the new one is not among them)
    private HashSet<(int2, TileType, int)> _layersBefore;            // the footprint's tile layers before placing (a tile item: a new layer of its type)
    private readonly int2? _stand;                                   // the stand the chosen option stated (review F1)
    public PlaceSkill(ObjectID item) { Name = "놓기"; _item = item; Key = $"place:{item}"; }
    private readonly bool _standOnly;   // 단계 2: only the given stand (the tile that closes a gap is placed from inside)
    public PlaceSkill(ObjectID item, int2 tile, bool allowImmune = false, int2? stand = null, bool standOnly = false)
    {
        Name = "놓기"; _item = item; _target = tile; _allowImmune = allowImmune; _stand = stand; _standOnly = standOnly && stand.HasValue; Key = $"place:{item}@{tile.x},{tile.y}";
    }

    public override bool Start(World server, Entity player, float now)
    {
        var inv = AIBody.InventoryCounts(server, player);
        inv.TryGetValue(_item, out _before);
        if (_before <= 0) { Status = SkillStatus.Failed; Reason = $"가방에 {_item}이(가) 없음"; return false; }
        _slot = AICraft.HotbarSlotFor(server, player, _item, out string swap);
        if (!string.IsNullOrEmpty(swap)) { Facts.Add(swap); AIPlayerSpikeMod.Log($"SKILL 놓기: {swap}"); }
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = "손 칸으로 옮길 자리가 없음"; return false; }
        if (!Pick(server, player, out string why)) { Status = SkillStatus.Failed; Reason = why; TileFault = true; return false; }
        if (AIBase.IsChestItem(server, _item)) foreach (var c in AIBase.ChestsInView(server, player)) _chestsBefore.Add(c.entity);
        if (AICook.IsPotItem(server, _item)) foreach (var p in AICook.PotsInView(server, player)) _chestsBefore.Add(p.entity);   // pots in view before: the new one becomes this character's (AICook)
        foreach (var s in AICraft.StationsInView(server, player)) if (s.id == _item) _chestsBefore.Add(s.entity);   // 실게임3 R-a: stations of this kind before
        Facts.Add($"item={_item} slot={_slot}");
        return true;
    }

    private bool Pick(World server, Entity player, out string note)
    {
        _spots++;
        bool ok = _target.HasValue
            ? AIPlaceSpot.ForTile(server, player, _item, _target.Value, _allowImmune, _badStands, _stand, out _spot, out note, _standOnly)
            : AIPlaceSpot.Choose(server, player, _item, _badAnchors, out _spot, out note);
        AIPlayerSpikeMod.Log($"SKILL 놓기: {note}");
        Facts.Add(note);
        _presses = 0; _pressAt = -1f; _settleOn = false; _press = null;
        if (ok) { _sameBefore = AIPlaceSpot.EntitiesNear(server, _item, _spot.anchor, _spot.size); _layersBefore = AIPlaceSpot.LayersOn(server, _spot.anchor, _spot.size); }
        return ok;
    }

    private BodyInput Retry(World server, Entity player, string why)
    {
        AIPlayerSpikeMod.Log($"SKILL 놓기: {(_target.HasValue ? $"stand ({_spot.stand.x},{_spot.stand.y}) for" : "spot")} ({_spot.anchor.x},{_spot.anchor.y}) dropped: {why}");
        if (_target.HasValue) _badStands.Add(_spot.stand); else _badAnchors.Add(_spot.anchor);
        if (_navOn) { _nav.Dispose(server); _navOn = false; }
        if (_spots >= 3) ObserveOnce("place-spots", _target.HasValue ? $"({_spot.anchor.x},{_spot.anchor.y})에 설 칸 3곳에서 놓지 못함(마지막: {why})" : $"놓을 자리 3곳에 놓지 못함(마지막: {why})");
        if (!Pick(server, player, out string note)) return Finish(SkillStatus.Failed, note);
        return BodyInput.Idle;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var inv = AIBody.InventoryCounts(server, player);
        inv.TryGetValue(_item, out int have);
        if (have < _before)
        {
            if (_navOn) { _nav.Dispose(server); _navOn = false; }
            // a chest has no recipe list: judged by a new chest of that kind in view within 3 tiles (not one that stood
            // there before, e.g. the host's), which becomes this character's own chest (AIBase, 생활-기지)
            if (AIBase.IsChestItem(server, _item))
            {
                var ch = AIBase.ChestsInView(server, player).Find(c => c.id == _item && c.dist <= 3f && !_chestsBefore.Contains(c.entity));
                if (ch != null) { AIBase.NoteOpened(server, ch.entity, true); return Placed($"{_item} 놓음 ({ch.pos.x:F0},{ch.pos.z:F0}) {ch.dist:F1}칸{SpotText()}"); }
                if (now - _pressAt > 2f) ObserveOnce("pending-1", $"가방에서 {_item}이 줄었으나 근처에 보이지 않음");
                return BodyInput.Idle;
            }
            // a cooking pot: a new pot of that kind within 3 tiles (not one that stood there before) becomes this
            // character's own pot (AICook, 먹거리 자급 3)
            if (AICook.IsPotItem(server, _item))
            {
                var pv = AICook.PotsInView(server, player).Find(p => p.id == _item && p.dist <= 3f && !_chestsBefore.Contains(p.entity));
                if (pv != null) { AICook.NotePlaced(server, pv.entity); AIMemory.NotePlaced(pv.id, pv.pos); return Placed($"{_item} 놓음 ({pv.pos.x:F0},{pv.pos.z:F0}) {pv.dist:F1}칸{SpotText()}"); }
                if (now - _pressAt > 2f) ObserveOnce("pending-2", $"가방에서 {_item}이 줄었으나 근처에 새 솥이 보이지 않음");
                return BodyInput.Idle;
            }
            // 실게임3 R-a: the station just placed — not one of this kind that stood in view before (an old anvil 1.4 tiles
            // away was named), the one on the chosen footprint first (AIPlaces.NewPlacedIndex)
            var sts = AICraft.StationsInView(server, player).FindAll(s => s.id == _item);
            var stBefore = new bool[sts.Count]; var stOn = new bool[sts.Count]; var stDist = new float[sts.Count];
            for (int si = 0; si < sts.Count; si++)
            {
                stBefore[si] = _chestsBefore.Contains(sts[si].entity);
                int2 stT = AIBody.TileOf(sts[si].pos);
                stOn[si] = _spot != null && stT.x >= _spot.anchor.x && stT.y >= _spot.anchor.y && stT.x < _spot.anchor.x + _spot.size.x && stT.y < _spot.anchor.y + _spot.size.y;
                stDist[si] = sts[si].dist;
            }
            int sti = AIPlaces.NewPlacedIndex(stBefore, stOn, stDist, 3f);
            var st = sti >= 0 ? sts[sti] : null;
            if (st != null) { AIMemory.NotePlaced(st.id, st.pos); return Placed($"{_item} 놓음 ({st.pos.x:F0},{st.pos.z:F0}) {st.dist:F1}칸{SpotText()}"); }
            // anything else (코어-안전지대 단계 1: wall blocks, wood wall, fence, floor, door, fence gate, torch): the tile
            // layer or a new object of that kind on the chosen footprint
            if (_spot != null && AIPlaceSpot.PlacedAt(server, _item, _spot.anchor, _spot.size, _sameBefore, _layersBefore, out string what))
            {
                try { AIEdge.NotePlaced(server, _item, _spot.anchor); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: note placed failed", e); }
                return Placed($"{_item} 놓음: {what}{SpotText()}");
            }
            if (now - _pressAt > 2f) ObserveOnce("pending-3", $"가방에서 {_item}이 줄었으나 고른 칸{(_spot != null ? $" ({_spot.anchor.x},{_spot.anchor.y})" : "")}에 보이지 않음");
            return BodyInput.Idle;
        }
        if (now - StartTime > 30f) ObserveOnce("pending-4", "30초 안에 놓지 못함");
        float3 me = AIBody.Pos(server, player);
        float2 aim = AIPlaceSpot.AimPoint(_spot);
        // live-run fix 8: a tile placed from a given stand only is placed from that side (in reach from the other side is
        // not enough: the door went in from outside); the stand of the chosen option is where it is placed from
        bool sideOk = !_standOnly || AIBaseGuard.OnStandSide((_spot.anchor.x, _spot.anchor.y), (_spot.stand.x, _spot.stand.y), me.x, me.z);
        bool reach = AIPlaceSpot.InReach(_spot, me) && sideOk;
        // 합치기 전 수정 1 (검수 D4): a press in flight goes on while the body stays in reach; otherwise the walk to the stand
        // (until it arrives, within 0.35) and then standing still on the stand centre — the press only with the body still
        // in the press window (AIPlaceGrid.PressStep: the door step's settle, AISettle). The old end "step onto the centre
        // within 0.06" swung round it at full-speed steps and failed "설 칸에 왔지만 닿지 않음" (22-짧은확인 t=616.6, 769.4).
        bool pressing = _pressAt >= 0f && now - _pressAt <= 0.8f && reach;
        if (!pressing)
        {
            if (!_settleOn && !reach)
            {
                if (!_navOn) { _nav.Start(server, player, _spot.stand, 0.35f, now); _navOn = true; }
                var ns = _nav.Tick(server, player, now, out float2 move);
                if (ns == NavStatus.Failed) return Retry(server, player, "설 칸으로 못 감: " + _nav.FailReason);
                if (now - _lastLog >= 1f) { _lastLog = now; AIPlayerSpikeMod.Log($"SKILL 놓기: walking to stand ({_spot.stand.x},{_spot.stand.y}) for ({_spot.anchor.x},{_spot.anchor.y})"); }
                if (ns != NavStatus.Arrived) return new BodyInput { move = math.lengthsq(move) > 1e-4f ? move : float2.zero, slot = _slot };
            }
            if (_navOn) { _nav.Dispose(server); _navOn = false; }
            if (!_settleOn) { _settleOn = true; _press = new PlacePressState(); }
            if (!reach && math.distance(me.xz, (float2)_spot.stand) > 1.5f) { _settleOn = false; return new BodyInput { slot = _slot }; }   // pushed away: walk again
            var po = AIPlaceGrid.PressStep(_press, _spot.anchor.x, _spot.anchor.y, _spot.size.x, _spot.size.y, _spot.stand.x, _spot.stand.y, me.x, me.z, sideOk, now);
            if (!string.IsNullOrEmpty(po.observation)) ObserveOnce("place-stand", po.observation);
            if (po.fail != null) return Retry(server, player, po.fail);
            if (!po.press) return new BodyInput { move = new float2(po.mx, po.my), slot = _slot, nudge = po.nudge };
        }
        // press (held 0.5 s), release, up to 3 presses per spot
        if (_pressAt < 0f) { _pressAt = now; _presses++; AIPlayerSpikeMod.Log($"SKILL 놓기: aim at ({aim.x:F1},{aim.y:F1}) press #{_presses} from ({me.x:F2},{me.z:F2}), {(_press != null ? _press.pressDist : -1f):F2} off the tile, standing still"); }
        float t = now - _pressAt;
        if (t > 0.8f)
        {
            if (_presses >= 3)
                ObserveOnce("place-no-change", $"고른 칸 ({_spot.anchor.x},{_spot.anchor.y})에 3번 눌렀으나 놓임을 확인하지 못함");
            _pressAt = now; _presses++; t = 0f;
            AIPlayerSpikeMod.Log($"SKILL 놓기: aim at ({aim.x:F1},{aim.y:F1}) press #{_presses} from ({me.x:F2},{me.z:F2})");
        }
        return new BodyInput { slot = _slot, face = AIBody.DirTo(me, aim), aim = true, hasPoint = true, point = aim, secondary = t > 0.05f && t < 0.55f };
    }

    private string SpotText() => _spot == null ? "" : $" · 고른 자리 ({_spot.anchor.x},{_spot.anchor.y}) {_spot.why}";

    // 제작대-놓기: every placing this skill saw done is noted for the place goal's end check (AIPlaceGoal.GoalDone)
    private BodyInput Placed(string text)
    {
        if (_spot != null) AIPlaceGoal.NotePlaced(_item, _spot.anchor);
        return Finish(SkillStatus.Done, text);
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }
}
