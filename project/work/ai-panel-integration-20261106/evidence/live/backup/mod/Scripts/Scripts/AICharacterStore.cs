// AICharacterStore — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pug.Properties;
using PugMod;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Transforms;
using UnityEngine;

// The AI's own character data: file I/O, new-character creation, and saving from the server player entity.
public static class AICharacterStore
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public CharacterData _current;
        public string _path;
        public bool _weaponChecked;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.CharacterStore);
    private static State S => StateOf(AIAgent.Cur);

    private static CharacterData _current { get => S._current; set => S._current = value; }
    private static string _path { get => S._path; set => S._path = value; }

    // Per world: <persistentDataPath>/AIPlayers/<world ServerGuid>/<name>.json. The world's ServerGuid is generated once
    // per world and saved inside it, so it survives reloads and slot reordering and differs between worlds.
    // The legacy single file AIPlayers/<name>.json (before per-world characters) is left untouched and not used.
    public static string FilePath(string aiName, string worldId)
    {
        return Path.Combine(Application.persistentDataPath, "AIPlayers", worldId, aiName + ".json");
    }

    // Same as the game's SaveManager.GetCharacterDataFromSerialized, without going through the patched method.
    private static CharacterData Decode(string json)
    {
        var cd = new CharacterData();
        JsonUtility.FromJsonOverwrite(json, cd);
        cd.UpgradeIfOutdated();
        return cd;
    }

    private static void Write(string path, CharacterData cd)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string json = JsonUtility.ToJson(cd, true);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public static bool Prepare(string aiName, string aiGuid, string worldId)
    {
        string path = FilePath(aiName, worldId);
        try
        {
            string legacy = Path.Combine(Application.persistentDataPath, "AIPlayers", aiName + ".json");
            if (File.Exists(legacy))
            {
                AIPlayerSpikeMod.Log($"AI character: legacy global file {legacy} exists; ignored (characters are per world now)");
            }
            if (File.Exists(path))
            {
                _current = Decode(File.ReadAllText(path, Encoding.UTF8));
                _path = path;
                AIPlayerSpikeMod.Log($"AI character: loaded '{aiName}' from {path} ({Summary(_current)})");
            }
            else
            {
                _current = CreateNewCharacter(aiName, aiGuid);
                _path = path;
                Write(path, _current);
                AIPlayerSpikeMod.Log($"AI character: created NEW character '{aiName}' at {path} ({Summary(_current)})");
            }
            return true;
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"AI character: prepare '{aiName}' ({path}) failed", e);
            _current = null;
            return false;
        }
    }

    // A fresh object for the server to consume (the server mutates what it receives).
    public static CharacterData CreateServerCopy()
    {
        if (_current == null) return null;
        return Decode(JsonUtility.ToJson(_current));
    }

    // Mirrors SaveManager._ClearCharacter (new-character defaults) plus what PlayerController hands out on a
    // character's first connect (role starter skill at level 3 except Nomad, role starter items from slot 1).
    // DLC/app extra start loot and name easter eggs are not given.
    private static CharacterData CreateNewCharacter(string aiName, string aiGuid)
    {
        var cd = new CharacterData();
        cd.version = CharacterData.CURRENT_VERSION;
        cd.characterGuid = aiGuid;
        cd.maxHealth = 100;
        cd.health = 0;
        cd.coinAmount = 0;
        cd.serverConnectCount = 0;
        cd.hasUnlockedSouls = false;
        cd.hasPlayedOutro = false;

        PlayerCustomizationTableDataBlock table = null;
        var cust = Manager.saves.playerCustomizationTable.TryGet(out table) ? new PlayerCustomization(table) : default(PlayerCustomization);
        if (table != null)
        {
            cust = DistinctLook(table, Manager.saves.GetCharacterCustomization(), cust);
        }
        cust.name = new FixedString32Bytes(aiName);
        cust.helm = DataBlockAddress.Empty;
        cust.breastArmor = DataBlockAddress.Empty;
        cust.pantsArmor = DataBlockAddress.Empty;

        RolePerksTable perksTable = Manager.main != null && Manager.main.player != null ? Manager.main.player.rolePerksTable : null;
        // The new-character menu resets the role to index 0 of the perks table.
        CharacterRole role = perksTable != null && perksTable.perks != null && perksTable.perks.Count > 0 ? perksTable.GetRole(0) : (CharacterRole)0;
        cust.role = (byte)role;
        cd.CharacterCustomization = cust;

        var given = new List<string>();
        if (perksTable != null)
        {
            RolePerksTable.Perks perks = perksTable.GetPerks(role);
            if ((byte)role != 6) // Nomad gets no starter skill
            {
                cd.skills.Add(new SkillData(perks.starterSkill, SkillExtensions.GetSkillFromLevel(perks.starterSkill, 3)));
                given.Add($"skill {perks.starterSkill} lv3");
            }
            int slot = 1;
            if (perks.starterItems != null)
            {
                foreach (ObjectData item in perks.starterItems)
                {
                    slot = PutStarterItem(cd, slot, item.objectID, item.amount, item.variation, given);
                }
            }
            // User decision 2026-11-04: the AI always carries a basic weapon (an early craftable melee weapon chosen from
            // game data, see ChooseStarterWeapon).
            ObjectID w = ChooseStarterWeapon(out string wWhy);
            if (w != ObjectID.None) slot = PutStarterItem(cd, slot, w, 1, 0, given);
            AIPlayerSpikeMod.Log($"AI character: starter weapon {w} ({wWhy})");
            cd.serverConnectCount = 1; // the first-connect kit has been handed out
        }
        else
        {
            AIPlayerSpikeMod.LogError("AI character: role perks table not available; new character has no starter kit");
        }
        cd.UpgradeIfOutdated();
        AIPlayerSpikeMod.Log($"AI character: new-character role={role} starter=[{string.Join(", ", given)}] look=distinct from host '{Manager.saves.GetCharacterCustomization().name}'");
        return cd;
    }

    // Like InventoryUtility.CreateObject: stackables keep their amount; non-stackables get one item per unit
    // with amount = initialAmount (durability).
    private static int PutStarterItem(CharacterData cd, int slot, ObjectID id, int amount, int variation, List<string> given)
    {
        if (id == ObjectID.None || amount <= 0) return slot;
        ObjectInfo info = PugDatabase.GetObjectInfo(id, variation);
        bool stackable = info == null || info.isStackable;
        int units = stackable ? 1 : amount;
        for (int u = 0; u < units; u++)
        {
            int a = stackable ? amount : (info != null ? info.initialAmount : 1);
            SetSlot(cd, slot, new ObjectDataCD { objectID = id, amount = a, variation = variation });
            given.Add($"slot{slot}={id}x{a}");
            slot++;
        }
        return slot;
    }

    private static void SetSlot(CharacterData cd, int slot, ObjectDataCD data)
    {
        while (cd.inventory.Count <= slot) cd.inventory.Add(default(ObjectDataCD));
        while (cd.inventoryObjectNames.Count <= slot) cd.inventoryObjectNames.Add(null);
        while (cd.inventoryAuxData.Count <= slot) cd.inventoryAuxData.Add(default(CharacterInventoryAuxData));
        cd.inventory[slot] = data;
    }

    private static int IndexOf<T>(DataBlockCollection<T> c, DataBlockAddress a) where T : ScriptableDataBlock
    {
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] != null && c[i].address == a) return i;
        }
        return -1;
    }

    // Deterministic option clearly different from the host's and from the other AI characters' (multi-AI).
    private static int Away(int hostIndex, int count)
    {
        if (count <= 1) return 0;
        int h = math.max(hostIndex, 0);
        // half-way around the list for Claude (as before); each other member a further step on, never the host's own
        int i = (h + math.max(1, count / 2) + AIAgent.Cur.Index * math.max(1, count / 5)) % count;
        return i == h ? (i + 1) % count : i;
    }

    private static PlayerCustomization DistinctLook(PlayerCustomizationTableDataBlock t, PlayerCustomization host, PlayerCustomization c)
    {
        var bodies = t.bodySkinCollection.Get();
        var eyes = t.eyeSkinCollection.Get();
        var shirts = t.shirtSkinCollection.Get();
        var pants = t.pantsSkinCollection.Get();
        var hairs = t.hairSkinCollection.Get();
        var hairColors = t.hairReplacementColors.Get();
        var hairShades = t.hairShadeReplacementColors.Get();
        var skins = t.skinReplacementColors.Get();
        var eyeColors = t.eyeReplacementColors.Get();

        int b = Away(IndexOf(bodies, host.body), bodies.Count);
        c.body = bodies[b].address;
        if (b < eyes.Count) c.eyes = eyes[b].address;
        if (b < shirts.Count)
        {
            c.shirtSkin = shirts[b].address;
            var sc = shirts[b].replacementColorsCollectionRef.Get();
            if (sc != null && sc.Count > 0) c.shirtColor = sc[Away(IndexOf(sc, host.shirtColor), sc.Count)].address;
        }
        if (b < pants.Count)
        {
            c.pantsSkin = pants[b].address;
            var pc = pants[b].replacementColorsCollectionRef.Get();
            if (pc != null && pc.Count > 0) c.pantsColor = pc[Away(IndexOf(pc, host.pantsColor), pc.Count)].address;
        }
        if (skins.Count > 0)
        {
            int s = Away(IndexOf(skins, host.skinColor), skins.Count);
            c.skinColor = skins[s].address;
            if (s < hairShades.Count) c.hairShadeColor = hairShades[s].address;
        }
        if (hairs.Count > 0) c.hair = hairs[Away(IndexOf(hairs, host.hair), hairs.Count)].address;
        if (hairColors.Count > 0) c.hairColor = hairColors[Away(IndexOf(hairColors, host.hairColor), hairColors.Count)].address;
        if (eyeColors.Count > 0) c.eyesColor = eyeColors[Away(IndexOf(eyeColors, host.eyesColor), eyeColors.Count)].address;
        return c;
    }

    public static string Summary(CharacterData cd)
    {
        if (cd == null) return "null";
        var items = new List<string>();
        for (int i = 0; i < cd.inventory.Count; i++)
        {
            if (cd.inventory[i].objectID != ObjectID.None) items.Add($"{i}:{cd.inventory[i].objectID}x{cd.inventory[i].amount}");
        }
        return $"guid={cd.characterGuid} role={(CharacterRole)cd.CharacterCustomization.role} items=[{string.Join(", ", items)}] skills={cd.skills.Count} coins={cd.coinAmount} connects={cd.serverConnectCount}";
    }

    private static string BufferSummary(EntityManager em, Entity e)
    {
        if (!em.Exists(e) || !em.HasBuffer<ContainedObjectsBuffer>(e)) return "no inventory";
        var buf = em.GetBuffer<ContainedObjectsBuffer>(e);
        var items = new List<string>();
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID != ObjectID.None) items.Add($"{i}:{buf[i].objectID}x{buf[i].amount}");
        }
        return $"{items.Count} non-empty of {buf.Length} [{string.Join(", ", items)}]";
    }

    public static void LogInventories(World server, Entity aiPlayer)
    {
        try
        {
            var em = server.EntityManager;
            AIPlayerSpikeMod.Log($"AI character: server inventory of AI player {aiPlayer}: {BufferSummary(em, aiPlayer)}");
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>()))
            using (var ents = q.ToEntityArray(Allocator.Temp))
            {
                foreach (var e in ents)
                {
                    if (e == aiPlayer) continue;
                    var name = em.HasComponent<PlayerCustomizationCD>(e) ? em.GetComponentData<PlayerCustomizationCD>(e).customization.name.ToString() : "?";
                    AIPlayerSpikeMod.Log($"AI character: server inventory of other player '{name}' {e}: {BufferSummary(em, e)}");
                }
            }
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("AI character: inventory log failed", e);
        }
    }

    // Reads the AI player's state from the server entity (what PlayerController/SaveSkillsSystem would save for a
    // real client) and writes the AI's file. Talents, discoveries, biomes, tutorials are kept from the file.
    public static void SaveFromServer(World server, Entity player, string reason)
    {
        if (_current == null || _path == null) return;
        if (server == null || !server.IsCreated || player == Entity.Null)
        {
            AIPlayerSpikeMod.Log($"AI character: save skipped ({reason}): server world or AI player not available");
            return;
        }
        var em = server.EntityManager;
        if (!em.Exists(player))
        {
            AIPlayerSpikeMod.Log($"AI character: save skipped ({reason}): AI player entity no longer exists");
            return;
        }
        var cd = _current;

        if (em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var items = em.GetBuffer<ContainedObjectsBuffer>(player).ToNativeArray(Allocator.Temp);
            bool hasAux = false;
            InventoryAuxDataSystemDataCD aux = default;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<InventoryAuxDataSystemDataCD>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    aux = q.GetSingleton<InventoryAuxDataSystemDataCD>();
                    hasAux = true;
                }
            }
            int nameProperty = Property.StringToHash("name");
            cd.inventory.Clear();
            cd.inventoryObjectNames.Clear();
            cd.inventoryAuxData.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                cd.inventory.Add(items[i].objectData);
                string objName = null;
                try { API.Authoring.ObjectProperties.TryGetPropertyString(items[i].objectData.objectID, nameProperty, out objName); } catch { objName = null; }
                cd.inventoryObjectNames.Add(objName);
                if (items[i].auxDataIndex != 0 && hasAux)
                {
                    cd.inventoryAuxData.Add(new CharacterInventoryAuxData { index = items[i].auxDataIndex, data = aux.GetDataAsJson(em, items[i].auxDataIndex) });
                }
                else
                {
                    cd.inventoryAuxData.Add(default(CharacterInventoryAuxData));
                }
            }
            items.Dispose();
        }

        if (em.HasBuffer<SkillBuffer>(player))
        {
            var skills = em.GetBuffer<SkillBuffer>(player);
            cd.skills.Clear();
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].Value != 0) cd.skills.Add(new SkillData((SkillID)i, skills[i].Value));
            }
        }

        if (em.HasBuffer<ConditionsBuffer>(player))
        {
            NetworkTick tick = NetworkTick.Invalid;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>()))
            {
                if (q.CalculateEntityCount() == 1) tick = q.GetSingleton<NetworkTime>().ServerTick;
            }
            ClientServerTickRate rate = default;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ClientServerTickRate>()))
            {
                if (q.CalculateEntityCount() == 1) rate = q.GetSingleton<ClientServerTickRate>();
                else rate.ResolveDefaults();
            }
            if (tick.IsValid)
            {
                var conds = em.GetBuffer<ConditionsBuffer>(player);
                cd.conditionsList.Clear();
                for (int i = 0; i < conds.Length; i++)
                {
                    cd.conditionsList.Add(conds[i].condition.ToConditionSerialized(tick, (uint)rate.SimulationTickRate));
                }
            }
        }

        if (em.HasBuffer<LockedObjectsBuffer>(player))
        {
            var locked = em.GetBuffer<LockedObjectsBuffer>(player);
            cd.lockedObjects.Clear();
            for (int i = 0; i < locked.Length; i++) cd.lockedObjects.Add(locked[i].Value);
        }

        if (em.HasComponent<CoinAmountCD>(player)) cd.coinAmount = em.GetComponentData<CoinAmountCD>(player).Value;

        if (em.HasComponent<HealthCD>(player))
        {
            var h = em.GetComponentData<HealthCD>(player);
            cd.health = h.health;
            cd.maxHealth = em.HasBuffer<SummarizedConditionEffectsBuffer>(player)
                ? h.GetMaxHealthWithConditions(em.GetBuffer<SummarizedConditionEffectsBuffer>(player))
                : h.maxHealth;
        }

        if (em.HasBuffer<CollectedSoulsBuffer>(player))
        {
            var souls = em.GetBuffer<CollectedSoulsBuffer>(player);
            cd.collectedSouls.Clear();
            for (int i = 0; i < souls.Length; i++) cd.collectedSouls.Add(souls[i].soulId);
        }
        if (em.HasComponent<SoulsInfoCD>(player)) cd.hasUnlockedSouls = em.GetComponentData<SoulsInfoCD>(player).hasUnlockedSouls;

        // Keep lastActiveSession unset so every (re)connect reloads the inventory from this file.
        cd.lastActiveSession = default;

        try
        {
            Write(_path, cd);
            string pos = em.HasComponent<LocalTransform>(player) ? em.GetComponentData<LocalTransform>(player).Position.ToString() : "?";
            AIPlayerSpikeMod.Log($"AI character: saved ({reason}) to {_path}: {Summary(cd)} health={cd.health}/{cd.maxHealth} serverPos={pos} world={AIPlayerSpikeMod.WorldId}");
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"AI character: writing {_path} failed ({reason})", e);
        }
    }

    // The basic early weapon (user decision 2026-11-04: "in a real game Claude would always carry at least one
    // weapon"). Among the earliest swords, the melee weapon with a crafting recipe and the lowest level, then the
    // fewest crafting ingredients, from the game's object data.
    private static readonly ObjectID[] StarterWeaponCandidates = { ObjectID.WoodSword, ObjectID.CopperSword, ObjectID.TinSword };
    public static ObjectID ChooseStarterWeapon(out string why)
    {
        ObjectID best = ObjectID.None; int bestLevel = int.MaxValue, bestCost = int.MaxValue;
        var facts = new List<string>();
        foreach (var id in StarterWeaponCandidates)
        {
            ObjectInfo info = PugDatabase.GetObjectInfo(id);
            if (info == null) { facts.Add($"{id}: no data"); continue; }
            int cost = 0; var parts = new List<string>();
            if (info.requiredObjectsToCraft != null) foreach (var r in info.requiredObjectsToCraft) { cost += r.amount; parts.Add($"{r.objectID}x{r.amount}"); }
            facts.Add($"{id}: type={info.objectType} level={info.level} rarity={info.rarity} recipe=[{string.Join(" ", parts)}]");
            if (info.objectType != ObjectType.MeleeWeapon || parts.Count == 0) continue;
            if (info.level < bestLevel || (info.level == bestLevel && cost < bestCost)) { best = id; bestLevel = info.level; bestCost = cost; }
        }
        why = string.Join("; ", facts);
        return best;
    }

    // One-time grant for an AI character made before the starter weapon existed: if no melee weapon is anywhere in
    // the AI's inventory and this character was never granted one, put the starter weapon into the first empty slot
    // (server side, like the game's inventory insert). Marker: <character>.weapon-granted next to the character file.
    private static bool _weaponChecked { get => S._weaponChecked; set => S._weaponChecked = value; }
    public static void ResetWeaponCheck() { _weaponChecked = false; }
    public static void EnsureWeaponOnce(World server, Entity player)
    {
        if (_weaponChecked || _path == null) return;
        _weaponChecked = true;
        try
        {
            var em = server.EntityManager;
            if (!em.HasBuffer<ContainedObjectsBuffer>(player)) { _weaponChecked = false; return; }
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
            ObjectID w = ChooseStarterWeapon(out string why);
            AIPlayerSpikeMod.Log($"AI character: starter weapon choice {w} ({why}); craftable at WoodenWorkBench={CraftableAt(server, ObjectID.WoodenWorkBench, w)}");
            for (int i = 0; i < buf.Length; i++)
            {
                ObjectInfo info = buf[i].objectID == ObjectID.None ? null : PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
                if (info != null && info.objectType == ObjectType.MeleeWeapon) { AIPlayerSpikeMod.Log($"AI character: has a weapon already ({buf[i].objectID} in slot {i}); no grant"); return; }
            }
            string marker = Path.ChangeExtension(_path, ".weapon-granted");
            if (File.Exists(marker)) { AIPlayerSpikeMod.Log($"AI character: no weapon, but one was granted before ({File.ReadAllText(marker).Trim()}); not granting again"); return; }
            if (w == ObjectID.None) { AIPlayerSpikeMod.LogError("AI character: no starter weapon found in game data"); return; }
            ObjectInfo wi = PugDatabase.GetObjectInfo(w);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].objectID != ObjectID.None) continue;
                var s = buf[i];
                s.objectData = new ObjectDataCD { objectID = w, amount = wi != null ? wi.initialAmount : 1 };
                s.auxDataIndex = 0;
                buf[i] = s;
                File.WriteAllText(marker, $"{w} slot {i} {DateTime.Now:yyyy-MM-dd HH:mm}");
                AIPlayerSpikeMod.Log($"AI character: granted starter weapon {w} (amount {s.objectData.amount}) into slot {i} (one time; marker {marker})");
                AIPlayerSpikeMod.Reply($"AI: 기본 무기 {w}를 {AIAgent.Cur.Name} 가방 {i}번 칸에 넣었습니다(한 번만).", ChatKind.Status);
                return;
            }
            AIPlayerSpikeMod.LogError("AI character: no empty slot for the starter weapon");
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("AI character: weapon grant failed", e); }
    }

    // Does the crafting station's prefab list this object among what it can craft (CanCraftObjectsBuffer)?
    private static string CraftableAt(World server, ObjectID station, ObjectID item)
    {
        try
        {
            var em = server.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
            {
                if (q.CalculateEntityCount() != 1) return "?";
                var bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(station, bank, 0);
                if (prefab == Entity.Null || !em.HasBuffer<CanCraftObjectsBuffer>(prefab)) return "no list";
                var b = em.GetBuffer<CanCraftObjectsBuffer>(prefab, true);
                for (int i = 0; i < b.Length; i++) if (b[i].objectID == item) return "true";
                return "false";
            }
        }
        catch (Exception e) { return "error " + e.GetType().Name; }
    }

    // TEST ONLY (verification aid, not gameplay): if <AIPlayers>/<name>.testgive exists with "ObjectIDName amount",
    // put that item into the AI player's first empty slot on the server once, then delete the file.
    public static void TestOnlyGiveFromFile(World server, Entity player)
    {
        if (_path == null || !AIBody.TestCommandsOn) return;   // test-only, behind the AIPlayers/testcmds.on switch
        string give = Path.ChangeExtension(_path, ".testgive");
        if (!File.Exists(give)) return;
        try
        {
            string[] parts = File.ReadAllText(give).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            File.Delete(give);
            var id = (ObjectID)Enum.Parse(typeof(ObjectID), parts[0]);
            int amount = parts.Length > 1 ? int.Parse(parts[1]) : 1;
            ObjectInfo info = PugDatabase.GetObjectInfo(id);
            if (info != null && !info.isStackable) amount = info.initialAmount; // tools: amount = durability
            var em = server.EntityManager;
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
            for (int i = 1; i < buf.Length; i++)
            {
                if (buf[i].objectID != ObjectID.None) continue;
                var slot = buf[i];
                slot.objectData = new ObjectDataCD { objectID = id, amount = amount };
                slot.auxDataIndex = 0;
                buf[i] = slot;
                AIPlayerSpikeMod.Log($"AI character: TEST-ONLY server-side insert of {id}x{amount} into AI slot {i} (from {give})");
                return;
            }
            AIPlayerSpikeMod.LogError("AI character: TEST-ONLY insert found no empty slot");
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("AI character: TEST-ONLY insert failed", e);
        }
    }
}
