// AICombat — combat support for the decision layer. Design: 행동설계 v2 §4·§5·§8; work
// work/2026-11/2026-11-04-전투-선택지.md (session 코어키퍼-AI-캐릭터).
//
// - AIVitals: always on while the AI is present (goal or not). Samples health/hunger/position/nearby enemies every
//   0.5 s, logs every down (health 0) and stand-up with position, health history, nearby enemies, the last choices
//   and the observable cause; appends to AIPlayers/<world>/<member>/deaths.log (per character). Learns from observation: damage per hit of
//   the held weapon (from the attack skill) and damage taken per enemy kind (health drops while one kind is the
//   only enemy within 2.5 tiles). These are facts for the candidates, marked as observed estimates.
// - AIItems: which items heal, from the game data (GivesConditionsWhenConsumedBuffer: HealthAddition,
//   HealthAdditionPercentage, HealOverTime).
// - EatSkill / KeepAwaySkill / RetreatSkill: actions the decision model can choose. They never start on their own.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AIVitals
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public readonly List<Sample> _hist = new List<Sample>();
        public float _lastSample = -999f;
        public bool _down;
        public int _prevHp = -1, _maxNow = -1;   // 합치기 전 수정 6: the max with conditions now (AIBaseGuard.HurtAmount)
        public float _downAt;
        public int Downs;
        public readonly List<string> _choices = new List<string>();
        public readonly Dictionary<ObjectID, (int sum, int n, int gapMs, int gapN, float lastT)> _hit = new Dictionary<ObjectID, (int, int, int, int, float)>();
        public readonly Dictionary<ObjectID, (int sum, int n)> _taken = new Dictionary<ObjectID, (int, int)>();
        public readonly Dictionary<ObjectID, (int ok, int n)> _away = new Dictionary<ObjectID, (int, int)>();
        public readonly Dictionary<ObjectID, float> _ate = new Dictionary<ObjectID, float>();
        public readonly Dictionary<ObjectID, (int gapMs, int gapN, float lastT)> _takenGap = new Dictionary<ObjectID, (int, int, float)>();
        public long _downsLen = -1; public readonly Dictionary<ObjectID, int> _downsNear = new Dictionary<ObjectID, int>();
        public readonly Dictionary<ObjectID, int> _lethal = new Dictionary<ObjectID, int>();   // largest health taken to 0 in one hit, per kind
        public string _hurtWhy = ""; public float _hurtWhyAt = -999f, _hurtLogAt = -999f; public bool _hurtLooked;   // 실측결함수정3 N8
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Vitals);
    private static State S => StateOf(AIAgent.Cur);

    private struct Sample { public float t; public int hp, maxHp, hunger; public float3 pos; public string enemies; }
    private static List<Sample> _hist => S._hist;
    private static float _lastSample { get => S._lastSample; set => S._lastSample = value; }
    private static bool _down { get => S._down; set => S._down = value; }
    private static int _prevHp { get => S._prevHp; set => S._prevHp = value; }
    private static float _downAt { get => S._downAt; set => S._downAt = value; }
    public static int Downs { get => S.Downs; set => S.Downs = value; }                      // this join
    public static int LastHp => _prevHp;
    // 합치기 전 수정 6: health lost to a hit from hp0 (an earlier reading) to now — AIBaseGuard.HurtAmount with the max now
    public static int LostSince(int hp0, int hpNow) => AIBaseGuard.HurtAmount(hp0, hpNow, S._maxNow > 0 ? S._maxNow : int.MaxValue);
    private static List<string> _choices => S._choices;   // last decisions (for the down log)

    // learned (observed) numbers
    // per weapon: damage sum/count, and gaps between consecutive hits shorter than 2 s (sum in ms, count)
    private static Dictionary<ObjectID, (int sum, int n, int gapMs, int gapN, float lastT)> _hit => S._hit;
    private static Dictionary<ObjectID, (int sum, int n)> _taken => S._taken;

    // Weapon base damage and cooldown. The damage is on the weapon's level entity (AIGameData, the item tooltip's rule;
    // the base prefab has no WeaponDamageCD), the cooldown on the prefab (CooldownCD.cooldown). variation = the held
    // item's level when known, 0 = the item's default level.
    private static readonly Dictionary<(ObjectID, int), (int dmg, float cd)> _weaponData = new Dictionary<(ObjectID, int), (int, float)>();
    public static bool WeaponData(World server, ObjectID weapon, out int dmg, out float cooldown, int variation = 0)
    {
        dmg = 0; cooldown = 0f;
        if (weapon == ObjectID.None) return false;
        if (_weaponData.TryGetValue((weapon, variation), out var w)) { dmg = w.dmg; cooldown = w.cd; return dmg > 0; }
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(weapon, bank, 0);
                    AIGameData.WeaponDamage(em, prefab, variation, out dmg, out _);
                    if (prefab != Entity.Null && em.HasComponent<CooldownCD>(prefab)) cooldown = em.GetComponentData<CooldownCD>(prefab).cooldown;
                }
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"weapon data for {weapon} failed", e); }
        _weaponData[(weapon, variation)] = (dmg, cooldown);
        return dmg > 0;
    }

    // keep-distance outcomes per enemy kind (did it reach the distance?)
    private static Dictionary<ObjectID, (int ok, int n)> _away => S._away;
    public static void NoteAway(ObjectID enemy, bool reached) { _away.TryGetValue(enemy, out var a); a.n++; if (reached) a.ok++; _away[enemy] = a; AIMemory.MarkDirty(); }
    public static string AwayNote(ObjectID enemy) => _away.TryGetValue(enemy, out var a) && a.n > 0 ? $"이 종류에게 거리 벌리기 결과(관찰): {a.n}번 중 {a.ok}번 5칸 벌림." : "이 종류에게 거리 벌리기가 통하는지 모름.";

    // Last time a healing item was eaten (kind -> time), for "an effect is still running" facts.
    private static Dictionary<ObjectID, float> _ate => S._ate;
    public static void NoteAte(ObjectID id, float now) { _ate[id] = now; }
    public static bool AteRecently(ObjectID id, float now, out float ago) { ago = 0f; if (!_ate.TryGetValue(id, out float t)) return false; ago = now - t; return true; }

    // Persist the learned numbers with the per-world memory (AIMemory): flat int records [kind, id, a, b, c].
    public static int[] ExportLearned()
    {
        var l = new List<int>();
        foreach (var kv in _hit)
        {
            int ms = kv.Value.gapN > 0 ? kv.Value.gapMs / kv.Value.gapN : 0;
            l.Add(1); l.Add((int)kv.Key); l.Add(kv.Value.sum); l.Add(kv.Value.n); l.Add(ms);
        }
        foreach (var kv in _taken) { l.Add(2); l.Add((int)kv.Key); l.Add(kv.Value.sum); l.Add(kv.Value.n); l.Add(S._takenGap.TryGetValue(kv.Key, out var g) && g.gapN > 0 ? g.gapMs / g.gapN : 0); }
        foreach (var kv in _away) { l.Add(3); l.Add((int)kv.Key); l.Add(kv.Value.ok); l.Add(kv.Value.n); l.Add(0); }
        foreach (var kv in S._lethal) { l.Add(4); l.Add((int)kv.Key); l.Add(kv.Value); l.Add(0); l.Add(0); }
        return l.ToArray();
    }

    public static void ImportLearned(int[] a)
    {
        if (a == null) return;
        for (int i = 0; i + 4 < a.Length; i += 5)
        {
            var id = (ObjectID)a[i + 1];
            if (a[i] == 1 && !_hit.ContainsKey(id) && a[i + 3] > 1) _hit[id] = (a[i + 2], a[i + 3], a[i + 4], a[i + 4] > 0 ? 1 : 0, -999f);
            else if (a[i] == 2 && !_taken.ContainsKey(id)) { _taken[id] = (a[i + 2], a[i + 3]); if (a[i + 4] > 0) S._takenGap[id] = (a[i + 4], 1, -999f); }
            else if (a[i] == 3 && !_away.ContainsKey(id)) _away[id] = (a[i + 2], a[i + 3]);
            else if (a[i] == 4 && !S._lethal.ContainsKey(id)) S._lethal[id] = a[i + 2];
        }
    }

    public static void Reset()
    {
        _hist.Clear(); _down = false; _prevHp = -1; Downs = 0; _choices.Clear();
    }

    // The last downs of this world (deaths.log tail): "where, cause" lines for the upper LLM's report.
    public static List<string> RecentDowns(int n)
    {
        var res = new List<string>();
        try
        {
            string w = AIPlayerSpikeMod.WorldId;
            string p = Path.Combine(AIMemory.CharDir(w), "deaths.log");   // this character's own downs
            if (!File.Exists(p)) return res;
            var lines = File.ReadAllLines(p, Encoding.UTF8);
            for (int i = Math.Max(0, lines.Length - n); i < lines.Length; i++)
            {
                var m = System.Text.RegularExpressions.Regex.Match(lines[i], @"^(\S+ \S+) DOWN #\d+ at \(([-\d.]+),([-\d.]+)\).*?cause: ([^;]{0,90})");
                if (m.Success) res.Add($"{m.Groups[1].Value} ({m.Groups[2].Value},{m.Groups[3].Value}) {m.Groups[4].Value}");
            }
        }
        catch { }
        return res;
    }

    public static void NoteChoice(string s)
    {
        _choices.Add(s);
        while (_choices.Count > 5) _choices.RemoveAt(0);
    }

    // Hit stats are kept apart for a broken weapon (durability 0: the game cuts its damage). Claude's WoodSword was at 0
    // and hit for ~11 while Gemini's working one hit for ~33 (ai2 stab3-7); one average mixed the two and gave a kill
    // time three times too long or too short. Broken = weapon id + offset (ObjectIDs are far below it; persisted as is).
    public const int BrokenKeyOffset = 1 << 24;
    public static ObjectID HitKey(ObjectID weapon, bool broken) => broken && weapon != ObjectID.None ? (ObjectID)((int)weapon + BrokenKeyOffset) : weapon;

    // Called from the attack skill whenever the target's health changes (weapon = HitKey).
    public static void RecordHit(ObjectID weapon, int damage, float now)
    {
        if (damage <= 0 || damage > 1000) return;
        _hit.TryGetValue(weapon, out var h);
        // the average follows the recent ~20 hits (it was over every hit ever: 607 hits of a sword that had broken
        // kept a working sword at ~10/hit, ai2 Claude 2026-11-04)
        if (h.n >= 20) { h.sum = (int)math.round(h.sum * 19f / h.n); h.n = 19; }
        if (h.gapN >= 20) { h.gapMs = (int)math.round(h.gapMs * 19f / h.gapN); h.gapN = 19; }
        if (h.n > 0 && now - h.lastT < 2f && now > h.lastT) { h.gapMs += (int)((now - h.lastT) * 1000f); h.gapN++; }
        h.sum += damage; h.n++; h.lastT = now;
        _hit[weapon] = h;
        AIMemory.MarkDirty();
    }

    // Observed average damage per hit with this weapon and seconds per hit (null when not observed yet).
    public static bool HitStats(ObjectID weapon, out float perHit, out float secPerHit)
    {
        perHit = 0f; secPerHit = 0f;
        if (!_hit.TryGetValue(weapon, out var h) || h.n < 2 || h.gapN < 1) return false;
        perHit = h.sum / (float)h.n;
        secPerHit = math.max(0.2f, h.gapMs / 1000f / h.gapN);
        return true;
    }

    // Largest health this kind took to 0 in one hit (0 = never seen).
    public static int LethalHit(ObjectID enemy) => S._lethal.TryGetValue(enemy, out int v) ? v : 0;

    // Observed seconds between consecutive hits from this enemy kind (n >= 1 interval).
    public static bool TakenGap(ObjectID enemy, out float sec)
    {
        sec = 0f;
        if (!S._takenGap.TryGetValue(enemy, out var g) || g.gapN < 1) return false;
        sec = math.max(0.2f, g.gapMs / 1000f / g.gapN);
        return true;
    }

    // How many of this character's downs (its own deaths.log) had this kind as the first enemy last seen.
    public static int DownsNear(ObjectID enemy)
    {
        try
        {
            string w = AIPlayerSpikeMod.WorldId;
            if (string.IsNullOrEmpty(w)) return 0;
            string p = Path.Combine(AIMemory.CharDir(w), "deaths.log");
            long len = File.Exists(p) ? new FileInfo(p).Length : 0;
            if (len != S._downsLen)
            {
                S._downsLen = len; S._downsNear.Clear();
                if (len > 0)
                    foreach (var line in File.ReadAllLines(p, Encoding.UTF8))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"마지막으로 본 적: (\w+)");
                        if (m.Success && Enum.TryParse(m.Groups[1].Value, out ObjectID id)) { S._downsNear.TryGetValue(id, out int c); S._downsNear[id] = c + 1; }
                    }
            }
            return S._downsNear.TryGetValue(enemy, out int n) ? n : 0;
        }
        catch { return 0; }
    }

    public static bool TakenStats(ObjectID enemy, out float perHit, out int n)
    {
        perHit = 0f; n = 0;
        if (!_taken.TryGetValue(enemy, out var t) || t.n == 0) return false;
        perHit = t.sum / (float)t.n; n = t.n;
        return true;
    }

    // Health change over the last `seconds` (negative = lost).
    public static int HpChange(float seconds, float now)
    {
        if (_hist.Count == 0) return 0;
        var last = _hist[_hist.Count - 1];
        for (int i = _hist.Count - 1; i >= 0; i--)
            if (now - _hist[i].t >= seconds) return last.hp - _hist[i].hp;
        return last.hp - _hist[0].hp;
    }

    // 코어-안전지대 단계 3 recheck R1: health lost within `seconds` that a lower max health does not explain (hunger
    // lowers the max; AIBaseGuard.HurtBy)
    public static bool HurtRecently(float seconds, float now)
    {
        if (_hist.Count == 0) return false;
        var last = _hist[_hist.Count - 1]; var then = _hist[0];
        for (int i = _hist.Count - 1; i >= 0; i--) if (now - _hist[i].t >= seconds) { then = _hist[i]; break; }
        return AIBaseGuard.HurtBy(then.hp, then.maxHp, last.hp, last.maxHp);
    }

    public static void Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(player) || !em.HasComponent<HealthCD>(player)) return;
        var h = em.GetComponentData<HealthCD>(player);
        int hunger = em.HasComponent<HungerCD>(player) ? em.GetComponentData<HungerCD>(player).hunger : -1;
        float3 pos = em.GetComponentData<LocalTransform>(player).Position;
        // live-run fix 6 (사실 6): the max health with conditions (hunger below 25 lowers it by StarvingHealthDecrease ->
        // MaxHealthPercentage, HungerAndRunningSystem; the health above it goes with it, measured 100 -> 75 at hunger 0) — so a
        // starving drop is a lower max, not a hit (AIBaseGuard.HurtBy). 합치기 전 수정 6: read every tick, for every drop check
        int maxC = em.HasBuffer<SummarizedConditionEffectsBuffer>(player) ? h.GetMaxHealthWithConditions(em.GetBuffer<SummarizedConditionEffectsBuffer>(player, true)) : h.maxHealth;
        S._maxNow = maxC;
        int lost = _prevHp > 0 ? AIBaseGuard.HurtAmount(_prevHp, h.health, maxC) : 0;

        // damage attribution: a drop while exactly one enemy kind is within 2.5 tiles
        if (lost > 0)
        {
            var o = AIObservation.Take(server, player, false);
            // 실측결함수정3 N8: a drop with no enemy in view: what was on the AI and around it then (a damage-over-time
            // condition, a projectile from out of view) — the down record names it (run2 t=154.4 had only "원인 모름")
            if (o.enemies.Count == 0)
            {
                string why = HurtWhy(server, player, pos);
                S._hurtLooked = true;
                if (why != "") { S._hurtWhy = why; S._hurtWhyAt = now; }
                if (now - S._hurtLogAt >= 1f) { S._hurtLogAt = now; AIPlayerSpikeMod.Log($"VITALS: hurt with no enemy in view: health {_prevHp} -> {h.health}; {(why != "" ? why : "no damage-over-time condition, no projectile within 4 tiles")}"); }
            }
        }
        if (lost > 0 && h.health > 0)
        {
            var o = AIObservation.Take(server, player, false);
            var near = o.enemies.FindAll(e => e.dist <= 2.5f);
            if (near.Count > 0 && near.TrueForAll(e => e.id == near[0].id))
            {
                _taken.TryGetValue(near[0].id, out var t);
                t.sum += lost; t.n++;
                _taken[near[0].id] = t;
                // interval between consecutive hits from this kind (shorter than 4 s: the same fight)
                S._takenGap.TryGetValue(near[0].id, out var g);
                if (now - g.lastT < 4f && now > g.lastT) { g.gapMs += (int)((now - g.lastT) * 1000f); g.gapN++; }
                g.lastT = now; S._takenGap[near[0].id] = g;
                AIMemory.MarkDirty();
            }
        }
        // a hit that took the AI from health X straight to 0 (the drop above only counts hits it survived, so a
        // one-hit down by a MushroomBrute left "damage unknown", ai2 2026-11-04): remember "at least X in one hit"
        if (_prevHp > 0 && h.health <= 0)
        {
            var o = AIObservation.Take(server, player, false);
            var near = o.enemies.FindAll(e => e.dist <= 2.5f);
            if (near.Count > 0 && near.TrueForAll(e => e.id == near[0].id))
            {
                S._lethal.TryGetValue(near[0].id, out int was);
                if (_prevHp > was) S._lethal[near[0].id] = _prevHp;
                AIPlayerSpikeMod.Log($"VITALS: one hit from {near[0].id} took health {_prevHp} -> 0 (remembered: this kind can do at least {_prevHp} in one hit)");
                AIMemory.MarkDirty();
            }
        }

        if (!_down && h.health <= 0 && _prevHp > 0) Down(server, player, now, pos, hunger);
        else if (_down && h.health > 0)
        {
            _down = false;
            AIPlayerSpikeMod.Log($"VITALS: stood up again after {now - _downAt:F1}s at ({pos.x:F1},{pos.z:F1}) health {h.health}/{h.maxHealth} hunger {hunger}");
            AIPlanner.OnBackUp(server, player);
        }
        _prevHp = h.health;

        if (now - _lastSample < 0.5f) return;
        _lastSample = now;
        string en = "";
        try
        {
            var o = AIObservation.Take(server, player, false);
            var parts = new List<string>();
            for (int i = 0; i < o.enemies.Count && i < 3; i++) parts.Add($"{o.enemies[i].id} {o.enemies[i].dist:F1} hp{o.enemies[i].health}");
            en = string.Join(", ", parts);
        }
        catch { }
        _hist.Add(new Sample { t = now, hp = h.health, maxHp = maxC, hunger = hunger, pos = pos, enemies = en });
        while (_hist.Count > 0 && now - _hist[0].t > 12f) _hist.RemoveAt(0);
    }

    // 실측결함수정3 N8: the damage-over-time conditions on the AI (poison, burning, damage-over-time auras) and the
    // projectiles within 4 tiles, as one fact ("" when none)
    private static string HurtWhy(World server, Entity player, float3 pos)
    {
        var dots = new List<string>(); var shots = new List<string>();
        try
        {
            var em = server.EntityManager;
            if (em.HasBuffer<ConditionsBuffer>(player))
            {
                var cb = em.GetBuffer<ConditionsBuffer>(player, true);
                for (int i = 0; i < cb.Length; i++)
                {
                    var cd = cb[i].condition.conditionData; string n = cd.conditionID.ToString();
                    bool dot = n == "Poisoned" || n == "Burning" || n == "AuraBurning" || n.EndsWith("DamageOverTime") && !n.Contains("Immune") && !n.Contains("Apply");
                    if (dot && !dots.Exists(x => x.StartsWith(n + "("))) dots.Add($"{n}(값 {cd.value})");
                }
            }
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<ObjectDataCD>()))
            using (var ents = q.ToEntityArray(Allocator.Temp))
                foreach (var e in ents)
                {
                    float d = math.distance(em.GetComponentData<LocalTransform>(e).Position.xz, pos.xz);
                    if (d > 4f) continue;
                    string id = em.GetComponentData<ObjectDataCD>(e).objectID.ToString();
                    shots.Add($"{id} {d:F1}칸");
                    if (shots.Count >= 3) break;
                }
        }
        catch (Exception ex) { return "확인 오류 " + ex.GetType().Name; }
        return AIOutcomes.HurtWithoutEnemy(dots, shots);
    }

    private static void Down(World server, Entity player, float now, float3 pos, int hunger)
    {
        _down = true; _downAt = now; Downs++;
        var hp = new List<string>();
        int step = math.max(1, _hist.Count / 8);
        for (int i = 0; i < _hist.Count; i += step) hp.Add($"{now - _hist[i].t:F0}s ago {_hist[i].hp}");
        string lastEnemies = _hist.Count > 0 ? _hist[_hist.Count - 1].enemies : "";
        // 실측결함수정3 N8: with no enemy seen, what was found when the health dropped (within the last 12 s)
        string cause = AIOutcomes.DownCause(lastEnemies, hunger, now - S._hurtWhyAt <= 12f ? S._hurtWhy : "", S._hurtLooked);
        S._hurtWhy = ""; S._hurtLooked = false;
        string cur = AIBody.Current != null ? $"{AIBody.Current.Name} {AIBody.Current.Key}" : "없음";
        string line = $"DOWN #{Downs} at ({pos.x:F1},{pos.z:F1}) hunger {hunger}; health [{string.Join(", ", hp)}]; cause: {cause}; running skill: {cur}; goal: {AIBrain.Goal.Text}; last choices: [{string.Join(" | ", _choices)}]";
        AIPlayerSpikeMod.Log("VITALS: " + line);
        AIPlayerSpikeMod.Reply($"AI 쓰러짐 #{Downs}: ({pos.x:F0},{pos.z:F0}), {cause}", ChatKind.Status);
        AIPlanner.AddEvent($"쓰러짐({cause})");
        try
        {
            string w = AIPlayerSpikeMod.WorldId;
            if (!string.IsNullOrEmpty(w))
            {
                string p = Path.Combine(AIMemory.CharDir(w), "deaths.log");
                File.AppendAllText(p, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}\n", new UTF8Encoding(false));
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("vitals: deaths.log write failed", e); }
    }
}

