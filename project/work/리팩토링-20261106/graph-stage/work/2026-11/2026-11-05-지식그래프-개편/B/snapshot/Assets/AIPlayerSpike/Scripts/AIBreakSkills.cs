// AIBreakSkills — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using Inventory;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Cuts a big root (a bigRoot tile layer, the thick roots in the dirt; the game lets any swing hit it:
// EntityUtility.EvaluateCanOnlyHitCertainObjects bigRoot) with the pick: walk next to it, face it, swing until the
// bigRoot layer is gone; reports the bag change and remembers it (Drops).
public class RootSkill : AISkill
{
    private readonly int2 _t;
    private int _slot;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn;
    private Dictionary<ObjectID, int> _before;
    private float _goneAt = -1f, _inRangeSince = -1f, _lastLog;
    public RootSkill(int2 tile) { Name = "뿌리 캐기"; _t = tile; Key = $"root:{tile.x},{tile.y}"; }

    public static bool IsRoot(AITileReader tr, int2 t) => tr.Layers(t).Exists(l => l.tileType == TileType.bigRoot);

    public override bool Start(World server, Entity player, float now)
    {
        if (!IsRoot(new AITileReader(server), _t)) { Status = SkillStatus.Failed; Reason = "그 칸에 큰 뿌리가 없음"; return false; }
        _slot = AIBody.HandSlot(server, player, true, out string tool, out string handSwap);
        if (!string.IsNullOrEmpty(handSwap)) Facts.Add(handSwap);
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = AIBody.CanHold(server, player, true, out _, out string noHold) ? "손에 들 빈 칸이나 곡괭이가 없음" : noHold; return false; }
        _before = AIBody.InventoryCounts(server, player);
        Facts.Add($"target=bigRoot ({_t.x},{_t.y}) tool={tool}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        float3 me = AIBody.Pos(server, player);
        if (_goneAt > 0f || !IsRoot(new AITileReader(server), _t))
        {
            if (_goneAt < 0f) _goneAt = now;
            var after = AIBody.InventoryCounts(server, player);
            string diff = AIBody.InventoryDiff(_before, after);
            if (diff == "변화 없음" && now - _goneAt < 3f) return BodyInput.Idle; // auto pickup
            AIDrops.Note(AIDrops.RootSource, _before, after);   // learned per world (memory.json), kept across restarts
            return Finish(SkillStatus.Done, $"큰 뿌리 ({_t.x},{_t.y}) 캠 · 가방 {diff}");
        }
        if (now - StartTime > 25f) return Finish(SkillStatus.Failed, "25초 안에 못 캠");
        float d = math.distance(me.xz, (float2)_t);
        float2 face = AIBody.DirTo(me, (float2)_t);
        if (now - _lastLog >= 1f) { _lastLog = now; AIPlayerSpikeMod.Log($"SKILL 뿌리 캐기: ({_t.x},{_t.y}) d={d:F2}"); }
        if (d <= 1.4f)
        {
            if (_inRangeSince < 0f) _inRangeSince = now;
            if (now - _inRangeSince > 12f) return Finish(SkillStatus.Failed, "12초 동안 휘둘러도 안 캐짐");
            if (_navOn) { _nav.Dispose(server); _navOn = false; }
            return new BodyInput { face = face, slot = _slot, primary = true, aim = true };
        }
        // Walk to an open side tile of the root, not onto the root tile: the game's path search reported "no path" to
        // the root tile itself while the candidate (in-view flood) had an open side tile next to it (2026-11-04 four
        // players run 2 t=746: both roots left out after "다가가기 실패: 길 없음(게임 길찾기)" twice; knownPathAtStart=False
        // there too, as the known-tile check also has to reach the root tile).
        if (!_navOn) { _nav.Start(server, player, StandFor(server, player, me), 0.6f, now); _navOn = true; }
        var st = _nav.Tick(server, player, now, out float2 move);
        if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "다가가기 실패: " + _nav.FailReason);
        var sm = AIDoorPass.SkillMove(move.x, move.y, () => true, face.x, face.y);   // 검수 F1: a door step of the walk holds the move itself (AIBody.Tick)
        return new BodyInput { move = new float2(sm.x, sm.y), slot = _slot };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }

    // The nearest walkable side tile of the root with no object on it (the root tile itself when there is none).
    private int2 StandFor(World server, Entity player, float3 me)
    {
        var tr = new AITileReader(server);
        HashSet<int2> ob;
        try { ob = AIObjects.BlockedInView(server, player, out _); } catch { ob = new HashSet<int2>(); }
        int2 best = _t; float bd = float.MaxValue;
        var shut = AIEdge.ShutDoorsNear(server, _t, 1);   // 검수 S2: the one stand rule (no shut door either)
        foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
        {
            int2 n = _t + d;
            if (!AIEdge.StandOk(tr, n, ob, shut)) continue;
            float dd = math.distance(me.xz, (float2)n);
            if (dd < bd) { bd = dd; best = n; }
        }
        if (!math.all(best == _t)) Facts.Add($"stand=({best.x},{best.y})");
        return best;
    }
}

