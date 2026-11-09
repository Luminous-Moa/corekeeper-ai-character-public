// AIAttackSkill — extracted from the existing implementation; behavior and state ownership are unchanged.
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

public class AttackSkill : AISkill
{
    private Entity _target = Entity.Null;
    private ObjectID _id;
    private int _hpStart, _hpLast;
    private int _slot;
    private AIHandLease _hand;
    private string _tool;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navActive;
    private int2 _navTarget;
    private float _lastSeen;
    private float _lastLog;
    private float _navRetryAt;
    private int _pathFails;

    private readonly Entity _preset = Entity.Null;
    private ObjectID _weapon = ObjectID.None, _weaponKey = ObjectID.None;   // key: AIVitals.HitKey (broken apart)

    public bool NoDoorWait;   // 재검수 R2b: "지금 치기(문 열린 채)" — the swing does not wait for the walk's door step to close
    public AttackSkill() { Name = "공격"; }
    public AttackSkill(Entity target) { Name = "공격"; _preset = target; }

    public override bool Start(World server, Entity player, float now)
    {
        var o = AIObservation.Take(server, player, false);
        if (o.enemies.Count == 0) { Status = SkillStatus.Failed; Reason = "보이는 적이 없음"; return false; }
        var e = o.enemies[0];
        if (_preset != Entity.Null)
        {
            e = o.enemies.Find(x => x.entity == _preset);
            if (e == null) { Status = SkillStatus.Failed; Reason = "정한 적이 보이지 않음"; return false; }
        }
        Key = "attack:" + e.entity.Index;
        _target = e.entity; _id = e.id; _hpStart = e.health; _hpLast = e.health;
        _slot = AIBody.WeaponSlotForAttack(server, player, out _tool, out string swap);
        if (!string.IsNullOrEmpty(swap)) Facts.Add(swap);
        if (!Enum.TryParse(_tool, out _weapon)) _weapon = ObjectID.None;
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = AIBody.CanHold(server, player, false, out _, out string noHold) ? "손에 들 빈 칸이나 무기가 없음" : noHold; return false; }
        _hand = new AIHandLease(server, player, _slot, false);
        _weaponKey = AIVitals.HitKey(_weapon, AIBody.SlotBroken(server, player, _slot));
        if (_weaponKey != _weapon) Facts.Add($"weapon {_weapon} is broken (durability 0)");
        _lastSeen = now;
        Facts.Add($"target={_id} {_target} at ({e.pos.x:F1},{e.pos.z:F1}) d={e.dist:F1} hp={e.health}/{e.maxHealth} tool={_tool} slot={_slot}");
        return true;
    }

    public override string Progress(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!Alive(em, out float3 tp, out int hp)) return "대상이 이미 쓰러짐";
        int max = em.HasComponent<HealthCD>(_target) ? em.GetComponentData<HealthCD>(_target).maxHealth : hp;
        float d = math.distance(AIBody.Pos(server, player).xz, tp.xz);
        string est = AIVitals.HitStats(_weaponKey, out float per, out float sec) ? $", 관찰 기준 약 {(int)math.ceil(hp / math.max(1f, per))}타({math.ceil(hp / math.max(1f, per)) * sec:F1}초) 남음" : "";
        return $"대상 {_id} 체력 {hp}/{max}, {d:F1}칸{est}";
    }

    private bool Alive(EntityManager em, out float3 pos, out int hp)
    {
        pos = float3.zero; hp = 0;
        if (!em.Exists(_target) || em.HasComponent<Disabled>(_target)) return false;
        if (em.HasComponent<EntityDestroyedCD>(_target) && em.IsComponentEnabled<EntityDestroyedCD>(_target)) return false;
        if (!em.HasComponent<HealthCD>(_target)) return false;
        hp = em.GetComponentData<HealthCD>(_target).health;
        pos = em.GetComponentData<LocalTransform>(_target).Position;
        return hp > 0;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        if (!Alive(em, out float3 tp, out int hp))
        {
            Facts.Add($"target hp {_hpStart}->{(em.Exists(_target) && em.HasComponent<HealthCD>(_target) ? em.GetComponentData<HealthCD>(_target).health : 0)} (entity {(em.Exists(_target) ? "exists" : "gone")})");
            // combat stage 2 (HEAD review item 6): a kill only when the death is seen (health 0 / destroyed at 0); a target
            // that vanished, was disabled or lost its health component is not counted as a kill
            var how = AITargetEnd.How(em, _target, _hpLast, out string gone);
            if (how == SkillEnd.Killed) return Finish(SkillStatus.Done, $"{_id} 처치 (체력 {_hpStart}→0)", SkillEnd.Killed);
            return Finish(SkillStatus.Done, $"{_id} {gone} (체력 {_hpStart}→{_hpLast}에서)");
        }
        string handChanged = _hand.Check(server, player, out bool handReady);
        if (handChanged != null) return Finish(SkillStatus.Failed, handChanged);
        if (hp != _hpLast) { AIPlayerSpikeMod.Log($"SKILL 공격: {_id} hp {_hpLast}->{hp}"); if (hp < _hpLast) { AIVitals.RecordHit(_weaponKey, _hpLast - hp, now); _lastHitAt = now; } _hpLast = hp; }
        if (AIVision.CanSee(me, tp.xz)) _lastSeen = now;
        else if (now - _lastSeen > 3f) { Facts.Add($"target hp {_hpStart}->{hp}"); return Finish(SkillStatus.Failed, $"대상이 안 보임 (체력 {_hpStart}→{hp})"); }
        if (now - StartTime > 45f) ObserveOnce("attack-duration", $"공격 45초 경과 (체력 {_hpStart}→{hp})");

        float d = math.distance(me.xz, tp.xz);
        float2 face = AIBody.DirTo(me, tp.xz);
        if (now - _lastLog >= 1f)
        {
            _lastLog = now;
            int myHp = em.HasComponent<HealthCD>(player) ? em.GetComponentData<HealthCD>(player).health : -1;
            AIPlayerSpikeMod.Log($"SKILL 공격: me=({me.x:F2},{me.z:F2}) myHp={myHp} target {_id} ({tp.x:F2},{tp.z:F2}) d={d:F2} hp={hp}");
        }
        // 검수 F1: open -> through -> close is one bundle ("문으로 나가서 치기" says the door is closed after): in reach while
        // the walk's door step holds the door open, the walk goes on to the close first (the body takes its move)
        // 재검수 R1: only with nothing shut between (the touch of the facts; a closed door or a wall between: the walk and
        // its door step go on — it swung at the shut door from inside, Ng5); R2: the close first unless "hit now" was chosen
        int2 tt = AIEdge.EnemyTile(server, tp);   // 검수 F8: the enemy's tile as the facts read it (pressed on a shut door: its side)
        if (d <= 1.3f && AIDoorPass.SwingNow(d, 1.3f, AIEdge.ReachOf(server, player, tt, _id, _target).touch, _navActive && _nav.HoldsDoor, NoDoorWait))
        {
            if (_navActive) { _nav.Dispose(server); _navActive = false; }
            return new BodyInput { face = face, slot = _slot, primary = handReady };
        }
        if (!_navActive && now >= _navRetryAt)
        {
            _nav.Start(server, player, tt, 1.0f, now, _target);
            _navActive = true;
            _navTarget = tt;
        }
        else if (_navActive && !math.all(tt == _navTarget))
        {
            _nav.Retarget(server, tt, now);
            _navTarget = tt;
        }
        // direct approach when no path is known — live-run fix 2: not into a shut door / an unwalkable tile (then it stands
        // and waits for the walk's way. Repeated path failures are reported for the model to reconsider.
        // Review S6: the step check (door query + tile read) only when the walk gives no direction this frame.
        float2 move = float2.zero;
        if (_navActive)
        {
            var st = _nav.Tick(server, player, now, out float2 navMove);
            if (st == NavStatus.Failed)
            {
                _pathFails++;
                AIPlayerSpikeMod.Log($"SKILL 공격: path to target failed #{_pathFails} ({_nav.FailReason}, {_nav.Mode}); approaching directly, retry path in 2s");
                _nav.Dispose(server); _navActive = false; _navRetryAt = now + 2f;
                if (_pathFails >= 3)
                {
                    ObserveOnce("attack-path-failures", $"대상에게 갈 길을 세 번 찾지 못함 ({_nav.FailReason}, 체력 {_hpStart}→{hp})");
                }
            }
            else if (st == NavStatus.Moving && math.lengthsq(navMove) > 1e-4f) { move = navMove; _pathFails = 0; }
        }
        // 검수 F1: AIDoorPass.SkillMove; while the walk's door step runs the body takes its move instead (AIBody.Tick)
        var sm = AIDoorPass.SkillMove(move.x, move.y, () => AIEdge.DirectStepOk(server, me, face), face.x, face.y);
        return new BodyInput { move = new float2(sm.x, sm.y), slot = _slot };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }

    // The attack ends when the target's health reaches 0. Estimated only from observation: the observed damage per hit
    // and hit interval of this weapon (AIVitals.HitStats, n >= 2), the target's health now, and the time of the last
    // hit seen in this skill. Only for the last hit while in reach (one more observed-average hit kills it): then the
    // end is about one hit interval after the last hit. Otherwise unknown (-1): earlier hits, out of reach, no stats.
    private float _lastHitAt = -1f;
    public override float EtaSeconds(World server, Entity player)
    {
        var em = server.EntityManager;
        if (!Alive(em, out float3 tp, out int hp)) return 0f;
        if (_lastHitAt < 0f || !AIVitals.HitStats(_weaponKey, out float per, out float sec)) return -1f;
        if (math.distance(AIBody.Pos(server, player).xz, tp.xz) > 1.3f) return -1f;
        if (hp > per) return -1f;
        return math.max(0f, sec - (Time.realtimeSinceStartup - _lastHitAt));
    }
}