public static class AIItems
{
    private static readonly Dictionary<long, string> _cache = new Dictionary<long, string>();

    // "" when the item does not heal; otherwise a short description such as "즉시 체력 +30" or "12초 동안 체력 회복 5/…".
    public static string HealInfo(World server, ObjectID id, int variation)
    {
        long key = ((long)id << 16) | (uint)variation;
        if (_cache.TryGetValue(key, out string c)) return c;
        string res = "";
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(id, bank, variation);
                    if (prefab != Entity.Null && em.HasBuffer<GivesConditionsWhenConsumedBuffer>(prefab))
                    {
                        var b = em.GetBuffer<GivesConditionsWhenConsumedBuffer>(prefab, true);
                        var parts = new List<string>();
                        for (int i = 0; i < b.Length; i++)
                        {
                            var cd = b[i].conditionDataContainer.conditionData;
                            if (cd.conditionID == ConditionID.HealthAddition) parts.Add($"즉시 체력 +{cd.value}");
                            else if (cd.conditionID == ConditionID.HealthAdditionPercentage) parts.Add($"즉시 체력 +{cd.value}%");
                            else if (cd.conditionID == ConditionID.HealOverTime) parts.Add($"{cd.duration:F0}초 동안 체력 회복(값 {cd.value})");
                        }
                        res = string.Join(", ", parts);
                    }
                }
            }
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"heal info for {id} failed", e); }
        _cache[key] = res;
        return res;
    }

    // Healing items in the inventory: (id, variation, count, info), one entry per kind.
    public static List<(ObjectID id, int variation, int count, string info)> HealingInInventory(World server, Entity player)
    {
        var res = new List<(ObjectID, int, int, string)>();
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return res;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        var seen = new Dictionary<long, int>();
        for (int i = 0; i < buf.Length; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            if (info == null || info.objectType != ObjectType.Eatable) continue;
            string h = HealInfo(server, id, buf[i].variation);
            if (string.IsNullOrEmpty(h)) continue;
            long key = ((long)id << 16) | (uint)buf[i].variation;
            int n = info.isStackable ? buf[i].amount : 1;
            if (seen.TryGetValue(key, out int idx)) { var e = res[idx]; res[idx] = (e.Item1, e.Item2, e.Item3 + n, e.Item4); }
            else { seen[key] = res.Count; res.Add((id, buf[i].variation, n, h)); }
        }
        return res;
    }

    // Puts the first slot holding this item into the hotbar (swap like a player dragging it), returns the hotbar slot.
    public static int ToHotbar(World server, Entity player, ObjectID id, int variation, out string swap)
    {
        swap = "";
        var em = server.EntityManager;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
        int at = -1;
        for (int i = 0; i < buf.Length; i++) if (buf[i].objectID == id && buf[i].variation == variation) { at = i; break; }
        if (at < 0) return -1;
        if (at < 10) return at;
        int hot = -1;
        for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == ObjectID.None) { hot = i; break; }
        if (hot < 0)
        {
            for (int i = 9; i >= 0; i--)
            {
                ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
                if (info == null || (info.objectType != ObjectType.MiningPick && info.objectType != ObjectType.MeleeWeapon)) { hot = i; break; }
            }
        }
        if (hot < 0) return -1;
        var a = buf[hot]; var b = buf[at];
        buf[hot] = b; buf[at] = a;
        swap = $"moved {id} from bag slot {at} into hotbar slot {hot}";
        return hot;
    }
}

