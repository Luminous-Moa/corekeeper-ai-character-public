// AIDecisions — action selection by a Decisions service (POST /v1/decisions; Celeris celeris-1.1-decision by default,
// OpenAI gpt-6-luna selectable in decisions-settings.json "provider") on top of the body
// skills. Design: work/2026-11/2026-11-03-행동판단-연결-설계.md (session 코어키퍼-AI-캐릭터).
//
// - The host gives a goal in chat (/ai goal 흙 5 · 탐험 · 따라와 · 없음). With no goal: no calls, the AI stands.
// - Candidates are built only from what the AI can see (AIObservation / AIVision) plus the goal. Decisions picks one
//   ("choice" question); the brain starts the matching skill. Input is still produced only by AIBody.Tick.
// - One request in flight. Calls happen when no skill is running (right after one ends) or when a judgement premise
//   changes while a skill runs (new enemy in view, own health dropped), with a minimum gap.
// - Stale responses (goal revision changed, target gone, too old) are discarded and a new call is made.
// - Reflex (user decision: only while the response is late, off as soon as it arrives): minimal set — keep the
//   running skill if it is still valid, otherwise stand still. Tactical reflexes are a pending user decision.
// - Cost guard: tokens from each response's usage; monthly cap (AIPlayers/decisions-settings.json); usage in
//   AIPlayers/decisions-usage.json. The API key comes from CELERIS_API_KEY (or OPENAI_API_KEY for provider openai) and
//   is never logged or written.
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

[Serializable]
public class DecisionsSettings
{
    // Which decisions service answers (user decision 2026-11-08: Celeris celeris-1.1-decision; OpenAI gpt-6-luna kept for
    // switching back). Both speak the OpenAI Decisions request/answer format; each has its own address, key variable,
    // model id and input price. A settings file written before this field existed is moved over once
    // (AIDecisionsApi.Settings: its old model/price become the openai pair, provider becomes celeris).
    public string provider = ProviderCeleris;  // "celeris" | "openai"
    public string celerisModel = "celeris-1.1-decision";
    public float celerisUsdPerMillionInputTokens = 0.004f;   // input and cached input; output free (docs.celeris.ai/pricing)
    public string openaiModel = "gpt-6-luna";
    public float openaiUsdPerMillionInputTokens = 0.10f;
    public float monthlyCapUsd = 70f;          // ~100,000 KRW/month at ~1,430 KRW per USD (assumption)

    public const string ProviderCeleris = "celeris", ProviderOpenAI = "openai";
    public const string CelerisUrl = "https://inference.celeris.ai/celeris-1-decision/v1/decisions";
    public const string OpenAIUrl = "https://api.openai.com/v1/decisions";
    public bool IsOpenAI => string.Equals((provider ?? "").Trim(), ProviderOpenAI, StringComparison.OrdinalIgnoreCase);
    public string ProviderName => IsOpenAI ? ProviderOpenAI : ProviderCeleris;   // anything unknown falls back to celeris
    public bool ProviderKnown => IsOpenAI || string.Equals((provider ?? "").Trim(), ProviderCeleris, StringComparison.OrdinalIgnoreCase);
    public string model => IsOpenAI ? openaiModel : celerisModel;
    public float usdPerMillionInputTokens => IsOpenAI ? openaiUsdPerMillionInputTokens : celerisUsdPerMillionInputTokens;
    public string Url => IsOpenAI ? OpenAIUrl : CelerisUrl;
    public string KeyVariable => IsOpenAI ? "OPENAI_API_KEY" : "CELERIS_API_KEY";
    public float minGapSeconds = 2.5f;         // min gap between calls while a skill is running
    public float idleGapSeconds = 0.3f;        // min gap between calls when nothing is running
    public float lateAfterSeconds = 1.5f;      // reflex turns on after this without a response
    public float timeoutSeconds = 10f;
    public float maxResponseAgeSeconds = 8f;
    public int maxCallsPerMinute = 30;         // no effect since 2026-11-08 (per-minute caps removed); kept so old settings files read
    public int hpDropTrigger = 15;
    public float waitSeconds = 2f;
    // combat (an enemy within combatRange tiles): faster re-decision, smaller health-drop trigger, higher per-minute cap
    public float combatRange = 8f;
    public float combatMinGapSeconds = 0.25f;  // no effect since 2026-11-08 (no combat floor); kept so old settings files read
    public int combatMaxCallsPerMinute = 240;  // no effect since 2026-11-08 (per-minute caps removed)
    public bool combatContinuous = true;       // in combat, re-ask as soon as the previous answer arrived (one in flight)
    public int combatHpDropTrigger = 5;
    // Historical compatibility field. The 2026-11-07 user decision removed candidate count cuts in every mode.
    // Values saved by older builds are retained when reading settings but no longer truncate choices.
    public int combatMaxCandidates = 0;
    // 자연어 명령 (user decisions 2026-11-05): the upper LLM looks at the situation every this many seconds while a goal runs
    // or auto is on (the clock restarts at every report), besides the host speaking, fight ends, downs and goal ends. 0 = off.
    public float upperPeriodSeconds = 120f;
    // 합의 3e: how many goal steps one upper-LLM answer may give (a plan); steps after this are not taken. 1 = no plans.
    public int upperPlanMaxSteps = 0; // legacy compatibility; plan lists are not truncated
}

