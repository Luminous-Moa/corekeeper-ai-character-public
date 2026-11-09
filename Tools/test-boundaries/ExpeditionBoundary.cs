// Expedition physical/choice owners have production-linked tests in execution-, gather- and travel-tests.
// These lifecycle-only adapters keep the separate life/maintenance suites scoped to their own consumers.
using Unity.Entities;
public sealed class ExpeditionNav { }
public sealed class AIGatherSelection { }
public static partial class AIGathering
{
#if LIFE_GATHER
    public static string LimitReason(World w, Entity p, AICandidate c) => HarvestCandidateLimit(w,p,c);
#else
    public static string LimitReason(World w, Entity p, AICandidate c) => null;
#endif
}
public static class AIExpedition
{
    public static void Observe(World w, Entity p) { }
    public static string Stale(AICandidate c) => null;
    public static string Navigate(AICandidate c) => null;
    public static AICandidate Resolve(AICandidate c) => c.resume;
    public static void Starting(World w, Entity p, AICandidate c, AISkill s, float now) { }
    public static void Choosing(World w, Entity p, AICandidate c) { }
    public static void Ended(AISkill s) { }
    public static long Gathered(AIGoal goal, int net) => net;
    public static void LowerAnswered(AICandidate chosen, float now) { }
    public static string SetValuable(System.Collections.Generic.Dictionary<string, bool> changes) => "";
#if LIFE_GATHER
    public static System.Collections.Generic.IReadOnlyList<ExpeditionLimit> ActiveLimits()
    {
        var g=AIGoals.Goal;
        return g.kind==GoalKind.Gather&&g.maxCount>=0 ? new[]{new ExpeditionLimit{OrderId=g.maxOrder,Item=g.item.ToString(),Maximum=g.maxCount,Acquired=0}} : System.Array.Empty<ExpeditionLimit>();
    }
#endif
}
public static class AITravel
{
    public static bool TryApplyTurn(World w, Entity p, AICandidate c, out string why) { why = null; return true; }
}
