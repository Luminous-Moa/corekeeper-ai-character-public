// AIPlaceGoal — the upper LLM's goto and place goals (제작대-놓기, work/2026-11/2026-11-05-제작대-놓기/1-설계.md §2.1-2.6, §2.11).
// - Places: a place identifier (Core/AIPlaces) is read against what the AI remembers (base, core, own field, stations,
//   own chests and pots, ores, water) into a target: its centre, where to stand, when the AI is there, the area a
//   placing counts in. A place that is not remembered is refused with the fact.
// - goto: walk there with the dig option every "go to X" already uses (AIBrain.PlanC -> TunnelSkill: digging, own and
//   my doors on the way); done when there.
// - place: walk to the place, then the place options of AIPlaceSpot (each spot with what placing there does, the place's
//   area in the facts) — the decision model picks the spot; a rect / line places its tiles in order (PlaceTilesSkill).
//   Done when PlaceSkill saw the item placed in the area as many times as the goal asked (every tile of a shape holds it).
// - The report's places list and can_do.place.
using System;
using System.Collections.Generic;
using System.Text;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// a place goal's area on the place options (AIPlaceSpot.Candidates): the facts say "반경 밖" past it
public sealed class PlaceArea { public int2 center; public float radius; public string word; }

public sealed class PlaceTarget
{
    public PlaceRef Ref;
    public string Label = "";
    public int2 Center;
    public float ArriveR = 1.5f;   // "there" for goto (distance from the centre)
    public float AreaR = 5f;       // a placing counts within this of the centre (0 = anywhere: here)
    public readonly List<int2> Stands = new List<int2>();
    public List<int2> Plots;       // farm
    public List<int2> Shape;       // rect / line
}

public static class AIPlaceGoal
{
    private sealed class State
    {
        public PlaceRef Ref;
        public float Start;
        public readonly List<(ObjectID item, int2 at, float t)> Placed = new List<(ObjectID, int2, float)>();
        public string LastWhy = "";
        public string RouteFact = "";   // 실게임 D1: the edge fact of the walk option offered last (for the stall sentence)
        // 검수 F4: tiles of a rect / line left out — refused when the goal was taken, or failed while placing — with why
        public readonly List<(int2 at, string why)> Skipped = new List<(int2, string)>();
        public readonly Dictionary<string, string> LastLog = new Dictionary<string, string>();
    }
    private static State S => AIAgent.Cur.Get<State>(AISlot.PlaceGoal);

    public static void OnGoalSet(World server, Entity player, AIGoal g, PlaceRef r)
    {
        var s = S;
        s.Ref = r; s.Start = g.startTime; s.LastWhy = "";
        s.Placed.Clear(); s.Skipped.Clear(); s.RouteFact = "";
        g.note = "";
        if (r != null && r.IsShape) g.count = AIPlaces.ShapeTiles(r).Count;
        if (g.kind != GoalKind.Place || r == null) return;
        try
        {
            // 검수 F3/F4: the facts the goal is taken with — tiles that cannot take the item, a bag short of what it needs
            if (r.IsShape)
            {
                ShapePlan(server, player, g.item, ShapeList(r), out _, out var todo, s.Skipped);
                g.note = Join(AIPlaces.ShortFact(g.item.ToString(), g.startCount, todo.Count, true), AIPlaces.SkippedText(SkipList(s)));
            }
            else g.note = AIPlaces.ShortFact(g.item.ToString(), g.startCount, g.count, false);
            if (g.note != "") AIPlayerSpikeMod.Log($"PLACEGOAL: taken with: {g.note}");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("place: goal facts failed", e); }
    }

    // 실게임2 P3: tile items that go on water (a bridge); floors and walls do not
    public static bool TileGoesOnWater(TileType t) => AIPlaces.TileGoesOnWater(t.ToString());

    private static string Join(string a, string b) => a == "" ? b : b == "" ? a : a + " · " + b;
    private static List<(int x, int y, string why)> SkipList(State s) => s.Skipped.ConvertAll(k => (k.at.x, k.at.y, k.why));
    private static bool IsSkipped(State s, int2 t) => s.Skipped.Exists(k => math.all(k.at == t));
    private static List<int2> ShapeList(PlaceRef r) { var l = new List<int2>(); foreach (var p in AIPlaces.ShapeTiles(r)) l.Add(new int2(p.x, p.y)); return l; }

    // 검수 F4: a tile in the shape fails and is left out (PlaceTilesSkill); the reason goes to the stall / end sentence
    public static void NoteSkip(int2 t, string why)
    {
        var s = S;
        if (IsSkipped(s, t)) return;
        s.Skipped.Add((t, why));
        s.LastWhy = $"({t.x},{t.y}) {why}";
    }

    // 재검수 N2: the batch stopped for a cause that is not a tile (the hand slot): the stall sentence says it (also a
    // goto / place walk stopped on edge walls it did not state, 실게임 D1)
    public static void NoteStop(string why) { S.LastWhy = why; }

