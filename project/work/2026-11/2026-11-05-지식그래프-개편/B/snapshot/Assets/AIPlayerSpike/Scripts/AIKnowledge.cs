// AIKnowledge — game knowledge for the AI characters: (1) a test-only dump of the game's object database for the
// knowledge graph (Tools/knowledge/build.mjs), (2) per character, what it has seen or held, so the graph can show each
// character only what is open to it. Design: work/2026-11/2026-11-04-게임-지식그래프/2-설계.md (session 코어키퍼-AI-캐릭터).
//
// - Dump: "/ai kgdump" (behind AIPlayers/testcmds.on). Walks DatabaseBankCD.objectInfos and each primary prefab's
//   components, writes <persistentDataPath>/AIPlayers/knowledge-dump.json (hand-written JSON; JsonUtility drops nested
//   classes in mod-compiled code). Read-only on the game.
// - Known: <persistentDataPath>/AIPlayers/<world>/<member>/known.json, next to memory.json but a separate file.
//   seen = objects, enemies, dropped items and stations in view; held = in the bag or in hand; tiles = wall and ore
//   tiles in view as "tileset:tileType" (resolved to objects by the graph); drops = what breaking a source gave
//   (AIDrops). The first time, it is filled from memory.json (ores, stations, tools, combat numbers, drops) so a
//   character that played before does not start empty. Recipes of seen stations and their materials are opened by
//   the graph side (as on the game's crafting screen), not here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Entities;
using UnityEngine;

public static class AIKnowledge
{
    // ------------------------------------------------------------------ known (per character)

    [Serializable]
    public class KnownData
    {
        public int version = 1;
        public int[] seenId = new int[0];
        public long[] seenAt = new long[0];
        public int[] heldId = new int[0];
        public long[] heldAt = new long[0];
        public string[] tiles = new string[0];   // "tileset:tileType"
        public long[] tilesAt = new long[0];
        public int[] dropSrc = new int[0];       // AIDrops source (ObjectID, or -1 = big root)
        public int[] dropItem = new int[0];
        public string prefilled = "";            // "memory.json" when the first fill came from it
        public long updated;
    }

    private sealed class State
    {
        public string world, path;
        public readonly Dictionary<int, long> seen = new Dictionary<int, long>();
        public readonly Dictionary<int, long> held = new Dictionary<int, long>();
        public readonly Dictionary<string, long> tiles = new Dictionary<string, long>();
        public readonly HashSet<(int src, int item)> drops = new HashSet<(int, int)>();
        public string prefilled = "";
        public bool dirty;
        public float lastNote = -999f, lastSave;
        public int notes;
    }
    // keyed by member id, so no slot in AIAgent is needed
    private static readonly Dictionary<string, State> _states = new Dictionary<string, State>();
    private static State S
    {
        get
        {
            string id = AIAgent.Cur.Id;
            if (!_states.TryGetValue(id, out var s)) _states[id] = s = new State();
            return s;
        }
    }

    public const float NoteEvery = 2f;   // seconds
    public const int TilesEvery = 3;     // every 3rd note also reads the wall tiles in view

    // The same disclosure boundary as kg.openView: seen/held objects, observed ore tiles,
    // hand recipes and the recipes/materials of stations this character has actually seen.
    // Being known does not assert that a source exists or that the current tool can harvest it.
    public static List<ObjectID> KnownItems(World world, Entity player)
    {
        Ensure();
        var known = new HashSet<ObjectID>();
        foreach (var id in S.seen.Keys) known.Add((ObjectID)id);
        foreach (var id in S.held.Keys) known.Add((ObjectID)id);
        foreach (var kv in AIBody.InventoryCounts(world, player)) known.Add(kv.Key);
        var observation = AIObservation.Take(world, player, true);
        foreach (var o in observation.objects) known.Add(o.id);
        foreach (var o in observation.items) known.Add(o.id);
        foreach (var wall in observation.wallTiles)
        {
            if (AIWallData.TryGet(world, wall.tileset, out _, out _, out ObjectID block)) known.Add(block);
            if (wall.ore && AIBrain.TryOreObject(world, wall.oreTileset, out ObjectID ore)) known.Add(ore);
        }
        foreach (var tile in S.tiles.Keys)
        {
            var fields = tile.Split(':');
            if (fields.Length == 2 && int.TryParse(fields[0], out int tileset) && int.TryParse(fields[1], out int type))
            {
                if (type == (int)TileType.ore && AIBrain.TryOreObject(world, tileset, out ObjectID ore)) known.Add(ore);
                else if (type == (int)TileType.wall && AIWallData.TryGet(world, tileset, out _, out _, out ObjectID block)) known.Add(block);
            }
        }
        var recipes = new HashSet<ObjectID>(AICraft.HandRecipes(world, player));
        foreach (var station in new List<ObjectID>(known)) foreach (var item in AICraft.PrefabRecipes(world, station)) recipes.Add(item);
        foreach (var item in recipes)
        {
            known.Add(item);
            foreach (var material in AICraft.Materials(world, player, item, 1)) known.Add(material.id);
        }
        known.Remove(ObjectID.None);
        var result = new List<ObjectID>(known);
        result.Sort((a, b) => ((int)a).CompareTo((int)b));
        return result;
    }

