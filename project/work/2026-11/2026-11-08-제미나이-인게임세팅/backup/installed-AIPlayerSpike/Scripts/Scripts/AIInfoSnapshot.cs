using System;
using Unity.Collections;
using Unity.Entities;

// A detached view of the selected member. No planner, storage, network or model calls.
internal sealed class AIInfoSnapshot
{
    internal string Status = "미접속";
    internal string Diagnostics = "";
    internal string[] Skills = Array.Empty<string>();
    internal string[] Equipment = Array.Empty<string>();
    private static readonly string[] SkillNames = { "채광", "달리기", "근접", "활력", "제작", "원거리", "원예", "낚시", "요리", "마법", "소환", "폭발" };

    internal static AIInfoSnapshot Read(World server, AIAgent member)
    {
        var view = new AIInfoSnapshot();
        view.Diagnostics = "member=" + member.Id + "; world=" + (server != null && server.IsCreated ? server.Name : "unavailable");
        if (server == null || !server.IsCreated || !AIPlayerSpikeMod.IsJoined(member)) return view;
        var em = server.EntityManager;
        var player = AIPlayerSpikeMod.ServerPlayerOf(member);
        view.Diagnostics += "; entity=" + player;
        if (player == Entity.Null || !em.Exists(player)) return view;
        view.Status = "접속 중 · 읽기 전용";
        if (em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var inventory = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            view.Diagnostics += "; inventoryLength=" + inventory.Length;
            view.Diagnostics += "; currentEquipment=" + (em.HasComponent<EquipmentCD>(player) ? DescribeEquipment(em.GetComponentData<EquipmentCD>(player), inventory) : "missing");
            view.Diagnostics += "; serverVanity=" + DescribeVanity(em, player, inventory);
            int activeIndex = em.HasComponent<ActiveEquipmentPresetCD>(player) ? em.GetComponentData<ActiveEquipmentPresetCD>(player).Value : -1;
            view.Diagnostics += "; activePreset=" + activeIndex;
            if (em.HasBuffer<EquipmentPresetsBuffer>(player))
            {
                var equipmentPresets = em.GetBuffer<EquipmentPresetsBuffer>(player, true);
                view.Diagnostics += "; presetCount=" + equipmentPresets.Length + "; selectedPreset=" +
                    (activeIndex >= 0 && activeIndex < equipmentPresets.Length ? DescribeEquipment(equipmentPresets[activeIndex].equipment, inventory) : "out-of-range");
            }
            else view.Diagnostics += "; presets=missing";
            if (em.HasComponent<EquippedObjectCD>(player))
            {
                var heldObject = em.GetComponentData<EquippedObjectCD>(player);
                view.Diagnostics += "; heldSlot=" + DescribeSlot(heldObject.equippedSlotIndex, inventory) + "; heldContained=" + heldObject.containedObject.objectID + "x" + heldObject.containedObject.amount;
            }
        }
        else view.Diagnostics += "; inventory=missing";
        try { view.Diagnostics += DescribeClient(em, player); }
        catch (Exception e) { view.Diagnostics += "; clientDiagnosticUnavailable=" + e.GetType().Name; }
        if (em.HasBuffer<SkillBuffer>(player))
        {
            var skills = em.GetBuffer<SkillBuffer>(player, true);
            view.Skills = new string[Math.Min(skills.Length, SkillNames.Length)];
            for (int i = 0; i < view.Skills.Length; i++)
                view.Skills[i] = SkillNames[i] + "  Lv." + SkillExtensions.GetLevelFromSkill((SkillID)i, skills[i].Value);
        }
        if (em.HasBuffer<ContainedObjectsBuffer>(player) && em.HasBuffer<EquipmentPresetsBuffer>(player) && em.HasComponent<ActiveEquipmentPresetCD>(player))
        {
            var items = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            var presets = em.GetBuffer<EquipmentPresetsBuffer>(player, true);
            int active = em.GetComponentData<ActiveEquipmentPresetCD>(player).Value;
            if (active >= 0 && active < presets.Length)
            {
                var equipment = presets[active].equipment;
                int held = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player).equippedSlotIndex : -1;
                int[] slots = { held, equipment.helmSlotIndex, equipment.breastSlotIndex, equipment.pantsSlotIndex, equipment.necklaceSlotIndex, equipment.ring1SlotIndex, equipment.ring2SlotIndex, equipment.offHandIndex, equipment.bagIndex, equipment.lanternIndex };
                string[] labels = { "손", "머리", "상의", "하의", "목걸이", "반지 1", "반지 2", "보조 손", "가방", "랜턴" };
                view.Equipment = new string[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    int index = slots[i];
                    string name = "없음";
                    if (index >= 0 && index < items.Length && items[index].objectData.objectID != ObjectID.None)
                    {
                        var id = items[index].objectData.objectID;
                        name = I2.Loc.LocalizationManager.GetTranslation("Items/" + id, overrideLanguage: "Korean");
                        if (string.IsNullOrEmpty(name)) name = id.ToString();
                    }
                    view.Equipment[i] = labels[i] + "  " + name;
                }
            }
        }
        return view;
    }

    private static string DescribeSlot(int index, DynamicBuffer<ContainedObjectsBuffer> items) => index + ":" +
        (index >= 0 && index < items.Length ? items[index].objectData.objectID + "x" + items[index].objectData.amount : "out-of-range");

    private static string DescribeEquipment(EquipmentCD equipment, DynamicBuffer<ContainedObjectsBuffer> items) =>
        "{head=" + DescribeSlot(equipment.helmSlotIndex, items) + ",chest=" + DescribeSlot(equipment.breastSlotIndex, items) +
        ",legs=" + DescribeSlot(equipment.pantsSlotIndex, items) + ",neck=" + DescribeSlot(equipment.necklaceSlotIndex, items) +
        ",ring1=" + DescribeSlot(equipment.ring1SlotIndex, items) + ",ring2=" + DescribeSlot(equipment.ring2SlotIndex, items) +
        ",offhand=" + DescribeSlot(equipment.offHandIndex, items) + ",bag=" + DescribeSlot(equipment.bagIndex, items) +
        ",lantern=" + DescribeSlot(equipment.lanternIndex, items) + "}";

    private static string DescribeVanity(EntityManager em, Entity entity, DynamicBuffer<ContainedObjectsBuffer> items)
    {
        if (!em.HasComponent<VanitySlotsCD>(entity)) return "missing";
        var vanity = em.GetComponentData<VanitySlotsCD>(entity);
        return "{head=" + DescribeSlot(vanity.helmVanitySlotIndex, items) + ",chest=" + DescribeSlot(vanity.breastVanitySlotIndex, items) +
            ",legs=" + DescribeSlot(vanity.pantsVanitySlotIndex, items) + "}";
    }

    private static string DescribeClient(EntityManager server, Entity player)
    {
        var client = Manager.ecs != null ? Manager.ecs.ClientWorld : null;
        if (client == null || !client.IsCreated || !server.HasComponent<PlayerGhost>(player)) return "; client=unavailable";
        var guid = server.GetComponentData<PlayerGhost>(player).playerGuid;
        var em = client.EntityManager;
        Entity matched = Entity.Null;
        using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>()))
        using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var candidate in entities)
                if (em.GetComponentData<PlayerGhost>(candidate).playerGuid.Equals(guid)) { matched = candidate; break; }
        string result = "; guid=" + guid + "; clientWorld=" + client.Name + "; clientEntity=" + matched;
        if (matched == Entity.Null || !em.HasBuffer<ContainedObjectsBuffer>(matched)) return result + "; clientInventory=unavailable";
        var items = em.GetBuffer<ContainedObjectsBuffer>(matched, true);
        result += "; clientInventoryLength=" + items.Length + "; clientCurrent=" +
            (em.HasComponent<EquipmentCD>(matched) ? DescribeEquipment(em.GetComponentData<EquipmentCD>(matched), items) : "missing");
        int active = em.HasComponent<ActiveEquipmentPresetCD>(matched) ? em.GetComponentData<ActiveEquipmentPresetCD>(matched).Value : -1;
        result += "; clientActive=" + active;
        if (em.HasBuffer<EquipmentPresetsBuffer>(matched))
        {
            var presets = em.GetBuffer<EquipmentPresetsBuffer>(matched, true);
            result += "; clientPresetCount=" + presets.Length + "; clientPreset=" +
                (active >= 0 && active < presets.Length ? DescribeEquipment(presets[active].equipment, items) : "out-of-range");
        }
        result += "; clientVanity=" + DescribeVanity(em, matched, items);
        if (Manager.main != null)
            foreach (var controller in Manager.main.allPlayers)
            {
                if (controller == null || controller.world != client || controller.entity != matched) continue;
                var gear = controller.equipmentHandler;
                var vanity = controller.vanitySlotsHandler;
                if (gear != null) result += "; renderedEquipmentHandlers={head=" + DescribeObject(gear.helmInventoryHandler.GetObjectData(0)) +
                    ",chest=" + DescribeObject(gear.breastInventoryHandler.GetObjectData(0)) + ",legs=" + DescribeObject(gear.pantsInventoryHandler.GetObjectData(0)) + "}";
                if (vanity != null) result += "; renderedVanityHandlers={head=" + DescribeObject(vanity.helmVanitySlotInventoryHandler.GetObjectData(0)) +
                    ",chest=" + DescribeObject(vanity.breastVanitySlotInventoryHandler.GetObjectData(0)) + ",legs=" + DescribeObject(vanity.pantsVanitySlotInventoryHandler.GetObjectData(0)) + "}";
                var appearance = controller.activeCustomization;
                result += "; renderedArmorAddresses={head=" + UnityEngine.JsonUtility.ToJson(appearance.helm) +
                    ",chest=" + UnityEngine.JsonUtility.ToJson(appearance.breastArmor) + ",legs=" + UnityEngine.JsonUtility.ToJson(appearance.pantsArmor) + "}";
                return result;
            }
        return result + "; renderedController=unavailable";
    }

    private static string DescribeObject(ObjectDataCD item) => item.objectID + "x" + item.amount;
}
