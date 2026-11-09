using System;
using Unity.Entities;

// A detached view of the selected member. No planner, storage, network or model calls.
internal sealed class AIInfoSnapshot
{
    internal string Status = "미접속";
    internal string[] Skills = Array.Empty<string>();
    internal string[] Equipment = Array.Empty<string>();
    private static readonly string[] SkillNames = { "채광", "달리기", "근접", "활력", "제작", "원거리", "원예", "낚시", "요리", "마법", "소환", "폭발" };

    internal static AIInfoSnapshot Read(World server, AIAgent member)
    {
        var view = new AIInfoSnapshot();
        if (server == null || !server.IsCreated || !AIPlayerSpikeMod.IsJoined(member)) return view;
        var em = server.EntityManager;
        var player = AIPlayerSpikeMod.ServerPlayerOf(member);
        if (player == Entity.Null || !em.Exists(player)) return view;
        view.Status = "접속 중 · 읽기 전용";
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
}
