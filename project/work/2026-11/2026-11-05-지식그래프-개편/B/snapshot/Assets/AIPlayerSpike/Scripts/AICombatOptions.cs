using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

// Species-independent short controls. A measured charge profile may add predictions, but does not grant the controls.
public static class AICombatOptions
{
    public static List<AICandidate> Candidates(World world, Entity player, AIObservation observation, string currentKey)
    {
        var result = new List<AICandidate>();
        var me = observation.pos.xz;
        var tiles = new AITileReader(world);
        var blocked = AIObjects.BlockedInView(world, player, out _);
        bool Clear(float2 destination)
        {
            float distance = math.distance(me, destination);
            for (float d = 0.2f; d <= distance + 0.2f; d += 0.2f)
            {
                float2 p = math.lerp(me, destination, math.min(d / math.max(distance, 0.01f), 1f));
                int2 t = new int2((int)math.round(p.x), (int)math.round(p.y));
                if (!AIVision.CanSee(observation.pos, t) || !tiles.Walkable(t) || blocked.Contains(t)) return false;
            }
            return true;
        }
        void Add(AICandidate candidate) { if (candidate.key != currentKey && !result.Exists(c => c.key == candidate.key)) result.Add(candidate); }
        var weapon = AIBody.FindWeapon(world, player);
        var info = PugDatabase.GetObjectInfo(weapon);
        bool canFight = AIBody.CanHold(world, player, false, out _, out _);
        bool distanceWeapon = info != null && AIHandCapabilities.IsDistanceWeapon(info.objectType.ToString());
        SecondaryUseCD secondary = default;
        bool hasSecondary = false;
        if (AIInventory.TryChosenHand(world, player, false, out int selected, out _, out _) && selected >= 0 && AIInventory.Read(world, player, selected, out var selectedItem))
        {
            var prefab = AIInventory.Prefab(world, selectedItem);
            if (prefab != Entity.Null && world.EntityManager.HasComponent<SecondaryUseCD>(prefab))
            { secondary = world.EntityManager.GetComponentData<SecondaryUseCD>(prefab); hasSecondary = secondary.hasSecondaryUse && secondary.hasWindup && secondary.windupTiers > 0; }
        }
        foreach (var enemy in observation.enemies)
        {
            float2 toward = enemy.pos.xz - me;
            if (math.lengthsq(toward) < 0.01f) toward = new float2(1, 0); else toward = math.normalize(toward);
            float2 left = new float2(-toward.y, toward.x);
            if (canFight && !distanceWeapon && enemy.dist <= HitOnceSkill.Reach)
                Add(new AICandidate { kind = "hit", key = "hit:" + enemy.entity.Index, entity = enemy.entity, obsAt = observation.time,
                    desc = $"한 번 치기 — {enemy.id} {enemy.dist:F1}칸, 체력 {enemy.health}/{enemy.maxHealth}. 한 번 사용하고 결과를 관측한 뒤 다시 선택. 이 적의 공격 예고/빈틈 시간은 확인되지 않았으면 모름." });
            else if (canFight && !distanceWeapon)
            {
                var point = enemy.pos.xz - toward * 1.1f;
                if (Clear(point)) Add(new AICandidate { kind = "approach", key = "approach:" + enemy.entity.Index, entity = enemy.entity, dest = point, obsAt = observation.time,
                    desc = $"사거리까지 접근 — {enemy.id} 쪽으로 {math.distance(me, point):F1}칸 이동. 공격은 따로 선택. 공격 예고/빈틈 시간은 모름." });
            }
            if (canFight && distanceWeapon)
                Add(new AICandidate { kind = "attack", key = "attack:" + enemy.entity.Index, entity = enemy.entity, obsAt = observation.time,
                    desc = $"조준해 기본 사용 — 선택한 {weapon}으로 보이는 {enemy.id} {enemy.dist:F1}칸을 조준해 기본 사용을 유지. 소환무기 기본 사용은 기존 소환수에게 명령하며 신규 소환은 보조 사용. 실제 무기 동작·비용은 게임이 처리. 적중·피해는 실행 후 관측하며 사거리·장애물·마나/탄약으로 실패할 수 있음. 제자리에서 사용하고 거리 조절은 따로 선택." });
            if (canFight && hasSecondary)
            {
                if (secondary.summonsMinion && secondary.cancelAttackIfNotAtWindupTier < secondary.windupTiers)
                    Add(new AICandidate { kind = "attack", actionMode = "summon", key = "attack:" + enemy.entity.Index + ":summon", entity = enemy.entity, obsAt = observation.time,
                        desc = $"개별 소환 — {weapon}을 {enemy.id} 방향으로 조준해 보조 충전을 시작하고 필요한 최소 충전 단계에서 최대 충전 전에 해제. 미최대 해제는 게임의 SpawnMinion 요청이며 마나/소환수 제한으로 실패하거나 기존 소환수를 교체할 수 있음. 지연으로 최대 충전에 이른 뒤 해제되면 같은 종류 해산/전체 교체로 바뀔 수 있어 결과를 확인해야 함. 입력만으로 소환 성공을 확정하지 않음." });
                Add(new AICandidate { kind = "attack", actionMode = "secondary", key = "attack:" + enemy.entity.Index + ":secondary", entity = enemy.entity, obsAt = observation.time,
                    desc = $"조준해 최대 충전 보조 사용 — {weapon}을 {enemy.id} 방향으로 조준하고 최대 충전 상태를 관측해 해제. {(secondary.summonsMinion ? "같은 종류 소환수가 있으면 그 종류를 전부 해산하고 새로 소환하지 않음. 같은 종류가 없으면 다른 소환수를 해산하고 전체 교체를 요청함." : "선택한 무기의 충전 보조 사용.")} 비용·소환·피해는 게임이 처리하며 입력만으로 성공을 확정하지 않음." });
            }
            var directions = new[] { (toward, "앞"), (-toward, "뒤"), (left, "왼쪽"), (-left, "오른쪽"), (math.normalize(toward + left), "왼쪽 앞"), (math.normalize(toward - left), "오른쪽 앞"), (math.normalize(-toward + left), "왼쪽 뒤"), (math.normalize(-toward - left), "오른쪽 뒤") };
            foreach (var direction in directions)
            {
                var destination = me + direction.Item1;
                if (observation.enemies.Exists(e => math.distance(destination, e.pos.xz) < 0.9f) || !Clear(destination)) continue;
                Add(new AICandidate { kind = "step", key = $"step:enemy{enemy.entity.Index}:{direction.Item2}:1", entity = enemy.entity, dest = destination, obsAt = observation.time,
                    targetLabel = $"{enemy.id} 기준 {direction.Item2} 한 칸", desc = $"짧은 이동 — {enemy.id} 기준 {direction.Item2} 한 칸. 선택한 지점까지 이동하며 공격하지 않음. 적이 제자리에 있다면 거리 {enemy.dist:F1}→{math.distance(destination, enemy.pos.xz):F1}칸. 피격을 피할 수 있다는 보장은 없음." });
            }
        }
        return result;
    }
}