    // 실게임 D1: the walls of a dig route that open the base edge, with the edge sentences (shared with the enemy dig
    // option and the home walk)
    public static List<((int x, int y) t, string note)> DigEdge(TunnelPlan plan) =>
        !AIEdge.PlanActive || plan == null ? new List<((int x, int y) t, string note)>() : AIBaseGuard.EdgeWallsOn(plan.walls.ConvertAll(w => (w.p.x, w.p.y)), q => AIEdge.WallFact(new int2(q.x, q.y)));
    // the option's edge sentence of a plan (검수 E1: said to be the only way when the plan needed the second pass)
    public static string EdgeText(TunnelPlan plan, List<((int x, int y) t, string note)> edge = null) =>
        plan == null ? "" : AIBaseGuard.DigEdgeText(edge ?? DigEdge(plan), 3, plan.edgeRoute, plan.firstCapped);

    // 우회 사실: the first way's fact line when plan.alt (the edge way) is much quicker; "" otherwise
    public static string DetourNote(TunnelPlan plan)
    {
        if (plan?.alt == null) return "";
        bool viaDoor = false; foreach (var p in plan.path) if (AIEdge.IsWalkDoorTile(p)) { viaDoor = true; break; }
        return " " + AIBaseGuard.DetourFact(viaDoor, plan.path.Count, AITunnelSearch.EstTiles(plan.path.Count, plan.totalHits), plan.alt.path.Count, AITunnelSearch.EstTiles(plan.alt.path.Count, plan.alt.totalHits), DigEdge(plan.alt)) + ".";
    }

    // 우회 사실: the edge way of plan.alt as its own option (key digedge:x,y; its walk plans with the edge open and stops on
    // edge walls it did not state). null when there is none. The assembler puts it last, in the edge share (AsmCand.detour).
    public static AICandidate DetourOption(TunnelPlan plan, int2 tile, string what, string head, string end, Entity entity = default, bool openEnough = false)
    {
        if (plan?.alt == null) return null;
        var a = plan.alt; var edge = DigEdge(a);
        return new AICandidate
        {
            kind = "dig", key = $"digedge:{tile.x},{tile.y}", tile = tile, entity = entity, digWall = false, digOpenEnough = openEnough, targetLabel = what, progress = true,
            digAllowEdge = true, digDetour = true, digEdgeOk = edge.ConvertAll(q => new int2(q.t.x, q.t.y)),
            desc = $"{head} — 둘레를 파서 가는 짧은 길. 아는 길 {a.path.Count}칸, 부술 벽 {a.walls.Count}개({a.WallSummary()}), 예상 약 {a.path.Count / 2.4f + a.totalHits * 0.5f:F0}초.{DetourNote(plan)} {end}",   // 실게임2 S-a: the detour fact carries the edge sentences (once)
        };
    }

    // the own / my doors a dig route goes through, or null. 검수 E2: the plan's route, not a promise — the walk takes the
    // game's path search, which may find another way (the doors actually passed are logged "nav: through own door")
    public static string DoorsOn(TunnelPlan plan)
    {
        if (plan == null) return null;
        var d = new List<string>();
        foreach (var p in plan.path) if (AIEdge.IsWalkDoorTile(p)) d.Add($"({p.x},{p.y})");
        return AIPlaces.DoorsOnText(d);
    }

    // 검수 S5: a log line once per change (the place target is read again at every candidate build and end check)
    public static void LogChanged(string key, string msg) => LogChangedBy(key, msg, msg);
    // 검수 H3: compare on `same` (what the line says without its times), log `msg`
    public static void LogChangedBy(string key, string same, string msg)
    {
        var s = S;
        if (s.LastLog.TryGetValue(key, out string last) && last == same) return;
        if (s.LastLog.Count > 256) s.LastLog.Clear();
        s.LastLog[key] = same;
        AIPlayerSpikeMod.Log(msg);
    }

    // PlaceSkill saw `item` placed with its footprint corner at `at`
    public static void NotePlaced(ObjectID item, int2 at)
    {
        var s = S;
        s.Placed.Add((item, at, Time.realtimeSinceStartup));
        if (s.Placed.Count > 64) s.Placed.RemoveAt(0);
    }

    // ------------------------------------------------------------------ what can be placed (§2.4)

    // One rule from the game data: an item whose ObjectInfo is a placeable prefab (objects, tiles: wall blocks, floor,
    // bridge, fence, rail; doors, torches) or a tile item or a door / gate prefab; seeds are planted by the farm goal.
    public static bool IsPlaceable(World server, ObjectID id, out string why)
    {
        why = null;
        ObjectInfo info = id == ObjectID.None ? null : PugDatabase.GetObjectInfo(id, 0);
        if (info == null) { why = $"{id}: 게임 데이터에 없는 품목"; return false; }
        if (AIFarm.IsSeed(server, id)) { why = $"{id}은(는) 씨앗 — 심기는 farm 목표"; return false; }
        if (info.objectType == ObjectType.PlaceablePrefab || info.tileType != TileType.none) return true;
        try { if (AIEdge.IsDoorItem(server, id)) return true; } catch { }
        why = $"{id}은(는) 놓는 물건이 아님(게임 데이터 objectType {info.objectType})";
        return false;
    }

    // can_do.place: the bag's placeable items
    public static List<string> BagPlaceable(World server, Entity player)
    {
        var res = new List<string>();
        foreach (var kv in AIBody.InventoryCounts(server, player))
            if (kv.Value > 0 && IsPlaceable(server, kv.Key, out _)) res.Add($"{kv.Key}:{kv.Value}");
        return res;
    }

    // ------------------------------------------------------------------ places (§2.1)