// Eats one healing item: hold it, hold "second interact" (EatableSlot eats on secondInteractHeld). Done when the
// item count drops by one; reports the health change 1 s later. Fails after 3 s without eating.
public class EatSkill : AISkill
{
    private readonly ObjectID _id;
    private readonly int _variation;
    private int _slot, _countBefore, _hpBefore;
    private float _ateAt;
    public EatSkill(ObjectID id, int variation) { Name = "먹기"; _id = id; _variation = variation; Key = $"eat:{id}"; }

    private static int Count(World server, Entity player, ObjectID id)
    {
        AIBody.InventoryCounts(server, player).TryGetValue(id, out int c);
        return c;
    }

    public override bool Start(World server, Entity player, float now)
    {
        _slot = AIItems.ToHotbar(server, player, _id, _variation, out string swap);
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = $"{_id}이(가) 가방에 없거나 손 칸에 놓을 수 없음"; return false; }
        if (!string.IsNullOrEmpty(swap)) Facts.Add(swap);
        _countBefore = Count(server, player, _id);
        var em = server.EntityManager;
        _hpBefore = em.GetComponentData<HealthCD>(player).health;
        Facts.Add($"item={_id} slot={_slot} count={_countBefore} hp={_hpBefore}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        int hp = em.GetComponentData<HealthCD>(player).health;
        if (_ateAt > 0f)
        {
            if (now - _ateAt < 1.0f) return new BodyInput { slot = _slot };
            Facts.Add($"hp {_hpBefore}->{hp} 1s after eating");
            return Finish(SkillStatus.Done, $"{_id} 먹음 · 체력 {_hpBefore}→{hp}");
        }
        if (Count(server, player, _id) < _countBefore) { _ateAt = now; AIVitals.NoteAte(_id, now); return new BodyInput { slot = _slot }; }
        if (now - StartTime > 3f) return Finish(SkillStatus.Failed, $"3초 동안 먹히지 않음({_id})");
        // first 0.4 s only select the slot, then hold second interact
        bool hold = now - StartTime > 0.4f;
        return new BodyInput { slot = _slot, secondary = hold };
    }

