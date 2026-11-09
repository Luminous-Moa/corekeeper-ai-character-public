// AIShortSkills — the short close-fight actions of combat stage 2 (work/2026-11/2026-11-05-전투-적용-계획.md): a short
// move to a fixed point, one swing, a walk into reach, holding the place. Each does exactly what was chosen and ends
// (arrived / blocked with the reason / one swing over / in reach / time), then the brain decides again. None reacts to
// the enemy by itself (pol.combat-no-auto-dodge): a move goes to its point even if a charge comes, a swing is one swing.
// The moves take the end point the option was built with (HEAD review item 2) and turn the straight segment checked then
// into the movement input directly — no path-finding helper, no first-path wait (item 3).
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

// How an attacked target ended (HEAD review item 6): a kill only when the death is seen.
public static class AITargetEnd
{
    public static SkillEnd How(EntityManager em, Entity t, int hpLast, out string why)
    {
        if (em.Exists(t))
        {
            bool hasHp = em.HasComponent<HealthCD>(t);
            int hp = hasHp ? em.GetComponentData<HealthCD>(t).health : -1;
            bool destroyed = em.HasComponent<EntityDestroyedCD>(t) && em.IsComponentEnabled<EntityDestroyedCD>(t);
            // review: the health must be read (hp -1 was "no health component", counted as a kill when being destroyed)
            if (AIRunGate.KilledSeen(true, hasHp, hp)) { why = "처치"; return SkillEnd.Killed; }
            if (em.HasComponent<Disabled>(t)) { why = "대상이 비활성(멀어져 게임이 멈춤, 죽음 아님)"; return SkillEnd.None; }
            if (destroyed) { why = $"대상이 없어지는 중({(hasHp ? $"체력 {hp}" : "체력 못 읽음")}, 죽음 확인 못 함)"; return SkillEnd.None; }
            if (!hasHp) { why = "대상의 체력을 읽을 수 없음(죽음 확인 못 함)"; return SkillEnd.None; }
            why = $"대상 체력 {hp}"; return SkillEnd.None;
        }
        why = $"대상이 사라짐(죽음 확인 못 함, 마지막으로 본 체력 {hpLast})";
        return SkillEnd.None;
    }
}

