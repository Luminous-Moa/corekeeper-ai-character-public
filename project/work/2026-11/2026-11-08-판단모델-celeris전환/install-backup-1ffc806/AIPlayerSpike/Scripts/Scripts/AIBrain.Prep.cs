// AIBrain's preparation path (refactor S6b, moved unchanged from AIBrain.Loop.cs): what a goal needs to gather or
// craft now (WantedItems, the preparation note) and the pick tiers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;

public static partial class AIBrain
{
    // ------------------------------------------------------------------ crafting (preparation path, v2 §3)

    private static string _prepNote { get => S._prepNote; set => S._prepNote = value; }
    public static string PrepNote => _prepNote;   // last preparation note (what is needed to craft, for the upper-LLM report)
    private static bool Gatherable(ObjectID id, World world, Entity player) => AIKnowledge.KnownItems(world, player).Contains(id) && !AICraft.KnownRecipe(world, player, id, out _);

    // Items to gather now (the gather goal's item, or the missing gatherable materials of something to craft) and
    // craft candidates that can start now (recipe offered by hand or by a station in view, materials in the bag).
    // What cannot be crafted yet goes to _prepNote ("needs Wood 3 more", "only at a workbench, none in view").
    private static List<ObjectID> WantedItems(World server, Entity player, out List<AICandidate> craft)
    {
        craft = new List<AICandidate>();
        var wanted = new List<ObjectID>();
        var notes = new List<string>();
        AIBase.ResetMissing();   // missing materials noted below; AIBase offers taking them out of a known chest
        if (Goal.kind == GoalKind.Gather) wanted.Add(Goal.item);
        var inv = AIBody.InventoryCounts(server, player);
        var targets = new List<(ObjectID id, int n, string why, bool forGoal)>();
        if (Goal.kind == GoalKind.Craft)
        {
            int have = AICraft.MainCount(server, player, Goal.item);
            targets.Add((Goal.item, math.max(1, Goal.count - (have - Goal.startCount)), "목표 품목", true));
        }
        // store goal with no base chest that may have room and none in the bag: make one (AIBase)
        ObjectID chestT = AIBase.ChestTarget(server, player);
        if (chestT != ObjectID.None) targets.Add((chestT, 1, "기지에 넣을 자리가 있는 상자가 없음", true));
        // farm goal without a hoe / water can: make it (AIFarm, 먹거리 자급 2)
        try { foreach (var ft in AIFarm.ToolTargets(server, player)) targets.Add((ft.id, 1, ft.why, true)); }
        catch (Exception e) { AIPlayerSpikeMod.LogError("brain: farm tool targets failed", e); }
        // cook goal with a better pair but no own pot: make one (AICook, 먹거리 자급 3)
        try { foreach (var ct in AICook.ToolTargets(server, player)) targets.Add((ct.id, 1, ct.why, true)); }
        catch (Exception e) { AIPlayerSpikeMod.LogError("brain: cook pot target failed", e); }
        bool hasPick = AIBody.HasWorkingPick(server, player, out bool brokenOnly);
        ObjectID better = ObjectID.None;
        if (Goal.kind == GoalKind.Gather && ToolShortRule(_blockNote, S._blockToolShort, "prep"))
        {
            // the lowest pick tier above the best working pick whose remembered damage beats the blocking wall
            // (game tiers: wood, copper, tin, iron, ...; a tier never held counts as "may be enough")
            better = NextPickTier(server, player, _toolShortRed, out string bestHave, out string tierNote);
            if (better != ObjectID.None) targets.Add((better, 1, $"도구 부족 벽(감소 {_toolShortRed})이 목표를 막음 — 지금 쓸 수 있는 곡괭이 {bestHave}; {tierNote}", true));
        }
        // no working pick: a wood pick, unless a better pick is already the step (it replaces it)
        if (Goal.kind == GoalKind.Gather && !hasPick && (better == ObjectID.None || better == ObjectID.WoodMiningPick) && !targets.Exists(t => t.id == ObjectID.WoodMiningPick))
            targets.Add((ObjectID.WoodMiningPick, 1, brokenOnly ? "가진 곡괭이가 부서짐(내구도 0)" : "곡괭이가 없음", true));
        if (AIBody.FindWeapon(server, player) == ObjectID.None) targets.Add((ObjectID.WoodSword, 1, "무기가 없음", false));
        if (targets.Count == 0) { _prepNote = ""; return wanted; }

        var stations = AICraft.StationsInView(server, player);
        var hand = AICraft.HandRecipes(server, player);
        for (int ti = 0; ti < targets.Count; ti++)
        {
            var tg = targets[ti];
            Entity at = Entity.Null; string where = null;
            if (hand.Contains(tg.id)) where = "손에서(제작대 없이)";
            else
            {
                var st = stations.Find(s => s.recipes.Contains(tg.id));
                if (st != null) { at = st.entity; where = $"제작대 {st.id} ({st.pos.x:F0},{st.pos.z:F0}) {st.dist:F1}칸"; }
            }
            // Runtime recipe output and costs decide executable requests before legacy preparation estimates.
            int candidatesBefore = craft.Count;
            bool currentItem = Goal.kind == GoalKind.Craft && Goal.item == tg.id;
            var selection = currentItem ? AILife.ForGoal(Goal) : new LifeSelection { Branch = "craft", Item = tg.id.ToString(), RequestedCount = 1, GoalRevision = AIGoals.GoalRevision };
            if (currentItem && !string.IsNullOrEmpty(Goal.where))
            {
                var exact = AICraft.ExactCandidate(server, player, null, "", selection);
                if (exact != null) craft.Add(exact);
            }
            else
            {
                var observation = AIObservation.Take(server, player, false);
                foreach (var entry in AICraft.LifeEntries(server, player, observation, "", selection))
                {
                    if (entry.Op != "where") continue;
                    var request = selection.Copy(); request.Where = entry.Id;
                    var exact = AICraft.ExactCandidate(server, player, observation, "", request);
                    if (exact != null && !craft.Exists(c => c.key == exact.key)) craft.Add(exact);
                }
            }
            if (craft.Count > candidatesBefore) continue;
            var m = AICraft.Materials(server, player, tg.id, tg.n);
            bool actualCosts = false;
            if (where != null)
            {
                Entity costStation = at;
                bool costStationKnown = true;
                if (currentItem && !string.IsNullOrEmpty(Goal.where))
                {
                    if (Goal.where == "hand") costStation = Entity.Null;
                    else
                    {
                        var selectedStation = stations.Find(st => AIPlaces.ObjectId("station", st.id.ToString(), AIBody.TileOf(st.pos).x, AIBody.TileOf(st.pos).y) == Goal.where);
                        costStationKnown = selectedStation != null;
                        if (costStationKnown) costStation = selectedStation.entity;
                    }
                }
                AICraftSelection recipeInfo = null;
                if (costStationKnown) AICraft.ReadRecipe(server, player, costStation, tg.id, selection.RequestedCount > 0 ? selection.RequestedCount : 1, out recipeInfo, out _);
                if (recipeInfo != null)
                {
                    m = recipeInfo.Costs.ConvertAll(cost => (cost.id, cost.need, AICraft.MaterialCount(server, player, costStation, cost.id)));   // bag + the chests the server crafts from
                    actualCosts = true;
                }
            }
            if (where == null)
            {
                // a station the AI saw or placed before (now out of view) makes it: walk back to it instead of
                // building another one (it is only "known at X, last seen T"; its state now is not known)
                var known = AICraft.RememberedStationFor(server, player, tg.id);
                if (known.HasValue)
                {
                    var ks = known.Value;
                    float kd = math.distance(AIBody.Pos(server, player).xz, (float2)ks.tile);
                    string kdesc = $"{ks.id} ({ks.tile.x},{ks.tile.y}) {kd:F0}칸, {AIMemory.Ago(ks.seenUnix)}에 봄{(ks.placed ? ", " + AIAgent.Cur.Name + "가 놓은 것" : "")}(지금은 화면 밖이라 지금 상태는 모름)";
                    var stands = AICraft.StandTilesFor(server, player, ks);
                    var kplan = AIApproach.FirstCached(server, player, stands, out int2 kstand, out string kwhy);
                    bool enough = AICraft.HasMaterials(m);
                    foreach (var x in m)
                    {
                        if (x.have >= x.need) continue;
                        AIBase.NoteMissing(x.id, x.need - x.have);
                        if (Gatherable(x.id, server, player)) { if (!wanted.Contains(x.id)) wanted.Add(x.id); }
                        else if (!targets.Exists(t => t.id == x.id) && (hand.Contains(x.id) || AICraft.StationFor(server, player, x.id) != ObjectID.None || AICraft.RememberedStationFor(server, player, x.id).HasValue))
                            targets.Add((x.id, x.need - x.have, $"{tg.id}의 재료", tg.forGoal));
                    }
                    if (kplan != null)
                    {
                        if (enough)
                            craft.Add(AIApproach.WalkCandidate(kstand, $"기억한 제작대 {ks.id}", tg.forGoal,
                                $"기억한 제작대로 가기 — {kdesc}. 거기서 {tg.id}({tg.why})를 만들 수 있음(재료 {AICraft.MaterialsText(m)}, 충분). 아는 길 {kplan.path.Count}칸{(kplan.walls.Count > 0 ? $", 부술 벽 {kplan.walls.Count}개({kplan.WallSummary()})" : ", 부술 벽 0개")}, 예상 약 {kplan.EstimatedSeconds:F0}초. 옆 칸 ({kstand.x},{kstand.y})에 서면 끝(만들기는 따로)."));
                        notes.Add($"{tg.id}({tg.why}): {ks.id}에서 만듦 — 지금 안 보이지만 기억한 {kdesc}, 아는 길 {kplan.path.Count}칸{(enough ? "" : $"; 재료 먼저({AICraft.MaterialsText(m)})")}");
                    }
                    else notes.Add($"{tg.id}({tg.why}): {ks.id}에서 만듦 — 기억한 {kdesc}, 그러나 갈 길 없음({kwhy}); 재료 {AICraft.MaterialsText(m)}");
                    inv.TryGetValue(ks.id, out int hasKs);
                    if (hasKs > 0)
                    {
                        AIPlaceSpot.AddUnique(craft, AIPlaceSpot.Candidates(server, player, ks.id, "놓기",
                            $"까닭: {tg.id}({tg.why})는 {ks.id}에서 만듦. 기억한 {ks.id}가 있으나 화면 밖({kd:F0}칸). {tg.id} 재료 {AICraft.MaterialsText(m)}.", tg.forGoal, out string noKs, false));
                        if (noKs != null) notes.Add($"{tg.id}({tg.why}): {noKs}");   // review F3: why the bag's station cannot go down now
                    }
                    if (kplan != null || hasKs > 0) continue;
                }

                // no station in view makes it: a station the AI can build by hand (one step), placed from the bag
                ObjectID sid = AICraft.StationFor(server, player, tg.id);
                foreach (var x in m)
                {
                    if (x.have >= x.need) continue;
                    AIBase.NoteMissing(x.id, x.need - x.have);
                    if (Gatherable(x.id, server, player)) { if (!wanted.Contains(x.id)) wanted.Add(x.id); }
                    else if (!targets.Exists(t => t.id == x.id) && (hand.Contains(x.id) || AICraft.StationFor(server, player, x.id) != ObjectID.None))
                        targets.Add((x.id, x.need - x.have, $"{tg.id}의 재료", tg.forGoal));
                }
                if (sid == ObjectID.None) { notes.Add($"{tg.id}: 손 제작 목록에 없고 이것을 만드는 제작대가 보이지 않음(재료 {AICraft.MaterialsText(m)})"); continue; }
                inv.TryGetValue(sid, out int hasSt);
                if (hasSt > 0)
                {
                    AIPlaceSpot.AddUnique(craft, AIPlaceSpot.Candidates(server, player, sid, "놓기",
                        $"까닭: {tg.id}({tg.why})는 {sid}에서 만드는데 보이는 {sid}가 없음. {tg.id} 재료 {AICraft.MaterialsText(m)}. 결과는 놓인 제작대가 보이는지로 확인.", tg.forGoal, out string noSt, false));
                    // review F3: "놓으면 됨" only when a place option exists; otherwise why it cannot go down now
                    notes.Add(noSt == null ? $"{tg.id}({tg.why}): {sid}에서 만듦 — 가방에 {sid} 있음, 놓으면 됨(재료 {AICraft.MaterialsText(m)})"
                        : $"{tg.id}({tg.why}): {sid}에서 만듦 — {noSt}(재료 {AICraft.MaterialsText(m)})");
                }
                else if (!targets.Exists(t => t.id == sid))
                {
                    targets.Add((sid, 1, $"{tg.id}을(를) 만들 제작대", tg.forGoal));
                    notes.Add($"{tg.id}({tg.why}): {sid}에서 만듦 — 보이는 {sid} 없음, 먼저 {sid}를 만들어 놓아야 함(재료 {AICraft.MaterialsText(m)})");
                }
                continue;
            }
            var miss = new List<string>();
            foreach (var x in m)
            {
                if (x.have >= x.need) continue;
                AIBase.NoteMissing(x.id, x.need - x.have);
                miss.Add($"{x.id} {x.need - x.have}개 더");
                if (Gatherable(x.id, server, player)) { if (!wanted.Contains(x.id)) wanted.Add(x.id); }
                else if (!targets.Exists(t => t.id == x.id) && (hand.Contains(x.id) || stations.Exists(s => s.recipes.Contains(x.id)) || AICraft.StationFor(server, player, x.id) != ObjectID.None))
                    targets.Add((x.id, x.need - x.have, $"{tg.id}의 재료", tg.forGoal)); // a made material (e.g. CopperBar): its own step
            }
            notes.Add(miss.Count > 0 ? $"{tg.id}({tg.why}): {where} {(actualCosts ? "실제 레시피 재료" : "준비 추정 재료")} {string.Join(", ", miss)} 필요{(actualCosts ? "" : "(실제 레시피 비용은 실행 후보에서 다시 확인)")}" : $"{tg.id}({tg.why}): {where}의 실제 레시피/비용/출력 공간 조건 미확인 또는 부족; 실행 가능하다고 추측하지 않음");
        }
        _prepNote = string.Join(" / ", notes);
        if (notes.Count > 0) AIPlayerSpikeMod.Log($"BRAIN: preparation: {_prepNote}");
        return wanted;
    }