    public static string Resolve(World server, Entity player, PlaceRef r, out PlaceTarget t)
    {
        t = new PlaceTarget { Ref = r };
        float3 me = AIBody.Pos(server, player);
        int2 meT = AIBody.TileOf(me);
        var tr = new AITileReader(server);
        switch (r.Kind)
        {
            case "here":
                t.Label = "지금 자리"; t.Center = meT; t.ArriveR = float.MaxValue; t.AreaR = 0f; return null;
            case "base":
                t.Label = $"기지 {AIBase.BaseText}"; t.Center = AIBase.BaseTile; t.ArriveR = AIBase.HomeArrive; t.AreaR = AIBase.BaseRadius;
                if (AIBase.BaseStand(server, player, out int2 bs)) t.Stands.Add(bs);
                return null;
            case "core":
                t.Label = "코어 (0,0)"; t.Center = int2.zero; t.ArriveR = AIBase.HomeArrive; t.AreaR = 5f;
                if (AIBase.StandNear(server, player, int2.zero, out int2 cs)) t.Stands.Add(cs);
                return null;
            case "farm":
            {
                var plots = new List<int2>(AIFarm.OwnPlotTiles);
                if (plots.Count == 0) return "farm — 내 밭이 없음(밭은 farm 목표가 일굼)";
                int sx = 0, sy = 0; foreach (var p in plots) { sx += p.x; sy += p.y; }
                t.Label = $"내 밭 {plots.Count}칸"; t.Center = new int2((int)math.round(sx / (float)plots.Count), (int)math.round(sy / (float)plots.Count)); t.AreaR = 5f; t.Plots = plots;
                plots.Sort((a, b) => math.distance(me.xz, (float2)a).CompareTo(math.distance(me.xz, (float2)b)));
                foreach (var p in plots) if (t.Stands.Count < 3 && tr.Walkable(p)) t.Stands.Add(p);
                return null;
            }
            case "tile":
                t.Label = $"칸 ({r.X},{r.Y})"; t.Center = new int2(r.X, r.Y); t.ArriveR = 1.5f; t.AreaR = 3f;
                if (tr.Walkable(t.Center)) t.Stands.Add(t.Center); else SideStands(tr, me, t.Center, t.Stands);
                return null;
            case "water":
            {
                var w = new int2(r.X, r.Y);
                bool known = false; foreach (var k in AIFarm.KnownWaterTiles) if (math.all(k == w)) { known = true; break; }
                if (!known && !AIFarm.IsWater(tr, w)) return $"{AIPlaces.Format(r)} — 아는 물 칸이 아님";
                t.Label = $"물 ({r.X},{r.Y})"; t.Center = w; t.ArriveR = 1.5f; t.AreaR = 3f;
                SideStands(tr, me, w, t.Stands);
                return null;
            }
            case "ore":
            {
                var at = new int2(r.X, r.Y);
                if (!AIMemory.OreAt(at, out var ore) || !string.Equals(ore.ore.ToString(), r.Item, StringComparison.OrdinalIgnoreCase))
                    return $"{AIPlaces.Format(r)} — 그 칸의 광석은 기억에 없음(다시 보았을 때 없어 잊었거나 잘못된 식별자). 아는 장소는 보고 places 참고";
                t.Label = $"광석 {ore.ore} ({r.X},{r.Y})"; t.Center = at; t.ArriveR = 1.5f; t.AreaR = 3f;
                SideStands(tr, me, at, t.Stands);
                return null;
            }
            case "station": case "chest": case "pot":
            {
                var at = new int2(r.X, r.Y);
                if (!Enum.TryParse(r.Item, true, out ObjectID id) || id == ObjectID.None) return $"{AIPlaces.Format(r)} — 모르는 품목 '{r.Item}'";
                bool found = AIMemory.StationAt(at, out var st) && st.id == id
                    || AIBase.OwnChests().Exists(c => math.all(c.tile == at) && c.id == id)
                    || AICook.OwnPots().Exists(p => math.all(p.tile == at) && p.id == id);
                if (!found) return $"{AIPlaces.Format(r)} — 기억에 없음(지워졌거나 잘못된 식별자). 아는 장소는 보고 places 참고";
                t.Label = $"{id} ({r.X},{r.Y})"; t.Center = at; t.ArriveR = AICraft.StationRange; t.AreaR = 5f;
                foreach (var s in AICraft.StandTilesFor(server, player, new AIMemory.Station { id = id, tile = at })) if (t.Stands.Count < 3) t.Stands.Add(s);
                return null;
            }
            case "rect": case "line":
            {
                t.Shape = new List<int2>();
                foreach (var p in AIPlaces.ShapeTiles(r)) t.Shape.Add(new int2(p.x, p.y));
                t.Label = AIPlaces.Format(r); t.Center = t.Shape[0]; t.AreaR = 0f;
                return null;
            }
        }
        return $"모르는 장소 '{AIPlaces.Format(r)}'";
    }

    private static void SideStands(AITileReader tr, float3 me, int2 c, List<int2> to)
    {
        foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
        {
            int2 n = c + d;
            if (tr.Walkable(n) && !AIFarm.IsWater(tr, n)) to.Add(n);
        }
        to.Sort((a, b) => math.distance(me.xz, (float2)a).CompareTo(math.distance(me.xz, (float2)b)));
    }

