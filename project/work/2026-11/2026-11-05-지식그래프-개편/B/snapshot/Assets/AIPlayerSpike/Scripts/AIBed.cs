// AIBed — sleeping in a bed as a life choice (2026-11-08 선택지 공백 M2; session 코어키퍼-AI-캐릭터,
// work/2026-11/2026-11-08-선택지누락점검/agreement.md). Graph: game:bed-sleep (Bed 4760, acceptance:B-world-tools122).
//
// Game rules (decompile C:\Dev\_ref\corekeeper-decomp):
// - ClaimBedSystem: a player standing within 0.4 tiles (lengthsq < 0.16) of a bed's sleeping point (bed position +
//   OccupiableCD offset for the bed's DirectionCD, Sleep.GetOffsetAndFacingDirectionFromOccupiable) claims the bed when it
//   is unclaimed (ClaimedByPlayerGuidCD.playerGuid = the player's PlayerGuidCD) and, with no enemy within 15 tiles
//   (HasAnyNearbyEnemy; cattle and befriended factions excepted), pushes PlayerStateEnum.Sleep (TrySleepInClaimedBed).
//   A bed claimed by another player is not slept in (an emote only). There is no button: standing on the spot is the input.
// - Sleep.EnterState adds HealOverTimePercentage 20 (more with ExtraHealFromSleepPercentage) while asleep; after 2 s the
//   quality-sleep timer may add buffs (BuffsFromQualitySleep). Sleep.UpdateState pops the state when the player moves
//   (targetMovementVelocity > 0.1 after 0.3 s) or leaves the spot, so the next action that walks wakes the character.
// The judgment model chooses the bed and when to stop (another choice); the skill only walks onto the spot, stands still
// and reports what the game state shows (asleep or not, health).
using System;
using System.Collections.Generic;
using PlayerState;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class AIBed
{
    public const float SleepPoint = 0.35f;      // inside the game's 0.4-tile claim/sleep radius
    public const float MaxSleepSeconds = 60f;   // one choice sleeps at most this long; the model can choose again
    public const float QualitySleepSeconds = 2f;   // Sleep: quality-sleep buffs may come only after 2 s asleep (review L10)

    public sealed class BedView
    {
        public Entity entity; public ObjectID id; public float3 pos, point; public int2 tile; public float dist;
        public bool claimed, mine;
        public string Key => $"bed:{id}@{tile.x},{tile.y}";
        public string State => mine ? "내 침대(내가 차지함)" : claimed ? "다른 플레이어가 차지한 침대 — 게임 규칙상 잘 수 없음" : "빈 침대(누우면 내 침대가 됨)";
    }

    private static bool Guid(World w, Entity e, out Unity.Entities.Hash128 guid)
    {
        guid = default;
        if (!w.EntityManager.HasComponent<PlayerGuidCD>(e)) return false;
        guid = w.EntityManager.GetComponentData<PlayerGuidCD>(e).Value; return true;
    }

    // The sleeping point (Sleep.GetOffsetAndFacingDirectionFromOccupiable): position + the OccupiableCD offset of the
    // bed's direction variation (0 forward, 1 right, 2 back, 3 left).
    public static float3 Point(World w, Entity bed)
    {
        var em = w.EntityManager;
        float3 pos = em.GetComponentData<LocalTransform>(bed).Position;
        if (!em.HasComponent<DirectionCD>(bed) || !em.HasComponent<OccupiableCD>(bed)) return pos;
        float3 d = em.GetComponentData<DirectionCD>(bed).direction;
        var o = em.GetComponentData<OccupiableCD>(bed);
        switch (DirectionBasedOnVariationCD.GetVariationFromDirection(new int2((int)math.round(d.x), (int)math.round(d.z))))
        {
            case 0: return pos + o.occupyOffsetForward;
            case 1: return pos + o.occupyOffsetRight;
            case 2: return pos + o.occupyOffsetBack;
            case 3: return pos + o.occupyOffsetLeft;
            default: return pos;
        }
    }

    public static List<BedView> BedsInView(World w, Entity player)
    {
        var res = new List<BedView>();
        var em = w.EntityManager;
        float3 me = AIBody.Pos(w, player);
        bool hasGuid = Guid(w, player, out var myGuid);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BedCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var v = new BedView { entity = e, id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None, pos = p, tile = AIBody.TileOf(p), dist = math.distance(me.xz, p.xz), point = Point(w, e) };
                if (em.HasComponent<ClaimedByPlayerGuidCD>(e))
                {
                    var c = em.GetComponentData<ClaimedByPlayerGuidCD>(e);
                    v.claimed = c.isClaimed; v.mine = c.isClaimed && hasGuid && c.playerGuid.Equals(myGuid);
                }
                res.Add(v);
            }
        }
        res.Sort((a, b) => a.dist.CompareTo(b.dist));
        return res;
    }

    public static string HealthText(World w, Entity player)
    {
        if (!w.EntityManager.HasComponent<HealthCD>(player)) return "체력 모름";
        var h = w.EntityManager.GetComponentData<HealthCD>(player); return $"체력 {h.health}/{h.maxHealth}";
    }

    public static List<AILife.Entry> LifeEntries(World w, Entity p, AIObservation o, string currentKey, LifeSelection s)
    {
        var entries = new List<AILife.Entry>();
        var beds = BedsInView(w, p);
        if (string.IsNullOrEmpty(s.Where))
        {
            foreach (var bed in beds)
            {
                if (bed.claimed && !bed.mine) { entries.Add(new AILife.Entry { Text = $"{bed.Key} {bed.dist:F1}칸: {bed.State}" }); continue; }
                entries.Add(new AILife.Entry { Op = "where", Id = bed.Key, Text = $"{bed.Key} {bed.dist:F1}칸 — {bed.State}" });
            }
            if (beds.Count == 0) entries.Add(new AILife.Entry { Text = "현재 보이는 침대 없음" });
            return entries;
        }
        var candidate = ExactCandidate(w, p, o, currentKey, s);
        if (candidate != null) entries.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate });
        else entries.Add(new AILife.Entry { Text = "선택한 침대가 지금 보이지 않거나 다른 플레이어가 차지함" });
        return entries;
    }

    public static AICandidate ExactCandidate(World w, Entity p, AIObservation o, string currentKey, LifeSelection s)
    {
        if (s == null || string.IsNullOrEmpty(s.Where)) return null;
        var bed = BedsInView(w, p).Find(b => b.Key == s.Where);
        if (bed == null || bed.claimed && !bed.mine) return null;
        string key = $"sleep:{bed.entity.Index}:{bed.entity.Version}";
        return new AICandidate { kind = "sleep", key = key, entity = bed.entity, tile = bed.tile, life = s.Copy(), progress = false,
            desc = $"침대에서 자기 — {bed.Key} {bed.dist:F1}칸, {bed.State}. 지금 {HealthText(w, p)}. 잠자리에 서면 게임이 재움(자는 동안 체력 회복). 15칸 안에 적이 있으면 잠들지 못함(게임 규칙). 체력이 차고 잠든 지 {QualitySleepSeconds:F0}초가 넘거나 {MaxSleepSeconds:F0}초가 지나면 끝; 다른 행동을 고르면 일어남." };
    }

    public static string Stale(World w, Entity p, AICandidate c)
    {
        var bed = BedsInView(w, p).Find(b => b.entity == c.entity);
        return bed == null ? "선택한 침대가 지금 보이지 않음" : bed.claimed && !bed.mine ? "선택한 침대를 다른 플레이어가 차지함" : null;
    }

    public static AISkill Make(AICandidate c) => new SleepSkill(c.entity) { Key = c.key };

    public static bool Asleep(World w, Entity player) =>
        w.EntityManager.HasComponent<PlayerStateCD>(player) && w.EntityManager.GetComponentData<PlayerStateCD>(player).HasAnyState(PlayerStateEnum.Sleep);
}

