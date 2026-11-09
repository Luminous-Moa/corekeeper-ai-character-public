using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static class AIApproach
{
    public static TunnelPlan FirstCached(World server, Entity player, IList<int2> stands, out int2 stand, out string why) =>
        AIApproachSearch.FirstOfThree<int2, TunnelPlan>(stands, (int2 tile, out string note) => AIBrain.PlanC(server, player, tile, out note), out stand, out why);

    public static TunnelPlan FirstImmediate(World server, Entity player, IList<int2> stands, out int2 stand, out string why) =>
        AIApproachSearch.FirstOfThree<int2, TunnelPlan>(stands, (int2 tile, out string note) => AITunnel.Plan(server, player, tile, out note), out stand, out why);

    public static AICandidate WalkCandidate(int2 stand, string what, bool progress, string description) =>
        new AICandidate { kind = "dig", key = $"dig:{stand.x},{stand.y}", tile = stand, digWall = false, targetLabel = what, progress = progress, desc = description };
}
