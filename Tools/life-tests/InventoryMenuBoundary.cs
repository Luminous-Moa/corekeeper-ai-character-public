// Unrelated inventory-menu boundary. These tests run the actual life/Loop source, not inventory navigation policy.
using Unity.Entities;

public sealed class AIInventoryMenuChoice
{
    public bool IsLeaf;
}

public static class AIInventoryMenu
{
    public static bool Active;
    public static int ResetCount;
    public static bool AcceptNavigation;
    public static void Reset(string reason) { Active = false; ResetCount++; }
    public static string Stale(World world, Entity player, AICandidate candidate) => null;
    public static bool ApplyNavigation(World world, Entity player, AICandidate candidate, out string note)
    {
        note = "inventory navigation adapter boundary";
        if (AcceptNavigation) Active = true;
        return AcceptNavigation;
    }
    public static void NoteLeafSelected(AICandidate candidate) { }
}