    // After eating (count dropped) the skill waits a fixed 1.0 s for the health change: that part is exact.
    public override float EtaSeconds(World server, Entity player) => _ateAt > 0f ? math.max(0f, 1.0f - (Time.realtimeSinceStartup - _ateAt)) : -1f;
}

// Short combat move: get to at least `minDist` tiles from every enemy in view, for at most `maxSeconds`, along open
// known tiles in view (objects block). Ends: far enough from the nearest one, time up, no enemy left, or arrived.
// One option for the whole fight (key "away:near"), not one per enemy: with several enemies close, "away from A" and
// "away from B" were two options for the same move, and a different nearest enemy on the next call made a new option
// (away:66032 -> away:65863 -> away:66032 within 3 s, ai2 stab5).
public class KeepAwaySkill : AISkill
{
    public const string NearKey = "away:near";
    private readonly float _minDist, _maxSeconds;
    private AINavigator _nav = new AINavigator();
    private int2 _to;
    public int2 To => _to;   // the tile it is going to now (for "same way as the running move" in the options)
    private ObjectID _id;
    public KeepAwaySkill(float minDist, float maxSeconds) { Name = "거리 벌리기"; _minDist = minDist; _maxSeconds = maxSeconds; Key = NearKey; }

    // Nearest living enemy in view (light query: enemies only).
    public static bool Nearest(World server, Entity player, out float dist, out ObjectID id, out float3 pos)
    {
        dist = float.MaxValue; id = ObjectID.None; pos = default;
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
            {
                if (em.GetComponentData<HealthCD>(e).health <= 0) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                float d = math.distance(p.xz, me.xz);
                if (d >= dist || d > 20f || !AIVision.CanSee(me, p.xz)) continue;
                dist = d; pos = p; id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None;
            }
        return dist < float.MaxValue;
    }

