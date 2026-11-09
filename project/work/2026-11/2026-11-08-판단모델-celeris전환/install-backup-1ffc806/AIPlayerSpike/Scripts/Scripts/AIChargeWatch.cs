// AIChargeWatch — the charge state of charging enemies (now: MushroomBrute) read from the server, for the close-fight
// observation, options and the emergency "anticipation started" trigger (combat stage 2,
// work/2026-11/2026-11-05-전투-적용-계획.md). Read only: the game state is never written.
// Source of the reading: the stage-0 probe (combat-s0 AIBruteProbe.cs): StateInfoCD.currentState == Charge and
// ChargeAttackStateCD.internalState / targetEntity / targetDirection / chargeConnected / cooldownTimer. Phase lengths are
// counted in server ticks seen by this watch (the timers read outside the system were ~0.14 s off, 2-측정.md §3).
// When it runs (review F1, 4-독립검수.md): once per frame from the decision loop of a character whose AI is on (AIBrain.Tick),
// and only for chargers within AIChargeMath.WatchTiles of such a character — with the AI off nothing is read. Its state
// is cleared when the world changes.
// Logs (Player.log, sparse, measurement only: with the test switch testcmds.on, AITestSwitches.Test): CHARGE (phase
// changes, one end line per charge with the target's health, the choices made during it and the first move), BRUTE-HP
// (each health change with the vulnerability state).
using System;
using System.Collections.Generic;
using System.Text;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public static class AIChargeWatch
{
    // Measured prediction profile only. Generic hit/approach/step and weapon-use controls are offered separately by
    // AICombatOptions to every observed enemy, including enemies without a readable/measured charge profile.
    public static bool IsCharger(ObjectID id) => id == ObjectID.MushroomBrute;

    private sealed class Brute
    {
        public Entity e; public int idx;
        public ChargePhase phase = ChargePhase.Other; public uint phaseTick; public bool seenStart;
        public uint connectTick; public uint exitTick; public bool exited;
        public int chargeN; public float antStart = -1f; public Entity target = Entity.Null; public int targetHp0 = -1;
        public int hp = -1, maxDmg = -1; public float3 pos; public float3 dir;
        public readonly List<string> picks = new List<string>(); public float firstMove = -1f; public float connectRun = -1f; public float3 antPos;
        public bool cdRunning; public float cdLeft;
        public uint lastSeenFrame;
    }

    private static readonly Dictionary<Entity, Brute> _brutes = new Dictionary<Entity, Brute>();
    private static int _frame = -1;
    private static uint _tick; private static float _rate = 20f;
    private static World _world;
    // anticipation-start events (emergency trigger): sequence, brute, its target. The sequence is never reset (the
    // characters keep the last one they saw).
    private static int _seq;
    private static readonly List<(int seq, Entity brute, int idx, int n, Entity target, float t)> _events = new List<(int, Entity, int, int, Entity, float)>();
    private static readonly List<V2> _ais = new List<V2>();

    public static float Seconds(uint ticks) => ticks / _rate;

    // Measurement lines only with the test switch (review F1).
    private static void MLog(string s) { if (AITestSwitches.Test) AIPlayerSpikeMod.Log(s); }

    // The world changed (or is unloading): forget every charger and event of the old one.
    public static void Reset(string why)
    {
        if (_brutes.Count > 0 || _events.Count > 0) using (AIAgent.Enter(null)) MLog($"CHARGE watch reset ({why}): forgot {_brutes.Count} charger(s), {_events.Count} event(s)");
        _brutes.Clear(); _events.Clear(); _frame = -1; _tick = 0; _world = null;
    }

    // Once per frame, from the decision loop of a character whose AI is on (AIBrain.Tick).
    public static void Frame(World server)
    {
        if (_frame == Time.frameCount) return;
        _frame = Time.frameCount;
        if (_world != server) { if (_world != null) Reset("world changed"); _world = server; }
        var em = server.EntityManager;
        // the characters whose AI is on (joined and walking): chargers near them only
        _ais.Clear();
        foreach (var a in AIAgent.All)
        {
            if (!AIPlayerSpikeMod.IsJoined(a)) continue;
            var p = AIPlayerSpikeMod.ServerPlayerOf(a);
            if (em.Exists(p) && em.HasComponent<LocalTransform>(p)) { var x = em.GetComponentData<LocalTransform>(p).Position; _ais.Add(new V2(x.x, x.z)); }
        }
        if (_ais.Count == 0) return;
        uint tick = 0;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>()))
            if (q.CalculateEntityCount() == 1) { var nt = q.GetSingleton<NetworkTime>(); if (nt.ServerTick.IsValid) tick = nt.ServerTick.TickIndexForValidTick; }
        if (tick == 0) return;
        _rate = AIMotion.TickRate(server);
        _tick = tick;
        float now = Time.realtimeSinceStartup;
        using (AIAgent.Enter(null))
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<ChargeAttackStateCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (!IsCharger(em.GetComponentData<ObjectDataCD>(e).objectID)) continue;
                var pos = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIChargeMath.Watched(new V2(pos.x, pos.z), _ais)) continue;   // far from every AI: not read (forgotten below)
                if (!_brutes.TryGetValue(e, out var b)) { b = new Brute { e = e, idx = e.Index }; _brutes[e] = b; }
                b.lastSeenFrame = (uint)_frame;
                Sample(em, b, tick, now);
            }
        }
        if (_brutes.Count > 0)
        {
            var gone = new List<Entity>();
            // not read this frame: a brute far from every camera is disabled by the game now and then (it comes back with
            // the same entity), so only a destroyed or dead one, or one that is no longer near any AI, is forgotten
            foreach (var kv in _brutes)
            {
                if (kv.Value.lastSeenFrame == (uint)_frame) continue;
                bool dead = !em.Exists(kv.Key) || (em.HasComponent<HealthCD>(kv.Key) && em.GetComponentData<HealthCD>(kv.Key).health <= 0);
                bool far = !dead && em.HasComponent<LocalTransform>(kv.Key) && !AIChargeMath.Watched(new V2(em.GetComponentData<LocalTransform>(kv.Key).Position.x, em.GetComponentData<LocalTransform>(kv.Key).Position.z), _ais);
                if (dead || far) gone.Add(kv.Key);
            }
            foreach (var g in gone)
            {
                using (AIAgent.Enter(null)) MLog($"CHARGE e={_brutes[g].idx} {(em.Exists(g) && !(em.HasComponent<HealthCD>(g) && em.GetComponentData<HealthCD>(g).health <= 0) ? $"left the watch (farther than {AIChargeMath.WatchTiles:F0} tiles from every AI)" : "gone (dead or removed)")}; charges {_brutes[g].chargeN}");
                _brutes.Remove(g);
            }
        }
    }

    private static void Sample(EntityManager em, Brute b, uint tick, float now)
    {
        var c = em.GetComponentData<ChargeAttackStateCD>(b.e);
        bool inCharge = em.HasComponent<StateInfoCD>(b.e) && em.GetComponentData<StateInfoCD>(b.e).currentState == StateID.Charge;
        b.pos = em.GetComponentData<LocalTransform>(b.e).Position;
        int hp = em.HasComponent<HealthCD>(b.e) ? em.GetComponentData<HealthCD>(b.e).health : -1;
        int maxDmg = em.HasComponent<DamageReductionCD>(b.e) ? em.GetComponentData<DamageReductionCD>(b.e).maxDamagePerHit : -1;
        b.cdRunning = c.cooldownTimer.isRunning;
        ChargePhase ph;
        if (!inCharge) ph = ChargePhase.Ready;
        else switch (c.internalState)
        {
            case ChargeAttackInternalState.ChargeAnticipation:
            case ChargeAttackInternalState.StartCharging: ph = ChargePhase.Anticipation; break;
            case ChargeAttackInternalState.Charging: ph = ChargePhase.Charging; break;
            case ChargeAttackInternalState.PendingCollided:
            case ChargeAttackInternalState.End:
            case ChargeAttackInternalState.LeaveState: ph = c.chargeConnected ? ChargePhase.Vulnerable : ChargePhase.Recover; break;
            default: ph = ChargePhase.Other; break;
        }
        if (math.lengthsq(c.targetDirection.xz) > 1e-6f) b.dir = c.targetDirection;
        bool first = !b.seenStart;
        if (first) { b.seenStart = true; b.phase = ph; b.phaseTick = tick; }
        if (ph != b.phase)
        {
            var old = b.phase;
            b.phase = ph; b.phaseTick = tick;
            if (ph == ChargePhase.Anticipation)
            {
                b.chargeN++; b.antStart = now; b.antPos = b.pos; b.target = c.targetEntity; b.picks.Clear(); b.firstMove = -1f; b.connectRun = -1f;
                b.targetHp0 = b.target != Entity.Null && em.Exists(b.target) && em.HasComponent<HealthCD>(b.target) ? em.GetComponentData<HealthCD>(b.target).health : -1;
                _seq++; _events.Add((_seq, b.e, b.idx, b.chargeN, b.target, now)); while (_events.Count > 16) _events.RemoveAt(0);
            }
            if (ph == ChargePhase.Vulnerable && old != ChargePhase.Vulnerable) { b.connectTick = tick; b.connectRun = math.distance(b.pos.xz, b.antPos.xz); }
            if (ph == ChargePhase.Ready && old != ChargePhase.Ready) { b.exitTick = tick; b.exited = true; End(em, b, old, now); }
            Log(em, b, old, ph, c, hp, now);
        }
        if (b.hp >= 0 && hp != b.hp)
            MLog($"BRUTE-HP e={b.idx} {b.hp}->{hp} vuln={(maxDmg == 0 ? 1 : 0)} phase={ph}{(ph == ChargePhase.Vulnerable ? $" vulnLeft={math.max(0f, AIChargeMath.Vulnerable - Seconds(tick - b.connectTick)):F2}" : "")} charge#{b.chargeN}");
        b.hp = hp; b.maxDmg = maxDmg;
        if (c.cooldownTimer.isRunning)
        {
            try { b.cdLeft = c.cooldownTimer.GetRemainingTime(em.World.Time.ElapsedTime); } catch { b.cdLeft = -1f; }
        }
    }

    private static string Name(EntityManager em, Entity e)
    {
        if (e == Entity.Null) return "none";
        foreach (var a in AIAgent.All) if (AIPlayerSpikeMod.ServerPlayerOf(a) == e) return "AI:" + a.Name;
        return em.Exists(e) && em.HasComponent<PlayerGhost>(e) ? $"player:{e.Index}" : $"e:{e.Index}";
    }

    private static void Log(EntityManager em, Brute b, ChargePhase old, ChargePhase ph, ChargeAttackStateCD c, int hp, float now)
    {
        if (!AITestSwitches.Test) return;   // measurement only (review F1)
        string tgt = Name(em, c.targetEntity);
        float tdist = c.targetEntity != Entity.Null && em.Exists(c.targetEntity) && em.HasComponent<LocalTransform>(c.targetEntity) ? math.distance(em.GetComponentData<LocalTransform>(c.targetEntity).Position.xz, b.pos.xz) : -1f;
        MLog($"CHARGE e={b.idx} #{b.chargeN} {old}->{ph} pos=({b.pos.x:F2},{b.pos.z:F2}) dir=({b.dir.x:F2},{b.dir.z:F2}) target={tgt} dist={tdist:F2} hp={hp}{(b.antStart >= 0f && ph != ChargePhase.Ready ? $" since-anticipation={now - b.antStart:F2}" : "")}");
    }

    // One line per charge when the charge state ends: did it hit its target (the target's health dropped), did it connect,
    // the options this target chose during it (with their predictions) and its first move.
    private static void End(EntityManager em, Brute b, ChargePhase last, float now)
    {
        if (b.antStart < 0f || !AITestSwitches.Test) return;   // measurement only (review F1)
        int hp1 = b.target != Entity.Null && em.Exists(b.target) && em.HasComponent<HealthCD>(b.target) ? em.GetComponentData<HealthCD>(b.target).health : -1;
        bool hit = b.targetHp0 > 0 && b.targetHp0 - hp1 >= 30;   // a downed player reads below 0 (S2 run: "45->-13 (not hit)" was a hit)
        MLog($"CHARGE e={b.idx} #{b.chargeN} end: target={Name(em, b.target)} hp {b.targetHp0}->{hp1} ({(hit ? "HIT" : "not hit")}), connected={(b.connectRun >= 0f ? $"1 run={b.connectRun:F1}" : "0")}, lasted {now - b.antStart:F2}s; firstMove={(b.firstMove >= 0f ? $"+{b.firstMove - b.antStart:F2}s" : "-")}; picks=[{string.Join("; ", b.picks)}]");
    }

    // ---- per character

    // The view of one charger for this character (null if not a tracked charger).
    public static ChargeView View(Entity brute, Entity player, out int chargeN)
    {
        chargeN = 0;
        if (!_brutes.TryGetValue(brute, out var b)) return null;
        chargeN = b.chargeN;
        float el = Seconds(_tick - b.phaseTick);
        var v = new ChargeView { phase = b.phase, phaseElapsed = el, pos = new V2(b.pos.x, b.pos.z) };
        var d = b.dir.xz; if (math.lengthsq(d) > 1e-6f) { d = math.normalize(d); v.dir = new V2(d.x, d.y); }
        v.targetKnown = b.target != Entity.Null;
        v.aimAtMe = b.target == player && (b.phase == ChargePhase.Anticipation || b.phase == ChargePhase.Charging);
        if (b.phase == ChargePhase.Vulnerable) v.vulnLeft = math.max(0f, AIChargeMath.Vulnerable - Seconds(_tick - b.connectTick));
        if (b.phase == ChargePhase.Recover) v.recoverLeft = math.max(0f, 1.6f - el);
        if (b.phase == ChargePhase.Ready) v.cooldownLeft = b.exited ? math.max(0f, AIChargeMath.CooldownAfter - Seconds(_tick - b.exitTick)) : (b.cdRunning ? math.max(0f, b.cdLeft) : -1f);
        return v;
    }

    // One anticipation aimed at a character (the emergency trigger).
    public struct Anticipation { public Entity brute; public int idx, n; public float at; }

    // The anticipations that started since `seen` and target this player (oldest first). Moves `seen` on.
    public static void NewAnticipations(Entity player, ref int seen, List<Anticipation> into)
    {
        foreach (var ev in _events)
        {
            if (ev.seq <= seen) continue;
            if (ev.target == player) into.Add(new Anticipation { brute = ev.brute, idx = ev.idx, n = ev.n, at = ev.t });
        }
        if (_events.Count > 0) seen = math.max(seen, _events[_events.Count - 1].seq);
    }

    // That charge (brute, its charge number) is still under way (anticipation or charging); pos: where the brute is now.
    public static bool ChargeStillOn(Entity brute, int n, out V2 pos)
    {
        pos = default;
        if (!_brutes.TryGetValue(brute, out var b)) return false;
        pos = new V2(b.pos.x, b.pos.z);
        return b.chargeN == n && (b.phase == ChargePhase.Anticipation || b.phase == ChargePhase.Charging);
    }

    public static int Seq => _seq;

    // A choice applied during a charge aimed at this player (for the CHARGE end line).
    public static void NotePick(Entity player, int req, string key, string pred, float now)
    {
        foreach (var b in _brutes.Values)
            if (b.antStart >= 0f && b.target == player && b.phase != ChargePhase.Ready)
                b.picks.Add($"#{req} {key} pred={pred} @+{now - b.antStart:F2}s phase={b.phase}");
    }

    // The first move of a short move during a charge aimed at this player.
    public static void NoteMove(Entity player, float now)
    {
        foreach (var b in _brutes.Values)
            if (b.antStart >= 0f && b.target == player && b.firstMove < 0f && (b.phase == ChargePhase.Anticipation || b.phase == ChargePhase.Charging))
                b.firstMove = now;
    }
}
