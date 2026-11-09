// Feature entry points for a goal. Algorithms and persistent state stay with their owners.
using System;
using System.Collections.Generic;
using Unity.Entities;

public static class AIGoalFeatures
{
    public static void AddCandidates(World server, Entity player, AIObservation o, string curKey, List<AICandidate> dest)
    {
        // base and chests (생활-기지): go home, store into a base chest, place a chest, take missing materials out
        if (AIGoals.Goal.kind != GoalKind.None && (!AIInventory.OwnsGoal(AIGoals.Goal) || AIGoals.Goal.kind == GoalKind.Store))
        {
            try
            {
                var baseOptions = AIBase.Candidates(server, player, o, curKey);
                if (AIInventory.OwnsGoal(AIGoals.Goal)) baseOptions.RemoveAll(c => c.kind == "store"); // keep chest preparation/home routes; actual moves are exact quantity choices
                dest.AddRange(baseOptions);
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("brain: base candidates failed", e); }
        }
        // Concrete inventory operations are selected through AIInventoryMenu at the public choice surface. Keep base
        // preparation routes here, but do not also publish the old per-item flat inventory list beside that entry.
        // The approved life consumer may execute a fully specified storage request directly. Other per-item
        // inventory operations are offered by the category/item menu, rather than restoring the old flat list.
        var inventoryGoal = AIGoals.Goal;
        if ((inventoryGoal.kind == GoalKind.Store || inventoryGoal.kind == GoalKind.Take) && inventoryGoal.slot >= 0 &&
            !string.IsNullOrEmpty(inventoryGoal.where) && inventoryGoal.where != "base")
        {
            try { dest.AddRange(AIInventory.Candidates(server, player, o, curKey)); }
            catch (Exception e) { AIPlayerSpikeMod.LogError("brain: exact life storage candidates failed", e); }
        }
        // own field (먹거리 자급 2): dig plots, plant, water / fill the can, harvest, seeds from an own chest or wild plants
        if (AIGoals.Goal.kind == GoalKind.Farm) { try { dest.AddRange(AIFarm.Candidates(server, player, o, curKey)); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: farm candidates failed", e); } }
        // own cooking pot (먹거리 자급 3): place one, put a better pair in, wait, take the dishes out
        if (AIGoals.Goal.kind == GoalKind.Cook) { try { dest.AddRange(AICook.Candidates(server, player, o, curKey)); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: cook candidates failed", e); } }
        if (AIGoals.Goal.kind == GoalKind.Maintenance)
        {
            try { var candidate = AIMaintenance.ExactCandidate(server, player, o, curKey, AILife.ForGoal(AIGoals.Goal)); if (candidate != null) dest.Add(candidate); }
            catch (Exception e) { AIPlayerSpikeMod.LogError("brain: maintenance candidate failed", e); }
        }
        // goto / place goals (제작대-놓기): walk to the place, the place options there, a rect / line in order
        if (AIGoals.Goal.kind == GoalKind.Goto || AIGoals.Goal.kind == GoalKind.Place) { try { dest.AddRange(AIPlaceGoal.Candidates(server, player, o, curKey)); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: place goal candidates failed", e); } }

    }

    public static bool TryDone(World server, Entity player, GoalKind kind, out string note)
    {
        if (AIInventory.OwnsGoal(AIGoals.Goal)) return AIInventory.GoalDone(server, player, out note);
        switch (AIGoalKinds.Of(kind)?.Feature)
        {
            case GoalFeature.Base: return AIBase.GoalDone(server, player, out note);
            case GoalFeature.Farm: return AIFarm.GoalDone(server, player, out note);
            case GoalFeature.Cook: return AICook.GoalDone(server, player, out note);
            case GoalFeature.Place: return AIPlaceGoal.GoalDone(server, player, out note);
            case GoalFeature.Inventory: return AIInventory.GoalDone(server, player, out note);
            case GoalFeature.Maintenance: return AIMaintenance.GoalDone(server, player, out note);
            default: note = ""; return false;
        }
    }

    // Facts added to a rejected step completion (2026-11-08 이동·전달 개선 4/4): for an inventory goal, where its item
    // is and how that relates to the open bag menu. Choice stays with the action model.
    public static string CompletionGuidance(World server, Entity player, AIGoal goal)
        => AIInventory.OwnsGoal(goal) ? AIInventoryMenu.GoalRelation(server, player, goal) : "";

    public static string StallNote(World server, Entity player, GoalKind kind)
    {
        if (AIInventory.OwnsGoal(AIGoals.Goal)) return AIInventory.StallNote(server, player);
        if (kind == GoalKind.Maintenance) return AIMaintenance.StallNote(server, player);
        return kind == GoalKind.Goto ? $"갈 방법이 없음 — {AIPlaceGoal.StallNote(server, player)}"
                : kind == GoalKind.Place ? $"놓을 방법이 없음 — {AIPlaceGoal.StallNote(server, player)}"
                : kind == GoalKind.Farm ? $"밭 일을 할 방법이 없음 — {AIFarm.StallNote(server, player)}"
                : kind == GoalKind.Cook ? $"요리할 방법이 없음 — {AICook.StallNote(server, player)}"
                : $"{(kind == GoalKind.Home ? "기지로 갈 방법이 없음" : "넣을 방법이 없음")} — {AIBase.StallNote(server, player)}";

    }
}