    // The open tile within 7 steps farthest from every enemy in view (short walk preferred). gain = how much farther
    // the nearest enemy would be than it is now.
    public static bool PickTile(World server, Entity player, float minDist, out int2 to, out float gain)
    {
        to = default; gain = 0f;
        var tr = new AITileReader(server);
        float3 me = AIBody.Pos(server, player);
        var blocked = AIObjects.BlockedInView(server, player, out _);
        var o = AIObservation.Take(server, player, false);
        if (o.enemies.Count == 0) return false;
        float nearNow = float.MaxValue;
        foreach (var e in o.enemies) nearNow = math.min(nearNow, math.distance(me.xz, e.pos.xz));
        int2 c = AIEdge.StandTile(server, me);   // 재실측 결함 2: the spacing BFS from the stand tile
        float best = float.MinValue, bestNear = 0f; bool ok = false;
        // BFS over walkable, unblocked tiles within 7 steps
        var dist = new Dictionary<int2, int> { [c] = 0 };
        var q = new Queue<int2>(); q.Enqueue(c);
        int2[] dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (dist[p] >= 7) continue;
            foreach (var d in dirs)
            {
                var n = p + d;
                if (dist.ContainsKey(n) || !tr.Walkable(n) || blocked.Contains(n) || !AIVision.CanSee(me, n)) continue;
                dist[n] = dist[p] + 1;
                q.Enqueue(n);
            }
        }
        foreach (var kv in dist)
        {
            float near = float.MaxValue;
            foreach (var e in o.enemies) near = math.min(near, math.distance((float2)kv.Key, e.pos.xz));
            float score = math.min(near, minDist + 1f) - 0.1f * kv.Value;   // far from every enemy (enough is enough), short walk
            if (score > best) { best = score; bestNear = near; to = kv.Key; ok = true; }
        }
        gain = ok ? bestNear - nearNow : 0f;
        return ok && gain > 0.5f;
    }

    public override bool Start(World server, Entity player, float now)
    {
        if (!Nearest(server, player, out float d0, out _id, out float3 ep)) { Status = SkillStatus.Failed; Reason = "보이는 적이 없음"; return false; }
        if (d0 >= _minDist) { Status = SkillStatus.Failed; Reason = $"가장 가까운 적과 이미 {d0:F1}칸 떨어져 있음"; return false; }
        if (!PickTile(server, player, _minDist, out _to, out float gain)) { Status = SkillStatus.Failed; Reason = "적들에게서 멀어질 열린 칸이 없음"; return false; }
        Facts.Add($"nearest enemy={_id} at ({ep.x:F1},{ep.z:F1}) d={d0:F1} to=({_to.x},{_to.y}) gain≈{gain:F1}");
        _nav.Start(server, player, _to, 0.5f, now);
        return true;
    }

    public override string Progress(World server, Entity player)
    {
        if (!Nearest(server, player, out float d, out ObjectID id, out _)) return "보이는 적이 없어짐";
        return $"가장 가까운 적 {id}와 {d:F1}칸{(d >= _minDist ? "(벌림)" : "")}, 지금 가는 칸 ({_to.x},{_to.y}){(_repicks > 0 ? $", 적이 다가와 칸을 {_repicks}번 다시 고름" : "")}, {_maxSeconds - (Time.realtimeSinceStartup - StartTime):F1}초 남음";
    }

    // Keeps the distance for the whole duration (2026-11-04): it used to end as soon as the nearest enemy was 5 tiles
    // away (~1 s); in a chase the enemies came back at once, the next call picked an explore walk toward them and the
    // call after that keep-away again (away -> walk -> away ... 9 of 10 switches in Claude's 20.9 s fight, ai2 swarm1).
    // Now it stays a running action the model can continue or leave: when an enemy comes within the distance again it
    // picks a new tile; it ends at the time cap, when no enemy is within combat range, or when no tile gets farther.
    private bool _reached;
    private int _repicks;
    private float _lastPick;
    public override BodyInput Tick(World server, Entity player, float now)
    {
        if (!Nearest(server, player, out float d, out ObjectID nid, out _) || d > AIBrain.Settings().combatRange)
        { if (!_reached) AIVitals.NoteAway(_id, d >= _minDist); return Finish(SkillStatus.Done, d < float.MaxValue ? $"가장 가까운 적 {nid}이(가) {d:F1}칸(싸움 거리 밖)" : "보이는 적이 없어짐"); }
        if (d >= _minDist && !_reached) { _reached = true; AIVitals.NoteAway(_id, true); }
        if (now - StartTime > _maxSeconds) { if (!_reached) AIVitals.NoteAway(_id, false); return Finish(SkillStatus.Done, $"{_maxSeconds:F0}초 끝, 가장 가까운 적 {nid}와 {d:F1}칸"); }
        var st = _nav.Tick(server, player, now, out float2 move);
        if ((st == NavStatus.Arrived || st == NavStatus.Failed) && d >= _minDist) return BodyInput.Idle;   // far enough: hold here
        if (st == NavStatus.Arrived || st == NavStatus.Failed || (d < _minDist - 1f && now - _lastPick >= 0.5f && math.distance(AIBody.Pos(server, player).xz, (float2)_to) < 1.5f))
        {
            if (now - _lastPick < 0.3f) return BodyInput.Idle;
            _lastPick = now;
            if (!PickTile(server, player, _minDist, out int2 to, out _))
            { if (!_reached) AIVitals.NoteAway(_id, false); return Finish(st == NavStatus.Failed ? SkillStatus.Failed : SkillStatus.Done, st == NavStatus.Failed ? $"이동 실패: {_nav.FailReason}" : $"더 멀어질 열린 칸 없음, 가장 가까운 적 {nid}와 {d:F1}칸"); }
            _repicks++;
            _to = to;
            _nav.Dispose(server); _nav = new AINavigator(); _nav.Start(server, player, _to, 0.5f, now);
            return BodyInput.Idle;
        }
        return new BodyInput { move = move, slot = -1 };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }

    // Known end: the time cap (exact). Leaving combat range can end it earlier (not estimated).
    public override float EtaSeconds(World server, Entity player) => math.max(0f, _maxSeconds - (Time.realtimeSinceStartup - StartTime));
}

