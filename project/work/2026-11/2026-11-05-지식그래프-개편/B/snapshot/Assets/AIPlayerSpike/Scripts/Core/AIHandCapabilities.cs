using System;

// Holding is an inventory operation available to every actual main-inventory item. An attack still needs a weapon.
// Use the game's type family rather than an allow-list of two historical types or particular item IDs.
public static class AIHandCapabilities
{
    public static bool IsWeapon(string objectType) => !string.IsNullOrEmpty(objectType) && objectType.EndsWith("Weapon", StringComparison.Ordinal);
    public static bool IsDistanceWeapon(string objectType) => IsWeapon(objectType) && objectType != "MeleeWeapon";
    public static bool IsMiningTool(string objectType) => objectType == "MiningPick" || objectType == "DrillTool" || objectType == "Sledge";
}
