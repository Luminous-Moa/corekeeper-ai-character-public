// AINavigator — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// ------------------------------------------------------------------------------------------ navigation

public enum NavStatus { Moving, Arrived, Failed }

// Walks to a tile. First tries the game's own pathfinding (a helper entity with PathFindCD + PathFindBfsCD +
// PathFindNodeBuffer whose startEntity is the AI player; PathFindSystem fills the next 3-5 nodes). If the helper
// produces no nodes within 1 s, falls back to a small A* on the tile map (walkable = top tile IsWalkableTile).
public class AINavigator
{
    public int2 Target;
    public float Tolerance;
    public string Mode = "";            // "game-bfs" | "mod-astar"
    public string FailReason = "";
    public SkillEnd FailCode;   // the same failure as a value (refactor S2): NoPath / Stuck / Timeout, set with FailReason
    public int StuckCount;
    public int Replans;
    private Entity _helper = Entity.Null;
    private float _start;
    private float _budget;
    private float _lastProgressT;
    private float3 _lastProgressPos;
    private List<int2> _path;
    private int _pathIdx;
    private float _searchRetryAt;
    private bool _knownSearchCapped;
    internal static bool LastAStarCapped { get; private set; }
    private bool _helperEverHadNodes;
    private readonly HashSet<string> _observations = new HashSet<string>();

    private void ObserveOnce(string key, string reason)
    {
        if (_observations.Add(key)) AIBrain.ObserveExecution($"이동 ({Target.x},{Target.y}): {reason}");
    }

    // targetEntity: when walking to a creature, the game's pathfinding is given the entity (its collider then does not
    // block its own tile; PathFindSystem.GetBlockedPositionsFromColliderHits skips targetEntity).
    public void Start(World server, Entity player, int2 target, float tolerance, float now, Entity targetEntity = default)
    {
        _targetEntity = targetEntity;
        Target = target;
        _searchRetryAt = 0f;
        Tolerance = tolerance;
        _start = now;
        float3 p = AIBody.Pos(server, player);
        _budget = math.distance(p.xz, (float2)target) * 1.5f + 6f;
        if (!_restarting)
        {
            _observations.Clear();
            _doorsUsed.Clear(); _passedDoor = false; _padHop = false; _segGoal = null; _doorsPassable = false; _doorStep = null;
            _walkStart = now; _walkBudget = _budget + 20f;   // N1: the whole walk, one door step included
        }
        _lastProgressT = now;
        _lastProgressPos = p;
        _path = null;
        _helperEverHadNodes = false;
        _shortHopTried = false; _noPathRetry = false;
        _knownOnly = false;
        AITravel.ObserveView(server, player, true);
        // information-leak check (observation only): what was known when the walk started
        _leakStartPos = p;
        _seenAtStart = AITravel.SnapshotKnown();
        _walkedTiles.Clear();
        _unknownSteps = 0;
        _leakLogged = false;
        try { _objBlockedAtStart = AIObjects.BlockedInView(server, player, out _); } catch { _objBlockedAtStart = new HashSet<int2>(); }
        _knownPathAtStart = KnownPathExists(new AITileReader(server), AIEdge.StandTile(server, p), target);   // live-run fix 2
        // The game's global path search can use topology never observed by this character.
        // Travel uses exact remembered geometry and re-observes it on approach instead.
        Mode = "mod-astar";
        _noPathRetry = true;
    }

    private void CreateHelper(World server, Entity player, int2 target)
    {
        var em = server.EntityManager;
        uint belongs = 0xFFFFFFFFu;
        try
        {
            if (em.HasComponent<PhysicsCollider>(player))
            {
                var pc = em.GetComponentData<PhysicsCollider>(player);
                if (pc.IsValid) belongs = pc.Value.Value.GetCollisionFilter().BelongsTo;
            }
        }
        catch { }
        _helper = em.CreateEntity();
        em.AddComponentData(_helper, new PathFindCD
        {
            startEntity = player,
            belongsToLayer = belongs,
            targetEntity = _targetEntity,
            targetPosition = target,
            searchRadius = new int2(40, 40),
            blockedByCreatures = false,
            isFlying = false,
        });
        em.AddComponent<PathFindBfsCD>(_helper);
        // PathFindSystem writes into existing elements (AddStartingNodesToBuffer), so the buffer must already have
        // PathFindNodeBuffer.MAXIMUM_NODE_COUNT elements. Validity is read from PathFindCD.pathValidTime.
        var nb = em.AddBuffer<PathFindNodeBuffer>(_helper);
        for (int i = 0; i < PathFindNodeBuffer.MAXIMUM_NODE_COUNT; i++) nb.Add(new PathFindNodeBuffer { position = target });
    }

