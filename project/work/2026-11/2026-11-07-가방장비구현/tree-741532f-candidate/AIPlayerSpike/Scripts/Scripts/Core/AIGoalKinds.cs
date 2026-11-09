// The shared vocabulary of goals. Execution and world checks remain in their feature modules.
using System;
using System.Collections.Generic;

public enum GoalKind { None, Gather, Explore, Follow, Craft, Store, Home, Farm, Cook, Free, Goto, Place, Equip, Unequip, Hold, Take, Drop, Give }
public enum GoalItemMode { None, Required, Optional }
public enum GoalCompletion { None, Inventory, Feature, FollowTimeout }
public enum GoalFeature { None, Base, Farm, Cook, Place, Inventory }
public enum GoalExperienceItem { None, Always, WhenPresent }

public sealed class AIGoalKindInfo
{
    public GoalKind Kind { get; }
    public string WireName { get; }
    public IReadOnlyList<string> HostWords { get; }
    public GoalItemMode ItemMode { get; }
    public int PlannerMaxCount { get; }
    public GoalCompletion Completion { get; }
    public GoalFeature Feature { get; }
    public bool NeedsProgress { get; }
    public GoalExperienceItem ExperienceItem { get; }

    internal AIGoalKindInfo(GoalKind kind, string wire, GoalItemMode item, int max,
        GoalCompletion completion, GoalFeature feature, bool progress, GoalExperienceItem experience, params string[] words)
    {
        Kind = kind; WireName = wire; ItemMode = item; PlannerMaxCount = max;
        Completion = completion; Feature = feature; NeedsProgress = progress; ExperienceItem = experience;
        HostWords = Array.AsReadOnly(words);
    }
}

public static class AIGoalKinds
{
    public static IReadOnlyList<AIGoalKindInfo> All { get; } = Array.AsReadOnly(new[]
    {
        new AIGoalKindInfo(GoalKind.None, null, GoalItemMode.None, 0, GoalCompletion.None, GoalFeature.None, false, GoalExperienceItem.None),
        new AIGoalKindInfo(GoalKind.Gather, "gather", GoalItemMode.Required, 30, GoalCompletion.Inventory, GoalFeature.None, true, GoalExperienceItem.Always),
        new AIGoalKindInfo(GoalKind.Explore, "explore", GoalItemMode.None, 0, GoalCompletion.None, GoalFeature.None, false, GoalExperienceItem.None, "탐험", "explore"),
        new AIGoalKindInfo(GoalKind.Follow, "follow", GoalItemMode.None, 0, GoalCompletion.FollowTimeout, GoalFeature.None, false, GoalExperienceItem.None, "따라와", "follow"),
        new AIGoalKindInfo(GoalKind.Craft, "craft", GoalItemMode.Required, 10, GoalCompletion.Inventory, GoalFeature.None, true, GoalExperienceItem.Always, "만들기", "craft"),
        new AIGoalKindInfo(GoalKind.Store, "store", GoalItemMode.Optional, 0, GoalCompletion.Feature, GoalFeature.Base, true, GoalExperienceItem.WhenPresent, "보관", "store"),
        new AIGoalKindInfo(GoalKind.Home, "home", GoalItemMode.None, 0, GoalCompletion.Feature, GoalFeature.Base, true, GoalExperienceItem.WhenPresent, "기지로", "home"),
        new AIGoalKindInfo(GoalKind.Farm, "farm", GoalItemMode.None, 0, GoalCompletion.Feature, GoalFeature.Farm, true, GoalExperienceItem.WhenPresent, "농사", "밭", "farm"),
        new AIGoalKindInfo(GoalKind.Cook, "cook", GoalItemMode.None, 0, GoalCompletion.Feature, GoalFeature.Cook, true, GoalExperienceItem.WhenPresent, "요리", "cook"),
        new AIGoalKindInfo(GoalKind.Free, null, GoalItemMode.None, 0, GoalCompletion.None, GoalFeature.None, false, GoalExperienceItem.None),
        new AIGoalKindInfo(GoalKind.Goto, "goto", GoalItemMode.None, 0, GoalCompletion.Feature, GoalFeature.Place, true, GoalExperienceItem.WhenPresent, "가기", "goto"),
        new AIGoalKindInfo(GoalKind.Place, "place", GoalItemMode.Required, AIPlaces.MaxShapeTiles, GoalCompletion.Feature, GoalFeature.Place, true, GoalExperienceItem.WhenPresent, "놓기", "place"),
        new AIGoalKindInfo(GoalKind.Equip, "equip", GoalItemMode.Required, 1, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.Always, "장착", "equip"),
        new AIGoalKindInfo(GoalKind.Unequip, "unequip", GoalItemMode.Optional, 1, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.WhenPresent, "벗기", "unequip"),
        new AIGoalKindInfo(GoalKind.Hold, "hold", GoalItemMode.Required, 1, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.Always, "들기", "hold"),
        new AIGoalKindInfo(GoalKind.Take, "take", GoalItemMode.Required, 9999, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.Always, "꺼내기", "take"),
        new AIGoalKindInfo(GoalKind.Drop, "drop", GoalItemMode.Required, 9999, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.Always, "내려놓기", "drop"),
        new AIGoalKindInfo(GoalKind.Give, "give", GoalItemMode.Required, 9999, GoalCompletion.Feature, GoalFeature.Inventory, true, GoalExperienceItem.Always, "건네기", "give"),
    });

    public static AIGoalKindInfo Of(GoalKind kind)
    {
        foreach (var info in All) if (info.Kind == kind) return info;
        return null;
    }

    public static bool TryWire(string word, out GoalKind kind)
    {
        foreach (var info in All) if (info.WireName != null && info.WireName == word) { kind = info.Kind; return true; }
        kind = GoalKind.None; return false;
    }

    public static bool IsHostWord(GoalKind kind, string word)
    {
        var info = Of(kind);
        if (info != null) foreach (var alias in info.HostWords) if (alias == word) return true;
        return false;
    }

    public static bool IsInventory(GoalKind kind) => kind == GoalKind.Equip || kind == GoalKind.Unequip || kind == GoalKind.Hold
        || kind == GoalKind.Take || kind == GoalKind.Drop || kind == GoalKind.Give;

    public static string ExperienceKey(GoalKind kind, string item, bool hasItem)
    {
        if (kind == GoalKind.None || kind == GoalKind.Free) return "";
        var info = Of(kind);
        if (info == null) return kind.ToString().ToLowerInvariant() + (hasItem ? ":" + item : "");
        return info.WireName + (info.ExperienceItem == GoalExperienceItem.Always
            || info.ExperienceItem == GoalExperienceItem.WhenPresent && hasItem ? ":" + item : "");
    }
}
