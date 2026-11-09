using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Observed walkability and direction-only corridor decisions. Knowledge acquired by the
// lookahead exception never enters this map, the general observation, or resource memory.
public static class AITravel
{
    private sealed class State
    {
        public string World, Path;
        public float NextObserve, NextSave, ResponseSeconds = 1.5f;
        public bool Dirty;
        public readonly Dictionary<AITravelRules.Cell, bool> Map = new Dictionary<AITravelRules.Cell, bool>();
    }
    [Serializable] public class MemoryData { public int version = 1; public int[] xy = new int[0]; public bool[] walk = new bool[0]; }
    private static State S => AIAgent.Cur.Get<State>(AISlot.Travel);
    internal static AITravelRules.Cell Cell(int2 p) => new AITravelRules.Cell(p.x, p.y);
    internal static int2 Tile(AITravelRules.Cell p) => new int2(p.X, p.Y);
    private static State Ensure()
    {
        var s = S; string world = AIPlayerSpikeMod.WorldId ?? "";
        if (s.World == world) return s;
        Save(s); // retain the last observation before switching worlds, even inside the periodic-save window
        s.World = world; s.Map.Clear(); s.NextObserve = s.NextSave = 0; s.Dirty = false;
        s.Path = string.IsNullOrEmpty(world) ? null : System.IO.Path.Combine(AIMemory.CharDir(world), "travel-memory.json");
        if (s.Path != null && File.Exists(s.Path))
            try
            {
                var d = JsonUtility.FromJson<MemoryData>(File.ReadAllText(s.Path, Encoding.UTF8));
                if (d != null && d.xy != null && d.walk != null)
                    for (int i = 0; i < d.walk.Length && i * 2 + 1 < d.xy.Length; i++)
                        s.Map[new AITravelRules.Cell(d.xy[i * 2], d.xy[i * 2 + 1])] = d.walk[i];
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("travel: remembered geometry could not be read", e); }
        return s;
    }
    internal static void ObserveView(World server, Entity player, bool force = false)
    {
        var s = Ensure(); float now = Time.realtimeSinceStartup;
        if (!force && now < s.NextObserve) return;
        s.NextObserve = now + 0.5f;
        var pos = AIBody.Pos(server, player); var at = AIBody.TileOf(pos); var half = AIVision.HalfExtents();
        var tr = new AITileReader(server); var objects = AIObjects.BlockedInView(server, player, out _);
        for (int x = at.x - (int)Math.Ceiling(half.x); x <= at.x + (int)Math.Ceiling(half.x); x++)
            for (int y = at.y - (int)Math.Ceiling(half.y); y <= at.y + (int)Math.Ceiling(half.y); y++)
            {
                var p = new int2(x, y); if (!AIVision.CanSee(pos, p)) continue;
                // An owned door is a known passage with an existing open/pass/close executor.
                bool walk = tr.Walkable(p) && (!objects.Contains(p) || AIEdge.IsWalkDoorTile(p));
                var key = Cell(p);
                if (!s.Map.TryGetValue(key, out bool old) || old != walk) { s.Map[key] = walk; s.Dirty = true; }
            }
        if (s.Dirty && s.Path != null && now >= s.NextSave)
        {
            s.NextSave = now + 10f;
            Save(s);
        }
    }
    private static void Save(State s)
    {
        if (!s.Dirty || s.Path == null) return;
        var d = new MemoryData { xy = new int[s.Map.Count * 2], walk = new bool[s.Map.Count] };
        int i = 0; foreach (var kv in s.Map) { d.xy[i * 2] = kv.Key.X; d.xy[i * 2 + 1] = kv.Key.Y; d.walk[i++] = kv.Value; }
        try { File.WriteAllText(s.Path, JsonUtility.ToJson(d), Encoding.UTF8); s.Dirty = false; }
        catch (Exception e) { AIPlayerSpikeMod.LogError("travel: could not save observed geometry", e); }
    }
    internal static bool KnownWalkable(int2 p) => Ensure().Map.TryGetValue(Cell(p), out bool w) && w;
    internal static bool Known(int2 p) => Ensure().Map.ContainsKey(Cell(p));
    internal static HashSet<int2> SnapshotKnown()
    {
        var result = new HashSet<int2>(); foreach (var p in Ensure().Map.Keys) result.Add(Tile(p)); return result;
    }
    internal static List<int2> KnownPath(int2 start, int2 goal, Func<int2, bool> allowed = null, float tolerance = 0f)
    {
        var path = AITravelRules.KnownPath(Cell(start), Cell(goal), Ensure().Map, p => allowed == null || allowed(Tile(p)), tolerance);
        return path == null ? null : path.ConvertAll(Tile);
    }
    internal static bool CanApproach(int2 at, int2 goal, float tolerance)
        => AITravelRules.Approach(Cell(at), Cell(goal), tolerance, Ensure().Map, null);
    internal static float LeadDistance => AITravelRules.LeadDistance(AIBody.WalkSpeed, S.ResponseSeconds);

