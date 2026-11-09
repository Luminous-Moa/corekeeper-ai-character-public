// AIActions — per candidate kind, the skill a chosen candidate starts (Make) and why an answer for it is no longer
// valid (Stale) (refactor S5, from the two switches of AIBrain.Apply and AIBrain.StaleReason; same branches and
// sentences). A new kind adds one entry here. Make reads the settings when it is called, not when the table is built.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class AIAction
{
    public Func<AICandidate, AISkill> Make;                  // null: starts no skill ("continue")
    public Func<World, Entity, AICandidate, string> Stale;   // null: nothing of its own to check
}

public static class AIActions
{
    private static readonly Dictionary<string, AIAction> Kinds = new Dictionary<string, AIAction>
    {
        ["mine"] = new AIAction { Make = c => new MineSkill(c.tile, false), Stale = MineStale },
        ["attack"] = new AIAction { Make = c => new AttackSkill(c.entity) { SecondaryUse = c.actionMode == "secondary" || c.actionMode == "summon", IndividualSummon = c.actionMode == "summon" }, Stale = AttackStale },
        ["attacknow"] = new AIAction { Make = c => new AttackSkill(c.entity) { NoDoorWait = true, SecondaryUse = c.actionMode == "secondary" || c.actionMode == "summon", IndividualSummon = c.actionMode == "summon" }, Stale = AttackStale },   // 재검수 R2b: hit now, the door left open
        ["pickup"] = new AIAction { Make = c => new GotoSkill(c.tile, "아이템"), Stale = PickupStale },
        ["explore"] = new AIAction { Make = c => AITravel.Make(c), Stale = (w, p, c) => AITravel.Stale(w, p, c) },
        ["travel"] = new AIAction { Make = c => AITravel.Make(c), Stale = (w, p, c) => AITravel.Stale(w, p, c) },
        ["travel_turn"] = new AIAction { Stale = (w, p, c) => AITravel.Stale(w, p, c) },
        ["gather_group"] = new AIAction { Make = c => AIGathering.Make(c), Stale = (w, p, c) => AIGathering.Stale(w, p, c) },
        ["follow"] = new AIAction { Make = c => new FollowSkill(c.entity, string.IsNullOrEmpty(c.targetLabel) ? "호스트" : c.targetLabel) },
        ["wait"] = new AIAction { Make = c => new WaitSkill(AIBrain.Settings().waitSeconds) },
        ["dig"] = new AIAction { Make = c => new TunnelSkill(c.tile, c.digWall, c.digOpenEnough, c.targetLabel) { EdgeOk = c.digEdgeOk, AllowEdge = c.digAllowEdge }, Stale = DigStale },   // 실게임 D1: a goto / place walk stops on edge walls not stated
        ["eat"] = new AIAction { Make = c => new EatSkill(c.itemId, c.itemVariation), Stale = EatStale },
        ["craft"] = new AIAction { Make = c => AICraft.Make(c), Stale = (w, p, c) => AICraft.Stale(w, p, c) },
        ["maintain"] = new AIAction { Make = c => AIMaintenance.Make(c), Stale = (w, p, c) => AIMaintenance.Stale(w, p, c) },
        ["break"] = new AIAction { Make = c => new BreakSkill(c.entity) },
        ["place"] = new AIAction { Make = c => c.placeAt ? new PlaceSkill(c.itemId, c.tile, false, c.placeStand, c.placeStandOnly) { Key = c.key.EndsWith(":out") ? c.key : $"place:{c.itemId}@{c.tile.x},{c.tile.y}" } : new PlaceSkill(c.itemId), Stale = PlaceStale },   // user decision A: the outside door option keeps its key
        // 코어-안전지대 단계 2: the base edge (AIEdge.cs)
        ["edgefill"] = new AIAction { Make = c => new EdgeFillSkill(c.edgeGap, c.tiles, null, c.key.EndsWith(":out")) },   // user decision A: ":out" closes from outside
        ["doorpass"] = new AIAction { Make = c => new DoorPassSkill(c.tile) },   // test command / not offered now (walking passes own doors)
        ["doorclose"] = new AIAction { Make = c => new DoorCloseSkill(c.tile) },
        // 코어-안전지대 단계 3: keeping the base (AIEdge.Guard.cs)
        ["edgerepair"] = new AIAction { Make = c => new EdgeFillSkill(c.edgeGap, c.tiles, c.group ?? c.tiles, c.key.EndsWith(":out")) },
        ["edgepickup"] = new AIAction { Make = c => new PickupSkill(c.tile, float.IsNaN(c.dest.x) ? (int2?)null : new int2((int)c.dest.x, (int)c.dest.y), c.pickupStandOnly ? c.pickupStand : (int2?)null, c.pickupStandOnly) },
        ["edgemat"] = new AIAction { Make = c => new MineSkill(c.tile, false), Stale = MineStale },   // running, its key is mine:x,y (GuardCandidates leaves it out then)
        ["store"] = new AIAction { Make = c => new StoreSkill(c.entity, c.itemId) },
        ["inventory"] = new AIAction { Make = c => new InventorySkill(c.inventory), Stale = (w, p, c) => AIInventory.Stale(w, p, c.inventory) },
        ["placetiles"] = new AIAction { Make = c => new PlaceTilesSkill(c.itemId, c.tiles) },   // 제작대-놓기 §2.11: a place goal's rect / line
        ["take"] = new AIAction { Make = c => new TakeSkill(c.entity, c.itemId, c.itemCount) },
        ["farm"] = new AIAction { Make = c => AIFarm.Make(c), Stale = (w, p, c) => AIFarm.Stale(w, p, c) },
        ["cook"] = new AIAction { Make = c => AICook.Make(c), Stale = (w, p, c) => AICook.Stale(w, p, c) },
        ["cookwait"] = new AIAction { Make = c => new WaitSkill(c.waitSeconds) { Key = c.key }, Stale = (w, p, c) => AICook.Stale(w, p, c) },
        ["root"] = new AIAction { Make = c => new RootSkill(c.tile) },
        ["away"] = new AIAction { Make = c => new KeepAwaySkill(5f, 3f), Stale = AwayStale },
        ["retreat"] = new AIAction { Make = c => new RetreatSkill(c.tile, string.IsNullOrEmpty(c.targetLabel) ? "코어 스폰 자리" : c.targetLabel, c.retreatInside) },   // 단계 3: retreatInside = the point is inside the base
        // combat stage 2: the short close-fight actions (AIShortSkills; applied at once, AIRunGate)
        ["step"] = new AIAction { Make = c => new StepSkill(c.key, c.dest, c.targetLabel, c.entity, c.obsAt) },
        ["hit"] = new AIAction { Make = c => new HitOnceSkill(c.entity), Stale = AttackStale },
        ["approach"] = new AIAction { Make = c => new ApproachSkill(c.entity, c.dest), Stale = AttackStale },
        ["hold"] = new AIAction { Make = c => new HoldSkill() },
        ["combo"] = new AIAction { Make = MakeCombo, Stale = AttackStale },
        ["continue"] = new AIAction { Stale = ContinueStale },
    };