    private Entity _targetEntity = Entity.Null;
    private bool _shortHopTried;
    // 코어-안전지대 단계 2: an own closed door is a tile the walk opens and passes (the game's path search treats it as
    // a wall): with own doors near, the tile A* may plan through them and the walk does the door step there
    private bool _doorsPassable;
    private DoorPassState _doorStep; private int2 _doorTile; private float2 _doorDir; private int _doorUse;
    public bool InDoorStep => _doorStep != null;   // 검수 F1: the door step runs (it holds the move input, AIBody.NavDoorStep)
    public bool HoldsDoor => AIDoorPass.HoldsDoor(_doorStep);   // ... and the door may be open because of it (the skill's swing waits for the close)
    private int2? _segGoal;   // review G7: the tile A* goes only to this own door; past it the game's path search again
    // recheck N1: one walk (across the restart past a door) remembers its doors, has one time budget, and uses a door
    // only to cross the plan's edge (the AI and the target on different sides)
    private readonly HashSet<int2> _doorsUsed = new HashSet<int2>();
    private bool _restarting, _passedDoor, _padHop;   // _padHop: this walk already stepped off the spawn pad
    private bool _noPathRetry;   // recheck R3: the tile A* runs as a try after the game's "no path" (a failed one takes the next try)
    private float _walkStart, _walkBudget;
    private bool _knownOnly;   // the retry A* walks only on tiles known at the start

    // The target moved (e.g. a creature): keep timers and stuck counters, only update where to go.
    public void Retarget(World server, int2 target, float now)
    {
        if (math.all(target == Target)) return;
        Target = target;
        _searchRetryAt = 0f;
        if (_doorStep == null) _path = null;   // 검수 F1: a running door step is not replanned under (past the door the walk starts again)
        if (_helper != Entity.Null && server.EntityManager.Exists(_helper))
        {
            var pf = server.EntityManager.GetComponentData<PathFindCD>(_helper);
            pf.targetPosition = target;
            server.EntityManager.SetComponentData(_helper, pf);
        }
        _budget = math.max(_budget, (now - _start) + 8f);
    }

    public void Dispose(World server)
    {
        if (_helper != Entity.Null && server != null && server.IsCreated && server.EntityManager.Exists(_helper))
        {
            server.EntityManager.DestroyEntity(_helper);
        }
        _helper = Entity.Null;
    }

    // ---- information-leak check (d): does the walk go through tiles Claude had not seen when it started?
    private float3 _leakStartPos;
    private HashSet<int2> _seenAtStart = new HashSet<int2>();
    private readonly HashSet<int2> _walkedTiles = new HashSet<int2>();
    private int _unknownSteps;
    private bool _knownPathAtStart, _leakLogged;
    private HashSet<int2> _objBlockedAtStart = new HashSet<int2>();   // object footprints in view at the start
    public string LeakFacts => $"knownPathAtStart={(_knownSearchCapped ? "unknown(search budget)" : _knownPathAtStart.ToString())} tilesWalked={_walkedTiles.Count} unseenAtStartWalked={_unknownSteps}";

    private bool KnownAtStart(int2 t) => _seenAtStart.Contains(t);

    // BFS over walkable tiles that were seen (memory) or in view at the start. Tiles only (objects not seen).
    private bool KnownPathExists(AITileReader tr, int2 start, int2 goal)
    {
        _knownSearchCapped = false;
        return AITravel.KnownPath(start, goal, n => !_objBlockedAtStart.Contains(n), Tolerance) != null;
    }

    public NavStatus Tick(World server, Entity player, float now, out float2 move)
    {
        AITravel.ObserveView(server, player);
        var st = TickInner(server, player, now, out move);
        int2 cur = AIBody.TileOf(AIBody.Pos(server, player));
        if (_walkedTiles.Add(cur) && !KnownAtStart(cur)) _unknownSteps++;
        if (st != NavStatus.Moving && !_leakLogged)
        {
            _leakLogged = true;
            AIPlayerSpikeMod.Log($"LEAK-CHECK walk to ({Target.x},{Target.y}) {st} mode={Mode}: {LeakFacts}");
        }
        return st;
    }