    private static readonly ObjectID[] PickTiers = { ObjectID.WoodMiningPick, ObjectID.CopperMiningPick, ObjectID.TinMiningPick, ObjectID.IronMiningPick, ObjectID.ScarletMiningPick, ObjectID.OctarineMiningPick, ObjectID.GalaxiteMiningPick, ObjectID.SolariteMiningPick };

    private static ObjectID NextPickTier(World server, Entity player, int needRed, out string bestHave, out string note)
    {
        bestHave = "없음";
        note = "";
        int best = -1;
        var em = server.EntityManager;
        if (em.HasBuffer<ContainedObjectsBuffer>(player))
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
            for (int i = 0; i < buf.Length; i++)
            {
                int ti = Array.IndexOf(PickTiers, buf[i].objectID);
                if (ti > best && !PlayerController.ItemIsBroken(buf[i].objectData)) { best = ti; bestHave = buf[i].objectID.ToString(); }
            }
        }
        // the block note is from the previous build: if the pick the AI now has already beats the wall (e.g. it was
        // just made), there is no next step (a TinMiningPick note appeared right after the copper pick, ai2)
        if (best >= 0 && AITunnel.ToolBonus.TryGetValue(PickTiers[best], out int have) && 20 + have > needRed) { note = $"{bestHave} 추정 피해 {20 + have} > 감소 {needRed}"; return ObjectID.None; }
        for (int ti = best + 1; ti < PickTiers.Length; ti++)
        {
            if (!AITunnel.ToolBonus.TryGetValue(PickTiers[ti], out int bonus)) { note = $"{PickTiers[ti]}는 들어 본 적이 없어 피해 모름"; return PickTiers[ti]; }
            if (20 + bonus > needRed) { note = $"{PickTiers[ti]} 기억한 추정 피해 {20 + bonus} > 감소 {needRed}"; return PickTiers[ti]; }
            note += $"{PickTiers[ti]}(추정 {20 + bonus})는 모자람; ";
        }
        return ObjectID.None;
    }
}