// Walk straight to a point (enemy-relative option "짧은 이동 / 긴 이동"). Ends on arrival (0.2 tiles), when stuck
// (no progress for 0.25 s after the start delay) or after twice the expected time. Measures its own start (item 8):
// what the body was doing (standing / moving the same way / turning / reversing), the frame the input was given, the tick
// the server took the new input and the first frame the body moved along the new direction.
public class StepSkill : AISkill
{
    private readonly float2 _dest;
    private readonly string _label;
    private readonly Entity _enemy;
    private readonly float _obsAt;
    private float2 _from, _chk, _dir, _last;
    private float _expect, _chkAt;
    private string _prior = "?";
    private float _tInput = -1f, _tServer = -1f, _tAlong = -1f;
    public float2 Dest => _dest;
    public float2 From => _from;
    public StepSkill(string key, float2 dest, string label, Entity enemy, float obsAt) { Name = "짧은 이동"; Key = key; _dest = dest; _label = label; _enemy = enemy; _obsAt = obsAt; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        _from = AIBody.Pos(server, player).xz; _chk = _from; _chkAt = now; _last = _from;
        float d = math.distance(_from, _dest);
        _expect = d / AIBody.WalkSpeed;
        _dir = d > 1e-4f ? (_dest - _from) / d : float2.zero;
        float2 v = em.HasComponent<PhysicsVelocity>(player) ? em.GetComponentData<PhysicsVelocity>(player).Linear.xz : float2.zero;
        _prior = MoveStart.Prior(v.x, v.y, _dir.x, _dir.y);
        Facts.Add($"run #{Run} {_label}: ({_from.x:F2},{_from.y:F2}) -> ({_dest.x:F2},{_dest.y:F2}) {d:F2} tiles, expected {_expect:F2}s, enemy {_enemy.Index}, observed {now - _obsAt:F2}s before the start, prior motion {_prior}");
        if (d < 0.15f) { Status = SkillStatus.Done; Reason = "이미 그 자리"; return false; }
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float2 me = AIBody.Pos(server, player).xz;
        if (_tInput < 0f) _tInput = now;
        if (_tServer < 0f && em.HasComponent<ClientInput>(player))
        {
            float2 si = em.GetComponentData<ClientInput>(player).movementDirection;
            if (math.lengthsq(si) > 1e-4f && math.dot(math.normalize(si), _dir) > 0.9f) _tServer = now;
        }
        if (_tAlong < 0f && MoveStart.Along(me.x - _last.x, me.y - _last.y, _dir.x, _dir.y))
        {
            _tAlong = now;
            AIPlayerSpikeMod.Log($"EXEC #{Run} step move-start: prior={_prior} input +{_tInput - StartTime:F3}s server-input +{(_tServer >= 0f ? (_tServer - StartTime).ToString("F3") : "?")}s along +{_tAlong - StartTime:F3}s ({_label})");
            AIChargeWatch.NoteMove(player, now);
        }
        _last = me;
        float2 d = _dest - me;
        if (math.length(d) <= 0.2f) return Finish(SkillStatus.Done, $"도착 ({_dest.x:F1},{_dest.y:F1}), {now - StartTime:F2}초");
        if (now - StartTime > _expect * 2f + 0.6f) return Finish(SkillStatus.Failed, $"막힘: {now - StartTime:F1}초 안에 못 감(남은 {math.length(d):F1}칸)", SkillEnd.Stuck);
        if (now - _chkAt >= 0.25f)
        {
            if (now - StartTime > 0.35f && math.distance(me, _chk) < 0.05f) return Finish(SkillStatus.Failed, $"막힘: 0.25초 동안 움직이지 못함(남은 {math.length(d):F1}칸)", SkillEnd.Stuck);
            _chk = me; _chkAt = now;
        }
        return new BodyInput { move = math.normalize(d), slot = -1 };
    }

    public override void Cleanup(World server)
    {
        if (_tAlong < 0f) AIPlayerSpikeMod.Log($"EXEC #{Run} step move-start: none (ended '{Reason}' after {(UnityEngine.Time.realtimeSinceStartup - StartTime):F2}s; prior={_prior}, server-input {(_tServer >= 0f ? "+" + (_tServer - StartTime).ToString("F3") + "s" : "not seen")})");
    }

    public override float RemainingSeconds(World server, Entity player) => math.distance(AIBody.Pos(server, player).xz, _dest) / AIBody.WalkSpeed;

    public override string Progress(World server, Entity player)
    {
        float2 me = AIBody.Pos(server, player).xz;
        return $"{_label}: 지금까지 {math.distance(me, _from):F1}칸 옴, 도착지 ({_dest.x:F1},{_dest.y:F1})까지 {math.distance(me, _dest):F1}칸 남음";
    }
}

// One swing at a target in reach ("한 번 치기"): press until the game starts a swing, release, end when that swing is
// over — hit or miss. Not offered out of reach; it does not walk after the target.
public class HitOnceSkill : AISkill
{
    private readonly Entity _target;
    private readonly HitOnceLogic _logic = new HitOnceLogic();
    private int _slot, _seq0, _hp0, _hpLast;
    private ObjectID _id, _weaponKey, _weaponId;
    private bool _brokenAtStart;
    private string _tool;
    public const float Reach = 1.3f;   // AttackSkill's swing distance
    public HitOnceSkill(Entity target, float endAfterStart = -1f) { Name = "한 번 치기"; _target = target; Key = "hit:" + target.Index; _logic.EndAfterStart = endAfterStart; }
    public float SwingStartedAt => _logic.Started ? StartTime + _logic.StartedAt : -1f;   // realtime of the swing start (-1: not yet)