// Walk next to the bed, step onto its sleeping point and stand still; the game claims the bed and puts the character to
// sleep. Done when health is full or MaxSleepSeconds pass asleep; the result says whether the game state showed sleep.
public class SleepSkill : AISkill, ILifeResult
{
    private readonly Entity _bed;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _wasAsleep;
    private float _onPointAt = -1f, _asleepAt = -1f, _stepAt = -1f;
    private int _hp0;
    public int ConfirmedWork { get; private set; }
    public int ConfirmedReceived => 0;
    public int RequestedWork => 1;
    public string ResultStage { get; private set; } = "not_asleep";
    public SleepSkill(Entity bed) { _bed = bed; Name = "침대에서 자기"; Key = $"sleep:{bed.Index}:{bed.Version}"; }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!em.Exists(_bed) || !em.HasComponent<LocalTransform>(_bed)) { Status = SkillStatus.Failed; Reason = "침대가 없음"; return false; }
        _hp0 = em.HasComponent<HealthCD>(player) ? em.GetComponentData<HealthCD>(player).health : 0;
        var point = AIBed.Point(server, _bed);
        Facts.Add($"침대 {_bed.Index}:{_bed.Version} 잠자리 ({point.x:F1},{point.z:F1}), {AIBed.HealthText(server, player)}");
        if (math.distance(AIBody.Pos(server, player).xz, point.xz) > 1.5f)
        { _nav.Start(server, player, AIBody.TileOf(em.GetComponentData<LocalTransform>(_bed).Position), 1.2f, now, _bed); _navOn = true; }
        return true;
    }

    private BodyInput End(World w, Entity p, SkillStatus status, string why)
    {
        int hp = w.EntityManager.HasComponent<HealthCD>(p) ? w.EntityManager.GetComponentData<HealthCD>(p).health : 0;
        string text = $"{why} — 잠듦 확인 {(_wasAsleep ? "예" : "아니오")}, 체력 {_hp0}→{hp}{(w.EntityManager.HasComponent<HealthCD>(p) ? "/" + w.EntityManager.GetComponentData<HealthCD>(p).maxHealth : "")}";
        Facts.Add(text);
        return Finish(status, text);
    }

    public override BodyInput Tick(World w, Entity p, float now)
    {
        var em = w.EntityManager;
        if (!em.Exists(_bed) || !em.HasComponent<LocalTransform>(_bed)) return End(w, p, _wasAsleep ? SkillStatus.Done : SkillStatus.Failed, "침대가 사라짐");
        bool asleep = AIBed.Asleep(w, p);
        if (asleep && !_wasAsleep) { _wasAsleep = true; _asleepAt = now; ConfirmedWork = 1; ResultStage = "asleep"; Facts.Add($"게임 상태 Sleep 확인 ({AIBed.HealthText(w, p)})"); }
        if (_wasAsleep)
        {
            var h = em.HasComponent<HealthCD>(p) ? em.GetComponentData<HealthCD>(p) : default;
            if (!asleep) return End(w, p, SkillStatus.Done, "잠에서 깸(게임 상태 Sleep 해제)");
            // Review L10: the game may give quality-sleep buffs only after 2 s asleep, so a full-health sleep lasts that long.
            if (h.maxHealth > 0 && h.health >= h.maxHealth && now - _asleepAt >= AIBed.QualitySleepSeconds) return End(w, p, SkillStatus.Done, "체력 가득(잠든 뒤 " + AIBed.QualitySleepSeconds.ToString("F0") + "초 이상)");
            if (now - _asleepAt >= AIBed.MaxSleepSeconds) return End(w, p, SkillStatus.Done, $"{AIBed.MaxSleepSeconds:F0}초 잠");
            return new BodyInput { slot = -1 };   // stand still: any move would wake
        }
        float3 me = AIBody.Pos(w, p);
        float3 point = AIBed.Point(w, _bed);
        float d = math.distance(me.xz, point.xz);
        if (_navOn)
        {
            if (d <= 1.5f) { _nav.Dispose(w); _navOn = false; }
            else
            {
                var st = _nav.Tick(w, p, now, out float2 move);
                if (st == NavStatus.Failed) return End(w, p, SkillStatus.Failed, "침대로 가지 못함: " + _nav.FailReason);
                if (st == NavStatus.Moving) return new BodyInput { move = move, slot = -1 };
                _nav.Dispose(w); _navOn = false;
            }
        }
        if (d > AIBed.SleepPoint)
        {
            if (_stepAt < 0f) _stepAt = now;
            if (now - _stepAt > 4f) return End(w, p, SkillStatus.Failed, $"잠자리에 올라서지 못함(남은 거리 {d:F2}칸)");
            _onPointAt = -1f;
            return new BodyInput { move = math.normalize(point.xz - me.xz), slot = -1 };
        }
        if (_onPointAt < 0f) _onPointAt = now;
        if (now - _onPointAt > 5f)
        {
            bool claimedByOther = false;
            if (em.HasComponent<ClaimedByPlayerGuidCD>(_bed) && em.HasComponent<PlayerGuidCD>(p))
            { var c = em.GetComponentData<ClaimedByPlayerGuidCD>(_bed); claimedByOther = c.isClaimed && !c.playerGuid.Equals(em.GetComponentData<PlayerGuidCD>(p).Value); }
            return End(w, p, SkillStatus.Failed, claimedByOther ? "다른 플레이어가 차지한 침대라 잠들지 않음" : "잠자리에 5초 서 있었으나 잠들지 않음(근처 적 등 게임 조건 미충족 가능)");
        }
        return new BodyInput { slot = -1 };
    }

    public override string Progress(World server, Entity player) => _wasAsleep ? $"자는 중, {AIBed.HealthText(server, player)}" : _navOn ? "침대로 가는 중" : "잠자리에 서는 중";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
