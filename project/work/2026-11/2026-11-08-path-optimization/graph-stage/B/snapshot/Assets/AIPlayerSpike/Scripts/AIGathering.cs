using System;
using System.Collections.Generic;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class AIGatherTarget
{
    public readonly string Kind, Key, Family, Description;
    public readonly int2 Tile;
    public readonly Entity Entity;
    public readonly ObjectID Item;
    public readonly int Variation, Tileset, OreTileset;
    public readonly int MaxYield; // -1: a native action's maximum result is unknown, not an assumed reward
    public readonly bool Ore, Progress;
    public AIGatherTarget(string kind, string key, string family, string description, int2 tile, Entity entity,
        ObjectID item, int variation, int tileset, bool ore, int oreTileset, bool progress, int maxYield = -1)
    { Kind = kind; Key = key; Family = family; Description = description; Tile = tile; Entity = entity;
      Item = item; Variation = variation; Tileset = tileset; Ore = ore; OreTileset = oreTileset; Progress = progress; MaxYield = maxYield; }
}

// Read-only snapshot: callers receive a copy rather than the mutable array that execution owns.
public sealed class AIGatherSelection
{
    private readonly AIGatherTarget[] _targets;
    public readonly string Key;
    public readonly Entity Player;
    public AIGatherSelection(Entity player, string key, IEnumerable<AIGatherTarget> targets)
    { Player = player; Key = key; _targets = new List<AIGatherTarget>(targets).ToArray(); }
    public AIGatherTarget[] Targets => (AIGatherTarget[])_targets.Clone();
    public int Count => _targets.Length;
}