    private bool Alive(EntityManager em, out float3 pos, out int hp)
    {
        pos = float3.zero; hp = 0;
        if (!em.Exists(_target) || em.HasComponent<Disabled>(_target) || !em.HasComponent<HealthCD>(_target)) return false;
        if (em.HasComponent<EntityDestroyedCD>(_target) && em.IsComponentEnabled<EntityDestroyedCD>(_target)) return false;
        hp = em.GetComponentData<HealthCD>(_target).health; pos = em.GetComponentData<LocalTransform>(_target).Position;
        return hp > 0;
    }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!Alive(em, out float3 tp, out int hp)) { Status = SkillStatus.Failed; Reason = "대상 적이 없음"; return false; }
        _id = em.HasComponent<ObjectDataCD>(_target) ? em.GetComponentData<ObjectDataCD>(_target).objectID : ObjectID.None;
        float d = math.distance(AIBody.Pos(server, player).xz, tp.xz);
        if (d > Reach) { Status = SkillStatus.Failed; Reason = $"휘두르는 거리 밖({d:F1}칸 > {Reach:F1})"; return false; }
        _slot = AIBody.WeaponSlotForAttack(server, player, out _tool, out string swap);
        if (!string.IsNullOrEmpty(swap)) Facts.Add(swap);
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = AIBody.CanHold(server, player, false, out _, out string noHold) ? "손에 들 빈 칸이나 무기가 없음" : noHold; return false; }
        Enum.TryParse(_tool, out ObjectID weapon);
        _weaponId = AIBody.SlotItem(server, player, _slot, out _brokenAtStart);   // what the slot holds now (None: bare hands)
        _weaponKey = AIVitals.HitKey(weapon, _brokenAtStart);
        _seq0 = AIMotion.SwingSeq; _hp0 = hp; _hpLast = hp;
        Facts.Add($"run #{Run} target={_id} {_target} d={d:F2} hp={hp} tool={_tool} slot={_slot} swingLeft={AIMotion.SwingLeft:F2}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        if (!Alive(em, out float3 tp, out int hp))
        {
            var how = AITargetEnd.How(em, _target, _hpLast, out string gone);
            return how == SkillEnd.Killed ? Finish(SkillStatus.Done, $"{_id} 처치 (체력 {_hp0}→0)", SkillEnd.Killed) : Finish(SkillStatus.Done, $"{_id} {gone}");
        }
        // review (asynchronous condition 4): the weapon in the chosen slot broke or was replaced / removed on the way
        ObjectID inSlot = AIBody.SlotItem(server, player, _slot, out bool brokenNow);
        if (_logic.WeaponCheck(inSlot == _weaponId, brokenNow, _brokenAtStart))
        {
            _weaponKey = AIVitals.HitKey(_weaponId, true);
            Facts.Add($"weapon {_weaponId} broke during the action (durability 0): hits recorded as the broken weapon");
        }
        if (_logic.Done) return Finish(_logic.Failed ? SkillStatus.Failed : SkillStatus.Done, $"{_logic.Reason} (손 칸 {_slot}: {(inSlot == ObjectID.None ? "빈 칸" : inSlot.ToString())}, 대상 체력 {_hp0}→{hp})");
        if (hp != _hpLast) { if (hp < _hpLast) AIVitals.RecordHit(_weaponKey, _hpLast - hp, now); _hpLast = hp; }
        float d = math.distance(me.xz, tp.xz);
        bool press = _logic.Step(now - StartTime, AIMotion.TimerOk, AIMotion.SwingSeq != _seq0, AIMotion.SwingLeft, d <= Reach);
        if (_logic.Done) return Finish(_logic.Failed ? SkillStatus.Failed : SkillStatus.Done, $"{_logic.Reason} (대상 체력 {_hp0}→{hp}{(_logic.Started ? $", 휘두르기 시작 +{_logic.StartedAt:F2}초" : "")})");
        return new BodyInput { face = AIBody.DirTo(me, tp.xz), slot = _slot, primary = press };
    }

    public override string Progress(World server, Entity player) => _logic.Started ? "휘두르는 중" : "휘두르기 시작 전";
    public override float RemainingSeconds(World server, Entity player) => _logic.Started ? (_logic.EndAfterStart >= 0f ? math.max(0f, _logic.EndAfterStart - (UnityEngine.Time.realtimeSinceStartup - StartTime - _logic.StartedAt)) : AIMotion.SwingLeft) : AIMotion.SwingLeft + 0.4f;
}