[Serializable]
public class DecisionsUsage
{
    public string month = "";
    public int calls;
    public long inputTokens;
    public double usd;
    // per AI character (member id) this month, parallel arrays (JsonUtility keeps primitive arrays in mod code)
    public string[] who = new string[0];
    public int[] whoCalls = new int[0];
    public long[] whoTokens = new long[0];
    public double[] whoUsd = new double[0];
}

[Serializable] public class DProb { public string value; public float probability; }
[Serializable] public class DAnswer { public string type; public string name; public string choice; public float confidence; public DProb[] probabilities; }
[Serializable] public class DInDetails { public int cached_tokens; }
[Serializable] public class DUsage { public int input_tokens; public int total_tokens; public DInDetails input_tokens_details; }
[Serializable] public class DResp { public string model; public DAnswer[] answers; public DUsage usage; }
[Serializable] public class DErr { public string message; public string type; public string code; }
[Serializable] public class DErrWrap { public DErr error; }

public class AIGoal
{
    public GoalKind kind = GoalKind.None;
    public ObjectID item = ObjectID.None;
    public string itemName = "";
    public int count;
    public int startCount;
    public int maxCount = -1;        // gather: explicit ceiling, independent from the desired count
    public int maxOrder = -1;        // originating HostOrder id, resolved explicitly or from a unique carried order
    public int keep = -1;            // store: leave this many in the main inventory; -1 = no reserve
    public int slot = -1;            // a selected main/gear slot, -1 = let Decisions compare actual slots
    public float startTime;
    public string source = "host";   // "host" or "llm:<member>#<report>"
    public string where = "";        // 제작대-놓기: goto / place — the place identifier (Core/AIPlaces)
    public string mode = "", second = ""; // explicit life selectors; independent of the read-only cursor
    public string note = "";         // 검수 F3/F4: the facts a place goal was taken with (a short bag, tiles left out); in the report's goal_now
    public string Text => AIGoalText.Format(this);
    // a goal someone chose (host or upper LLM)
    public bool IsReal => kind != GoalKind.None;
}

public class AICandidate
{
    public string value;     // c1..cN
    public string kind;      // mine | attack | pickup | explore | follow | wait | continue
    public string key;       // target key, compared with the running skill's key
    public string[] resources;   // 검수 F4: the resources a gather group covers, as "<item>@x,y" (one report fact each)
    public string desc;
    public int2 tile;
    public Entity entity = Entity.Null;
    // dig (TunnelSkill) parameters
    public bool digWall, digOpenEnough;
    public string targetLabel = "";   // dig / craft / follow / retreat / step / combo: target description
    public string actionMode = "";    // farm / cook: execution mode
    public string exploreDirection = ""; // explore: skill Tag direction
    public bool progress;    // a candidate that can move a gather goal forward (used for the stall check)
    public bool comesByItself; // pickup of an item the game already pulls in (flying to this AI or another player, or inside the auto-pickup radius)
    public ObjectID itemId = ObjectID.None;
    public int itemVariation; // eat: item variant
    public int itemCount;     // craft / take / cook: amount
    public AIInventoryMove inventory; // exact observed inventory action, checked again before applying
    public LifeNav lifeNav;
    public ExpeditionNav expeditionNav;
    public AICandidate resume;
    public AIGatherSelection gather;
    public LifeSelection life;
    public AIFarm.Selection farm;
    public AICook.Selection cook;
    public AICraftSelection craft;
    public AIInventoryMenuChoice inventoryMenu; // immutable menu node/revision and selected observed item; no body action for navigation
    public MaintenanceRequest maintenance;
    public int edgeGap;       // edgefill / edgerepair: gap index
    public int waitSeconds;   // cookwait: duration
    public EdgeCandidateRole edgeRole; // meaning independent of desc
    public List<int2> tiles;   // farm (FarmSkill): the tiles of one farm action, in visiting order
    public ObjectID secondItemId = ObjectID.None;   // cook (CookSkill): the second ingredient
    public int run;          // continue: the run number of the action it continues (AIRunGate)
    public float2 dest;      // step (StepSkill): the point it walks to
    public string pred = ""; // combat stage 2 close-fight options: the predicted charge outcome code (AIChargeMath.Code), for the logs
    public string combo = "";  // combat stage 2 composite action: hit-move | approach-hit | move-hit
    public float obsAt;      // combat stage 2: when the observation the option was built from was taken (realtime)
    public bool placeAt;     // place (코어-안전지대 단계 1): the option places on `tile` (one option per spot, AIPlaceSpot.Candidates)
    public int2 pickupStand; // edgepickup: required collection stand
    public bool pickupStandOnly, retreatInside; // separate from placing constraints
    public int2 placeStand;  // place: the stand the option states (PlaceSkill walks there first, review F1)
    public bool digAllowEdge, digDetour;   // 우회 사실: the dig plans with the edge open (TunnelSkill.AllowEdge); offered beside a long door way (AsmCand.detour)
    public List<int2> digEdgeOk;   // 제작대-놓기 실게임 D1: a goto / place / home walk or an enemy dig: the edge walls its option stated (null: no edge check)
    public bool placeStandOnly;   // place (단계 2 review G3): only from placeStand (the base door goes in from inside)
    public List<int2> group;      // edgerepair (단계 3): every tile of the broken opening (the closing check)
}