    // Composite actions (user decision 2026-11-05): the stages the option named, with its end point and target.
    private static AISkill MakeCombo(AICandidate c)
    {
        switch (c.combo)
        {
            case "hit-move": return new ComboSkill(c.key, c.targetLabel, new HitOnceSkill(c.entity, ComboHitEnd), new StepSkill(c.key + "#2", c.dest, c.targetLabel, c.entity, c.obsAt));
            case "approach-hit": return new ComboSkill(c.key, c.targetLabel, new ApproachSkill(c.entity, c.dest), new HitOnceSkill(c.entity));
            default: return new ComboSkill(c.key, c.targetLabel, new StepSkill(c.key + "#1", c.dest, c.targetLabel, c.entity, c.obsAt), new HitOnceSkill(c.entity));
        }
    }
    public const float ComboHitEnd = 0.12f;   // hit then move: the move starts this long after the swing started (the hit lands ~0.07 s)

    private static AIAction Of(AICandidate c) => c.kind != null && Kinds.TryGetValue(c.kind, out var a) ? a : null;

    // the skill the candidate starts (null: none, as for "continue" or an unknown kind)
    public static AISkill Make(AICandidate c) { var a = Of(c); return a?.Make == null ? null : a.Make(c); }

    // why an answer that chose c is stale now (null: still valid)
    public static string Stale(World server, Entity player, AICandidate c)
    {
        string expeditionStale = AIExpedition.Stale(c);
        if (expeditionStale != null) return expeditionStale;
        if (c.expeditionNav != null) return null;
        if (c.kind == "expedition_resume") return c.resume == null ? "재개 대상이 없음" : Stale(server, player, c.resume);
        string gatheringLimit = AIGathering.LimitReason(server, player, c);
        if (gatheringLimit != null) return gatheringLimit;
        string lifeStale = AILife.Stale(server, player, c);
        if (lifeStale != null) return lifeStale;
        if (c.kind == "life_nav") return null;
        if (c.inventoryMenu != null)
        {
            if (AILife.BlocksInventoryView(server, player)) return "생활 목록이 열려 있어 가방 선택을 다시 조회해야 함";
            string menuStale = AIInventoryMenu.Stale(server, player, c);
            if (menuStale != null) return menuStale;
            if (c.kind == "inventory_menu") return null;
        }
        else if (c.kind == "inventory_menu") return "가방 메뉴 선택 토큰이 없음";
        if (c.kind == "stop_action")
        {
            var current = AIBody.Current;
            bool running = current != null && current.Status == SkillStatus.Running;
            if (c.run == 0) return running ? "중단 관측 뒤 새 실행이 시작됨" : null;
            return AIRunGate.ContinueStale(c.run, running, current?.Run ?? 0, "", current?.Key ?? "");
        }
        var a = Of(c);
        return a?.Stale == null ? null : a.Stale(server, player, c);
    }