// Walk straight to the point the option named, next to a target ("사거리까지 접근"); ends there or as soon as the target is
// in swing reach on the way; no swing (that is a separate choice).
public class ApproachSkill : AISkill
{
    private readonly Entity _target;
    private readonly float2 _dest;
    private float2 _chk; private float _chkAt;
    public ApproachSkill(Entity target, float2 dest) { Name = "사거리까지 접근"; _target = target; _dest = dest; Key = "approach:" + target.Index; }
    public override float RemainingSeconds(World server, Entity player) => math.distance(AIBody.Pos(server, player).xz, _dest) / AIBody.WalkSpeed;

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_target) || !em.HasComponent<HealthCD>(_target) || em.GetComponentData<HealthCD>(_target).health <= 0) { Status = SkillStatus.Failed; Reason = "대상 적이 없음"; return false; }
        _chk = AIBody.Pos(server, player).xz; _chkAt = now;
        Facts.Add($"run #{Run} to ({_dest.x:F2},{_dest.y:F2}) next to {_target}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_target) || !em.HasComponent<HealthCD>(_target) || em.GetComponentData<HealthCD>(_target).health <= 0) return Finish(SkillStatus.Failed, "대상 적이 없어짐");
        float2 me = AIBody.Pos(server, player).xz, tp = em.GetComponentData<LocalTransform>(_target).Position.xz;
        float d = math.distance(me, tp);
        if (d <= HitOnceSkill.Reach - 0.1f) return Finish(SkillStatus.Done, $"휘두르는 거리 안({d:F1}칸), {now - StartTime:F2}초");
        if (math.distance(me, _dest) <= 0.2f) return Finish(SkillStatus.Done, $"정한 자리 도착, 적과 {d:F1}칸(적이 움직였으면 거리 밖일 수 있음), {now - StartTime:F2}초");
        if (now - StartTime > 2.5f) return Finish(SkillStatus.Failed, $"2.5초 안에 닿지 못함({d:F1}칸)", SkillEnd.Timeout);
        if (now - _chkAt >= 0.25f)
        {
            if (now - StartTime > 0.35f && math.distance(me, _chk) < 0.05f) return Finish(SkillStatus.Failed, $"막힘: 0.25초 동안 움직이지 못함({d:F1}칸)", SkillEnd.Stuck);
            _chk = me; _chkAt = now;
        }
        return new BodyInput { move = math.normalize(_dest - me), slot = -1 };
    }
}

// A composite action (user decision 2026-11-05: "때리고 이동, 이동 후 때리기"): stages run in a fixed order as one run (one
// execution id), the next stage starts in the same frame the previous one ends — no decision round trip in between, and no
// condition inside (it never changes course because a charge comes). A stage that cannot run ends the whole action with
// that stage's reason. A new choice by the model can cut in at any stage (AIRunGate: applied at once).
public class ComboSkill : AISkill
{
    private readonly AISkill[] _stages;
    private readonly string _label;
    private int _i;
    public int Stage => _i + 1;
    public int Stages => _stages.Length;
    public AISkill Current => _i < _stages.Length ? _stages[_i] : null;
    public ComboSkill(string key, string label, params AISkill[] stages) { Name = "복합 행동"; Key = key; _label = label; _stages = stages; }

    private bool StartStage(World server, Entity player, float now)
    {
        while (_i < _stages.Length)
        {
            var s = _stages[_i];
            s.Run = Run; s.StartTime = now; s.StartPos = AIBody.Pos(server, player);
            float prevSwing = _i > 0 && _stages[_i - 1] is HitOnceSkill h ? h.SwingStartedAt : -1f;
            AIPlayerSpikeMod.Log($"EXEC #{Run} combo {_label} stage {_i + 1}/{_stages.Length} start {s.Name} ({s.Key}) +{now - StartTime:F3}s{(prevSwing >= 0f ? $", {now - prevSwing:F3}s after the swing started" : "")}");
            bool ok;
            try { ok = s.Start(server, player, now); } catch (Exception e) { ok = false; s.Status = SkillStatus.Failed; s.Reason = "시작 오류: " + e.GetType().Name; }
            if (ok && s.Status == SkillStatus.Running) return true;
            if (s.Status == SkillStatus.Done) { Facts.Add($"stage {_i + 1} {s.Name}: {s.Reason}"); _i++; continue; }   // e.g. already there
            Finish(SkillStatus.Failed, $"{_i + 1}단계 {s.Name} 시작 못 함: {s.Reason}", s.End);
            return false;
        }
        Finish(SkillStatus.Done, $"{_stages.Length}단계 모두 끝");
        return false;
    }