    public static bool Arrived(World server, Entity player, PlaceTarget t, out float dist)
    {
        float3 me = AIBody.Pos(server, player);
        dist = math.distance(me.xz, (float2)t.Center);
        if (t.Ref.Kind == "here") return true;
        if (t.Ref.Kind == "base" && AIEdge.PlanActive && AIEdge.IsInside(AIEdge.StandTile(server, me))) return true;
        if (t.Plots != null) { float m = float.MaxValue; foreach (var p in t.Plots) m = math.min(m, math.distance(me.xz, (float2)p)); dist = m; return m <= 2f; }
        // 검수 F1: on a stand the walk aimed at is there too (a big station's far side, a base stand out to ring 6)
        float sd = float.MaxValue;
        foreach (var st in t.Stands) sd = math.min(sd, math.distance(me.xz, (float2)st));
        return AIPlaces.ArrivedAt(dist, t.ArriveR, sd);
    }

    // close enough to place from here (the place options look SearchR tiles around the AI)
    private static bool InArea(World server, Entity player, PlaceTarget t)
    {
        float3 me = AIBody.Pos(server, player);
        if (t.Ref.Kind == "here") return true;
        if (t.Ref.Kind == "base") return AIBase.AtBase(me) || AIEdge.PlanActive && AIEdge.IsInside(AIEdge.StandTile(server, me));
        if (t.Plots != null) return Arrived(server, player, t, out _);
        return math.distance(me.xz, (float2)t.Center) <= t.AreaR;
    }

    private static bool CountsIn(PlaceTarget t, ObjectID item, int2 at)
    {
        if (t.Ref.Kind == "here" || t.AreaR <= 0f) return true;
        if (t.Ref.Kind == "base" && AIEdge.PlanActive && AIEdge.IsInside(at)) return true;
        int2 sz = AIPlaceSpot.SizeOf(item);   // 실게임 D2: the options' rule (footprint's farthest tile)
        return !AIPlaceGrid.OutsideArea(AIPlaceGrid.Farthest(at.x, at.y, sz.x, sz.y, t.Center.x, t.Center.y), t.AreaR);
    }

    // ------------------------------------------------------------------ checks (AIPlanner.CheckGoal)

    public static string CheckGoto(World server, Entity player, string where)
    {
        string form = AIPlaces.WhereForm("goto", where);
        if (form != null) return form;
        AIPlaces.Parse(where, out var r, out _);
        string err = Resolve(server, player, r, out var t);
        if (err != null) return err;
        if (r.Kind == "here") return "goto here — 이미 지금 자리";
        if (Arrived(server, player, t, out float d)) return $"이미 {t.Label}에 있음({d:F1}칸)";
        return null;
    }

    public static string CheckPlace(World server, Entity player, string item, int count, string where)
    {
        if (!AIBrain.TryItemPublic(item ?? "", out ObjectID id, out _)) return $"모르는 품목 '{item}'";
        if (!IsPlaceable(server, id, out string why)) return why;
        string form = AIPlaces.WhereForm("place", where);
        if (form != null) return form;
        AIPlaces.Parse(where, out var r, out _);
        string cnt = AIPlaces.PlaceCount(r, count, out _);   // 검수 B1: no count is 1
        if (cnt != null) return cnt;
        AIBody.InventoryCounts(server, player).TryGetValue(id, out int have);
        if (have <= 0)
            return AICraft.KnownRecipe(server, player, id, out string at) ? $"가방에 {id} 없음 — 먼저 craft {id}(만드는 곳 {at})" : $"가방에 {id} 없음 — {AICraft.CraftFact(server, player, id)}";
        string err = Resolve(server, player, r, out var t);
        if (err != null) return err;
        if (r.IsShape)
        {
            // 검수 F4: a shape with no tile left to place is refused with what is on its tiles
            var sk = new List<(int2, string)>();
            ShapePlan(server, player, id, t.Shape, out int held, out var todo, sk);
            if (todo.Count == 0)
                return $"{AIPlaces.Format(r)}에 놓을 칸이 없음(이미 {id} 있는 칸 {held}개) — {AIPlaces.SkippedText(sk.ConvertAll(k => (k.Item1.x, k.Item1.y, k.Item2)))}";
        }
        // a station / pot already placed at base by this AI: one more needs another where (chests: several are normal)
        if (r.Kind == "base" && !AIBase.IsChestItem(server, id))
            foreach (var st in AIMemory.AllStations())
                if (st.placed && st.id == id && AIBase.BaseDistTile(st.tile) <= AIBase.BaseRadius)
                    return $"이미 기지에 내가 놓은 {id} 있음 ({st.tile.x},{st.tile.y}) — 하나 더 놓으려면 다른 where를 씀";
        return null;
    }

    // ------------------------------------------------------------------ goal life

    private static PlaceTarget Target(World server, Entity player, out string err)
    {
        var s = S;
        var g = AIBrain.Goal;
        PlaceRef r = s.Ref;
        if (r == null && !AIPlaces.Parse(g.where, out r, out err)) return null;
        err = Resolve(server, player, r, out var t);
        return err == null ? t : null;
    }

