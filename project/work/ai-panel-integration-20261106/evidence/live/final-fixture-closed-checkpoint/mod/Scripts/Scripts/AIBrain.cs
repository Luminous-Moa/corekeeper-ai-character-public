// AIBrain — the per-character decision state, its reset and the names other modules call (refactor S5: AIBrain is
// one partial class over AIBrain.cs, AIBrain.Loop.cs, AIBrain.Candidates.cs and AIBrain.Prep.cs (S6b); the shared types
// stay in AIDecisions.cs).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;

public static partial class AIBrain
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public int Revision;
        public UnityWebRequest _req;
        public float _reqStart;
        public int _reqRev;
        public int _reqId;
        public string _reqTrigger = "";
        public List<AICandidate> _reqCands;
        public bool _reflex;
        public bool _reqPrefetch;
        public bool _prefetchSkillDone;
        public bool _applyQueuedNow;
        public AISkill _prefetchFor;
        public AISkill _queuedFor;
        public AICandidate _queued;
        public float _queuedReqStart;
        public float _lastEtaCheck;
        public int _queuedRev;
        public int _queuedId;
        public int Prefetches;
        public int PrefetchUsed;
        public int PrefetchDropped;
        public float _lastCallStart = -999f;
        public int _consecutiveFails;
        public float _retryAt;
        public bool _applying;
        public bool _needCall;
        public string _needReason = "";
        public bool _needBreach;   // breach meaning belongs to the pending reason
        public bool _needEmergency, _reqEmergency;   // refactor S2: _needReason / _reqTrigger is an emergency (see IsEmergencyTrigger)
        public float _lastWatch;
        public readonly HashSet<Entity> _seenEnemies = new HashSet<Entity>();
        public readonly Dictionary<Entity, ObjectID> _callEnemies = new Dictionary<Entity, ObjectID>();
        public int _hpAtLastCall = -1;
        public readonly List<string> _recent = new List<string>();
        public readonly Dictionary<string, (int count, float last, string reason)> _fails = new Dictionary<string, (int, float, string)>();
        public readonly List<(int2 p, float until)> _noPathAreas = new List<(int2, float)>();
        public int _onlyWait;
        public string _objNote = "";
        public int _exploreOptions;
        public int _exploreNoPath;
        public string _exploreFrontierNote = "";
        public bool _rebuildingForTool;
        public int _toolShortRed;
        public bool _wasDown;
        public string _skippedNote = "";
        public string _edgeMoreNote = "";   // recheck N4: the edge options the cut left out, one line
        public readonly List<float> _callTimes = new List<float>();
        public readonly List<float> _peaceCallTimes = new List<float>();   // calls made with no enemy within combatRange (the peaceful cap counts only these)
        public readonly List<float> Latencies = new List<float>();
        public int Calls;
        public int Applied;
        public int Discarded;
        public int Failed;
        public long TokensThisSession;
        public bool _sd;
        public float _sdStart;
        public float _sdLastThreat;
        public float _sdLastCheck;
        public int _sdCalls0;
        public int _sdHp0;
        public int _sdDowns0;
        public int _sdEpisodes;
        public string _sdWhy = "";
        public string _prepNote = "";
        public string _blockNote = "";
        public bool _blockToolShort;   // refactor S2: _blockNote has a tool-short block (set with it, SetBlockNote)
        public bool _inCombat;
        // combat episode stats (COMBAT start|end log): start time, calls, latencies and tokens at the start
        public bool _cOn; public float _cStart; public int _cCalls0, _cLat0, _cN; public long _cTok0;
        public int _cSw0, _cSwB0, _cSwE0, _cSwM0, _cSwS0, _cSwI0, _cHeld0, _cHeldDrop0, _cCont0, _cKills0, _cDowns0, _cHp0, _cPrep0;
        // motion-boundary switching (HEAD decision 2026-11-04): an answer that switches action mid-motion waits here
        public AICandidate _held; public int _heldId, _heldRev; public float _heldAt, _heldReqStart; public string _heldTrigger = "";
        public int _reqHp = -1; public readonly HashSet<Entity> _reqAdjacent = new HashSet<Entity>();
        public float _lastEmergencyCheck;
        public int Switches, SwitchBoundary, SwitchEmergency, SwitchMid, SwitchShort, SwitchIdle, Held, HeldDropped, ContinueChosen, Kills, Reversals;
        public int _cRev0;
        public readonly List<(string key, float t)> _left = new List<(string, float)>();   // actions switched away from (reversal: back to one left < 3 s ago)
        // combat stage 2: fromKind (the left action's kind) for the close-fight switch line (HEAD decision 2026-11-05 2)
        public readonly List<(float t, string from, string to, float ran, string how, string toKind, string fromKind)> _switches = new List<(float, string, string, float, string, string, string)>();
        public readonly List<float> PrepMs = new List<float>();
        public string _prepNoteMs = "";
        // decision-preparation caches (combat re-decision every ~0.3 s): remembered-frontier search and tunnel plans
        public float _frontierAt = -99f; public int2 _frontierTile; public int _frontierSeen = -1;
        public List<(int2 t, string dir, int novelty, int cost)> _frontier; public int _frontierExpanded;
        public AITunnel.FrontierJob _fJob; public float _fJobStarted; public int _fJobFrames; public bool _fPending;
        public readonly Dictionary<ObjectID, float> _fightLog = new Dictionary<ObjectID, float>();
        public readonly Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> _plans = new Dictionary<long, (float, int2, int, TunnelPlan, string)>();
        public int _planDamage = -1;
        public float _planDamageLogAt = -999f;
        public int _planDamageFlips;
        // whether the running action was a goal step when chosen (the "continue" option says so truthfully)
        public string _runKey = ""; public bool _runProgress = true;
        // gather stall facts from the last build (what was looked at, so the stall text says only what is true)
        public string _gatherFacts = "";
        // the body cannot hold a pick / a weapon (no tool, hotbar and bag full): why, from the last build
        public string _noMineWhy = "", _noFightWhy = "";
        // combat stage 2: charge anticipations seen (AIChargeWatch sequence), the urgent call it asks for, the last answer's
        // input tokens (estimate for a dropped request), counts
        public int _chargeSeen; public bool _urgentCharge; public float _urgentAt; public int _lastTokens; public int ChargeTriggers, ChargeAborts;
        // anticipations aimed at this character from beyond the close-fight range, waiting while their charge is on (review)
        public readonly List<AIChargeWatch.Anticipation> _chargePend = new List<AIChargeWatch.Anticipation>();
        public bool _killIntent;   // TEST ONLY (/ai arena spawn … kill): the close-fight input says the host asked to kill this brute
        public bool _reqUrgent, _reqUrgentNext;   // the request in flight is an urgent re-ask (not dropped by a second anticipation)
        // "hit again" queued while a swing runs (starts as a new run when that swing is over)
        public AICandidate _nextHit; public AISkill _nextHitFor; public int _nextHitId, _nextHitRev; public float _nextHitReq; public bool _nextHitNow;
        // why the last call gave nothing usable, since when no usable answer (logged when one comes)
        public FailKind _failKind; public float _blockedSince = -1f; public int _blockedFails;
        // answers by what happened to them (HEAD review item 6; Applied counts continued + started)
        public int AnsReceived, AnsStarted, AnsContinued, AnsQueued, AnsHeldReplaced;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Brain);
    private static State S => StateOf(AIAgent.Cur);

    // the Decisions call, its settings, usage and key are in AIDecisionsApi (refactor S5); these keep the names
    public static DecisionsSettings Settings() => AIDecisionsApi.Settings();
    internal static DecisionsUsage Usage() => AIDecisionsApi.Usage();
    private static void AddUsage(int tokens, double usd) => AIDecisionsApi.AddUsage(tokens, usd);
    private static string WhoText(DecisionsUsage u) => AIDecisionsApi.WhoText(u);
    internal static bool KeyMissing() => AIDecisionsApi.KeyMissing();
    private static string ApiKey() => AIDecisionsApi.ApiKey();

    public static AIGoal Goal { get => AIGoals.Goal; set => AIGoals.Goal = value; }   // AIGoals (refactor S3)
    public static bool InFlight => _req != null;
    public static int Revision { get => S.Revision; set => S.Revision = value; }
    // Changes only when the goal is set or cleared (not on self-defence start/end, which also bump Revision for the
    // decision requests). The upper LLM's answers are judged stale against this one: with no goal, an enemy passing
    // within 8 tiles while a 12-15 s Gemini answer was on the way dropped it as "목표 수정 번호가 바뀜(2→4)" although
    // no goal had changed (2026-11-04 넷이 함께 플레이, ai2).
    public static int GoalRevision => AIGoals.GoalRevision;
    public static bool Verbose;
    // TEST ONLY: set by "/ai arena spawn … kill" for this character (each spawn sets or clears it); read by the close-fight input
    public static bool KillIntent { get => S._killIntent; set => S._killIntent = value; }

    // request in flight
    private static UnityWebRequest _req { get => S._req; set => S._req = value; }
    private static float _reqStart { get => S._reqStart; set => S._reqStart = value; }
    private static int _reqRev { get => S._reqRev; set => S._reqRev = value; }
    private static int _reqId { get => S._reqId; set => S._reqId = value; }
    private static string _reqTrigger { get => S._reqTrigger; set => S._reqTrigger = value; }
    private static List<AICandidate> _reqCands { get => S._reqCands; set => S._reqCands = value; }
    private static bool _reflex { get => S._reflex; set => S._reflex = value; }

    // Prefetch (ask for the next action shortly before the running one ends by itself, so the AI does not stand still
    // while the answer travels). One prefetch per running skill; the answer waits ("queued") until that skill ends.
    // It is used only if the skill ends as done; a failure or a cancel is new information, so the queued answer is
    // dropped and the normal call follows. Decisions still chooses; the program only asks earlier.
    private const float PrefetchLeadSeconds = 0.4f;   // decision latency p50 0.22-0.25 s (measured)
    private static bool _reqPrefetch { get => S._reqPrefetch; set => S._reqPrefetch = value; }
    private static bool _prefetchSkillDone { get => S._prefetchSkillDone; set => S._prefetchSkillDone = value; }
    private static bool _applyQueuedNow { get => S._applyQueuedNow; set => S._applyQueuedNow = value; }
    private static AISkill _prefetchFor { get => S._prefetchFor; set => S._prefetchFor = value; }
    private static AISkill _queuedFor { get => S._queuedFor; set => S._queuedFor = value; }
    private static AICandidate _queued { get => S._queued; set => S._queued = value; }
    private static float _queuedReqStart { get => S._queuedReqStart; set => S._queuedReqStart = value; }
    private static float _lastEtaCheck { get => S._lastEtaCheck; set => S._lastEtaCheck = value; }
    private static int _queuedRev { get => S._queuedRev; set => S._queuedRev = value; }
    private static int _queuedId { get => S._queuedId; set => S._queuedId = value; }
    public static int Prefetches { get => S.Prefetches; set => S.Prefetches = value; }
    public static int PrefetchUsed { get => S.PrefetchUsed; set => S.PrefetchUsed = value; }
    public static int PrefetchDropped { get => S.PrefetchDropped; set => S.PrefetchDropped = value; }

    private static float _lastCallStart { get => S._lastCallStart; set => S._lastCallStart = value; }
    private static int _consecutiveFails { get => S._consecutiveFails; set => S._consecutiveFails = value; }
    private static float _retryAt { get => S._retryAt; set => S._retryAt = value; }
    private static bool _applying { get => S._applying; set => S._applying = value; }
    internal static bool _needCall { get => S._needCall; set => S._needCall = value; }
    // Every new reason clears the emergency flag; the two emergency places (EmergencyNow, a big health drop) set it right
    // after their reason (refactor S2: the flag says what IsEmergencyTrigger read from the sentence).
    internal static string _needReason { get => S._needReason; set { S._needReason = value; S._needEmergency = false; S._needBreach = false; } }
    private static float _lastWatch { get => S._lastWatch; set => S._lastWatch = value; }
    private static HashSet<Entity> _seenEnemies => S._seenEnemies;
    private static Dictionary<Entity, ObjectID> _callEnemies => S._callEnemies;   // enemies in view at the last call
    internal static int _hpAtLastCall { get => S._hpAtLastCall; set => S._hpAtLastCall = value; }
    private static List<string> _recent => S._recent;
    // Recent failures per target key: shown on the candidate; after 2 failures within FailMemorySeconds the target is
    // left out until the memory expires (v2 §10: not a permanent ban; conditions may change).
    private const float FailMemorySeconds = 30f;
    private static Dictionary<string, (int count, float last, string reason)> _fails => S._fails;
    private const float NoPathSeconds = 90f;
    private const float NoPathRadius = 5f;
    private static List<(int2 p, float until)> _noPathAreas => S._noPathAreas;
    private static int _onlyWait { get => S._onlyWait; set => S._onlyWait = value; }
    private static string _objNote { get => S._objNote; set => S._objNote = value; }
    private static int _exploreOptions { get => S._exploreOptions; set => S._exploreOptions = value; }
    private static int _exploreNoPath { get => S._exploreNoPath; set => S._exploreNoPath = value; }   // explore candidates offered / walk targets left out (no path) in the last build
    private static string _exploreFrontierNote { get => S._exploreFrontierNote; set => S._exploreFrontierNote = value; }      // last remembered-frontier search (when nothing new within one screen)
    private static bool _rebuildingForTool { get => S._rebuildingForTool; set => S._rebuildingForTool = value; }
    private static int _toolShortRed { get => S._toolShortRed; set => S._toolShortRed = value; }   // reduction of the easiest tool-short goal wall in the last build
    private static bool _wasDown { get => S._wasDown; set => S._wasDown = value; }
    private static string _skippedNote { get => S._skippedNote; set => S._skippedNote = value; }
    private static string _edgeMoreNote { get => S._edgeMoreNote; set => S._edgeMoreNote = value; }
    private static List<float> _callTimes => S._callTimes;
    private static List<float> _peaceCallTimes => S._peaceCallTimes;
    public static List<float> Latencies => S.Latencies;
    public static int Calls { get => S.Calls; set => S.Calls = value; }
    public static int Applied { get => S.Applied; set => S.Applied = value; }
    public static int Discarded { get => S.Discarded; set => S.Discarded = value; }
    public static int Failed { get => S.Failed; set => S.Failed = value; }
    public static long TokensThisSession { get => S.TokensThisSession; set => S.TokensThisSession = value; }
    public static int Switches { get => S.Switches; set => S.Switches = value; }
    public static int SwitchBoundary { get => S.SwitchBoundary; set => S.SwitchBoundary = value; }
    public static int SwitchEmergency { get => S.SwitchEmergency; set => S.SwitchEmergency = value; }
    public static int SwitchMid { get => S.SwitchMid; set => S.SwitchMid = value; }
    public static int SwitchShort { get => S.SwitchShort; set => S.SwitchShort = value; }
    public static int SwitchIdle { get => S.SwitchIdle; set => S.SwitchIdle = value; }
    public static int Held { get => S.Held; set => S.Held = value; }
    public static int HeldDropped { get => S.HeldDropped; set => S.HeldDropped = value; }
    public static int ContinueChosen { get => S.ContinueChosen; set => S.ContinueChosen = value; }
    public static int Kills { get => S.Kills; set => S.Kills = value; }

    // decision-preparation timing (frame hitches while a character prepares a decision)
    [ThreadStatic] private static List<string> _prepPartsTS;
    private static List<string> _prepParts => _prepPartsTS ?? (_prepPartsTS = new List<string>());
    private static double _prepLastMs;
    private static void PrepPart(string name, System.Diagnostics.Stopwatch sw)
    {
        double t = sw.Elapsed.TotalMilliseconds;
        if (name == "observe") _prepLastMs = 0;
        _prepParts.Add($"{name} {t - _prepLastMs:F1}");
        _prepLastMs = t;
    }
    // time spent inside BuildCandidates on the two expensive searches (summed per build)
    private static double _msFrontier { get => BC._msFrontier; set => BC._msFrontier = value; }
    private static double _msPlans { get => BC._msPlans; set => BC._msPlans = value; }
    private static int _nPlans { get => BC._nPlans; set => BC._nPlans = value; }
    private static int _nPlansCached { get => BC._nPlansCached; set => BC._nPlansCached = value; }

    // ---- one decision preparation's scratch (refactor S5): what BuildCandidates leaves for the input and the SEND line,
    // and the path-planning counts. Shared by all characters as the static fields were (one character prepares at a
    // time). _building is the preparation running now (Send sets it around BuildCandidates): PlanC, also called by the
    // feature modules during a build (AIBase, AICook), counts into it; outside a preparation the counts go to the shared
    // one and are reset before the next preparation reads them (review R6). The names above and in AIBrain.Loop forward.
    internal sealed class BuildContext
    {
        public World _server;
        public bool _weaponBroken, _closeSet;
        public string _setLog = "", _swarmLog = "";
        public string _bruteObs = "";   // combat stage 2: the charge observation lines of a close fight with a charging enemy ("" otherwise)
        public string _curMoveName; public int2? _curMoveTo;
        public readonly Dictionary<int2, int> _reachDist = new Dictionary<int2, int>();
        public double _msFrontier, _msPlans; public int _nPlans, _nPlansCached;
    }
    private static readonly BuildContext _ctx = new BuildContext();
    [ThreadStatic] private static BuildContext _building;
    private static BuildContext BC => _building ?? _ctx;

    // ------------------------------------------------------------------ lifecycle

    public static void Reset()
    {
        AbortRequest("reset");
        AIMemory.Save(false);
        // per-join counters ("이번 접속")
        Calls = Applied = Discarded = Failed = 0;
        Prefetches = PrefetchUsed = PrefetchDropped = 0;
        _prefetchFor = null;
        Latencies.Clear();
        TokensThisSession = 0;
        SetBlockNote("", false);
        _noPathAreas.Clear();
        _onlyWait = 0;
        _sd = false;
        Goal = new AIGoal();
        _needCall = false;
        _reflex = false;
        _seenEnemies.Clear();
        _hpAtLastCall = -1;
        _recent.Clear();
        _fails.Clear();
        _wasDown = false;
        _consecutiveFails = 0;
    }

    internal static void AbortRequest(string why)
    {
        if (_req != null)
        {
            try { _req.Abort(); _req.Dispose(); } catch { }
            _req = null;
            AIPlayerSpikeMod.Log($"DECISION #{_reqId} dropped ({why})");
        }
        if (_reflex) SetReflex(false, why);
        // re-review R1: a charge anticipation drops only the request; the waiting choices stay (AIRunGate.AbortDropsWaiting)
        if (AIRunGate.AbortDropsWaiting(why))
        {
            DropQueued(why);
            if (S._held != null) { S.HeldDropped++; AIPlayerSpikeMod.Log($"DECISION #{S._heldId} held switch dropped ({why})"); S._held = null; }
            _prefetchSkillDone = false;
        }
        else if (S._held != null || _queued != null) AIPlayerSpikeMod.Log($"DECISION {(S._held != null ? $"#{S._heldId} held switch" : "")}{(S._held != null && _queued != null ? " and " : "")}{(_queued != null ? $"#{_queuedId} queued answer" : "")} kept ({why}: only the request in flight is dropped)");
    }

    private static void DropQueued(string why)
    {
        if (_queued != null)
        {
            PrefetchDropped++;
            AIPlayerSpikeMod.Log($"DECISION #{_queuedId} queued answer dropped ({why}): {Trim(_queued.desc, 60)}");
        }
        _queued = null;
        _queuedFor = null;
        _applyQueuedNow = false;
    }

    private static void SetReflex(bool on, string why)
    {
        if (_reflex == on) return;
        _reflex = on;
        AIPlayerSpikeMod.Log($"REFLEX {(on ? "ON" : "OFF")} ({why}); minimal reflex = keep the running skill if valid, else stand still; running={(AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current.Name : "none")}");
    }

    // The goal state and its life (set, clear, free time, done checks, the goal command) are in AIGoals (refactor S3);
    // these keep the names the brain and the feature modules call.
    public static void ClearGoal(GoalEnd e) => AIGoals.ClearGoal(e);
    private static void EndGoal(World server, Entity player, float now, GoalEnd e, EndOrder order, string stopReason, string trigger, string text, string reportWhy, string source)
        => AIGoals.EndGoal(server, player, now, e, order, stopReason, trigger, text, reportWhy, source);
    private static void FreeTime(World server, Entity player, float now) => AIGoals.FreeTime(server, player, now);

    // refactor S2b: returns the value's answer (AISkill.End); with the test switch, logs when the sentence disagrees
    private static bool SkillRule(string rule, AISkill s, bool byText, bool byValue)
    {
        if (AITestSwitches.Test) SkillMismatch(rule, s, byText, byValue);
        return byValue;
    }

    // refactor S2 transition, kept for the no-path rule only: no walk failed for want of a path in L1, so this rule
    // still decides by the sentence (as before) and logs when the value disagrees; it switches after L2 (plan S2b).
    private static bool SkillRuleText(string rule, AISkill s, bool byText, bool byValue)
    {
        SkillMismatch(rule, s, byText, byValue);
        return byText;
    }

    private static void SkillMismatch(string rule, AISkill s, bool byText, bool byValue)
    {
        string mm = AIOutcomes.Mismatch(rule, byText, byValue, $"skill={s.Name} key={s.Key} end={s.End} reason='{Trim(s.Reason, 120)}'");
        if (mm != null) AIPlayerSpikeMod.Log("OUTCOME-MISMATCH " + mm);
    }

    // Called by AIBody.EndSkill for every finished skill.
    public static void OnSkillEnded(AISkill s, float now)
    {
        string st = s.Status == SkillStatus.Done ? "완료" : s.Status == SkillStatus.Failed ? "실패" : "취소";
        _recent.Add($"{s.Name} {st}{(string.IsNullOrEmpty(s.Reason) ? "" : ": " + Trim(s.Reason, 90))} ({now - s.StartTime:F1}초)");
        while (_recent.Count > 4) _recent.RemoveAt(0);
        // A walk the game's pathfinding (tiles + objects) found no path for: the tile flood that offered it ignores
        // objects (core, doors, plants), so stop offering explore targets around that spot for a while.
        // refactor S2b: the down and kill rules below decide by the value set with the sentence (AISkill.End); the
        // no-path rule still decides by the sentence until L2 (SkillRuleText). Disagreements are logged as OUTCOME-MISMATCH.
        if (s is GotoSkill && s.Status == SkillStatus.Failed && s.Key.StartsWith("goto:") &&
            SkillRuleText("skill-nopath", s, AIOutcomes.NoPathText(s.Reason), AIOutcomes.NoPath(s.End)))
        {
            var parts = s.Key.Substring(5).Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int bx) && int.TryParse(parts[1], out int by))
            {
                _noPathAreas.RemoveAll(a => now > a.until);
                _noPathAreas.Add((new int2(bx, by), now + NoPathSeconds));
                AIPlayerSpikeMod.Log($"BRAIN: no path to ({bx},{by}) ({Trim(s.Reason, 40)}); explore targets within {NoPathRadius} tiles left out for {NoPathSeconds:F0}s");
            }
        }
        // combat stage 2: the short close-fight actions are not remembered as failed targets — their keys name a direction
        // relative to the enemy ("step:left:2") or a swing, not a place, so a blocked move says nothing about the next one
        bool shortAct = s is StepSkill || s is HitOnceSkill || s is ApproachSkill || s is HoldSkill || s is ComboSkill;
        if (!string.IsNullOrEmpty(s.Key) && s.Key != "wait" && !shortAct)
        {
            if (s.Status == SkillStatus.Done) _fails.Remove(s.Key);
            else if (s.Status == SkillStatus.Failed && !SkillRule("skill-down", s, AIOutcomes.DownText(s.Reason), s.End == SkillEnd.Down))
            {
                _fails.TryGetValue(s.Key, out var f);
                if (now - f.last > FailMemorySeconds) f.count = 0;
                _fails[s.Key] = (f.count + 1, now, Trim(s.Reason, 60));
            }
        }
        if ((s is AttackSkill || s is HitOnceSkill || s is ComboSkill) && s.Status == SkillStatus.Done && SkillRule("skill-kill", s, AIOutcomes.KillText(s.Reason), s.End == SkillEnd.Killed)) Kills++;
        if (_applying) { if (S._nextHitFor == s) { S._nextHit = null; S._nextHitFor = null; } return; } // cancelled because the brain itself started a new skill
        bool done = s.Status == SkillStatus.Done;
        // combat stage 2: the swing a "hit again" waited for is over -> the next swing starts (AIBrain.Tick, ApplyNextHit)
        if (S._nextHitFor == s) { if (done && S._nextHit != null) { S._nextHitNow = true; return; } AIPlayerSpikeMod.Log($"DECISION #{S._nextHitId} next swing dropped ('{s.Name}' {st})"); S._nextHit = null; S._nextHitFor = null; }
        if (done && _queued != null && _queuedFor == s) { _applyQueuedNow = true; return; }               // answer already here
        if (done && _req != null && _reqPrefetch && _prefetchFor == s) { _prefetchSkillDone = true; return; } // answer on the way
        // ended otherwise (failed / cancelled / prefetched for another skill): the prefetched answer assumed a normal end
        if (_queued != null) DropQueued($"'{s.Name}' {st}");
        if (_req != null && _reqPrefetch) { PrefetchDropped++; AbortRequest($"prefetch: '{s.Name}' {st}"); }
        _needCall = true;
        _needReason = $"기술 끝({s.Name} {st})";
    }

    public static void HandleGoalCommand(World server, Entity player, string[] parts) => AIGoals.HandleGoalCommand(server, player, parts);
    public static void SetGoal(World server, Entity player, GoalKind kind, ObjectID item, string itemName, int count, string source) => AIGoals.SetGoal(server, player, kind, item, itemName, count, source);
    public static bool TryItemPublic(string s, out ObjectID id, out string name) => AIGoals.TryItemPublic(s, out id, out name);

    public static string StatusText(World server, Entity player)
    {
        string prog = "";
        if (Goal.kind == GoalKind.Gather || Goal.kind == GoalKind.Craft)
        {
            AIBody.InventoryCounts(server, player).TryGetValue(Goal.item, out int c);
            prog = $" · 진행 {c - Goal.startCount}/{Goal.count}";
        }
        var u = Usage();
        return $"AI 목표: {Goal.Text}{(Goal.kind != GoalKind.None && !GoalSource.IsHost(Goal.source) ? "(" + Goal.source + ")" : "")}{prog} · {AIPlanner.StatusText()} · 이번 접속 호출 {Calls}(적용 {Applied}, 버림 {Discarded}, 실패 {Failed}, 미리 묻기 {Prefetches}·씀 {PrefetchUsed}) ·지연 중앙값 {Pct(0.5f):F2}초 p95 {Pct(0.95f):F2}초 · 이달 모두 {u.calls}회 ${u.usd:F4}(그중 {WhoText(u)}) / 상한 ${Settings().monthlyCapUsd:F0}";
    }

    public static float Pct(float q)
    {
        if (Latencies.Count == 0) return 0f;
        var l = new List<float>(Latencies);
        l.Sort();
        int i = Mathf.Clamp(Mathf.CeilToInt(q * l.Count) - 1, 0, l.Count - 1);
        return l[i];
    }
}