    private NavStatus TickInner(World server, Entity player, float now, out float2 move)
    {
        move = float2.zero;
        float3 pos = AIBody.Pos(server, player);
        float dist = math.distance(pos.xz, (float2)Target);
        if (dist <= Tolerance && AITravel.CanApproach(AIBody.TileOf(pos), Target, Tolerance) && !AIDoorPass.HoldsDoor(_doorStep)) return NavStatus.Arrived;   // 검수 S3: not in the middle of a door step (the door may be open)
        if (now - _start > _budget) ObserveOnce("nav-duration", $"예상 이동 시간 {_budget:F0}초 경과");
        if (_walkBudget > 0f && now - _walkStart > _walkBudget) ObserveOnce("nav-walk-duration", $"문 지나기 포함 전체 걷기 {_walkBudget:F0}초 경과");

        // stuck detection (0.4 tiles: small collision jitter against an object must not count as progress). 재실측 N-a:
        // not during the door step, which stands still on purpose while pressing or waiting for the door.
        if (_doorStep != null) { _lastProgressPos = pos; _lastProgressT = now; }
        else if (math.distance(pos.xz, _lastProgressPos.xz) >= 0.4f)
        {
            _lastProgressPos = pos;
            _lastProgressT = now;
        }
        else if (now - _lastProgressT > 2f)
        {
            StuckCount++;
            _lastProgressT = now;
            AIPlayerSpikeMod.Log($"nav: stuck #{StuckCount} at ({pos.x:F2},{pos.z:F2}) mode={Mode}; replanning");
            // Movement alone does not establish that a tile is permanently blocked. Re-read the actual path
            // obstacles and report the lack of progress while preserving the model's chosen destination.
            if (StuckCount >= 3)
            {
                int2 at = AIEdge.StandTile(server, pos);
                ObserveOnce("nav-no-progress", $"({at.x},{at.y})에서 세 번 이동 진전이 없었음");
            }
            // live-run fix 4 (사실 4): stuck on the game's path (a partial way) takes the same tries as "no path" — the own door
            // first when it is right there (the AI pushing at it); the old plain switch to the tile A* failed "길 없음"
            if (Mode == "game-bfs" && !NoPathRetry(server, player, pos, "stuck with game path", true)) SwitchToAStar(server, player, "stuck with game path");
            else _path = null;
        }

        int2 cur = AIBody.TileOf(pos);
        if (Mode == "game-bfs")
        {
            var em = server.EntityManager;
            if (_helper != Entity.Null && em.Exists(_helper))
            {
                var pf = em.GetComponentData<PathFindCD>(_helper);
                var nodes = em.GetBuffer<PathFindNodeBuffer>(_helper, true);
                bool valid = pf.pathValidTime > 0f;
                float2? aim = null;
                if (valid)
                {
                    _helperEverHadNodes = true;
                    // The game's path: nodes[0] is the tile the path was computed from (refreshed every 0.2-0.3 s), the
                    // next ones the steps ahead. Between refreshes the AI has usually already left nodes[0], so the path
                    // is followed from the segment the AI is on, never back to a node behind it (the old "first node
                    // that is not my tile" stepped back to nodes[0] after every tile boundary: 6 reversals a second).
                    _pts.Clear();
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        float2 q = (float2)nodes[i].position;
                        if (_pts.Count == 0 || math.any(_pts[_pts.Count - 1] != q)) _pts.Add(q);
                    }
                    if (_pts.Count >= 2) aim = Steer(server, player, pos, _pts, 0, now, out _);
                }
                if (AIMoveTrace.On)
                {
                    var nsb = new StringBuilder();
                    for (int i = 0; i < nodes.Length; i++) nsb.Append(i == 0 ? "" : ";").Append(AIMoveTrace.T(nodes[i].position));
                    AIMoveTrace.Nav = $"g|{AIMoveTrace.T(cur)}|{(aim.HasValue ? $"{aim.Value.x:F1} {aim.Value.y:F1}" : "none")}|{nsb}|{pf.pathValidTime:F2}";
                }
                if (aim.HasValue && math.distancesq(pos.xz, aim.Value) > 0.0025f)
                {
                    move = AIBody.DirTo(pos, aim.Value);
                    return NavStatus.Moving;
                }
                if (math.all(cur == Target) || (valid && math.distance(pos.xz, (float2)Target) < 1.2f))
                {
                    move = AIBody.DirTo(pos, (float2)Target);
                    return NavStatus.Moving;
                }
            }
            if (!_helperEverHadNodes && now - _start > 1f)
            {
                // Did PathFindSystem process the request? It sets timer > 0 and lastCalculatedPosition on every refresh.
                bool processed = false;
                if (_helper != Entity.Null && em.Exists(_helper))
                {
                    var pf = em.GetComponentData<PathFindCD>(_helper);
                    processed = pf.timer > 0f || math.all(pf.lastCalculatedPosition == Target);
                }
                if (processed)
                {
                    // The game searched (with tiles AND object colliders) and found no path. Trust it for longer walks
                    // (the tile-only A* would walk into doors). For a short hop (<= 3 tiles) try the tile A* once: the
                    // game's search also reports no path when the AI itself stands on a spot it treats as blocked
                    // (the core spawn pad, a pocket between plants); lack of progress is reported separately.
                    // Also when a route over tiles Claude knows exists (seen before or in view at the start): the
                    // game also says "no path" when the AI stands on a spot it treats as blocked (the core spawn
                    // pad). That retry walks only on known tiles (no unseen shortcuts).
                    // 단계 3 review F2 / recheck R3: the order is AIDoorPass.NoPathNext (a known open way, the own door,
                    // the short hop, off the pad, fail)
                    return NoPathRetry(server, player, pos, "game pathfinding found no path") ? NavStatus.Moving : NavStatus.Failed;
                }
                SwitchToAStar(server, player, "game pathfinding never processed the request in 1s");
            }
            else
            {
                return NavStatus.Moving; // wait for the game's path
            }
        }