    private static bool Ensure()
    {
        var s = S;
        string w = AIPlayerSpikeMod.WorldId;
        if (string.IsNullOrEmpty(w)) return false;
        if (w == s.world) return true;
        if (s.world != null) Save(true);
        s.world = w;
        s.seen.Clear(); s.held.Clear(); s.tiles.Clear(); s.drops.Clear(); s.prefilled = ""; s.dirty = false;
        s.path = Path.Combine(AIMemory.CharDir(w), "known.json");
        try
        {
            if (File.Exists(s.path))
            {
                var d = JsonUtility.FromJson<KnownData>(File.ReadAllText(s.path, Encoding.UTF8));
                if (d != null)
                {
                    for (int i = 0; d.seenId != null && i < d.seenId.Length; i++) s.seen[d.seenId[i]] = d.seenAt != null && i < d.seenAt.Length ? d.seenAt[i] : 0;
                    for (int i = 0; d.heldId != null && i < d.heldId.Length; i++) s.held[d.heldId[i]] = d.heldAt != null && i < d.heldAt.Length ? d.heldAt[i] : 0;
                    for (int i = 0; d.tiles != null && i < d.tiles.Length; i++) s.tiles[d.tiles[i]] = d.tilesAt != null && i < d.tilesAt.Length ? d.tilesAt[i] : 0;
                    for (int i = 0; d.dropSrc != null && d.dropItem != null && i < d.dropSrc.Length && i < d.dropItem.Length; i++) s.drops.Add((d.dropSrc[i], d.dropItem[i]));
                    s.prefilled = d.prefilled ?? "";
                }
            }
            else
            {
                Prefill(s, Path.Combine(AIMemory.CharDir(w), "memory.json"));
            }
            AIPlayerSpikeMod.Log($"KNOWN loaded for {AIAgent.Cur.Id} in world {w}: seen {s.seen.Count}, held {s.held.Count}, tiles {s.tiles.Count}, drops {s.drops.Count}{(s.prefilled != "" ? ", first filled from " + s.prefilled : "")} ({s.path})");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"KNOWN: reading {s.path} failed; starting empty", e); }
        return true;
    }

    // First fill from this character's memory.json (same rule as Tools/knowledge/kg.mjs prefill).
    private static void Prefill(State s, string memPath)
    {
        if (!File.Exists(memPath)) return;
        var d = JsonUtility.FromJson<AIMemory.Data>(File.ReadAllText(memPath, Encoding.UTF8));
        if (d == null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        void Seen(int id, long t) { if (id > 0 && !s.seen.ContainsKey(id)) s.seen[id] = t; }
        void Held(int id, long t) { if (id > 0 && !s.held.ContainsKey(id)) s.held[id] = t; }
        for (int i = 0; d.oreId != null && i < d.oreId.Length; i++) Seen(d.oreId[i], d.oreSeen != null && i < d.oreSeen.Length ? d.oreSeen[i] : now);
        for (int i = 0; d.stationId != null && i < d.stationId.Length; i++) Seen(d.stationId[i], d.stationSeen != null && i < d.stationSeen.Length ? d.stationSeen[i] : now);
        for (int i = 0; d.toolId != null && i < d.toolId.Length; i++) Held(d.toolId[i], now);
        for (int i = 0; d.noDmgId != null && i < d.noDmgId.Length; i++) Seen(d.noDmgId[i], now);
        for (int i = 0; d.combatLearned != null && i + 4 < d.combatLearned.Length; i += 5)
        {
            int kind = d.combatLearned[i], id = d.combatLearned[i + 1];
            if (kind == 1) Held(id >= AIVitals.BrokenKeyOffset ? id - AIVitals.BrokenKeyOffset : id, now);   // weapon used
            else Seen(id, now);                                                                                // enemy met
        }
        for (int i = 0; d.drops != null && i + 4 < d.drops.Length; i += 5)
        {
            int src = d.drops[i], item = d.drops[i + 1];
            Seen(src, now);
            if (item > 0) { Held(item, now); s.drops.Add((src, item)); }
        }
        s.prefilled = "memory.json";
        s.dirty = true;
    }

    // Called from AIMemory.Observe (about once a second); does work every NoteEvery seconds.
    public static void Note(World server, Entity player, float now)
    {
        var s = S;
        if (now - s.lastNote < NoteEvery) return;
        s.lastNote = now;
        if (!Ensure()) return;
        try
        {
            long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool withTiles = (s.notes++ % TilesEvery) == 0;
            var o = AIObservation.Take(server, player, withTiles);
            void Seen(ObjectID id) { if (id != ObjectID.None && !s.seen.ContainsKey((int)id)) { s.seen[(int)id] = unix; s.dirty = true; } }
            void Held(ObjectID id) { if (id != ObjectID.None && !s.held.ContainsKey((int)id)) { s.held[(int)id] = unix; s.dirty = true; } Seen(id); }
            foreach (var x in o.objects) Seen(x.id);
            foreach (var x in o.enemies) Seen(x.id);
            foreach (var x in o.items) Seen(x.id);
            Held(o.equipped);
            foreach (var kv in AIBody.InventoryCounts(server, player)) Held(kv.Key);
            if (withTiles)
            {
                foreach (var t in o.wallTiles)
                {
                    string wk = $"{t.tileset}:{(int)TileType.wall}";
                    if (!s.tiles.ContainsKey(wk)) { s.tiles[wk] = unix; s.dirty = true; }
                    if (t.ore)
                    {
                        string ok = $"{t.oreTileset}:{(int)TileType.ore}";
                        if (!s.tiles.ContainsKey(ok)) { s.tiles[ok] = unix; s.dirty = true; }
                    }
                }
            }
            foreach (var (src, item) in DropPairs()) if (s.drops.Add((src, item))) s.dirty = true;
            if (s.dirty && now - s.lastSave > 30f) Save(false);
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("KNOWN: note failed", e); }
    }

    private static List<(int, int)> DropPairs()
    {
        var res = new List<(int, int)>();
        int[] a = AIDrops.Export();
        for (int i = 0; a != null && i + 4 < a.Length; i += 5) if (a[i + 1] > 0) res.Add((a[i], a[i + 1]));
        return res;
    }

    // Called from AIMemory.Save (force on leave / world change) and from Note every 30 s while something is new.
    public static void Save(bool force)
    {
        var s = S;
        if (s.world == null || s.path == null || (!s.dirty && !force)) return;
        try
        {
            var d = new KnownData();
            var si = new List<int>(); var st = new List<long>();
            foreach (var kv in s.seen) { si.Add(kv.Key); st.Add(kv.Value); }
            var hi = new List<int>(); var ht = new List<long>();
            foreach (var kv in s.held) { hi.Add(kv.Key); ht.Add(kv.Value); }
            var ti = new List<string>(); var tt = new List<long>();
            foreach (var kv in s.tiles) { ti.Add(kv.Key); tt.Add(kv.Value); }
            var ds = new List<int>(); var dit = new List<int>();
            foreach (var p in s.drops) { ds.Add(p.src); dit.Add(p.item); }
            d.seenId = si.ToArray(); d.seenAt = st.ToArray(); d.heldId = hi.ToArray(); d.heldAt = ht.ToArray();
            d.tiles = ti.ToArray(); d.tilesAt = tt.ToArray(); d.dropSrc = ds.ToArray(); d.dropItem = dit.ToArray();
            d.prefilled = s.prefilled; d.updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Directory.CreateDirectory(Path.GetDirectoryName(s.path));
            string tmp = s.path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(d), new UTF8Encoding(false));
            if (File.Exists(s.path)) File.Delete(s.path);
            File.Move(tmp, s.path);
            s.dirty = false;
            s.lastSave = Time.realtimeSinceStartup;
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError($"KNOWN: writing {s.path} failed", e); }
    }

    // ------------------------------------------------------------------ dump (test-only)

    public static string DumpPath => Path.Combine(Application.persistentDataPath, "AIPlayers", "knowledge-dump.json");

    public static void Dump(World server)
    {
        var em = server.EntityManager;
        var errors = new List<string>();
        int n = 0;
        try
        {
            BlobAssetReference<PugDatabase.PugDatabaseBank> bank;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() != 1) { AIPlayerSpikeMod.Reply("kgdump: 게임 데이터베이스를 찾지 못함"); return; }
                bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
            }
            var sb = new StringBuilder(1 << 20);
            sb.Append("{\"dump\":1,\"appVersion\":\"").Append(Esc(Application.version)).Append("\",\"time\":").Append(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            sb.Append(",\"objects\":[");
            ref var infos = ref bank.Value.objectInfos;
            bool first = true;
            for (int i = 0; i < infos.Length; i++)
            {
                ref var info = ref infos[i];
                try
                {
                    var o = new StringBuilder(256);
                    o.Append("{\"id\":").Append((int)info.objectID).Append(",\"var\":").Append(info.variation)
                     .Append(",\"type\":\"").Append(info.objectType).Append("\",\"rarity\":").Append((int)info.rarity)
                     .Append(",\"sell\":").Append(info.sellValue).Append(",\"loot\":").Append((int)info.lootTableID)
                     .Append(",\"craftTime\":").Append(F(info.craftingTime)).Append(",\"stack\":").Append(info.isStackable ? "true" : "false")
                     .Append(",\"ts\":").Append(info.tileset).Append(",\"tt\":").Append((int)info.tileType);
                    o.Append(",\"req\":[");
                    for (int r = 0; r < info.requiredObjectsToCraft.Length; r++)
                    {
                        if (r > 0) o.Append(',');
                        o.Append('[').Append((int)info.requiredObjectsToCraft[r].objectID).Append(',').Append(info.requiredObjectsToCraft[r].amount).Append(']');
                    }
                    o.Append(']');
                    Entity prefab = Entity.Null;
                    try { prefab = PugDatabase.GetPrimaryPrefabEntity(info.objectID, bank, info.variation); } catch (Exception e) { errors.Add($"{(int)info.objectID}/{info.variation}: prefab {e.GetType().Name}"); }
                    if (prefab != Entity.Null && em.Exists(prefab)) { AppendPrefab(em, prefab, o); AppendCombat(em, prefab, info.variation, o); }
                    else o.Append(",\"noPrefab\":true");
                    if (info.variation == 0)
                    {
                        o.Append(",\"ko\":\"").Append(Esc(Name(info.objectID, "Korean"))).Append('"');
                        o.Append(",\"en\":\"").Append(Esc(Name(info.objectID, "English"))).Append('"');
                    }
                    o.Append('}');
                    if (!first) sb.Append(',');
                    sb.Append(o);
                    first = false;
                    n++;
                }
                catch (Exception e) { errors.Add($"{(int)info.objectID}/{info.variation}: {e.GetType().Name} {e.Message}"); }
            }
            sb.Append("],\"errors\":[");
            for (int i = 0; i < errors.Count && i < 200; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(Esc(errors[i])).Append('"'); }
            sb.Append("]}");
            Directory.CreateDirectory(Path.GetDirectoryName(DumpPath));
            File.WriteAllText(DumpPath, sb.ToString(), new UTF8Encoding(false));
            AIPlayerSpikeMod.Log($"KGDUMP: {n} entries, {errors.Count} errors -> {DumpPath}");
            AIPlayerSpikeMod.Reply($"kgdump 완료: 항목 {n}, 오류 {errors.Count} ({DumpPath})");
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("KGDUMP failed", e);
            AIPlayerSpikeMod.Reply($"kgdump 실패: {e.GetType().Name} {e.Message}");
        }
    }

    private static void AppendPrefab(EntityManager em, Entity p, StringBuilder o)
    {
        if (em.HasBuffer<CanCraftObjectsBuffer>(p))
        {
            var b = em.GetBuffer<CanCraftObjectsBuffer>(p, true);
            o.Append(",\"canCraft\":[");
            int k = 0;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i].objectID == ObjectID.None) continue;
                if (k++ > 0) o.Append(',');
                o.Append('[').Append((int)b[i].objectID).Append(',').Append(b[i].amount).Append(',').Append(F(b[i].craftingTimeOverride)).Append(']');
            }
            o.Append(']');
        }
        if (em.HasComponent<HealthCD>(p)) o.Append(",\"hp\":").Append(em.GetComponentData<HealthCD>(p).maxHealth);
        if (em.HasComponent<DamageReductionCD>(p)) o.Append(",\"red\":").Append(em.GetComponentData<DamageReductionCD>(p).reduction);
        if (em.HasComponent<DurabilityCD>(p)) o.Append(",\"dur\":").Append(em.GetComponentData<DurabilityCD>(p).maxDurability);
        if (em.HasComponent<DropsLootFromLootTableCD>(p)) o.Append(",\"dropLoot\":").Append((int)em.GetComponentData<DropsLootFromLootTableCD>(p).lootTableID);
        if (em.HasBuffer<GivesConditionsWhenConsumedBuffer>(p))
        {
            var b = em.GetBuffer<GivesConditionsWhenConsumedBuffer>(p, true);
            o.Append(",\"eat\":[");
            for (int i = 0; i < b.Length; i++)
            {
                var c = b[i].conditionDataContainer.conditionData;
                if (i > 0) o.Append(',');
                o.Append('[').Append((int)c.conditionID).Append(',').Append(c.value).Append(',').Append(F(c.duration)).Append(']');
            }
            o.Append(']');
        }
        if (em.HasBuffer<GivesConditionsWhenEquippedBuffer>(p))
        {
            var b = em.GetBuffer<GivesConditionsWhenEquippedBuffer>(p, true);
            o.Append(",\"equip\":[");
            for (int i = 0; i < b.Length; i++)
            {
                var c = b[i].equipmentCondition;
                if (i > 0) o.Append(',');
                o.Append('[').Append((int)c.id).Append(',').Append(c.value).Append(']');
            }
            o.Append(']');
        }
    }

    // Weapon damage and creature attacks. The base prefab has no WeaponDamageCD: like the item tooltip
    // (SlotUIBase), the damage is on the level entity (AIGameData.LevelEntity). Written: wdmg (default level), lv,
    // wdmgLv (damage per level index), wRange/wMagic (HasWeaponDamageCD), rangeWeapon (RangeWeaponCD), cd (CooldownCD;
    // missing = the tooltip's default 0.6 s ranged / 0.4 s otherwise, left to the graph). Creatures: atk {melee, touch,
    // touchCd, jump, rangeProj, rangeCdMin, rangeCdMax} and speed, from the prefab or else its level entity.
    private static void AppendCombat(EntityManager em, Entity p, int variation, StringBuilder o)
    {
        bool weapon = em.HasComponent<HasWeaponDamageCD>(p);
        if (weapon)
        {
            var h = em.GetComponentData<HasWeaponDamageCD>(p);
            o.Append(",\"wRange\":").Append(h.isRange ? "true" : "false").Append(",\"wMagic\":").Append(h.isMagic ? "true" : "false");
        }
        if (em.HasComponent<RangeWeaponCD>(p)) o.Append(",\"rangeWeapon\":true");
        if (em.HasComponent<CooldownCD>(p)) o.Append(",\"cd\":").Append(F(em.GetComponentData<CooldownCD>(p).cooldown));
        Entity lv = AIGameData.LevelEntity(em, p, variation, out int level);
        if (em.HasComponent<WeaponDamageCD>(p)) o.Append(",\"wdmg\":").Append(em.GetComponentData<WeaponDamageCD>(p).damage);
        else if (lv != Entity.Null && em.HasComponent<WeaponDamageCD>(lv)) o.Append(",\"wdmg\":").Append(em.GetComponentData<WeaponDamageCD>(lv).damage);
        if (level >= 0) o.Append(",\"lv\":").Append(level);
        if (weapon && em.HasBuffer<LevelEntitiesBuffer>(p))
        {
            var b = em.GetBuffer<LevelEntitiesBuffer>(p, true);
            var dm = new StringBuilder();
            bool any = false;
            for (int i = 0; i < b.Length; i++)
            {
                var e = b[i].entity;
                int d = e != Entity.Null && em.Exists(e) && em.HasComponent<WeaponDamageCD>(e) ? em.GetComponentData<WeaponDamageCD>(e).damage : 0;
                if (d > 0) any = true;
                if (i > 0) dm.Append(',');
                dm.Append(d);
            }
            if (any) o.Append(",\"wdmgLv\":[").Append(dm).Append(']');
        }
        var a = new StringBuilder();
        AppendAttack(em, p, a);
        if (a.Length == 0 && lv != Entity.Null) AppendAttack(em, lv, a);
        if (a.Length > 0) o.Append(",\"atk\":{").Append(a.ToString(1, a.Length - 1)).Append('}');
        if (em.HasComponent<MovementSpeedCD>(p)) o.Append(",\"speed\":").Append(F(em.GetComponentData<MovementSpeedCD>(p).speed));
        else if (lv != Entity.Null && em.HasComponent<MovementSpeedCD>(lv)) o.Append(",\"speed\":").Append(F(em.GetComponentData<MovementSpeedCD>(lv).speed));
    }

    // ",key:value" pairs (leading comma) for the attacks an entity has; nothing when it has none.
    private static void AppendAttack(EntityManager em, Entity e, StringBuilder a)
    {
        if (e == Entity.Null || !em.Exists(e)) return;
        if (em.HasComponent<MeleeAttackStateCD>(e)) a.Append(",\"melee\":").Append(em.GetComponentData<MeleeAttackStateCD>(e).meleeDamage);
        if (em.HasComponent<TouchAttackCD>(e)) { var t = em.GetComponentData<TouchAttackCD>(e); a.Append(",\"touch\":").Append(t.damage).Append(",\"touchCd\":").Append(F(t.cooldown)); }
        if (em.HasComponent<JumpAttackStateCD>(e)) a.Append(",\"jump\":").Append(em.GetComponentData<JumpAttackStateCD>(e).jumpDamage);
        if (em.HasComponent<RangeAttackStateCD>(e))
        {
            var r = em.GetComponentData<RangeAttackStateCD>(e);
            a.Append(",\"rangeProj\":").Append((int)r.projectileID).Append(",\"rangeCdMin\":").Append(F(r.minCooldown)).Append(",\"rangeCdMax\":").Append(F(r.maxCooldown));
        }
    }

    // Display name in a given language through the game's localization ("Items/<ObjectID>"); "" when missing.
    private static string Name(ObjectID id, string language)
    {
        try
        {
            string t = I2.Loc.LocalizationManager.GetTranslation("Items/" + id, overrideLanguage: language);
            return string.IsNullOrEmpty(t) ? "" : t;
        }
        catch { return ""; }
    }

    private static string F(float f) => float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);   // JSON has no Infinity (e.g. a condition with no time limit)
    private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
}