    public static bool GoalDone(World server, Entity player, out string text)
    {
        text = "";
        var g = AIBrain.Goal;
        var t = Target(server, player, out _);
        if (t == null) return false;
        if (g.kind == GoalKind.Goto)
        {
            if (!Arrived(server, player, t, out float d)) return false;
            text = $"{t.Label} 도착({d:F1}칸)";
            return true;
        }
        if (g.kind != GoalKind.Place) return false;
        if ((AIBody.Current is PlaceTilesSkill || AIBody.Current is PlaceSkill) && AIBody.Current.Status == SkillStatus.Running) return false;
        var s = S;
        AIBody.InventoryCounts(server, player).TryGetValue(g.item, out int have);
        // 검수 F3/F4: done when nothing is left (skipped tiles not counted), or when the bag ran out after some — with
        // how many went down, how many did not, and the tiles skipped and why
        if (t.Shape != null)
        {
            var left = Remaining(server, player, g.item, t.Shape);
            left.RemoveAll(p => IsSkipped(s, p));
            // 재검수 N1: what this goal placed, not the tiles that held the item before it (those are skipped when taken)
            var at = new List<(int x, int y)>();
            foreach (var p in s.Placed) if (p.item == g.item && p.t >= s.Start) at.Add((p.at.x, p.at.y));
            int placed = AIPlaces.ShapePlaced(t.Shape.ConvertAll(q => (q.x, q.y)), at);
            var end = AIPlaces.EndOf(placed, left.Count, have);
            if (end != AIPlaces.PlaceEnd.Done && end != AIPlaces.PlaceEnd.Short) return false;
            text = AIPlaces.EndText(g.item.ToString(), t.Label, end, placed, t.Shape.Count, true, s.Skipped.Count, AIPlaces.SkippedText(SkipList(s)));
            return true;
        }
        int n = 0; int2 last = default;
        foreach (var p in s.Placed) if (p.item == g.item && p.t >= s.Start && CountsIn(t, g.item, p.at)) { n++; last = p.at; }
        int need = math.max(1, g.count);
        var e2 = AIPlaces.EndOf(n, need - n, have);
        if (e2 != AIPlaces.PlaceEnd.Done && e2 != AIPlaces.PlaceEnd.Short) return false;
        text = AIPlaces.EndText(g.item.ToString(), t.Label, e2, n, need, false, 0, "") + $"(마지막 ({last.x},{last.y}))";
        return true;
    }

    // 검수 F4: the tiles of a shape — already holding the item, to place, refused (the tile itself cannot take it: wall,
    // object, the spawn pad, water for an item that cannot go there, not a floor). Refused ones are added to `skipped`.
    private static void ShapePlan(World server, Entity player, ObjectID item, List<int2> shape, out int held, out List<int2> todo, List<(int2, string)> skipped)
    {
        held = 0; todo = new List<int2>();
        var tr = new AITileReader(server);
        HashSet<int2> occupied;
        try { occupied = AIObjects.OccupiedInView(server, player); } catch { occupied = new HashSet<int2>(); }
        foreach (var p in shape)
        {
            // 재검수 N1: a tile that held the item before the goal is left out with the fact (it is not this goal's placing)
            if (Holds(server, tr, item, p)) { held++; if (!skipped.Exists(k => math.all(k.Item1 == p))) skipped.Add((p, $"이미 {item} 놓여 있음")); continue; }
            string why = TileRefusal(tr, occupied, item, p);
            if (why != null) { if (!skipped.Exists(k => math.all(k.Item1 == p))) skipped.Add((p, why)); continue; }
            todo.Add(p);
        }
    }

    private static string TileRefusal(AITileReader tr, HashSet<int2> occupied, ObjectID item, int2 anchor)
    {
        int2 size = AIPlaceSpot.SizeOf(item);
        for (int fx = 0; fx < size.x; fx++)
            for (int fy = 0; fy < size.y; fy++)
            {
                int2 f = anchor + new int2(fx, fy);
                string np = AIPlaceSpot.NoPlaceText(tr, f);
                if (np != null) return np;
                if (tr.HasWall(f, out _, out _, out _)) return "이미 벽이 있음";
                if (occupied.Contains(f)) return "이미 물건이 있음";
                bool water = AIFarm.IsWater(tr, f) && !AIPlaceSpot.IsLava(tr, f);
                if (water && !AIPlaceSpot.CanGoOnWater(item)) return "물 위에는 놓을 수 없음(게임 설정)";
                if (!water && !AIPlaceSpot.CanGoOnLand(item)) return "물 위에만 놓임(물 칸이 아님)";
                if (!tr.Walkable(f) && !water) return $"걷는 칸이 아님({tr.Top(f).tileType})";
            }
        return null;
    }

    private static bool Holds(World server, AITileReader tr, ObjectID item, int2 p)
    {
        ObjectInfo info = PugDatabase.GetObjectInfo(item, 0);
        return info != null && info.tileType != TileType.none
            ? tr.Layers(p).Exists(l => l.tileType == info.tileType)
            : AIPlaceSpot.EntityOn(server, item, p, new int2(1, 1), null) != Entity.Null;
    }

    // tiles of a shape that do not hold the item yet (a tile item: its layer; an object: one on the tile)
    private static List<int2> Remaining(World server, Entity player, ObjectID item, List<int2> shape)
    {
        var res = new List<int2>();
        var tr = new AITileReader(server);
        foreach (var p in shape) if (!Holds(server, tr, item, p)) res.Add(p);
        return res;
    }

    public static string StallNote(World server, Entity player)
    {
        var g = AIBrain.Goal;
        AIBody.InventoryCounts(server, player).TryGetValue(g.item, out int have);
        var s = S;
        string why = string.IsNullOrEmpty(s.LastWhy) ? "진행할 후보 없음" : s.LastWhy;
        string sk = AIPlaces.SkippedText(SkipList(s));   // 검수 F4
        string rf = string.IsNullOrEmpty(s.RouteFact) || why.Contains(s.RouteFact) ? "" : " · 가는 길: " + s.RouteFact;   // 실게임 D1
        return g.kind == GoalKind.Place ? $"{why} · 가방에 {g.item} {have}개{(sk == "" ? "" : " · " + sk)}{rf}" : why + rf;
    }

