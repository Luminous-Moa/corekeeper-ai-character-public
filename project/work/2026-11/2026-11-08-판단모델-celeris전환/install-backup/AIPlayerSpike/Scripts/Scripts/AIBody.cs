// AIBody — the AI body's basic skills (look, inv, goto, mine, attack, stop, walktest) and the single
// input executor. Design: work/2026-11/2026-11-03-몸-기본-기술-설계.md (session 코어키퍼-AI-캐릭터).
//
// - One skill runs at a time. A new skill cancels the previous one ("cancelled").
// - The executor (AIBody.Tick) is the only place that decides the AI's ClientInput each tick. With no skill the
//   AI stands still (no movement, no buttons). The old STEP3 walking pattern runs only via "/ai walktest".
// - Skills judge progress/completion/failure from observed server state (position, tiles, enemy health,
//   inventory), not from "the command was accepted".
// - What the AI may "see" is decided in one place: AIVision. First rule: the same rectangle a human player sees
//   on this host's screen (measured from the host's game camera), including tiles behind walls as on screen.
//   Light/darkness is not applied yet (flagged in every look).
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

public static partial class AIBody
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public AISkill Current;
        public float2 _lastFace = new float2(0f, -1f);
        public int _slot = 0;
        public bool _primaryWasDown;
        public bool _secondaryWasDown;
        public float _lastTestGiveCheck;
        public int runSeq;   // the last run number given (AIRunGate)
        // interact-with-object press (단계 2): the asked id, the id sent, the server tick it is sent on (0 = not yet)
        public readonly PressQueue use = new PressQueue();   // 합치기 전 수정 C2: the rules (asked, sent, dropped) in Core
        public bool interactNoted;                           // 검수 D7: the "CanInteract not read" line was logged
        // a walk through an own door (AINavigator) steers face and press for this frame
        public int navFrame = -1; public float2 navFace; public int navUse;
        public int navNudge;                                 // 검수 M1: the door step's correction id this frame (with navMove)
        public readonly TickNudge nudger = new TickNudge();  // 검수 M1: a correction goes out for one server tick
        public int nudgeAsk; public int nudgeLogged;         // this frame's correction id (BodyInput.nudge); the last one logged
        // 검수 F1/F7: ... and holds the move input; the step itself (its phase, the door, open) for the facts
        public float2 navMove; public float navAt = -1f; public DoorPassState navStep; public int2 navDoor; public bool navOpen;
        public float2 navDir; public DoorPassState navHpStep; public int navHpOpen = -1;   // 재검수 R2b: the passage axis; health when the door was seen open
        public bool canInteract = true;   // 실측결함수정3 N5: the player's state takes an interact press now (read each frame)
        // live-run fix 3: the closing tail of a skill that ended next to an own door it left open (its real end kept)
        public bool tailOn; public SkillStatus tailStatus; public string tailReason = ""; public SkillEnd tailEnd;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Body);
    private static State S => StateOf(AIAgent.Cur);

    internal static AISkill Current { get => S.Current; set => S.Current = value; }
    internal const float WalkSpeed = AIMoveEstimate.WalkSpeed;   // tiles/s on open ground (measured, movetrace 2026-11-04: p50 5.72)
    internal static int HeldSlot => _slot;
    private static float2 _lastFace { get => S._lastFace; set => S._lastFace = value; }
    private static int _slot { get => S._slot; set => S._slot = value; }
    private static bool _primaryWasDown { get => S._primaryWasDown; set => S._primaryWasDown = value; }
    private static bool _secondaryWasDown { get => S._secondaryWasDown; set => S._secondaryWasDown = value; }
    private static float _lastTestGiveCheck { get => S._lastTestGiveCheck; set => S._lastTestGiveCheck = value; }

    // ---- interact-with-object press (코어-안전지대 단계 2, 6-문-여닫기.md): the bit InteractWithObject_Pressed is read
    // by the game once per server tick; a frame of a tick can overwrite another (AddCommandData keeps the last input of
    // a tick), so the bit is set on every frame of one ServerTick and cleared from the next. One press per asked id.
    internal static int UseDone => S.use.done;
    internal static int UseAsked => S.use.asked;
    internal static int NextUseId => S.use.asked + 1;   // the id a new press gets (a skill keeps sending it until UseDone reaches it)
    // 합치기 전 수정 C2: the sentence of a press the body dropped unsent ("" when id was not dropped)
    internal static string UseDroppedWhy(int id) => S.use.DroppedWhy(id);
    internal static void DropPress(string why, float now)
    {
        string l = S.use.Drop(why, now);
        if (l != null) AIPlayerSpikeMod.Log(l);
    }
    // the AI's own walk through a door asks face and press for this frame (AINavigator door step)
    internal static void NavDoorStep(float2 face, int usePress, float2 move, DoorPassState step, int2 door, float2 dir, bool open, int nudge = 0)
    {
        var s = S; s.navFrame = Time.frameCount; s.navFace = face; s.navUse = usePress; s.navMove = move; s.navNudge = nudge;
        s.navAt = Time.realtimeSinceStartup; s.navStep = step; s.navDoor = door; s.navDir = dir; s.navOpen = open;
    }
    private static bool DoorStepNow(State s) => s.navStep != null && Current != null && Current.Status == SkillStatus.Running && Time.realtimeSinceStartup - s.navAt <= 0.5f;
    // 재검수 R2b: the close of the running action's door step is blocked (AIDoorPass.BlockedNote; "" when not)
    internal static string DoorBlockedNote(IList<(float x, float y)> enemies, int hpNow)
    {
        var s = S;
        if (!DoorStepNow(s)) return "";
        return AIDoorPass.BlockedNote(s.navStep, s.navDoor.x, s.navDoor.y, s.navDir.x, s.navDir.y, s.navOpen, enemies, s.navHpStep == s.navStep ? s.navHpOpen : -1, hpNow);
    }
    // the running action waits with its swing until the close (attack, clearing; 재검수 R2a)
    private static bool WaitsSwing => Current is AttackSkill a && !a.NoDoorWait || Current is PickupSkill;
    // 검수 F7: the door step of the running action's walk, as a fact ("" when none ran in the last 0.5 s)
    internal static string DoorStepNote
    {
        get
        {
            var s = S;
            if (!DoorStepNow(s)) return "";
            return AIDoorPass.StepNote(s.navStep, s.navDoor.x, s.navDoor.y, s.navOpen, WaitsSwing);
        }
    }
    // 실측결함수정3 N2: the door the running action's door step is on (its close is part of that action: no separate
    // close option for it) and, while the step holds it open, the short fact for every other option
    // 검수 D1: only while the step holds the door (pressed, passing, closing) — a step that just failed holds nothing, so
    // the close option of that door comes back at once (it was gone for the 0.5 s the failed step stayed recent)
    internal static bool DoorStepOn(int2 t) { var s = S; return DoorStepNow(s) && AIDoorPass.HoldsDoor(s.navStep) && math.all(s.navDoor == t); }
    internal static string DoorLeftOpenNote
    {
        get
        {
            var s = S;
            if (!DoorStepNow(s) || !s.navOpen || !AIDoorPass.HoldsDoor(s.navStep)) return "";
            return AIDoorPass.LeftOpenNote(s.navDoor.x, s.navDoor.y);
        }
    }
    // 실측결함수정3 N3: what the body holds and its state, for a door step's trace (the slow / failed steps all held an
    // empty hand; whether that matters is to be seen)
    internal static string HandNote(World server, Entity player)
    {
        try
        {
            var em = server.EntityManager;
            string held = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID.ToString() : "?";
            string st = em.HasComponent<PlayerState.PlayerStateCD>(player) ? em.GetComponentData<PlayerState.PlayerStateCD>(player).AllStates().ToString() : "?";
            return $"slot={_slot} held={held} state={st} canInteract={S.canInteract}";
        }
        catch (Exception e) { return "hand ?" + e.GetType().Name; }
    }

    // 실측결함수정3 N5: the game takes the interact press only when the player's state allows interacting
    // (PlayerController.CurrentStateAllowInteractions, isTryingToInteractWithObject: false in Release — the state after a
    // melee swing — and in death, spawning, teleporting...; ResolveInteractorActiveSystem turns the interactor off then).
    // The tail close right after a kill pressed in that state (t=1564.1). A press waits for a state that takes it (the
    // door step stands and watches meanwhile; its own time limits stay).
    internal static bool CanInteract(World server, Entity player)
    {
        try
        {
            var em = server.EntityManager;
            if (!em.HasComponent<PlayerState.PlayerStateCD>(player) || !em.HasComponent<PlayerGhost>(player) || !em.HasComponent<PlayerEquipment.EquipmentSlotCD>(player)
                || !em.HasComponent<ClientInput>(player) || !em.HasComponent<PlayerState.PlayerSleepStateCD>(player))
            {
                // 검수 D7: said once — the wait for a state that takes the press is off then (canInteract=True in the trace)
                NoteInteractUnread($"component missing (PlayerStateCD {em.HasComponent<PlayerState.PlayerStateCD>(player)}, PlayerGhost {em.HasComponent<PlayerGhost>(player)}, EquipmentSlotCD {em.HasComponent<PlayerEquipment.EquipmentSlotCD>(player)}, ClientInput {em.HasComponent<ClientInput>(player)}, PlayerSleepStateCD {em.HasComponent<PlayerState.PlayerSleepStateCD>(player)})");
                return true;
            }
            WorldInfoCD wi = default;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldInfoCD>())) if (q.CalculateEntityCount() == 1) wi = q.GetSingleton<WorldInfoCD>();
            var pg = em.GetComponentData<PlayerGhost>(player); var ps = em.GetComponentData<PlayerState.PlayerStateCD>(player);
            var es = em.GetComponentData<PlayerEquipment.EquipmentSlotCD>(player); var ci = em.GetComponentData<ClientInput>(player);
            var sl = em.GetComponentData<PlayerState.PlayerSleepStateCD>(player);
            return PlayerController.CurrentStateAllowInteractions(in wi, in pg, in ps, in es, false, in ci, in sl, true);
        }
        catch (Exception e) { NoteInteractUnread("exception " + e.GetType().Name); return true; }
    }
    private static void NoteInteractUnread(string why)
    {
        if (S.interactNoted) return;
        S.interactNoted = true;
        AIPlayerSpikeMod.Log($"BODY: CanInteract not read ({why}); taken as true — the interact press does not wait for the player state (logged once)");
    }
    // 검수 M1: called by the input sender with the tick the input goes to: a settle correction (BodyInput.nudge) moves
    // the body one server tick — its move on every frame of the first tick, none after (TickNudge). zeroAhead: write
    // the next two ticks without a move too (a tick with no frame reuses the last older input).
    internal static void NudgeFor(uint tick, ref ClientInput ci, out bool zeroAhead)
    {
        var s = S;
        float mx = ci.movementDirection.x, my = ci.movementDirection.y;
        s.nudger.Move(s.nudgeAsk, ref mx, ref my, tick, out zeroAhead);
        ci.movementDirection = new float2(mx, my);
        if (zeroAhead && s.nudgeLogged != s.nudger.id) { s.nudgeLogged = s.nudger.id; AIPlayerSpikeMod.Log($"BODY: settle nudge #{s.nudger.id} ({mx:F2},{my:F2}) on server tick {tick} only"); }
    }

    // called by the input sender with the tick the input goes to: whether to set the bit on it
    internal static bool UseBitFor(uint tick)
    {
        var s = S;
        bool bit = s.use.Bit(tick, s.canInteract, out bool first);   // 실측결함수정3 N5: waits for a state the game takes it in
        if (first) AIPlayerSpikeMod.Log($"BODY: interact-with-object press #{s.use.asked} on server tick {tick}");
        return bit;
    }

    public static bool WalkTestActive => Current is WalkTestSkill && Current.Status == SkillStatus.Running;

    public static void Reset()
    {
        AIInventory.Reset();
        DropPress("AI 초기화", Time.realtimeSinceStartup);   // 합치기 전 수정 C2
        Current = null;
        // review S4: a closing tail does not outlive the skill it belonged to (the next skill would end at once with the old end)
        S.tailOn = false;
        try { AIEdge.TailAbort(); } catch { }
        _lastFace = new float2(0f, -1f);
        _slot = 0;
        _primaryWasDown = false;
    }

    // Called once per frame while the AI is present. Returns the one input for this tick.
    public static ClientInput Tick(World server, Entity player, float now)
    {
        BodyInput bi = BodyInput.Idle;
        var em0 = server.EntityManager;
        int myHp = em0.Exists(player) && em0.HasComponent<HealthCD>(player) ? em0.GetComponentData<HealthCD>(player).health : 1;
        // live-run fix 3: an own door the AI goes into is owed a close
        try { AIEdge.TrackDoor(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: door track failed", e); }
        if (Current != null && Current.Status == SkillStatus.Running && myHp <= 0)
        {
            // The AI is down (death / respawn at the core): no skill can continue from where it was.
            Current.Status = SkillStatus.Failed;
            Current.Reason = "AI가 쓰러짐(체력 0)";
            Current.End = SkillEnd.Down;
            EndSkill(server, player, now);
        }
        Current?.BeginObservationTick();
        if (Current != null && Current.Status == SkillStatus.Running && S.tailOn)
        {
            // live-run fix 3: the skill's own work is over; it closes the own door it passed before its end is reported
            string fact = ""; bool done = false;
            try { bi = AIEdge.TailTick(server, player, now, out done, out fact); }
            catch (Exception e) { AIPlayerSpikeMod.LogError("edge: door tail failed", e); done = true; fact = "지난 문 닫기 오류: " + e.GetType().Name; }
            if (done)
            {
                Current.Status = S.tailStatus; Current.End = S.tailEnd; Current.Reason = S.tailReason + (string.IsNullOrEmpty(fact) ? "" : " · " + fact);
                if (!string.IsNullOrEmpty(fact)) Current.Facts.Add(fact);
                S.tailOn = false;
                EndSkill(server, player, now);
                bi = BodyInput.Idle;
            }
        }
        else if (Current != null && Current.Status == SkillStatus.Running)
        {
            try
            {
                bi = Current.Tick(server, player, now);
            }
            catch (Exception e)
            {
                AIPlayerSpikeMod.LogError($"SKILL {Current.Name}: exception; failing the skill", e);
                Current.Status = SkillStatus.Failed;
                Current.Reason = "오류: " + e.GetType().Name;
                bi = BodyInput.Idle;
            }
            if (Current.Status != SkillStatus.Running)
            {
                bool tail = false;
                try { tail = AIEdge.TailStart(server, player, Current, myHp <= 0); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: door tail start failed", e); }
                DropPress($"기술 {Current.Name} 끝남", now);   // 합치기 전 수정 C2: its press does not go out later
                if (tail)
                {
                    // live-run fix 3: open -> through -> close is one bundle: the end waits for the close (its fact joins the end)
                    S.tailOn = true; S.tailStatus = Current.Status; S.tailReason = Current.Reason; S.tailEnd = Current.End;
                    Current.Status = SkillStatus.Running;
                }
                else EndSkill(server, player, now);
                bi = BodyInput.Idle;
            }
        }

        if (now - _lastTestGiveCheck >= 2f)
        {
            _lastTestGiveCheck = now;
            AICharacterStore.TestOnlyGiveFromFile(server, player);
            // an item left on the cursor (crafting output slot) blocks every tool and weapon: put it into the bag
            if (!(Current is CraftSkill && Current.Status == SkillStatus.Running))
            {
                try
                {
                    var stowed = AICraft.StowCursorItem(server, player, out int stowAmt);
                    if (stowed != ObjectID.None) AIPlayerSpikeMod.Log($"BODY: item on the cursor (crafting output slot): {stowed} x{stowAmt}; moving it into the bag (client MoveOrDropItem)");
                }
                catch (Exception e) { AIPlayerSpikeMod.LogError("body: stow cursor item failed", e); }
            }
            try { AITunnel.RecordHeld(server, player); } catch (Exception e) { AIPlayerSpikeMod.LogError("tunnel: tool bonus record failed", e); }
        }
        try { AIMemory.Observe(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("memory: observe failed", e); }
        try { AIVitals.Tick(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("vitals: tick failed", e); }
        try { AIPlanner.Tick(server, player, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("planner: tick failed", e); }

        // an own-door step of the walk (AINavigator) this frame: its face and press
        // 검수 F1: and the move — while the door step runs it alone holds the move input (a skill's straight step toward its
        // target between the step's stand-still frames pushed the body off the press spot; the close timed out open)
        if (S.navFrame == Time.frameCount)
        {
            if (math.lengthsq(S.navFace) > 1e-4f) bi.face = S.navFace; if (S.navUse > bi.usePress) bi.usePress = S.navUse;
            if (Current != null && Current.Status == SkillStatus.Running) { var mv = AIDoorPass.BodyMove(true, S.navMove.x, S.navMove.y, bi.move.x, bi.move.y); bi.move = new float2(mv.x, mv.y); bi.nudge = S.navNudge; }
            if (S.navOpen && S.navHpStep != S.navStep) { S.navHpStep = S.navStep; S.navHpOpen = myHp; }   // 재검수 R2b: health when the door opened
        }
        if (bi.usePress > 0) S.use.Ask(bi.usePress, now);
        if (S.use.asked > S.use.done) S.canInteract = CanInteract(server, player);   // 실측결함수정3 N5 (read only while a press waits)
        if (S.use.Waiting) { string dl = S.use.CheckWait(now, HandNote(server, player)); if (dl != null) AIPlayerSpikeMod.Log(dl); }   // C2: the wait's limit
        if (math.lengthsq(bi.face) > 1e-4f) _lastFace = math.normalize(bi.face);
        else if (math.lengthsq(bi.move) > 1e-4f) _lastFace = math.normalize(bi.move);
        if (bi.slot >= 0 && bi.slot <= 9) _slot = bi.slot;

        S.nudgeAsk = bi.nudge;   // 검수 M1: the input sender keeps a correction to one server tick (NudgeFor)
        ClientInput ci = default;
        ci.movementDirection = math.lengthsq(bi.move) > 1e-4f ? math.normalize(bi.move) : float2.zero;
        ci.facingDirection = Direction.FromVector(new Vector3(_lastFace.x, 0f, _lastFace.y), 0.01f);
        ci.targetingDirection = _lastFace;
        ci.aimDirection = _lastFace;
        ci.wasAiming = bi.aim;
        ci.equippedSlotIndex = (byte)_slot;
        var em = server.EntityManager;
        if (em.Exists(player) && em.HasComponent<LocalTransform>(player))
        {
            float3 p = em.GetComponentData<LocalTransform>(player).Position;
            ci.mouseOrJoystickWorldPoint = bi.hasPoint ? bi.point : p.xz + _lastFace;
            // A real client sends its camera position every input (SendClientInputSystem: FollowPlayer camera =
            // the player's own position). The server copies it to PlayerGhost.cameraPosition and uses it for ghost
            // relevancy, DisableEntitiesSystem (entities far from every camera are disabled) and DistanceToPlayer.
            // Left unset it stayed (0,0) = the core, so entities near a far-away AI (e.g. a damaged wall) were
            // disabled and mining there lost its damage (U.far-mining).
            ci.cameraPosition = p.xz;
        }
        if (bi.primary)
        {
            ci.SetButtonState(CommandInputButtonStateNames.Interact_HeldDown, true);
            if (!_primaryWasDown) ci.SetButtonState(CommandInputButtonStateNames.Interact_Pressed, true);
        }
        _primaryWasDown = bi.primary;
        if (bi.secondary)
        {
            ci.SetButtonState(CommandInputButtonStateNames.SecondInteract_HeldDown, true);
            if (!_secondaryWasDown) ci.SetButtonState(CommandInputButtonStateNames.SecondInteract_Pressed, true);
        }
        _secondaryWasDown = bi.secondary;
        return ci;
    }

    public static void StartSkill(World server, Entity player, AISkill s, float now)
    {
        if (Current != null && Current.Status == SkillStatus.Running)
        {
            Current.Status = SkillStatus.Cancelled;
            Current.Reason = "새 기술 " + s.Name + "(으)로 바뀜";
            EndSkill(server, player, now);
        }
        DropPress($"새 기술 {s.Name} 시작", now);   // 합치기 전 수정 C2 (one left with no running skill: a cut tail, a reset)
        var em = server.EntityManager;
        s.StartTime = now;
        s.Run = ++S.runSeq;
        s.BindObservationGoal();
        s.StartPos = em.Exists(player) ? em.GetComponentData<LocalTransform>(player).Position : float3.zero;
        AIPlayerSpikeMod.Log($"SKILL {s.Name} START at ({s.StartPos.x:F2},{s.StartPos.z:F2})");
        bool ok;
        try { ok = s.Start(server, player, now); }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"SKILL {s.Name}: start threw", e);
            ok = false;
            s.Status = SkillStatus.Failed;
            s.Reason = "시작 오류: " + e.GetType().Name;
        }
        Current = s;
        if (!ok || s.Status != SkillStatus.Running)
        {
            if (s.Status == SkillStatus.Running) { s.Status = SkillStatus.Failed; }
            EndSkill(server, player, now);
        }
    }

    // the closing tail runs now (the skill's own work is over; its end waits for the close of the own door it passed)
    internal static bool TailRunning => S.tailOn && Current != null && Current.Status == SkillStatus.Running;

    // Review F2 (2026-11-06): a stop the program makes (goal done / stalled / cleared, self-defence over, the upper LLM's
    // hold, call failures, cost cap, free time over) does not cut open -> through -> close: a running closing tail
    // finishes (at most ~6 s, the Close step's timeout) and a skill stopped next to an owed open own door closes it
    // first. hard = the host's /ai stop or the AI leaving: those cut it (the door stays owed; the close option says so).
    // A new choice of the action model cuts it too (StartSkill).
    public static void Stop(World server, Entity player, float now, string reason, bool hard = false)
    {
        if (Current == null || Current.Status != SkillStatus.Running) return;
        var s = S;
        switch (AIBaseGuard.OnStop(hard, s.tailOn))
        {
            case AIBaseGuard.TailOnStop.KeepTail:
                s.tailReason = AIBaseGuard.AddReason(s.tailReason, $"{reason}(지난 문 닫기는 마저 함)");
                AIPlayerSpikeMod.Log($"SKILL {Current.Name}: stop '{reason}' while closing the own door it passed; the close goes on (open -> through -> close is one bundle)");
                return;
            case AIBaseGuard.TailOnStop.TryTail:
            {
                Current.Status = SkillStatus.Cancelled;
                Current.Reason = reason;
                bool tail = false;
                try { tail = AIEdge.TailStart(server, player, Current, IsDown(server, player)); } catch (Exception e) { AIPlayerSpikeMod.LogError("edge: door tail start (stop) failed", e); }
                DropPress($"멈춤({reason})", now);   // 합치기 전 수정 C2
                if (tail)
                {
                    s.tailOn = true; s.tailStatus = Current.Status; s.tailReason = Current.Reason; s.tailEnd = Current.End;
                    Current.Status = SkillStatus.Running;
                    AIPlayerSpikeMod.Log($"SKILL {Current.Name}: stop '{reason}' next to an own door it left open; closing it before the end is reported");
                    return;
                }
                EndSkill(server, player, now);
                return;
            }
            default:
                Current.Status = SkillStatus.Cancelled;
                Current.Reason = reason;
                EndSkill(server, player, now);
                return;
        }
    }

    private static bool IsDown(World server, Entity player)
    {
        var em = server.EntityManager;
        return em.Exists(player) && em.HasComponent<HealthCD>(player) && em.GetComponentData<HealthCD>(player).health <= 0;
    }

    private static void EndSkill(World server, Entity player, float now)
    {
        var s = Current;
        if (s == null) return;
        DropPress($"기술 {s.Name} 끝남", now);   // 합치기 전 수정 C2
        if (S.tailOn)
        {
            // the closing tail cut (a new skill, stop, down): the skill's own end stands, with the cut as a fact; the door
            // stays owed (the close option says so)
            string cut = s.Reason;
            s.Status = S.tailStatus; s.End = s.End == SkillEnd.Down ? SkillEnd.Down : S.tailEnd; s.Reason = S.tailReason + $" · 지난 문 닫기 끊김({cut}) — 문이 열린 채일 수 있음";
            S.tailOn = false;
            try { AIEdge.TailAbort(); } catch { }
        }
        try { s.Cleanup(server); } catch (Exception e) { AIPlayerSpikeMod.LogError($"SKILL {s.Name}: cleanup failed", e); }
        var em = server.EntityManager;
        float3 end = em.Exists(player) ? em.GetComponentData<LocalTransform>(player).Position : float3.zero;
        string status = s.Status == SkillStatus.Done ? "완료" : s.Status == SkillStatus.Failed ? "실패" : s.Status == SkillStatus.Cancelled ? "취소" : "진행 중";
        string facts = string.Join("; ", s.Facts);
        AIPlayerSpikeMod.Log($"SKILL {s.Name} {s.Status.ToString().ToUpperInvariant()} after {now - s.StartTime:F1}s: reason='{s.Reason}' start=({s.StartPos.x:F2},{s.StartPos.z:F2}) end=({end.x:F2},{end.z:F2}) facts=[{facts}]");
        if (!(s is LookSkill) && !(s is InvSkill))
        {
            AIPlayerSpikeMod.Reply($"AI {s.Name} {status}{(string.IsNullOrEmpty(s.Reason) ? "" : ": " + s.Reason)} ({now - s.StartTime:F1}초)", ChatKind.Status);
        }
        Current = null;
        try { AIBrain.OnSkillEnded(s, now); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: OnSkillEnded failed", e); }
    }

    // Test-only commands (2026-11-04): everything except goal and stop is a direct order or a test/diagnostic tool, which
    // normal play must not show (Decisions picks actions; the host only sets goals). They work only while the switch
    // file AIPlayers/testcmds.on exists (checked at each command); nothing is deleted.
    public static readonly string[] TestCommands =
    {
        "look", "보기", "inv", "가방", "goto", "가기", "mine", "캐기", "attack", "공격", "walktest", "dig", "파기", "mem", "기억",
        "where", "tiles", "plan", "hostto", "nonew", "heals", "give", "hp", "bridge", "craftinfo", "breaktest", "craft",
        "place", "take", "findwall", "rmstation", "route", "buff", "arena", "hunger", "kgdump", "baseprobe", "edge",
    };

    internal static float3 HostPosition(World server, Entity aiPlayer)
    {
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                // the human host: not this AI and not any other AI character (several AIs: AIAgents.cs)
                if (e == aiPlayer || AIPlayerSpikeMod.IsAiPlayer(e)) continue;
                return em.GetComponentData<LocalTransform>(e).Position;
            }
        }
        return float3.zero;
    }

    // --------------------------------------------------------------- shared helpers for skills

    internal static float3 Pos(World server, Entity player)
    {
        return server.EntityManager.GetComponentData<LocalTransform>(player).Position;
    }

    internal static int2 TileOf(float3 p)
    {
        return new int2((int)math.round(p.x), (int)math.round(p.z));
    }

    // Hotbar slot (0..9) to hold: best mining pick for mining, a melee weapon for attacking, else an empty slot
    // (bare hands). -1 when none fits.
    internal static int ChooseSlot(World server, Entity player, bool forMining, out string what)
    {
        if (AIInventory.TryChosenHand(server, player, forMining, out int chosen, out what, out _)) return chosen;
        what = "맨손";
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return -1;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int empty = -1, brokenPick = -1, brokenWeapon = -1;
        for (int i = 0; i < 10 && i < buf.Length; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None)
            {
                if (empty < 0) empty = i;
                continue;
            }
            ObjectInfo info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            if (info == null) continue;
            // a broken pick (PlayerController.ItemIsBroken: durability item at 0; the hotbar shows it broken)
            if (forMining && AIHandCapabilities.IsMiningTool(info.objectType.ToString()) && PlayerController.ItemIsBroken(buf[i].objectData)) { if (brokenPick < 0) brokenPick = i; continue; }
            if (forMining && AIHandCapabilities.IsMiningTool(info.objectType.ToString())) { what = id.ToString(); return i; }
            if (!forMining && AIHandCapabilities.IsWeapon(info.objectType.ToString()) && PlayerController.ItemIsBroken(buf[i].objectData)) { if (brokenWeapon < 0) brokenWeapon = i; continue; }
            if (!forMining && AIHandCapabilities.IsWeapon(info.objectType.ToString())) { what = id.ToString(); return i; }
        }
        if (brokenPick >= 0) { what = buf[brokenPick].objectID + "(부서짐, 내구도 0)"; return brokenPick; }
        if (brokenWeapon >= 0) { what = buf[brokenWeapon].objectID.ToString(); return brokenWeapon; }   // name only: AttackSkill parses it
        return empty;
    }

    // ---- holding a tool the way a player does (2026-11-04 막힘-멈춤-정리, U.no-pick-full-hotbar)
    // ChatGPT had no pick and slots 0-9 full of dirt and seeds: ChooseSlot found neither a pick nor an empty hand slot,
    // every dig failed at once ("손에 들 빈 칸이나 곡괭이가 없음") and dig options were still offered. A player drags
    // a pick from the bag into the hotbar, or drags a hotbar item into the bag to have an empty hand. HandSlot does
    // that (as the weapon swap in WeaponSlotForAttack); CanHold says beforehand whether it is possible (candidates).

    // End of the main inventory (hotbar 0-9 + bag): InventoryBuffer[0] size (bag upgrades included).
    internal static int MainEnd(EntityManager em, Entity player, int bufLen)
    {
        try
        {
            if (em.HasBuffer<InventoryBuffer>(player))
            {
                var ib = em.GetBuffer<InventoryBuffer>(player, true);
                if (ib.Length > 0 && ib[0].size > 10) return math.min(bufLen, ib[0].startIndex + ib[0].size);
            }
        }
        catch { }
        return math.min(bufLen, 40);
    }

    private static bool IsTool(ObjectInfo info) => info != null && (AIHandCapabilities.IsMiningTool(info.objectType.ToString()) || AIHandCapabilities.IsWeapon(info.objectType.ToString()));

    // The hotbar slot a bag item may be moved into: an empty one, else the last one holding neither pick nor weapon.
    private static int HotbarRoom(DynamicBuffer<ContainedObjectsBuffer> buf)
    {
        for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == ObjectID.None) return i;
        for (int i = math.min(9, buf.Length - 1); i >= 0; i--) if (!IsTool(PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation))) return i;
        return -1;
    }

    private static int BagEmpty(DynamicBuffer<ContainedObjectsBuffer> buf, int end)
    {
        for (int i = 10; i < end; i++) if (buf[i].objectID == ObjectID.None) return i;
        return -1;
    }

    private static int BagTool(DynamicBuffer<ContainedObjectsBuffer> buf, int end, ObjectType type)
    {
        for (int i = 10; i < end; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info != null && (type == ObjectType.MeleeWeapon ? AIHandCapabilities.IsWeapon(info.objectType.ToString()) : type == ObjectType.MiningPick ? AIHandCapabilities.IsMiningTool(info.objectType.ToString()) : info.objectType == type) && !PlayerController.ItemIsBroken(buf[i].objectData)) return i;
        }
        return -1;
    }

    // Read only: what the AI would hold to mine (forMining) or fight, and whether it can hold anything at all.
    // what = the tool's name or "맨손"; false with why when there is no tool and no hand slot can be made empty.
    internal static bool CanHold(World server, Entity player, bool forMining, out string what, out string why)
    {
        if (AIInventory.TryChosenHand(server, player, forMining, out int chosen, out what, out why)) return chosen >= 0;
        why = "";
        int s = ChooseSlot(server, player, forMining, out what);
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return s >= 0;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = MainEnd(em, player, buf.Length);
        bool working = s >= 0 && buf[s].objectID != ObjectID.None && !PlayerController.ItemIsBroken(buf[s].objectData);
        if (working) return true;
        int bt = BagTool(buf, end, forMining ? ObjectType.MiningPick : ObjectType.MeleeWeapon);
        if (bt >= 0 && (s >= 0 || HotbarRoom(buf) >= 0)) { what = buf[bt].objectID.ToString(); return true; }
        if (s >= 0) return true;   // a broken tool in the hotbar, or an empty hand slot (bare hands)
        if (BagEmpty(buf, end) >= 0 && HotbarRoom(buf) >= 0) { what = "맨손"; return true; }
        what = "";
        why = $"{(forMining ? "곡괭이" : "무기")}가 없고 손 칸 10칸이 모두 차 있으며 가방에도 빈 칸이 없어 {(forMining ? "맨손으로도 캘" : "맨손으로도 칠")} 수 없음(가방이나 손 칸을 비우면 됨)";
        return false;
    }

    // Read only: whether an item lying on the ground could go into the bag (hotbar + bag, the extension included): an
    // empty slot, or a stack of the same stackable item (variation not known for a seen item: any stack of that id).
    // A full bag made "pick up the WoodMiningPick" the chosen option 28 times in a row, each walk ending at once with
    // nothing picked up (2026-11-04 막힘-멈춤-정리 2단계, test 2 with the bag full).
    internal static bool BagHasRoomFor(World server, Entity player, ObjectID id)
    {
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return true;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = MainEnd(em, player, buf.Length);
        ObjectInfo info = PugDatabase.GetObjectInfo(id, 0);
        bool stackable = info != null && info.isStackable;
        for (int i = 0; i < end; i++)
        {
            if (buf[i].objectID == ObjectID.None) return true;
            if (stackable && buf[i].objectID == id) return true;
        }
        return false;
    }

    // The slot to mine with, moving items as a player would: a working pick in the hotbar; else a working pick from the
    // bag into the hotbar (the broken pick's slot, an empty slot or the last non-tool slot); else a broken pick or an
    // empty hand slot (bare hands); else a hotbar item into an empty bag slot (bare hands). -1 when none of these.
    internal static int HandSlot(World server, Entity player, bool forMining, out string what, out string swap, bool bareBeforeBroken = false)
    {
        swap = "";
        if (AIInventory.TryChosenHand(server, player, forMining, out int chosen, out what, out string chosenWhy)) { swap = chosenWhy; return chosen; }
        if (forMining && bareBeforeBroken) return PickupHandSlot(server, player, out what, out swap);
        if (!forMining) return WeaponSlotForAttack(server, player, out what, out swap);
        int s = ChooseSlot(server, player, true, out what);
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return s;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
        int end = MainEnd(em, player, buf.Length);
        bool hotPick = s >= 0 && buf[s].objectID != ObjectID.None;
        if (hotPick && !PlayerController.ItemIsBroken(buf[s].objectData)) return s;
        int bag = BagTool(buf, end, ObjectType.MiningPick);
        if (bag >= 0)
        {
            int hot = hotPick ? s : HotbarRoom(buf);
            if (hot >= 0)
            {
                var a = buf[hot]; var b = buf[bag];
                buf[hot] = b; buf[bag] = a;
                what = b.objectID.ToString();
                swap = $"swapped {b.objectID} from bag slot {bag} into hotbar slot {hot} (moved {(a.objectID == ObjectID.None ? "nothing" : a.objectID.ToString())} to slot {bag})";
                AIPlayerSpikeMod.Log($"BODY: pick for mining: {swap}");
                return hot;
            }
        }
        if (s >= 0) return s;
        return FreeHand(em, player, buf, end, out what, out swap);
    }

    // 단계 3 review S3, picking a placed object up: a working pick (moved from the bag as HandSlot does), else the empty
    // hand, a broken pick only when no hand slot can be made empty (AIBaseGuard.PickupHoldChoice)
    private static int PickupHandSlot(World server, Entity player, out string what, out string swap)
    {
        swap = "";
        int s = ChooseSlot(server, player, true, out what);
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return s;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        int end = MainEnd(em, player, buf.Length);
        bool working = s >= 0 && buf[s].objectID != ObjectID.None && !PlayerController.ItemIsBroken(buf[s].objectData);
        bool brokenHot = s >= 0 && buf[s].objectID != ObjectID.None && !working;
        if (!working && BagTool(buf, end, ObjectType.MiningPick) >= 0) return HandSlot(server, player, true, out what, out swap);   // a working pick from the bag
        int empty = -1; for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == ObjectID.None) { empty = i; break; }
        bool canFree = BagEmpty(buf, end) >= 0;
        string brokenName = brokenHot ? what : "";
        switch (AIBaseGuard.PickupHoldChoice(working, empty >= 0, canFree, brokenHot))
        {
            case AIBaseGuard.PickupHold.WorkingPick: return s;
            case AIBaseGuard.PickupHold.EmptyHand: what = "맨손"; return empty;
            case AIBaseGuard.PickupHold.FreeHand:
            {
                int h = FreeHand(em, player, em.GetBuffer<ContainedObjectsBuffer>(player), end, out what, out swap);
                if (h >= 0 || !brokenHot) return h;
                what = brokenName; return s;
            }
            case AIBaseGuard.PickupHold.BrokenPick: return s;
        }
        return -1;
    }

    // A hotbar item (not a pick or weapon) into an empty bag slot, so an empty hand slot exists (bare hands).
    private static int FreeHand(EntityManager em, Entity player, DynamicBuffer<ContainedObjectsBuffer> buf, int end, out string what, out string swap)
    {
        what = "맨손"; swap = "";
        int j = BagEmpty(buf, end);
        int h = -1;
        for (int i = math.min(9, buf.Length - 1); i >= 0 && j >= 0; i--) if (!IsTool(PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation))) { h = i; break; }
        if (j < 0 || h < 0) return -1;
        var a = buf[h]; var b = buf[j];
        buf[h] = b; buf[j] = a;
        swap = $"moved {a.objectID} x{a.amount} from hotbar slot {h} into bag slot {j} for an empty hand";
        AIPlayerSpikeMod.Log($"BODY: {swap}");
        return h;
    }

    // A working mining pick (durability above 0) anywhere in the inventory.
    internal static bool HasWorkingPick(World server, Entity player, out bool brokenOnly)
    {
        brokenOnly = false;
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || !AIHandCapabilities.IsMiningTool(info.objectType.ToString())) continue;
            if (!PlayerController.ItemIsBroken(buf[i].objectData)) return true;
            brokenOnly = true;
        }
        return false;
    }

    // For fighting (user decision 2026-11-04: never fight bare-handed when a weapon is in the bag): a melee weapon in
    // the hotbar (slots 0-9) is used as is; a weapon only in the bag is swapped into the hotbar (an empty hotbar slot,
    // else the last hotbar slot that holds neither a pick nor a weapon), like a player dragging it there. Without any
    // weapon: an empty hotbar slot (bare hands), or -1.
    internal static int WeaponSlotForAttack(World server, Entity player, out string what, out string swap)
    {
        swap = "";
        if (AIInventory.TryChosenHand(server, player, false, out int chosen, out what, out string chosenWhy)) { swap = chosenWhy; return chosen; }
        int s = ChooseSlot(server, player, false, out what);
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return s;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
        bool hotWeapon = s >= 0 && s < buf.Length && buf[s].objectID != ObjectID.None;
        bool hotBroken = hotWeapon && PlayerController.ItemIsBroken(buf[s].objectData);
        if (hotWeapon && !hotBroken) return s; // a working weapon in the hotbar
        int bag = -1;
        for (int i = 10; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || !AIHandCapabilities.IsWeapon(info.objectType.ToString())) continue;
            bool br = PlayerController.ItemIsBroken(buf[i].objectData);
            if (!br) { bag = i; break; }
            if (bag < 0 && !hotWeapon) bag = i;   // a broken one only when the hotbar has no weapon at all
        }
        if (bag < 0) return s >= 0 ? s : FreeHand(em, player, buf, MainEnd(em, player, buf.Length), out what, out swap);   // no weapon, no empty hand slot: bare hands after moving a hotbar item into the bag
        int hot = hotBroken ? s : -1;   // a working weapon from the bag takes the broken one's hotbar slot
        if (hot < 0) for (int i = 0; i < 10 && i < buf.Length; i++) if (buf[i].objectID == ObjectID.None) { hot = i; break; }
        if (hot < 0)
        {
            for (int i = 9; i >= 0; i--)
            {
                ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
                if (info == null || (!AIHandCapabilities.IsMiningTool(info.objectType.ToString()) && !AIHandCapabilities.IsWeapon(info.objectType.ToString()))) { hot = i; break; }
            }
        }
        if (hot < 0) return s;
        var a = buf[hot]; var b = buf[bag];
        buf[hot] = b; buf[bag] = a;
        what = b.objectID.ToString();
        swap = $"swapped {b.objectID} from bag slot {bag} into hotbar slot {hot} (moved {(a.objectID == ObjectID.None ? "nothing" : a.objectID.ToString())} to slot {bag})";
        AIPlayerSpikeMod.Log($"SKILL 공격: {swap}");
        return hot;
    }

    // The first melee weapon anywhere in the inventory (for candidate descriptions), None if none.
    internal static ObjectID FindWeapon(World server, Entity player) => FindWeapon(server, player, out _);

    // The melee weapon the attack skill will use: a working one first, else a broken one (durability 0,
    // PlayerController.ItemIsBroken). broken = only broken ones in the inventory.
    internal static ObjectID FindWeapon(World server, Entity player, out bool broken)
    {
        broken = false;
        if (AIInventory.TryChosenHand(server, player, false, out int chosen, out _, out _)) return SlotItem(server, player, chosen, out broken);
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return ObjectID.None;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        ObjectID brokenOne = ObjectID.None;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(buf[i].objectID, buf[i].variation);
            if (info == null || !AIHandCapabilities.IsWeapon(info.objectType.ToString())) continue;
            if (!PlayerController.ItemIsBroken(buf[i].objectData)) return buf[i].objectID;
            if (brokenOne == ObjectID.None) brokenOne = buf[i].objectID;
        }
        broken = brokenOne != ObjectID.None;
        return brokenOne;
    }

    // What a slot holds now (None: empty or no such slot) and whether it is broken (durability 0).
    internal static ObjectID SlotItem(World server, Entity player, int slot, out bool broken)
    {
        broken = false;
        var em = server.EntityManager;
        if (slot < 0 || !em.HasBuffer<ContainedObjectsBuffer>(player)) return ObjectID.None;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        if (slot >= buf.Length) return ObjectID.None;
        broken = buf[slot].objectID != ObjectID.None && PlayerController.ItemIsBroken(buf[slot].objectData);
        return buf[slot].objectID;
    }

    internal static bool SlotBroken(World server, Entity player, int slot)
    {
        var em = server.EntityManager;
        if (slot < 0 || !em.HasBuffer<ContainedObjectsBuffer>(player)) return false;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        return slot < buf.Length && buf[slot].objectID != ObjectID.None && PlayerController.ItemIsBroken(buf[slot].objectData);
    }

    internal static Dictionary<ObjectID, int> InventoryCounts(World server, Entity player)
    {
        var d = new Dictionary<ObjectID, int>();
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(player)) return d;
        var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
        for (int i = 0; i < buf.Length; i++)
        {
            var id = buf[i].objectID;
            if (id == ObjectID.None) continue;
            ObjectInfo info = PugDatabase.GetObjectInfo(id, buf[i].variation);
            int n = info != null && !info.isStackable ? 1 : buf[i].amount;
            d.TryGetValue(id, out int c);
            d[id] = c + n;
        }
        return d;
    }

    internal static string InventoryDiff(Dictionary<ObjectID, int> before, Dictionary<ObjectID, int> after)
    {
        var parts = new List<string>();
        foreach (var kv in after)
        {
            before.TryGetValue(kv.Key, out int b);
            if (kv.Value != b) parts.Add($"{kv.Key} {(kv.Value - b > 0 ? "+" : "")}{kv.Value - b}");
        }
        foreach (var kv in before)
        {
            if (!after.ContainsKey(kv.Key)) parts.Add($"{kv.Key} -{kv.Value}");
        }
        return parts.Count == 0 ? "변화 없음" : string.Join(", ", parts);
    }

    internal static float2 DirTo(float3 from, float2 to)
    {
        float2 d = to - from.xz;
        return math.lengthsq(d) > 1e-6f ? math.normalize(d) : float2.zero;
    }
}