// Retreat toward a known safe point (the core spawn) along an open known route (no digging). Ends: arrived within
// 2 tiles, or no enemy within 10 tiles in view for 2 s after moving at least 4 tiles, or 60 s.
public class RetreatSkill : AISkill
{
    private readonly int2 _point;
    private readonly string _label;
    private AISkill _child;
    private float _clearSince = -1f;
    private bool _lastLeg;
    private readonly bool _inside;   // 단계 3: the point is a walkable inside tile of the base (arrived = inside too; the walk passes own doors)
    public int2 Point => _point;
    public RetreatSkill(int2 point, string label, bool inside = false) { Name = "물러나기"; _point = point; _label = label; _inside = inside; Key = $"retreat:{point.x},{point.y}"; }

    private bool NextLeg(World server, Entity player, float now)
    {
        var plan = AITunnel.Plan(server, player, _point, out string why);
        // 단계 3: into the base past an own door: the tile plan sees the closed door as blocked; the walk (AINavigator)
        // opens and passes own doors, so walk to the point directly
        if ((plan == null || plan.walls.Count > 0) && _inside && AIEdge.PlanActive && !AIEdge.IsInside(AIEdge.StandTile(server, AIBody.Pos(server, player))))
        {
            _lastLeg = true;
            _child = new GotoSkill(_point, _label);
            _child.StartTime = now; _child.StartPos = AIBody.Pos(server, player);
            if (!_child.Start(server, player, now)) { Finish(SkillStatus.Failed, "걷기 시작 실패: " + _child.Reason); return false; }
            Facts.Add("no open tile route (a door on the way): walking to the point through own doors");
            return true;
        }
        if (plan == null || plan.walls.Count > 0) { Finish(SkillStatus.Failed, plan == null ? why : $"열린 길 없음(벽 {plan.walls.Count}개)"); return false; }
        int2 wp = plan.path.Count > 30 ? plan.path[29] : _point;
        _lastLeg = math.all(wp == _point);
        _child = new GotoSkill(wp, _label);
        _child.StartTime = now; _child.StartPos = AIBody.Pos(server, player);
        if (!_child.Start(server, player, now)) { Finish(SkillStatus.Failed, "걷기 시작 실패: " + _child.Reason); return false; }
        return true;
    }

    public override bool Start(World server, Entity player, float now)
    {
        Facts.Add($"to {_label} ({_point.x},{_point.y})");
        return NextLeg(server, player, now) || false;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        float3 me = AIBody.Pos(server, player);
        if (math.distance(me.xz, (float2)_point) <= 2f && (!_inside || AIEdge.IsInside(AIEdge.StandTile(server, me)))) return Finish(SkillStatus.Done, $"{_label} 도착");
        if (now - StartTime > 60f) return Finish(SkillStatus.Failed, "시간 초과(60초)");
        var o = AIObservation.Take(server, player, false);
        bool clear = !o.enemies.Exists(e => e.dist <= 10f);
        if (clear && (!_inside || AIEdge.IsInside(AIEdge.StandTile(server, me))) && math.distance(me.xz, StartPos.xz) >= 4f) { if (_clearSince < 0f) _clearSince = now; if (now - _clearSince >= 2f) return Finish(SkillStatus.Done, "10칸 안에 보이는 적이 없어 멈춤"); }
        else _clearSince = -1f;
        if (_child == null && !NextLeg(server, player, now)) return BodyInput.Idle;
        var bi = _child.Tick(server, player, now);
        if (_child.Status != SkillStatus.Running)
        {
            bool done = _child.Status == SkillStatus.Done;
            string r = _child.Reason;
            try { _child.Cleanup(server); } catch { }
            _child = null;
            if (!done) return Finish(SkillStatus.Failed, "걷기 실패: " + r);
        }
        return bi;
    }

    public override void Cleanup(World server) { if (_child != null) { try { _child.Cleanup(server); } catch { } _child = null; } }

    // Known end conditions: arriving within 2 tiles of the point on the last leg (straight distance at the measured walk
    // speed), or the 2 s "no enemy within 10 tiles" wait once it has started. Otherwise unknown.
    public override float EtaSeconds(World server, Entity player)
    {
        float eta = -1f;
        float d = math.distance(AIBody.Pos(server, player).xz, (float2)_point);
        if (_lastLeg && _child != null) eta = math.max(0f, d - 2f) / AIBody.WalkSpeed;
        if (_clearSince >= 0f) { float c = math.max(0f, 2f - (Time.realtimeSinceStartup - _clearSince)); eta = eta < 0f ? c : math.min(eta, c); }
        return eta;
    }
}

// Motion boundaries (HEAD decision 2026-11-04, user question "판단속도는 최대로, 모션에 따른 판단속도 제한?"): Decisions
// is still asked as fast as possible, but a switch to another action is applied only at a natural breakpoint of the
// motion in progress, never in the middle of a swing, a one-tile step or eating (emergencies excepted, AIBrain).
// The swing timing is the game's own effective value for this player right now, read every frame:
// - EquipmentSlot.AttackWithItem (Pug.Other/EquipmentSlot.cs:248-253): cooldownTime = 0.4 s / max(1 + stat[40]/1000 +
//   stat[65]/1000, 0.1) (SummarizedConditionEffectsBuffer: melee attack speed from gear, buffs, food, set bonuses), then
//   StartCooldownForItem(..., isRegularHit: true) (EquipmentSlot.cs:296-297) starts
//   SyncedPlayerSharedCooldownTimersCD[MeleeWeaponSlot] with that time and PlayerAttackCooldownCD with min(time, 0.5 s).
// - So the remaining ticks of those two TickTimers (targetTicks - GetElapsedTicks) say how much of the current swing is
//   left; a swing is "in progress" while more than one server tick remains. Weapons, gear, buffs and food are covered
//   because the game already applied them to the timer.
// - Fallback / check: the observed interval between this character's own swing starts (startTick changes), recent
//   median per held item, logged as "MOTION swing ..." next to the game's timer length.
// Walking: one tile step is the motion unit (a boundary whenever the AI enters a new tile, ~0.18 s at 5.7 tiles/s);
// eating is one motion until the item is eaten (EatSkill ends). Standing / waiting is always a boundary.
public static class AIMotion
{
    private sealed class State
    {
        public int2 lastTile; public bool hasTile; public float3 lastPos; public float lastT = -1f;
        public readonly List<(float t, float3 p)> trail = new List<(float, float3)>();   // positions of the last 0.3 s (the server moves the player 20 times a second, not every frame)
        public uint lastStart; public bool hadStart; public float lastSwingAt = -1f;
        public readonly Dictionary<ObjectID, List<float>> intervals = new Dictionary<ObjectID, List<float>>();
        public readonly Dictionary<ObjectID, int> swings = new Dictionary<ObjectID, int>();
        public readonly Dictionary<ObjectID, float> cooldown = new Dictionary<ObjectID, float>();
        public bool boundary = true; public string mid = ""; public float swingLeft, swingLen;
        public int swingSeq; public bool timerOk;   // swing starts seen so far; the game's swing timer could be read this frame (HitOnceSkill)
        public int frame = -1;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Motion);
    private static State S => StateOf(AIAgent.Cur);

    public static bool AtBoundary => S.boundary;
    public static string Mid => S.mid;            // what is in progress when not at a boundary ("" at a boundary)
    public static float SwingLeft => S.swingLeft; // seconds left of the current swing (0 = none)
    public static int SwingSeq => S.swingSeq;     // number of swing starts seen (a new swing increments it)
    public static bool TimerOk => S.timerOk;      // the swing timer was readable this frame

