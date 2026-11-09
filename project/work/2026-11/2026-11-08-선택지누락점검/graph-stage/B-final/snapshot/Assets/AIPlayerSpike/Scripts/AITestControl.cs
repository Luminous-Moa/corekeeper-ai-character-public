// AITestControl — TEST ONLY controls of one AI character for setting up test sites (치트명령-이식 추가 범위, 2026-11-08;
// session 코어키퍼-AI-캐릭터, work/2026-11/2026-11-08-cheat-commands). Behind AIPlayers/testcmds.on like the other test
// commands.
//   /ai <member> hold [on|off]   no argument = toggle. While held the character makes no model call (action model
//                                AIBrain.Send, upper model AIPlanner.Report) and takes no automatic action: the brain
//                                tick (decisions, self-defence, emergency, reflex, goal checks, expedition reports) does
//                                not run, the running skill is stopped, and the body sends a stand-still input (no skill,
//                                door close or cursor stow; memory and vitals keep reading). A request in flight is
//                                dropped; an upper report trigger is kept and goes after the release. hold off = the
//                                brain decides again from the current facts with the goal it had. Not kept over a game
//                                restart (AIAgent field).
//   /ai <member> tp <x> <z>      teleport the character the way /ai arena go does (the game's player Teleporting state,
//   /ai <member> tp host         ~6 s) to the nearest walkable tile within 3 of x z (world tiles, "~N" from the AI, as the
//                                other test commands) or of the host; no walkable tile there = no teleport (the game would
//                                send a stuck body to the core spawn). The running skill, waiting choices and the request
//                                in flight are dropped, and the brain makes no decision until the body has arrived (or
//                                20 s passed); then it decides again from the new place.
// Test note: the game makes a respawned player immune to damage (ConditionID.ImmuneToDamageAfterRespawn, no duration)
// until it moves or presses attack/interact/off-hand, then for 2 s more (ConditionsFromMovementSystem). A held character
// gives no such input, so after a death (also the stuck death of a teleport into a wall) it takes no damage until it acts.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AITestControl
{
    private const float TpLimitSeconds = 20f;

    // ------------------------------------------------------------------ gates (read by the brain, body and planner)

    public static bool Held => AIAgent.Cur != null && AIAgent.Cur.Hold;

    // True while the brain must not run this frame: held, or a test teleport still on its way. The frame the teleport
    // has arrived, the brain drops what it prepared at the old place and decides again (and runs this frame).
    public static bool BlocksBrain(World server, Entity player, float now)
    {
        var a = AIAgent.Cur;
        if (a == null) return false;
        if (a.Hold) return true;
        if (!a.TpPending) return false;
        var em = server.EntityManager;
        float3 p = em.GetComponentData<LocalTransform>(player).Position;
        bool teleporting = false;
        if (em.HasComponent<PlayerState.PlayerStateCD>(player))
        {
            var ps = em.GetComponentData<PlayerState.PlayerStateCD>(player);
            teleporting = ps.HasAnyState(PlayerState.PlayerStateEnum.Teleporting) || ps.IsNextState(PlayerState.PlayerStateEnum.Teleporting);
        }
        bool arrived = !teleporting && math.distance(p.xz, new float2(a.TpX, a.TpZ)) <= 2f;
        bool late = now - a.TpSince > TpLimitSeconds;
        if (!arrived && !late) return true;
        a.TpPending = false;
        string why = arrived ? $"순간이동 도착({a.TpX},{a.TpZ})" : $"순간이동 {TpLimitSeconds:F0}초 안에 도착 확인 못함(지금 {p.x:F1},{p.z:F1})";
        AIPlayerSpikeMod.Log($"TEST-ONLY tp: {AIAgent.Cur.Name} {(arrived ? "arrived" : "not confirmed")} after {now - a.TpSince:F1}s at ({p.x:F1},{p.z:F1}); deciding again from here");
        AIBrain.ForgetPlace(why);
        return false;
    }

    // ------------------------------------------------------------------ commands

    public static void Command(World server, Entity player, string arg, string[] parts)
    {
        float now = Time.realtimeSinceStartup;
        if (arg == "hold") Hold(server, player, parts, now);
        else if (arg == "tp") Teleport(server, player, parts, now);
    }

    private static void Hold(World server, Entity player, string[] parts, float now)
    {
        var a = AIAgent.Cur;
        string w = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        bool on;
        if (w == "") on = !a.Hold;
        else if (w == "on" || w == "켬") on = true;
        else if (w == "off" || w == "끔") on = false;
        else { AIPlayerSpikeMod.Reply("[시험용] 사용법: /ai <멤버> hold [on|off] (없으면 켜고 끄기를 바꿈)"); return; }
        if (on == a.Hold) { AIPlayerSpikeMod.Reply($"[시험용] {a.Name} 정지(hold) 이미 {(on ? "켜짐" : "꺼짐")}"); return; }
        a.Hold = on;
        if (on)
        {
            AIBody.Stop(server, player, now, "/ai hold", hard: true);
            AIBrain.CancelPendingChoices("/ai hold");
            AIPlanner.DropRequestForHold("/ai hold");
            AIPlayerSpikeMod.Log($"TEST-ONLY hold ON: {a.Name}: no model calls, no automatic actions; running skill stopped, request in flight dropped (goal kept: '{AIBrain.Goal.Text}')");
            AIPlayerSpikeMod.Reply($"[시험용] {a.Name} 정지(hold) 켬: 판단 모델·상위 계획 호출과 자동 행동을 멈춤. 풀기: /ai {a.Id} hold off");
        }
        else
        {
            AIBrain.ForgetPlace("정지(hold) 풀림");
            AIPlayerSpikeMod.Log($"TEST-ONLY hold OFF: {a.Name}: deciding again from the current facts (goal '{AIBrain.Goal.Text}')");
            AIPlayerSpikeMod.Reply($"[시험용] {a.Name} 정지(hold) 끔: 지금 상황에서 다시 판단");
        }
    }

    private static void Teleport(World server, Entity player, string[] parts, float now)
    {
        const string usage = "[시험용] 사용법: /ai <멤버> tp <x> <z> · /ai <멤버> tp host (좌표 ~N = AI 기준, ±1000000 안)";
        var a = AIAgent.Cur;
        var em = server.EntityManager;
        if (!em.HasComponent<PlayerState.PlayerStateCD>(player) || !em.HasComponent<PlayerState.TeleportingStateCD>(player))
        {
            AIPlayerSpikeMod.Reply("[시험용] 이 캐릭터는 순간이동 상태가 없음");
            return;
        }
        if (em.HasComponent<HealthCD>(player) && em.GetComponentData<HealthCD>(player).health <= 0)
        {
            AIPlayerSpikeMod.Reply($"[시험용] {a.Name}이(가) 쓰러져 있어 순간이동하지 않음");
            return;
        }
        int2 me = AIBody.TileOf(AIBody.Pos(server, player));
        int2 aim;
        string what;
        if (parts.Length == 3 && parts[2].Equals("host", StringComparison.OrdinalIgnoreCase))
        {
            aim = AIBody.TileOf(AIBody.HostPosition(server, player));
            what = "호스트 옆";
        }
        else
        {
            var args = new List<string>();
            for (int i = 2; i < parts.Length; i++) args.Add(parts[i]);
            if (args.Count != 2 || !AITestWorldEdit.Pos(args, 0, me, out aim)) { AIPlayerSpikeMod.Reply(usage); return; }
            what = $"({aim.x},{aim.y})";
        }
        var tr = new AITileReader(server);
        bool host = what == "호스트 옆";
        int2 to = NearestWalkable(tr, aim, 3, host, out bool found);
        if (!found)
        {
            // no walkable tile (a wall block, an unloaded area): the game would treat the body as stuck there and send it
            // to the core spawn (live check: a death and respawn at (0,-0.1)), so do not teleport
            AIPlayerSpikeMod.Log($"TEST-ONLY tp: {a.Name} refused: no walkable tile within 3 of {what} ({aim.x},{aim.y})");
            AIPlayerSpikeMod.Reply($"[시험용] 순간이동 안 함: {what} ({aim.x},{aim.y}) 주변 3칸 안에 걸을 수 있는 칸이 없음(벽 속이거나 아직 읽지 않은 곳). " + usage.Substring("[시험용] ".Length));
            return;
        }

        // nothing planned at the old place goes on: the skill, the waiting choices and the request in flight
        AIBody.Stop(server, player, now, "/ai tp", hard: true);
        AIBrain.CancelPendingChoices("/ai tp");
        // the game's own player teleport, as /ai arena go (AIArena.Go)
        var ps = em.GetComponentData<PlayerState.PlayerStateCD>(player);
        ps.SetNextState(PlayerState.PlayerStateEnum.Teleporting, true);
        em.SetComponentData(player, ps);
        var ts = em.GetComponentData<PlayerState.TeleportingStateCD>(player);
        ts.targetPosition = new Vector3(to.x, 0f, to.y);
        em.SetComponentData(player, ts);
        a.TpPending = true; a.TpX = to.x; a.TpZ = to.y; a.TpSince = now;
        AIPlayerSpikeMod.Log($"TEST-ONLY tp: {a.Name} from ({me.x},{me.y}) to ({to.x},{to.y}) for {what}; skill stopped, choices dropped, decisions wait for the arrival");
        AIPlayerSpikeMod.Reply($"[시험용] {a.Name} 순간이동 {what} → ({to.x},{to.y}), 약 6초");
    }

    // The nearest walkable tile within rad (ring by ring, as AIArena.Nearest). With skipCenter the center itself is left
    // out (the host stands there).
    private static int2 NearestWalkable(AITileReader tr, int2 p, int rad, bool skipCenter, out bool found)
    {
        for (int d = skipCenter ? 1 : 0; d <= rad; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dz = -d; dz <= d; dz++)
                    if (math.max(math.abs(dx), math.abs(dz)) == d && tr.Walkable(p + new int2(dx, dz))) { found = true; return p + new int2(dx, dz); }
        found = false;
        return p;
    }
}