    public override bool Start(World server, Entity player, float now)
    {
        Facts.Add($"run #{Run} {_label}: {_stages.Length} stages");
        _i = 0;
        return StartStage(server, player, now);
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        for (int guard = 0; guard < 4 && _i < _stages.Length; guard++)
        {
            var s = _stages[_i];
            var bi = s.Tick(server, player, now);
            if (s.Status == SkillStatus.Running) return bi;
            try { s.Cleanup(server); } catch { }
            AIPlayerSpikeMod.Log($"EXEC #{Run} combo {_label} stage {_i + 1}/{_stages.Length} {s.Status.ToString().ToUpperInvariant()} {s.Name} after {now - s.StartTime:F3}s: {s.Reason}");
            Facts.Add($"stage {_i + 1} {s.Name} {s.Status}: {s.Reason}");
            if (s.End == SkillEnd.Killed) return Finish(SkillStatus.Done, $"{_i + 1}단계 {s.Name}: {s.Reason}", SkillEnd.Killed);
            if (s.Status != SkillStatus.Done) return Finish(SkillStatus.Failed, $"{_i + 1}단계 {s.Name} 실패: {s.Reason}", s.End);
            _i++;
            if (_i >= _stages.Length) return Finish(SkillStatus.Done, $"{_stages.Length}단계 모두 끝: {s.Reason}");
            if (!StartStage(server, player, now)) return BodyInput.Idle;
        }
        return BodyInput.Idle;
    }

    public override void Cleanup(World server)
    {
        var c = Current;
        if (c != null && c.Status == SkillStatus.Running)
        {
            AIPlayerSpikeMod.Log($"EXEC #{Run} combo {_label} cut at stage {_i + 1}/{_stages.Length} ({c.Name}, {UnityEngine.Time.realtimeSinceStartup - c.StartTime:F3}s in): {Reason}");
            try { c.Cleanup(server); } catch { }
        }
    }

    // the running stage's remaining time plus the later stages (a swing 0.4 s, a move its straight length)
    public override float RemainingSeconds(World server, Entity player)
    {
        var c = Current;
        if (c == null) return 0f;
        float rest = math.max(0f, c.RemainingSeconds(server, player));
        float2 at = AIBody.Pos(server, player).xz;
        for (int k = _i + 1; k < _stages.Length; k++)
        {
            if (_stages[k] is HitOnceSkill) rest += 0.4f;
            else if (_stages[k] is StepSkill sk) { rest += math.distance(at, sk.Dest) / AIBody.WalkSpeed; at = sk.Dest; }
        }
        return rest;
    }

    public override string Progress(World server, Entity player)
    {
        var c = Current;
        if (c == null) return "끝남";
        string p = ""; try { p = c.Progress(server, player); } catch { }
        return $"{_i + 1}/{_stages.Length}단계 {c.Name}{(string.IsNullOrEmpty(p) ? "" : ": " + p)}";
    }
}

// Stand still until the next decision ("제자리 유지"); ends after 1 s if no decision came.
public class HoldSkill : AISkill
{
    public const float Seconds = 1.0f;
    public HoldSkill() { Name = "제자리 유지"; Key = "hold"; }
    public override float RemainingSeconds(World server, Entity player) => math.max(0f, Seconds - (UnityEngine.Time.realtimeSinceStartup - StartTime));
    public override bool Start(World server, Entity player, float now) => true;
    public override BodyInput Tick(World server, Entity player, float now) => now - StartTime >= Seconds ? Finish(SkillStatus.Done, $"제자리 {Seconds:F1}초") : BodyInput.Idle;
}