    public static List<AICandidate> Compose(World server, Entity player, AIObservation observation, List<AICandidate> candidates, float responseSeconds = 1.5f)
    {
        ObserveView(server, player);
        S.ResponseSeconds = Math.Max(0f, responseSeconds);
        candidates.RemoveAll(c => c.kind == "explore");
        if (AIBody.Current is TravelSkill active && active.Status == SkillStatus.Running)
        {
            active.AddChoices(server, player, candidates);
            return candidates;
        }
        var pos = observation.pos; var at = AIBody.TileOf(pos); var tr = new AITileReader(server);
        var objects = AIObjects.BlockedInView(server, player, out _);
        foreach (var next in AITravelRules.Exits(Cell(at), null, p => AIVision.CanSee(pos, Tile(p)) && tr.Walkable(Tile(p)) && !objects.Contains(Tile(p))))
        {
            string dir = AITravelRules.Direction(Cell(at), next);
            candidates.Add(new AICandidate { kind = "explore", key = "travel:explore:" + at.x + "," + at.y + ":" + next,
                tile = Tile(next), exploreDirection = dir, progress = AIBrain.Goal.kind == GoalKind.Explore,
                desc = "통로 탐색 — 지금 보이는 " + dir + "쪽 길로 출발. 외길은 코너를 따라 계속 걷고, 다음 갈림길은 방향을 선택. 막힘·발견·위험은 다시 판단." });
        }
        return candidates;
    }
    public static AISkill Make(AICandidate candidate) => new TravelSkill(candidate.tile) { Key = candidate.key };
    public static string Stale(World server, Entity player, AICandidate c)
    {
        if (c.kind == "travel_turn")
        {
            if (!(AIBody.Current is TravelSkill current) || current.Status != SkillStatus.Running || current.Run != c.run)
                return "갈림길을 질문한 탐색 실행이 바뀜";
            return current.ValidateTurn(server, player, c);
        }
        var pos = AIBody.Pos(server, player); var at = AIBody.TileOf(pos);
        if (Math.Abs(at.x - c.tile.x) + Math.Abs(at.y - c.tile.y) > 1 || !AIVision.CanSee(pos, c.tile)) return "출발 위치 또는 보이는 통로가 바뀜";
        var tr = new AITileReader(server);
        return tr.Walkable(c.tile) && !AIObjects.BlockedInView(server, player, out _).Contains(c.tile) ? null : "선택한 통로가 지금 막힘";
    }
    public static bool TryApplyTurn(World server, Entity player, AICandidate c, out string why)
    {
        why = Stale(server, player, c);
        if (why != null) return false;
        if (!(AIBody.Current is TravelSkill current)) { why = "탐색 실행 없음"; return false; }
        current.ReserveTurn(c); return true;
    }
}

