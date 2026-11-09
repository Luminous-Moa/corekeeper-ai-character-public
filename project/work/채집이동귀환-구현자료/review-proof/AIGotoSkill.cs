// AIGotoSkill — extracted from the existing implementation; behavior and state ownership are unchanged.
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

public class GotoSkill : AISkill
{
    private readonly int2 _target;
    private readonly string _what;
    private readonly AINavigator _nav = new AINavigator();
    private float _lastLog;

    public GotoSkill(int2 target, string what) { Name = "걷기"; _target = target; _what = what; Key = $"goto:{target.x},{target.y}"; }

    public override bool Start(World server, Entity player, float now)
    {
        AITravel.ObserveView(server, player, true);
        var tr = new AITileReader(server);
        bool visible = AIVision.CanSee(AIBody.Pos(server, player), _target);
        Facts.Add($"target={_what} ({_target.x},{_target.y}) basis={(visible ? "현재 관측" : "마지막 통행 기억(현재 상태 미확인)")}");
        if (!AITravel.KnownWalkable(_target))
        {
            // The host stands on walkable ground; for coordinates, a blocked target is a start failure.
            Status = SkillStatus.Failed;
            Reason = visible ? $"목표 칸이 현재 걸을 수 없는 곳({tr.Top(_target).tileType})" : "목표 칸의 통행 가능성을 직접 관측한 정확한 기억이 없음. 이동 목적은 유지하고 탐색·다른 수단을 판단해야 함";
            return false;
        }
        _nav.Start(server, player, _target, 0.5f, now);
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var st = _nav.Tick(server, player, now, out float2 move);
        if (now - _lastLog >= 1f)
        {
            _lastLog = now;
            var p = AIBody.Pos(server, player);
            AIPlayerSpikeMod.Log($"SKILL 걷기: pos=({p.x:F2},{p.z:F2}) target=({_target.x},{_target.y}) dist={math.distance(p.xz, (float2)_target):F2} mode={_nav.Mode} stuck={_nav.StuckCount}");
        }
        if (st == NavStatus.Arrived)
        {
            var p = AIBody.Pos(server, player);
            Facts.Add($"arrived dist={math.distance(p.xz, (float2)_target):F2} nav={_nav.Mode} stuck={_nav.StuckCount} replans={_nav.Replans}");
            return Finish(SkillStatus.Done, $"도착 ({_target.x},{_target.y}), 길찾기 {_nav.Mode}");
        }
        if (st == NavStatus.Failed)
        {
            Facts.Add($"nav={_nav.Mode} stuck={_nav.StuckCount} replans={_nav.Replans}");
            return Finish(SkillStatus.Failed, _nav.FailReason + $" (길찾기 {_nav.Mode})", _nav.FailCode);
        }
        return new BodyInput { move = move, slot = HoldSlot };
    }

    // hotbar slot to hold while walking (-1 = keep): a walk to a wall takes the pick out on the way, like a player
    public int HoldSlot = -1;

    public override void Cleanup(World server) { _nav.Dispose(server); }

    public override string Progress(World server, Entity player)
    {
        float d = math.distance(AIBody.Pos(server, player).xz, (float2)_target);
        return $"{_what} 목적지 ({_target.x},{_target.y})까지 {d:F1}칸 남음, 약 {d / AIBody.WalkSpeed:F1}초";
    }

    public override float EtaSeconds(World server, Entity player)
        => math.max(0f, math.distance(AIBody.Pos(server, player).xz, (float2)_target) - 0.5f) / AIBody.WalkSpeed;
}
