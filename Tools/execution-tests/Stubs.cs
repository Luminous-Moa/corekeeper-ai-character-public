// Game, network and physical body boundaries only. AIGoals, Apply, AISkill and host-order rules are production code.
using System;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Entities;

namespace Pug.UnityExtensions { public sealed class Unused { } }
namespace PugTilemap { public sealed class Unused { } }
namespace Unity.Collections { public sealed class Unused { } }
namespace Unity.Physics { public sealed class Unused { } }
namespace Unity.Transforms { public struct LocalTransform { public Unity.Mathematics.float3 Position; } }
namespace Unity.Mathematics
{
    public struct float2 { public float x, y; }
    public struct float3 { public float x, y, z; public static float3 zero => default; }
    public struct int2 { public int x, y; public int2(int x, int y) { this.x = x; this.y = y; } }
    public static class math { public static int max(int a, int b) => Math.Max(a, b); }
}
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace Unity.Entities
{
    public struct Entity
    {
        public int Index;
        public static Entity Null => default;
        public static bool operator ==(Entity a, Entity b) => a.Index == b.Index;
        public static bool operator !=(Entity a, Entity b) => a.Index != b.Index;
        public override bool Equals(object other) => other is Entity e && this == e;
        public override int GetHashCode() => Index;
    }
    public sealed class EntityManager
    {
        public bool Exists(Entity entity) => true;
        public bool HasComponent<T>(Entity entity) => false;
        public T GetComponentData<T>(Entity entity) => default;
    }
    public sealed class World { public EntityManager EntityManager = new EntityManager(); }
}