// Breaks a destructible object (not a creature): walk next to it, hold the pick (or bare hands) and swing until it is
// gone; reports the bag change and remembers it per object kind ("what does this give").
public class BreakSkill : AISkill
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public readonly HashSet<Entity> NoDamage = new HashSet<Entity>();
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.BreakSkill);
    private static State S => StateOf(AIAgent.Cur);

    public static HashSet<Entity> NoDamage => S.NoDamage;   // objects that took no damage (this session; also noted per world as a fact, AIMemory.NoteNoDamage)
    private float _inRangeSince = -1f;
    private readonly Entity _target;
    private ObjectID _id;
    private int _slot;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn;
    private Dictionary<ObjectID, int> _before;
    private float _goneAt = -1f;
    private int _hp0;
    private float _lastLog;
    public BreakSkill(Entity target) { Name = "부수기"; _target = target; Key = "break:" + target.Index; }

    private bool Alive(EntityManager em, out float3 p, out int hp)
    {
        p = float3.zero; hp = 0;
        if (!em.Exists(_target) || em.HasComponent<Disabled>(_target) || !em.HasComponent<HealthCD>(_target)) return false;
        if (em.HasComponent<EntityDestroyedCD>(_target) && em.IsComponentEnabled<EntityDestroyedCD>(_target)) return false;
        hp = em.GetComponentData<HealthCD>(_target).health;
        p = em.GetComponentData<LocalTransform>(_target).Position;
        return hp > 0;
    }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!Alive(em, out float3 p, out _hp0)) { Status = SkillStatus.Failed; Reason = "대상 물체가 없음"; return false; }
        _id = em.HasComponent<ObjectDataCD>(_target) ? em.GetComponentData<ObjectDataCD>(_target).objectID : ObjectID.None;
        // the weapon, as for attacking (both the pick and the sword failed at first; the real cause was a Torch left in
        // the cursor slot, which blocks every tool: AICraft.StowCursorItem, report §2)
        _slot = AIBody.WeaponSlotForAttack(server, player, out string tool, out string swap);
        if (!string.IsNullOrEmpty(swap)) Facts.Add(swap);
        if (_slot < 0) { Status = SkillStatus.Failed; Reason = AIBody.CanHold(server, player, false, out _, out string noHold) ? "손에 들 빈 칸이나 무기가 없음" : noHold; return false; }
        _before = AIBody.InventoryCounts(server, player);
        Facts.Add($"target={_id} at ({p.x:F1},{p.z:F1}) hp={_hp0} tool={tool}");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        if (_goneAt > 0f || !Alive(em, out float3 tp, out int hp))
        {
            if (_goneAt < 0f) _goneAt = now;
            var after = AIBody.InventoryCounts(server, player);
            string diff = AIBody.InventoryDiff(_before, after);
            if (diff == "변화 없음" && now - _goneAt < 3f) return BodyInput.Idle; // auto pickup
            // drops vary per break (Wood, ScrapPart, MechanicalPart from the same crate kind): count every result seen,
            // per world across restarts (AIDrops in memory.json)
            AIDrops.Note((int)_id, _before, after);
            return Finish(SkillStatus.Done, $"{_id} 부숨 · 가방 {diff}");
        }
        if (now - StartTime > 30f) return Finish(SkillStatus.Failed, $"30초 안에 못 부숨(체력 {_hp0}→{hp})");
        float d = math.distance(me.xz, tp.xz);
        // swinging next to it for 5 s without any health change: it does not take damage here (as a player sees the
        // hits bounce off); remembered for this session so it is not offered again
        if (d <= 1.4f && _inRangeSince < 0f) _inRangeSince = now;
        if (_inRangeSince > 0f && hp == _hp0 && now - _inRangeSince > 5f)
        {
            NoDamage.Add(_target);
            AIMemory.NoteNoDamage(_id, tp);
            return Finish(SkillStatus.Failed, $"5초 동안 휘둘러도 체력이 줄지 않음(체력 {_hp0}→{hp}) — 이 물체는 여기서 안 부서지는 것으로 기억");
        }
        float2 face = AIBody.DirTo(me, tp.xz);
        if (now - _lastLog >= 1f)
        {
            _lastLog = now;
            AIPlayerSpikeMod.Log($"SKILL 부수기: {_id} hp={hp} d={d:F2}");
        }
        if (d <= 1.4f)
        {
            if (_navOn) { _nav.Dispose(server); _navOn = false; }
            // click repeatedly (press 0.3 s, release 0.2 s) like a player clicking; not the cause of the early misses
            // (that was the cursor item, report §2) but kept as ordinary client input
            bool press = (now - _inRangeSince) % 0.5f < 0.3f;
            return new BodyInput { face = face, slot = _slot, primary = press, aim = true };
        }
        if (!_navOn) { _nav.Start(server, player, AIBody.TileOf(tp), 1.2f, now, _target); _navOn = true; }
        var st = _nav.Tick(server, player, now, out float2 move);
        if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "다가가기 실패: " + _nav.FailReason);
        var sm = AIDoorPass.SkillMove(move.x, move.y, () => true, face.x, face.y);   // 검수 F1: a door step of the walk holds the move itself (AIBody.Tick)
        return new BodyInput { move = new float2(sm.x, sm.y), slot = _slot };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }
}