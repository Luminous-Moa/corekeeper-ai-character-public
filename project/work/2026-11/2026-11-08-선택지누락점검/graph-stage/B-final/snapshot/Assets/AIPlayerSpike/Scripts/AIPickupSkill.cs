// AIPickupSkill — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.Text;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Picks a placed object up (단계 3): a pick or the empty hand (a melee weapon does not hit placed objects), next to it,
// swing until it is gone (its health must drop within 5 s of each hit), wait for the bag; then, when a new tile is given,
// place it again there (PlaceSkill; from inside only when asked). The edge memory forgets the old tile; the new place is
// recorded by PlaceSkill (AIEdge.NotePlaced). Steps: Core/AIBaseGuard.PickupStep.
public class PickupSkill : AISkill
{
    private readonly int2 _tile; private readonly int2? _to, _stand; private readonly bool _standOnly;
    private Entity _obj; private ObjectID _item; private int _slot; private bool _melee; private string _tool = "";
    private readonly PickupState _st = new PickupState();
    private readonly AINavigator _nav = new AINavigator(); private bool _navOn;
    private PlaceSkill _place; private bool _forgot; private float _lastLog;
    public PickupSkill(int2 tile, int2? to, int2? stand, bool standOnly)
    {
        Name = to.HasValue ? "옮기기" : "치우기"; _tile = tile; _to = to; _stand = stand; _standOnly = standOnly; Key = $"edgepickup:{tile.x},{tile.y}";
        _st.replace = to.HasValue;
    }

    private bool Alive(EntityManager em, out float3 p, out int hp)
    {
        p = float3.zero; hp = 0;
        if (_obj == Entity.Null || !em.Exists(_obj) || em.HasComponent<Disabled>(_obj) || !em.HasComponent<HealthCD>(_obj)) return false;
        if (em.HasComponent<EntityDestroyedCD>(_obj) && em.IsComponentEnabled<EntityDestroyedCD>(_obj)) return false;
        hp = em.GetComponentData<HealthCD>(_obj).health; p = em.GetComponentData<LocalTransform>(_obj).Position;
        return hp > 0;
    }

    public override bool Start(World server, Entity player, float now)
    {
        _obj = AIEdge.PlacedAt(server, _tile, out _item);
        if (_obj == Entity.Null) { Status = SkillStatus.Failed; Reason = $"({_tile.x},{_tile.y})에 놓은 물체가 없음"; return false; }
        _slot = AIBody.HandSlot(server, player, true, out _tool, out string swap, bareBeforeBroken: true);   // review S3
        if (!string.IsNullOrEmpty(swap)) Facts.Add(swap);
        var em = server.EntityManager;
        if (_slot >= 0 && em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            if (_slot < buf.Length && buf[_slot].objectID != ObjectID.None) { var info = PugDatabase.GetObjectInfo(buf[_slot].objectID, buf[_slot].variation); _melee = info != null && info.objectType == ObjectType.MeleeWeapon; }
        }
        Facts.Add($"object {_item} at ({_tile.x},{_tile.y}) tool={_tool} slot={_slot}{(_to.HasValue ? $" then place at ({_to.Value.x},{_to.Value.y})" : "")}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        if (_place != null)
        {
            var pb = _place.Tick(server, player, now);
            if (_place.Status == SkillStatus.Running) return pb;
            var r = _place; _place = null; r.Cleanup(server);
            return r.Status == SkillStatus.Done ? Finish(SkillStatus.Done, $"{_item} ({_tile.x},{_tile.y})에서 회수해 ({_to.Value.x},{_to.Value.y})에 다시 놓음({_st.facts.Trim()}; {r.Reason})") : Finish(SkillStatus.Failed, $"{_item} 회수했으나 다시 놓기 실패: {r.Reason} ({_st.facts.Trim()})");
        }
        if (now - StartTime > 40f) ObserveOnce("pending-1", $"40초 안에 못 끝냄({_st.phase}; {_st.facts.Trim()})");
        bool there = Alive(em, out float3 tp, out int hp);
        AIBody.InventoryCounts(server, player).TryGetValue(_item, out int bag);
        var o = AIBaseGuard.PickupStep(_st, new PickupObs { holdOk = _slot >= 0, heldMelee = _melee, objectThere = there, objHp = hp, dist = there ? math.distance(me.xz, tp.xz) : 0f, bagCount = bag, now = now });
        if (!string.IsNullOrEmpty(o.observation)) ObserveOnce("pickup-" + _st.phase, o.observation);
        if (now - _lastLog >= 1f) { _lastLog = now; AIPlayerSpikeMod.Log($"SKILL {Name}: phase={_st.phase} {_item} hp={hp} bag={bag} {_st.facts}"); }
        // review S3: gone from its tile = forgotten there, also when the pick-up then fails (not in the bag)
        if (!_forgot && AIBaseGuard.ShouldForget(_st)) { _forgot = true; AIEdge.ForgetPlaced(_tile); }
        if (_st.phase == PickupPhase.Failed) return Finish(SkillStatus.Failed, _st.fail);
        if (_st.phase == PickupPhase.Done) return Finish(SkillStatus.Done, $"{_item} ({_tile.x},{_tile.y}) 회수: {_st.facts.Trim()}");
        if (o.place && _to.HasValue)
        {
            _nav.Dispose(server); _navOn = false;
            _place = new PlaceSkill(_item, _to.Value, false, _stand, _standOnly);
            _place.StartTime = now; _place.StartPos = me;
            if (!_place.Start(server, player, now)) { string why = _place.Reason; _place = null; return Finish(SkillStatus.Failed, $"{_item} 회수했으나 다시 놓기 시작 실패: {why}"); }
            return BodyInput.Idle;
        }
        // 검수 F1: in reach while the walk's door step holds the door open: the walk goes on to the close first
        if (o.swing && !(_navOn && _nav.HoldsDoor))
        {
            if (_navOn) { _nav.Dispose(server); _navOn = false; }
            float2 face = AIBody.DirTo(me, tp.xz);
            bool press = (now - _st.hitSince) % 0.5f < 0.3f;
            return new BodyInput { face = face, slot = _slot, primary = press, aim = true };
        }
        if ((o.walk || o.swing) && there)
        {
            if (!_navOn) { _nav.Start(server, player, _tile, 1.2f, now, _obj); _navOn = true; }
            var ns = _nav.Tick(server, player, now, out float2 mv);
            if (ns == NavStatus.Failed) return Finish(SkillStatus.Failed, "다가가기 실패: " + _nav.FailReason);
            // live-run fix 2: the direct step only when it does not press into a shut door; 검수 F1: AIDoorPass.SkillMove (a
            // door step of the walk holds the move input itself, AIBody.Tick)
            float2 dir = AIBody.DirTo(me, tp.xz);
            var sm = AIDoorPass.SkillMove(mv.x, mv.y, () => AIEdge.DirectStepOk(server, me, dir), dir.x, dir.y);
            return new BodyInput { move = new float2(sm.x, sm.y), slot = _slot };
        }
        return new BodyInput { slot = _slot };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); if (_place != null) { _place.Cleanup(server); _place = null; } }
}