public static class AIGathering
{
    public static void AddCandidates(World server, Entity player, AIObservation observation, List<AICandidate> candidates)
    {
        var targets = new List<AIGatherTarget>(); var keys = new HashSet<string>();
        var em = server.EntityManager; var tr = new AITileReader(server);
        // Item observation has no entity identity. Capture it here so a delayed answer never selects a replacement.
        var drops = new List<AIGatherTarget>();
        if (candidates.Exists(c => c.kind == "pickup"))
        {
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PickUpItemCD>(), ComponentType.ReadOnly<ContainedObjectsBuffer>(), ComponentType.ReadOnly<LocalTransform>()))
            using (var entities = query.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    var pos = em.GetComponentData<LocalTransform>(entity).Position;
                    if (!AIVision.CanSee(observation.pos, pos.xz)) continue;
                    var data = em.GetBuffer<ContainedObjectsBuffer>(entity, true);
                    if (data.Length == 0 || data[0].objectID == ObjectID.None) continue;
                    var pick = em.GetComponentData<PickUpItemCD>(entity);
                    if (pick.state == PickUpItemState.HasBeenPickedUp) continue;
                    var tile = AIBody.TileOf(pos);
                    drops.Add(new AIGatherTarget("pickup", "drop:" + entity.Index + ":" + entity.Version,
                        "pickup:" + data[0].objectID + ":" + data[0].variation, "", tile, entity,
                        data[0].objectID, data[0].variation, 0, false, 0, false, data[0].amount));
                }
        }
        foreach (var candidate in candidates)
        {
            if (!AIVision.CanSee(observation.pos, candidate.tile)) continue;
            AIGatherTarget target = null;
            if (candidate.kind == "mine" && tr.HasWall(candidate.tile, out var wall, out bool ore, out int oreTileset)
                && AIWallData.TryGet(server, wall.tileset, out _, out _, out ObjectID wallItem))
            {
                ObjectID product = wallItem;
                if (ore && AIBrain.TryOreObject(server, oreTileset, out ObjectID oreItem)) product = oreItem;
                target = new AIGatherTarget("mine", candidate.key, "mine:" + wall.tileset + ":" + ore + ":" + oreTileset,
                    candidate.desc, candidate.tile, Entity.Null, product, 0, wall.tileset, ore, oreTileset, candidate.progress);
            }
            else if (candidate.kind == "root")
            {
                foreach (var layer in tr.Layers(candidate.tile)) if (layer.tileType == TileType.bigRoot)
                { target = new AIGatherTarget("root", candidate.key, "root:" + layer.tileset, candidate.desc, candidate.tile,
                    Entity.Null, ObjectID.Wood, 0, layer.tileset, false, 0, candidate.progress); break; }
            }
            else if (candidate.kind == "break" && em.Exists(candidate.entity) && em.HasComponent<ObjectDataCD>(candidate.entity))
            {
                var data = em.GetComponentData<ObjectDataCD>(candidate.entity);
                target = new AIGatherTarget("break", candidate.key + ":" + candidate.entity.Version,
                    "break:" + data.objectID + ":" + data.variation, candidate.desc, candidate.tile, candidate.entity,
                    data.objectID, data.variation, 0, false, 0, candidate.progress);
            }
            else if (candidate.kind == "pickup")
                foreach (var drop in drops)
                    if (math.all(drop.Tile == candidate.tile) && (candidate.itemId == ObjectID.None || candidate.itemId == drop.Item) && keys.Add(drop.Key))
                        targets.Add(new AIGatherTarget(drop.Kind, drop.Key, drop.Family, candidate.desc, drop.Tile, drop.Entity,
                            drop.Item, drop.Variation, 0, false, 0, candidate.progress, drop.MaxYield));
            if (target != null)
            {
                candidate.itemId = target.Item;
                if (keys.Add(target.Key)) targets.Add(target);
            }
        }
        foreach (var group in AIGatheringRules.Components(targets, t => t.Family, t => t.Tile.x, t => t.Tile.y))
        {
            if (group.Count < 2) continue; // a single observed target already has its direct choice
            group.Sort((a, b) => math.distance(observation.pos.xz, (float2)a.Tile).CompareTo(math.distance(observation.pos.xz, (float2)b.Tile)));
            var names = new List<string>(); var identities = new List<string>(); bool progress = false;
            foreach (var target in group) { names.Add(target.Description); identities.Add(target.Key); progress |= target.Progress; }
            identities.Sort(StringComparer.Ordinal);
            string key = "gather:" + string.Join("|", identities);
            if (candidates.Exists(c => c.key == key)) continue;
            var selection = new AIGatherSelection(player, key, group);
            candidates.Add(new AICandidate { kind = "gather_group", key = key, gather = selection, tile = group[0].Tile,
                itemId = group[0].Item, progress = progress, targetLabel = "보이는 채집 묶음",
                desc = $"보이는 같은 종류 {group.Count}대상 묶음 채집 — 아래에 지정한 대상에 접근·채집·회수를 이어감. 새 대상은 포함하지 않음. 대상 수와 실제 확보량은 다름. 다른 행동으로 전환 가능. [{string.Join("; ", names)}]" });
        }
    }

    public static AISkill Make(AICandidate candidate) => candidate.gather == null ? null : new AIGatherSkill(candidate.gather);
    public static string LimitNote(World server, Entity player)
    {
        var notes = new List<string>();
        foreach (var limit in AIExpedition.ActiveLimits()) notes.Add($"원부탁#{limit.OrderId} 명시 확보 상한 {limit.Item} {limit.Maximum}개, 실제 누적 {limit.Acquired}");
        return notes.Count == 0 ? "" : string.Join("; ", notes) + ". 단순 목표와 별개이며 귀환·정리·정비에도 원부탁 조건 유지. 최대 산출량 미확인·불가분 물품 초과 가능 시 다음 채집을 보류하고 작은 대상/다른 행동/상위 재계획 선택. 주변 자동줍기까지 절대 수량 보장은 아님.";
    }
    public static string LimitReason(World server, Entity player, AICandidate candidate)
    {
        if (candidate == null || !HasExplicitLimit) return null;
        if (candidate.kind == "mine" || candidate.kind == "edgemat")
            return WallLimit(server, player, candidate.tile, candidate.kind);
        if (candidate.kind == "root") return RootLimit(server, player, candidate.tile, candidate.kind);
        if (candidate.kind == "break") return ObjectLimit(server, player, candidate.entity, candidate.kind);
        if (candidate.kind == "farm" && candidate.actionMode == "harvest") return HarvestCandidateLimit(server, player, candidate);
        if (candidate.kind == "dig")
        {
            var plan = AITunnel.Plan(server, player, candidate.tile, out string why, -1, candidate.digAllowEdge);
            if (plan == null) return UnknownDestruction(server, player, "dig 경로 " + candidate.tile + ": " + why);
            foreach (var wall in plan.walls)
            {
                string limited = WallLimit(server, player, wall.p, "dig 경로 벽");
                if (limited != null) return limited;
            }
            return null; // a reobserved route with no destructive action is an ordinary walk
        }
        if (candidate.gather != null)
        {
            var runtime = new AIGatherRuntime();
            foreach (var target in candidate.gather.Targets)
                if (runtime.Check(server, player, target) == null)
                    return LimitTarget(server, player, target);
            return null;
        }
        if (candidate.kind != "pickup" && candidate.kind != "edgepickup") return null;
        int upper = -1;
        if (candidate.kind == "pickup")
        {
            // The source candidate may be old. Sum current observed same-item stacks at its exact selected tile.
            long count = 0;
            var observation = AIObservation.Take(server, player, false);
            foreach (var item in observation.items)
                if (item.id == candidate.itemId && math.all(AIBody.TileOf(item.pos) == candidate.tile)) count += item.amount;
            if (count > 0 && count <= int.MaxValue) upper = (int)count;
        }
        foreach (var limit in AIExpedition.ActiveLimits())
            if (string.Equals(limit.Item, candidate.itemId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                string reason = AIGatheringRules.LimitReason(limit.Maximum, limit.Acquired, upper);
                if (reason != null) return $"원부탁#{limit.OrderId} {limit.Item}: " + reason;
            }
        return null;
    }
    internal static string LimitTarget(World server, Entity player, AIGatherTarget target, Func<ExpeditionLimit, long> acquired = null)
    {
        if (!HasExplicitLimit) return null;
        if (target.Kind == "mine") return WallLimit(server, player, target.Tile, "선택 묶음 mine");
        if (target.Kind == "root") return RootLimit(server, player, target.Tile, "선택 묶음 root");
        if (target.Kind == "break") return ObjectLimit(server, player, target.Entity, "선택 묶음 break");
        foreach (var limit in AIExpedition.ActiveLimits())
            if (string.Equals(limit.Item, target.Item.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                string reason = AIGatheringRules.LimitReason(limit.Maximum, acquired == null ? limit.Acquired : acquired(limit), target.Kind == "pickup" ? target.MaxYield : -1);
                if (reason != null) return $"원부탁#{limit.OrderId} {limit.Item}: " + reason;
            }
        return null;
    }
    // Runtime destruction gates inspect the real source. Candidate progress/item metadata is never a drop contract.
    public static string WallLimit(World server, Entity player, int2 tile, string action)
    {
        if (!HasExplicitLimit) return null;
        if (!AIVision.CanSee(AIBody.Pos(server, player), tile)) return UnknownDestruction(server, player, $"{action} ({tile.x},{tile.y})가 현재 보이지 않아 파괴 원본을 재검증하지 못함");
        if (!new AITileReader(server).HasWall(tile, out var wall, out bool ore, out int oreTileset)) return null;
        ObjectID source = ObjectID.None, oreItem = ObjectID.None;
        AIWallData.TryGet(server, wall.tileset, out _, out _, out source);
        bool knownOre = ore && AIBrain.TryOreObject(server, oreTileset, out oreItem);
        return UnknownDestruction(server, player, $"{action} 실제 벽 ({tile.x},{tile.y}) tileset={wall.tileset} 원본={source}, ore={ore}, 광석={(knownOre ? oreItem.ToString() : "미확정")}");
    }
    public static string RootLimit(World server, Entity player, int2 tile, string action)
    {
        if (!HasExplicitLimit) return null;
        if (!AIVision.CanSee(AIBody.Pos(server, player), tile)) return UnknownDestruction(server, player, $"{action} ({tile.x},{tile.y})가 현재 보이지 않아 뿌리를 재검증하지 못함");
        foreach (var layer in new AITileReader(server).Layers(tile))
            if (layer.tileType == TileType.bigRoot) return UnknownDestruction(server, player, $"{action} 실제 뿌리 ({tile.x},{tile.y}) tileset={layer.tileset}");
        return null;
    }
    public static string ObjectLimit(World server, Entity player, Entity entity, string action)
    {
        if (!HasExplicitLimit) return null;
        var em = server.EntityManager;
        if (!em.Exists(entity) || em.HasComponent<Disabled>(entity)
            || em.HasComponent<EntityDestroyedCD>(entity) && em.IsComponentEnabled<EntityDestroyedCD>(entity)
            || !em.HasComponent<HealthCD>(entity) || em.GetComponentData<HealthCD>(entity).health <= 0) return null;
        if (!em.HasComponent<LocalTransform>(entity) || !AIVision.CanSee(AIBody.Pos(server, player), em.GetComponentData<LocalTransform>(entity).Position.xz))
            return UnknownDestruction(server, player, $"{action} 선택 물체 entity={entity.Index}:{entity.Version}가 현재 보이지 않아 원본을 재검증하지 못함");
        string source = em.HasComponent<ObjectDataCD>(entity) ? em.GetComponentData<ObjectDataCD>(entity).objectID.ToString() : "미확정";
        string at = em.HasComponent<LocalTransform>(entity) ? AIBody.TileOf(em.GetComponentData<LocalTransform>(entity).Position).ToString() : "위치 미확정";
        return UnknownDestruction(server, player, $"{action} 실제 물체 entity={entity.Index}:{entity.Version}, 원본={source}, 위치={at}");
    }
    public static string HarvestCandidateLimit(World server, Entity player, AICandidate candidate)
    {
        if (!HasExplicitLimit || candidate.kind != "farm" || candidate.actionMode != "harvest") return null;
        foreach (var tile in candidate.tiles ?? new List<int2> { candidate.tile })
        {
            string limit = HarvestLimit(server, player, tile, "farm harvest 후보");
            if (limit != null) return limit;
        }
        return null;
    }
    public static string HarvestLimit(World server, Entity player, int2 tile, string action)
    {
        if (!HasExplicitLimit) return null;
        if (!AIVision.CanSee(AIBody.Pos(server, player), tile)) return UnknownDestruction(server, player, $"{action} ({tile.x},{tile.y})가 현재 보이지 않아 작물을 재검증하지 못함");
        if (!AIFarm.CropsInView(server, AIBody.Pos(server, player)).TryGetValue(tile, out var crop) || !crop.ripe) return null;
        return UnknownDestruction(server, player, $"{action} 실제 익은 작물 ({tile.x},{tile.y}) entity={crop.e.Index}:{crop.e.Version}, 원본={crop.id}, PlantCD 기본 산출={crop.harvest}x{crop.n}(보너스 포함 최대량 아님)");
    }
    private static bool HasExplicitLimit => AIExpedition.ActiveLimits().Count > 0;
    private static string UnknownDestruction(World server, Entity player, string source)
    {
        var reasons = new List<string>();
        foreach (var limit in AIExpedition.ActiveLimits()) reasons.Add($"원부탁#{limit.OrderId} {limit.Item}: "
            + AIGatheringRules.LimitReason(limit.Maximum, limit.Acquired, -1));
        return source + ". 조건 품목 이외 품목만 산출된다고 확정할 근거가 없고 최대 산출량도 미확정. " + string.Join("; ", reasons);
    }
    public static string Stale(World server, Entity player, AICandidate candidate)
    {
        var selection = candidate.gather;
        if (selection == null || selection.Player != player) return "채집 묶음의 대상 또는 캐릭터가 바뀜";
        var runtime = new AIGatherRuntime();
        foreach (var target in selection.Targets) if (runtime.Check(server, player, target) == null) return null;
        return "선택 당시 채집 대상이 모두 사라졌거나 현재 실행할 수 없음";
    }
}

// A world boundary allows offline verification of the actual production sequence, including interruption and recovery.
public interface IAIGatherRuntime
{
    string Check(World server, Entity player, AIGatherTarget target);
    AISkill Begin(AIGatherTarget target);
    AISkill Recovery(World server, Entity player, AIGatherTarget target);
    Dictionary<ObjectID, int> Counts(World server, Entity player);
    float3 Position(World server, Entity player);
}

public sealed class AIGatherRuntime : IAIGatherRuntime
{
    public string Check(World server, Entity player, AIGatherTarget target)
    {
        if (!AIVision.CanSee(AIBody.Pos(server, player), target.Tile)) return "선택한 대상이 현재 시야 밖임";
        var em = server.EntityManager; var tr = new AITileReader(server);
        if (target.Kind == "mine")
        {
            if (!tr.HasWall(target.Tile, out var wall, out bool ore, out int oreTileset)) return "선택한 벽이 이미 없음";
            return wall.tileset != target.Tileset || ore != target.Ore || oreTileset != target.OreTileset ? "선택한 벽/광석 종류가 바뀜" : null;
        }
        if (target.Kind == "root")
        {
            foreach (var layer in tr.Layers(target.Tile)) if (layer.tileType == TileType.bigRoot && layer.tileset == target.Tileset) return null;
            return "선택한 뿌리가 이미 없거나 종류가 바뀜";
        }
        if (!em.Exists(target.Entity) || em.HasComponent<Disabled>(target.Entity)) return "선택한 물체가 이미 없음";
        if (em.HasComponent<EntityDestroyedCD>(target.Entity) && em.IsComponentEnabled<EntityDestroyedCD>(target.Entity)) return "선택한 물체가 파괴됨";
        if (!em.HasComponent<LocalTransform>(target.Entity)) return "선택한 물체의 위치를 확인할 수 없음";
        var pos = em.GetComponentData<LocalTransform>(target.Entity).Position;
        if (!AIVision.CanSee(AIBody.Pos(server, player), pos.xz)) return "선택한 물체가 보이지 않음";
        if (!math.all(AIBody.TileOf(pos) == target.Tile)) return "선택한 물체가 관측 위치에서 이동함";
        if (target.Kind == "pickup")
        {
            if (!em.HasBuffer<ContainedObjectsBuffer>(target.Entity) || !em.HasComponent<PickUpItemCD>(target.Entity)) return "선택한 바닥 물품이 바뀜";
            var contents = em.GetBuffer<ContainedObjectsBuffer>(target.Entity, true);
            if (contents.Length == 0 || contents[0].objectID != target.Item || contents[0].variation != target.Variation) return "선택한 바닥 물품의 종류가 바뀜";
            if (target.MaxYield >= 0 && contents[0].amount > target.MaxYield) return "선택한 바닥 물품의 수량이 관측 뒤 늘어남";
            if (em.GetComponentData<PickUpItemCD>(target.Entity).state == PickUpItemState.HasBeenPickedUp) return "선택한 바닥 물품이 이미 회수됨";
            return AIBody.BagHasRoomFor(server, player, target.Item) ? null : "선택한 바닥 물품을 담을 가방 여유가 없음";
        }
        if (!em.HasComponent<ObjectDataCD>(target.Entity) || !em.HasComponent<HealthCD>(target.Entity)) return "선택한 물체를 부술 수 없음";
        var data = em.GetComponentData<ObjectDataCD>(target.Entity);
        return data.objectID != target.Item || data.variation != target.Variation || em.GetComponentData<HealthCD>(target.Entity).health <= 0
            ? "선택한 물체의 종류 또는 상태가 바뀜" : null;
    }
    public AISkill Begin(AIGatherTarget target)
    {
        switch (target.Kind)
        {
            case "mine": return new MineSkill(target.Tile, false);
            case "root": return new RootSkill(target.Tile);
            case "break": return new BreakSkill(target.Entity);
            case "pickup": return new GotoSkill(target.Tile, "선택한 바닥 물품 회수");
            default: return null;
        }
    }
    public AISkill Recovery(World server, Entity player, AIGatherTarget target)
    {
        if (target.Kind == "pickup") return null;
        // Individual mining/harvesting already observes the normal pickup transfer. Only approach again when
        // an actual visible drop remains at the selected target, rather than inserting a wait after every hit.
        var em = server.EntityManager;
        using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PickUpItemCD>(), ComponentType.ReadOnly<ContainedObjectsBuffer>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
            {
                var pos = em.GetComponentData<LocalTransform>(entity).Position;
                if (!AIVision.CanSee(AIBody.Pos(server, player), pos.xz) || math.distance(pos.xz, (float2)target.Tile) > 1.5f) continue;
                var contents = em.GetBuffer<ContainedObjectsBuffer>(entity, true);
                if (contents.Length == 0 || contents[0].objectID == ObjectID.None || em.GetComponentData<PickUpItemCD>(entity).state == PickUpItemState.HasBeenPickedUp) continue;
                return new GotoSkill(target.Tile, "선택한 채집 대상 주변에 관측된 떨어진 물품 회수");
            }
        return null;
    }
    public Dictionary<ObjectID, int> Counts(World server, Entity player) => AIBody.InventoryCounts(server, player);
    public float3 Position(World server, Entity player) => AIBody.Pos(server, player);
}

public sealed class AIGatherSkill : AISkill
{
    public readonly AIGatherSelection Selection;
    private readonly AIGatherTarget[] _targets;
    private readonly IAIGatherRuntime _world;
    private readonly Dictionary<ObjectID, int> _last = new Dictionary<ObjectID, int>();
    private readonly Dictionary<ObjectID, long> _received = new Dictionary<ObjectID, long>();
    private AISkill _child;
    private int _index, _completed, _skipped;
    private bool _recovering, _cleaned;
    private float _settleUntil = -1f;
    private readonly Dictionary<string, (long acquired, long received)> _limitStarts = new Dictionary<string, (long, long)>();
    private readonly HashSet<string> _seenResources = new HashSet<string>();
    public IReadOnlyDictionary<ObjectID, long> Received => _received;
    public int CompletedTargets => _completed;
    public AIGatherSelection RemainingSelection => new AIGatherSelection(Selection.Player, Selection.Key,
        new List<AIGatherTarget>(_targets).GetRange(_index, _targets.Length - _index));
    public AIGatherSkill(AIGatherSelection selection, IAIGatherRuntime runtime = null)
    { Selection = selection; _targets = selection.Targets; _world = runtime ?? new AIGatherRuntime(); Name = "묶음 채집"; Key = selection.Key; }
    public override bool Start(World server, Entity player, float now)
    {
        if (Selection.Player != player || _targets.Length == 0) { Status = SkillStatus.Failed; Reason = "채집 대상 또는 캐릭터가 바뀜"; return false; }
        foreach (var pair in _world.Counts(server, player)) _last[pair.Key] = pair.Value;
        foreach (var limit in AIExpedition.ActiveLimits()) LocalAcquired(limit);
        Discoveries(AIObservation.Take(server, player, true), false);
        Facts.Add($"선택 당시 고정 대상 {_targets.Length}개. 대상 수와 실제 가방 확보량을 별도로 기록.");
        return true;
    }
    private void Count(World server, Entity player) => AIGatheringRules.Observe(_last, _world.Counts(server, player), _received);
    private long LocalAcquired(ExpeditionLimit limit)
    {
        long received = 0;
        if (Enum.TryParse(limit.Item, true, out ObjectID item)) _received.TryGetValue(item, out received);
        string key = limit.OrderId + ":" + limit.Item;
        if (!_limitStarts.TryGetValue(key, out var start)) { start = (limit.Acquired, received); _limitStarts[key] = start; }
        return Math.Max(limit.Acquired, start.acquired + received - start.received);
    }
    // The brain's existing watch supplies visible observations. A new fact wakes judgement without
    // stopping this body, expanding the frozen selection, or treating each completed target as a new decision.
    public void ObserveDiscoveries(AIObservation observation) => Discoveries(observation, true);
    private void Discoveries(AIObservation observation, bool report)
    {
        if (observation == null || _cleaned || Status != SkillStatus.Running) return;
        var fresh = new List<string>();
        foreach (var root in observation.rootTiles)
            Notice("root:" + root.p.x + "," + root.p.y + ":" + root.tileset,
                "새로 보이는 뿌리 " + root.p.x + "," + root.p.y, report, fresh);
        foreach (var wall in observation.wallTiles)
            if (wall.ore) Notice("ore:" + wall.p.x + "," + wall.p.y + ":" + wall.oreTileset,
                "새로 보이는 광맥 " + wall.p.x + "," + wall.p.y, report, fresh);
        foreach (var item in observation.items)
        {
            var tile = AIBody.TileOf(item.pos);
            // Drops at selected targets are already covered by recovery, not an unrelated new task.
            if (Array.Exists(_targets, t => math.all(t.Tile == tile))) continue;
            Notice("drop:" + tile.x + "," + tile.y + ":" + item.id,
                "새로 보이는 바닥 물품 " + item.id + " @" + tile.x + "," + tile.y, report, fresh);
        }
        foreach (var item in observation.objects)
        {
            var tile = AIBody.TileOf(item.pos);
            Notice("object:" + tile.x + "," + tile.y + ":" + item.id,
                "새로 보이는 물체 " + item.id + " @" + tile.x + "," + tile.y, report, fresh);
        }
        if (fresh.Count > 0) ObserveOnce("new-visible:" + string.Join("|", fresh),
            string.Join("; ", fresh) + ". 현재 선택 묶음에는 추가하지 않음. 원래 작업을 계속하면서 다음 현장 판단에 제공.");
    }
    private void Notice(string key, string fact, bool report, List<string> fresh)
    { if (_seenResources.Add(key) && report) fresh.Add(fact); }
    private bool StartChild(World server, Entity player, float now, AISkill child)
    {
        _child = child;
        if (_child == null) return false;
        _child.StartTime = now; _child.StartPos = _world.Position(server, player);
        return _child.Start(server, player, now);
    }
    private void CloseChild(World server)
    {
        if (_child == null) return;
        Facts.Add(_child.Name + ": " + _child.Reason); Facts.AddRange(_child.Facts);
        _child.Cleanup(server); _child = null;
    }
    public override BodyInput Tick(World server, Entity player, float now)
    {
        if (_cleaned || Status != SkillStatus.Running) return BodyInput.Idle;
        Count(server, player);
        if (_settleUntil >= 0f)
        {
            if (now < _settleUntil) return BodyInput.Idle;
            if (_targets[_index].Kind == "pickup" && _world.Check(server, player, _targets[_index]) == null)
                return Finish(SkillStatus.Failed, "선택한 바닥 물품이 회수 접근 후에도 남아 있음. " + Summary());
            _settleUntil = -1f; _completed++; _index++; _recovering = false;
        }
        while (_index < _targets.Length)
        {
            if (_child == null)
            {
                string changed = _world.Check(server, player, _targets[_index]);
                if (changed != null)
                { _skipped++; Facts.Add(_targets[_index].Key + ": " + changed); _index++; continue; }
                string limited = AIGathering.LimitTarget(server, player, _targets[_index], LocalAcquired);
                if (limited != null)
                {
                    ObserveOnce("explicit-max-" + _index + ":" + limited, limited + ". 원래 부탁과 남은 선택은 유지; 다른 행동·개별 대상·재계획을 판단해야 함.");
                    return BodyInput.Idle;
                }
                if (!StartChild(server, player, now, _world.Begin(_targets[_index])))
                {
                    string reason = _child?.Reason ?? "지원되는 채집 실행이 없음"; CloseChild(server);
                    return Finish(SkillStatus.Failed, $"선택 묶음 {_index + 1}번째 시작 불가: {reason}. {Summary()}");
                }
            }
            BodyInput input = _child.Tick(server, player, now);
            Count(server, player);
            if (_child.Status == SkillStatus.Running) return input;
            bool success = _child.Status == SkillStatus.Done; string why = _child.Reason;
            CloseChild(server);
            if (!success) return Finish(SkillStatus.Failed, $"선택 묶음 {_index + 1}번째 {(_recovering ? "회수" : "채집")} 실패: {why}. {Summary()}");
            if (!_recovering)
            {
                _recovering = true;
                var recovery = _world.Recovery(server, player, _targets[_index]);
                if (recovery != null)
                {
                    if (!StartChild(server, player, now, recovery))
                    { string reason = _child?.Reason; CloseChild(server); return Finish(SkillStatus.Failed, "대상 처리 후 회수 접근 실패: " + reason + ". " + Summary()); }
                    continue;
                }
                if (_targets[_index].Kind != "pickup")
                { _completed++; _index++; _recovering = false; continue; }
            }
            // Allow the game's normal pickup transfer; this is an observation window, not an assumed item reward.
            _settleUntil = now + 3f;
            return BodyInput.Idle;
        }
        return Finish(SkillStatus.Done, "선택한 묶음 처리 종료. " + Summary());
    }
    private string Summary()
    {
        var amounts = new List<string>(); foreach (var pair in _received) amounts.Add(pair.Key + "+" + pair.Value);
        amounts.Sort(StringComparer.Ordinal);
        return $"완료 대상 {_completed}/{_targets.Length}, 사라짐/변경 {_skipped}, 관측 가방 증가 [{string.Join(", ", amounts)}]. 새 대상은 포함하지 않음";
    }
    public override string Progress(World server, Entity player) => Summary() + (_child == null ? "" : "; " + _child.Progress(server, player));
    public override float EtaSeconds(World server, Entity player) => _index == _targets.Length - 1 && _settleUntil >= 0f ? 0.2f : -1f;
    public override void Cleanup(World server) { if (_cleaned) return; _cleaned = true; CloseChild(server); }
}