public enum ObjectID { None, WallDirtBlock, CopperOre, TinOre, Wood, CopperBreastArmor }
public enum EdgeCandidateRole { None }
// Life producers are exercised by Tools/life-tests and the module adapters. This regression owns explicit controls.
public static class AIFarm { public sealed class Selection { } }
public static class AICook { public sealed class Selection { } }
public sealed class AICraftSelection { }
public sealed class MaintenanceRequest { }
public static class AICraft
{
    public static int MainCount(World world, Entity player, ObjectID item) => AIBody.Bag.TryGetValue(item, out int n) ? n : 0;
}
public static class AILife
{
    public static readonly LifeCursor Cursor = new LifeCursor("test", 0);
    public static bool BlocksInventoryView(World world, Entity player) => Cursor.Open;
    public static void LeaveNavigation() { Cursor.Reset("test", 0); }
    public static string Navigate(LifeNav nav) => null;
    public static string Stale(World world, Entity player, AICandidate candidate) => null;
    public static void Starting(AICandidate candidate, AISkill skill) { }
    public static void Ended(AISkill skill) { }
    public static void OnGoalChanged() { }
    public static string Report() => "";
}
public struct HealthCD { public int health; }
public enum ChatKind { Status, Error }
public static class AISlot { public const int Goals = 15, Expedition = 19; }
public sealed class AIAgent
{
    public string Name = "Gemini";
    public string Id = "gemini";
    public readonly object[] Slots = new object[21];
    public static AIAgent Cur = new AIAgent();
    private readonly Dictionary<int, object> _state = new Dictionary<int, object>();
    public T Get<T>(int slot) where T : class, new()
    {
        object state = Slots[slot];
        if (state == null) Slots[slot] = state = new T();
        return (T)state;
    }
}
public static class AIPlayerSpikeMod
{
    public static string WorldId = "test";
    public static void Log(string message) => Harness.Trace.Add(message);
    public static void Reply(string message, ChatKind kind = ChatKind.Status) => Harness.Trace.Add(message);
    public static void LogError(string message, Exception error) => Harness.Trace.Add(message + " " + error.Message);
}
public static class AIPlaces
{
    public const int MaxShapeTiles = 100;
    public static bool Parse(string where, out string parsed, out string why)
    { parsed = where ?? "base"; why = null; return true; }
}
public static class AIPlaceGoal
{
    public static string CheckGoto(World w, Entity p, string where) => null;
    public static string CheckPlace(World w, Entity p, string item, int n, string where) => null;
    public static void OnGoalSet(World w, Entity p, AIGoal goal, string parsed) { }
}
public static class AIGoalFeatures
{
    public static bool Done;
    public static bool TryDone(World w, Entity p, GoalKind kind, out string done)
    {
        if (AIInventory.OwnsGoal(AIGoals.Goal)) return AIInventory.GoalDone(w, p, out done);
        done = Done ? "arrived" : "";
        return Done;
    }
    public static string StallNote(World w, Entity p, GoalKind kind) => "no accessible facility";
    public static string Guidance = "";
    public static string CompletionGuidance(World w, Entity p, AIGoal goal) => AIInventory.OwnsGoal(goal) ? Guidance : "";
}
public static class AIExperience
{
    public static void GoalEnding(AIGoal goal, GoalEnd end) => Harness.Trace.Add("experience end " + end.Outcome);
    public static void GoalStarting(World w, Entity p, AIGoal oldGoal, AIGoal newGoal) { }
}
// Inventory engine/Unity is tested in its separate adapter. This boundary supplies observed receipts to real AIGoals.
public sealed class AIInventoryMove { }
public static class AIInventory
{
    public static bool Observed;
    public static bool OwnsGoal(AIGoal goal) => AIGoalKinds.IsInventory(goal.kind);
    public static void GoalSet(World world, Entity player, AIGoal goal) { Observed = false; }
    public static bool GoalDone(World world, Entity player, out string note) { note = Observed ? "actual inventory receipt" : ""; return Observed; }
    public static string StallNote(World world, Entity player) => "no current inventory choice";
    public static string CheckGoal(World world, Entity player, GoalKind kind, ObjectID item, int count, string where, int keep, int slot) => null;
}
public static partial class AIBody
{
    public static float3 Pos(World world, Entity player) => new float3();
    public static int2 TileOf(float3 pos) => new int2((int)pos.x, (int)pos.z);
    public static AISkill Current;
    public static int Stops;
    public static bool TailRunning => S.tailOn && Current != null && Current.Status == SkillStatus.Running;
    private sealed class BodyState
    {
        public bool tailOn;
        public SkillStatus tailStatus;
        public string tailReason = "";
        public SkillEnd tailEnd;
    }
    private static BodyState S = new BodyState();
    public static Dictionary<ObjectID, int> Bag = new Dictionary<ObjectID, int>();
    public static Dictionary<int, ObjectID> SlotObjects = new Dictionary<int, ObjectID>();
    public static ObjectID SlotItem(World world, Entity player, int slot, out bool broken) { broken = false; return SlotObjects.TryGetValue(slot, out var item) ? item : ObjectID.None; }
    public static Dictionary<ObjectID, int> InventoryCounts(World w, Entity p) => new Dictionary<ObjectID, int>(Bag);
    private static bool IsDown(World w, Entity p) => false;
    private static void DropPress(string why, float now) { }
    public static void ResetTail() => S = new BodyState();
    public static void BeginTail()
    {
        S.tailOn = true; S.tailStatus = SkillStatus.Done;
        S.tailReason = "selected mining complete"; S.tailEnd = SkillEnd.None;
    }
    public static void StartSkill(World w, Entity p, AISkill skill, float now)
    {
        if (Current != null) Stop(w, p, now, "new chosen action");
        Current = skill; skill.StartTime = now; skill.Start(w, p, now);
    }
}
public static partial class AIPlanner
{
    public sealed class State {
        public AIPlanQueue Plan = new AIPlanQueue(); public AIHostOrders Orders => AIPlanner.Orders;
        public bool _planNext, HeldWatch; public string _reqPlanSource, _planEndedSource, _owedPlanTrigger, _down;
        public string CompletionConflictScope;
        public readonly HashSet<string> ReportedCompletionConflicts = new HashSet<string>();
        public PlanInbox Inbox = new PlanInbox(); public float _lastPlanAt; public int _reqChatSeenBefore, _chatSeen, _reqOrderSeq, HostOrderSeq;
    }
    public static State Cur = new State();
    public static AIHostOrders Orders = new AIHostOrders();
    public static int EndReports, Replans;
#if BASELINE
    public static void AddEvent(string text, bool force = false) { }
#endif
    public static void Enable(World w, Entity p) { }
    public static void Disable(string why) { }
    public static void OnHostGoal(World w, Entity p, string text) { }
#if BASELINE
    public static void OnGoalEnded(World w, Entity p, string trigger, string text, string why, string source)
    {
        EndReports++;
        Orders.GoalEnded(source, AIPlanRules.StepDone(trigger), why, null);
    }
    public static void RequestReplan(World w, Entity p, string why) { Replans++; Harness.Trace.Add("replan " + why); }
#endif
}
public sealed class AIAction { public Func<World, Entity, AICandidate, string> Stale; }
public static partial class AIActions
{
    public static AISkill Make(AICandidate c) => c.kind == "mine" || c.kind == "gather_group" || c.kind == "explore" || c.kind == "retreat" ? new ProbeSkill(c.key) : null;
    private static AIAction Of(AICandidate candidate) => null;
}
public static class AIVitals { public static int Downs; public static void NoteChoice(string why) { } }
// The menu module has separate actual-source tree tests. This boundary lets the real Loop.Apply and public
// request surface be tested for body-neutral navigation and pending-choice ownership.
public sealed class AIInventoryMenuChoice { public bool IsLeaf; public string action; }
public static class AIInventoryMenu
{
    public static bool Active;
    public static int NavigationCount, LeafCount;
    public static List<AICandidate> Choices = new List<AICandidate>();
    public static string Stale(World w, Entity p, AICandidate c) => c.inventoryMenu == null ? "menu token missing" : null;
    public static bool ApplyNavigation(World w, Entity p, AICandidate c, out string note)
    { note = c.inventoryMenu.action; Active = note != "cancel"; NavigationCount++; return true; }
    public static void NoteLeafSelected(AICandidate c) { LeafCount++; Active = false; }
    public static List<AICandidate> Candidates(World w, Entity p, AIObservation o, string key) => new List<AICandidate>(Choices);
    public static void Reset(string reason = "") { Active = false; NavigationCount = LeafCount = 0; Choices.Clear(); }
}
public sealed class SeenEnemy { public Entity entity; public float dist; }
public sealed class AIObservation { public float3 pos; public List<SeenEnemy> enemies = new List<SeenEnemy>(); }
public static class AIVision { public static bool CanSee(float3 pos, int2 tile) => Math.Abs(tile.x) <= 10 && Math.Abs(tile.y) <= 10; }
public sealed class AIGatherSelection { }
public static class AIGathering
{
    public static string LimitBlock;
    public static string LimitReason(World w, Entity p, AICandidate c) => LimitBlock;
    public static string LimitNote(World w, Entity p) => "";
}
public static class AITravel
{
    public static bool TryApplyTurn(World w, Entity p, AICandidate c, out string why) { why = null; return true; }
}
public static class AIContinueText { public static string ForShow(string text) => text; }
public static class AIEdge
{
    public static int TailAborts, TailStartCalls;
    public static bool TailContinues(string key) => false;
    public static bool TailStart(World w, Entity p, AISkill skill, bool down) { TailStartCalls++; return false; }
    public static void TailAbort() => TailAborts++;
    public static bool LastAssessedCannotCome(Entity e) => false;
}
public static class AIChargeWatch { public static void NotePick(Entity p, int req, string key, string pred, float now) { } }
public static partial class AIBrain
{
    public static bool _treeStepNext, _issuePending;
    public static void MarkIssue(string t) { if (AIRunGate.IsIdleIssue(t)) _issuePending = true; }
    public static void ResetIdleEngagement() { _issuePending = false; }
}
public static partial class AIBrain
{
    public static int Revision, Calls;
    public static bool Verbose, _sd, _needCall;
    public static string _needReason;
    public static int _hpAtLastCall;
    public static AIGoal Goal => AIGoals.Goal;
    public static readonly List<string> Observations = new List<string>();
    private static AICandidate _queued;
    private static AICandidate _held { get => S._held; set => S._held = value; }
    private static int HeldDropped { get => S.HeldDropped; set => S.HeldDropped = value; }
    private static bool _reflex;
    private static AISkill _queuedFor, _prefetchFor;
    private static int _queuedId;
    private static bool _applyQueuedNow, _reqPrefetch, _prefetchSkillDone;
    private sealed class Request { public void Abort() { AbortedRequests++; } public void Dispose() { } }
    private static Request _req;
    public static int AbortedRequests;
    public static int Kills, PrefetchDropped;
    private const float FailMemorySeconds = 30, NoPathSeconds = 90;
    private const int NoPathRadius = 5;
    private static RecentResults _recent = new RecentResults();
    private static Dictionary<string, (int count, float last, string why)> _fails = new Dictionary<string, (int, float, string)>();
    private static List<(Unity.Mathematics.int2 tile, float until)> _noPathAreas = new List<(Unity.Mathematics.int2, float)>();
    private static bool _applying;
    private static int _reqId;
    private static bool _inCombat;
    private static float _retryAt;
    private static readonly List<float> _callTimes = new List<float>(), _peaceCallTimes = new List<float>();
    private sealed class PreparationProbe { public bool Prefetch, PrefetchCompleted; }
    private static void CancelPreparation(string why) { S.Preparation = null; _needCall = true; }
    public static void SeedPreparingPrefetch(AISkill skill) { _prefetchFor = skill; S.Preparation = new PreparationProbe { Prefetch = true }; }
    public static bool HasPreparingPrefetch => S.Preparation != null;
    public static bool PreparingPrefetchCompleted => S.Preparation?.PrefetchCompleted == true;
    private sealed class ApplyState
    {
        public PreparationProbe Preparation;
        public string _runKey, ExecutionReason;
        public bool _runProgress;
        public int ExecutionRevision, AskedExecutionRevision;
        public int ExecutionGoalRevision = -1, ExecutionRun;
        public string ExecutionGoalText = "";
        public AICandidate _nextHit, _held;
        public AISkill _nextHitFor;
        public int _nextHitId, _heldId, HeldDropped;
        public bool _nextHitNow;
        public object _fJob; public bool _fPending;   // remembered-frontier search state (AIBrain.ForgetPlace, ae09acf)
    }
    private static ApplyState S = new ApplyState();
    private static readonly DecisionsUsage Use = new DecisionsUsage();
    private static readonly DecisionsSettings Config = new DecisionsSettings();
    public static string LastEndedResult => _recent.Last ?? "";
    public static void SetBlockNote(string note, bool toolShort) { }
    public static bool KeyMissing() => false;
    public static DecisionsUsage Usage() => Use;
    public static DecisionsSettings Settings() => Config;
    public static string StatusText(World w, Entity p) => Goal.Text;
#if BASELINE
    public static void ObserveExecution(string reason) { Observations.Add(reason); Harness.Trace.Add("observe " + reason); }
#endif
    private static string Trim(string s, int max) => s ?? "";
    private static string MirrorNote(AICandidate candidate) => "";
    public static void SeedPending(AISkill selected)
    {
        _queued = new AICandidate { kind = "mine", key = "old-prefetch-Y" }; _queuedFor = selected;
        S._held = new AICandidate { kind = "mine", key = "old-held-Y" };
        S._nextHit = new AICandidate { kind = "hit", key = "old-next-hit" }; S._nextHitFor = selected;
        S._nextHitNow = false;
    }
    public static void SeedQueuedOnly(AISkill selected)
    {
        _queued = new AICandidate { kind = "mine", key = "chosen-next-Y" }; _queuedFor = selected;
    }
    public static bool QueuedReady => _queued != null && _applyQueuedNow;
    public static bool HasPendingChoices => _queued != null || S._held != null || S._nextHit != null || _applyQueuedNow || S._nextHitNow;
    public static bool HasInFlight => _req != null;
    public static void SeedInFlight() => _req = new Request();
    public static void ResetPending()
    {
        _queued = null; _queuedFor = null; _applyQueuedNow = false;
        _req = null; _reqPrefetch = _prefetchSkillDone = false; _prefetchFor = null;
        _recent.Clear(); _fails.Clear(); _noPathAreas.Clear();
        S.Preparation = null;
        AbortedRequests = 0;
    }
#if !BASELINE
    public static void ApplyCandidate(AICandidate c, float now) => Apply(Harness.World, Harness.Player, now, c);
    public static void ApplyChosen(World w, Entity p, float now, string kind, string key = "")
        => Apply(w, p, now, new AICandidate { kind = kind, key = key, desc = kind });
    public static void ApplyMenu(World w, Entity p, float now, string action)
        => Apply(w, p, now, new AICandidate { kind = "inventory_menu", key = action, desc = action, inventoryMenu = new AIInventoryMenuChoice { action = action } });
    public static List<AICandidate> Surface(World w, Entity p, AIObservation o, List<AICandidate> ordinary) => InventoryChoiceSurface(w, p, o, ordinary);
    public static string RequestJson(string input, List<AICandidate> choices) => BuildJson(input, choices);
    public static bool BudgetAllows(float now) => CallBudgetOk(now);
    public static void ExhaustRate(float now) { _inCombat = false; _peaceCallTimes.Clear(); for (int i = 0; i < Config.maxCallsPerMinute; i++) _peaceCallTimes.Add(now); }
    public static void ExhaustCost() { _peaceCallTimes.Clear(); Use.usd = Config.monthlyCapUsd; }
    public static void ResetBudget() { _inCombat = false; _callTimes.Clear(); _peaceCallTimes.Clear(); Use.usd = 0; }
    public static string RetainedExecutionReason => S.ExecutionReason;
    public static int RetainedExecutionRevision => S.ExecutionRevision;
    public static void ResetExecutionObservations() => S = new ApplyState();
#endif
}
public class ProbeSkill : AISkill
{
    public int Ticks;
    public ProbeSkill(string key) { Key = key; Name = "selected mining"; }
    public override bool Start(World w, Entity p, float now) => true;
    public override BodyInput Tick(World w, Entity p, float now) { Ticks++; return new BodyInput { primary = true, slot = 1 }; }
    public void Observe(string key, string reason) => ObserveOnce(key, reason);
}
public sealed class TunnelSkill : ProbeSkill { public TunnelSkill() : base("dig") { } }
public sealed class GotoSkill : ProbeSkill { public GotoSkill() : base("goto") { } }
public sealed class LookSkill : ProbeSkill { public LookSkill() : base("look") { } }
public sealed class InvSkill : ProbeSkill { public InvSkill() : base("inv") { } }
public sealed class StepSkill : ProbeSkill { public StepSkill() : base("step") { } }
public sealed class HitOnceSkill : ProbeSkill { public HitOnceSkill() : base("hit") { } }
public sealed class ApproachSkill : ProbeSkill { public ApproachSkill() : base("approach") { } }
public sealed class HoldSkill : ProbeSkill { public HoldSkill() : base("hold") { } }
public sealed class ComboSkill : ProbeSkill { public ComboSkill() : base("combo") { } }
public sealed class AttackSkill : ProbeSkill { public AttackSkill() : base("attack") { } }
public static class AITestSwitches { public static bool Test => false; }
public static class AIMemory { public static string LastFrontierSearch => ""; public static int SeenCount => 0; }
public static class Harness
{
    public static readonly List<string> Trace = new List<string>();
    public static readonly World World = new World();
    public static readonly Entity Player = new Entity();
    public static void Reset()
    {
        AIAgent.Cur = new AIAgent(); AIBody.Current = null; AIBody.Stops = 0; AIBody.Bag.Clear();
        AIBody.SlotObjects.Clear();
        AIBody.ResetTail(); AIEdge.TailAborts = AIEdge.TailStartCalls = 0; AIBrain.ResetPending();
        AIBrain.Observations.Clear(); AIBrain.Revision = 0; AIBrain._sd = false;
#if !BASELINE
        AIBrain.ResetExecutionObservations();
#endif
        AIPlanner.Orders = new AIHostOrders(); AIPlanner.EndReports = AIPlanner.Replans = 0;
        AIPlanner.Cur = new AIPlanner.State(); AIPlayerSpikeMod.WorldId = "test";
#if !BASELINE
        AIPlanner.ResetTransport();
        AIGathering.LimitBlock = null;
#endif
        AIGoalFeatures.Done = false; Trace.Clear(); UnityEngine.Time.realtimeSinceStartup = 0;
        AIInventoryMenu.Reset();
#if !BASELINE
        AIBrain.ResetBudget();
#endif
    }
}

// TEST ONLY /ai hold gate (ae09acf AITestControl): never held in these offline checks
public static class AITestControl { public static bool Held; }
