// AISkill — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.Text;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct BodyInput
{
    public float2 move;      // walking direction (zero = stand)
    public float2 face;      // facing direction (zero = keep the last facing)
    public bool primary;     // hold the primary use (left click)
    public bool secondary;   // hold the secondary use (right click; e.g. eat the held food)
    public int slot;         // hotbar slot to hold (-1 = keep)
    public bool aim;         // aim along `face` like a gamepad's right stick (ClientInput.wasAiming): a swing hits the object
                             // nearest pos + 0.95*face instead of pos + 0.6*facing (PlayerController.CalculateTargetHitPosition)
    public bool hasPoint;    // with aim: the aimed world point (mouseOrJoystickWorldPoint) is `point` instead of pos + face —
    public float2 point;     // a held tool / placeable acts on that tile (PlacementHandler.FindPlaceablePositionFromMouseOrJoystick; AIFarm)
    public int usePress;     // 코어-안전지대 단계 2: the id of an "interact with object" press (E: InteractWithObject_Pressed,
                             // a door). A new id makes the body send the bit for one whole ServerTick (AIBody.UseBitFor);
                             // the skill sees it sent when AIBody.UseDone reaches its id (6-문-여닫기.md 4.3)
    public int nudge;        // 검수 M1 (25-문단계멈춰서기): `move` is a settle correction with this id: the body sends it for exactly
                             // one server tick (AIBody.NudgeFor, TickNudge; 0 = an ordinary move)

    public static BodyInput Idle => new BodyInput { slot = -1 };
}

public enum SkillStatus { Running, Done, Failed, Cancelled }

public abstract class AISkill
{
    public string Name;
    public string Key = "";   // target key (e.g. "mine:3,-4", "attack:123", "goto:5,2"), used by the decision layer
    public string Tag = "";   // candidate group set by the decision layer (e.g. "explore:동" for an explore walk)
    public SkillStatus Status = SkillStatus.Running;
    public string Reason = "";
    public SkillEnd End;      // what the end means where the brain cares (AIOutcomes, refactor S2), next to the Reason sentence
    public int Run;           // run number (AIBody.StartSkill, per character): a "continue" answer applies to this run only (AIRunGate)
    public float StartTime;
    public float3 StartPos;
    public readonly List<string> Facts = new List<string>();

    // Returns false (and sets Status/Reason) when the start conditions are not met.
    public abstract bool Start(World server, Entity player, float now);
    public abstract BodyInput Tick(World server, Entity player, float now);
    public virtual void Cleanup(World server) { }
    // Short progress note for the "continue" candidate (what is left of this action), "" if nothing to say.
    public virtual string Progress(World server, Entity player) => "";
    // Estimated seconds until this selected action reaches its stated endpoint (done), from observed state; -1 = unknown. The decision layer
    // asks for the next action shortly before (prefetch) so the AI does not stand while the answer travels.
    public virtual float EtaSeconds(World server, Entity player) => -1f;
    // Combat stage 2: seconds this short close-fight action still takes, -1 unknown. Only for the "continue" option's fact
    // (an action that ends before the answer comes: the "continue" answer is then dropped and the body stands meanwhile).
    public virtual float RemainingSeconds(World server, Entity player) => -1f;

    private readonly HashSet<string> _observations = new HashSet<string>();
    public readonly List<string> ExecutionObservations = new List<string>();
    public int ObservationGoalRevision { get; private set; } = -1;
    public string ObservationGoalText { get; private set; } = "";
    private string _currentObservation = "";
    private float _observationReportedAt = -999f;
    private bool _observedThisTick;

    public void BindObservationGoal()
    {
        if (ObservationGoalRevision >= 0) return;
        ObservationGoalRevision = AIGoals.GoalRevision;
        ObservationGoalText = AIGoals.Goal.Text;
    }

    protected void ObserveOnce(string key, string reason)
    {
        BindObservationGoal();
        float now = Time.realtimeSinceStartup;
        _currentObservation = reason;
        _observedThisTick = true;
        if (!_observations.Add(key)) return;
        Facts.Add(reason);
        ExecutionObservations.Add(reason);
        _observationReportedAt = now;
        AIBrain.ObserveExecution(Name + ": " + reason, this);
    }

    internal void ObserveNavigation(string key, string reason) => ObserveOnce("navigation:" + key, reason);

    public void BeginObservationTick() => _observedThisTick = false;

    // Only a condition observed again by the running skill is refreshed. Historical
    // facts that stopped occurring do not turn into a periodic decision request.
    public void RefreshPendingObservation(float now)
    {
        if (Status != SkillStatus.Running || !_observedThisTick ||
            now - _observationReportedAt < AIBrain.ExecutionReviewSeconds) return;
        _observationReportedAt = now;
        AIBrain.ObserveExecution($"{Name}: {_currentObservation} (대기 조건 재관측, 현재 실행 {now - StartTime:F1}초 경과)", this);
    }

    protected BodyInput Finish(SkillStatus s, string reason)
    {
        Status = s;
        Reason = reason;
        return BodyInput.Idle;
    }

    protected BodyInput Finish(SkillStatus s, string reason, SkillEnd end)
    {
        End = end;
        return Finish(s, reason);
    }
}
