// Network, motion, body dispatch, and unrelated features are adapter boundaries.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum ChatKind { Status,Error }
public static class AIDecisionsApi
{
    public sealed class Reply
    {
        public long Code=200; public string Text="{}",NetErr="",ErrorMessage="",RequestId=""; public bool Ok=true,NetOk=true,Fallback; public Exception ParseError;
        public DAnswer Answer;public int Tokens,Cached;
    }
    public static Reply Next;
    public static Reply Read(object request)=>Next;
}
public static class AIMotion {public static bool AtBoundary=true;public static string Mid=>"adapter mid motion";public static float SwingLeft=>0.5f;}
public static class AITestSwitches {public static bool DecisionLog,Test;public static void Write(string text){} }
public static class AIContinueText {public static string ForShow(string s)=>s;}
public static class AIVitals {public static void NoteChoice(string s){} }
public static class AIChargeWatch {public static void NotePick(Entity player,int req,string key,string pred,float now){} }
public static class AIExperience
{public static void GoalEnding(AIGoal g,GoalEnd e){}public static void GoalStarting(World w,Entity p,AIGoal before,AIGoal after){} }
public static class AIPlaces
{public const int MaxShapeTiles=100;public static bool Parse(string where,out string parsed,out string why){parsed=where;why=null;return true;} }
public static class AIPlaceGoal
{
    public static void OnGoalSet(World w,Entity p,AIGoal g,string parsed){}
    public static string CheckGoto(World w,Entity p,string where)=>null;
    public static string CheckPlace(World w,Entity p,string item,int count,string where)=>null;
    public static string StallNote(World w,Entity p)=>"adapter";
}
public static class AIGoalFeatures
{
    public static bool TryDone(World w,Entity p,GoalKind kind,out string note){note="";return false;}
    public static string StallNote(World w,Entity p,GoalKind kind)=>"adapter feature boundary";
    public static string CompletionGuidance(World w,Entity p,AIGoal g)=>"";
}
public static partial class AIInventory
{
    public static bool AtBaseContext; public static bool BaseChestContext(World w,Entity p,int2 t)=>AtBaseContext;
    public static bool OwnsGoal(AIGoal g)=>AIGoalKinds.IsInventory(g.kind);
    public static void GoalSet(World w,Entity p,AIGoal g){}
    public static bool GoalDone(World w,Entity p,out string note){note="";return false;}
    public static string StallNote(World w,Entity p)=>"adapter inventory boundary";
    public static string CheckGoal(World w,Entity p,GoalKind k,ObjectID i,int n,string where,int keep,int slot)=>null;
}
public static partial class AIEdge {public static bool TailContinues(string key)=>false;}
public static partial class AICraft {public static int MainCount(World w,Entity p,ObjectID id){AIBody.InventoryCounts(w,p).TryGetValue(id,out int count);return count;}}
public static partial class AIPlanner
{
    public static bool ReplanPending;public static int Replans;
    public static void AddEvent(string text){}
    public static void Enable(World w,Entity p){}public static void Disable(string why){}
    public static void OnHostGoal(World w,Entity p,string text){}
    public static void OnGoalEnded(World w,Entity p,string trigger,string text,string why,string source){}
    public static void RequestReplan(World w,Entity p,string why){Replans++;}
    public static void SetStep(World w,Entity p,PlanStep step)=>SetGoalFrom(w,p,step.Do,step.Item,step.Count,"llm:test",step.Where,step.Keep,step.Slot,step.Mode,step.Second,step.MaxCount,step.MaxOrder);
}
public static partial class AIBody
{
    public static AISkill Current;public static int Starts,Stops,LastRun;
    public static bool TailRunning=>false;
    public static ObjectID SlotItem(World w,Entity p,int slot,out bool broken){broken=false;return AIInventory.Read(w,p,slot,out var x)?x.objectID:ObjectID.None;}
    public static void StartSkill(World w,Entity p,AISkill skill,float now)
    {
        if(Current!=null&&Current.Status==SkillStatus.Running)Stop(w,p,now,"selected next action");
        Current=skill;Starts++;skill.Run=++LastRun;skill.StartTime=now;skill.StartPos=Pos(w,p);skill.Start(w,p,now);
    }
    public static void Stop(World w,Entity p,float now,string why,bool hard=false)
    {Stops++;if(Current==null)return;var old=Current;old.Status=SkillStatus.Cancelled;old.Reason=why;old.Cleanup(w);Current=null;AIBrain.OnSkillEnded(old,now);}
}
public static partial class AIBrain{public static bool _treeStepNext,_issuePending;public static void MarkIssue(string t){if(AIRunGate.IsIdleIssue(t))_issuePending=true;}public static void ResetIdleEngagement(){_issuePending=false;}}
public static partial class AIBrain
{
    public static int Revision,Calls,Applied,Discarded,Failed,PrefetchUsed,PrefetchDropped,ContinueChosen,Held,HeldDropped,Kills,Switches,SwitchEmergency,SwitchShort,SwitchMid,SwitchBoundary,SwitchIdle;
    public static long TokensThisSession;
    public static bool Verbose,_sd,_needCall;
    public static string _needReason="";public static int _hpAtLastCall;
    public static AIGoal Goal=>AIGoals.Goal;public static int GoalRevision=>AIGoals.GoalRevision;
    public static readonly List<string> Observations=new List<string>();
    private static readonly DecisionsSettings Config=new DecisionsSettings();private static readonly DecisionsUsage Use=new DecisionsUsage();
    private sealed class State
    {
        public PreparationProbe Preparation;
        public int ExecutionGoalRevision = -1, ExecutionRun;
        public string ExecutionGoalText = "";
        public int _lastTokens,AnsReceived,AnsStarted,AnsQueued,AnsContinued,AnsHeldReplaced,ExecutionRevision,AskedExecutionRevision,Reversals;
        public float _blockedSince=-1;public int _blockedFails;public FailKind _failKind;
        public string _runKey="",ExecutionReason="";public bool _runProgress,_reqEmergency;
        public AICandidate _nextHit;public AISkill _nextHitFor;public int _nextHitId,_nextHitRev;public float _nextHitReq;public bool _nextHitNow;
        public List<(string key,float t)> _left=new List<(string,float)>();
        public List<(float t,string from,string to,float ran,string how,string toKind,string fromKind)> _switches=new List<(float,string,string,float,string,string,string)>();
    }
    private static State S=new State();
    private sealed class PreparationProbe { public bool Prefetch, PrefetchCompleted; }
    private static void CancelPreparation(string why) { S.Preparation = null; _needCall = true; }
    private static object _req;private static float _reqStart,_retryAt,_heldAt,_heldReqStart,_queuedReqStart;private static int _reqRev,_reqId,_consecutiveFails,_heldId,_heldRev,_queuedRev,_queuedId;
    private static bool _reflex,_reqPrefetch,_prefetchSkillDone,_applyQueuedNow,_applying,_inCombat;
    private static string _reqTrigger="";
    private static AICandidate _held,_queued;private static AISkill _prefetchFor,_queuedFor;
    private static List<AICandidate> _reqCands;private static readonly List<float> Latencies=new List<float>();
    private static readonly RecentResults _recent=new RecentResults();private static readonly List<(int2 tile,float until)> _noPathAreas=new List<(int2,float)>();
    private static readonly Dictionary<string,(int count,float last,string why)> _fails=new Dictionary<string,(int,float,string)>();
    private const float NoPathSeconds=90,FailMemorySeconds=30;private const int NoPathRadius=5;
    public static DecisionsSettings Settings()=>Config;public static DecisionsUsage Usage()=>Use;public static bool KeyMissing()=>false;
    public static string StatusText(World w,Entity p)=>Goal.Text;
    public static string PrepNote=>"adapter";
    public static void SetBlockNote(string note,bool toolShort){}
    public static void AbortRequest(string why){_req=null;}
    public static bool TryItemPublic(string item,out ObjectID id,out string name)=>AIGoals.TryItemPublic(item,out id,out name);
    public static void CancelPendingChoices(string why){_held=_queued=null;S._nextHit=null;_queuedFor=null;}
    private static void DropQueued(string why){_queued=null;_queuedFor=null;}
    private static void SetReflex(bool value,string why){_reflex=value;}
    private static void AddUsage(int tokens,double usd){Use.usd+=usd;}
    private static string Trim(string s,int length)=>s??"";
    private static string MirrorNote(AICandidate c)=>"";
    private static bool EmergencyRule(string trigger,bool flag)=>flag;
    private static string Who(AICandidate c)=>c.kind;
    public static int StartedAnswers=>S.AnsStarted;public static bool NeedCall=>_needCall;
    public static bool CheckCost()=>CostAllows(Time.realtimeSinceStartup);
    public static float RetryAt=>_retryAt;
    public static AICandidate HeldChoice=>_held;public static AICandidate QueuedChoice=>_queued;
    public static void SeedPending(AISkill selected)
    {_held=new AICandidate{kind="probe",key="held"};_queued=new AICandidate{kind="probe",key="queued"};_queuedFor=selected;}
    public static void ResetAdapter()
    {S=new State();Revision=Applied=Discarded=Failed=Calls=0;_held=_queued=null;_req=null;_reqPrefetch=_prefetchSkillDone=_inCombat=false;_needCall=false;_recent.Clear();_fails.Clear();_noPathAreas.Clear();AIBody.Current=null;AIBody.Starts=AIBody.Stops=AIBody.LastRun=0;Observations.Clear();}
    public static void Response(World w,Entity p,List<AICandidate> candidates,AICandidate chosen,bool prefetch=false,int? requestRevision=null,AIDecisionsApi.Reply response=null)
    {
        _req=new object();_reqStart=Time.realtimeSinceStartup;_reqId++;_reqRev=requestRevision??Revision;_reqCands=candidates;_reqPrefetch=prefetch;_prefetchFor=AIBody.Current;
        AIDecisionsApi.Next=response??new AIDecisionsApi.Reply{Answer=new DAnswer{type="choice",choice=chosen.value}};
        HandleResponse(w,p,Time.realtimeSinceStartup+0.1f);
    }
    public static void ApplyChosen(World w,Entity p,AICandidate chosen)=>Apply(w,p,Time.realtimeSinceStartup,chosen);
}
public class ProbeSkill:AISkill
{public ProbeSkill(string key="probe"){Key=key;Name="probe";}public override bool Start(World w,Entity p,float now)=>true;public override BodyInput Tick(World w,Entity p,float now)=>BodyInput.Idle;}
public sealed class GotoSkill:ProbeSkill{}public sealed class StepSkill:ProbeSkill{}public sealed class HitOnceSkill:ProbeSkill{}public sealed class ApproachSkill:ProbeSkill{}public sealed class HoldSkill:ProbeSkill{}public sealed class ComboSkill:ProbeSkill{}public sealed class AttackSkill:ProbeSkill{}

