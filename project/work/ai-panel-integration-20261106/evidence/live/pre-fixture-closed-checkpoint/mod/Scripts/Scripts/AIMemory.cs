// AIExplore — exploration memory and "dig toward" (tunnelling) for the AI body.
// Design: work/2026-11/2026-11-03-파들어가기-탐색기억.md (session 코어키퍼-AI-캐릭터).
//
// - AIMemory: per world, what the AI has actually seen (4x4-tile cells inside its view rectangle), where it has stood,
//   and ore walls it has seen (with the time). Persisted next to the per-world AI character:
//   <persistentDataPath>/AIPlayers/<world ServerGuid>/<member id>/memory.json (per character). Remembered ores are offered to the decision layer
//   only as "remembered (last seen T ago)" — never as live observations. A remembered ore is forgotten when the AI
//   sees that tile again without the ore.
// - AITunnel.Plan: cheapest route from the AI to a target through walkable tiles and diggable walls (Dijkstra; a wall
//   costs by estimated hits = ceil(hp / (damage - reduction))). Only tiles the AI can see now or has seen before are
//   used; walls the estimated damage can't hurt are avoided and counted as "도구 부족".
// - TunnelSkill: alternates mining the next wall on the planned route (MineSkill) and walking (GotoSkill), replanning
//   after every step, with observed progress/fail facts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIMemory
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public readonly Dictionary<int2, Station> _stations = new Dictionary<int2, Station>();
        public readonly Dictionary<(int id, int2 t), long> _noDamage = new Dictionary<(int, int2), long>();
        public string _world;
        public string _path;
        public readonly HashSet<int2> _seen = new HashSet<int2>();
        public readonly HashSet<int2> _visited = new HashSet<int2>();
        public readonly Dictionary<int2, Ore> _ores = new Dictionary<int2, Ore>();
        public bool _dirty;
        public float _lastObserve = -999f;
        public float _lastSave;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Memory);
    private static State S => StateOf(AIAgent.Cur);

    public const int CellSize = 4;

    [Serializable]
    public class Data
    {
        public int version = 1;
        public int[] seen = new int[0];      // x0,y0,x1,y1,... cell coordinates
        public int[] visited = new int[0];
        // remembered ores as parallel primitive arrays (JsonUtility drops nested custom classes in mod-compiled code)
        public int[] oreXY = new int[0];     // x0,y0,x1,y1,...
        public int[] oreId = new int[0];
        public int[] oreTs = new int[0];
        public long[] oreSeen = new long[0];
        // mining bonus last seen while holding each tool (so tunnel plans know it before the tool is held again)
        public int[] toolId = new int[0];
        public int[] toolBonus = new int[0];
        // learned combat numbers (AIVitals.ExportLearned)
        public int[] combatLearned = new int[0];
        // crafting stations the AI has seen or placed (entity tile), with the time last seen and whether it placed it
        public int[] stationId = new int[0];
        public int[] stationXY = new int[0];
        public long[] stationSeen = new long[0];
        public int[] stationPlaced = new int[0];
        // learned drops (AIDrops.Export): flat records [source, item, times, total, breaks]
        public int[] drops = new int[0];
        // objects that took no damage when hit (BreakSkill): object id, entity tile, when
        public int[] noDmgId = new int[0];
        public int[] noDmgXY = new int[0];
        public long[] noDmgSeen = new long[0];
        // base and chests (AIBase.Export / Import): base tile, chests seen or placed, what was inside when last opened
        public int baseSet;
        public int[] baseXY = new int[0];
        public string baseBy = "";
        public long baseSeen;
        public int[] chestId = new int[0];
        public int[] chestXY = new int[0];
        public long[] chestSeen = new long[0];
        public int[] chestPlaced = new int[0];
        public long[] chestOpened = new long[0];
        public int[] chestSlots = new int[0];   // slots, free per chest
        public int[] chestItems = new int[0];   // flat [chest index, item, amount]
        // own farm plots and known water tiles (AIFarm.Export / Import, 먹거리 자급 2)
        public int[] farmXY = new int[0];
        public long[] farmSince = new long[0];
        public int[] waterXY = new int[0];
        public long[] waterSeen = new long[0];
        // own cooking pots (AICook.Export / Import, 먹거리 자급 3)
        public int[] potId = new int[0];
        public int[] potXY = new int[0];
        public long[] potSeen = new long[0];
        // the base edge (AIEdge.Export / Import, 코어-안전지대 단계 2): plan (seed, inside tiles, gap tiles as
        // [gap, x, y], door tile) and what was placed for it ([item, x, y]) — the reference for repairs (단계 3)
        public int edgeSet;
        public int[] edgeSeed = new int[0];
        public int[] edgeIn = new int[0];
        public int[] edgePlan = new int[0];
        public int[] edgeDoorAt = new int[0];
        public int[] edgeWalls = new int[0];
        public int[] edgeDoors = new int[0];
        public int[] edgeLights = new int[0];
        public int[] edgeMyDoors = new int[0];   // 제작대-놓기 §6: [x, y] doors the AI placed off the edge plan (AIEdge my doors)
        public int[] edgeSeen = new int[0];   // 재검수 R3: [x, y, open 0/1] the last look of each edge tile (EdgeLastSeen)
    }

    public struct Ore { public ObjectID ore; public int tileset; public long seenUnix; }

    // A crafting station the AI saw (or placed). Key = the entity's tile. Out of view it is offered only as
    // "known station at X, last seen T"; it is forgotten when the AI sees that spot without it.
    public struct Station { public ObjectID id; public int2 tile; public long seenUnix; public bool placed; public int goneSeen; }
    private static Dictionary<int2, Station> _stations => S._stations;
    public static int StationCount => _stations.Count;
    // objects that did not take damage (id + tile -> unix time), across joins; shown as a fact, not a ban
    private static Dictionary<(int id, int2 t), long> _noDamage => S._noDamage;

    private static string _world { get => S._world; set => S._world = value; }
    private static string _path { get => S._path; set => S._path = value; }
    private static HashSet<int2> _seen => S._seen;
    private static HashSet<int2> _visited => S._visited;
    private static Dictionary<int2, Ore> _ores => S._ores;
    private static bool _dirty { get => S._dirty; set => S._dirty = value; }
    private static float _lastObserve { get => S._lastObserve; set => S._lastObserve = value; }
    private static float _lastSave { get => S._lastSave; set => S._lastSave = value; }
    public static int SeenCount => _seen.Count;
    public static int VisitedCount => _visited.Count;
    public static int OreCount => _ores.Count;

    public static int2 CellOf(int2 t) => new int2((int)math.floor(t.x / (float)CellSize), (int)math.floor(t.y / (float)CellSize));
    public static bool SeenTile(int2 t) { Ensure(); return _seen.Contains(CellOf(t)); }

    // Per character (multi-AI decision 2026-11-04: each character remembers only what it saw itself, like a person):
    // <persistentDataPath>/AIPlayers/<world>/<member id>/ holds memory.json and deaths.log.
    public static string CharDir(string worldId)
    {
        string d = Path.Combine(Application.persistentDataPath, "AIPlayers", worldId ?? "", AIAgent.Cur.Id);
        try { Directory.CreateDirectory(d); } catch { }
        return d;
    }

    // Before per-character memory there was one AIPlayers/<world>/memory.json (and deaths.log); it was Claude's. Claude
    // starts from a copy of it the first time; the old files are left where they were.
    private static void MigrateLegacy(string w)
    {
        if (AIAgent.Cur.Id != "claude") return;
        string root = Path.Combine(Application.persistentDataPath, "AIPlayers", w);
        foreach (var f in new[] { "memory.json", "deaths.log" })
        {
            string from = Path.Combine(root, f), to = Path.Combine(CharDir(w), f);
            if (File.Exists(from) && !File.Exists(to))
            {
                File.Copy(from, to);
                AIPlayerSpikeMod.Log($"MEMORY: copied the single-AI {f} to Claude's own folder ({to}); the old file is kept");
            }
        }
    }

    private static bool Ensure()
    {
        string w = AIPlayerSpikeMod.WorldId;
        if (string.IsNullOrEmpty(w)) return false;
        if (w == _world) return true;
        if (_world != null) Save(true);
        _world = w;
        _seen.Clear(); _visited.Clear(); _ores.Clear(); _stations.Clear(); _noDamage.Clear(); AIDrops.Clear(); AIObjects.ClearMemory(); AIBase.ClearMemory(); AIFarm.ClearMemory(); AICook.ClearMemory(); AIEdge.ClearMemory(); _dirty = false;
        _path = Path.Combine(CharDir(w), "memory.json");
        try
        {
            MigrateLegacy(w);
            if (File.Exists(_path))
            {
                var d = JsonUtility.FromJson<Data>(File.ReadAllText(_path, Encoding.UTF8));
                if (d != null)
                {
                    for (int i = 0; d.seen != null && i + 1 < d.seen.Length; i += 2) _seen.Add(new int2(d.seen[i], d.seen[i + 1]));
                    for (int i = 0; d.visited != null && i + 1 < d.visited.Length; i += 2) _visited.Add(new int2(d.visited[i], d.visited[i + 1]));
                    for (int i = 0; d.toolId != null && d.toolBonus != null && i < d.toolId.Length && i < d.toolBonus.Length; i++)
                        if (!AITunnel.ToolBonus.ContainsKey((ObjectID)d.toolId[i])) AITunnel.ToolBonus[(ObjectID)d.toolId[i]] = d.toolBonus[i];
                    AIVitals.ImportLearned(d.combatLearned);
                    for (int i = 0; d.oreXY != null && d.oreId != null && d.oreTs != null && d.oreSeen != null && i < d.oreId.Length && 2 * i + 1 < d.oreXY.Length && i < d.oreTs.Length && i < d.oreSeen.Length; i++)
                        _ores[new int2(d.oreXY[2 * i], d.oreXY[2 * i + 1])] = new Ore { ore = (ObjectID)d.oreId[i], tileset = d.oreTs[i], seenUnix = d.oreSeen[i] };
                    for (int i = 0; d.stationId != null && d.stationXY != null && d.stationSeen != null && i < d.stationId.Length && 2 * i + 1 < d.stationXY.Length && i < d.stationSeen.Length; i++)
                    {
                        var t = new int2(d.stationXY[2 * i], d.stationXY[2 * i + 1]);
                        _stations[t] = new Station { id = (ObjectID)d.stationId[i], tile = t, seenUnix = d.stationSeen[i], placed = d.stationPlaced != null && i < d.stationPlaced.Length && d.stationPlaced[i] != 0 };
                    }
                    AIDrops.Import(d.drops);
                    for (int i = 0; d.noDmgId != null && d.noDmgXY != null && d.noDmgSeen != null && i < d.noDmgId.Length && 2 * i + 1 < d.noDmgXY.Length && i < d.noDmgSeen.Length; i++)
                        _noDamage[(d.noDmgId[i], new int2(d.noDmgXY[2 * i], d.noDmgXY[2 * i + 1]))] = d.noDmgSeen[i];
                    AIBase.Import(d);
                    AIFarm.Import(d);
                    AICook.Import(d);
                    AIEdge.Import(d);
                }
            }
            AIPlayerSpikeMod.Log($"MEMORY loaded for world {w}: seen cells {_seen.Count}, visited cells {_visited.Count}, remembered ores {_ores.Count}, stations {_stations.Count} [{StationsText()}], learned drops [{AIDrops.Summary()}], no-damage objects {_noDamage.Count} ({_path})");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"MEMORY: reading {_path} failed; starting empty", e); }
        return true;
    }

    public static void Save(bool force)
    {
        AIKnowledge.Save(force);   // known.json saves with memory.json on leave / world change
        if (_world == null || _path == null || (!_dirty && !force)) return;
        try
        {
            var d = new Data();
            var s = new List<int>(_seen.Count * 2); foreach (var c in _seen) { s.Add(c.x); s.Add(c.y); }
            var v = new List<int>(_visited.Count * 2); foreach (var c in _visited) { v.Add(c.x); v.Add(c.y); }
            var oxy = new List<int>(); var oid = new List<int>(); var ots = new List<int>(); var osn = new List<long>();
            foreach (var kv in _ores) { oxy.Add(kv.Key.x); oxy.Add(kv.Key.y); oid.Add((int)kv.Value.ore); ots.Add(kv.Value.tileset); osn.Add(kv.Value.seenUnix); }
            var ti = new List<int>(); var tb = new List<int>();
            foreach (var kv in AITunnel.ToolBonus) { ti.Add((int)kv.Key); tb.Add(kv.Value); }
            d.toolId = ti.ToArray(); d.toolBonus = tb.ToArray();
            d.combatLearned = AIVitals.ExportLearned();
            var sid = new List<int>(); var sxy = new List<int>(); var ssn = new List<long>(); var spl = new List<int>();
            foreach (var kv in _stations) { sid.Add((int)kv.Value.id); sxy.Add(kv.Key.x); sxy.Add(kv.Key.y); ssn.Add(kv.Value.seenUnix); spl.Add(kv.Value.placed ? 1 : 0); }
            d.stationId = sid.ToArray(); d.stationXY = sxy.ToArray(); d.stationSeen = ssn.ToArray(); d.stationPlaced = spl.ToArray();
            d.drops = AIDrops.Export();
            var nid = new List<int>(); var nxy = new List<int>(); var nsn = new List<long>();
            foreach (var kv in _noDamage) { nid.Add(kv.Key.id); nxy.Add(kv.Key.t.x); nxy.Add(kv.Key.t.y); nsn.Add(kv.Value); }
            d.noDmgId = nid.ToArray(); d.noDmgXY = nxy.ToArray(); d.noDmgSeen = nsn.ToArray();
            AIBase.Export(d);
            AIFarm.Export(d);
            AICook.Export(d);
            AIEdge.Export(d);
            d.seen = s.ToArray(); d.visited = v.ToArray(); d.oreXY = oxy.ToArray(); d.oreId = oid.ToArray(); d.oreTs = ots.ToArray(); d.oreSeen = osn.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(d), new UTF8Encoding(false));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
            _dirty = false;
            _lastSave = Time.realtimeSinceStartup;
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"MEMORY: writing {_path} failed", e); }
    }

    // Called every tick by the body executor; does work about once per second.
    public static void Observe(World server, Entity player, float now, bool force = false)
    {
        if (!force && now - _lastObserve < 1f) return;
        _lastObserve = now;
        if (!Ensure()) return;
        var em = server.EntityManager;
        if (!em.Exists(player) || !em.HasComponent<LocalTransform>(player)) return;
        if (em.HasComponent<HealthCD>(player) && em.GetComponentData<HealthCD>(player).health <= 0) return;
        float3 pos = em.GetComponentData<LocalTransform>(player).Position;
        float2 half = AIVision.HalfExtents();
        int2 me = AIBody.TileOf(pos);
        if (_visited.Add(CellOf(me))) _dirty = true;

        // cells whose centre is inside the view rectangle
        int2 c0 = CellOf(new int2((int)math.floor(pos.x - half.x), (int)math.floor(pos.z - half.y)));
        int2 c1 = CellOf(new int2((int)math.ceil(pos.x + half.x), (int)math.ceil(pos.z + half.y)));
        for (int cx = c0.x; cx <= c1.x; cx++)
            for (int cy = c0.y; cy <= c1.y; cy++)
            {
                float2 centre = new float2(cx * CellSize + (CellSize - 1) * 0.5f, cy * CellSize + (CellSize - 1) * 0.5f);
                if (AIVision.CanSee(pos, centre) && _seen.Add(new int2(cx, cy))) _dirty = true;
            }

        // ore walls in view: remember (with time); a remembered ore seen again without its ore is forgotten
        var tr = new AITileReader(server);
        long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int hx = (int)math.floor(half.x), hz = (int)math.floor(half.y);
        for (int x = me.x - hx; x <= me.x + hx; x++)
            for (int z = me.y - hz; z <= me.y + hz; z++)
            {
                var p = new int2(x, z);
                if (!AIVision.CanSee(pos, p)) continue;
                bool wall = tr.HasWall(p, out TileCD w, out bool ore, out int oreTs);
                if (wall && ore)
                {
                    AIBrain.TryOreObject(server, oreTs, out ObjectID obj);
                    if (!_ores.TryGetValue(p, out var old) || old.ore != obj || unix - old.seenUnix >= 30) _dirty = true;
                    _ores[p] = new Ore { ore = obj, tileset = oreTs, seenUnix = unix };
                }
                else if (_ores.ContainsKey(p))
                {
                    _ores.Remove(p);
                    _dirty = true;
                    AIPlayerSpikeMod.Log($"MEMORY: ore at ({p.x},{p.y}) seen gone; forgotten");
                }
            }
        ObserveStations(server, player, pos, unix);
        try { AIBase.Observe(server, player, pos, unix); } catch (Exception e) { AIPlayerSpikeMod.LogError("MEMORY: chests in view failed", e); }
        AIKnowledge.Note(server, player, now);   // seen/held for the knowledge graph (own file, own timer)
        try { AIFarm.Observe(server, player, pos, unix); } catch (Exception e) { AIPlayerSpikeMod.LogError("MEMORY: farm plots / water in view failed", e); }
        try { AICook.Observe(server, player, pos, unix); } catch (Exception e) { AIPlayerSpikeMod.LogError("MEMORY: cooking pots in view failed", e); }
        if (_dirty && now - _lastSave > 30f) Save(false);
    }

    // Crafting stations in view are remembered (kind, tile, time). A remembered station whose tile is in view while no
    // station of that kind is there, on two observations in a row (about 2 s; a station entity can appear a moment
    // after its area loads), is forgotten.
    private static void ObserveStations(World server, Entity player, float3 pos, long unix)
    {
        var inView = new Dictionary<int2, ObjectID>();
        try { foreach (var s in AICraft.StationsInView(server, player)) inView[AIBody.TileOf(s.pos)] = s.id; }
        catch (Exception e) { AIPlayerSpikeMod.LogError("MEMORY: stations in view failed", e); return; }
        foreach (var kv in inView)
        {
            bool had = _stations.TryGetValue(kv.Key, out var old);
            if (!had || old.id != kv.Value)
            {
                AIPlayerSpikeMod.Log($"MEMORY: station {kv.Value} seen at ({kv.Key.x},{kv.Key.y}){(had ? $" (was {old.id})" : "")}; remembered");
                old = new Station { id = kv.Value, tile = kv.Key };
                _dirty = true;
            }
            if (unix - old.seenUnix >= 30) _dirty = true;
            old.seenUnix = unix; old.goneSeen = 0;
            _stations[kv.Key] = old;
        }
        foreach (var t in new List<int2>(_stations.Keys))
        {
            if (inView.ContainsKey(t) || !AIVision.CanSee(pos, t)) continue;
            var st = _stations[t];
            st.goneSeen++;
            if (st.goneSeen >= 2)
            {
                _stations.Remove(t);
                _dirty = true;
                AIPlayerSpikeMod.Log($"MEMORY: station {st.id} at ({t.x},{t.y}) seen gone; forgotten (last seen {Ago(st.seenUnix)}{(st.placed ? ", placed by the AI" : "")})");
            }
            else _stations[t] = st;
        }
    }

    // The AI placed a station (PlaceSkill saw it next to itself).
    public static void NotePlaced(ObjectID id, float3 p)
    {
        if (!Ensure()) return;
        int2 t = AIBody.TileOf(p);
        _stations[t] = new Station { id = id, tile = t, seenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), placed = true };
        _dirty = true;
        AIPlayerSpikeMod.Log($"MEMORY: station {id} placed by the AI at ({t.x},{t.y}); remembered");
    }

    // 제작대-놓기 (places, AIPlaceGoal): every remembered station (in view too), one at a tile, and remembered ores
    public static List<Station> AllStations() { Ensure(); return new List<Station>(_stations.Values); }
    public static bool StationAt(int2 t, out Station s) { Ensure(); return _stations.TryGetValue(t, out s); }
    public static bool OreAt(int2 t, out Ore o) { Ensure(); return _ores.TryGetValue(t, out o); }
    public static List<(int2 p, Ore o)> AllOres() { Ensure(); var l = new List<(int2, Ore)>(); foreach (var kv in _ores) l.Add((kv.Key, kv.Value)); return l; }

    // Remembered stations not in view now, nearest first.
    public static List<Station> RememberedStations(float3 aiPos)
    {
        Ensure();
        var l = new List<Station>();
        foreach (var kv in _stations) if (!AIVision.CanSee(aiPos, kv.Key)) l.Add(kv.Value);
        l.Sort((a, b) => math.distance(aiPos.xz, (float2)a.tile).CompareTo(math.distance(aiPos.xz, (float2)b.tile)));
        return l;
    }

    public static string StationsText(float3? aiPos = null)
    {
        var parts = new List<string>();
        foreach (var kv in _stations)
            parts.Add($"{kv.Value.id} ({kv.Key.x},{kv.Key.y}){(aiPos.HasValue ? $" {math.distance(aiPos.Value.xz, (float2)kv.Key):F0}칸" : "")} {Ago(kv.Value.seenUnix)}에 봄{(kv.Value.placed ? ", " + AIAgent.Cur.Name + "가 놓음" : "")}");
        return string.Join("; ", parts);
    }

    public static void NoteNoDamage(ObjectID id, float3 p)
    {
        if (!Ensure()) return;
        _noDamage[((int)id, AIBody.TileOf(p))] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _dirty = true;
    }

    public static bool NoDamageAt(ObjectID id, float3 p, out long when) { Ensure(); return _noDamage.TryGetValue(((int)id, AIBody.TileOf(p)), out when); }

    // Not-yet-seen cells that standing at tile t would bring into view (same rule as Observe: cell centre inside
    // the view rectangle around t). After the AI has stood at t this is 0, so a reached spot is not offered again.
    // TEST ONLY (/ai nonew): pretend every cell has been seen, to exercise the "nothing left to explore" path.
    public static bool TestOnlyNothingNew;

    public static int Novelty(int2 t)
    {
        Ensure();
        if (TestOnlyNothingNew) return 0;
        float2 half = AIVision.HalfExtents();
        var at = new float3(t.x, 0f, t.y);
        int2 c0 = CellOf(new int2((int)math.floor(t.x - half.x), (int)math.floor(t.y - half.y)));
        int2 c1 = CellOf(new int2((int)math.ceil(t.x + half.x), (int)math.ceil(t.y + half.y)));
        int n = 0;
        for (int cx = c0.x; cx <= c1.x; cx++)
            for (int cy = c0.y; cy <= c1.y; cy++)
            {
                float2 centre = new float2(cx * CellSize + (CellSize - 1) * 0.5f, cy * CellSize + (CellSize - 1) * 0.5f);
                // 2-tile margin: arriving a tile or two off the exact spot must still count the spot as explored
                if (AIVision.CanSee(at, centre) && math.all(math.abs(centre - at.xz) <= half - 2f) && !_seen.Contains(new int2(cx, cy))) n++;
            }
        return n;
    }

    // Remembered ores of a given object (None = any), nearest first, excluding tiles the AI can see right now.
    public static List<(int2 p, Ore o)> RememberedOres(float3 aiPos, ObjectID want, int max)
    {
        Ensure();
        var l = new List<(float d, int2 p, Ore o)>();
        foreach (var kv in _ores)
        {
            if (want != ObjectID.None && kv.Value.ore != want) continue;
            if (AIVision.CanSee(aiPos, kv.Key)) continue;
            l.Add((math.distance(aiPos.xz, (float2)kv.Key), kv.Key, kv.Value));
        }
        l.Sort((a, b) => a.d.CompareTo(b.d));
        var r = new List<(int2, Ore)>();
        for (int i = 0; i < l.Count && i < max; i++) r.Add((l[i].p, l[i].o));
        return r;
    }

    // Copy of the seen cells (for the information-leak check of a walk: what was known when it started).
    public static void MarkDirty() { _dirty = true; }
    public static void Load() { Ensure(); }

    public static HashSet<int2> SnapshotSeen() { Ensure(); return new HashSet<int2>(_seen); }

    public static int CountOres(ObjectID want)
    {
        Ensure();
        int n = 0;
        foreach (var kv in _ores) if (want == ObjectID.None || kv.Value.ore == want) n++;
        return n;
    }

    public static string Ago(long unix)
    {
        long s = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix;
        return s < 60 ? $"{s}초 전" : s < 3600 ? $"{s / 60}분 전" : $"{s / 3600}시간 전";
    }

    public static string Summary()
    {
        Ensure();
        return $"본 구역 {_seen.Count}칸(4x4), 서 본 구역 {_visited.Count}칸, 기억한 광석 {_ores.Count}곳, 기억한 제작대 {_stations.Count}곳";
    }
}
