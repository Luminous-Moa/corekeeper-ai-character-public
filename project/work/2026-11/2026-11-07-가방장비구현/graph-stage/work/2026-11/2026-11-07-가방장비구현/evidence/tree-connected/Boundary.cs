using Unity.Entities;
public static class AIPlaces { public const int MaxShapeTiles = 30; } // exact production constant; unrelated place descriptor dependency

// Only the world, physical dispatcher and planner/network boundaries are fake. Menu/selection/normal inventory
// factory/Apply and request JSON are the actual production code. No game consumer or model response is executed.
public static partial class AIBody
{
    public static AISkill Current;
    public static int Starts, Stops;
    public static bool TailRunning => false;
    public static void StartSkill(World w, Entity p, AISkill skill, float now)
    {
        Starts++; if (Current != null) Current.Status = SkillStatus.Cancelled;
        Current = skill; skill.Run = Starts; skill.StartTime = now; skill.Status = SkillStatus.Running;
        if (!skill.Start(w, p, now)) skill.Status = SkillStatus.Failed;
    }
    public static void Stop(World w, Entity p, float now, string reason, bool hard) { Stops++; Current = null; }
}
public static partial class AIGoals
{
    public static string Observation = "controlled offline observation";
    public static int Finishes;
    public static void FinishChosen(World w, Entity p, float now) { Finishes++; }
}
public static class AIPlanner
{
    public static AIHostOrders Orders = new();
    public static int Replans;
    public static void RequestReplan(World w, Entity p, string reason) { Replans++; }
}
public enum ChatKind { Status }
public static class AIPlayerSpikeMod
{
    public static readonly List<string> LogLines = new();
    public static void Log(string text) { LogLines.Add(text); Console.WriteLine("TRACE " + text); }
    public static void Reply(string text, ChatKind kind) { }
}
public static class AIContinueText { public static string ForShow(string text) => text; }
public static class AIVitals { public static void NoteChoice(string text) { } }
public static class AIEdge
{
    public static bool TailContinues(string key) => false;
    public static bool LastAssessedCannotCome(Entity e) => false;
}
public static class AIChargeWatch { public static void NotePick(Entity p, int req, string key, string pred, float now) { } }
public static partial class AIBrain
{
    public static int Revision;
    private static bool _sd, _needCall, _applying, _reflex;
    private static string _needReason;
    private static int _reqId, _queuedId, PrefetchDropped, HeldDropped;
    private static AICandidate _queued, _held;
    private static AISkill _queuedFor, _prefetchFor;
    private static bool _applyQueuedNow, _prefetchSkillDone;
    private sealed class Pending { public void Abort() { } public void Dispose() { } }
    private static Pending _req;
    private sealed class ApplyState
    {
        public AICandidate _nextHit, _held;
        public AISkill _nextHitFor;
        public bool _nextHitNow, _runProgress;
        public string _runKey;
        public int HeldDropped, _heldId;
    }
    private static ApplyState S = new();
    private static bool Verbose;
    private static readonly DecisionsSettings Config = new();
    private static DecisionsSettings Settings() => Config;
    private static AIGoal Goal => AIGoals.Goal;
    private static string Trim(string text, int n) => text ?? "";
    private static string MirrorNote(AICandidate c) => "";
    public static void Select(World w, Entity p, float now, AICandidate c) => Apply(w, p, now, c);
    public static List<AICandidate> Choices(World w, Entity p, AIObservation o, List<AICandidate> world) => InventoryChoiceSurface(w, p, o, world);
    public static string Request(string input, List<AICandidate> choices) => BuildJson(input, choices);
}
public sealed class ExistingAction : AISkill
{
    public ExistingAction() { Name = "existing selected action"; Key = "existing"; Run = 1; }
    public override bool Start(World w, Entity p, float now) => true;
    public override BodyInput Tick(World w, Entity p, float now) => new() { primary = true, slot = 2 };
    public override string Progress(World w, Entity p) => "still valid";
}