    private static string MineStale(World server, Entity player, AICandidate c)
    {
        var tr = new AITileReader(server);
        if (!tr.HasWall(c.tile, out _, out _, out _)) return "대상 벽이 이미 없음";
        return null;
    }

    // a place option on a tile: the tile can no longer take it (filled, a wall, the AI on it, ...)
    private static string PlaceStale(World server, Entity player, AICandidate c)
    {
        if (!c.placeAt) return null;
        if (!AIPlaceSpot.ForTile(server, player, c.itemId, c.tile, false, null, c.placeStand, out _, out string why)) return why;
        return null;
    }

    private static string AttackStale(World server, Entity player, AICandidate c)
    {
        var em = server.EntityManager;
        if (!em.Exists(c.entity) || !em.HasComponent<HealthCD>(c.entity) || em.GetComponentData<HealthCD>(c.entity).health <= 0) return "대상 적이 없음";
        var p = em.GetComponentData<LocalTransform>(c.entity).Position;
        if (!AIVision.CanSee(AIBody.Pos(server, player), p.xz)) return "대상 적이 안 보임";
        return null;
    }

    private static string PickupStale(World server, Entity player, AICandidate c)
    {
        var o = AIObservation.Take(server, player, false);
        bool still = false;
        foreach (var i in o.items) if (math.distance(i.pos.xz, (float2)c.tile) < 1.2f) still = true;
        if (!still) return "아이템이 이미 없음";
        return null;
    }

    private static string AwayStale(World server, Entity player, AICandidate c)
    {
        if (!KeepAwaySkill.Nearest(server, player, out float ad, out _, out _)) return "보이는 적이 없음";
        if (ad >= 5f) return $"가장 가까운 적과 이미 {ad:F1}칸";
        return null;
    }

    private static string EatStale(World server, Entity player, AICandidate c)
    {
        AIBody.InventoryCounts(server, player).TryGetValue(c.itemId, out int have);
        if (have <= 0) return "먹을 것이 이미 없음";
        return null;
    }

    private static string ContinueStale(World server, Entity player, AICandidate c)
    {
        if (AIBody.Current == null || AIBody.Current.Status != SkillStatus.Running) return "계속할 기술이 이미 끝남";
        // "continue" means the action that ran when the request was built (its key). If another action started
        // since (a held switch applied at the motion boundary, an emergency switch), the answer is about
        // something that is no longer running: drop it and ask again with the new state, instead of letting it
        // keep the new action (ai2 swarm4 #827: keep-away "continue" kept a just-started attack running).
        if (!string.IsNullOrEmpty(c.key) && AIBody.Current.Key != c.key) return $"계속하라던 행동이 이미 바뀜({c.key}→{AIBody.Current.Key})";
        // combat stage 2: the same key, but another run of it (a new swing, a new move with the same option key) started
        // since the request was built: the answer was about the run that ended (AIRunGate)
        return AIRunGate.ContinueStale(c.run, true, AIBody.Current.Run, c.key, AIBody.Current.Key);
    }

    private static string DigStale(World server, Entity player, AICandidate c)
    {
        var em = server.EntityManager;
        // only a target in view can be checked (a remembered one out of view is checked on arrival)
        if (c.digWall && AIVision.CanSee(AIBody.Pos(server, player), c.tile))
        {
            var tr = new AITileReader(server);
            if (!tr.HasWall(c.tile, out _, out _, out _)) return "파 들어갈 대상 벽이 이미 없음";
        }
        if (c.entity != Entity.Null && (!em.Exists(c.entity) || !em.HasComponent<HealthCD>(c.entity) || em.GetComponentData<HealthCD>(c.entity).health <= 0)) return "대상 적이 없음";
        return null;
    }
}
