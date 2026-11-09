// AIExperience — the game side of the upper LLM's experience memory (AIExpCore.cs has the files and the report part).
// Session 코어키퍼-AI-캐릭터, branch LLM-기억: work/2026-11/2026-11-04-LLM-기억/1-구현.md.
//
// Hooks: AIBrain.SetGoal -> GoalStarting, AIBrain.ClearGoal -> GoalEnding (one line each in AIDecisions.cs), and from
// AIPlanner: Tick (every frame, also while the upper LLM is off), Reset, ReportPart (the report's "experience"),
// PlanResult (what happened to each upper-LLM answer). Everything else is observed here: downs (AIVitals.Downs went
// up), back up, fights (who was on screen), bosses (BossCD) seen and killed, entering an area (biome), the first time an
// item is in the inventory. Per character and world, like memory.json. Nothing here changes what the AI does.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIExperience
{
    private sealed class State
    {
        public ExpRecorder rec;
        public string world;
        public World server;
        public Entity player;
        public float lastTick = -1f, nextScan, nextSlow, nextSave;
        public int lastDowns = -1;
        public bool hpKnown, wasDown;
        public bool firstItemScan;
        public string lastEndedKey = "";
        // the fight going on (enemies within 7 tiles on screen)
        public bool inFight;
        public float fightStart, fightLast;
        public int fightDowns0;
        public readonly Dictionary<string, int> fightEnemies = new Dictionary<string, int>();
        public readonly HashSet<string> fightWith = new HashSet<string>();
        public string fightBoss = "";
        // 자연어 명령 (user decisions 3·4): damage taken in the fight, enemies that fell in view, and the fight log the
        // upper LLM's report reads (recent fights, tally per enemy kind)
        public int fightHpPrev = -1, fightDamage;
        public readonly Dictionary<Entity, string> fightSeen = new Dictionary<Entity, string>();
        public readonly Dictionary<string, int> fightFell = new Dictionary<string, int>();
        public AIFightLog Fights = new AIFightLog();
        public string fightsWorld;
        // bosses on screen: last health and when
        public readonly Dictionary<Entity, (string id, int hp, int max, float seen)> bosses = new Dictionary<Entity, (string, int, int, float)>();
        // area (biome) with an 8 s settle time
        public string area = "", areaCand = "";
        public float areaCandSince;
        public readonly Dictionary<string, string> biomeCache = new Dictionary<string, string>();
        // biome samples while a goal runs (its work area), and a world whose memory failed to load this join
        public readonly Dictionary<string, int> runAreas = new Dictionary<string, int>();
        public string runOf = "";
        public string failedWorld;
    }
    private static readonly Dictionary<AIAgent, State> _st = new Dictionary<AIAgent, State>();
    private static State S { get { var a = AIAgent.Cur; if (!_st.TryGetValue(a, out var s)) _st[a] = s = new State(); return s; } }

    public static string KeyOf(AIGoal g)
    {
        if (g == null) return "";
        return AIGoalKinds.ExperienceKey(g.kind, g.item.ToString(), g.item != ObjectID.None);
    }

    private static bool Alive(World server, Entity player) => server != null && server.IsCreated && server.EntityManager.Exists(player);

    private static ExpRecorder Rec(World server, Entity player)
    {
        var s = S;
        string w = AIPlayerSpikeMod.WorldId;
        if (string.IsNullOrEmpty(w)) return null;
        if (s.rec != null && s.world == w) return s.rec;
        if (s.failedWorld == w) return null;   // off until the next join (OnReset)
        if (s.rec != null) { try { s.rec.SaveState(); } catch { } }
        s.rec = null;
        var r = new ExpRecorder(AIMemory.CharDir(w), w, AIAgent.Cur.Id) { Log = m => AIPlayerSpikeMod.Log(m) };
        var sw = Stopwatch.StartNew();
        try { r.Load(Alive(server, player) ? Cond(server, player) : null); }
        catch (Exception e)
        {
            // nothing half-read is written on top of the files: the memory stays off for this join
            AIPlayerSpikeMod.LogError($"exp: load failed for world {w} ({AIAgent.Cur.Id}); experience memory off until the next join", e);
            s.failedWorld = w;
            return null;
        }
        s.rec = r; s.world = w;
        s.firstItemScan = r.Items.Count == 0;
        s.lastDowns = -1; s.hpKnown = false; s.inFight = false; s.bosses.Clear(); s.area = ""; s.areaCand = ""; s.biomeCache.Clear(); s.lastTick = -1f;
        AIPlayerSpikeMod.Log($"EXP: loaded for world {w} ({AIAgent.Cur.Id}): goal runs {r.Book.Rows.Count}, next event id {r.NextId}, play {r.Play / 3600:F1}h, session {r.Sess}, {sw.ElapsedMilliseconds} ms ({r.Dir})");
        return r;
    }

    // ------------------------------------------------------------------ conditions

    // The item level the game shows (CharacterStatsWindow.GetTotalItemLevel): variation if set, else the prefab LevelCD.
    public static int ItemLevel(ObjectDataCD od)
    {
        try
        {
            if (od.variation > 0) return od.variation;
            if (PugDatabase.HasComponent<LevelCD>(od)) return PugDatabase.GetComponent<LevelCD>(od).level;
        }
        catch { }
        return 0;
    }

    // The max health the game uses (HealthCD.GetMaxHealthWithConditions): base max plus the condition effects.
    public static int MaxHp(EntityManager em, Entity player, HealthCD h)
        => em.HasBuffer<SummarizedConditionEffectsBuffer>(player) ? h.GetMaxHealthWithConditions(em.GetBuffer<SummarizedConditionEffectsBuffer>(player)) : h.maxHealth;

    public static ExpCond Cond(World server, Entity player)
    {
        var c = new ExpCond();
        if (!Alive(server, player)) return c;
        var em = server.EntityManager;
        try
        {
            if (em.HasComponent<HealthCD>(player)) c.hpMax = MaxHp(em, player, em.GetComponentData<HealthCD>(player));
            if (em.HasComponent<HungerCD>(player)) c.hunger = em.GetComponentData<HungerCD>(player).hunger;
            if (em.HasComponent<EquippedObjectCD>(player)) c.hand = em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID.ToString();
            float3 pos = em.GetComponentData<LocalTransform>(player).Position;
            int2 t = AIBody.TileOf(pos);
            c.cell = $"{(int)math.floor(t.x / 16f)},{(int)math.floor(t.y / 16f)}";
            c.coreDist = (int)math.round(math.length(pos.xz));
            c.area = BiomeAt(server, t);
            c.mates = AITeammates.Last.Count;
            AIMemory.Load();   // this world's memory read before its counts (SeenCount does not load by itself)
            c.seen = AIMemory.SeenCount;
            var far = new float3(1e7f, 0f, 1e7f);   // RememberedStations leaves out the ones in view: from far away it is all of them
            c.stations = AIMemory.RememberedStations(far).Select(x => x.id).Distinct().Count();
            if (em.HasBuffer<ContainedObjectsBuffer>(player))
            {
                c.toolLv = 0; c.weaponLv = 0; c.armorLv = 0;
                var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
                for (int i = 0; i < buf.Length; i++)
                {
                    if (buf[i].objectID == ObjectID.None) continue;
                    ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
                    if (info == null) continue;
                    var ty = info.objectType;
                    bool broken = (ty == ObjectType.MiningPick || ty == ObjectType.MeleeWeapon || ty == ObjectType.RangeWeapon) && PlayerController.ItemIsBroken(buf[i].objectData);
                    if (broken) continue;
                    int lv = ItemLevel(buf[i].objectData);   // ObjectInfo.level is 0 for items: the game keeps it in LevelCD / variation
                    if (ty == ObjectType.MiningPick && (lv > c.toolLv || c.tool == "")) { c.toolLv = math.max(c.toolLv, lv); if (lv >= c.toolLv) c.tool = buf[i].objectID.ToString(); }
                    else if ((ty == ObjectType.MeleeWeapon || ty == ObjectType.RangeWeapon) && (lv > c.weaponLv || c.weapon == "")) { c.weaponLv = math.max(c.weaponLv, lv); if (lv >= c.weaponLv) c.weapon = buf[i].objectID.ToString(); }
                    else if ((ty == ObjectType.Helm || ty == ObjectType.BreastArmor || ty == ObjectType.PantsArmor) && lv > c.armorLv) c.armorLv = lv;
                }
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: conditions failed", e); }
        return c;
    }

    // The biome at a tile (the game's BiomeSamplesCD, else BiomeRangesCD), cached per 16-tile cell; "" if unknown.
    private static string BiomeAt(World server, int2 t)
    {
        var s = S;
        string key = $"{(int)math.floor(t.x / 16f)},{(int)math.floor(t.y / 16f)}";
        if (s.biomeCache.TryGetValue(key, out var hit)) return hit;
        string b = "";
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BiomeSamplesCD>()))
                if (q.CalculateEntityCount() == 1) { try { b = new BiomeLookup(q.GetSingleton<BiomeSamplesCD>()).GetBiome(t).ToString(); } catch { b = ""; } }
            if (b == "")
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BiomeRangesCD>()))
                    if (q.CalculateEntityCount() == 1)
                        using (var bl = new BiomeLookup(q.GetSingleton<BiomeRangesCD>().Value, Allocator.Temp)) b = bl.GetBiome(t).ToString();
        }
        catch { b = ""; }
        if (b == "None") b = "";
        if (s.biomeCache.Count > 4000) s.biomeCache.Clear();
        s.biomeCache[key] = b;
        return b;
    }

    // ------------------------------------------------------------------ goal runs (AIDecisions hooks)

    // AIBrain.SetGoal, before the new goal replaces the old one.
    public static void GoalStarting(World server, Entity player, AIGoal prev, AIGoal g)
    {
        try
        {
            var s = S; s.server = server; s.player = player;
            var r = Rec(server, player);
            if (r == null || g == null) return;
            var run = r.GoalStart(g, prev != null && prev.kind != GoalKind.None ? prev : null, KeyOf(g), g.kind.ToString().ToLowerInvariant(),
                g.item == ObjectID.None ? "" : g.item.ToString(), g.count, g.source, Cond(server, player), g.startCount);
            if (s.runOf != run.run) { s.runOf = run.run; s.runAreas.Clear(); }
            AIPlayerSpikeMod.Log($"EXP: goal run {run.run} {(run.resets > 0 ? "goes on (same goal set again)" : "started")}: {run.key} x{run.count} ({g.source})");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: goal start failed", e); }
    }

    // AIBrain.ClearGoal, with the goal that ends and why (end.Why; end.Outcome / end.Cls are compared with the classified sentence,
    // refactor S2).
    public static void GoalEnding(AIGoal g, GoalEnd end)
    {
        string why = end.Why;
        try
        {
            var s = S;
            var r = Rec(s.server, s.player);
            if (r == null || r.Open == null || g == null) return;
            int got = -1;
            var c = Cond(s.server, s.player);
            if ((g.kind == GoalKind.Gather || g.kind == GoalKind.Craft) && Alive(s.server, s.player))
            {
                AIBody.InventoryCounts(s.server, s.player).TryGetValue(g.item, out int have);
                int start = r.Open.start >= 0 ? Math.Min(r.Open.start, g.startCount) : g.startCount;   // the run's start (the goal may have been set again)
                got = Math.Max(0, have - start);
                if (g.item.ToString().EndsWith("Ore")) c.ore = AIMemory.CountOres(g.item);   // ores are measured directly (remembered ore tiles); other items are not
            }
            if (g.kind == GoalKind.Craft && Alive(s.server, s.player)) { var cs = CraftState(s.server, s.player, g.item); r.Open.craftMissing = cs.miss; r.Open.craftStation = cs.st; }
            string run = r.Open.run;
            if (r.GoalEnd(g, why, end.Outcome, end.Cls, got, c, WorkArea(s), AITestSwitches.Test))
            {
                s.lastEndedKey = KeyOf(g);
                var row = r.Book.Rows[r.Book.Rows.Count - 1];
                AIPlayerSpikeMod.Log($"EXP: goal run {run} ended: {row.key} -> {row.outcome}{(row.cls != "" ? "/" + row.cls : "")} got {row.got}/{row.count}, {row.secs:F0}s, downs {row.downs}");
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: goal end failed", e); }
    }

    // For a craft goal: material units still missing for one (AICraft.Materials), and a station that makes it: 2 at hand
    // (hand recipe or in view), 1 remembered, 0 none known. (-1, -1) if it cannot be read.
    private static (int miss, int st) CraftState(World server, Entity player, ObjectID item)
    {
        try
        {
            int miss = 0;
            foreach (var m in AICraft.Materials(server, player, item, 1)) miss += Math.Max(0, m.need - m.have);
            int st = AICraft.HandRecipes(server, player).Contains(item) || AICraft.StationsInView(server, player).Exists(x => x.recipes.Contains(item)) ? 2
                : AICraft.RememberedStationFor(server, player, item).HasValue ? 1 : 0;
            return (miss, st);
        }
        catch { return (-1, -1); }
    }

    // ------------------------------------------------------------------ AIPlanner hooks

    // A base set (AIBase /ai base): a "함께 한 일" material event. Never throws.
    public static void BaseSet(World server, Entity player, int2 tile, string by)
    {
        try
        {
            var r = Rec(server, player);
            if (r == null || !Alive(server, player)) return;
            var c = Cond(server, player);
            r.Event("base_set", r.Open?.run, new List<string> { $"cell:{(int)math.floor(tile.x / 16f)},{(int)math.floor(tile.y / 16f)}", "area:" + c.area }, by == "호스트" ? "host" : "game",
                $"{{\"tile\":[{tile.x},{tile.y}],\"by\":{ExpJson.S(by)},\"with\":{ExpJson.Arr(AITeammates.Last.Select(p => p.name))},\"c\":{c.ToJson()}}}");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("EXP: base_set failed", e); }
    }

    public static void OnReset()
    {
        try
        {
            var s = S;
            if (s.rec != null)
            {
                if (s.rec.Open != null) s.rec.End("unknown", "", "접속 끊김·초기화로 목표의 끝을 보지 못함", -1, Cond(s.server, s.player), WorkArea(s));
                s.rec.SaveState();
            }
            s.failedWorld = null;   // the next join tries to load again
            s.inFight = false; s.bosses.Clear(); s.lastDowns = -1; s.hpKnown = false; s.lastTick = -1f;
            s.server = null; s.player = Entity.Null;
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: reset failed", e); }
    }

    // What happened to an upper-LLM answer: accepted (applied), dropped (the mod's checks), failed (bridge/CLI error).
    // The answer's why/say are the LLM's words (src llm); they are not observations.
    public static void PlanResult(string result, int report, string trigger, string decision, string goal, string why, string say, string reason, float secs, string recall = null)
    {
        try
        {
            var r = Rec(S.server, S.player);
            if (r == null) return;
            string d = $"{{\"result\":{ExpJson.S(result)},\"report\":{report},\"trigger\":{ExpJson.S(trigger)},\"decision\":{ExpJson.S(decision)},\"goal\":{ExpJson.S(goal)},\"why\":{ExpJson.S(ExpRun.Trim(why, 160))},\"say\":{ExpJson.S(ExpRun.Trim(say, 100))},\"reason\":{ExpJson.S(ExpRun.Trim(reason, 160))},\"secs\":{ExpJson.N(Math.Round(secs, 1))}{(string.IsNullOrEmpty(recall) || recall == "null" ? "" : $",\"recall\":{ExpJson.S(ExpRun.Trim(recall, 200))}")}}}";
            r.Event("plan_result", result == "accepted" ? r.Open?.run : null, null, result == "accepted" ? "llm" : "game", d);
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: plan result failed", e); }
    }

    // The fight log of this character in this world (AIFightLog); the per-kind tally is read once from the saved fight
    // events (events*.jsonl), then kept up to date by each new fight.
    private static AIFightLog FightsOf(ExpRecorder r, State s)
    {
        if (s.fightsWorld != r.World) { s.Fights = new AIFightLog(); s.fightsWorld = r.World; }
        if (s.Fights.TallyLoaded) return s.Fights;
        s.Fights.TallyLoaded = true;
        int n = 0;
        try
        {
            if (Directory.Exists(r.Dir))
                foreach (var file in Directory.GetFiles(r.Dir, "events*.jsonl"))
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var rd = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = rd.ReadLine()) != null)
                        {
                            if (line.IndexOf("\"kind\":\"fight\"", StringComparison.Ordinal) < 0) continue;
                            var ev = ExpJson.Parse(line) as Dictionary<string, object>;
                            var d = ExpJson.GetO(ev, "d");
                            var enemies = ExpJson.GetO(d, "enemies");
                            if (enemies == null) continue;
                            bool down = d.TryGetValue("downs", out var dv) && dv is double dd && dd > 0;
                            s.Fights.Count(enemies.Keys, down); n++;
                        }
                    }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: fight tally read failed", e); }
        AIPlayerSpikeMod.Log($"EXP: fight tally read from {n} saved fights");
        return s.Fights;
    }

    // The report's "recent_fights" part (fights of the last 10 minutes, one line each, facts only).
    public static string RecentFightsJson(Func<string, string> esc)
    {
        try
        {
            var r = Rec(S.server, S.player);
            if (r == null) return "[]";
            return FightsOf(r, S).ReportJson(Time.realtimeSinceStartup, esc);
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: recent fights failed", e); return "[]"; }
    }

    // The upper LLM's problem report (answer field "problem", 자연어 명령 decision 6): one event, the only path.
    public static void Problem(string text, string say, int report, string trigger)
    {
        try
        {
            var r = Rec(S.server, S.player);
            if (r == null) return;
            r.Event("problem", r.Open?.run, null, "llm", $"{{\"text\":{ExpJson.S(ExpRun.Trim(text, 200))},\"say\":{ExpJson.S(ExpRun.Trim(say, 100))},\"report\":{report},\"trigger\":{ExpJson.S(trigger)}}}");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: problem report failed", e); }
    }

    // The upper LLM changed what happens to the host's instruction (따름 / 미룸 / 포기 / 끝남): an "order" event (합의 2
    // "바꾼 사건은 기록", 검수 작은 지적 4).
    public static void Order(string instruction, string was, string now, string why, int report, string trigger)
    {
        try
        {
            var r = Rec(S.server, S.player);
            if (r == null) return;
            r.Event("order", r.Open?.run, null, "llm", $"{{\"text\":{ExpJson.S(ExpRun.Trim(instruction, 160))},\"was\":{ExpJson.S(was)},\"now\":{ExpJson.S(now)},\"why\":{ExpJson.S(ExpRun.Trim(why, 160))},\"report\":{report},\"trigger\":{ExpJson.S(trigger)}}}");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: order event failed", e); }
    }

    // The report's "experience" part.
    public static string ReportPart(World server, Entity player, IList<string> candidates)
    {
        try
        {
            var r = Rec(server, player);
            if (r == null) return "null";
            var sw = Stopwatch.StartNew();
            string lastKey = AIBrain.Goal.IsReal ? KeyOf(AIBrain.Goal) : S.lastEndedKey;
            var craftMemo = new Dictionary<string, (int, int)>();
            string part = r.Report(Cond(server, player), lastKey, candidates,
                name => Enum.TryParse(name, out ObjectID id) && id != ObjectID.None ? AIMemory.CountOres(id) : 0,
                name =>
                {
                    if (craftMemo.TryGetValue(name, out var v)) return v;
                    v = Enum.TryParse(name, out ObjectID id) && id != ObjectID.None ? CraftState(server, player, id) : (-1, -1);
                    craftMemo[name] = v; return v;
                });
            AIPlayerSpikeMod.Log($"EXP: report part {part.Length} chars in {sw.Elapsed.TotalMilliseconds:F1} ms (goal runs {r.Book.Rows.Count})");
            return part;
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("exp: report part failed", e); return "null"; }
    }

    // ------------------------------------------------------------------ per frame

    public static void Tick(World server, Entity player, float now)
    {
        var s = S; s.server = server; s.player = player;
        var r = Rec(server, player);
        if (r == null) return;
        if (s.lastTick >= 0f) r.Play += math.clamp(now - s.lastTick, 0f, 1f);
        s.lastTick = now;
        var em = server.EntityManager;
        if (!em.Exists(player) || !em.HasComponent<HealthCD>(player)) return;
        int hp = em.GetComponentData<HealthCD>(player).health;
        if (s.inFight && s.fightHpPrev >= 0 && hp < s.fightHpPrev) s.fightDamage += s.fightHpPrev - hp;
        s.fightHpPrev = hp;
        // downs: AIVitals.Tick (earlier in the same frame) counts them and writes deaths.log
        int downs = AIVitals.Downs;
        if (s.lastDowns < 0 || downs < s.lastDowns) s.lastDowns = downs;
        else if (downs > s.lastDowns) { s.lastDowns = downs; OnDown(server, player, s, r); }
        if (s.hpKnown && s.wasDown && hp > 0) r.Event("back_up", r.Open?.run, null, "game", $"{{\"hp\":{hp}}}");
        s.hpKnown = true; s.wasDown = hp <= 0;
        if (now >= s.nextScan) { s.nextScan = now + 1f; ScanFight(server, player, now, s, r); }
        if (now >= s.nextSlow) { s.nextSlow = now + 2f; ScanArea(server, player, now, s, r); ScanItems(server, player, s, r); }
        if (now >= s.nextSave) { s.nextSave = now + 30f; r.SaveState(); }
    }

    private static void OnDown(World server, Entity player, State s, ExpRecorder r)
    {
        var o = AIObservation.Take(server, player, false);
        var enemies = o.enemies.Where(e => e.dist <= 6f).Select(e => e.id.ToString()).Distinct().Take(4).ToList();
        string cause = "";
        var last = AIVitals.RecentDowns(1);
        if (last.Count > 0) cause = last[0];
        if (enemies.Count == 0)
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(cause, @"([A-Za-z]+) [\d.]+ hp\d+"))
                if (!enemies.Contains(m.Groups[1].Value)) enemies.Add(m.Groups[1].Value);
        r.Down(enemies, cause, Cond(server, player));
        AIPlayerSpikeMod.Log($"EXP: down recorded (run {r.Open?.run ?? "none"}, enemies [{string.Join(",", enemies)}])");
    }

    private static void ScanFight(World server, Entity player, float now, State s, ExpRecorder r)
    {
        var em = server.EntityManager;
        var o = AIObservation.Take(server, player, false);
        // bosses on screen (BossCD), including one at 0 health
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BossCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(o.pos, p.xz)) continue;
                var h = em.GetComponentData<HealthCD>(e);
                string id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID.ToString() : "Boss";
                if (h.health > 0)
                {
                    if (!s.bosses.ContainsKey(e) && r.Bosses.Add(id))
                        r.Event("boss_seen", r.Open?.run, new List<string> { "enemy:" + id }, "game", $"{{\"boss\":{ExpJson.S(id)},\"first\":true,\"hp\":{h.health},\"max\":{h.maxHealth},\"c\":{Cond(server, player).ToJson()}}}");
                    s.bosses[e] = (id, h.health, h.maxHealth, now);
                    if (s.inFight) s.fightBoss = id;
                }
            }
        }
        // a boss that was low and is now gone or at 0 health: killed (with whoever was on screen; who hit it is not known)
        foreach (var kv in s.bosses.ToList())
        {
            var b = kv.Value;
            bool gone = !em.Exists(kv.Key) || !em.HasComponent<HealthCD>(kv.Key) || em.GetComponentData<HealthCD>(kv.Key).health <= 0;
            if (gone)
            {
                if (now - b.seen <= 10f && b.hp <= b.max / 2)
                {
                    var with = AITeammates.Last.Select(p => p.name + (p.isAi ? "(AI)" : "(호스트)")).ToList();
                    var ents2 = new List<string> { "enemy:" + b.id }; ents2.AddRange(AITeammates.Last.Select(p => "player:" + p.name));
                    r.Event("boss_kill", r.Open?.run, ents2, "game", $"{{\"boss\":{ExpJson.S(b.id)},\"with\":{ExpJson.Arr(with)},\"last_hp\":{b.hp},\"max\":{b.max},\"note\":\"화면에서 쓰러짐을 봄. 누가 몇 번 쳤는지는 모름\",\"c\":{Cond(server, player).ToJson()}}}");
                    AIPlayerSpikeMod.Log($"EXP: boss kill {b.id} seen (with {string.Join(",", with)})");
                }
                s.bosses.Remove(kv.Key);
            }
            else if (now - b.seen > 60f) s.bosses.Remove(kv.Key);
        }
        // the fight: an enemy seen near in this fight that is gone or at 0 health fell (who hit it is not known)
        if (s.inFight)
            foreach (var kv in s.fightSeen.ToList())
            {
                bool dead = !em.Exists(kv.Key) || (em.HasComponent<HealthCD>(kv.Key) && em.GetComponentData<HealthCD>(kv.Key).health <= 0);
                if (!dead) continue;
                s.fightFell.TryGetValue(kv.Value, out int nf); s.fightFell[kv.Value] = nf + 1;
                s.fightSeen.Remove(kv.Key);
            }
        var near = o.enemies.Where(e => e.dist <= 7f).ToList();
        if (near.Count > 0)
        {
            if (!s.inFight)
            {
                s.inFight = true; s.fightStart = now; s.fightDowns0 = AIVitals.Downs; s.fightBoss = "";
                s.fightEnemies.Clear(); s.fightWith.Clear();
                s.fightDamage = 0; s.fightSeen.Clear(); s.fightFell.Clear();
            }
            s.fightLast = now;
            foreach (var e in near) if (e.entity != Entity.Null) s.fightSeen[e.entity] = e.id.ToString();
            foreach (var g in near.GroupBy(e => e.id.ToString()))
            {
                s.fightEnemies.TryGetValue(g.Key, out int n);
                s.fightEnemies[g.Key] = Math.Max(n, g.Count());
            }
            foreach (var p in AITeammates.Last)
                if (p.dist <= 10f && !(p.maxHealth > 0 && p.health <= 0)) s.fightWith.Add(p.name + (p.isAi ? "(AI)" : "(호스트)"));
        }
        else if (s.inFight && now - s.fightLast >= 5f)
        {
            s.inFight = false;
            float secs = s.fightLast - s.fightStart;
            int downs = Math.Max(0, AIVitals.Downs - s.fightDowns0);
            if (secs >= 2f || downs > 0 || s.fightBoss != "")
            {
                var ents = s.fightEnemies.Keys.Select(k => "enemy:" + k).ToList();
                ents.AddRange(s.fightWith.Select(w => "player:" + w.Split('(')[0]));
                var fc = Cond(server, player); if (fc.cell != "") ents.Add("cell:" + fc.cell); if (fc.area != "") ents.Add("area:" + fc.area);
                string en = string.Join(",", s.fightEnemies.Select(kv => $"{ExpJson.S(kv.Key)}:{kv.Value}"));
                var log = FightsOf(r, s);   // read the saved tally before this fight is written (검수 작은 지적 1: counted twice)
                var hc = em.GetComponentData<HealthCD>(player);
                int hcMax = MaxHp(em, player, hc);   // with the conditions (armour, food): 156/100 otherwise (실게임2 N3)
                string fell = string.Join(",", s.fightFell.Select(kv => $"{ExpJson.S(kv.Key)}:{kv.Value}"));
                r.Event("fight", r.Open?.run, ents, "game",
                    $"{{\"secs\":{ExpJson.N(Math.Round(secs, 1))},\"enemies\":{{{en}}},\"with\":{ExpJson.Arr(s.fightWith)},\"together\":{(s.fightWith.Count > 0 ? "true" : "false")},\"downs\":{downs},\"boss\":{ExpJson.S(s.fightBoss)},\"end_hp\":{hc.health},\"max_hp\":{hcMax},\"damage\":{s.fightDamage},\"fell\":{{{fell}}},\"c\":{fc.ToJson()}}}");
                var rec = new FightRecord { At = now, Secs = secs, Downs = downs, Damage = s.fightDamage, EndHp = hc.health, MaxHp = hcMax,
                    Enemies = new Dictionary<string, int>(s.fightEnemies), Fell = new Dictionary<string, int>(s.fightFell) };
                log.Add(rec);
                AIPlayerSpikeMod.Log($"EXP: fight ended: {log.Line(rec, now)}");
                try { AIPlanner.OnFightEnded(server, player, rec); } catch (Exception e) { AIPlayerSpikeMod.LogError("planner: fight end failed", e); }
            }
        }
    }

    private static void ScanArea(World server, Entity player, float now, State s, ExpRecorder r)
    {
        var em = server.EntityManager;
        string a = BiomeAt(server, AIBody.TileOf(em.GetComponentData<LocalTransform>(player).Position));
        if (r.Open != null && a != "") { s.runAreas.TryGetValue(a, out int n); s.runAreas[a] = n + 1; }   // the goal's work area
        if (a == "" || a == s.area) { s.areaCand = ""; return; }
        if (s.area == "" && r.Areas.Contains(a)) { s.area = a; return; }   // just joined in a known area
        if (a != s.areaCand) { s.areaCand = a; s.areaCandSince = now; return; }
        if (now - s.areaCandSince < 8f) return;
        string from = s.area;
        s.area = a; s.areaCand = "";
        bool first = r.Areas.Add(a);
        var c = Cond(server, player);
        r.Event("area_enter", r.Open?.run, new List<string> { "area:" + a, "cell:" + c.cell }, "game",
            $"{{\"area\":{ExpJson.S(a)},\"from\":{ExpJson.S(from)},\"first\":{(first ? "true" : "false")},\"with\":{ExpJson.Arr(AITeammates.Last.Select(p => p.name))},\"c\":{c.ToJson()}}}");
        if (first) { r.SaveState(); AIPlayerSpikeMod.Log($"EXP: first time in area {a}"); }
    }

    // The biome seen most while the open run went on ("" if none).
    private static string WorkArea(State s)
    {
        if (s.rec?.Open == null || s.runOf != s.rec.Open.run || s.runAreas.Count == 0) return "";
        return s.runAreas.OrderByDescending(kv => kv.Value).First().Key;
    }

    private static void ScanItems(World server, Entity player, State s, ExpRecorder r)
    {
        bool added = false;
        foreach (var kv in AIBody.InventoryCounts(server, player))
        {
            string id = kv.Key.ToString();
            if (!r.Items.Add(id)) continue;
            added = true;
            if (!s.firstItemScan) r.Event("item_first", r.Open?.run, new List<string> { "item:" + id }, "game", $"{{\"item\":{ExpJson.S(id)},\"count\":{kv.Value}}}");
        }
        s.firstItemScan = false;
        if (added) r.SaveState();
    }
}
