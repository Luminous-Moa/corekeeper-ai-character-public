// AIMoveTrace — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// The old STEP3 pattern (right 3s, left 3s, circle 6s, stop 3s, repeat) — only on "/ai walktest".
public class WalkTestSkill : AISkill
{
    private float _lastLog;
    public WalkTestSkill() { Name = "걷기시험"; }

    public override bool Start(World server, Entity player, float now)
    {
        AIPlayerSpikeMod.Reply("AI 걷기시험 시작: /ai stop 으로 멈춤");
        return true;
    }

    public static float2 Pattern(float t)
    {
        float c = t % 15f;
        if (c < 3f) return new float2(1f, 0f);
        if (c < 6f) return new float2(-1f, 0f);
        if (c < 12f)
        {
            float a = (c - 6f) * (2f * math.PI / 6f);
            return new float2(math.cos(a), math.sin(a));
        }
        return float2.zero;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        float t = now - StartTime;
        if (now - _lastLog >= 1f)
        {
            _lastLog = now;
            var p = AIBody.Pos(server, player);
            AIPlayerSpikeMod.Log($"SKILL 걷기시험: t={t:F1}s pos=({p.x:F2},{p.z:F2}) distFromStart={math.distance(p.xz, StartPos.xz):F2}");
        }
        return new BodyInput { move = Pattern(t), slot = -1 };
    }
}

// ------------------------------------------------------------------------------------------ test-only trace
// TEST-ONLY movement trace (work/2026-11/2026-11-04-이동-드득거림.md). Off unless the switch file
// AIPlayers/movetrace.on exists (checked once a second). One CSV line per frame in AIPlayers/movetrace-<n>.csv:
// what the mod sent, what the server applied, server and host-screen (client ghost) positions, the navigator state.
public static class AIMoveTrace
{
    public static bool On;
    internal static string Nav = "";      // filled by AINavigator while On
    private static float _lastCheck = -10f, _lastT;
    private static System.IO.StreamWriter _w;
    private static int _n;

    private static string Dir => System.IO.Path.Combine(Application.persistentDataPath, "AIPlayers");

    public static void Frame(World server, Entity player, ClientInput sent, uint thinTick, float now)
    {
        if (now - _lastCheck >= 1f)
        {
            _lastCheck = now;
            bool want = false;
            try { want = System.IO.File.Exists(System.IO.Path.Combine(Dir, "movetrace.on")); } catch { }
            if (want != On) Toggle(want);
        }
        if (!On || _w == null) { Nav = ""; return; }
        try
        {
            var em = server.EntityManager;
            uint stk = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Unity.NetCode.NetworkTime>()))
            {
                if (q.CalculateEntityCount() == 1) { var nt = q.GetSingleton<Unity.NetCode.NetworkTime>(); if (nt.ServerTick.IsValid) stk = nt.ServerTick.TickIndexForValidTick; }
            }
            float3 p = em.GetComponentData<LocalTransform>(player).Position;
            float2 si = float2.zero; uint sitk = 0;
            if (em.HasComponent<ClientInput>(player))
            {
                var ci = em.GetComponentData<ClientInput>(player);
                si = ci.movementDirection;
                sitk = ci.Tick.IsValid ? ci.Tick.TickIndexForValidTick : 0;
            }
            float2 cp = new float2(float.NaN, float.NaN);
            var cw = Manager.ecs != null ? Manager.ecs.ClientWorld : null;
            if (cw != null && cw.IsCreated && em.HasComponent<PlayerGhost>(player))
            {
                var guid = em.GetComponentData<PlayerGhost>(player).playerGuid;
                var cem = cw.EntityManager;
                using (var q = cem.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>()))
                using (var ents = q.ToEntityArray(Allocator.Temp))
                {
                    foreach (var e in ents)
                    {
                        if (cem.GetComponentData<PlayerGhost>(e).playerGuid == guid) { cp = cem.GetComponentData<LocalTransform>(e).Position.xz; break; }
                    }
                }
            }
            string skill = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current.Name : "-";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _w.WriteLine(string.Format(inv, "{0:F4},{1:F4},{2},{3},{4},{5:F3},{6:F3},{7:F3},{8:F3},{9},{10:F4},{11:F4},{12:F4},{13:F4},{14},{15},{16}",
                now, now - _lastT, stk, thinTick, skill, sent.movementDirection.x, sent.movementDirection.y, si.x, si.y, sitk,
                p.x, p.z, cp.x, cp.y, sent.IsButtonStateSet(CommandInputButtonStateNames.Interact_HeldDown) ? 1 : 0, AIBrain.InFlight ? "req" : "-", Nav));
            _lastT = now;
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("movetrace: frame failed; turning off", e); Toggle(false); }
        Nav = "";
    }

    private static void Toggle(bool on)
    {
        if (on)
        {
            try
            {
                _n++;
                string path = System.IO.Path.Combine(Dir, $"movetrace-{DateTime.Now:HHmmss}.csv");
                _w = new System.IO.StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = false };
                _w.WriteLine("t,dt,stk,ttk,skill,inx,inz,six,siz,sitk,px,pz,cx,cz,btn,brain,nav");
                On = true;
                AIPlayerSpikeMod.Log($"movetrace: ON -> {path}");
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("movetrace: open failed", e); On = false; _w = null; }
        }
        else
        {
            On = false;
            try { _w?.Flush(); _w?.Dispose(); } catch { }
            _w = null;
            AIPlayerSpikeMod.Log("movetrace: OFF");
        }
    }

    internal static string T(int2 v) => $"{v.x} {v.y}";
}