    private static uint _tickRate;
    private static int _tickFrame = -1; private static bool _tickOk; private static Unity.NetCode.NetworkTick _tick;

    public static uint TickRate(World server)
    {
        if (_tickRate != 0) return _tickRate;
        try
        {
            using (var q = server.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Unity.NetCode.ClientServerTickRate>()))
                if (q.CalculateEntityCount() == 1) { int r = q.GetSingleton<Unity.NetCode.ClientServerTickRate>().SimulationTickRate; if (r > 0) _tickRate = (uint)r; }
        }
        catch { }
        if (_tickRate == 0) _tickRate = 60;   // Netcode default when the singleton is not there
        AIPlayerSpikeMod.Log($"MOTION: server simulation tick rate {_tickRate}/s");
        return _tickRate;
    }

    private static bool ServerTick(World server, out Unity.NetCode.NetworkTick tick)
    {
        if (_tickFrame != Time.frameCount)
        {
            _tickFrame = Time.frameCount; _tickOk = false;
            try
            {
                using (var q = server.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Unity.NetCode.NetworkTime>()))
                    if (q.CalculateEntityCount() == 1) { _tick = q.GetSingleton<Unity.NetCode.NetworkTime>().ServerTick; _tickOk = _tick.IsValid; }
            }
            catch { }
        }
        tick = _tick;
        return _tickOk;
    }

    // Remaining seconds of the player's current swing (max of the two game timers), its full length, and the start tick
    // of the melee shared timer (a new swing starts it again).
    public static bool Swing(World server, Entity player, out float left, out float length, out uint startTick)
    {
        left = 0f; length = 0f; startTick = 0;
        if (!ServerTick(server, out var tick)) return false;
        uint rate = TickRate(server);
        var em = server.EntityManager;
        bool any = false;
        if (em.HasBuffer<SyncedPlayerSharedCooldownTimersCD>(player))
        {
            var b = em.GetBuffer<SyncedPlayerSharedCooldownTimersCD>(player, true);
            int idx = (int)SyncedSharedCooldownType.MeleeWeaponSlot;
            if (idx >= 0 && idx < b.Length)
            {
                var t = b[idx].cooldown;
                if (t.isRunning)
                {
                    int rem = (int)t.targetTicks - t.GetElapsedTicks(tick);
                    if (rem > 0) { left = math.max(left, rem / (float)rate); any = true; }
                    length = math.max(length, t.targetTicks / (float)rate);
                    startTick = t.startTick.SerializedData;
                }
            }
        }
        if (em.HasComponent<PlayerAttackCooldownCD>(player))
        {
            var t = em.GetComponentData<PlayerAttackCooldownCD>(player).cooldown;
            if (t.isRunning)
            {
                int rem = (int)t.targetTicks - t.GetElapsedTicks(tick);
                if (rem > 0) { left = math.max(left, rem / (float)rate); any = true; }
                length = math.max(length, t.targetTicks / (float)rate);
            }
        }
        return any;
    }

    // Called once per frame per character (AIBrain.Tick) with the running skill.
    public static void Frame(World server, Entity player, AISkill cur, float now)
    {
        var s = S;
        if (s.frame == Time.frameCount) return;
        s.frame = Time.frameCount;
        var em = server.EntityManager;
        if (!em.Exists(player)) return;
        float3 pos = em.GetComponentData<LocalTransform>(player).Position;
        int2 tile = AIBody.TileOf(pos);
        s.trail.Add((now, pos));
        while (s.trail.Count > 1 && now - s.trail[0].t > 0.3f) s.trail.RemoveAt(0);
        float dt = now - s.trail[0].t;
        float speed = dt >= 0.1f ? math.distance(pos.xz, s.trail[0].p.xz) / dt : 0f;
        bool newTile = s.hasTile && !math.all(tile == s.lastTile);
        s.lastTile = tile; s.hasTile = true; s.lastPos = pos; s.lastT = now;

        bool swinging = false;
        float left = 0f, len = 0f; uint start = 0;
        try { swinging = Swing(server, player, out left, out len, out start); } catch (Exception e) { AIPlayerSpikeMod.LogError("motion: swing timer read failed", e); }
        s.swingLeft = swinging ? left : 0f; s.swingLen = len;
        s.timerOk = ServerTick(server, out _) && (em.HasBuffer<SyncedPlayerSharedCooldownTimersCD>(player) || em.HasComponent<PlayerAttackCooldownCD>(player));
        if (start != 0 && (!s.hadStart || start != s.lastStart))
        {
            // a new swing started: observed interval since the previous one (per held item)
            ObjectID item = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID : ObjectID.None;
            if (s.hadStart && s.lastSwingAt > 0f && now - s.lastSwingAt < 2f)
            {
                if (!s.intervals.TryGetValue(item, out var l)) s.intervals[item] = l = new List<float>();
                l.Add(now - s.lastSwingAt); while (l.Count > 15) l.RemoveAt(0);
            }
            s.swings.TryGetValue(item, out int n); s.swings[item] = ++n;
            bool changed = !s.cooldown.TryGetValue(item, out float oldLen) || math.abs(oldLen - len) > 0.001f;
            s.cooldown[item] = len;
            if (changed || n % 20 == 0) AIPlayerSpikeMod.Log($"MOTION swing #{n} item={item}: game cooldown timer {len:F3}s ({len * TickRate(server):F0} ticks){(changed ? " (new value)" : "")}; observed interval {IntervalNote(item)}; attack-speed stats {SpeedStats(server, player)}");
            s.lastStart = start; s.hadStart = true; s.lastSwingAt = now; s.swingSeq++;
        }

        string mid = "";
        if (cur != null && cur.Status == SkillStatus.Running)
        {
            if (cur is EatSkill) mid = "먹는 중(먹을 때까지 한 동작)";
            else if (swinging && left > 1f / TickRate(server) + 1e-4f) mid = $"휘두르는 중(게임 쿨다운 {len:F2}초 중 {left:F2}초 남음)";
            else if (!newTile && speed > 1f) mid = "한 칸 걷는 중";
        }
        s.mid = mid;
        s.boundary = mid == "";
    }

    // "median 0.40 s (n 12)" of the observed swing intervals with this item, or "none yet".
    public static string IntervalNote(ObjectID item)
    {
        if (!S.intervals.TryGetValue(item, out var l) || l.Count == 0) return "none yet";
        var c = new List<float>(l); c.Sort();
        return $"median {c[c.Count / 2]:F3}s (n {c.Count}, min {c[0]:F3}, max {c[c.Count - 1]:F3})";
    }

    // Game cooldown length last seen for this item (0 = not seen).
    public static float CooldownOf(ObjectID item) => S.cooldown.TryGetValue(item, out float v) ? v : 0f;

    // The two attack-speed condition sums AttackWithItem divides by (EquipmentSlot.cs:247-252), for the log.
    public static string SpeedStats(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!em.HasBuffer<SummarizedConditionEffectsBuffer>(player)) return "?";
        var b = em.GetBuffer<SummarizedConditionEffectsBuffer>(player, true);
        if (b.Length <= 65) return "?";
        return $"[40]={b[40].value} [65]={b[65].value} -> 0.4/{math.max(1f + b[40].value / 1000f + b[65].value / 1000f, 0.1f):F3}";
    }
}

