using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Observed walkability and area/exit travel decisions (2026-11-08 area-exit-design.md). Only cells this character has
// seen are used: the area split, the exits and every question come from the observed map; nothing off screen is read.
public static class AITravel
{
    private sealed class State
    {
        public string World, Path;
        public float NextObserve, NextSave;
        public bool Dirty;
        public int ExitSeq;
        public readonly Dictionary<AITravelRules.Cell, bool> Map = new Dictionary<AITravelRules.Cell, bool>();
        public readonly ObservedGrid Grid = new ObservedGrid();   // the same cells in blocks, for the area analysis window
        public readonly Dictionary<string, ExitRecord> Exits = new Dictionary<string, ExitRecord>(StringComparer.Ordinal);
        public readonly AreaTracker Areas = new AreaTracker();
        // the last area analysis (recomputed when the map changed and at most every AnalyzeSeconds)
        public AIAreaRules.Map Analysis; public Dictionary<int, int> Ids = new Dictionary<int, int>();
        public AITravelRules.Cell AnalysisCentre; public float NextAnalyze, NextOutside; public bool AnalysisDirty = true;
    }
    public const float AnalyzeSeconds = 0.5f;
    // What this character actually found after taking an exit, kept across runs and sessions (keyed by the exit's gate
    // cell and the direction it was taken in). It is shown in the choice text only; every exit stays a choice for the model.
    private sealed class ExitRecord { public int Visits, Steps, Seq; public string Outcome = ""; }
    // version 4 (최종 검수 G3): exit records keyed by gate cell + outward step ("g:x,y:E"), and the known areas.
    // Version 1/2/3 files still read: v2 "from>exit" keys carry their direction; v3 "g:x,y" keys have none (the way out
    // and the way back through one cell were one record) and are dropped — the map and the areas are kept.
    [Serializable] public class MemoryData
    {
        public int version = 4; public int[] xy = new int[0]; public bool[] walk = new bool[0];
        public string[] exitKeys = new string[0]; public int[] exitVisits = new int[0]; public int[] exitSteps = new int[0]; public int[] exitSeq = new int[0]; public string[] exitOutcomes = new string[0];
        public int[] areaIds = new int[0]; public int[] areaAnchors = new int[0]; public int[] areaVisits = new int[0]; public int[] areaSeq = new int[0];
    }
    private static State S => AIAgent.Cur.Get<State>(AISlot.Travel);
    internal static AITravelRules.Cell Cell(int2 p) => new AITravelRules.Cell(p.x, p.y);
    internal static int2 Tile(AITravelRules.Cell p) => new int2(p.X, p.Y);
    private static State Ensure()
    {
        var s = S; string world = AIPlayerSpikeMod.WorldId ?? "";
        if (s.World == world) return s;
        Save(s); // retain the last observation before switching worlds, even inside the periodic-save window
        s.World = world; s.Map.Clear(); s.Grid.Clear(); s.Exits.Clear(); s.ExitSeq = 0; s.NextObserve = s.NextSave = 0; s.Dirty = false;
        s.Areas.Reset(); s.Analysis = null; s.Ids.Clear(); s.AnalysisDirty = true; s.NextAnalyze = s.NextOutside = 0;
        s.Path = string.IsNullOrEmpty(world) ? null : System.IO.Path.Combine(AIMemory.CharDir(world), "travel-memory.json");
        if (s.Path != null && File.Exists(s.Path))
            try
            {
                var d = JsonUtility.FromJson<MemoryData>(File.ReadAllText(s.Path, Encoding.UTF8));
                if (d != null && d.xy != null && d.walk != null)
                    for (int i = 0; i < d.walk.Length && i * 2 + 1 < d.xy.Length; i++)
                    { var c = new AITravelRules.Cell(d.xy[i * 2], d.xy[i * 2 + 1]); s.Map[c] = d.walk[i]; s.Grid.Set(c, d.walk[i]); }
                if (d?.exitKeys != null)
                    for (int i = 0; i < d.exitKeys.Length; i++)
                    {
                        var r = new ExitRecord
                        {
                            Visits = d.exitVisits != null && i < d.exitVisits.Length ? d.exitVisits[i] : 0,
                            Steps = d.exitSteps != null && i < d.exitSteps.Length ? d.exitSteps[i] : 0,
                            Seq = d.exitSeq != null && i < d.exitSeq.Length ? d.exitSeq[i] : 0,
                            Outcome = d.exitOutcomes != null && i < d.exitOutcomes.Length ? d.exitOutcomes[i] ?? "" : ""
                        };
                        string key = MigrateExitKey(d.exitKeys[i]);
                        if (key != null && (!s.Exits.TryGetValue(key, out var old) || old.Seq < r.Seq)) { s.Exits[key] = r; s.ExitSeq = Math.Max(s.ExitSeq, r.Seq); }
                    }
                if (d?.areaIds != null)
                    for (int i = 0; i < d.areaIds.Length; i++)
                    {
                        if (d.areaAnchors == null || i * 2 + 1 >= d.areaAnchors.Length) break;
                        var k = new AreaTracker.Known { Id = d.areaIds[i], Anchor = new AITravelRules.Cell(d.areaAnchors[i * 2], d.areaAnchors[i * 2 + 1]),
                            Visits = d.areaVisits != null && i < d.areaVisits.Length ? d.areaVisits[i] : 0, Seq = d.areaSeq != null && i < d.areaSeq.Length ? d.areaSeq[i] : 0 };
                        s.Areas.Areas[k.Id] = k; s.Areas.NextId = Math.Max(s.Areas.NextId, k.Id + 1); s.Areas.Seq = Math.Max(s.Areas.Seq, k.Seq);
                    }
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("travel: remembered geometry could not be read", e); }
        return s;
    }
    // v2 keys were "from>exit" cells: the exit cell becomes the gate, the step from->exit its direction. v4 keys are
    // kept; v3 keys ("g:x,y", no direction) cannot tell the way out from the way back and are dropped.
    private static string MigrateExitKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.StartsWith("g:", StringComparison.Ordinal)) return key.IndexOf(':', 2) > 0 ? key : null;
        int at = key.IndexOf('>');
        if (at < 0 || !TryCell(key.Substring(0, at), out var from) || !TryCell(key.Substring(at + 1), out var exit)) return null;
        return ExitKey(exit, AITravelRules.DirIndex(from, exit));
    }
    private static bool TryCell(string s, out AITravelRules.Cell c)
    {
        c = default; int comma = s.IndexOf(',');
        if (comma < 0 || !int.TryParse(s.Substring(0, comma), out int x) || !int.TryParse(s.Substring(comma + 1), out int y)) return false;
        c = new AITravelRules.Cell(x, y); return true;
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
                if (!s.Map.TryGetValue(key, out bool old) || old != walk) { s.Map[key] = walk; s.Grid.Set(key, walk); s.Dirty = true; s.AnalysisDirty = true; }
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
        int n = s.Exits.Count; d.exitKeys = new string[n]; d.exitVisits = new int[n]; d.exitSteps = new int[n]; d.exitSeq = new int[n]; d.exitOutcomes = new string[n];
        i = 0; foreach (var kv in s.Exits) { d.exitKeys[i] = kv.Key; d.exitVisits[i] = kv.Value.Visits; d.exitSteps[i] = kv.Value.Steps; d.exitSeq[i] = kv.Value.Seq; d.exitOutcomes[i++] = kv.Value.Outcome; }
        int a = s.Areas.Areas.Count; d.areaIds = new int[a]; d.areaAnchors = new int[a * 2]; d.areaVisits = new int[a]; d.areaSeq = new int[a];
        i = 0; foreach (var k in s.Areas.Areas.Values) { d.areaIds[i] = k.Id; d.areaAnchors[i * 2] = k.Anchor.X; d.areaAnchors[i * 2 + 1] = k.Anchor.Y; d.areaVisits[i] = k.Visits; d.areaSeq[i++] = k.Seq; }
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


    // ---- areas and exits (observed cells only)
    internal static AIAreaRules.Map Areas(AITravelRules.Cell at, bool force = false)
    {
        var s = Ensure(); float now = Time.realtimeSinceStartup;
        bool far = s.Analysis == null || Math.Abs(at.X - s.AnalysisCentre.X) + Math.Abs(at.Y - s.AnalysisCentre.Y) > 8;
        // 최종 검수 S4: a cell outside the last analysis (e.g. standing on an object cell the memory holds as blocked: the
        // analysis from there is empty) re-runs it only from a new cell and at most every OutsideSeconds; from the same
        // cell with an unchanged map it would give the same result, so only the ordinary dirty rule applies
        bool outside = !far && s.Analysis.AreaAt(at) == null && s.Analysis.ConnectorAt(at) == null && !at.Equals(s.AnalysisCentre) && now >= s.NextOutside;
        if (!far && !outside)
        {
            if (!s.AnalysisDirty) return s.Analysis;
            if (!force && now < s.NextAnalyze) return s.Analysis;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        s.Analysis = AIAreaRules.Analyze(at, s.Grid);
        s.Ids = s.Areas.Assign(s.Analysis); s.AnalysisCentre = at; s.AnalysisDirty = false; s.NextAnalyze = now + AnalyzeSeconds; s.NextOutside = now + OutsideSeconds;
        LastAnalysisMs = (float)sw.Elapsed.TotalMilliseconds; AnalysisCount++;
        return s.Analysis;
    }
    public const float OutsideSeconds = 0.25f;
    internal static float LastAnalysisMs;
    internal static int AnalysisCount;   // analyses run (offline tests: the rate)
    internal static int AreaId(AIAreaRules.Area area) => area != null && S.Ids.TryGetValue(area.Index, out int id) ? id : 0;
    internal static AIAreaRules.Area AreaById(AIAreaRules.Map map, int id)
    {
        if (map == null || id == 0) return null;
        foreach (var a in map.Areas) if (AreaId(a) == id) return a;
        return null;
    }
    internal static int AreaVisits(int id) => Ensure().Areas.Areas.TryGetValue(id, out var k) ? k.Visits : 0;
    internal static void NoteAreaVisit(int id) { var s = Ensure(); if (s.Areas.Areas.TryGetValue(id, out var k)) { k.Visits++; s.Dirty = true; } }

    // 최종 검수 G3: gate cell + the outward step (no area id: ids change on a split/merge, the cell and the direction do not)
    private static string ExitKey(AITravelRules.Cell gate, int dir) => "g:" + gate.X + "," + gate.Y + ":" + "EWNS"[dir & 3];
    internal static void NoteExitTaken(AITravelRules.Cell gate, int dir)
    {
        var s = Ensure(); string key = ExitKey(gate, dir);
        if (!s.Exits.TryGetValue(key, out var r)) { s.Exits[key] = r = new ExitRecord { Outcome = "결과 확인 전에 기록이 끊김" }; TrimExits(s); }
        r.Visits++; r.Seq = ++s.ExitSeq; s.Dirty = true;   // 검수 S5: the earlier result stays until this visit has one
    }
    internal static void NoteExitResult(AITravelRules.Cell gate, int dir, string outcome, int steps, bool aborted = false)
    {
        var s = Ensure();
        if (!s.Exits.TryGetValue(ExitKey(gate, dir), out var r)) return;
        // Leaving a just-taken exit for another action says nothing about it: an earlier real result is kept.
        if (aborted && steps == 0 && r.Outcome != "결과 확인 전에 기록이 끊김") return;
        r.Outcome = outcome; r.Steps = steps; r.Seq = ++s.ExitSeq; s.Dirty = true;
    }
    public const int MaxExitRecords = 2000;   // 검수 S5: travel-memory.json stays bounded (least recently used dropped)
    private static void TrimExits(State s)
    {
        while (s.Exits.Count > MaxExitRecords)
        {
            string oldest = null; int seq = int.MaxValue;
            foreach (var kv in s.Exits) if (kv.Value.Seq < seq) { seq = kv.Value.Seq; oldest = kv.Key; }
            s.Exits.Remove(oldest);
        }
    }
    // The fact part of an exit text: the latest result recorded on any gate cell of the exit taken outward, else what the
    // observed map says.
    internal static string ExitFacts(AITravelRules.Cell gate, int dir, AIAreaRules.AreaExit e = null)
    {
        var s = Ensure(); ExitRecord best = null;
        if (s.Exits.TryGetValue(ExitKey(gate, dir), out var r) && r.Visits > 0) best = r;
        if (e != null) for (int i = 0; i < e.Mouth.Count; i++) if (s.Exits.TryGetValue(ExitKey(e.Mouth[i], e.MouthOut[i]), out var m) && m.Visits > 0 && (best == null || m.Seq > best.Seq)) best = m;
        if (best != null) return $"{best.Visits}번 감, 마지막: {best.Outcome}({best.Steps}칸).";
        if (e == null) return "미확인.";
        return e.DeadEnd ? "보이는 데까지 막다른 통로." : e.Loop ? "이 공간으로 돌아오는 통로." : e.ToArea >= 0 ? "다른 공간으로 이어짐(미탐색)." : "미탐색.";
    }
    // One exit as a choice text (short; the area line is said once in the question or the first choice).
    internal static string ExitText(AITravelRules.Cell from, AIAreaRules.AreaExit e, bool entrance = false, bool current = false)
    {
        string kind = e.Kind == AIAreaRules.ExitKind.Connector ? "통로" : e.Kind == AIAreaRules.ExitKind.Contact ? "이웃 공간 쪽" : "못 본 쪽";
        int d = AIAreaRules.Dist(e.Gate, from);
        return $"출구 {AIAreaRules.SectorNames[e.Sector]} {kind}{(entrance ? "(들어온 길)" : "")} ({e.Gate.X},{e.Gate.Y}) {d}칸. {(current ? "지금 가는 출구." : ExitFacts(e.Gate, e.GateOut, e))}";
    }
    internal static string AreaSummary(AIAreaRules.Area area, bool ahead = false)
    {
        int id = AreaId(area);
        return $"공간 {id}({(area.Junction ? "통로 갈림길" : "약 " + area.Count + "칸")}, {Math.Max(1, AreaVisits(id) + (ahead ? 1 : 0))}번째 방문), 출구 {area.Exits.Count}개";
    }

    public static List<AICandidate> Compose(World server, Entity player, AIObservation observation, List<AICandidate> candidates)
    {
        ObserveView(server, player);
        candidates.RemoveAll(c => c.kind == "explore");
        if (AIBody.Current is TravelSkill active && active.Status == SkillStatus.Running)
        {
            active.AddChoices(server, player, candidates);
            return candidates;
        }
        var pos = observation.pos; var at = Cell(AIBody.TileOf(pos));
        var map = Areas(at, true); var area = map.AreaAt(at);
        if (area != null)
        {
            // In an area: one start choice per exit of this area (the AI picks; nothing is chosen for it). The area line
            // goes with the first choice only (the same fact once).
            for (int i = 0; i < area.Exits.Count; i++)
            {
                var e = area.Exits[i];
                candidates.Add(new AICandidate { kind = "explore", key = "travel:area:" + AreaId(area) + ":" + e.Gate, tile = Tile(e.Gate),
                    exploreDirection = AIAreaRules.SectorNames[e.Sector], progress = AIBrain.Goal.kind == GoalKind.Explore,
                    desc = "탐험 출발 — " + (i == 0 ? "지금 " + AreaSummary(area) + ". " : "") + ExitText(at, e) });
            }
            return candidates;
        }
        // In a corridor: the visible open neighbours.
        var tr = new AITileReader(server); var objects = AIObjects.BlockedInView(server, player, out _);
        foreach (var next in AITravelRules.Exits(at, null, p => AIVision.CanSee(pos, Tile(p)) && tr.Walkable(Tile(p)) && !objects.Contains(Tile(p))))
        {
            string dir = AITravelRules.Direction(at, next);
            candidates.Add(new AICandidate { kind = "explore", key = "travel:explore:" + at + ":" + next,
                tile = Tile(next), exploreDirection = dir, progress = AIBrain.Goal.kind == GoalKind.Explore,
                desc = "통로 탐색 — " + dir + "쪽 통로로 출발. 외길은 따라 걷고, 다음 공간에서 출구를 고름. " + ExitFacts(next, AITravelRules.DirIndex(at, next)) });
        }
        return candidates;
    }
    public static AISkill Make(AICandidate candidate) => new TravelSkill(candidate.tile) { Key = candidate.key };
    public static string Stale(World server, Entity player, AICandidate c)
    {
        if (c.kind == "travel_turn")
        {
            if (!(AIBody.Current is TravelSkill current) || current.Status != SkillStatus.Running || current.Run != c.run)
                return "출구를 질문한 탐색 실행이 바뀜";
            return current.ValidateTurn(server, player, c);
        }
        var pos = AIBody.Pos(server, player); var at = Cell(AIBody.TileOf(pos)); var target = Cell(c.tile);
        var area = Areas(at)?.AreaAt(at);
        if (area != null && area.Exits.Exists(e => e.Has(target))) return null;
        if (c.key != null && c.key.StartsWith("travel:area:", StringComparison.Ordinal)) return "고른 출구가 지금 이 공간의 출구로 보이지 않음";
        if (Math.Abs(at.X - target.X) + Math.Abs(at.Y - target.Y) > 1 || !AIVision.CanSee(pos, c.tile)) return "출발 위치 또는 보이는 통로가 바뀜";
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

// One exploration run over areas and connectors (area-exit-design.md).
// - In an area the body crosses to the exit the model chose (Cross).
// - Along a connector it follows the one way on to the next area or the unknown edge (Follow); a fork, a dead end or a
//   loop stops the run and the model chooses.
// - Entering an area it is asked once per visit for that area's exits, without the peaceful floor (as soon as the
//   next area is in the observed map while following, else on arrival). While the answer is on the way the body walks
//   into the area — to its core centre and once around it (Wander) — and only then stands (Wait). It never walks into
//   an exit the model did not choose.
public sealed class TravelSkill : AISkill
{
    private enum Mode { Cross, Follow, Wander, Wait }
    // Origin (최종 검수 G2): the area centre the exit was offered from. A chosen unseen side keeps its Sector seen from this
    // fixed point, so following it never turns the bearing step by step.
    private sealed class Choice { public AIAreaRules.ExitKind Kind; public int Sector, Out; public AITravelRules.Cell Gate, Origin; }
    public const float NavTolerance = 0.3f;
    public const int WanderStep = 3;           // look-around points this far from the area centre (core cells only)
    public const float EdgeWaitSeconds = 1f;   // standing at the seen edge this long with nothing new = cannot see ahead
    private Mode _mode;
    private readonly int2 _first;
    private AITravelRules.Cell _at, _prevCell, _lastOutside;
    private bool _started, _halted, _leftArea;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navActive; private AITravelRules.Cell _navTarget;
    private string _haltObservationKey, _haltObservationReason;
    private int _visitArea;                 // the area of the current visit (id; 0 = none)
    // 최종 검수 G1: the visit and the asked area follow cells, not ids — a recomputation can relabel the area under the
    // character (a split leaves the id with the larger part, a merge keeps the older id) without anything being crossed.
    // _visitCell: the last cell walked inside the visit area; _askedCell: the cell the question was asked for (the
    // entrance of an area asked ahead, or where the character stood); _visitIds: every id this visit has had.
    private AITravelRules.Cell _visitCell, _askedCell; private readonly HashSet<int> _visitIds = new HashSet<int>();
    private AIAreaRules.Map _trackedMap;
    // the question about one area's exits (one per visit)
    private int _askedArea, _askCount, _askedAhead;
    private string _askToken, _askKey, _askReason;
    private readonly Dictionary<string, Choice> _offered = new Dictionary<string, Choice>();
    private Choice _reserved; private string _reservedToken;
    // crossing to an exit
    private Choice _crossing; private AIAreaRules.Map _crossMap;
    // following a connector: came from _entry, heading through _firstCell
    private AITravelRules.Cell _entry, _firstCell; private float _edgeSince = -1f;
    private List<AITravelRules.Cell> _along = new List<AITravelRules.Cell>(); private AIAreaRules.Map _alongMap; private AITravelRules.Cell _alongFrom, _alongTo;   // cached walk along the connector
    // walking into the area while the answer is on the way
    private readonly List<AITravelRules.Cell> _wander = new List<AITravelRules.Cell>(); private int _wanderIndex;
    // the exit record this run is writing (gate, steps walked since)
    private bool _branchOpen; private AITravelRules.Cell _branchGate; private int _branchDir, _branchSteps;

    public TravelSkill(int2 first) { _first = first; Name = "통로 탐색"; Key = "travel:explore:" + first.x + "," + first.y; }
    // Walking (crossing, following or walking into the area while asked) — not halted and not standing to wait.
    public bool Moving => _started && !_halted && _mode != Mode.Wait && Status == SkillStatus.Running;
    internal int VisitArea => _visitArea;
    internal int AskedArea => _askedArea;
    internal string AskToken => _askToken;

    public override bool Start(World server, Entity player, float now)
    {
        AITravel.ObserveView(server, player, true);
        _at = _prevCell = _lastOutside = AITravel.Cell(AIBody.TileOf(AIBody.Pos(server, player)));
        var target = AITravel.Cell(_first);
        var map = AITravel.Areas(_at, true); var area = map.AreaAt(_at);
        _started = true; _trackedMap = map;
        if (area != null)
        {
            int id = AITravel.AreaId(area); _visitArea = id; _visitCell = _askedCell = _at; _visitIds.Clear(); _visitIds.Add(id);
            if (id != 0 && AITravel.AreaVisits(id) == 0) AITravel.NoteAreaVisit(id);
            var e = area.Exits.Find(x => x.Has(target));
            if (e != null)
            {
                // a start choice of this area's exit: its exits stay offered under one token while crossing (no question)
                NewToken(id);
                OpenBranch(e.Gate, e.GateOut); BeginCross(server, player, now, ChoiceOf(e, area.Centre), map);
                return true;
            }
        }
        if (Math.Abs(_at.X - target.X) + Math.Abs(_at.Y - target.Y) != 1 || !VisibleOpen(server, player, target))
        { Status = SkillStatus.Failed; Reason = "선택한 출발 통로가 지금 열려 있지 않음"; return false; }
        OpenBranch(target, AITravelRules.DirIndex(_at, target));
        BeginFollow(_at, target);
        NavTo(server, player, now, target);
        return true;
    }
    private static Choice ChoiceOf(AIAreaRules.AreaExit e, AITravelRules.Cell origin) => new Choice { Kind = e.Kind, Sector = e.Sector, Gate = e.Gate, Out = e.GateOut, Origin = origin };
    private void NewToken(int areaId)
    {
        _askedArea = areaId; _askCount++; _askToken = areaId + "#" + Run + "#" + _askCount;
        _reserved = null; _reservedToken = null; _offered.Clear();
    }
    // 최종 검수 G1: a new analysis may give the visit area or the asked area another id with nothing crossed. The visit
    // keeps going under the id of the area that now holds its last walked cell (one visit: no question, the crossing,
    // the token and a reserved answer stay, no exit record); the asked area under the id of the area that holds the cell
    // it was asked for (the question about the visit area moves with the visit). Exits that came with a merge are simply
    // in the area's list at the next question, like exits seen while crossing.
    private void Track(AIAreaRules.Map map)
    {
        if (map == null || map == _trackedMap) return;
        _trackedMap = map;
        int oldVisit = _visitArea;
        if (_visitArea != 0)
        {
            int v = AITravel.AreaId(map.AreaAt(_visitCell));
            if (v != 0 && v != _visitArea)
            {
                if (_visitIds.Add(v)) AITravel.NoteAreaVisit(v);   // this visit counts once for each id it has had
                _visitArea = v;
            }
        }
        if (_askedArea != 0)
        {
            int a = _askedArea == oldVisit ? _visitArea : AITravel.AreaId(map.AreaAt(_askedCell));
            if (a != 0 && a != _askedArea) { if (_askedAhead == _askedArea) _askedAhead = a; _askedArea = a; }
        }
    }
    private void OpenBranch(AITravelRules.Cell gate, int dir)
    {
        CloseBranch("결과 전에 다른 출구를 고름", true);
        _branchOpen = true; _branchGate = gate; _branchDir = dir; _branchSteps = 0;
        AITravel.NoteExitTaken(gate, dir);
    }
    private void CloseBranch(string outcome, bool aborted = false)
    {
        if (!_branchOpen) return;
        _branchOpen = false;
        AITravel.NoteExitResult(_branchGate, _branchDir, outcome, _branchSteps, aborted);
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
    private void NavTo(World server, Entity player, float now, AITravelRules.Cell target)
    {
        if (_navActive && _navTarget.Equals(target)) return;
        _nav.Dispose(server); _nav.Start(server, player, AITravel.Tile(target), NavTolerance, now);
        _navActive = true; _navTarget = target;
    }
    private void BeginCross(World server, Entity player, float now, Choice choice, AIAreaRules.Map map)
    {
        _mode = Mode.Cross; _crossing = choice; _crossMap = map; _crossArrivalMap = null; _edgeSince = -1f;
        NavTo(server, player, now, choice.Gate);
    }
    private void BeginFollow(AITravelRules.Cell entry, AITravelRules.Cell first)
    {
        _mode = Mode.Follow; _entry = entry; _firstCell = first; _leftArea = false; _edgeSince = -1f;
    }
    private void BeginWander(World server, Entity player, float now, AIAreaRules.Area area)
    {
        _mode = Mode.Wander; _wander.Clear(); _wanderIndex = 0;
        _wander.Add(area.Centre);
        // once around the core near the centre: core cells (2+ from every wall) not within 2 of any exit mouth or unseen edge
        foreach (var d in AITravelRules.Directions)
        {
            var p = new AITravelRules.Cell(area.Centre.X + d.X * WanderStep, area.Centre.Y + d.Y * WanderStep);
            if (!area.Junction && area.IsCore(p) && !area.Exits.Exists(e => e.Near(p, 2))) _wander.Add(p);
        }
        if (_wander.Count > 1) _wander.Add(area.Centre);
        if (_wander[0].Equals(_at) && _wander.Count == 1) { _mode = Mode.Wait; _navActive = false; _nav.Dispose(server); return; }
        NavTo(server, player, now, _wander[0]);
    }
    // Ask once per area visit (no floor: AIBrain.MarkAreaQuestion); a later arrival in the same area asks again.
    private void Ask(AIAreaRules.Area area, AITravelRules.Cell cell, string why)
    {
        NewToken(AITravel.AreaId(area)); _askedCell = cell;
        // 최종 검수 S3: no area facts in the question itself — an area seen in part when asked (kind, size, exit count) can
        // differ by the time the call is prepared; the summary goes with the first exit choice, made at preparation (AddChoices)
        _askKey = "area:" + _askToken; _askReason = why + ". 공간 요약과 출구는 출구 후보에 있음.";
        AIBrain.MarkAreaQuestion();
        ObserveOnce(_askKey, _askReason);
    }
    // A chosen passage (connector or neighbouring area) as the area looks now: the exit holding its gate, else the same
    // kind within 3 cells and one sector (a gate moves a cell or two as more is seen).
    private static AIAreaRules.AreaExit Resolve(AIAreaRules.Area area, Choice ch)
    {
        if (area == null || ch == null || ch.Kind == AIAreaRules.ExitKind.Frontier) return null;
        AIAreaRules.AreaExit best = null; int bd = int.MaxValue;
        foreach (var e in area.Exits)
        {
            if (e.Kind != ch.Kind) continue;
            if (e.Has(ch.Gate)) return e;
            int d = AIAreaRules.Dist(e.Gate, ch.Gate);
            if (AIAreaRules.SectorGap(e.Sector, ch.Sector) <= 1 && d <= 3 && d < bd) { bd = d; best = e; }
        }
        return best;
    }
    // 최종 검수 G2: a chosen unseen side is the unknown edge of the area in the chosen sector seen from the fixed origin
    // (the same 45° wedge the exit was grouped by when offered: SectorOf(origin, cell) == Sector). Its nearest cell to
    // 'from' is the next place to walk to; none = that side has been seen to its end. The bearing is never re-based.
    private static bool FrontierTarget(AIAreaRules.Area area, Choice ch, AITravelRules.Cell from, AITravelRules.Cell? not, out AITravelRules.Cell cell, out int dir)
    {
        cell = default; dir = 0; int bd = int.MaxValue;
        if (area == null || ch == null) return false;
        foreach (var e in area.Exits)
        {
            if (e.Kind != AIAreaRules.ExitKind.Frontier) continue;
            for (int i = 0; i < e.Mouth.Count; i++)
            {
                var c = e.Mouth[i];
                if (not.HasValue && c.Equals(not.Value) || AIAreaRules.SectorOf(ch.Origin, c) != ch.Sector) continue;
                int d = AIAreaRules.Dist(c, from);
                if (d < bd || d == bd && (c.Y < cell.Y || c.Y == cell.Y && c.X < cell.X)) { bd = d; cell = c; dir = e.MouthOut[i]; }
            }
        }
        return bd != int.MaxValue;
    }
    // The user-approved continuation of a chosen unseen side that has been seen to its end: the passages on that side —
    // connector or neighbouring-area exits whose gate is in the chosen sector from the same origin (the chosen 45° wedge
    // only, ±22.5° of the chosen bearing; a passage in a neighbouring wedge is not counted), not the way in. Exactly one
    // is the way on; none or several = stop and ask (the script does not choose).
    private List<AIAreaRules.AreaExit> SidePassages(AIAreaRules.Map map, AIAreaRules.Area area, Choice ch)
    {
        var side = new List<AIAreaRules.AreaExit>();
        if (area == null || ch == null) return side;
        foreach (var e in area.Exits)
            if (e.Kind != AIAreaRules.ExitKind.Frontier && AIAreaRules.SectorOf(ch.Origin, e.Gate) == ch.Sector && !IsEntrance(map, e)) side.Add(e);
        return side;
    }
    private bool IsEntrance(AIAreaRules.Map map, AIAreaRules.AreaExit e)
    {
        if (map == null || _lastOutside.Equals(_at)) return false;
        if (e.Has(_lastOutside)) return true;
        if (e.Kind == AIAreaRules.ExitKind.Connector) return map.ConnectorIndex(_lastOutside) == e.Connector;
        return e.Kind == AIAreaRules.ExitKind.Contact && map.AreaIndex(_lastOutside) == e.ToArea;
    }
    public void AddChoices(World server, Entity player, List<AICandidate> candidates)
    {
        var map = AITravel.Areas(_at);
        Track(map);
        if (_halted)
        {
            var area = map?.AreaAt(_at);
            if (area != null)
                foreach (var e in area.Exits)
                    candidates.Add(new AICandidate { kind = "explore", key = "travel:restart:" + Run + ":" + e.Gate, tile = AITravel.Tile(e.Gate), progress = AIBrain.Goal.kind == GoalKind.Explore,
                        desc = "멈춘 곳에서 다시 출발 — " + AITravel.ExitText(_at, e, IsEntrance(map, e)) + " 원래 목적 유지." });
            else
                foreach (var exit in AITravelRules.Exits(_at, null, VisiblePredicate(server, player)))
                    candidates.Add(new AICandidate { kind = "explore", key = "travel:restart:" + Run + ":" + exit, tile = AITravel.Tile(exit), progress = AIBrain.Goal.kind == GoalKind.Explore,
                        desc = "멈춘 곳에서 " + AITravelRules.Direction(_at, exit) + "쪽 통로로 다시 출발. 원래 목적 유지. " + AITravel.ExitFacts(exit, AITravelRules.DirIndex(_at, exit)) });
            return;
        }
        // the asked area's exits (also while crossing: the model may change its exit, e.g. on a resource question)
        var asked = _askToken == null ? null : AITravel.AreaById(map, _askedArea);
        if (asked == null) return;
        bool first = true;
        foreach (var e in asked.Exits)
        {
            string key = "travel:turn:" + Run + ":" + _askToken + ":" + e.Gate;
            _offered[key] = ChoiceOf(e, asked.Centre);
            // 최종 검수 S3: the area line as the area looks now, once (with the first exit)
            string head = first ? "질문한 " + AITravel.AreaSummary(asked, _askedArea != _visitArea) + ". " : ""; first = false;
            candidates.Add(new AICandidate { kind = "travel_turn", key = key, run = Run, tile = AITravel.Tile(e.Gate), actionMode = _askToken, progress = true,
                exploreDirection = AIAreaRules.SectorNames[e.Sector], desc = head + AITravel.ExitText(_at, e, _askedArea == _visitArea && IsEntrance(map, e), _mode == Mode.Cross && _crossing != null && e.Has(_crossing.Gate)) });
        }
    }
    public string ValidateTurn(World server, Entity player, AICandidate c)
    {
        if (_halted) return "탐색이 멈춰 새 수단 판단이 필요함";
        if (c.actionMode != _askToken || !_offered.TryGetValue(c.key ?? "", out var ch)) return "질문한 공간 출구 목록이 바뀜";
        var map = AITravel.Areas(_at, true); Track(map);
        var area = AITravel.AreaById(map, _askedArea);
        return Way(map, area, ch, _at) != null ? null : "고른 출구가 지금 그 공간의 출구로 보이지 않음";
    }
    // Where a chosen exit is crossed to now: the passage itself, or for an unseen side its nearest edge cell in the chosen
    // wedge, or (that side seen to its end) its one passage. null = the chosen exit is not there any more.
    private Choice Way(AIAreaRules.Map map, AIAreaRules.Area area, Choice ch, AITravelRules.Cell from)
    {
        if (area == null || ch == null) return null;
        if (ch.Kind != AIAreaRules.ExitKind.Frontier)
        {
            var e = Resolve(area, ch);
            return e == null ? null : ChoiceOf(e, ch.Origin);
        }
        if (FrontierTarget(area, ch, from, null, out var cell, out int dir))
            return new Choice { Kind = ch.Kind, Sector = ch.Sector, Origin = ch.Origin, Gate = cell, Out = dir };
        var side = SidePassages(map, area, ch);
        return side.Count == 1 ? ChoiceOf(side[0], ch.Origin) : null;
    }
    public void ReserveTurn(AICandidate c)
    {
        if (c.key != null && _offered.TryGetValue(c.key, out var ch)) { _reserved = ch; _reservedToken = c.actionMode; }
    }
    private void ObserveStopped(string key, string reason)
    {
        _haltObservationKey = key;
        _haltObservationReason = reason;
        ObserveOnce(key, reason);
    }
    private BodyInput Halt(World server, string outcome, string key, string reason)
    {
        _halted = true; _navActive = false; _nav.Dispose(server);
        CloseBranch(outcome);
        ObserveStopped(key, reason);
        return BodyInput.Idle;
    }
    private BodyInput TakeReserved(World server, Entity player, float now, AIAreaRules.Map map)
    {
        var ch = _reserved; _reserved = null; _reservedToken = null;
        var way = Way(map, AITravel.AreaById(map, _visitArea), ch, _at);
        if (way == null) return Halt(server, "고른 출구가 사라짐", "gone:" + ch.Gate, "고른 출구가 지금 이 공간의 출구로 보이지 않음(막힘 또는 모양이 바뀜). 다른 출구·다른 행동을 직접 선택.");
        OpenBranch(way.Gate, way.Out); BeginCross(server, player, now, way, map);
        return NavStep(server, player, now);
    }
    // One navigator tick toward the current target; Arrived/Failed are returned to the mode logic.
    private NavStatus NavTick(World server, Entity player, float now, out float2 move)
    {
        move = float2.zero;
        if (!_navActive) return NavStatus.Arrived;
        var r = _nav.Tick(server, player, now, out move);
        if (r != NavStatus.Moving) { _navActive = false; _nav.Dispose(server); }
        return r;
    }
    private BodyInput NavStep(World server, Entity player, float now)
    {
        var r = NavTick(server, player, now, out float2 move);
        if (r == NavStatus.Moving) return new BodyInput { move = move, slot = -1 };
        if (r == NavStatus.Failed) return Halt(server, "이동 실패로 멈춤", "nav:" + _navTarget + ":" + _nav.FailReason, "탐색 이동 실패: " + _nav.FailReason + ". 새 수단은 판단모델이 선택.");
        return BodyInput.Idle;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        if (AITestControl.Held) return BodyInput.Idle;   // TEST ONLY /ai hold: no walking in, crossing, following or area question (the body also stops the run)
        AITravel.ObserveView(server, player);
        if (_halted)
        {
            ObserveOnce(_haltObservationKey, _haltObservationReason);
            return BodyInput.Idle;
        }
        var here = AITravel.Cell(AIBody.TileOf(AIBody.Pos(server, player)));
        if (!here.Equals(_at)) { _branchSteps += AIAreaRules.Dist(here, _at); _prevCell = _at; _at = here; }   // a step counts when walked (검수 S5)
        var map = AITravel.Areas(here);
        Track(map);
        var area = map?.AreaAt(here); int id = AITravel.AreaId(area);
        if (area == null && map?.ConnectorAt(here) != null) { _lastOutside = here; if (_mode == Mode.Follow) _leftArea = true; }

        // entering an area: another area, or any area again after following a connector out of it (a new visit). A relabel
        // of the area under the character is not an entry (Track above).
        if (area != null && id != 0 && (id != _visitArea || _mode == Mode.Follow && _leftArea))
        {
            CloseBranch($"공간 {id}로 이어짐");
            AITravel.NoteAreaVisit(id); _visitArea = id; _leftArea = false; _visitCell = here; _visitIds.Clear(); _visitIds.Add(id);
            bool askedAhead = _askedAhead == id && _askedArea == id; _askedAhead = 0;
            if (!askedAhead) Ask(area, here, "새 공간 출구 선택 필요");
            if (_reserved != null && _reservedToken == _askToken) return TakeReserved(server, player, now, map);
            BeginWander(server, player, now, area);
        }
        if (area != null && id == _visitArea) _visitCell = here;
        // an exit chosen for the area of this visit (waiting, walking in, or changing the exit while crossing)
        if (_reserved != null && _reservedToken == _askToken && _askedArea == _visitArea && _mode != Mode.Follow)
            return TakeReserved(server, player, now, map);

        switch (_mode)
        {
            case Mode.Cross: return TickCross(server, player, now, map, here);
            case Mode.Follow: return TickFollow(server, player, now, map, area, here);
            case Mode.Wander:
            {
                var step = NavStep(server, player, now);
                if (_halted || _navActive) return step;
                if (++_wanderIndex < _wander.Count) { NavTo(server, player, now, _wander[_wanderIndex]); return NavStep(server, player, now); }
                _mode = Mode.Wait; goto case Mode.Wait;
            }
            case Mode.Wait:
                // standing for the answer: the question stays the pending observation (re-observed after 5 s, AISkill)
                if (_askKey != null && _askedArea == _visitArea) ObserveOnce(_askKey, _askReason);
                return BodyInput.Idle;
        }
        return BodyInput.Idle;
    }
    private BodyInput TickCross(World server, Entity player, float now, AIAreaRules.Map map, AITravelRules.Cell here)
    {
        var visit = AITravel.AreaById(map, _visitArea);
        bool side = _crossing.Kind == AIAreaRules.ExitKind.Frontier;
        // the analysis changed while crossing: follow a moved gate; a chosen passage that is gone stops the run (design 2.3)
        if (map != _crossMap && visit != null)
        {
            _crossMap = map;
            if (side)
            {
                // 최종 검수 G2: the chosen unseen side keeps its bearing (the wedge from the fixed origin); when that wedge has
                // no unknown edge left, only its one passage goes on (SidePassages), else stop and ask
                if (!FrontierTarget(visit, _crossing, here, null, out var cell, out _)) return SideSeen(server, player, now, map, visit, here);
                if (!cell.Equals(_crossing.Gate)) { _crossing.Gate = cell; NavTo(server, player, now, cell); }
            }
            else
            {
                var e = Resolve(visit, _crossing);
                if (e == null)
                    return Halt(server, "고른 출구가 막힘", "gone:" + _crossing.Gate, "고른 출구가 건너는 중에 막히거나 다르게 보임. 다른 출구·다른 행동을 직접 선택.");
                if (!e.Has(_crossing.Gate)) { _crossing = ChoiceOf(e, _crossing.Origin); NavTo(server, player, now, e.Gate); }
            }
        }
        var step = NavStep(server, player, now);
        if (_halted || _navActive) return step;
        // arrived at the gate
        var conn = map?.ConnectorAt(here);
        if (conn != null)
        {
            var entry = _prevCell; foreach (var o in conn.Openings) if (o.Inside.Equals(here)) { entry = o.Outside; break; }
            BeginFollow(entry, here);
            return TickFollow(server, player, now, map, null, here);
        }
        if (side && visit != null)
        {
            // 최종 검수 G2/S2: at an edge cell of the chosen side, go on to its next edge cell in the same wedge once the view
            // has shown something new; nothing left in the wedge = that side seen (no 'any exit within 3 cells' shortcut)
            if (!FrontierTarget(visit, _crossing, here, here, out var cell, out _)) return SideSeen(server, player, now, map, visit, here);
            if (map != _crossArrivalMap) { _crossArrivalMap = map; _edgeSince = -1f; _crossing.Gate = cell; NavTo(server, player, now, cell); return NavStep(server, player, now); }
            if (_edgeSince < 0) _edgeSince = now;
            if (now - _edgeSince < EdgeWaitSeconds) return BodyInput.Idle;
            return Halt(server, "앞이 보이지 않아 멈춤", "dark:" + here, "고른 못 본 쪽 끝에서 더 보이지 않아 대기 중. 공간의 다른 출구·다른 행동을 직접 선택.");
        }
        return Halt(server, "고른 출구가 사라짐", "gone:" + here, "고른 출구에 도착했지만 이어지는 통로·공간이 보이지 않음. 다른 출구·다른 행동을 직접 선택.");
    }
    // The chosen unseen side has no unknown edge left in its wedge: its one passage is the way on (user-approved), none
    // or several = stop and ask.
    private BodyInput SideSeen(World server, Entity player, float now, AIAreaRules.Map map, AIAreaRules.Area visit, AITravelRules.Cell here)
    {
        var passages = SidePassages(map, visit, _crossing);
        if (passages.Count == 1) { _crossing = ChoiceOf(passages[0], _crossing.Origin); NavTo(server, player, now, passages[0].Gate); return NavStep(server, player, now); }
        return Halt(server, "고른 쪽 끝이 막힘", "seen:" + here, "고른 못 본 쪽이 다 보였고 그쪽으로 이어지는 통로가 " + (passages.Count == 0 ? "없음" : "여럿임") + ". 공간의 다른 출구·다른 행동을 직접 선택.");
    }
    private AIAreaRules.Map _crossArrivalMap;
    private BodyInput TickFollow(World server, Entity player, float now, AIAreaRules.Map map, AIAreaRules.Area area, AITravelRules.Cell here)
    {
        // the first step leads straight into an area (a corridor start at its mouth): walk in, the entry asks
        if (map != null && map.AreaIndex(_firstCell) >= 0 && !_leftArea && map.ConnectorAt(here) != null && here.Equals(_entry))
        { NavTo(server, player, now, _firstCell); return NavStep(server, player, now); }
        var conn = map?.ConnectorAt(here) ?? map?.ConnectorAt(_firstCell);
        if (conn == null)
        {
            if (_navActive) return NavStep(server, player, now);   // a cell the analysis does not hold yet: keep the last way
            return Halt(server, "통로를 확인하지 못해 멈춤", "lost:" + here, "지금 자리가 본 통로·공간으로 확인되지 않아 대기 중. 다른 수단은 판단모델이 선택.");
        }
        if (!conn.Cells.Contains(_firstCell) && conn.Cells.Contains(here)) { _entry = _prevCell; _firstCell = here; }   // the connector was relabelled
        var f = AIAreaRules.FollowOn(map, conn, _entry, _firstCell);
        switch (f.Kind)
        {
            case AIAreaRules.FollowKind.Way:
                if (f.ToArea >= 0)
                {
                    // the next area is in the observed map: ask its exits now so the answer can come before arrival
                    var next = map.Areas[f.ToArea]; int nid = AITravel.AreaId(next);
                    if (nid != 0 && _askedAhead != nid) { Ask(next, f.Target, "다음 공간 출구 선택 필요"); _askedAhead = nid; }
                }
                if (f.Target.Equals(here))
                {
                    if (_edgeSince < 0) _edgeSince = now;
                    if (now - _edgeSince < EdgeWaitSeconds) return BodyInput.Idle;
                    return Halt(server, "앞이 보이지 않아 멈춤", "dark:" + here, "통로 앞이 보이지 않아(본 칸 끝) 대기 중. 원래 목적 유지. 다른 수단·재계획을 직접 선택.");
                }
                _edgeSince = -1f;
                if (_alongMap != map || !_alongFrom.Equals(here) || !_alongTo.Equals(f.Target)) { _along = AIAreaRules.PathAlong(conn, here, f.Target); _alongMap = map; _alongFrom = here; _alongTo = f.Target; }
                var along = _along;
                var waypoint = along.Count == 0 ? f.Target : along[Math.Min(AIAreaRules.FollowLead, along.Count) - 1];
                if (!_navActive || !along.Contains(_navTarget) || AIAreaRules.Dist(here, _navTarget) <= 1) NavTo(server, player, now, waypoint);
                return NavStep(server, player, now);
            case AIAreaRules.FollowKind.Fork:
                return Halt(server, "갈래에서 멈춤", "fork:" + here, "통로가 여러 갈래로 보임. 갈래를 직접 선택해야 함.");
            case AIAreaRules.FollowKind.Loop:
                return Halt(server, "이미 지나온 통로로 되돌아오는 순환", "cycle:" + here, "보이는 통로가 고리로 이어져 새 끝이 없음(순환). 실행은 대기 중이며 새 출구·다른 작업 선택이 필요함.");
            default:
                return Halt(server, "막다른 길(통로 끝 또는 막힘)", "end:" + here, "보이는 통로 끝 또는 막힘에서 대기 중. 원래 목적 유지. 되돌아가기·다른 수단·재계획을 직접 선택.");
        }
    }
    public override string Progress(World server, Entity player)
        => !_started ? "통로 탐색 준비" : _halted ? "멈춰서 다음 행동 선택을 기다림" : _mode == Mode.Cross ? "고른 출구로 공간을 건너는 중" : _mode == Mode.Follow ? "통로를 따라 걷는 중, 다음 공간에서 출구를 고름"
            : _mode == Mode.Wander ? "출구 답을 기다리며 공간 안쪽으로 걷는 중" : "출구 선택을 기다림. 계속하기를 골라도 출구가 정해지지 않아 서 있음. 출구 후보 또는 다른 행동을 선택해야 이동함.";
    public override void Cleanup(World server) { CloseBranch("결과 전에 다른 행동으로 바꿈", true); _nav.Dispose(server); }
}