    // ------------------------------------------------------------------ options for the action model

    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey)
    {
        var res = new List<AICandidate>();
        var s = S;
        var g = AIBrain.Goal;
        var t = Target(server, player, out string err);
        if (t == null) { s.LastWhy = err; return res; }
        float3 me = o.pos;
        if (g.kind == GoalKind.Goto)
        {
            if (!Arrived(server, player, t, out _)) Go(server, player, curKey, t, "목표 — 그곳으로 가기", res);
            return res;
        }
        if (g.kind != GoalKind.Place) return res;
        AIBody.InventoryCounts(server, player).TryGetValue(g.item, out int have);
        if (have <= 0) { s.LastWhy = $"가방에 {g.item} 없음"; return res; }
        if (t.Shape != null)
        {
            var left = Remaining(server, player, g.item, t.Shape);
            left.RemoveAll(p => IsSkipped(s, p));   // 검수 F4
            if (left.Count == 0) { if (s.Skipped.Count > 0) s.LastWhy = $"남은 칸은 모두 놓을 수 없음 — {AIPlaces.SkippedText(SkipList(s))}"; return res; }
            left.Sort((a, b) => math.distance(me.xz, (float2)a).CompareTo(math.distance(me.xz, (float2)b)));
            int2 first = left[0];
            if (math.distance(me.xz, (float2)first) > 6f)
            {
                var near = new PlaceTarget { Ref = t.Ref, Label = $"{t.Label}의 가장 가까운 칸 ({first.x},{first.y})", Center = first };
                var tr = new AITileReader(server);
                if (tr.Walkable(first)) near.Stands.Add(first); else SideStands(tr, me, first, near.Stands);
                Go(server, player, curKey, near, $"{g.item}을(를) {t.Label}에 놓으려고", res);
                return res;
            }
            var order = Chain(left, first);
            string k = $"placetiles:{g.item}@{first.x},{first.y}";
            if (curKey.StartsWith("placetiles:")) return res;   // the running batch goes on ("continue")
            string shortNote = have < order.Count ? $" 가방에 {g.item} {have}개, 칸 {order.Count}개 — {have}칸만 놓임." : "";
            string edge = AIEdge.ShapeEdgeNote(order);
            res.Add(new AICandidate
            {
                kind = "placetiles", key = k, itemId = g.item, tiles = order, progress = true,
                desc = $"{g.item} 여러 칸 놓기 — {t.Label}에서 남은 {order.Count}칸을 가까운 칸부터 차례로({string.Join(" ", order.GetRange(0, math.min(6, order.Count)).ConvertAll(p => $"({p.x},{p.y})"))}{(order.Count > 6 ? " …" : "")}).{shortNote}{(edge == "" ? "" : " " + edge + ".")} 칸마다 옆에 서서 놓고, 놓을 수 없는 칸은 건너뛰고 까닭을 남김.",
            });
            return res;
        }
        if (!InArea(server, player, t))
        {
            Go(server, player, curKey, t, $"{g.item}을(를) 놓으려고", res);
            return res;
        }
        int done = 0; foreach (var p in s.Placed) if (p.item == g.item && p.t >= s.Start && CountsIn(t, g.item, p.at)) done++;
        var area = t.AreaR > 0f ? new PlaceArea { center = t.Center, radius = t.AreaR, word = t.Label } : null;
        var opts = AIPlaceSpot.Candidates(server, player, g.item, $"{g.item} 놓기", $"까닭: 목표 — {g.item}을(를) {t.Label}에 놓기({done}/{math.max(1, g.count)}).", true, out string none, true, area);
        if (opts.Count == 0) s.LastWhy = none ?? $"{t.Label}에서 {g.item} 놓을 자리가 없음";
        res.AddRange(opts);
        return res;
    }

    // tiles in a walk order: from `first`, the nearest left each time
    private static List<int2> Chain(List<int2> left, int2 first)
    {
        var rest = new List<int2>(left); var res = new List<int2>();
        int2 at = first;
        while (rest.Count > 0 && res.Count < AIPlaces.MaxShapeTiles)
        {
            int bi = 0; float bd = float.MaxValue;
            for (int i = 0; i < rest.Count; i++) { float d = math.distance((float2)at, (float2)rest[i]); if (d < bd) { bd = d; bi = i; } }
            at = rest[bi]; res.Add(at); rest.RemoveAt(bi);
        }
        return res;
    }

    // the walk to a place: the dig option ("go to X", AIBrain.PlanC -> TunnelSkill) to one of its stands (3 tried)
    private static void Go(World server, Entity player, string curKey, PlaceTarget t, string purpose, List<AICandidate> res)
    {
        var s = S;
        float3 me = AIBody.Pos(server, player);
        if (t.Stands.Count == 0) { s.LastWhy = $"{t.Label} 둘레에 아는 걸을 수 있는 칸 없음"; return; }
        string why = "";
        for (int i = 0; i < t.Stands.Count && i < 3; i++)
        {
            int2 st = t.Stands[i];
            string k = $"dig:{st.x},{st.y}";
            if (k == curKey || $"digedge:{st.x},{st.y}" == curKey) return;   // walking there now ("continue"; 우회 사실: the edge way too)
            var plan = AIBrain.PlanC(server, player, st, out why, true);   // 검수 H1: the detour check (its options offer the edge way)
            if (plan == null) continue;
            // 실게임 D1: a route through the base edge says so (the edge wall sentences, AIBaseGuard); the walk stops if a
            // replan while running finds edge walls this option did not state (TunnelSkill.EdgeOk)
            var edge = DigEdge(plan);
            string edgeText = EdgeText(plan, edge);
            s.RouteFact = edgeText.Trim();
            var walkEdgeOk = edge.ConvertAll(e => new int2(e.t.x, e.t.y));
            var walk = AIApproach.WalkCandidate(st, t.Label, true,
                $"{t.Label}(으)로 가기 — {purpose}. 여기서 {math.distance(me.xz, (float2)t.Center):F0}칸. 아는 길 {plan.path.Count}칸{(plan.walls.Count > 0 ? $", 부술 벽 {plan.walls.Count}개({plan.WallSummary()})" : ", 부술 벽 0개")}{(DoorsOn(plan) is string dr ? ", " + dr : "")}, 예상 약 {plan.path.Count / 2.4f + plan.totalHits * 0.5f:F0}초.{edgeText}{DetourNote(plan)} ({st.x},{st.y})에 서면 끝.");
            walk.digEdgeOk = walkEdgeOk;
            res.Add(walk);
            var alt = DetourOption(plan, st, t.Label, $"{t.Label}(으)로 가기({purpose})", $"({st.x},{st.y})에 서면 끝.");   // 우회 사실
            if (alt != null) res.Add(alt);
            return;
        }
        s.LastWhy = $"{t.Label}(으)로 갈 길 없음({why})";
        LogChanged("route", $"PLACEGOAL: no route to {t.Label}: {why}");
    }

    // ------------------------------------------------------------------ report (§2.6)

    // a station that crafts things (not a cooking pot, a boss statue / altar / shrine, or one that only cooks food)
    private static bool IsCraftStation(World server, ObjectID id)
    {
        string n = id.ToString();
        if (AICook.IsPotItem(server, id) || n.Contains("Statue") || n.Contains("Altar") || n.Contains("Shrine")) return false;
        foreach (var r in AICraft.PrefabRecipes(server, id)) if (!AICook.IsCookedFood(server, r, 0)) return true;
        return false;
    }

    // "places":[...],"places_more":{...} (no leading comma)
    public static string ReportFields(World server, Entity player, float3 pos, Func<string, string> esc)
    {
        var lines = new List<PlaceLine>();
        float D(int2 t) => math.distance(pos.xz, (float2)t);
        string Ago(long u) => u > 0 ? $", {AIMemory.Ago(u)}에 봄" : "";
        bool AtBase(int2 t) => AIBase.BaseDistTile(t) <= AIBase.BaseRadius;
        lines.Add(new PlaceLine { Group = "fixed", Dist = -3, Text = $"base 기지 {AIBase.BaseText}, {D(AIBase.BaseTile):F0}칸{(AIEdge.PlanActive ? ", 둘레 있음" : "")}" });
        if (!math.all(AIBase.BaseTile == int2.zero)) lines.Add(new PlaceLine { Group = "fixed", Dist = -2, Text = $"core 코어 (0,0), {D(int2.zero):F0}칸" });
        var plots = new List<int2>(AIFarm.OwnPlotTiles);
        if (plots.Count > 0) { float m = float.MaxValue; foreach (var p in plots) m = math.min(m, D(p)); lines.Add(new PlaceLine { Group = "fixed", Dist = -1, Text = $"farm 내 밭 {plots.Count}칸, {m:F0}칸" }); }
        try
        {
            var potTiles = new HashSet<int2>();
            foreach (var p in AICook.OwnPots()) { potTiles.Add(p.tile); lines.Add(new PlaceLine { Group = "mine", Sub = "pot", Dist = D(p.tile), Text = $"{AIPlaces.ObjectId("pot", p.id.ToString(), p.tile.x, p.tile.y)} 내 요리 솥, {D(p.tile):F0}칸{(AtBase(p.tile) ? ", 기지 안" : "")}{Ago(p.seenUnix)}" }); }
            foreach (var c in AIBase.OwnChests()) lines.Add(new PlaceLine { Group = "mine", Sub = "chest", Dist = D(c.tile), Text = $"{AIPlaces.ObjectId("chest", c.id.ToString(), c.tile.x, c.tile.y)} 내 상자, {D(c.tile):F0}칸{(AtBase(c.tile) ? ", 기지 안" : "")}{Ago(c.seenUnix)}" });
            foreach (var st in AIMemory.AllStations())
            {
                if (potTiles.Contains(st.tile)) continue;
                if (!st.placed && !IsCraftStation(server, st.id)) continue;   // 검수 S3: others' stations are crafting ones only
                lines.Add(new PlaceLine { Group = st.placed ? "mine" : "other", Sub = "station", Dist = D(st.tile),
                    Text = $"{AIPlaces.ObjectId("station", st.id.ToString(), st.tile.x, st.tile.y)} 제작대, {D(st.tile):F0}칸{(AtBase(st.tile) ? ", 기지 안" : "")}{(st.placed ? ", 내가 놓음" : "")}{Ago(st.seenUnix)}" });
            }
            foreach (var (p, ore) in AIMemory.AllOres())
                lines.Add(new PlaceLine { Group = "ore", Sub = ore.ore.ToString(), Dist = D(p), Text = $"{AIPlaces.ObjectId("ore", ore.ore.ToString(), p.x, p.y)} 광석 벽, {D(p):F0}칸{Ago(ore.seenUnix)}" });
            foreach (var w in AIFarm.KnownWaterTiles) lines.Add(new PlaceLine { Group = "water", Dist = D(w), Text = $"water:{w.x},{w.y} 아는 물, {D(w):F0}칸" });
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("place: places report failed", e); }
        var pick = AIPlaces.Pick(lines, out var more);
        var sb = new StringBuilder();
        sb.Append("\"places\":[");
        for (int i = 0; i < pick.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(esc(pick[i])).Append('"'); }
        sb.Append("],\"places_more\":{");
        int j = 0; foreach (var kv in more) { if (j++ > 0) sb.Append(','); sb.Append($"\"{esc(kv.Key)}\":{kv.Value}"); }
        sb.Append('}');
        return sb.ToString();
    }
}