// One selected corridor intent. The body does not stop for every corner; it holds at a
// branch until a model-selected exit for the same observed junction is available.
public sealed class TravelSkill : AISkill
{
    private AITravelRules.Cell _at, _previous, _next;
    private readonly int2 _first;
    private bool _moving, _started, _halted;
    private string _selectedToken;
    private AITravelRules.Cell _selectedExit;
    private AITravelRules.Junction _junction;
    private readonly AINavigator _nav = new AINavigator();
    private float _lastForecast = -1;
    private string _announced;
    private readonly HashSet<string> _walkedEdges = new HashSet<string>();
    private int _corridorWidth;
    public TravelSkill(int2 first) { _first = first; Name = "통로 탐색"; Key = "travel:explore:" + first.x + "," + first.y; }
    public override bool Start(World server, Entity player, float now)
    {
        AITravel.ObserveView(server, player, true);
        _at = AITravel.Cell(AIBody.TileOf(AIBody.Pos(server, player)));
        _previous = _at; _next = AITravel.Cell(_first);
        if (Math.Abs(_at.X - _next.X) + Math.Abs(_at.Y - _next.Y) != 1 || !VisibleOpen(server, player, _next))
        { Status = SkillStatus.Failed; Reason = "선택한 출발 통로가 지금 열려 있지 않음"; return false; }
        BeginStep(server, player, now, _next); _started = true;
        return true;
    }
    private bool VisibleOpen(World server, Entity player, AITravelRules.Cell p)
        => VisiblePredicate(server, player)(p);
    private Func<AITravelRules.Cell, bool> VisiblePredicate(World server, Entity player)
    {
        var pos = AIBody.Pos(server, player); var tr = new AITileReader(server);
        var blocked = AIObjects.BlockedInView(server, player, out _);
        var cache = new Dictionary<AITravelRules.Cell, bool>();
        return p =>
        {
            if (cache.TryGetValue(p, out bool open)) return open;
            var tile = AITravel.Tile(p);
            open = AIVision.CanSee(pos, tile) && tr.Walkable(tile) && !blocked.Contains(tile);
            cache[p] = open; return open;
        };
    }
    private bool BeginStep(World server, Entity player, float now, AITravelRules.Cell target)
    {
        if (!_walkedEdges.Add(_at + ">" + target))
        {
            _moving = false; _halted = true;
            ObserveOnce("cycle:" + _at + ">" + target, "이미 지나온 통로를 같은 방향으로 다시 순환하려 함. 자동 반복을 멈추고 원래 목적에 맞는 탐색 방향·다른 작업을 다시 선택.");
            return false;
        }
        int width = AITravelRules.Width(_at, new AITravelRules.Cell(target.X - _at.X, target.Y - _at.Y), VisiblePredicate(server, player));
        _corridorWidth = _corridorWidth > 0 ? Math.Min(_corridorWidth, width) : width;
        _next = target; _moving = true;
        _nav.Dispose(server); _nav.Start(server, player, AITravel.Tile(target), 0.18f, now);
        return true;
    }
    private void Forecast(World server, Entity player, bool force = false)
    {
        float now = Time.realtimeSinceStartup;
        if (!force && now - _lastForecast < 0.2f) return;
        _lastForecast = now;
        var pos = AIBody.Pos(server, player); var tr = new AITileReader(server);
        var objects = AIObjects.BlockedInView(server, player, out _);
        float radius = AITravel.LeadDistance;
        // Only the next junction's direction list is returned. These bounded reads do not
        // call ObserveView, AIMemory.Observe, or write geometry to the travel map.
        bool Look(AITravelRules.Cell p)
        {
            var t = AITravel.Tile(p);
            return (AIVision.CanSee(pos, t) || math.distance(pos.xz, (float2)t) <= radius + 1f) && tr.Walkable(t) && !objects.Contains(t);
        }
        var at = _moving ? _next : _at; var previous = _moving ? _at : _previous;
        _junction = AITravelRules.NextJunction(at, previous, radius, Look, _corridorWidth);
        if (_junction != null && math.distance(pos.xz, (float2)AITravel.Tile(_junction.At)) > radius) _junction = null;
        if (_junction != null && _announced != _junction.Token)
        {
            _announced = _junction.Token;
            AIBrain.ObserveExecution("다음 갈림길 방향 선택 필요: " + string.Join("·", _junction.Exits.ConvertAll(p => AITravelRules.Direction(_junction.At, p))));
        }
    }
    public void AddChoices(World server, Entity player, List<AICandidate> candidates)
    {
        if (_halted)
        {
            var at = AITravel.Cell(AIBody.TileOf(AIBody.Pos(server, player)));
            foreach (var exit in AITravelRules.Exits(at, null, VisiblePredicate(server, player)))
                candidates.Add(new AICandidate { kind = "explore", key = "travel:restart:" + Run + ":" + exit,
                    tile = AITravel.Tile(exit), progress = AIBrain.Goal.kind == GoalKind.Explore, desc = "막힘·순환에서 새로 " + AITravelRules.Direction(at, exit) + "쪽 보이는 통로 선택. 원래 목적 유지." });
            return;
        }
        Forecast(server, player, true);
        if (_junction != null)
            foreach (var exit in _junction.Exits)
                candidates.Add(new AICandidate { kind = "travel_turn", key = "travel:turn:" + Run + ":" + _junction.Token + ":" + exit,
                    run = Run, tile = AITravel.Tile(exit), actionMode = _junction.Token, progress = true,
                    desc = "다음 갈림길에서 " + AITravelRules.Direction(_junction.At, exit) + "쪽 통로 선택. 그 전의 공통 통로는 계속 걷고, 도착 시 방향이 여전히 유효한지 확인. 너머 자원·위험·연결은 미확인." });
        else if (!_moving && VisibleOpen(server, player, _previous))
            candidates.Add(new AICandidate { kind = "explore", key = "travel:reverse:" + Run, tile = AITravel.Tile(_previous),
                desc = "통로 탐색 방향 바꾸기 — 방금 지나온 보이는 길로 돌아간 뒤 다음 갈림길에서 다시 선택.", progress = AIBrain.Goal.kind == GoalKind.Explore });
    }
    public string ValidateTurn(World server, Entity player, AICandidate c)
    {
        if (_halted) return "통로 이동이 막혀 새 수단 판단이 필요함";
        Forecast(server, player, true);
        return AITravelRules.CanUseTurn(_junction, c.actionMode, AITravel.Cell(c.tile)) ? null : "질문 뒤 갈림길의 위치·진입방향·출구가 달라짐";
    }
    public void ReserveTurn(AICandidate c) { _selectedToken = c.actionMode; _selectedExit = AITravel.Cell(c.tile); }
    public override BodyInput Tick(World server, Entity player, float now)
    {
        AITravel.ObserveView(server, player);
        if (_halted) return BodyInput.Idle;
        Forecast(server, player);
        if (_moving)
        {
            if (!VisibleOpen(server, player, _next))
            {
                _moving = false; _halted = true; _nav.Dispose(server);
                ObserveOnce("blocked:" + _next, "선택 통로가 지금 막혀 있음. 원래 이동 목적을 유지하며 실행 가능한 걷기·채굴·배치·재계획 후보를 비교해야 함. 미지의 길은 우회로로 확인되지 않음.");
                return BodyInput.Idle;
            }
            var result = _nav.Tick(server, player, now, out float2 move);
            if (result == NavStatus.Moving) return new BodyInput { move = move, slot = -1 };
            _nav.Dispose(server); _moving = false;
            if (result == NavStatus.Failed)
            {
                _halted = true;
                ObserveOnce("nav:" + _next + ":" + _nav.FailReason, "선택 통로 이동 실패: " + _nav.FailReason + ". 새 수단은 판단모델이 선택.");
                return BodyInput.Idle;
            }
            _previous = _at; _at = _next;
        }
        var visible = VisiblePredicate(server, player);
        var exits = AITravelRules.PassageExits(_at, _previous, visible, _corridorWidth);
        if (_selectedToken != null && _selectedToken.StartsWith(_at + "|", StringComparison.Ordinal))
        {
            var actual = AITravelRules.NextJunction(_at, _previous, 0f, visible, _corridorWidth);
            if (!AITravelRules.CanUseTurn(actual, _selectedToken, _selectedExit))
            {
                _selectedToken = null; _halted = true;
                ObserveOnce("changed:" + _at, "도착 전에 선택한 갈림길의 출구가 달라짐. 예약한 방향을 폐기하고 현재 보이는 선택지를 다시 판단.");
                return BodyInput.Idle;
            }
        }
        if (exits.Count == 1)
        {
            return BeginStep(server, player, now, exits[0]) ? TickMove(server, player, now) : BodyInput.Idle;
        }
        if (exits.Count > 1)
        {
            // Reconstruct from the current visible geometry, not the earlier lookahead.
            var actual = AITravelRules.NextJunction(_at, _previous, 0f, visible, _corridorWidth);
            if (AITravelRules.CanUseTurn(actual, _selectedToken, _selectedExit))
            {
                var choice = _selectedExit; _selectedToken = null; _announced = null;
                _corridorWidth = 0;
                return BeginStep(server, player, now, choice) ? TickMove(server, player, now) : BodyInput.Idle;
            }
            _selectedToken = null;
            ObserveOnce("waiting:" + actual.Token, "갈림길 공통 구간 끝에 도착. 선택하지 않은 갈래에 들어가지 않고 방향 응답을 기다리며 다른 행동도 선택 가능.");
        }
        else
        {
            _halted = true;
            ObserveOnce("end:" + _at + ":" + _previous, "현재 보이는 통로 끝 또는 막힘. 원래 목적은 유지; 되돌아가기·가능한 다른 수단·재계획을 직접 선택.");
        }
        return BodyInput.Idle;
    }
    private BodyInput TickMove(World server, Entity player, float now)
    {
        _nav.Tick(server, player, now, out float2 move);
        return new BodyInput { move = move, slot = -1 };
    }
    public override string Progress(World server, Entity player)
        => !_started ? "통로 탐색 준비" : _moving ? "선택한 통로 탐색 중. 외길의 코너는 계속 조향, 다음 갈림길에서 모델이 방향 선택." : "통로 끝·막힘·갈림길에서 다음 행동 판단 중. 원래 목적 유지.";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
