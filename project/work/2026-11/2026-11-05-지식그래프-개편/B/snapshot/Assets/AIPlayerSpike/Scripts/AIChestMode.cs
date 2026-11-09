// 상자 열림 모드 (사용자 결정 2026-11-08; session 코어키퍼-AI-캐릭터, work/2026-11/2026-11-08-선택지누락점검/agreement.md
// '상자 열림 모드'): while the AI has a chest open, the decision request offers only that chest's work (AIInventoryMenu
// .ChestModeCandidates) and closing it. The mode ends when the AI closes the chest, when the open state is no longer real
// (the chest is gone, out of view or out of reach: AIInventory.ValidOpenChest) and when a fight starts.
//
// Fight start (agreement '전투 상태 진입 기준'): only a signal the existing code already treats as a real fight ends the
// mode, never "an enemy noticed me" alone, and no escape judgment is made here. The signal used is the existing hit
// trigger: health lost since the mode began reaching the brain's own hp-drop trigger (AIBrain.Loop premise watch:
// AIVitals.LostSince(hp, now) >= Settings().hpDropTrigger, the "체력 감소" call reason). This one method is the place a later
// combat mode's entry point replaces.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static class AIChestMode
{
    private static readonly Dictionary<AIAgent, int> Peak = new Dictionary<AIAgent, int>();
    public static string LastEnded { get; private set; } = "";
    // Merge review G-1: an open chest is an open choice view like a menu; the no-goal idle gate (AIRunGate.IdleHold)
    // lets the decision go on until the chest is closed (a final take/store clears the menu path, not the open chest).
    public static bool Open => AIInventory.ChestIsOpen;

    // True while the mode holds; `chest` is the open chest. `lostSince(peak, now)` is the existing hit measure.
    public static bool Active(World world, Entity player, int health, int hitDrop, Func<int, int, int> lostSince, out Entity chest)
    {
        var who = AIAgent.Cur;
        chest = AIInventory.ValidOpenChest(world, player, out string released);
        if (chest == Entity.Null)
        {
            if (released != null) End(released);
            AIInventoryMenu.LeaveChestMode(released);
            Peak.Remove(who); return false;
        }
        if (!Peak.TryGetValue(who, out int peak) || health > peak) Peak[who] = peak = health;
        if (FightStarted(peak, health, hitDrop, lostSince))
        {
            AIInventory.CloseChest(); AIInventoryMenu.Reset("피격: 상자 열림 모드를 끝내고 상자를 닫음");
            Peak.Remove(who); End($"피격(체력 {peak}→{health})으로 상자 열림 모드를 끝내고 상자를 닫음; 전투 쪽 선택지로 돌아감");
            chest = Entity.Null; return false;
        }
        return true;
    }
    // The fight-start check (see the header): a later combat mode's entry point goes here.
    private static bool FightStarted(int peak, int health, int hitDrop, Func<int, int, int> lostSince) => hitDrop > 0 && lostSince(peak, health) >= hitDrop;
    private static void End(string why) { LastEnded = why; AIPlayerSpikeMod.Log("CHEST MODE: " + why); }
    public static void Reset() { Peak.Remove(AIAgent.Cur); LastEnded = ""; }
}
