// AIMineSkill — extracted from the existing implementation; behavior and state ownership are unchanged.
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

public class MineSkill : AISkill
{
    private int2? _requested;
    private readonly bool _oreOnly;
    private int2 _target;
    private int2 _stand;
    private int _slot = -1;
    private AIHandLease _hand;
    private string _tool = "";
    private int _wallTileset;
    private bool _wallOre;
    private int _hp, _reduction;
    private ObjectID _wallObj;
    private readonly AINavigator _nav = new AINavigator();
    private enum Phase { Approach, Equip, Swing }
    private Phase _phase;
    private float _phaseStart;
    private Dictionary<ObjectID, int> _invBefore;
    private int _tileHpFirst = -2, _tileHpLast = -2;
    private float _lastLog;
    private float _lastHpChange;

    public MineSkill(int2? target, bool oreOnly) { Name = "캐기"; _requested = target; _oreOnly = oreOnly; }

    public override bool Start(World server, Entity player, float now)
    {
        var tr = new AITileReader(server);
        float3 pos = AIBody.Pos(server, player);
        var shut = AIEdge.ShutDoorsNear(server, AIBody.TileOf(pos), 30);   // 재실측 N-c: a shut door tile is no stand
        HashSet<int2> standBlocked; try { standBlocked = AIObjects.BlockedInView(server, player, out _); } catch { standBlocked = null; }   // 검수 S2: nor an object tile
        if (_requested.HasValue)
        {
            _target = _requested.Value;
            if (!AIVision.CanSee(pos, _target)) { Status = SkillStatus.Failed; Reason = "대상 칸이 보이지 않음"; return false; }
            if (!tr.HasWall(_target, out TileCD w, out _wallOre, out _)) { Status = SkillStatus.Failed; Reason = $"대상 칸에 벽이 없음({tr.Top(_target).tileType})"; return false; }
            _wallTileset = w.tileset;
            if (!TryStandFor(tr, _target, pos, shut, standBlocked, out _stand)) { Status = SkillStatus.Failed; Reason = "대상 옆에 설 수 있는 칸이 없음"; return false; }
        }
        else
        {
            var o = AIObservation.Take(server, player, true);
            float best = float.MaxValue;
            bool found = false;
            foreach (var w in o.wallTiles)
            {
                if (_oreOnly && !w.ore) continue;
                if (!TryStandFor(tr, w.p, pos, shut, standBlocked, out int2 stand)) continue;
                if (!AIWallData.TryGet(server, w.tileset, out _, out _, out _)) continue; // skip indestructible/unknown walls
                float d = math.distance(pos.xz, (float2)stand);
                if (d < best) { best = d; _target = w.p; _stand = stand; _wallTileset = w.tileset; _wallOre = w.ore; found = true; }
            }
            if (!found) { Status = SkillStatus.Failed; Reason = _oreOnly ? "보이는 광석 벽 중 다가갈 수 있는 것이 없음" : "보이는 벽 중 다가갈 수 있는 것이 없음"; return false; }
        }
        if (!AIWallData.TryGet(server, _wallTileset, out _hp, out _reduction, out _wallObj))
        {
            Status = SkillStatus.Failed; Reason = $"이 벽(tileset {(Tileset)_wallTileset})의 데이터를 읽을 수 없음(부술 수 없는 벽일 수 있음)"; return false;
        }
        Key = $"mine:{_target.x},{_target.y}";
        _slot = AIBody.HandSlot(server, player, true, out _tool, out string handSwap);
        if (!string.IsNullOrEmpty(handSwap)) Facts.Add(handSwap);
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = AIBody.CanHold(server, player, true, out _, out string noHold) ? "손에 들 빈 칸이나 곡괭이가 없음" : noHold; return false; }
        _hand = new AIHandLease(server, player, _slot, true);
        _invBefore = AIBody.InventoryCounts(server, player);
        _slotSince = AIBody.HeldSlot == _slot ? now - 1f : now;
        Facts.Add($"target=({_target.x},{_target.y}) wall={(Tileset)_wallTileset} obj={_wallObj} hp={_hp} reduction={_reduction} ore={_wallOre} stand=({_stand.x},{_stand.y}) tool={_tool} slot={_slot}");
        _nav.Start(server, player, _stand, 0.3f, now);
        _phase = Phase.Approach;
        _phaseStart = now;
        return true;
    }

    // A walkable 4-neighbour of the wall to stand on, nearest to the AI. 재실측 N-c: not a shut door tile (an own door
    // next to the wall was taken as the stand: the walk there could never arrive)
    private static bool TryStandFor(AITileReader tr, int2 wall, float3 aiPos, HashSet<int2> shut, HashSet<int2> blocked, out int2 stand)
    {
        stand = default;
        float best = float.MaxValue;
        bool ok = false;
        int2[] dirs = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        foreach (var d in dirs)
        {
            int2 n = wall + d;
            if (!AIEdge.StandOk(tr, n, blocked, shut)) continue;   // 검수 S2: the one stand rule
            float dist = math.distance(aiPos.xz, (float2)n);
            if (dist < best) { best = dist; stand = n; ok = true; }
        }
        return ok;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var tr = new AITileReader(server);
        float2 face = math.normalize((float2)(_target - _stand));
        if (_goneAt > 0f || !tr.HasWall(_target, out _, out _, out _))
        {
            return Done(server, player, now, tr);
        }
        string handChanged = _hand.Check(server, player, out bool handReady);
        if (handChanged != null) return Finish(SkillStatus.Failed, handChanged);
        switch (_phase)
        {
            case Phase.Approach:
            {
                var st = _nav.Tick(server, player, now, out float2 move);
                if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "다가가기 실패: " + _nav.FailReason);
                if (st == NavStatus.Arrived)
                {
                    _nav.Dispose(server);
                    _phase = Phase.Equip;
                    _phaseStart = now;
                    return new BodyInput { face = face, slot = _slot };
                }
                return new BodyInput { move = move, slot = _slot };
            }
            case Phase.Equip:
            {
                // turn to the wall (2 server ticks); wait for the tool switch only if the slot was not already held
                // for 0.6 s (the approach walk sends the slot from the start, so after a walk there is no extra stop)
                if (!handReady || now - _phaseStart < 0.1f || now - _slotSince < 0.6f) return new BodyInput { face = face, slot = _slot };
                int est = AIWallData.EstimateMiningDamage(server, player, out int eff);
                var em = server.EntityManager;
                string held = em.HasComponent<EquippedObjectCD>(player) ? em.GetComponentData<EquippedObjectCD>(player).containedObject.objectID.ToString() : "?";
                Facts.Add($"precheck: est damage {est} (20 + Mining effect {eff}) vs reduction {_reduction}; holding {held}");
                if (est <= _reduction)
                {
                    return Finish(SkillStatus.Failed, $"도구 부족: 추정 피해 {est} ≤ 벽 감소 {_reduction} ({_wallObj})");
                }
                _tileHpFirst = AIWallData.TileEntityHealth(server, _target);
                _tileHpLast = _tileHpFirst;
                _lastHpChange = now;
                _phase = Phase.Swing;
                _phaseStart = now;
                return new BodyInput { face = face, slot = _slot, primary = true };
            }
            default:
            {
                float3 pos = AIBody.Pos(server, player);
                float2 move = float2.zero;
                if (math.distance(pos.xz, (float2)_stand) > 0.45f) move = AIBody.DirTo(pos, (float2)_stand); // pushed away: step back
                int hp = AIWallData.TileEntityHealth(server, _target);
                if (hp != _tileHpLast) { _tileHpLast = hp; _lastHpChange = now; }
                if (now - _lastLog >= 1f)
                {
                    _lastLog = now;
                    string probe = "";
                    try { probe = AIWallData.DamageProbe(server, player, _target); } catch (Exception e) { probe = "probe error " + e.GetType().Name; }
                    AIPlayerSpikeMod.Log($"SKILL 캐기: swinging at ({_target.x},{_target.y}) tileHp={hp} pos=({pos.x:F2},{pos.z:F2}) {probe}");
                }
                if (now - _lastHpChange > 6f)
                {
                    ObserveOnce("mine-no-progress", $"6초 동안 변화 없음(타일 체력 {_tileHpFirst}→{_tileHpLast})");
                }
                if (now - _phaseStart > 60f) ObserveOnce("mine-duration", "현재 벽 채굴 60초 경과");
                return new BodyInput { move = move, face = face, slot = _slot, primary = handReady };
            }
        }
    }

    // The wall is gone: stand still up to 3 s so the server's auto-pickup can move the drop into the bag, then
    // report the observed inventory difference and any drop left on the ground nearby.
    private BodyInput Done(World server, Entity player, float now, AITileReader tr)
    {
        if (_goneAt <= 0f)
        {
            _goneAt = now;
            Facts.Add($"tile gone after {now - _phaseStart:F1}s of swinging: top now {tr.Top(_target).tileType}; tileHp {_tileHpFirst}->{_tileHpLast}");
        }
        var after = AIBody.InventoryCounts(server, player);
        string diff = AIBody.InventoryDiff(_invBefore, after);
        if (diff == "변화 없음" && now - _goneAt < 3f && !NoPickupWait)
        {
            // step onto the cleared tile so the drop is within pickup range
            float3 pos = AIBody.Pos(server, player);
            float2 mv = math.distance(pos.xz, (float2)_target) > 0.3f ? AIBody.DirTo(pos, (float2)_target) : float2.zero;
            return new BodyInput { move = mv, slot = _slot };
        }
        var o = AIObservation.Take(server, player, false);
        var near = new List<string>();
        foreach (var i in o.items) if (i.dist < 3f) near.Add($"{i.id}x{i.amount} d={i.dist:F1}");
        Facts.Add($"inventory diff {now - _goneAt:F1}s after: {diff}; drops still on ground within 3: [{string.Join(", ", near)}]");
        return Finish(SkillStatus.Done, $"벽 ({_target.x},{_target.y}) 부숨 · 가방 {diff}{(near.Count > 0 ? " · 땅에 남은 것 " + string.Join(", ", near) : "")}");
    }

    private float _goneAt;
    private float _slotSince;
    // A wall on the way (tunnel): go on to the next wall at once instead of standing until the drop is picked up
    // (measured 0.5 s standing per wall); the drop still flies in by auto-pickup, the tunnel reports the total.
    public bool NoPickupWait;
    private int _estHit = -1;

    public override void Cleanup(World server) { _nav.Dispose(server); }

    public override string Progress(World server, Entity player)
    {
        float d = math.distance(AIBody.Pos(server, player).xz, (float2)_target);
        if (_goneAt > 0f) return $"벽 ({_target.x},{_target.y}) 부숨, 떨어진 것 줍는 중";
        string risk = AIWallData.RiskText(server, _wallObj);
        if (_phase == Phase.Approach) return $"벽 {_wallObj} ({_target.x},{_target.y})로 다가가는 중 {d:F1}칸{risk}";
        int hp = AIWallData.TileEntityHealth(server, _target);
        return $"벽 {_wallObj} ({_target.x},{_target.y}) 캐는 중, 벽 체력 {(hp >= 0 ? hp : _hp)}/{_hp}{risk}";
    }

    // ~0.3 s left when one more hit should break the wall (estimated damage per hit, observed tile health); the pickup
    // wait after the wall is gone counts as ending.
    public override float EtaSeconds(World server, Entity player)
    {
        if (_goneAt > 0f) return 0.2f;
        if (_phase != Phase.Swing) return -1f;
        if (_estHit < 0) _estHit = math.max(1, AIWallData.EstimateMiningDamage(server, player, out _) - _reduction);
        int hp = AIWallData.TileEntityHealth(server, _target);
        if (hp < 0) hp = _hp;
        return hp > 0 && hp <= _estHit ? 0.3f : -1f;
    }
}