// TEST ONLY (behind AIPlayers/testcmds.on): a repeatable fight for comparing builds (2026-11-04, combat polish).
//   /ai <member> arena go [x z]  - teleport the AI to the arena spot (the game's own teleport state, as the map waypoint
//                                  teleport does: TriggerUIActionsSystem UIInputAction.Teleport), full health, and a fresh
//                                  WoodSword in the bag (slot 10+) after every melee weapon is removed, so each round starts
//                                  the same and the attack skill takes the working weapon out of the bag.
//   /ai arena spawn [n] [ObjectID] [radius] - remove the live enemies within 25 tiles of the spot, then spawn n enemies
//                                  (default 4 MushroomEnemy) on a ring around the spot (nearest walkable tile).
public static class AIArena
{
    public static int2 Spot = new int2(-32, 32);   // ai2: open mushroom cave where swarm fights happened (host stood there)

    public static void Command(World server, Entity player, string[] parts)
    {
        string sub = parts.Length >= 3 ? parts[2] : "";
        if (sub == "go") Go(server, player, parts);
        else if (sub == "spawn") Spawn(server, player, parts);
        else AIPlayerSpikeMod.Reply("사용법: /ai <멤버> arena go [x z] · /ai arena spawn [수] [ObjectID] [반지름] [explore]");
    }

    private static void Go(World server, Entity player, string[] parts)
    {
        var em = server.EntityManager;
        if (parts.Length >= 5 && int.TryParse(parts[3], out int x) && int.TryParse(parts[4], out int z)) Spot = new int2(x, z);
        var tr = new AITileReader(server);
        int2 to = Nearest(tr, Spot, 3);
        int cleared = Clear(em);   // the AI arrives in an empty arena; "spawn" then sets the fight
        // teleport the way the game does it for a player (state machine moves the ghost and plays the effect, ~6 s)
        if (em.HasComponent<PlayerState.PlayerStateCD>(player) && em.HasComponent<PlayerState.TeleportingStateCD>(player))
        {
            var ps = em.GetComponentData<PlayerState.PlayerStateCD>(player);
            ps.SetNextState(PlayerState.PlayerStateEnum.Teleporting, true);
            em.SetComponentData(player, ps);
            var ts = em.GetComponentData<PlayerState.TeleportingStateCD>(player);
            ts.targetPosition = new Vector3(to.x, 0f, to.y);
            em.SetComponentData(player, ts);
        }
        var h = em.GetComponentData<HealthCD>(player);
        // 합치기 전 수정 6: full = the max with conditions (the well-fed bonus); h.maxHealth set 105 -> 100 and read as a hit (t=461.2)
        h.health = em.HasBuffer<SummarizedConditionEffectsBuffer>(player) ? h.GetMaxHealthWithConditions(em.GetBuffer<SummarizedConditionEffectsBuffer>(player, true)) : h.maxHealth;
        em.SetComponentData(player, h);
        // weapon: remove every melee weapon, then one fresh WoodSword into the first empty bag slot (10+)
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
        int removed = 0, put = -1;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || info.objectType != ObjectType.MeleeWeapon) continue;
            var s = buf[i]; s.objectData = new ObjectDataCD(); s.auxDataIndex = 0; buf[i] = s; removed++;
        }
        ObjectInfo wi = PugDatabase.GetObjectInfo(ObjectID.WoodSword);
        for (int pass = 0; pass < 2 && put < 0; pass++)
            for (int i = pass == 0 ? 10 : 0; i < buf.Length && put < 0; i++)
                if (buf[i].objectID == ObjectID.None) { var s = buf[i]; s.objectData = new ObjectDataCD { objectID = ObjectID.WoodSword, amount = wi != null ? wi.initialAmount : 1 }; s.auxDataIndex = 0; buf[i] = s; put = i; }
        AIPlayerSpikeMod.Log($"TEST-ONLY arena go: cleared {cleared} enemies; teleport to ({to.x},{to.y}) (spot {Spot.x},{Spot.y}); health {h.health}; removed {removed} melee weapon(s), fresh WoodSword (durability {(wi != null ? wi.initialAmount : 1)}) into slot {put}");
        AIPlayerSpikeMod.Reply($"[시험용] {AIAgent.Cur.Name} 경기장 ({to.x},{to.y})로 순간이동, 체력 가득, 새 WoodSword {put}번 칸");
    }

    private static void Spawn(World server, Entity player, string[] parts)
    {
        var em = server.EntityManager;
        int n = parts.Length >= 4 && int.TryParse(parts[3], out int nn) ? nn : 4;
        ObjectID kind = parts.Length >= 5 && Enum.TryParse(parts[4], out ObjectID k) ? k : ObjectID.MushroomEnemy;
        float r = parts.Length >= 6 && float.TryParse(parts[5], out float rr) ? rr : 5f;
        // "... explore" (7th word): give this member the explore goal in the same frame, before the enemies appear, so
        // the first decision of the fight already has the goal (A/B of the close-fight set; with the goal sent as a
        // separate chat line ~5 s earlier the AI had walked 10 tiles away before the spawn)
        if (parts.Length >= 7 && (parts[6] == "explore" || parts[6] == "탐험")) AIBrain.SetGoal(server, player, GoalKind.Explore, ObjectID.None, "", 0, "host");
        // "... kill" (7th word, combat stage 2 test ②): the close-fight input of this member gets the line "이번 교전 목표: 이 버섯
        // 야수 처치(호스트 지시)." — a test switch only, no goal kind; every spawn sets or clears it
        AIBrain.KillIntent = parts.Length >= 7 && (parts[6] == "kill" || parts[6] == "처치");
        if (AIBrain.KillIntent) AIPlayerSpikeMod.Log("TEST-ONLY arena spawn: kill intent line on for this member's close fight");
        int cleared = Clear(em);
        BlobAssetReference<PugDatabase.PugDatabaseBank> bank;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
        var tr = new AITileReader(server);
        var at = new List<string>();
        for (int i = 0; i < n; i++)
        {
            float a = 2f * math.PI * i / n;
            int2 t = Nearest(tr, Spot + new int2((int)math.round(r * math.cos(a)), (int)math.round(r * math.sin(a))), 2);
            var e = EntityUtility.CreateEntity(server, new Vector3(t.x, 0f, t.y), kind, 1, bank);
            if (e != Entity.Null) at.Add($"({t.x},{t.y})");
        }
        AIPlayerSpikeMod.Log($"TEST-ONLY arena spawn: cleared {cleared} enemies within 25 tiles of ({Spot.x},{Spot.y}); spawned {at.Count} {kind} at {string.Join(" ", at)}");
        AIPlayerSpikeMod.Reply($"[시험용] 경기장: 적 {cleared} 치움, {kind} {at.Count}마리");
    }

    private static int Clear(EntityManager em)
    {
        int cleared = 0;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents)
                if (math.distance(em.GetComponentData<LocalTransform>(e).Position.xz, (float2)Spot) <= 25f) { em.DestroyEntity(e); cleared++; }
        return cleared;
    }

    private static int2 Nearest(AITileReader tr, int2 p, int rad)
    {
        for (int d = 0; d <= rad; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dz = -d; dz <= d; dz++)
                    if (math.max(math.abs(dx), math.abs(dz)) == d && tr.Walkable(p + new int2(dx, dz))) return p + new int2(dx, dz);
        return p;
    }
}
