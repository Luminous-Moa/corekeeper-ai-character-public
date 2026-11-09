// AIBody.Commands — extracted from the existing implementation; behavior and state ownership are unchanged.
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

public static partial class AIBody
{

    // ------------------------------------------------------------- chat commands (host only, AI present)

    public static void HandleCommand(World server, Entity player, string arg, string[] parts)
    {
        float now = Time.realtimeSinceStartup;
        switch (arg)
        {
            case "look":
            case "보기":
                StartSkill(server, player, new LookSkill(), now);
                break;
            case "inv":
            case "가방":
                StartSkill(server, player, new InvSkill(), now);
                break;
            case "goto":
            case "가기":
            {
                if (parts.Length >= 3 && parts[2].Equals("me", StringComparison.OrdinalIgnoreCase) || parts.Length >= 3 && parts[2] == "나")
                {
                    float3 host = HostPosition(server, player);
                    StartSkill(server, player, new GotoSkill(host.xz.RoundToInt2(), "호스트"), now);
                }
                else if (parts.Length >= 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int z))
                {
                    StartSkill(server, player, new GotoSkill(new int2(x, z), "좌표"), now);
                }
                else
                {
                    AIPlayerSpikeMod.Reply("사용법: /ai goto me · /ai goto x z");
                }
                break;
            }
            case "mine":
            case "캐기":
            {
                if (parts.Length >= 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int z))
                {
                    StartSkill(server, player, new MineSkill(new int2(x, z), false), now);
                }
                else
                {
                    bool oreOnly = parts.Length >= 3 && (parts[2].Equals("ore", StringComparison.OrdinalIgnoreCase) || parts[2] == "광석");
                    StartSkill(server, player, new MineSkill(null, oreOnly), now);
                }
                break;
            }
            case "attack":
            case "공격":
                StartSkill(server, player, new AttackSkill(), now);
                break;
            case "stop":
            case "멈춤":
            {
                bool hadGoal = AIBrain.Goal.kind != GoalKind.None;
                AIPlanner.Disable("/ai stop");
                AIBrain.ClearGoal(new GoalEnd("/ai stop", "cancelled"));
                if (Current != null && Current.Status == SkillStatus.Running) Stop(server, player, now, "/ai stop", hard: true);
                else AIPlayerSpikeMod.Reply("AI: 하던 기술이 없습니다. 제자리에 서 있습니다.");
                if (hadGoal) AIPlayerSpikeMod.Reply("AI 목표 해제(/ai stop). 판단 호출 없음.");
                break;
            }
            case "goal":
            case "목표":
                AIBrain.HandleGoalCommand(server, player, parts);
                break;
            case "walktest":
                StartSkill(server, player, new WalkTestSkill(), now);
                break;
            case "dig":
            case "파기":
            {
                if (parts.Length >= 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int z))
                {
                    var t = new int2(x, z);
                    bool isWall = new AITileReader(server).HasWall(t, out _, out _, out _);
                    StartSkill(server, player, new TunnelSkill(t, isWall, false, "좌표"), now);
                }
                else AIPlayerSpikeMod.Reply("사용법: /ai dig x z (그 칸까지 벽을 뚫으며 감)");
                break;
            }
            case "craftinfo":
                AICraft.Diagnose(server, player);
                break;
            case "breaktest":
            {
                // TEST ONLY: break the nearest visible object of a kind (/ai breaktest Mushroom)
                if (parts.Length < 3 || !Enum.TryParse(parts[2], out ObjectID bid)) { AIPlayerSpikeMod.Reply("사용법: /ai breaktest <ObjectID>"); break; }
                Entity best = Entity.Null; float bd = float.MaxValue; float3 me = Pos(server, player);
                var emb = server.EntityManager;
                using (var q = emb.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
                using (var ents = q.ToEntityArray(Allocator.Temp))
                    foreach (var e in ents)
                    {
                        if (emb.GetComponentData<ObjectDataCD>(e).objectID != bid || BreakSkill.NoDamage.Contains(e)) continue;
                        float3 p = emb.GetComponentData<LocalTransform>(e).Position;
                        float d = math.distance(me.xz, p.xz);
                        if (AIVision.CanSee(me, p.xz) && d < bd) { bd = d; best = e; }
                    }
                if (best == Entity.Null) { AIPlayerSpikeMod.Reply($"보이는 {bid} 없음"); break; }
                StartSkill(server, player, new BreakSkill(best), now);
                break;
            }
            case "craft":
            {
                // TEST ONLY: craft directly (hand, or the nearest station in view that offers it)
                if (parts.Length < 3 || !Enum.TryParse(parts[2], out ObjectID cid)) { AIPlayerSpikeMod.Reply("사용법: /ai craft <ObjectID> [N]"); break; }
                int cn = parts.Length >= 4 && int.TryParse(parts[3], out int cnn) ? cnn : 1;
                if (AICraft.HandRecipes(server, player).Contains(cid)) { StartSkill(server, player, new CraftSkill(cid, cn, Entity.Null, "손"), now); break; }
                var cst = AICraft.StationsInView(server, player).Find(s => s.recipes.Contains(cid));
                if (cst == null) { AIPlayerSpikeMod.Reply($"{cid}: 손 제작도 아니고 보이는 제작대도 없음"); break; }
                StartSkill(server, player, new CraftSkill(cid, cn, cst.entity, $"{cst.id}"), now);
                break;
            }
            case "place":
            {
                // /ai place <ObjectID> [x z [force]] — with a tile: that tile (코어-안전지대 단계 1; force = try the
                // core's protected zone too, to measure it)
                if (parts.Length < 3 || !Enum.TryParse(parts[2], out ObjectID pid)) { AIPlayerSpikeMod.Reply("사용법: /ai place <ObjectID> [x z [force]]"); break; }
                if (parts.Length >= 5 && int.TryParse(parts[3], out int px) && int.TryParse(parts[4], out int pz))
                    StartSkill(server, player, new PlaceSkill(pid, new int2(px, pz), parts.Length >= 6 && parts[5].Equals("force", StringComparison.OrdinalIgnoreCase)), now);
                else StartSkill(server, player, new PlaceSkill(pid), now);
                break;
            }
            case "baseprobe":
                AIBaseProbe.Command(server, player, parts);
                break;
            case "edge":
                AIEdge.Command(server, player, parts);
                break;
            case "bridge":
                AIPlanner.HealthProbe();
                break;
            case "heals":
            {
                // TEST ONLY: which items heal (game data) — the AI's healing items and some common foods.
                var list = new List<string>();
                foreach (var h in AIItems.HealingInInventory(server, player)) list.Add($"{h.id} x{h.count}: {h.info}");
                var probe = new List<string>();
                foreach (var id in new[] { ObjectID.HeartBerry, ObjectID.FoodRation, ObjectID.Mushroom, ObjectID.GlowingTulipFlower, ObjectID.BombPepper, ObjectID.HealingPotion })
                    probe.Add($"{id}: {(string.IsNullOrEmpty(AIItems.HealInfo(server, id, 0)) ? "회복 없음" : AIItems.HealInfo(server, id, 0))}");
                AIPlayerSpikeMod.Log($"TEST-ONLY heals: inventory=[{string.Join("; ", list)}] probe=[{string.Join("; ", probe)}]");
                AIPlayerSpikeMod.Reply($"[시험용] 가방의 회복 아이템: {(list.Count == 0 ? "없음" : string.Join(", ", list))}");
                break;
            }
            case "give":
            {
                // TEST ONLY (ai2 tests): put N of an item into the AI's first empty slot on the server.
                if (parts.Length >= 3 && Enum.TryParse(parts[2], true, out ObjectID gid) && gid != ObjectID.None)
                {
                    int n = parts.Length >= 4 && int.TryParse(parts[3], out int gn) ? gn : 1;
                    ObjectInfo gi = PugDatabase.GetObjectInfo(gid);
                    int amount = gi != null && !gi.isStackable ? gi.initialAmount : n;
                    var em = server.EntityManager;
                    var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
                    bool put = false;
                    for (int i = 0; i < buf.Length && !put; i++)
                    {
                        if (buf[i].objectID != ObjectID.None) continue;
                        var s = buf[i]; s.objectData = new ObjectDataCD { objectID = gid, amount = amount }; s.auxDataIndex = 0; buf[i] = s; put = true;
                        AIPlayerSpikeMod.Log($"TEST-ONLY give: {gid} x{amount} into AI slot {i}");
                        AIPlayerSpikeMod.Reply($"[시험용] {gid} x{amount} → {AIAgent.Cur.Name} 가방 {i}번 칸");
                    }
                    if (!put) AIPlayerSpikeMod.Reply("[시험용] 빈 칸 없음");
                }
                else AIPlayerSpikeMod.Reply("사용법: /ai give <ObjectID> [개수]");
                break;
            }
            case "take":
            {
                // TEST ONLY (ai2 tests): remove every stack of an item from the AI's inventory on the server (e.g. take
                // the copper pick away to set up a "tool short" case).
                if (parts.Length >= 3 && Enum.TryParse(parts[2], true, out ObjectID tid) && tid != ObjectID.None)
                {
                    var em = server.EntityManager;
                    var buf = em.GetBuffer<ContainedObjectsBuffer>(player);
                    int n = 0;
                    for (int i = 0; i < buf.Length; i++)
                    {
                        if (buf[i].objectID != tid) continue;
                        var s = buf[i]; s.objectData = new ObjectDataCD(); s.auxDataIndex = 0; buf[i] = s; n++;
                    }
                    AIPlayerSpikeMod.Log($"TEST-ONLY take: removed {n} stack(s) of {tid} from the AI inventory");
                    AIPlayerSpikeMod.Reply($"[시험용] {AIAgent.Cur.Name} 가방에서 {tid} {n}칸 뺌");
                }
                else AIPlayerSpikeMod.Reply("사용법: /ai take <ObjectID>");
                break;
            }
            case "findwall":
            {
                // TEST ONLY (setup aid): nearest tiles the AI has seen (memory) with a wall of this kind.
                if (parts.Length < 3 || !Enum.TryParse(parts[2], true, out ObjectID wid)) { AIPlayerSpikeMod.Reply("사용법: /ai findwall <ObjectID>"); break; }
                var tr = new AITileReader(server);
                int2 c = TileOf(Pos(server, player));
                var found = new List<(float d, int2 t)>();
                var tsOk = new Dictionary<int, bool>();
                for (int x = -110; x <= 110; x++) for (int z = -110; z <= 110; z++)
                {
                    int2 t = c + new int2(x, z);
                    if (!AIMemory.SeenTile(t) || !tr.HasWall(t, out TileCD w, out _, out _)) continue;
                    if (!tsOk.TryGetValue(w.tileset, out bool ok)) { ok = AIWallData.TryGet(server, w.tileset, out _, out _, out ObjectID o) && o == wid; tsOk[w.tileset] = ok; }
                    if (ok) found.Add((math.length(new float2(x, z)), t));
                }
                found.Sort((a, b) => a.d.CompareTo(b.d));
                var fs = new List<string>();
                for (int i = 0; i < found.Count && fs.Count < 8; i++) fs.Add($"({found[i].t.x},{found[i].t.y}) {found[i].d:F0}");
                AIPlayerSpikeMod.Log($"TEST-ONLY findwall {wid}: {found.Count} seen tiles; nearest {string.Join(" | ", fs)}");
                AIPlayerSpikeMod.Reply($"[시험용] {wid} 본 칸 {found.Count}: {(fs.Count > 0 ? fs[0] : "-")}");
                break;
            }
            case "rmstation":
            {
                // TEST ONLY (ai2, to see "station seen gone"): destroy the crafting station entity at tile x z on the
                // server, as if someone had picked it up. Claude's memory notices only when it sees the spot.
                if (parts.Length < 4 || !int.TryParse(parts[2], out int rx) || !int.TryParse(parts[3], out int rz)) { AIPlayerSpikeMod.Reply("사용법: /ai rmstation x z"); break; }
                var em = server.EntityManager;
                Entity hit = Entity.Null; ObjectID hid = ObjectID.None;
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CanCraftObjectsBuffer>(), ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
                using (var ents = q.ToEntityArray(Allocator.Temp))
                    foreach (var e in ents)
                        if (!em.HasComponent<PlayerGhost>(e) && math.all(TileOf(em.GetComponentData<LocalTransform>(e).Position) == new int2(rx, rz))) { hit = e; hid = em.GetComponentData<ObjectDataCD>(e).objectID; }
                // also a chest (생활-기지 T10: "own chest gone" without the host breaking a chest that holds items)
                if (hit == Entity.Null)
                    using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<InventoryAutoTransferEnabledCD>(), ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
                    using (var ents = q.ToEntityArray(Allocator.Temp))
                        foreach (var e in ents)
                            if (!em.HasComponent<PlayerGhost>(e) && math.all(TileOf(em.GetComponentData<LocalTransform>(e).Position) == new int2(rx, rz))) { hit = e; hid = em.GetComponentData<ObjectDataCD>(e).objectID; }
                if (hit == Entity.Null) { AIPlayerSpikeMod.Reply("[시험용] 그 칸에 제작대·상자 없음"); break; }
                em.DestroyEntity(hit);
                AIPlayerSpikeMod.Log($"TEST-ONLY rmstation: destroyed {hid} at ({rx},{rz}) on the server");
                AIPlayerSpikeMod.Reply($"[시험용] {hid} ({rx},{rz}) 없앰");
                break;
            }
            case "hp":
            {
                // TEST ONLY (ai2 tests): set the AI's current health (for the low-health fight test).
                if (parts.Length >= 3 && int.TryParse(parts[2], out int hpv))
                {
                    var em = server.EntityManager;
                    var h = em.GetComponentData<HealthCD>(player);
                    int before = h.health;
                    h.health = math.clamp(hpv, 1, h.maxHealth);
                    em.SetComponentData(player, h);
                    AIPlayerSpikeMod.Log($"TEST-ONLY hp: AI health {before} -> {h.health}");
                    AIPlayerSpikeMod.Reply($"[시험용] {AIAgent.Cur.Name} 체력 {before} → {h.health}");
                }
                break;
            }
            case "hunger":
            {
                // TEST ONLY (생활-기지 tests): /ai hunger N sets the AI's hunger (hunger only drops with distance walked);
                // /ai hunger alone logs the hunger value of the bag's food and some common foods (game data).
                var em = server.EntityManager;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int hv) && em.HasComponent<HungerCD>(player))
                {
                    var hc = em.GetComponentData<HungerCD>(player);
                    int before = hc.hunger;
                    hc.hunger = math.clamp(hv, 0, 100);
                    em.SetComponentData(player, hc);
                    AIPlayerSpikeMod.Log($"TEST-ONLY hunger: AI hunger {before} -> {hc.hunger}");
                    AIPlayerSpikeMod.Reply($"[시험용] {AIAgent.Cur.Name} 배고픔 {before} → {hc.hunger}");
                    break;
                }
                var probe = new List<string>();
                foreach (var f in AIFood.InBag(server, player)) probe.Add($"bag {f.id} x{f.count} +{f.hunger}");
                foreach (var id in new[] { ObjectID.FoodRation, ObjectID.Mushroom, ObjectID.HeartBerry, ObjectID.BombPepper, ObjectID.GlowingTulipFlower, ObjectID.LarvaMeat })
                    probe.Add($"{id} +{AIFood.HungerAdd(server, id, 0)}");
                AIPlayerSpikeMod.Log($"TEST-ONLY hunger values: [{string.Join("; ", probe)}]");
                AIPlayerSpikeMod.Reply($"[시험용] 배고픔 값: {string.Join(", ", probe)}");
                break;
            }
            case "tiles":
                TestOnlyTiles(server, player);
                break;
            case "nonew":
                AIMemory.TestOnlyNothingNew = !AIMemory.TestOnlyNothingNew;
                AIPlayerSpikeMod.Log($"TEST-ONLY nonew: pretend nothing unseen = {AIMemory.TestOnlyNothingNew}");
                AIPlayerSpikeMod.Reply($"[시험용] 못 본 구역 없음 가정: {(AIMemory.TestOnlyNothingNew ? "켬" : "끔")}");
                break;
            case "hostto":
            {
                // TEST ONLY (user allowed moving the host): the game's own map-waypoint teleport for the HOST's local
                // player (PlayerController.QueueInputAction(UIInputAction.Teleport), as MapMarkerUIElement does).
                // "/ai hostto ai" = next to Claude; "/ai hostto x z" = that tile.
                float2 to;
                if (parts.Length >= 3 && (parts[2] == "ai" || parts[2] == "claude"))
                {
                    var tr = new AITileReader(server);
                    int2 c = TileOf(Pos(server, player));
                    int2 best = c;
                    foreach (var d in new[] { new int2(0, 2), new int2(0, -2), new int2(2, 0), new int2(-2, 0), new int2(0, 1), new int2(1, 0), new int2(-1, 0), new int2(0, -1) })
                        if (tr.Walkable(c + d)) { best = c + d; break; }
                    to = best;
                }
                else if (parts.Length >= 4 && int.TryParse(parts[2], out int hx) && int.TryParse(parts[3], out int hz)) to = new float2(hx, hz);
                else { AIPlayerSpikeMod.Reply("사용법: /ai hostto ai · /ai hostto x z"); break; }
                var pc = Manager.main != null ? Manager.main.player : null;
                if (pc == null) { AIPlayerSpikeMod.Reply("[시험용] 호스트 플레이어를 찾지 못함"); break; }
                float3 before = HostPosition(server, player);
                pc.QueueInputAction(new UIInputActionData { action = UIInputAction.Teleport, position = to });
                AIPlayerSpikeMod.Log($"TEST-ONLY hostto: host teleport queued from ({before.x:F1},{before.z:F1}) to ({to.x:F1},{to.y:F1}) via PlayerController.QueueInputAction(UIInputAction.Teleport)");
                AIPlayerSpikeMod.Reply($"[시험용] 호스트 순간이동 요청 ({to.x:F0},{to.y:F0}), 약 6초");
                break;
            }
            case "plan":
            {
                // TEST ONLY: plan a tunnel without digging; optional assumed damage to exercise tool-short avoidance.
                if (parts.Length >= 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int z))
                {
                    int dmg = parts.Length >= 5 && int.TryParse(parts[4], out int d) ? d : -1;
                    var plan = AITunnel.Plan(server, player, new int2(x, z), out string why, dmg);
                    if (plan == null)
                    {
                        AIPlayerSpikeMod.Log($"TEST-ONLY plan to ({x},{z}) dmg={dmg}: none: {why}");
                        AIPlayerSpikeMod.Reply($"[시험용] 계획 없음: {why}");
                    }
                    else
                    {
                        var ps = new List<string>();
                        foreach (var t in plan.path) ps.Add($"{t.x},{t.y}");
                        AIPlayerSpikeMod.Log($"TEST-ONLY plan to ({x},{z}) dmg={plan.damage} ({plan.damageNote}): path {plan.path.Count}, walls {plan.walls.Count} [{plan.WallSummary()}], hits {plan.totalHits}, tool-short avoided {plan.toolShortSeen} (e.g. {plan.toolShortExample}); path=[{string.Join(" ", ps)}]");
                        AIPlayerSpikeMod.Reply($"[시험용] 계획: 길 {plan.path.Count}칸, 벽 {plan.walls.Count}개({plan.WallSummary()}), 도구 부족 벽 {plan.toolShortSeen}개 피함, 가정 피해 {plan.damage}");
                    }
                }
                else AIPlayerSpikeMod.Reply("사용법: /ai plan x z [가정 피해]");
                break;
            }
            case "mem":
            case "기억":
                AIPlayerSpikeMod.Reply($"AI 탐색 기억: {AIMemory.Summary()}");
                AIPlayerSpikeMod.Log($"MEMORY summary: {AIMemory.Summary()}");
                break;
            case "where":
                TestOnlyWhere(server, player);
                break;
            case "arena":
                AIArena.Command(server, player, parts);
                break;
            case "placetile":
            case "placetilearea":
            case "removetile":
            case "removetilearea":
            case "spawn":
                // TEST ONLY (치트명령-이식): build test sites - tiles and objects, adapted from ChatCommands (AITestWorldEdit)
                AITestWorldEdit.Command(server, player, arg, parts);
                break;
            case "hold":
            case "tp":
                // TEST ONLY (치트명령-이식 추가 범위): freeze calls and automatic actions / teleport (AITestControl)
                AITestControl.Command(server, player, arg, parts);
                break;
            case "buff":
            {
                // TEST ONLY: give the AI a condition the way the game does (NewConditionsBuffer, e.g. AttackSystem.cs:1231),
                // to check that motion boundaries follow the real swing speed: /ai buff MeleeAttackSpeedIncrease 500 30
                if (parts.Length < 4 || !Enum.TryParse(parts[2], out ConditionID cond) || !int.TryParse(parts[3], out int val)) { AIPlayerSpikeMod.Reply("사용법: /ai buff <ConditionID> <값> [초]"); break; }
                float dur = parts.Length >= 5 && float.TryParse(parts[4], out float dd) ? dd : 30f;
                var emc = server.EntityManager;
                if (!emc.HasBuffer<NewConditionsBuffer>(player)) { AIPlayerSpikeMod.Reply("[시험용] 조건 버퍼 없음"); break; }
                emc.GetBuffer<NewConditionsBuffer>(player).Add(new NewConditionsBuffer { conditionData = new ConditionData { conditionID = cond, value = val, duration = dur, valueMultiplier = 1f } });
                AIPlayerSpikeMod.Log($"TEST-ONLY buff: {cond} value {val} for {dur:F0}s added to the AI (NewConditionsBuffer)");
                AIPlayerSpikeMod.Reply($"[시험용] {AIAgent.Cur.Name}에게 {cond} {val} {dur:F0}초");
                break;
            }
        }
    }

    // TEST ONLY (to find a target for the live attack test; not a skill, not what the AI knows): nearest enemies
    // anywhere on the server, ignoring vision.
    private static void TestOnlyWhere(World server, Entity player)
    {
        var em = server.EntityManager;
        float3 me = Pos(server, player);
        var list = new List<(float d, string s)>();
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<HealthCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                var h = em.GetComponentData<HealthCD>(e);
                if (h.health <= 0) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                var id = em.HasComponent<ObjectDataCD>(e) ? em.GetComponentData<ObjectDataCD>(e).objectID : ObjectID.None;
                float d = math.distance(p.xz, me.xz);
                list.Add((d, $"{id} ({p.x:F1},{p.z:F1}) d={d:F1} hp={h.health}"));
            }
        }
        list.Sort((a, b) => a.d.CompareTo(b.d));
        var top = new List<string>();
        for (int i = 0; i < list.Count && i < 6; i++) top.Add(list[i].s);
        AIPlayerSpikeMod.Log($"TEST-ONLY where: {list.Count} live enemies on server; nearest: {string.Join(" | ", top)}");
        AIPlayerSpikeMod.Reply($"[시험용] 서버 전체 적 {list.Count}: {(top.Count > 0 ? top[0] : "-")}");
    }

    // TEST ONLY (diagnosis of mining far from the host; not what the AI knows): tile entities and area-loading
    // radii around the AI and the host, and wall kinds by damage reduction.
    private static void TestOnlyTiles(World server, Entity player)
    {
        var em = server.EntityManager;
        float3 me = Pos(server, player);
        float3 host = HostPosition(server, player);
        string Keep(Entity e)
        {
            if (!em.HasComponent<KeepAreaLoadedCD>(e)) return "none";
            var k = em.GetComponentData<KeepAreaLoadedCD>(e);
            return $"keep={k.KeepLoadedRadius} start={k.StartLoadRadius} immediate={k.ImmediateLoadRadius}";
        }
        Entity hostE = Entity.Null;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
            foreach (var e in ents) if (e != player) { hostE = e; break; }
        int total = 0, nearAi = 0, nearHost = 0, anyNearAi = 0;
        float nearest = float.MaxValue, farFromHost = 0f;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<TileCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                float dAi = math.distance(p.xz, me.xz), dHost = math.distance(p.xz, host.xz);
                if (dAi <= 6f) anyNearAi++;
                if (em.GetComponentData<TileCD>(e).tileType != TileType.wall || !em.HasComponent<HealthCD>(e)) continue;
                total++;
                if (dAi <= 6f) nearAi++;
                if (dHost <= 6f) nearHost++;
                nearest = math.min(nearest, dAi);
                farFromHost = math.max(farFromHost, dHost);
            }
        }
        var tr = new AITileReader(server);
        int wallsAroundAi = 0;
        int2 c = TileOf(me);
        for (int x = -6; x <= 6; x++) for (int z = -6; z <= 6; z++) if (tr.HasWall(c + new int2(x, z), out _, out _, out _)) wallsAroundAi++;
        var hard = new List<string>();
        for (int ts = 0; ts < 160; ts++)
        {
            if (!AIWallData.TryGet(server, ts, out int hp, out int red, out ObjectID obj)) continue;
            if (red >= 15) hard.Add($"{(PugTilemap.Tileset)ts}:{obj} hp={hp} red={red}");
        }
        int est = AIWallData.EstimateMiningDamage(server, player, out int eff);
        AIPlayerSpikeMod.Log($"TEST-ONLY tiles: ai=({me.x:F1},{me.z:F1}) host=({host.x:F1},{host.z:F1}) dist={math.distance(me.xz, host.xz):F1}; server PlayerGhost.cameraPosition ai={(em.HasComponent<PlayerGhost>(player) ? em.GetComponentData<PlayerGhost>(player).cameraPosition.ToString() : "?")} host={(hostE != Entity.Null && em.HasComponent<PlayerGhost>(hostE) ? em.GetComponentData<PlayerGhost>(hostE).cameraPosition.ToString() : "?")}; ai KeepAreaLoaded {Keep(player)} AreaLoaded={em.HasComponent<AreaLoadedCD>(player)}; host KeepAreaLoaded {(hostE == Entity.Null ? "?" : Keep(hostE))}; wall tile entities total={total} within6(ai)={nearAi} within6(host)={nearHost} nearestToAi={(nearest == float.MaxValue ? -1 : nearest):F1} farthestFromHost={farFromHost:F1}; any TileCD entities within6(ai)={anyNearAi}; wall tiles in map within6(ai)={wallsAroundAi}; est damage {est}; walls with reduction>=15: [{string.Join(" | ", hard)}]");
        AIPlayerSpikeMod.Reply($"[시험용] 벽 타일 엔티티: AI 주변 {nearAi} (지도 벽 {wallsAroundAi}), 호스트 주변 {nearHost}, 전체 {total}, 호스트에서 가장 먼 것 {farFromHost:F0}칸");
    }

    public static bool IsSkillCommand(string arg)
    {
        switch (arg)
        {
            case "look": case "보기": case "inv": case "가방": case "goto": case "가기": case "mine": case "캐기":
            case "attack": case "공격": case "stop": case "멈춤": case "walktest": case "where": case "goal": case "목표":
            case "dig": case "파기": case "mem": case "기억": case "tiles": case "plan": case "hostto": case "nonew": case "heals": case "give": case "hp": case "bridge": case "craftinfo":
            case "breaktest": case "craft": case "place": case "take": case "findwall": case "rmstation": case "buff": case "arena":
            case "hunger": case "baseprobe": case "edge":
            case "placetile": case "placetilearea": case "removetile": case "removetilearea": case "spawn":
            case "hold": case "tp":
                return true;
        }
        return false;
    }
    public static bool IsTestCommand(string arg) => Array.IndexOf(TestCommands, arg) >= 0;
    public static string TestSwitchPath => System.IO.Path.Combine(Application.persistentDataPath, "AIPlayers", "testcmds.on");
    public static bool TestCommandsOn { get { try { return System.IO.File.Exists(TestSwitchPath); } catch { return false; } } }
}
