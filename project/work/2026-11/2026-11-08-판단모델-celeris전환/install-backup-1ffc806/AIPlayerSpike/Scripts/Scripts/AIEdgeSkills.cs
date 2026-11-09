// AIEdgeSkills — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// One batch of the edge: places wall blocks on the batch's tiles in order (at most AIBaseEdgeCore.MaxBatch), each from
// an inside stand; the tile that closes the gap only from inside. Ends: all filled (Done), blocks ran out (Failed,
// with how far it got), or blocked (Failed: which tile and why). No branches of its own: the action model can switch
// at any trigger.
public class EdgeFillSkill : AISkill
{
    private readonly int _gap;
    private readonly List<int2> _tiles;
    private int _i, _done;
    private PlaceSkill _cur;
    private readonly List<string> _log = new List<string>();
    private readonly List<int2> _group;   // 단계 3 repair: every tile of the broken opening (the closing check); null = the plan gap
    private readonly bool _closeOutside;   // user decision A: the closing tile from outside (the option said so)
    public EdgeFillSkill(int gap, List<int2> tiles, List<int2> group = null, bool closeOutside = false)
    {
        Name = group != null ? "둘레 보수" : "둘레 메우기"; _gap = gap; _tiles = tiles; _group = group; _closeOutside = closeOutside;
        Key = (group != null ? $"edgerepair:{tiles[0].x},{tiles[0].y}" : $"edgefill:{gap}:{tiles[0].x},{tiles[0].y}") + (closeOutside ? ":out" : "");
    }
    private string What => _group != null ? $"구멍 {_gap + 1}" : $"틈 {_gap + 1}";

    public override bool Start(World server, Entity player, float now)
    {
        Facts.Add($"{(_group != null ? "repair opening" : "gap")} {_gap + 1}, tiles [{string.Join(" ", _tiles.ConvertAll(t => $"({t.x},{t.y})"))}]");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var tr = new AITileReader(server);
        while (_cur == null)
        {
            if (_i >= _tiles.Count) return Finish(SkillStatus.Done, $"{What}: {_done}칸 메움({string.Join(", ", _log)})");
            int2 t = _tiles[_i];
            if (tr.HasWall(t, out _, out _, out _)) { _i++; _log.Add($"({t.x},{t.y}) 이미 벽"); continue; }
            bool water = AIFarm.IsWater(tr, t);
            var pickId = AIEdge.BlockFor(server, player, water);   // review G11
            if (pickId == ObjectID.None)
                return Finish(SkillStatus.Failed, $"재료 떨어짐: {_tiles.Count}칸 중 {_done}칸 메움, 남은 {_tiles.Count - _i}칸(다음 ({t.x},{t.y}){(water ? "은 물 칸이라 물 위에 놓이는 블록이 필요" : "에 놓을 벽 블록 없음")})");
            // the stand: an inside walkable neighbour; the tile that closes the gap only from there
            int2? stand = null;
            foreach (var d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
                if (AIEdge.IsInside(t + d) && tr.Walkable(t + d) && !AIPlaceGrid.SpawnPad(t.x + d.x, t.y + d.y)) { stand = t + d; break; }
            // review G2: closing = the last open tile of this gap right now (other tiles walled, the door placed)
            // (a door spot's side wall in a repair batch belongs to no opening: never the closing tile)
            // (review F3: the outside option of a door gap closes its last tile other than the door spot, which stays the way in)
            bool closing = _group != null ? _group.Contains(t) && AIEdge.IsLastOpenOf(server, player, _group, t) : AIEdge.IsLastOpen(server, player, _gap, t, _closeOutside);
            // user decision A: the option that closes from outside places the closing tile from an outside stand
            if (closing && _closeOutside)
            {
                var os = AIEdge.OutsideStandOf(tr, (t.x, t.y));
                if (!os.HasValue) return Finish(SkillStatus.Failed, $"막힘: {What}을(를) 닫는 마지막 칸 ({t.x},{t.y})의 바깥 설 칸이 없음; {_done}칸 메운 뒤");
                stand = new int2(os.Value.x, os.Value.y);
            }
            if (closing && !stand.HasValue) return Finish(SkillStatus.Failed, $"막힘: {What}을(를) 닫는 마지막 칸 ({t.x},{t.y})의 안쪽 설 칸이 없음(이 후보는 안쪽에서 닫음; 바깥에서 닫는 후보는 따로 있음); {_done}칸 메운 뒤");
            _cur = new PlaceSkill(pickId, t, false, stand, closing);
            if (closing) Facts.Add($"({t.x},{t.y}) closes gap {_gap + 1}: from {(_closeOutside ? "outside" : "inside")} ({stand.Value.x},{stand.Value.y}) only");
            if (!_cur.Start(server, player, now)) { string why = _cur.Reason; _cur = null; return Finish(SkillStatus.Failed, $"막힘: ({t.x},{t.y}) {why}; {_done}칸 메운 뒤"); }
            _cur.StartTime = now;
        }
        var bi = _cur.Tick(server, player, now);
        if (_cur.Status == SkillStatus.Running) return bi;
        var r = _cur; _cur = null; r.Cleanup(server);
        int2 tt = _tiles[_i];
        AIPlayerSpikeMod.Log($"SKILL {Name}: ({tt.x},{tt.y}) {r.Status}: {r.Reason}");
        if (r.Status == SkillStatus.Done) { _done++; _i++; _log.Add($"({tt.x},{tt.y})"); return BodyInput.Idle; }
        return Finish(SkillStatus.Failed, $"막힘: ({tt.x},{tt.y}) {r.Reason}; {_done}칸 메운 뒤");
    }

    public override void Cleanup(World server) { if (_cur != null) { _cur.Cleanup(server); _cur = null; } }
}

// Through an own door as one action: open (E) -> walk through -> close (AIDoorPass). The passage runs across the door
// (east-west for variation 2/3, north-south for 0/1), from the side the AI is on.
public class DoorPassSkill : AISkill
{
    private readonly int2 _door;
    private readonly DoorPassState _st = new DoorPassState();
    private float2 _dir;
    private int _useId;
    private float _lastLog;
    private readonly AINavigator _nav = new AINavigator();
    private bool _walking;   // review G5: first walk to the tile in front of the door (around corners), then the door steps
    private int2 _front;
    public DoorPassSkill(int2 door) { Name = "문 지나가기"; _door = door; Key = $"doorpass:{door.x},{door.y}"; }
    protected virtual void Setup(DoorPassState st) { }