// Stands still for a fixed time (the "wait" choice).
public class WaitSkill : AISkill
{
    private readonly float _seconds;
    public WaitSkill(float seconds) { Name = "기다리기"; _seconds = seconds; Key = "wait"; }
    public override bool Start(World server, Entity player, float now) => true;
    public override BodyInput Tick(World server, Entity player, float now)
    {
        if (now - StartTime >= _seconds) return Finish(SkillStatus.Done, $"{_seconds:F0}초 기다림");
        return BodyInput.Idle;
    }
}

// TEST ONLY switches for arena comparisons on one build (2026-11-04, combat polish 1). Files in AIPlayers/, read once
// a second, honoured only while AIPlayers/testcmds.on exists (so a forgotten file does nothing in normal play):
//   closefight.off - the close-fight candidate set (AIBrain.CloseFight) is not used: every candidate as before.
//                    Default (no file): used. The SEND line says set=close-fight, or set=all(close-fight off) when the
//                    close-fight condition held but the set was switched off.
//   decisionlog.on - every request's whole input and every option text, and every answer with all probabilities, plus
//                    the DECISION / SWITCH / SKILL lines, go to AIPlayers/decisions-<HHmmss>.log. Own buffered file,
//                    flushed once a second (Player.log goes through Debug.Log with a stack trace per line; this does
//                    not). Never the API key or the request JSON.
public static class AITestSwitches
{
    public static bool CloseFightOff, DecisionLog;
    public static bool Test;   // testcmds.on (refactor S2b: the OUTCOME-/EMERGENCY-/REJECT-/TOOLSHORT-MISMATCH comparisons log only then)
    private static float _last = -10f;
    private static StreamWriter _w;
    private static string Dir => Path.Combine(Application.persistentDataPath, "AIPlayers");

    public static void Refresh(float now)
    {
        if (now - _last < 1f) return;
        _last = now;
        bool test = false, cfOff = false, dl = false;
        try
        {
            test = File.Exists(AIBody.TestSwitchPath);
            if (test) { cfOff = File.Exists(Path.Combine(Dir, "closefight.off")); dl = File.Exists(Path.Combine(Dir, "decisionlog.on")); }
        }
        catch { }
        Test = test;
        if (cfOff != CloseFightOff) { CloseFightOff = cfOff; AIPlayerSpikeMod.Log($"TEST-ONLY close-fight candidate set: {(cfOff ? "OFF (closefight.off)" : "ON")}"); }
        if (dl != DecisionLog) ToggleLog(dl);
        if (_w != null) { try { _w.Flush(); } catch (Exception e) { AIPlayerSpikeMod.LogError("decisionlog: flush failed; turning off", e); ToggleLog(false); } }
    }

    private static void ToggleLog(bool on)
    {
        if (on)
        {
            try
            {
                string path = Path.Combine(Dir, $"decisions-{DateTime.Now:HHmmss}.log");
                _w = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16) { AutoFlush = false };
                DecisionLog = true;
                AIPlayerSpikeMod.Log($"TEST-ONLY decisionlog: ON -> {path}");
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("decisionlog: open failed", e); DecisionLog = false; _w = null; }
        }
        else
        {
            DecisionLog = false;
            try { _w?.Flush(); _w?.Dispose(); } catch { }
            _w = null;
            AIPlayerSpikeMod.Log("TEST-ONLY decisionlog: OFF");
        }
    }

    public static void Write(string line)
    {
        if (_w == null) return;
        try { _w.Write($"t={Time.realtimeSinceStartup:F2} {(AIAgent.InScope != null ? "@" + AIAgent.InScope.Name + " " : "")}"); _w.WriteLine(line); }
        catch (Exception e) { AIPlayerSpikeMod.LogError("decisionlog: write failed; turning off", e); ToggleLog(false); }
    }
}
