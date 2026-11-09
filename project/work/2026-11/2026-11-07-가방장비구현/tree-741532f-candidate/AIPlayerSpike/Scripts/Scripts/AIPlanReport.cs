// AIPlanReport — builds the report JSON the upper LLM gets (refactor plan 2-계획.md S4). Moved from AIPlanner with the
// code text kept: the report state (report id, last goal, events, rejected answer, gather food, health timer) stays in
// AIPlanner's per-character state and is read through same-named members below. The report bytes and the
// "PLANNER: report #n sent" line (read by replay.mjs) do not change.
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class AIPlanReport
{
    private static AIPlanner.State S => AIPlanner.Cur;
    private static int _reportId { get => S._reportId; set => S._reportId = value; }
    private static string _lastGoalText => S._lastGoalText;
    private static string _lastWhy => S._lastWhy;
    private static string _lastSource => S._lastSource;
    private static string _rejected => S._rejected;
    private static List<string> _events => S._events;

    internal static string ReportJson(World server, Entity player, string trigger, out int reportId)
    {
        reportId = ++_reportId;
        var em = server.EntityManager;
        var o = AIObservation.Take(server, player, false);
        var inv = AIBody.InventoryCounts(server, player);
        var bag = new List<string>();
        foreach (var kv in inv) if (bag.Count < 10) bag.Add($"\"{kv.Key}\":{kv.Value}");
        // gather items: known aliases + ores remembered or seen, and food that can be had now (먹거리 자급: what visible
        // ripe plants / ground food give, what broke into food before, food lying in view)
        var gather = new List<string> { "WallDirtBlock", "CopperOre", "TinOre", "Wood" };
        S._gatherFood.Clear();
        try { S._gatherFood.AddRange(AIFoodSource.GatherableFood(server, player, o)); } catch (Exception e) { AIPlayerSpikeMod.LogError("planner: gatherable food failed", e); }
        foreach (var f in S._gatherFood) gather.Add(f.ToString());
        float3 host = AIBody.HostPosition(server, player);
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"trigger\":\"{trigger}\",");
        sb.Append($"\"goal\":{{\"source\":\"{Esc(_lastSource)}\",\"text\":\"{Esc(_lastGoalText)}\"}},");
        // 자연어 명령 (2026-11-05 합의 2·3): the goal running now, the host's latest instruction and its state, the world's
        // recent talk (host mentions and every member's say), and this member's last applied decision
        float nowT = Time.realtimeSinceStartup;
        var cg = AIBrain.Goal;
        string gsrc = !cg.IsReal ? (cg.kind == GoalKind.Free ? "free" : "none") : GoalSource.IsHost(cg.source) ? "host" : GoalSource.IsLlm(cg.source) ? "me" : cg.source;
        sb.Append(cg.IsReal ? $"\"goal_now\":{{\"text\":\"{Esc(cg.Text)}\",\"by\":\"{gsrc}\",\"for_s\":{(int)(nowT - cg.startTime)}{(string.IsNullOrEmpty(cg.note) ? "" : $",\"note\":\"{Esc(cg.note)}\"")}}}," : $"\"goal_now\":{{\"text\":\"없음\",\"by\":\"{gsrc}\"}},");
        sb.Append($"\"goal_observation\":\"{Esc(AIGoals.Observation)}\",");
        sb.Append($"\"execution_observation\":\"{Esc(AIBrain.ExecutionObservation + (AIBody.Current == null ? "" : " / " + string.Join(" / ", AIBody.Current.ExecutionObservations)))}\",");
        sb.Append($"\"plan\":{S.Plan.ReportJson(Esc)},");   // 합의 3e: the running plan / the one that stopped last
        sb.Append($"\"plan_max\":{math.max(1, AIBrain.Settings().upperPlanMaxSteps)},");   // steps one plan may have (재검수 작은 지적 7)
        sb.Append(S.Orders.ReportFields(nowT, Esc)).Append(',');   // host_order (the latest) and host_orders_open (재검수 R3)
        sb.Append($"\"chat\":{AIPlanner.Chat.ReportJson(nowT, AIAgent.Cur.Id, S._chatSeen, Esc, id => AIAgent.ById(id)?.Name ?? id)},");
        sb.Append($"\"recent_fights\":{AIExperience.RecentFightsJson(Esc)},");
        sb.Append(S._lastDecision == null ? "\"last_decision\":null," : $"\"last_decision\":{{\"ago_s\":{(int)(nowT - S._lastDecisionAt)},\"what\":\"{Esc(S._lastDecision)}\"}},");
        sb.Append($"\"why_ended\":\"{Esc(_lastWhy)}\",");
        sb.Append($"\"events\":[{string.Join(",", _events.ConvertAll(e => $"\"{Esc(e)}\""))}],");
        int coreDist = (int)math.round(math.distance(o.pos.xz, float2.zero));
        int hostDist = (int)math.round(math.distance(o.pos.xz, host.xz));
        sb.Append($"\"me\":{{\"hp\":\"{o.health}/{o.maxHealth}\",\"hunger\":{o.hunger},\"hand\":\"{o.equipped}\",\"core_dist\":{coreDist},\"tile\":\"{AIBody.TileOf(o.pos).x},{AIBody.TileOf(o.pos).y}\",\"bag\":{{{string.Join(",", bag)}}},\"enemies_in_view\":{o.enemies.Count}}},");
        sb.Append($"\"hp_note\":\"{Esc(HpNote(server, player, o))}\",");
        sb.Append($"\"recent_downs\":[{string.Join(",", AIVitals.RecentDowns(3).ConvertAll(e => $"\"{Esc(e)}\""))}],");
        sb.Append($"\"memory\":\"{Esc(AIMemory.Summary())}, 기억한 구리 {AIMemory.CountOres(ObjectID.CopperOre)}곳, 주석 {AIMemory.CountOres(ObjectID.TinOre)}곳\",");
        // craftable things with what the bag has against what one needs (hand recipes and stations in view), grouped by
        // where they are made (제작대-놓기 §2.8: the cut at 16 left out what the stations in view make, 실게임4 CopperAnvil)
        var rows = new List<(string where, string item, int have)>();
        var texts = new Dictionary<string, string>();
        void Row(string where, ObjectID cid)
        {
            var m = AICraft.Materials(server, player, cid, 1);
            int have = AICraft.HasMaterials(m) ? 2 : m.Exists(x => x.have > 0) ? 1 : 0;
            rows.Add((where, cid.ToString(), have));
            if (!texts.ContainsKey(cid.ToString())) texts[cid.ToString()] = $"{cid}: {AICraft.MaterialsText(m)}";
        }
        foreach (var cid in AICraft.HandRecipes(server, player)) Row("손", cid);
        var near = AICraft.StationsInView(server, player);
        // a cooking pot's recipe entry (CookedSoup) is not crafted: cooking is the cook goal (AICook)
        foreach (var st in near) { if (AICook.IsPotItem(server, st.id)) continue; foreach (var r in st.recipes) Row(st.id.ToString(), r); }
        var groups = AIPlaces.CraftGroups(rows, CraftMax, out int craftMore);
        groups = AIPlaces.CapGroups(groups, x => texts[x], CraftChars, ref craftMore);   // 검수 보고 길이: a char cap too
        var craftIds = new List<ObjectID>();
        var craftJ = new List<string>();
        foreach (var gr in groups)
        {
            craftJ.Add($"\"{Esc(gr.where)}\":[{string.Join(",", gr.items.ConvertAll(x => $"\"{Esc(texts[x])}\""))}]");
            foreach (var x in gr.items) if (Enum.TryParse(x, out ObjectID cid)) craftIds.Add(cid);
        }
        var placeable = AIPlaces.CapChars(AIPlaceGoal.BagPlaceable(server, player), PlaceChars, out int placeMore);
        sb.Append($"\"can_do\":{{\"{AIGoalKinds.Of(GoalKind.Gather).WireName}\":[{string.Join(",", gather.ConvertAll(x => $"\"{x}\""))}],\"{AIGoalKinds.Of(GoalKind.Explore).WireName}\":true,\"{AIGoalKinds.Of(GoalKind.Follow).WireName}\":true,\"{AIGoalKinds.Of(GoalKind.Goto).WireName}\":true,\"{AIGoalKinds.Of(GoalKind.Craft).WireName}\":{{{string.Join(",", craftJ)}}},\"craft_more\":{craftMore},\"{AIGoalKinds.Of(GoalKind.Place).WireName}\":[{string.Join(",", placeable.ConvertAll(x => $"\"{Esc(x)}\""))}],\"place_more\":{placeMore},\"stations_in_view\":[{string.Join(",", near.ConvertAll(s => $"\"{s.id}\""))}],\"{AIGoalKinds.Of(GoalKind.Store).WireName}\":{(AIBase.CanStore(server, player) ? "true" : "false")},\"{AIGoalKinds.Of(GoalKind.Home).WireName}\":{(AIBase.BaseDist(o.pos) > AIBase.HomeArrive ? "true" : "false")},\"{AIGoalKinds.Of(GoalKind.Farm).WireName}\":{(AIFarm.CanFarm(server, player) == null ? "true" : "false")},\"{AIGoalKinds.Of(GoalKind.Cook).WireName}\":{(AICook.CanCook(server, player) == null ? "true" : "false")}}},");
        // base and chests (생활-기지): where base is, chests known (contents as of when last opened), what would be stored
        sb.Append(AIBase.ReportFields(server, player, o.pos, Esc)).Append(',');
        sb.Append("\"inventory_actions\":").Append(AIInventory.ReportJson(server, player)).Append(',');
        // hunger meaning and the hunger food in the bag (AIFood, 생활-기지 piece 2 A)
        sb.Append(AIFood.ReportFields(server, player, o, Esc)).Append(',');
        // own field (AIFarm, 먹거리 자급 2): plots, crops, seeds, tools, water
        sb.Append(AIFarm.ReportFields(server, player, o, Esc)).Append(',');
        // own cooking pot (AICook, 먹거리 자급 3): pots, ingredients in the bag, the best pair against eating raw
        sb.Append(AICook.ReportFields(server, player, o, Esc)).Append(',');
        // places the AI knows (제작대-놓기 §2.6, the where of goto / place; known_stations is merged in: stations seen or
        // placed before, with their identifiers)
        sb.Append(AIPlaceGoal.ReportFields(server, player, o.pos, Esc)).Append(',');
        // experience memory (AIExperience / AIExpCore): recent goal runs and the goals that can be chosen now, with the
        // conditions they ran under
        var expKeys = new List<string>();
        foreach (var x in gather) expKeys.Add(AIGoalKinds.Of(GoalKind.Gather).WireName + ":" + x);
        foreach (var cid in craftIds) expKeys.Add(AIGoalKinds.Of(GoalKind.Craft).WireName + ":" + cid);
        expKeys.Add(AIGoalKinds.Of(GoalKind.Explore).WireName); expKeys.Add(AIGoalKinds.Of(GoalKind.Follow).WireName); expKeys.Add(AIGoalKinds.Of(GoalKind.Farm).WireName); expKeys.Add(AIGoalKinds.Of(GoalKind.Cook).WireName); expKeys.Add(AIGoalKinds.Of(GoalKind.Goto).WireName);
        foreach (var x in placeable) expKeys.Add(AIGoalKinds.Of(GoalKind.Place).WireName + ":" + x.Split(':')[0]);
        sb.Append($"\"experience\":{AIExperience.ReportPart(server, player, expKeys)},");
        sb.Append($"\"craft_needed\":\"{Esc(AIBrain.PrepNote)}\",");
        sb.Append("\"cannot_yet\":[\"건축\",\"전투 자체를 목표로\"],");
        sb.Append($"\"host\":{{\"in_world\":true,\"distance\":{hostDist}}},");
        // other players on this character's own screen (host and other AI characters), as a human would see them
        var mates = AITeammates.Visible(server, player, o.enemies);
        sb.Append($"\"visible_players\":[{string.Join(",", mates.ConvertAll(p => $"{{\"name\":\"{Esc(p.name)}\",\"role\":\"{(p.isAi ? "ai_teammate" : "host")}\",\"distance\":{(int)math.round(p.dist)},\"direction\":\"{p.dir}\",\"hp\":\"{p.health}/{p.maxHealth}\",\"doing\":\"{Esc(p.doing)}\"}}"))}],");
        sb.Append($"\"last_answer_rejected\":{(_rejected == null ? "null" : $"\"{Esc(_rejected)}\"")}");
        sb.Append('}');
        return sb.ToString();
    }

    public const int CraftMax = 40;   // 제작대-놓기 §2.8: can_do.craft items in all (the rest counted in craft_more)
    public const int CraftChars = 1800, PlaceChars = 400;   // 검수 보고 길이: can_do.craft / can_do.place characters (the rest counted)

    // B1 (refactor final review): since S1 the plan reader decodes JSON escapes, so an LLM answer's \t, \r or \u0001 can
    // reach a report string (reject sentence, goal text). A raw U+0000-U+001F makes the report invalid JSON (the
    // bridge's JSON.parse answers 500), so every control character becomes a space, as \n always did. Strings without
    // control characters come out as before. Copied and checked in Tools/core-tests/ReportEscTests.cs.
    private static string Esc(string s)
    {
        var t = (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
        for (int i = 0; i < t.Length; i++)
            if (t[i] < ' ')
            {
                var c = t.ToCharArray();
                for (int j = i; j < c.Length; j++) if (c[j] < ' ') c[j] = ' ';
                return new string(c);
            }
        return t;
    }

    // What health does over time and what fills it (2026-11-04 넷이 함께 플레이). Gemini at hp 17/100 answered "hold"
    // nine times in a row over 17 minutes ("쉬며 회복할게요") and the hp stayed 17: the report gave the number only, so
    // the model assumed resting heals. The game regenerates health only for entities with HealthRegenerationCD (read
    // here from the AI's own player entity) and otherwise through food/potion effects (HealthAddition, HealOverTime).
    private static string HpNote(World server, Entity player, AIObservation o)
    {
        float now = Time.realtimeSinceStartup;
        if (o.health != S._hpSeen) { S._hpSeen = o.health; S._hpSince = now; }
        var em = server.EntityManager;
        string regen;
        if (em.HasComponent<HealthRegenerationCD>(player))
        {
            var r = em.GetComponentData<HealthRegenerationCD>(player);
            regen = r.NormalizedHealthIncreasePerFiveSeconds > 0f
                ? $"싸움에서 벗어나 {r.HealDelayAfterLeavingCombat:F0}초 뒤부터 5초마다 최대 체력의 {r.NormalizedHealthIncreasePerFiveSeconds * 100f:F0}%씩 저절로 참"
                : "가만히 있거나 기지에 있어도 저절로 차지 않음(자연 회복 0)";
        }
        else regen = "가만히 있거나 기지에 있어도 저절로 차지 않음(이 캐릭터에는 자연 회복이 없음)";
        var heals = AIItems.HealingInInventory(server, player);
        string bagHeal = heals.Count == 0 ? "없음" : string.Join(", ", heals.ConvertAll(h => $"{h.id} {h.count}({h.info})"));
        string still = o.health < o.maxHealth && now - S._hpSince >= 60f ? $" 지금 체력은 {(now - S._hpSince) / 60f:F0}분째 {o.health}에서 그대로." : "";
        return $"지금 {o.health}/{o.maxHealth}. 체력은 {regen}. 차는 길: 회복 효과가 있는 음식·물약을 먹기(가방에 있으면 목표를 하는 중이나 싸우는 중에 체력이 가득이 아니면 행동 판단기가 먹기를 고를 수 있음. hold로 기다리는 동안은 먹지 않음). 가방의 회복 음식: {bagHeal}.{still}";
    }
}