    public override bool Start(World server, Entity player, float now)
    {
        float3 me = AIBody.Pos(server, player);
        var dv = AIEdge.DoorAt(server, _door, me);
        if (dv == null) { Status = SkillStatus.Failed; Reason = $"({_door.x},{_door.y})에 문이 없음"; return false; }
        float2 axis = AIDoorPass.PassageEastWest(dv.variation) ? new float2(1f, 0f) : new float2(0f, 1f);
        float side = math.dot(me.xz - (float2)_door, axis);
        _dir = side <= 0f ? axis : -axis;   // from the AI's side through the door
        Setup(_st);
        if (_st.phase == DoorPhase.Close) _dir = -_dir;   // close only: the AI's side is the "far" side
        // the tile in front of the door on the AI's side
        _front = _st.phase == DoorPhase.Close ? _door + new int2((int)math.round(_dir.x), (int)math.round(_dir.y)) : _door - new int2((int)math.round(_dir.x), (int)math.round(_dir.y));
        if (math.distance(me.xz, (float2)_front) > 0.9f) { _nav.Start(server, player, _front, 0.35f, now); _walking = true; }
        Facts.Add($"door {dv.id} ({_door.x},{_door.y}) variation={dv.variation} kind={dv.kind} dir=({_dir.x:F0},{_dir.y:F0})");
        return true;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        float3 me = AIBody.Pos(server, player);
        var dv = AIEdge.DoorAt(server, _door, me);
        if (dv == null) return Finish(SkillStatus.Failed, $"({_door.x},{_door.y})의 문이 없어짐");
        bool open = AIDoorPass.IsOpen(dv.variation);
        if (_walking)
        {
            var ns = _nav.Tick(server, player, now, out float2 mv);
            if (ns == NavStatus.Failed) { _nav.Dispose(server); return Finish(SkillStatus.Failed, $"문 앞 칸 ({_front.x},{_front.y})으로 못 감: {_nav.FailReason}"); }
            if (ns != NavStatus.Arrived) return new BodyInput { move = mv, slot = -1 };
            _nav.Dispose(server); _walking = false; _st.stepAt = -1f;
        }
        bool pressDone = _useId == 0 || AIBody.UseDone >= _useId;
        AIDoorPass.PressDropped(_st, AIBody.UseDroppedWhy(_useId), now);   // 합치기 전 수정 C2
        var o = AIDoorPass.Step(_st, dv.kind, _door.x, _door.y, _dir.x, _dir.y, me.x, me.z, open, pressDone, now);
        if (!string.IsNullOrEmpty(o.observation)) ObserveOnce("door-" + _st.phase, o.observation);
        if (o.press) _useId = AIBody.NextUseId;
        if (now - _lastLog >= 0.5f || o.press) { _lastLog = now; AIPlayerSpikeMod.Log($"SKILL {Name}: phase={_st.phase} door ({_door.x},{_door.y}) variation={dv.variation} me=({me.x:F2},{me.z:F2}){(o.press ? $" press #{_useId}" : "")}"); }
        if (_st.phase == DoorPhase.Done) return Finish(SkillStatus.Done, $"문 ({_door.x},{_door.y}): {_st.facts}");
        if (_st.phase == DoorPhase.Failed) return Finish(SkillStatus.Failed, $"문 ({_door.x},{_door.y}): {_st.fail}{(_st.facts.Length > 0 ? $" ({_st.facts})" : "")}");
        return new BodyInput { move = new float2(o.mx, o.my), face = new float2(o.fx, o.fy), slot = -1, usePress = _useId != 0 && AIBody.UseDone < _useId ? _useId : 0, nudge = o.nudge };
    }

    public override void Cleanup(World server) { _nav.Dispose(server); }
}

// Close an own door that is open, from the side the AI is on.
public class DoorCloseSkill : DoorPassSkill
{
    public DoorCloseSkill(int2 door) : base(door) { Name = "문 닫기"; Key = $"doorclose:{door.x},{door.y}"; }
    protected override void Setup(DoorPassState st) { st.phase = DoorPhase.Close; }
}