// Game data shared by the knowledge dump and combat (AIVitals.WeaponData): an item's level entity, which holds the
// stats that change with the item level (weapon damage). Same rule as the game's EntityUtility.GetLevelEntity /
// the item tooltip (SlotUIBase): level = variation when > 0 (capped at the max level), else the prefab's LevelCD.level;
// entity = the prefab's LevelEntitiesBuffer[level]. Works on any world's EntityManager (the AI runs on the server).
public static class AIGameData
{
    public static Entity PrimaryPrefab(EntityManager em, ObjectID id, int variation)
    {
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
        {
            if (q.CalculateEntityCount() != 1) return Entity.Null;
            var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
            return PugDatabase.GetPrimaryPrefabEntity(id, bank, variation);
        }
    }

    public static Entity LevelEntity(EntityManager em, Entity prefab, int variation, out int level)
    {
        level = -1;
        if (prefab == Entity.Null || !em.Exists(prefab) || !em.HasBuffer<LevelEntitiesBuffer>(prefab) || !em.HasComponent<LevelCD>(prefab)) return Entity.Null;
        var b = em.GetBuffer<LevelEntitiesBuffer>(prefab, true);
        int max = b.Length - 1;
        try { max = Math.Min(max, LevelScaling.GetMaxLevel()); } catch { }
        int idx = variation > 0 ? Math.Min(max, variation) : em.GetComponentData<LevelCD>(prefab).level;
        if (idx < 0 || idx >= b.Length) return Entity.Null;
        var e = b[idx].entity;
        if (e == Entity.Null || !em.Exists(e)) return Entity.Null;
        level = idx;
        return e;
    }

    // Weapon damage as the tooltip shows it (before the ±10% range): the prefab's WeaponDamageCD if it has one,
    // otherwise the level entity's. level = the level index used (-1 when no level entity).
    public static bool WeaponDamage(EntityManager em, Entity prefab, int variation, out int damage, out int level)
    {
        damage = 0; level = -1;
        if (prefab == Entity.Null || !em.Exists(prefab)) return false;
        if (em.HasComponent<WeaponDamageCD>(prefab)) damage = em.GetComponentData<WeaponDamageCD>(prefab).damage;
        var lv = LevelEntity(em, prefab, variation, out level);
        if (damage <= 0 && lv != Entity.Null && em.HasComponent<WeaponDamageCD>(lv)) damage = em.GetComponentData<WeaponDamageCD>(lv).damage;
        return damage > 0;
    }
}
