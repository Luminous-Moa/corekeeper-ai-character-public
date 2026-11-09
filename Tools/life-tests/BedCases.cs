// 2026-11-08 gap M2: the bed branch. AIBed.cs and SleepSkill are linked production source; this file holds the bed
// component shapes and the game's claim/sleep boundary (ClaimBedSystem: standing within 0.4 of the sleeping point of an
// unclaimed or own bed claims it and pushes Sleep; another player's bed is not slept in).
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Entities { public struct Hash128 { public uint x; public Hash128(uint v) { x = v; } } }
namespace PlayerState
{
    [Flags] public enum PlayerStateEnum { Null = 0, Walk = 4, Sleep = 1 << 20 }
    public struct PlayerStateCD { public PlayerStateEnum state; public bool HasAnyState(PlayerStateEnum s) => (state & s) != 0; }
}
public struct BedCD { }
public struct OccupiableCD { public float3 occupyOffsetForward, occupyOffsetRight, occupyOffsetBack, occupyOffsetLeft; }
public struct DirectionCD { public float3 direction; }
public static class DirectionBasedOnVariationCD { public static int GetVariationFromDirection(int2 d, bool zeroIsNone = false) => d.y > 0 ? 0 : d.x > 0 ? 1 : d.y < 0 ? 2 : d.x < 0 ? 3 : 0; }
public struct PlayerGuidCD { public Unity.Entities.Hash128 Value; }
public struct ClaimedByPlayerGuidCD { public Unity.Entities.Hash128 playerGuid; public bool isClaimed => playerGuid.x != 0; }

public static class BedCases
{
    static void Check(bool condition, string message) { Program.Check(condition, "bed: " + message); if (!condition) throw new Exception(message); }
    static Entity Bed(World w, float x, float y, uint claimedBy)
    {
        var em = w.EntityManager; var bed = em.New();
        em.Put(bed, new BedCD()); em.Put(bed, new ObjectDataCD { objectID = ObjectID.Station }); em.Put(bed, new LocalTransform { Position = new float3(x, 0, y) });
        em.Put(bed, new DirectionCD { direction = new float3(0, 0, -1) }); em.Put(bed, new OccupiableCD { occupyOffsetBack = new float3(0, 0, -0.5f) });
        em.Put(bed, new ClaimedByPlayerGuidCD { playerGuid = new Unity.Entities.Hash128(claimedBy) });
        return bed;
    }
    // ClaimBedSystem + Sleep: on the sleeping point of an unclaimed/own bed -> claim and sleep; moving away -> wake.
    static void Game(World w, Entity player, Entity bed, BodyInput input, float dt)
    {
        var em = w.EntityManager; var pos = AIBody.Pos(w, player);
        if (input.move.x != 0 || input.move.y != 0) { pos = new float3(pos.x + input.move.x * dt * 4, 0, pos.z + input.move.y * dt * 4); em.Put(player, new LocalTransform { Position = pos }); }
        var point = AIBed.Point(w, bed); var me = em.GetComponentData<PlayerGuidCD>(player).Value; var claim = em.GetComponentData<ClaimedByPlayerGuidCD>(bed);
        bool on = math.distance(pos.xz, point.xz) < 0.4f;
        if (on && !claim.isClaimed) { claim.playerGuid = me; em.Put(bed, claim); }
        bool own = claim.playerGuid.x == me.x;
        em.Put(player, new PlayerState.PlayerStateCD { state = on && own ? PlayerState.PlayerStateEnum.Sleep : PlayerState.PlayerStateEnum.Walk });
        if (on && own) { var h = em.GetComponentData<HealthCD>(player); h.health = Math.Min(h.maxHealth, h.health + 5); em.Put(player, h); }
    }
    public static void Run()
    {
        var f = FarmFixture.Create(0, 0, 1); var w = f.World; var em = w.EntityManager; var p = f.Player;
        AIGoals.Goal = new AIGoal { kind = GoalKind.None }; AILife.OnGoalChanged();
        em.Put(p, new PlayerGuidCD { Value = new Unity.Entities.Hash128(7) }); em.Put(p, new HealthCD { health = 40, maxHealth = 100 });
        var bed = Bed(w, 6, 0, 0); var taken = Bed(w, 8, 2, 9);
        var root = AILife.Cursor; root.Apply(root.Token("open"), AIAgent.Cur.Id, AIGoals.GoalRevision);
        Check(root.Check(root.Token("branch", "rest"), AIAgent.Cur.Id, AIGoals.GoalRevision) == null, "the life tree accepts the rest branch");
        var entries = AIBed.LifeEntries(w, p, f.Observation, "", new LifeSelection { Branch = "rest" });
        Check(entries.Exists(e => e.Op == "where" && e.Id == "bed:Station@6,0") && entries.Exists(e => e.Op == "" && e.Text.Contains("다른 플레이어")), "a free bed is a choice and another player's bed is a fact");
        Check(AIBed.ExactCandidate(w, p, f.Observation, "", new LifeSelection { Branch = "rest", Where = "bed:Station@8,2" }) == null, "another player's bed is not offered");
        var c = AIBed.ExactCandidate(w, p, f.Observation, "", new LifeSelection { Branch = "rest", Where = "bed:Station@6,0" });
        Check(c != null && c.kind == "sleep" && c.entity == bed && c.desc.Contains("체력 40/100"), "the bed leaf names the bed and the current health");
        var skill = AIBed.Make(c); Check(skill.Start(w, p, 0), "sleep starts");
        float now = 0; for (int i = 0; i < 400 && skill.Status == SkillStatus.Running; i++) { now += 0.1f; var input = skill.Tick(w, p, now); Game(w, p, bed, input, 0.1f); }
        var r = (ILifeResult)skill;
        Check(skill.Status == SkillStatus.Done && r.ConfirmedWork == 1 && r.ResultStage == "asleep" && em.GetComponentData<HealthCD>(p).health == 100, "the character steps onto the point, the game state shows sleep, and it ends at full health");
        Check(em.GetComponentData<ClaimedByPlayerGuidCD>(bed).playerGuid.x == 7, "the bed is claimed by the character through the game boundary");
        em.Put(taken, new ClaimedByPlayerGuidCD { playerGuid = new Unity.Entities.Hash128(9) });
        Check(AIBed.Stale(w, p, new AICandidate { entity = taken }) != null, "a bed claimed by someone else is stale");
    }
}