// Several tiles of one item in order (a place goal's rect / line, §2.11): PlaceSkill on each tile from a stand next to it
// (the stand rule of the place options), a tile that already holds the item is skipped. Ends: all placed (Done), the
// bag ran out (Failed, how far it got), a tile that does not take (Failed: which and why). The loop of EdgeFillSkill
// without the edge's meaning (the closing tile, the inside stand) — the place options' facts say what a tile does.
public class PlaceTilesSkill : AISkill
{
    private readonly ObjectID _item;
    private readonly List<int2> _tiles;
    private int _i, _done;
    private PlaceSkill _cur;
    private readonly List<string> _log = new List<string>();
    public PlaceTilesSkill(ObjectID item, List<int2> tiles) { Name = "여러 칸 놓기"; _item = item; _tiles = tiles ?? new List<int2>(); Key = _tiles.Count > 0 ? $"placetiles:{item}@{_tiles[0].x},{_tiles[0].y}" : $"placetiles:{item}"; }

    public override bool Start(World server, Entity player, float now)
    {
        if (_tiles.Count == 0) { Status = SkillStatus.Failed; Reason = "놓을 칸이 없음"; return false; }
        Facts.Add($"{_item} x{_tiles.Count}: [{string.Join(" ", _tiles.ConvertAll(t => $"({t.x},{t.y})"))}]");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var tr = new AITileReader(server);
        ObjectInfo info = PugDatabase.GetObjectInfo(_item, 0);
        while (_cur == null)
        {
            if (_i >= _tiles.Count) return Finish(SkillStatus.Done, $"{_item} {_done}칸 놓음({string.Join(", ", _log)})");
            int2 t = _tiles[_i];
            bool has = info != null && info.tileType != TileType.none ? tr.Layers(t).Exists(l => l.tileType == info.tileType) : AIPlaceSpot.EntityOn(server, _item, t, new int2(1, 1), null) != Entity.Null;
            if (has) { _i++; _log.Add($"({t.x},{t.y}) 이미 있음"); continue; }
            AIBody.InventoryCounts(server, player).TryGetValue(_item, out int have);
            if (have <= 0) return Finish(SkillStatus.Failed, $"재료 떨어짐: {_tiles.Count}칸 중 {_done}칸 놓음, 남은 {_tiles.Count - _i}칸(다음 ({t.x},{t.y}))");
            _cur = new PlaceSkill(_item, t);
            // 검수 F4: a tile that does not take is skipped (named with why for the goal's end / stall), the rest go on
            if (!_cur.Start(server, player, now))
            {
                string why = _cur.Reason; bool tileFault = _cur.TileFault; _cur = null;
                // 재검수 N2: a cause that is not the tile (the bag, the hand slot) ends the batch instead of skipping every tile
                if (AIPlaces.OnTileStartFail(tileFault) == AIPlaces.TileStartFail.Stop)
                {
                    try { AIPlaceGoal.NoteStop(why); } catch (Exception e) { AIPlayerSpikeMod.LogError("place: note stop failed", e); }
                    return Finish(SkillStatus.Failed, $"멈춤: {why}; {_tiles.Count}칸 중 {_done}칸 놓은 뒤");
                }
                Skip(t, why); continue;
            }
            _cur.StartTime = now;
        }
        var bi = _cur.Tick(server, player, now);
        if (_cur.Status == SkillStatus.Running) return bi;
        var r = _cur; _cur = null; r.Cleanup(server);
        int2 tt = _tiles[_i];
        AIPlayerSpikeMod.Log($"SKILL {Name}: ({tt.x},{tt.y}) {r.Status}: {r.Reason}");
        if (r.Status == SkillStatus.Done) { _done++; _i++; _log.Add($"({tt.x},{tt.y})"); return BodyInput.Idle; }
        Skip(tt, r.Reason);
        return BodyInput.Idle;
    }

    private void Skip(int2 t, string why)
    {
        _i++; _log.Add($"({t.x},{t.y}) 건너뜀");
        try { AIPlaceGoal.NoteSkip(t, why); } catch (Exception e) { AIPlayerSpikeMod.LogError("place: note skip failed", e); }
    }

    public override void Cleanup(World server) { if (_cur != null) { _cur.Cleanup(server); _cur = null; } }
}