        // mod A*: an object that came into view on the tiles just ahead (the path was planned with the objects seen
        // then) makes a new plan now instead of a walk into it and three stuck rounds
        if (_path != null && now - _aheadCheckAt >= 0.5f)
        {
            _aheadCheckAt = now;
            HashSet<int2> ob = null;
            try { ob = AIObjects.BlockedInView(server, player, out _); } catch { }
            if (ob != null)
                for (int i = math.max(0, _pathIdx - 1); i < _path.Count && i <= _pathIdx + 8; i++)
                {
                    if (AITravel.KnownWalkable(_path[i]) && (math.all(_path[i] == Target) || !ob.Contains(_path[i]))) continue;
                    if (_doorsPassable && AIEdge.IsWalkDoorTile(_path[i])) continue;   // live run: the own door is planned through
                    AIPlayerSpikeMod.Log($"nav: object now in view on the planned path at ({_path[i].x},{_path[i].y}); replanning");
                    _path = null;
                    break;
                }
        }
        if (_path == null)
        {
            // Objects in view now and those in view at the start are blocked (the A* used
            // to see the start's objects only on the known-tiles retry, and none otherwise). The AI's own rounded tile
            // can be an object's tile when it is pressed against it: start from the open neighbour its centre is in
            // front of instead (planning from inside the object went straight through it again).
            if (now < _searchRetryAt) return NavStatus.Moving;
            var trA = new AITileReader(server);
            try { _objBlockedNow = AIObjects.BlockedInView(server, player, out _); _walkCache.Clear(); _walkCacheAt = now; } catch { }
            Func<int2, bool> allowed = n => AITravel.KnownWalkable(n) && (!_knownOnly || KnownAtStart(n)) && (math.all(n == Target) || (_doorsPassable && AIEdge.IsWalkDoorTile(n)) || !_objBlockedNow.Contains(n)); // out-of-view geometry comes from this character's last observation
            // 재실측 결함 2: the plan starts from the tile the AI stands on (AIBaseGuard.PlanFrom) — pressed against a shut
            // door the rounded tile is the door tile, which `allowed` lets through when the walk may pass own doors, so
            // the A* to that door was 0 tiles and the door step never ran
            var shutA = AIEdge.ShutDoorsNear(server, cur, 2);
            var pfrom = AIBaseGuard.PlanFrom(pos.x, pos.z, u => { var v = new int2(u.x, u.y); return !shutA.Contains(v) && trA.Walkable(v); }, u => allowed(new int2(u.x, u.y)), (Target.x, Target.y));
            int2 from = new int2(pfrom.x, pfrom.y);
            if (!math.all(from == cur)) AIPlayerSpikeMod.Log($"nav: own tile {cur} is {(shutA.Contains(cur) ? "a shut door (pressed against it)" : "blocked (pressed against an object / stuck there)")}; planning from {from}");
            _path = AITravel.KnownPath(from, _segGoal ?? Target, allowed, _segGoal.HasValue ? 0f : Tolerance);
            if (_path != null && !math.all(from == cur)) _path.Insert(0, from);
            _pathIdx = 0;
            Replans++;
            if (_path == null && _noPathRetry)
            {
                // recheck R3: this try (after the game's "no path") found no tile way either: mark it tried and take the
                // next one (a failed A* to the door still tries the short hop, a failed known-way hop the door)
                string what = _doorsPassable ? $"own door ({(_segGoal ?? Target).x},{(_segGoal ?? Target).y})" : _padHop && _segGoal.HasValue ? "off the pad" : "short hop";
                if (_doorsPassable && _segGoal.HasValue) _doorsUsed.Add(_segGoal.Value);
                _doorsPassable = false; _segGoal = null; _knownOnly = false;
                if (NoPathRetry(server, player, pos, $"tile A* found no way for the {what} try")) return NavStatus.Moving;
                return NavStatus.Failed;
            }
            if (_path == null) { FailReason = "현재 관측과 마지막 통행 기억 안에서 목적지로 이어지는 길을 찾지 못함. 미지 구역의 우회 연결은 확인하지 않음"; FailCode = SkillEnd.NoPath; return NavStatus.Failed; }
            AIPlayerSpikeMod.Log($"nav: mod A* path {cur} -> {Target}: {_path.Count} tiles");
            _astarPts.Clear();
            _astarPts.Add((float2)cur);
            foreach (var t in _path) _astarPts.Add((float2)t);
        }
        if (_doorsPassable && DoorStep(server, player, pos, now, out move, out NavStatus ds)) return ds;
        if (_padHop && _segGoal.HasValue && math.distance(pos.xz, (float2)_segGoal.Value) <= 0.45f)
        {
            // off the pad: the rest of the way is the game's path search (one walk: same budget, same doors)
            int2 target = Target; float tol = Tolerance; Entity te = _targetEntity;
            AIPlayerSpikeMod.Log($"nav: off the spawn pad at ({_segGoal.Value.x},{_segGoal.Value.y}); back to the game's path search");
            Dispose(server);
            _restarting = true; try { Start(server, player, target, tol, now, te); } finally { _restarting = false; }
            _segGoal = null;
            move = float2.zero; return NavStatus.Moving;
        }
        float2 wp = _astarPts.Count >= 2 ? Steer(server, player, pos, _astarPts, _pathIdx, now, out _pathIdx) : (float2)Target;
        if (AIMoveTrace.On) AIMoveTrace.Nav = $"a|{AIMoveTrace.T(cur)}|{wp.x:F1} {wp.y:F1}|{_pathIdx}/{_astarPts.Count}|";
        move = AIBody.DirTo(pos, wp);
        return NavStatus.Moving;
    }

    private float _aheadCheckAt;

    // ---- path following (both the game's path and the mod A* path)
    private readonly List<float2> _pts = new List<float2>();
    private readonly List<float2> _astarPts = new List<float2>();
    private readonly Dictionary<int2, bool> _walkCache = new Dictionary<int2, bool>();
    private float _walkCacheAt = -10f;
    private HashSet<int2> _objBlockedNow = new HashSet<int2>();
    private const float BodyHalf = 0.3f;     // half width of the corridor a straight line must have (player body)
    private const int LookAheadNodes = 8;

    // Where to walk now along the polyline pts (tile centres): the farthest point ahead that can be reached in a
    // straight line through open tiles (string pulling; corners are cut only where the tiles at the corner are open),
    // else the next point after the segment the AI is on. Points behind the AI are never chosen. fromSeg: the first
    // segment to consider (progress so far, for a fixed path); passedSeg: the segment the AI is on now.
    private float2 Steer(World server, Entity player, float3 pos, List<float2> pts, int fromSeg, float now, out int passedSeg)
    {
        float2 p = pos.xz;
        int n = pts.Count;
        fromSeg = math.clamp(fromSeg, 0, math.max(0, n - 2));
        int k = fromSeg;
        float best = float.MaxValue;
        for (int i = fromSeg; i < n - 1 && i <= fromSeg + LookAheadNodes; i++)
        {
            float d = DistToSegment(p, pts[i], pts[i + 1]);
            if (d <= best + 1e-3f) { best = d; k = i; }   // ties (at a shared node): the later segment
        }
        passedSeg = k;
        if (now - _walkCacheAt > 1f)
        {
            _walkCache.Clear();
            _walkCacheAt = now;
            try { _objBlockedNow = AIObjects.BlockedInView(server, player, out _); } catch { _objBlockedNow = _objBlockedAtStart; }
        }
        var tr = new AITileReader(server);
        for (int j = math.min(n - 1, k + LookAheadNodes); j > k + 1; j--)
        {
            if (Clear(tr, p, pts[j])) return pts[j];
        }
        return pts[math.min(n - 1, k + 1)];
    }

    private static float DistToSegment(float2 p, float2 a, float2 b)
    {
        float2 ab = b - a;
        float l2 = math.lengthsq(ab);
        float t = l2 > 1e-6f ? math.saturate(math.dot(p - a, ab) / l2) : 0f;
        return math.distance(p, a + t * ab);
    }

    // A straight walk from a to b keeps the body (a square of half width BodyHalf) on open tiles all the way.
    private bool Clear(AITileReader tr, float2 a, float2 b)
    {
        float len = math.distance(a, b);
        int steps = math.max(1, (int)math.ceil(len / 0.2f));
        for (int s = 0; s <= steps; s++)
        {
            float2 q = math.lerp(a, b, s / (float)steps);
            for (int c = 0; c < 4; c++)
            {
                float2 corner = q + new float2((c & 1) == 0 ? -BodyHalf : BodyHalf, (c & 2) == 0 ? -BodyHalf : BodyHalf);
                int2 t = new int2((int)math.round(corner.x), (int)math.round(corner.y));
                if (!_walkCache.TryGetValue(t, out bool w))
                {
                    w = AITravel.KnownWalkable(t) && !_objBlockedNow.Contains(t);
                    _walkCache[t] = w;
                }
                if (!w) return false;
            }
        }
        return true;
    }

    // the walkable tile just off the spawn pad (Chebyshev distance SpawnPadHalf + 1 from the origin) nearest the target
    private static bool PadExit(World server, float3 pos, int2 target, out int2 exit)
    {
        exit = default; float best = float.MaxValue;
        var tr = new AITileReader(server);
        int r = AIPlaceGrid.SpawnPadHalf + 1;
        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            {
                if (math.max(math.abs(x), math.abs(y)) != r) continue;
                int2 t = new int2(x, y);
                if (!AITravel.KnownWalkable(t)) continue;
                float c = math.distance(pos.xz, (float2)t) + math.distance((float2)t, (float2)target);
                if (c < best) { best = c; exit = t; }
            }
        return best < float.MaxValue;
    }

    // the own door within 30 tiles of the AI that is the shortest detour on the way to the target — only when the AI
    // and the target lie on different sides of the planned edge, and not a door this walk already went through (N1)
    private bool OwnDoorToward(int2 at, int2 target, out int2 door)
    {
        door = default; float best = float.MaxValue;
        float2 a = (float2)at, b = (float2)target;
        if (AIEdge.PlanActive && AIEdge.IsInside(at) != AIEdge.IsInside(target))
            foreach (var d in AIEdge.OwnDoorTiles)
            {
                if (_doorsUsed.Contains(d) || math.distance(a, (float2)d) > 30f) continue;
                float c = math.distance(a, (float2)d) + math.distance((float2)d, b);
                if (c < best) { best = c; door = d; }
            }
        // 제작대-놓기 §6 (b): a door the AI placed off the plan (my door) near the AI, not a long detour, when the game's
        // path search found no way (it may be the door in the way). 실게임2 P1: the own edge doors join this choice
        // (shortest detour among both) — with only my doors here, a walk between two doors on the same side of the plan
        // (a re-picked edge) took my door behind it and failed after it, never the edge door on the way.
        if (best == float.MaxValue)
        {
            // 검수 Q1: not the edge doors when the AI and the target are both inside (it would walk out of the base)
            var edgeD = new List<(int x, int y)>();
            if (AIPlaces.EdgeDoorsJoin(AIEdge.PlanActive, AIEdge.PlanActive && AIEdge.IsInside(at), AIEdge.PlanActive && AIEdge.IsInside(target)))
                foreach (var d in AIEdge.OwnDoorTiles) if (!_doorsUsed.Contains(d)) edgeD.Add((d.x, d.y));
            var myD = new List<(int x, int y)>(); foreach (var d in AIEdge.MyDoorTiles) if (!_doorsUsed.Contains(d)) myD.Add((d.x, d.y));
            if (AIPlaces.NearDoor((a.x, a.y), (b.x, b.y), edgeD, myD, out var nd)) { door = new int2(nd.x, nd.y); best = 0f; }
        }
        return best < float.MaxValue;
    }

    // The door step of a walk through an own door: when the next tiles of the path hold an own closed door (or one
    // being passed), open -> through -> close (AIDoorPass), steering the body's face and press (AIBody.NavDoorStep).
    // True while the step drives the walk this frame.
    private bool DoorStep(World server, Entity player, float3 pos, float now, out float2 move, out NavStatus status)
    {
        move = float2.zero; status = NavStatus.Moving;
        if (_doorStep == null)
        {
            if (_path == null) return false;
            for (int i = math.max(0, _pathIdx - 1); i < _path.Count && i <= _pathIdx + 2; i++)
            {
                int2 t = _path[i];
                if (!AIEdge.IsWalkDoorTile(t) || math.distance(pos.xz, (float2)t) > 1.6f) continue;   // 제작대-놓기 §6 (b): my doors too
                var dv = AIEdge.DoorAt(server, t, pos);
                if (dv == null) continue;
                int2 before = i > 0 ? _path[i - 1] : AIEdge.StandTile(server, pos);   // 재실측 결함 2: not the door tile it is pressed on
                float2 dir = math.normalizesafe((float2)(t - before));
                if (math.lengthsq(dir) < 1e-4f) dir = math.normalizesafe((float2)t - pos.xz);
                _doorStep = new DoorPassState(); _doorTile = t; _doorDir = dir; _doorUse = 0;
                AIPlayerSpikeMod.Log($"nav: own door {dv.id} ({t.x},{t.y}) variation={dv.variation} on the path: open -> through -> close");
                break;
            }
            if (_doorStep == null) return false;
        }
        var d2 = AIEdge.DoorAt(server, _doorTile, pos);
        if (d2 == null) { _doorStep = null; return false; }
        bool pressDone = _doorUse == 0 || AIBody.UseDone >= _doorUse;
        AIDoorPass.PressDropped(_doorStep, AIBody.UseDroppedWhy(_doorUse), now);   // 합치기 전 수정 C2: not sent, not a press
        var o = AIDoorPass.Step(_doorStep, d2.kind, _doorTile.x, _doorTile.y, _doorDir.x, _doorDir.y, pos.x, pos.z, AIDoorPass.IsOpen(d2.variation), pressDone, now);
        if (!string.IsNullOrEmpty(o.observation)) ObserveOnce("door-" + _doorTile + "-" + _doorStep.phase, o.observation);
        if (o.press) _doorUse = AIBody.NextUseId;
        bool running = _doorStep.phase != DoorPhase.Done && _doorStep.phase != DoorPhase.Failed;
        // 실측결함수정3 N3: a slow (over 2.5 s) or failed step leaves its trace with what the body held and its state
        if (!running && (_doorStep.phase == DoorPhase.Failed || now - _doorStep.startAt > 2.5f))
            AIPlayerSpikeMod.Log($"nav: door step trace ({_doorTile.x},{_doorTile.y}) {now - _doorStep.startAt:F1}s {_doorStep.phase} skill='{AIBody.Current?.Name}' target=({Target.x},{Target.y}) {AIBody.HandNote(server, player)}: {_doorStep.trace}");
        if (running) AIBody.NavDoorStep(new float2(o.fx, o.fy), _doorUse != 0 && AIBody.UseDone < _doorUse ? _doorUse : 0, new float2(o.mx, o.my), _doorStep, _doorTile, _doorDir, AIDoorPass.IsOpen(d2.variation), o.nudge);
        if (_doorStep.phase == DoorPhase.Done)
        {
            AIPlayerSpikeMod.Log($"nav: through own door ({_doorTile.x},{_doorTile.y}): {_doorStep.facts}; back to the game's path search");
            _doorStep = null;
            // review G7: past the door the rest of the way is the game's path search again (no A* box limit)
            int2 target = Target; float tol = Tolerance; Entity te = _targetEntity;
            _doorsUsed.Add(_doorTile); _passedDoor = true;
            Dispose(server);
            _restarting = true; try { Start(server, player, target, tol, now, te); } finally { _restarting = false; }
            _doorsPassable = false; _segGoal = null;
            return false;
        }
        if (_doorStep.phase == DoorPhase.Failed)
        {
            FailReason = $"문 ({_doorTile.x},{_doorTile.y}) 지나가기 실패: {_doorStep.fail}"; FailCode = SkillEnd.NoPath;
            _doorStep = null; status = NavStatus.Failed; return true;
        }
        move = new float2(o.mx, o.my);
        return true;
    }

    // The next try after the game's path search found no path (AIDoorPass.NoPathNext; recheck R3: also after a try's
    // tile A* found none). False: nothing left, FailReason set.
    private bool NoPathRetry(World server, Entity player, float3 pos, string why, bool stuck = false)
    {
        int2 at = AIEdge.StandTile(server, pos), viaDoor = default, exit = default;   // live-run fix 2: not the shut door tile it is pressed on
        bool doorNear = !_doorsPassable && !_passedDoor && OwnDoorToward(at, Target, out viaDoor);
        bool shortNear = math.distance(pos.xz, (float2)Target) <= 3f;
        bool padExit = !_padHop && AIPlaceGrid.SpawnPad(at.x, at.y) && PadExit(server, pos, Target, out exit);
        bool doorAhead = stuck && doorNear && math.distance(pos.xz, (float2)viaDoor) <= 1.6f;   // fix 4: stuck right at the own door
        switch (AIDoorPass.NoPathNext(doorNear, _doorsPassable, _passedDoor, _shortHopTried, shortNear, _knownPathAtStart, _padHop, padExit, doorAhead))
        {
            case AIDoorPass.NoPathStep.OwnDoor:
                _doorsPassable = true; _segGoal = viaDoor; _noPathRetry = true;
                SwitchToAStar(server, player, $"{why}; own door ({viaDoor.x},{viaDoor.y}) is near: tile A* to it, open -> through -> close, then the game's path search again");
                return true;
            case AIDoorPass.NoPathStep.ShortHop:
                _shortHopTried = true; _noPathRetry = true;
                _knownOnly = _knownPathAtStart;
                SwitchToAStar(server, player, $"{why}; retrying with tile A*{(_knownOnly ? " on known tiles only (a known open way)" : " (short hop <= 3 tiles)")}");
                return true;
            case AIDoorPass.NoPathStep.PadHop:
                // live run: from the spawn pad the game's path search finds no way: first a tile A* hop off the pad
                _padHop = true; _segGoal = exit; _noPathRetry = true;
                SwitchToAStar(server, player, $"{why} from the spawn pad: tile A* off the pad to ({exit.x},{exit.y}), then the game's path search again");
                return true;
            case AIDoorPass.NoPathStep.FailAfterDoor:
                // N1: past an own door the game's path search failed again: the target has no way from here
                FailReason = "길 없음(자기 문을 지난 뒤에도 현재 관측·마지막 통행 기억 안에서 목적지 연결을 찾지 못함; 미지 우회로 미확인)"; FailCode = SkillEnd.NoPath;
                return false;
        }
        FailReason = "길 없음(현재 관측·마지막 통행 기억 안에서 목적지 연결을 찾지 못함; 미지 우회로 미확인)"; FailCode = SkillEnd.NoPath;
        return false;
    }

    private void SwitchToAStar(World server, Entity player, string why)
    {
        AIPlayerSpikeMod.Log($"nav: switching to mod A* ({why})");
        Dispose(server);
        Mode = "mod-astar";
        _path = null;
    }

    // 4-neighbour A* in a box around start/target. Walkable = top tile IsWalkableTile (objects are not seen).
    internal static List<int2> AStar(AITileReader tr, int2 start, int2 goal, Func<int2, bool> allowed = null)
    {
        int2 lo = new int2(int.MinValue,int.MinValue), hi = new int2(int.MaxValue,int.MaxValue);
        LastAStarCapped = false;
        var open = new List<int2> { start };
        var came = new Dictionary<int2, int2>();
        var g = new Dictionary<int2, int> { [start] = 0 };
        var closed = new HashSet<int2>();
        var walk = new Dictionary<int2, bool>();
        int2[] dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        int guard = 0;
        while (open.Count > 0 && guard++ < 20000)
        {
            int bi = 0; long bf = long.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                long f = g[open[i]] + System.Math.Abs((long)open[i].x - goal.x) + System.Math.Abs((long)open[i].y - goal.y);
                if (f < bf) { bf = f; bi = i; }
            }
            int2 c = open[bi];
            open.RemoveAt(bi);
            if (math.all(c == goal))
            {
                var path = new List<int2>();
                while (!math.all(c == start)) { path.Add(c); c = came[c]; }
                path.Reverse();
                return path;
            }
            closed.Add(c);
            foreach (var d in dirs)
            {
                if ((d.x > 0 && c.x == int.MaxValue) || (d.x < 0 && c.x == int.MinValue) || (d.y > 0 && c.y == int.MaxValue) || (d.y < 0 && c.y == int.MinValue)) continue;
                int2 n = c + d;
                if (math.any(n < lo) || math.any(n > hi) || closed.Contains(n)) continue;
                if (!walk.TryGetValue(n, out bool w)) { w = tr.Walkable(n) && (allowed == null || allowed(n)); walk[n] = w; }
                if (!w && !math.all(n == goal)) continue;
                if (!w) continue;
                int ng = g[c] + 1;
                if (g.TryGetValue(n, out int og) && og <= ng) continue;
                g[n] = ng;
                came[n] = c;
                if (!open.Contains(n)) open.Add(n);
            }
        }
        LastAStarCapped = open.Count > 0;
        return null;
    }
}
