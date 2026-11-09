// AICook — cooking with the AI's own cooking pot at base (먹거리 자급 조각 3; report
// work/2026-11/2026-11-04-먹거리-자급/3-요리.md, session 코어키퍼-AI-캐릭터).
//
// Game rules (decompile C:\Dev\_ref\corekeeper-decomp, plus Pug.Automation.dll decompiled for this piece):
// - A cooking pot is a crafting building with CraftingCD.craftingType == Cooking (CookingPot : CraftingBuilding). It is a
//   "process auto crafter" (CraftingCD.IsProcessAutoCrafter): nobody presses a craft button. PugAutomationStartCraftSystem
//   (UpdateCraftingTimerJob) checks InventoryUtility.CanCraft on the pot's own inventory every tick and starts the
//   crafting timer (ObjectInfo.craftingTime of the recipe entry, CookedSoup 10 s) while the pot's automation is active
//   (PugAutomationCD.isActive = no ElectricityCD or enough electricity: PugAutomationInstantiateSystem :1125); the
//   crafting system then calls InventoryUtility.Craft with the pot as the main inventory.
// - CanCraft / Craft, Cooking branch (InventoryUtility.cs:1613-1745): slots 0 and 1 of the pot each hold an ingredient
//   (amount >= 1); the primary one (CookedFoodCD.GetPrimaryIngredient) has CookingIngredientCD; the result is its
//   turnsIntoFood with variation CookedFoodCD.GetFoodVariation(a, b) (the two ingredient ids), 1 per craft, one of each
//   ingredient used; the output slot (CraftingCD.outputSlotIndex) must be empty or hold the same food and variation.
//   So "cooking" = putting two ingredients into slots 0 and 1 (a client moving items into the open pot) and taking the
//   result out of the output slot later. The same client inventory moves as the chests (AIBase, 실기 통과 2026-11-04).
// - Eating cooked food (EatableSlotConsumeResultEvaluationSystem :245-274, ConditionUIExtensions.GetConditionsOnConsume):
//   the conditions are taken from [the cooked item, primary, secondary] using conditionDataWhenCooked, the largest value
//   per condition (permanent max health summed), times 1.25 / 1.5 for a rare / epic cooked item (x1.15 when both are
//   golden). The cooked prefab's own conditionData (Soup: hunger 20) is not what eating gives.
// Own pots only (as own chests and plots): a character remembers and uses only the pots it placed itself (PlaceSkill).
using System;
using System.Collections.Generic;
using Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class AICook
{
    public const int MaxPairs = 5;   // dishes per cooking round
    public const string Rule = "요리 솥 입력 칸 둘에 요리 재료를 하나씩 넣으면 솥이 저절로 요리함(한 접시 약 10초, 접시마다 두 재료를 하나씩 씀). 결과는 솥의 출력 칸에 쌓이고, 꺼내서 먹거나 가방에 둠. 요리한 음식의 효과는 두 재료의 '요리했을 때' 값 중 큰 것";

    // ---- per-character state (multi-AI): AIAgent.Cur's Slots[14]
    public sealed class PotMem { public ObjectID id; public int2 tile; public long seenUnix; public int goneSeen; public string last = ""; public int outN; public bool busy; }
    private sealed class State
    {
        public readonly Dictionary<int2, PotMem> pots = new Dictionary<int2, PotMem>();
        public readonly Dictionary<int2, (int outN, float t)> watch = new Dictionary<int2, (int, float)>();   // test log: output growth
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Cook);
    private static State S => StateOf(AIAgent.Cur);

    // ------------------------------------------------------------------ memory (memory.json via AIMemory)

    public static void ClearMemory() { S.pots.Clear(); S.watch.Clear(); }

    public static void Import(AIMemory.Data d)
    {
        for (int i = 0; d.potId != null && i < d.potId.Length && d.potXY != null && 2 * i + 1 < d.potXY.Length; i++)
        {
            var t = new int2(d.potXY[2 * i], d.potXY[2 * i + 1]);
            S.pots[t] = new PotMem { id = (ObjectID)d.potId[i], tile = t, seenUnix = d.potSeen != null && i < d.potSeen.Length ? d.potSeen[i] : 0 };
        }
    }

    public static void Export(AIMemory.Data d)
    {
        var id = new List<int>(); var xy = new List<int>(); var sn = new List<long>();
        foreach (var p in S.pots.Values) { id.Add((int)p.id); xy.Add(p.tile.x); xy.Add(p.tile.y); sn.Add(p.seenUnix); }
        d.potId = id.ToArray(); d.potXY = xy.ToArray(); d.potSeen = sn.ToArray();
    }

    // ------------------------------------------------------------------ game data

    private static bool Bank(World server, out BlobAssetReference<PugDatabase.PugDatabaseBank> bank)
    {
        bank = default;
        var em = server.EntityManager;
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>()))
        {
            if (q.CalculateEntityCount() != 1) return false;
            bank = q.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
            return true;
        }
    }

    private static Entity Prefab(World server, ObjectID id, int variation = 0)
    {
        if (id == ObjectID.None || !Bank(server, out var bank)) return Entity.Null;
        return PugDatabase.GetPrimaryPrefabEntity(id, bank, variation);
    }

    private static readonly Dictionary<ObjectID, bool> _potItem = new Dictionary<ObjectID, bool>();
    public static bool IsPotItem(World server, ObjectID id)
    {
        if (id == ObjectID.None) return false;
        if (_potItem.TryGetValue(id, out bool r)) return r;
        r = false;
        try
        {
            var em = server.EntityManager;
            Entity p = Prefab(server, id);
            r = p != Entity.Null && em.HasComponent<CraftingCD>(p) && em.GetComponentData<CraftingCD>(p).craftingType == CraftingType.Cooking;
        }
        catch { }
        _potItem[id] = r;
        return r;
    }

    public static bool IsPot(World server, Entity e)
    {
        var em = server.EntityManager;
        return em.Exists(e) && em.HasComponent<CraftingCD>(e) && em.GetComponentData<CraftingCD>(e).craftingType == CraftingType.Cooking;
    }

    // An ingredient (CookingIngredientCD on its prefab) and the dish it turns into as the primary one.
    public static bool IsIngredient(World server, ObjectID id, out ObjectID turnsInto)
    {
        turnsInto = ObjectID.None;
        try
        {
            var em = server.EntityManager;
            Entity p = Prefab(server, id);
            if (p == Entity.Null || !em.HasComponent<CookingIngredientCD>(p)) return false;
            turnsInto = em.GetComponentData<CookingIngredientCD>(p).turnsIntoFood;
            return true;
        }
        catch { return false; }
    }
    public static bool IsIngredient(World server, ObjectID id) => IsIngredient(server, id, out _);

    public static bool IsCookedFood(World server, ObjectID id, int variation)
    {
        try { var em = server.EntityManager; Entity p = Prefab(server, id, variation); return p != Entity.Null && em.HasComponent<CookedFoodCD>(p); }
        catch { return false; }
    }

    // What eating gives (the game's GetConditionsOnConsume without the player's skill bonuses): raw = the item's own
    // conditionData; cooked = conditionDataWhenCooked of [dish, primary, secondary], largest per condition, x rarity.
    public static Dictionary<ConditionID, int> Effects(World server, ObjectID food, int variation, bool cooked)
    {
        var res = new Dictionary<ConditionID, int>();
        if (!Bank(server, out var bank)) return res;
        var em = server.EntityManager;
        var list = new List<(ObjectID id, int v)> { (food, variation) };
        float mult = 1f;
        if (cooked)
        {
            ObjectID a = CookedFoodCD.GetPrimaryIngredientFromVariation(variation), b = CookedFoodCD.GetSecondaryIngredientFromVariation(variation);
            list.Add((a, 0)); list.Add((b, 0));
            ObjectInfo info = PugDatabase.GetObjectInfo(food, variation) ?? PugDatabase.GetObjectInfo(food);
            if (info != null) mult = info.rarity == Rarity.Rare ? 1.25f : info.rarity == Rarity.Epic ? 1.5f : 1f;
            if (CookedFoodCD.IngredientShouldBePrimary(a) && CookedFoodCD.IngredientShouldBePrimary(b)) mult *= 1.15f;
        }
        foreach (var it in list)
        {
            Entity p = PugDatabase.GetPrimaryPrefabEntity(it.id, bank, it.v);
            if (p == Entity.Null || !em.HasBuffer<GivesConditionsWhenConsumedBuffer>(p)) continue;
            var buf = em.GetBuffer<GivesConditionsWhenConsumedBuffer>(p, true);
            for (int i = 0; i < buf.Length; i++)
            {
                var cd = cooked ? buf[i].conditionDataContainer.conditionDataWhenCooked : buf[i].conditionDataContainer.conditionData;
                if (cd.conditionID == ConditionID.None) continue;
                bool perm = cd.conditionID == ConditionID.IncreasedMaxHealthPermanent;
                int v = perm ? cd.value : (int)math.round(cd.value * mult);
                if (!res.TryGetValue(cd.conditionID, out int had)) res[cd.conditionID] = v;
                else if (perm) res[cd.conditionID] = had + v;
                else if (had < v) res[cd.conditionID] = v;
            }
        }
        return res;
    }

    // Hunger from eating a cooked dish (AIFood.HungerAdd uses it for cooked food).
    public static bool TryCookedHunger(World server, ObjectID id, int variation, out int hunger)
    {
        hunger = 0;
        if (!IsCookedFood(server, id, variation)) return false;
        Effects(server, id, variation, true).TryGetValue(ConditionID.HungerAddition, out hunger);
        return true;
    }

    private static string EffectsText(Dictionary<ConditionID, int> e, bool skipHunger)
    {
        var parts = new List<string>();
        foreach (var kv in e) if (!(skipHunger && kv.Key == ConditionID.HungerAddition)) parts.Add($"{kv.Key} {kv.Value}");
        return parts.Count == 0 ? "없음" : string.Join(", ", parts);
    }

    // A pair of ingredients from the bag and what the dish gives compared with eating the two raw.
    public sealed class Pair
    {
        public ObjectID a, b, dish; public int variation, n, cookedHunger, rawA, rawB; public string why = ""; public bool better;
        public Dictionary<ConditionID, int> cooked;
        public string Text => $"{a}+{b} → {dish} 한 접시 배고픔 +{cookedHunger}(날로 둘 먹으면 +{rawA + rawB}), 다른 효과 [{EffectsText(cooked, true)}], {n}접시까지";
    }

    public static Pair Predict(World server, ObjectID a, ObjectID b)
    {
        ObjectID prim = CookedFoodCD.GetPrimaryIngredient(a, b);
        if (!IsIngredient(server, prim, out ObjectID dish) || dish == ObjectID.None) return null;
        int variation = CookedFoodCD.GetFoodVariation(a, b);
        var cooked = Effects(server, dish, variation, true);
        cooked.TryGetValue(ConditionID.HungerAddition, out int ch);
        var p = new Pair { a = a, b = b, dish = dish, variation = variation, cooked = cooked, cookedHunger = ch, rawA = AIFood.HungerAdd(server, a, 0), rawB = AIFood.HungerAdd(server, b, 0) };
        // better: no hunger lost against eating both raw, or as much hunger as the better raw one plus a stronger effect
        var ra = Effects(server, a, 0, false); var rb = Effects(server, b, 0, false);
        bool stronger = false;
        foreach (var kv in cooked)
        {
            if (kv.Key == ConditionID.HungerAddition) continue;
            ra.TryGetValue(kv.Key, out int x); rb.TryGetValue(kv.Key, out int y);
            if (kv.Value > math.max(x, y)) { stronger = true; break; }
        }
        if (ch >= p.rawA + p.rawB) { p.better = true; p.why = "날로 둘 먹는 것보다 배고픔이 같거나 많음"; }
        else if (ch >= math.max(p.rawA, p.rawB) && stronger) { p.better = true; p.why = "배고픔은 날로 둘보다 적지만 효과가 더 셈"; }
        else p.why = "날로 먹는 편이 나음";
        return p;
    }

    // The best better pair in the bag (null when none): most hunger gained over eating raw, then most hunger.
    public static Pair BestPair(World server, Entity player)
    {
        var counts = AIBody.InventoryCounts(server, player);
        var ing = new List<(ObjectID id, int n)>();
        foreach (var kv in counts) if (kv.Value > 0 && IsIngredient(server, kv.Key)) ing.Add((kv.Key, kv.Value));
        Pair best = null;
        for (int i = 0; i < ing.Count; i++)
            for (int j = i; j < ing.Count; j++)
            {
                int n = i == j ? ing[i].n / 2 : math.min(ing[i].n, ing[j].n);
                if (n <= 0) continue;
                var p = Predict(server, ing[i].id, ing[j].id);
                if (p == null || !p.better) continue;
                p.n = math.min(n, MaxPairs);
                if (best == null || (p.cookedHunger - p.rawA - p.rawB) > (best.cookedHunger - best.rawA - best.rawB)
                    || ((p.cookedHunger - p.rawA - p.rawB) == (best.cookedHunger - best.rawA - best.rawB) && p.cookedHunger > best.cookedHunger)) best = p;
            }
        return best;
    }

    public static string IngredientsText(World server, Entity player)
    {
        var parts = new List<string>();
        foreach (var kv in AIBody.InventoryCounts(server, player)) if (kv.Value > 0 && IsIngredient(server, kv.Key)) parts.Add($"{kv.Key} {kv.Value}");
        return parts.Count == 0 ? "없음" : string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ pots in view / own pots

    public sealed class PotView
    {
        public Entity entity; public ObjectID id; public float3 pos; public int2 tile; public float dist;
        public ObjectID inA, inB, outId; public int nA, nB, inVarA, inVarB, outN, outVar, outIdx; public float timeLeft = -1f;
        public bool Cooking => nA > 0 && nB > 0;
        public bool Empty => nA == 0 && nB == 0;
        public string Text => $"{id} ({tile.x},{tile.y}) {dist:F1}칸: 입력 [{(nA > 0 ? $"{inA} {nA}" : "빈 칸")}, {(nB > 0 ? $"{inB} {nB}" : "빈 칸")}], 출력 {(outN > 0 ? $"{outId} {outN}" : "빈 칸")}{(Cooking ? $", 요리 중{(timeLeft >= 0f ? $"(이번 접시 {timeLeft:F0}초 남음)" : "")}" : "")}";
    }

    private static PotView Read(World server, Entity e, float3 me)
    {
        var em = server.EntityManager;
        if (!em.HasBuffer<ContainedObjectsBuffer>(e) || !em.HasComponent<LocalTransform>(e) || !em.HasComponent<ObjectDataCD>(e)) return null;
        var cc = em.GetComponentData<CraftingCD>(e);
        var buf = em.GetBuffer<ContainedObjectsBuffer>(e, true);
        float3 p = em.GetComponentData<LocalTransform>(e).Position;
        var v = new PotView { entity = e, id = em.GetComponentData<ObjectDataCD>(e).objectID, pos = p, tile = AIBody.TileOf(p), dist = math.distance(me.xz, p.xz), outIdx = cc.outputSlotIndex };
        if (buf.Length > 0) { v.inA = buf[0].objectID; v.nA = v.inA == ObjectID.None ? 0 : buf[0].amount; v.inVarA = buf[0].variation; }
        if (buf.Length > 1) { v.inB = buf[1].objectID; v.nB = v.inB == ObjectID.None ? 0 : buf[1].amount; v.inVarB = buf[1].variation; }
        if (cc.outputSlotIndex >= 0 && cc.outputSlotIndex < buf.Length) { v.outId = buf[cc.outputSlotIndex].objectID; v.outN = v.outId == ObjectID.None ? 0 : buf[cc.outputSlotIndex].amount; v.outVar = buf[cc.outputSlotIndex].variation; }
        if (em.HasBuffer<CraftingTimerSlotBuffer>(e))
        {
            var tb = em.GetBuffer<CraftingTimerSlotBuffer>(e, true);
            for (int i = 0; i < tb.Length; i++) if (tb[i].timeLeftToCraft > 0f) { v.timeLeft = tb[i].timeLeftToCraft; break; }
        }
        return v;
    }

    public static List<PotView> PotsInView(World server, Entity player)
    {
        var res = new List<PotView>();
        var em = server.EntityManager;
        float3 me = AIBody.Pos(server, player);
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CraftingCD>(), ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (em.HasComponent<PlayerGhost>(e)) continue;
                if (em.GetComponentData<CraftingCD>(e).craftingType != CraftingType.Cooking) continue;
                float3 p = em.GetComponentData<LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var v = Read(server, e, me);
                if (v != null) res.Add(v);
            }
        }
        res.Sort((a, b) => a.dist.CompareTo(b.dist));
        return res;
    }

    public static PotMem Own(PotView v) => v != null && S.pots.TryGetValue(v.tile, out var m) && m.id == v.id ? m : null;
    public static bool IsOwnPot(World server, Entity e)
    {
        var em = server.EntityManager;
        if (!IsPot(server, e) || !em.HasComponent<LocalTransform>(e)) return false;
        var t = AIBody.TileOf(em.GetComponentData<LocalTransform>(e).Position);
        return S.pots.TryGetValue(t, out var m) && m.id == em.GetComponentData<ObjectDataCD>(e).objectID;
    }
    public static int OwnCount => S.pots.Count;
    public static List<PotMem> OwnPots() => new List<PotMem>(S.pots.Values);   // 제작대-놓기: places (AIPlaceGoal)

    // PlaceSkill: a new pot (not in view before) right after placing one becomes this character's.
    public static void NotePlaced(World server, Entity e)
    {
        var em = server.EntityManager;
        float3 p = em.GetComponentData<LocalTransform>(e).Position;
        var t = AIBody.TileOf(p);
        var id = em.GetComponentData<ObjectDataCD>(e).objectID;
        S.pots[t] = new PotMem { id = id, tile = t, seenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        AIMemory.MarkDirty();
        AIPlayerSpikeMod.Log($"COOK: own pot {id} placed at ({t.x},{t.y}), {math.distance((float2)t, (float2)AIBase.BaseTile):F0} tiles from base; remembered as {AIAgent.Cur.Name}'s");
    }

    // Called from AIMemory.Observe (about once per second): own pots in view refresh what was seen; an own pot whose tile
    // is in view without it on two observations in a row is forgotten. Logs output growth (cook time, test plan).
    public static void Observe(World server, Entity player, float3 pos, long unix)
    {
        var s = S;
        if (s.pots.Count == 0) return;
        var inView = new Dictionary<int2, PotView>();
        foreach (var v in PotsInView(server, player)) inView[v.tile] = v;
        float now = Time.realtimeSinceStartup;
        foreach (var t in new List<int2>(s.pots.Keys))
        {
            var m = s.pots[t];
            if (inView.TryGetValue(t, out var v) && v.id == m.id)
            {
                if (unix - m.seenUnix >= 30) AIMemory.MarkDirty();
                m.seenUnix = unix; m.goneSeen = 0; m.last = v.Text; m.outN = v.outN; m.busy = !v.Empty;
                if (s.watch.TryGetValue(t, out var w))
                {
                    if (v.outN > w.outN) AIPlayerSpikeMod.Log($"COOK: pot ({t.x},{t.y}) output {v.outId} {w.outN}->{v.outN} ({now - w.t:F1}s since the last change); inputs [{v.inA} {v.nA}, {v.inB} {v.nB}]");
                    if (v.outN != w.outN) s.watch[t] = (v.outN, now);
                }
                else s.watch[t] = (v.outN, now);
                continue;
            }
            if (!AIVision.CanSee(pos, t)) continue;
            if (++m.goneSeen >= 2) Forget(t, "seen gone from its tile (broken or picked up)");
        }
    }

    public static void Forget(int2 t, string why)
    {
        if (!S.pots.TryGetValue(t, out var m)) return;
        S.pots.Remove(t); S.watch.Remove(t);
        AIMemory.MarkDirty();
        AIPlayerSpikeMod.Log($"COOK: own pot {m.id} at ({t.x},{t.y}) {why}; forgotten");
    }

    private static ObjectID PotItem(World server)
    {
        // the pot the AI can make: CookingPot (basic workbench, Wood 2 + CopperBar 4); checked against game data
        return IsPotItem(server, ObjectID.CookingPot) ? ObjectID.CookingPot : ObjectID.None;
    }

    // ------------------------------------------------------------------ goal

    // Preparation path (AIBrain.WantedItems): a pot to make when the cook goal has a better pair but no own pot and none
    // in the bag.
    public static List<(ObjectID id, string why)> ToolTargets(World server, Entity player)
    {
        var res = new List<(ObjectID, string)>();
        if (AIBrain.Goal.kind != GoalKind.Cook || S.pots.Count > 0) return res;
        ObjectID pi = PotItem(server);
        if (pi == ObjectID.None) return res;
        AIBody.InventoryCounts(server, player).TryGetValue(pi, out int have);
        if (have > 0 || BestPair(server, player) == null) return res;
        res.Add((pi, $"{AIAgent.Cur.Name}가 놓은 요리 솥이 없음(다른 플레이어의 솥은 쓰지 않음)"));
        return res;
    }

    public static bool GoalDone(World server, Entity player, out string text)
    {
        text = "";
        if (AIBody.Current is CookSkill && AIBody.Current.Status == SkillStatus.Running) return false;
        // This is a maintenance observation, not completion of an explicit quantity or the host's order.
        if (CanCook(server, player) == null) return false;
        text = $"?? ??? ??? ?? ?? ???? ??/?? ??(?? {IngredientsText(server, player)}); ?? ?? ??? ?? ??";
        return true;
    }

    public static string StallNote(World server, Entity player)
    {
        var best = BestPair(server, player);
        return $"내 솥 {S.pots.Count}개[{string.Join("; ", new List<PotMem>(S.pots.Values).ConvertAll(m => $"({m.tile.x},{m.tile.y}) {(m.last == "" ? "상태 모름" : m.last)}"))}], 가방 재료 {IngredientsText(server, player)}, 나은 짝 {(best == null ? "없음" : best.Text)}, 준비: {AIBrain.PrepNote}";
    }

    // ------------------------------------------------------------------ candidates (added to the craft list in BuildCandidates)

    private static List<AICandidate> LegacyCandidates(World server, Entity player, AIObservation o, string curKey)
    {
        var res = new List<AICandidate>();
        float3 pos = o.pos;
        var best = BestPair(server, player);
        var pots = PotsInView(server, player);
        var own = pots.Find(v => Own(v) != null);
        if (own != null)
        {
            if (own.outN > 0)
            {
                string k = $"cook:take:{own.entity.Index}";
                if (k != curKey)
                {
                    int h = AIFood.HungerAdd(server, own.outId, own.outVar);
                    res.Add(new AICandidate { kind = "cook", key = k, entity = own.entity, tile = own.tile, actionMode = "take", itemId = own.outId, itemCount = own.outN, progress = true,
                        desc = $"요리 꺼내기 — {AIAgent.Cur.Name}의 솥 {own.Text}. 다 된 {own.outId} {own.outN}개(하나에 배고픔 +{h})를 가방으로. 옆으로 가서 열고 꺼냄." });
                }
            }
            if (own.Empty && best != null && (own.outN == 0 || (own.outId == best.dish && own.outVar == best.variation)))
            {
                string k = $"cook:put:{own.entity.Index}";
                if (k != curKey)
                    res.Add(new AICandidate { kind = "cook", key = k, entity = own.entity, tile = own.tile, actionMode = "put", itemId = best.a, secondItemId = best.b, itemCount = best.n, progress = true,
                        desc = $"요리하기 — 솥 {own.Text}에 {best.a}·{best.b}를 {best.n}개씩 넣음. {best.Text}. 까닭: {best.why}. 넣으면 솥이 저절로 한 접시 약 10초씩 요리함(그동안 다른 일 가능)." });
            }
            if (own.Cooking)
            {
                int left = (int)math.ceil(math.max(0f, own.timeLeft) + 10f * (math.min(own.nA, own.nB) - 1));
                res.Add(new AICandidate { kind = "cookwait", key = $"cookwait:{own.entity.Index}", entity = own.entity, tile = own.tile, waitSeconds = math.clamp(left, 1, 5), progress = true,
                    desc = $"요리 기다리기 — 솥 {own.Text}. 남은 접시를 다 하려면 약 {left}초. {math.clamp(left, 1, 5)}초 솥 옆에서 기다림." });
            }
            else if (own.nA > 0 && own.nB == 0 && own.outN == 0)
            {
                // one input only (a put that filled slot 0 but not slot 1, ai2 2026-11-04 C2): add the best partner
                // from the bag to slot 1 when that pair is better than eating raw
                var counts = AIBody.InventoryCounts(server, player);
                Pair fill = null;
                foreach (var kv in counts)
                {
                    if (kv.Value <= 0 || !IsIngredient(server, kv.Key)) continue;
                    var p = Predict(server, own.inA, kv.Key);
                    if (p == null || !p.better) continue;
                    p.n = math.min(own.nA, kv.Value);
                    if (fill == null || p.cookedHunger - p.rawA - p.rawB > fill.cookedHunger - fill.rawA - fill.rawB) fill = p;
                }
                string k = $"cook:fill:{own.entity.Index}";
                if (fill != null && k != curKey)
                    res.Add(new AICandidate { kind = "cook", key = k, entity = own.entity, tile = own.tile, actionMode = "fill", itemId = fill.b, secondItemId = ObjectID.None, itemCount = fill.n, progress = true,
                        desc = $"요리하기(빈 칸 채우기) — 솥 {own.Text}의 빈 둘째 칸에 {fill.b} {fill.n}개를 넣음. {fill.Text}. 까닭: {fill.why}. 넣으면 솥이 저절로 요리함." });
                else if (fill == null)
                    AIPlayerSpikeMod.Log($"COOK: own pot ({own.tile.x},{own.tile.y}) has one input only and no better partner in the bag: {own.Text}");
            }
            else if (!own.Empty && own.outN == 0)
                AIPlayerSpikeMod.Log($"COOK: own pot ({own.tile.x},{own.tile.y}) cannot cook: {own.Text}");
        }
        else if (S.pots.Count > 0)
        {
            // an own pot out of view: walk next to it when there is something to do there
            foreach (var m in S.pots.Values)
            {
                if (best == null && m.outN == 0 && !m.busy) continue;
                var stands = AICraft.StandTilesFor(server, player, new AIMemory.Station { id = m.id, tile = m.tile });
                var plan = AIApproach.FirstCached(server, player, stands, out int2 st, out string why);
                if (plan == null) { AIPlayerSpikeMod.Log($"COOK: own pot ({m.tile.x},{m.tile.y}): no route ({why})"); continue; }
                string k = $"dig:{st.x},{st.y}";
                if (k == curKey) break;
                res.Add(AIApproach.WalkCandidate(st, $"내 요리 솥 {m.id}", true,
                    $"내 요리 솥으로 가기 — ({m.tile.x},{m.tile.y}) {math.distance(pos.xz, (float2)m.tile):F0}칸(지금은 화면 밖, {AIMemory.Ago(m.seenUnix)}에 봄: {(m.last == "" ? "상태 모름" : m.last)}). 아는 길 {plan.path.Count}칸, 예상 약 {plan.EstimatedSeconds:F0}초. 옆 칸 ({st.x},{st.y})에 서면 끝(요리는 따로)."));
                break;
            }
        }
        else if (best != null)
        {
            // no own pot: place one from the bag at base (making one is the preparation path's step)
            ObjectID pi = PotItem(server);
            AIBody.InventoryCounts(server, player).TryGetValue(pi, out int have);
            if (pi != ObjectID.None && have > 0)
            {
                if (AIBase.AtBase(pos))
                    AIPlaceSpot.AddUnique(res, AIPlaceSpot.Candidates(server, player, pi, "요리 솥 놓기",
                        $"까닭: {AIAgent.Cur.Name}가 놓은 솥이 없음(다른 플레이어의 솥은 쓰지 않음), 기지 {AIBase.BaseText} 안({AIBase.BaseDist(pos):F0}칸). 놓인 솥이 보이는지로 확인.", true, out _));   // no spot: the reason goes to the blocked targets (AIPlaceSpot.TakeNoSpot)
                else if (AIBase.BaseStand(server, player, out int2 st))
                {
                    string k = $"dig:{st.x},{st.y}";
                    var plan = k == curKey ? null : AIBrain.PlanC(server, player, st, out string why);
                    if (plan != null)
                        res.Add(AIApproach.WalkCandidate(st, "기지", true,
                            $"기지로 가기 — 요리 솥을 기지에 놓으려고. 기지 {AIBase.BaseText}, 여기서 {AIBase.BaseDist(pos):F0}칸. 아는 길 {plan.path.Count}칸, 예상 약 {plan.EstimatedSeconds:F0}초."));
                }
            }
        }
        if (res.Count > 0) AIPlayerSpikeMod.Log($"COOK: candidates {res.Count}: {string.Join(" | ", res.ConvertAll(c => c.key))}");
        return res;
    }

    // A request-time snapshot. Tree leaves and fully specified goals use this exact same producer.
    public sealed class Selection
    {
        public Entity Pot;
        public string Mode, Key, Where;
        public ObjectID A, B, Output;
        public int VarA, VarB, OutputVariation, SlotA = -1, SlotB = -1, Amount, OutputSlot;
        public ContainedObjectsBuffer SourceA, SourceB, InputA, InputB, BeforeOutput;
    }
    private static string IngredientKey(ObjectID id, int variation) => id + "#v" + variation;
    private static bool IngredientMatches(string selector, ObjectID id, int variation) =>
        string.IsNullOrEmpty(selector) || selector == "all" || selector == id.ToString() || selector == IngredientKey(id, variation);
    private static string PotKey(PotView p) => $"pot:{p.id}@{p.tile.x},{p.tile.y}";
    private static string PotKey(PotMem p) => $"pot:{p.id}@{p.tile.x},{p.tile.y}";
    private static List<(int slot, ContainedObjectsBuffer item)> Ingredients(World w, Entity p)
    {
        var result = new List<(int, ContainedObjectsBuffer)>();
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
            if (AIInventory.Read(w, p, i, out var x) && x.amount > 0 && IsIngredient(w, x.objectID)) result.Add((i, x));
        return result;
    }
    private static (int slot, ContainedObjectsBuffer item) Source(World w, Entity p, string selector, int skipSlot = -1)
    {
        var best = (-1, default(ContainedObjectsBuffer)); int n = -1;
        foreach (var x in Ingredients(w, p))
            if (x.slot != skipSlot && IngredientMatches(selector, x.item.objectID, x.item.variation) && x.item.amount > n) { best = x; n = x.item.amount; }
        return best;
    }
    // Each input move keeps one exact source. For a repeated ingredient, a second stack can supply input1.
    private static (int slot, ContainedObjectsBuffer item) Partner(World w, Entity p, (int slot, ContainedObjectsBuffer item) a, string selector)
    {
        var b = Source(w, p, selector);
        if (a.slot < 0 || b.slot != a.slot) return b;
        var other = Source(w, p, selector, a.slot);
        return other.slot >= 0 && math.min(a.item.amount, other.item.amount) > a.item.amount / 2 ? other : b;
    }
    private static int SlotRoom(World w, Entity e, int slot, ContainedObjectsBuffer incoming)
    {
        var em = w.EntityManager;
        if (!AIInventory.Read(w, e, slot, out var existing) || AIInventory.SlotAllows(w, e, slot, incoming) != null || !em.HasBuffer<InventoryBuffer>(e)) return 0;
        foreach (var inv in em.GetBuffer<InventoryBuffer>(e, true))
        {
            if (slot < inv.startIndex || slot >= inv.startIndex + inv.size || inv.objectsGetLockedInPlace) continue;
            int cap = AIInventory.Info(incoming)?.isStackable == true && !inv.canOnlyContainOneItemPerSlot ? 9999 : 1;
            if (existing.objectID == ObjectID.None) return cap;
            return AIInventory.Identity(existing, incoming) ? math.max(0, cap - AIInventory.Quantity(existing)) : 0;
        }
        return 0;
    }
    private static int MainRoom(World w, Entity p, ContainedObjectsBuffer x)
    {
        int room = 0;
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++) room += SlotRoom(w, p, i, x);
        return room;
    }
    internal static int MainCount(World w, Entity p, ObjectID id, int variation)
    {
        int count = 0;
        for (int i = AIInventory.MainStart(w, p); i < AIInventory.MainEnd(w, p); i++)
            if (AIInventory.Read(w, p, i, out var x) && x.objectID == id && x.variation == variation) count += AIInventory.Quantity(x);
        return count;
    }
    public static List<AILife.Entry> LifeEntries(World w, Entity p, AIObservation o, string currentKey, LifeSelection s)
    {
        var entries = new List<AILife.Entry>();
        if (string.IsNullOrEmpty(s.Mode))
        {
            foreach (var mode in new[] { "put", "fill", "take", "wait" }) entries.Add(new AILife.Entry { Op = "mode", Id = mode, Text = mode == "put" ? "재료 두 칸에 넣기" : mode == "fill" ? "빈 입력1 채우기" : mode == "take" ? "완성 접시 꺼내기" : "요리 기다리기" });
            return entries;
        }
        bool first = (s.Mode == "put" || s.Mode == "fill") && string.IsNullOrEmpty(s.Item);
        bool second = s.Mode == "put" && !string.IsNullOrEmpty(s.Item) && string.IsNullOrEmpty(s.Second);
        if (first || second)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var chosen = second ? Source(w, p, s.Item) : (-1, default(ContainedObjectsBuffer));
            foreach (var x in Ingredients(w, p))
            {
                string id = IngredientKey(x.item.objectID, x.item.variation);
                if (!seen.Add(id)) continue;
                var source = x;
                string label = $"{x.item.objectID} 변형 {x.item.variation} (가방 {MainCount(w, p, x.item.objectID, x.item.variation)})";
                entries.Add(second
                    ? new AILife.Entry { Op = "second", Id = id, Text = label, Observe = () =>
                        {
                            if (chosen.Item1 < 0) return new AILife.Entry { Text = "선택한 첫 재료가 가방에서 사라짐" };
                            var partner = Partner(w, p, chosen, id);
                            var pair = Predict(w, chosen.Item2.objectID, source.item.objectID);
                            int pairs = partner.slot < 0 ? 0 : chosen.Item1 == partner.slot ? chosen.Item2.amount / 2 : math.min(chosen.Item2.amount, partner.item.amount);
                            if (pair == null || pairs <= 0)
                                return new AILife.Entry { Text = label + " — 유효한 재료 짝/두 개 소비량을 현재 충족하지 못함" };
                            pair.n = chosen.Item2.objectID == partner.item.objectID && chosen.Item2.variation == partner.item.variation
                                ? MainCount(w, p, partner.item.objectID, partner.item.variation) / 2
                                : math.min(MainCount(w, p, chosen.Item2.objectID, chosen.Item2.variation), MainCount(w, p, partner.item.objectID, partner.item.variation));
                            return new AILife.Entry { Op = "second", Id = id, Text = label + " · " + pair.Text + " · " + pair.why };
                        } }
                    : new AILife.Entry { Op = "item", Id = id, Text = label });
            }
            return entries;
        }
        var pots = PotsInView(w, p);
        if (s.Mode == "take" && string.IsNullOrEmpty(s.Item))
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            entries.Add(new AILife.Entry { Op = "item", Id = "all", Text = "출력 종류 무관 — 선택한 자기 솥에서 현재 출력 확인; 화면 밖이면 먼저 가서 확인" });
            foreach (var pot in pots) if (Own(pot) != null && pot.outN > 0 && seen.Add(IngredientKey(pot.outId, pot.outVar)))
                entries.Add(new AILife.Entry { Op = "item", Id = IngredientKey(pot.outId, pot.outVar), Text = $"출력 {pot.outId} 변형 {pot.outVar}" });
            return entries;
        }
        if (string.IsNullOrEmpty(s.Where))
        {
            foreach (var m in OwnPots())
            {
                var visible = pots.Find(v => Own(v) != null && PotKey(v) == PotKey(m));
                entries.Add(new AILife.Entry { Op = "where", Id = PotKey(m), Text = visible != null ? visible.Text : $"{m.id} ({m.tile.x},{m.tile.y}) 화면 밖, {AIMemory.Ago(m.seenUnix)} 마지막 관측: {m.last}; 현재 상태 모름, 가서 확인" });
            }
            return entries;
        }
        var candidate = ExactCandidate(w, p, o, currentKey, s);
        if (candidate != null) entries.Add(new AILife.Entry { Id = candidate.key, Text = candidate.desc, Candidate = candidate });
        else entries.Add(new AILife.Entry { Text = "선택한 요리 조건을 현재 관측에서 충족하지 못함: 재료·솥 입력/출력·공간을 다시 확인하거나 request_plan" });
        return entries;
    }
    public static AICandidate ExactCandidate(World w, Entity p, AIObservation o, string currentKey, LifeSelection s)
    {
        if (s == null || AILife.RequestSatisfied(s) || string.IsNullOrEmpty(s.Mode) || string.IsNullOrEmpty(s.Where)) return null;
        var own = PotsInView(w, p).Find(v => Own(v) != null && PotKey(v) == s.Where);
        if (own == null)
        {
            var memory = OwnPots().Find(m => PotKey(m) == s.Where);
            if (memory == null || AIVision.CanSee(o.pos, memory.tile)) return null;
            var stands = AICraft.StandTilesFor(w, p, new AIMemory.Station { id = memory.id, tile = memory.tile });
            var plan = AIApproach.FirstCached(w, p, stands, out int2 st, out _);
            if (plan == null) return null;
            var move = AIApproach.WalkCandidate(st, "내 요리 솥", false, $"내 솥 ({memory.tile.x},{memory.tile.y})으로 가서 확인 — 화면 밖, {AIMemory.Ago(memory.seenUnix)} 마지막 관측 {memory.last}; 현재 상태 모름. 도착 뒤 다시 선택.");
            move.life = s.Copy(); return move;
        }
        if (s.Mode == "wait")
        {
            if (!own.Cooking) return null;
            int sec = math.clamp((int)math.ceil(math.max(0f, own.timeLeft)), 1, 5);
            string key = $"cookwait:{own.entity.Index}:{own.entity.Version}";
            return new AICandidate { kind = "cookwait", key = key, entity = own.entity, tile = own.tile, waitSeconds = sec, life = s.Copy(), desc = $"솥 {own.Text} 옆에서 {sec}초 기다리기(투입·가방 확보 아님)" };
        }
        if (s.Mode != "put" && s.Mode != "fill" && s.Mode != "take") return null;
        var x = new Selection { Pot = own.entity, Mode = s.Mode, Where = PotKey(own), OutputSlot = own.outIdx };
        if (!AIInventory.Read(w, own.entity, 0, out x.InputA) || !AIInventory.Read(w, own.entity, 1, out x.InputB) || !AIInventory.Read(w, own.entity, own.outIdx, out x.BeforeOutput)) return null;
        int requested = s.RequestedCount, available, sourceAvailable;
        Pair pair = null;
        if (s.Mode == "take")
        {
            if (own.outN <= 0 || !IngredientMatches(s.Item, own.outId, own.outVar)) return null;
            x.Output = own.outId; x.OutputVariation = own.outVar;
            available = math.min(own.outN, MainRoom(w, p, x.BeforeOutput));
            sourceAvailable = available;
            x.Amount = math.min(requested > 0 ? requested : own.outN, available);
        }
        else
        {
            if (string.IsNullOrEmpty(s.Item) || s.Mode == "put" && (string.IsNullOrEmpty(s.Second) || !own.Empty) || s.Mode == "fill" && !(own.nA > 0 && own.nB == 0)) return null;
            var a = Source(w, p, s.Item); if (a.slot < 0) return null;
            x.A = a.item.objectID; x.VarA = a.item.variation; x.SourceA = a.item; x.SlotA = a.slot;
            if (s.Mode == "put")
            {
                var b = Partner(w, p, a, s.Second); if (b.slot < 0) return null;
                x.B = b.item.objectID; x.VarB = b.item.variation; x.SourceB = b.item; x.SlotB = b.slot;
                pair = Predict(w, x.A, x.B); if (pair == null) return null;
                sourceAvailable = x.SlotA == x.SlotB ? a.item.amount / 2 : math.min(a.item.amount, b.item.amount);
                available = x.A == x.B && x.VarA == x.VarB ? MainCount(w, p, x.A, x.VarA) / 2
                    : math.min(MainCount(w, p, x.A, x.VarA), MainCount(w, p, x.B, x.VarB));
                available = math.min(available, math.min(SlotRoom(w, own.entity, 0, a.item), SlotRoom(w, own.entity, 1, b.item)));
            }
            else
            {
                pair = Predict(w, own.inA, x.A); if (pair == null) return null;
                sourceAvailable = a.item.amount;
                available = math.min(own.nA, math.min(MainCount(w, p, x.A, x.VarA), SlotRoom(w, own.entity, 1, a.item)));
            }
            if (own.outN > 0 && (own.outId != pair.dish || own.outVar != pair.variation)) return null;
            available = math.min(available, 9999 - own.outN);
            pair.n = available;
            x.Amount = math.min(requested > 0 ? requested : MaxPairs, math.min(MaxPairs, math.min(available, sourceAvailable)));
            x.Output = pair.dish; x.OutputVariation = pair.variation;
        }
        if (x.Amount <= 0) return null;
        x.Key = $"cook:{x.Mode}:{x.Pot.Index}:{x.Pot.Version}:{x.A}:{x.VarA}:{x.SlotA}:{x.B}:{x.VarB}:{x.SlotB}:{x.Output}:{x.OutputVariation}:n{x.Amount}";
        var binding = s.Copy(); binding.OfferedCount = x.Amount;
        int shortage = requested > 0 ? math.max(0, requested - available) : 0;
        return new AICandidate { kind = "cook", key = x.Key, entity = own.entity, tile = own.tile, actionMode = x.Mode, itemId = x.Mode == "take" ? x.Output : x.A, itemVariation = x.Mode == "take" ? x.OutputVariation : x.VarA, secondItemId = x.B, itemCount = x.Amount, progress = s.MatchesGoal, life = binding, cook = x,
            desc = $"요리 {x.Mode} — {own.Text}. {(pair == null ? $"출력 {x.Output} 변형 {x.OutputVariation}" : pair.Text + " · " + pair.why)}. 요청 {requested} / 이번 시도 {x.Amount} / 전체 재고·시설 기준 가능 {available} / 이번 고정 출발 슬롯 묶음 가능 {sourceAvailable} / 부족 {shortage}. {(x.Mode == "take" ? "정확한 출력의 main inventory 증가로 수령 확인" : "투입 단계이며 음식 확보 아님; 실제 두 입력 확인량만 보고")}." };
    }
    public static string Stale(World w, Entity p, AICandidate c)
    {
        if (c.kind == "cookwait")
        {
            if (!IsOwnPot(w, c.entity)) return "선택한 자기 솥이 사라지거나 소유가 바뀜";
            var pot = PotsInView(w, p).Find(v => v.entity == c.entity);
            return pot == null ? "선택한 솥이 현재 관측 밖" : !pot.Cooking ? "선택한 솥이 더는 요리 중이 아님" : null;
        }
        return c.cook == null ? "정확한 요리 선택 payload 없음" : Stale(w, p, c.cook);
    }
    internal static string Stale(World w, Entity p, Selection x)
    {
        string gate = AIInventory.Gate(w, p); if (gate != null) return gate;
        if (!IsOwnPot(w, x.Pot)) return "선택한 자기 솥이 사라지거나 소유가 바뀜";
        var selectedPot = PotsInView(w, p).Find(v => v.entity == x.Pot);
        if (selectedPot == null) return "선택한 솥이 현재 관측 밖";
        if (PotKey(selectedPot) != x.Where) return "선택한 솥의 종류/위치가 바뀜";
        if (!AIInventory.Read(w, x.Pot, 0, out var a) || !a.EqualsExact(x.InputA) || !AIInventory.Read(w, x.Pot, 1, out var b) || !b.EqualsExact(x.InputB) || !AIInventory.Read(w, x.Pot, x.OutputSlot, out var output) || !output.EqualsExact(x.BeforeOutput)) return "선택 뒤 솥 입력/출력 슬롯이 바뀜";
        if (x.Mode == "take") return MainRoom(w, p, output) < x.Amount ? "선택 뒤 가방 수령 공간이 줄어듦" : null;
        if (!AIInventory.Read(w, p, x.SlotA, out a) || !a.EqualsExact(x.SourceA)) return "선택한 첫 재료 슬롯/variation/수량이 바뀜";
        if (x.Mode == "put" && (!AIInventory.Read(w, p, x.SlotB, out b) || !b.EqualsExact(x.SourceB))) return "선택한 둘째 재료 슬롯/variation/수량이 바뀜";
        return null;
    }
    public static AISkill Make(AICandidate c) => new CookSkill(c.cook) { Key = c.key };
    public static List<AICandidate> Candidates(World w, Entity p, AIObservation o, string currentKey)
    {
        var result = new List<AICandidate>();
        var selection = AILife.ForGoal(AIBrain.Goal);
        if (!string.IsNullOrEmpty(selection.Mode))
        {
            var exact = ExactCandidate(w, p, o, currentKey, selection);
            if (exact != null) result.Add(exact);
            return result;
        }
        var best = BestPair(w, p); // recommendation only; every valid pair remains reachable through the tree
        foreach (var pot in PotsInView(w, p))
        {
            if (Own(pot) == null) continue;
            var chosen = selection.Copy(); chosen.Where = PotKey(pot);
            if (pot.outN > 0) { chosen.Mode = "take"; chosen.Item = IngredientKey(pot.outId, pot.outVar); }
            else if (pot.Cooking) chosen.Mode = "wait";
            else if (pot.Empty && best != null) { chosen.Mode = "put"; chosen.Item = best.a.ToString(); chosen.Second = best.b.ToString(); }
            else continue;
            var candidate = ExactCandidate(w, p, o, currentKey, chosen); if (candidate != null) result.Add(candidate);
        }
        if (S.pots.Count == 0) result.AddRange(LegacyCandidates(w, p, o, currentKey).FindAll(c => c.kind == "place" || c.kind == "dig"));
        return result;
    }

    // ------------------------------------------------------------------ reports

    public static string PromptLine(World server, Entity player, AIObservation o)
    {
        if (AIBrain.Goal.kind != GoalKind.Cook) return "";
        var best = BestPair(server, player);
        var own = PotsInView(server, player).Find(v => Own(v) != null);
        return $"요리: 내 솥 {(own != null ? own.Text : S.pots.Count > 0 ? $"{S.pots.Count}개(화면 밖)" : "없음")}. 가방 재료 {IngredientsText(server, player)}. 가장 나은 짝 {(best == null ? "없음" : best.Text + " — " + best.why)}. 규칙: {Rule}.";
    }

    public static string ReportFields(World server, Entity player, AIObservation o, Func<string, string> esc)
    {
        var best = BestPair(server, player);
        var own = PotsInView(server, player).Find(v => Own(v) != null);
        string pots = own != null ? own.Text : S.pots.Count == 0 ? "없음" : string.Join("; ", new List<PotMem>(S.pots.Values).ConvertAll(m => $"({m.tile.x},{m.tile.y}) 화면 밖, {AIMemory.Ago(m.seenUnix)}에 봄: {(m.last == "" ? "상태 모름" : m.last)}"));
        return $"\"cook\":{{\"pots\":\"{esc(pots)}\",\"ingredients\":\"{esc(IngredientsText(server, player))}\",\"best\":\"{esc(best == null ? "없음" : best.Text + " — " + best.why)}\",\"note\":\"{esc(Rule)}\"}}";
    }

    // Whether a cook goal makes sense now (the upper LLM's answer check).
    public static string CanCook(World server, Entity player)
    {
        var ingredients = Ingredients(server, player);
        for (int i = 0; i < ingredients.Count; i++)
            for (int j = i; j < ingredients.Count; j++)
                if ((i != j || ingredients[i].item.amount >= 2) && Predict(server, ingredients[i].item.objectID, ingredients[j].item.objectID) != null) return null;
        foreach (var pot in PotsInView(server, player)) if (Own(pot) != null && (pot.outN > 0 || !pot.Empty)) return null;
        foreach (var m in S.pots.Values) if (m.outN > 0 || m.busy) return null;
        return "가방에 유효한 요리 재료 짝이 없고 솥에 꺼낼 것·요리 중인 것도 없음(cook 참고)";
    }

    // ------------------------------------------------------------------ /ai cook (test)

    public static void Command(World server, Entity player, string[] parts)
    {
        string a = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "";
        if (a == "잊기" || a == "forget")
        {
            int n = S.pots.Count; S.pots.Clear(); S.watch.Clear(); AIMemory.MarkDirty(); AIMemory.Save(true);
            AIPlayerSpikeMod.Log($"COOK: {n} pots forgotten (/ai cook forget, test)");
            AIPlayerSpikeMod.Reply($"{AIAgent.Cur.Name} 요리 솥 기억 {n}개를 지웠습니다.");
            return;
        }
        var lines = new List<string>();
        foreach (var v in PotsInView(server, player)) lines.Add($"{(Own(v) != null ? "내 솥" : "남의 솥")} {v.Text}");
        foreach (var m in S.pots.Values) if (!lines.Exists(l => l.Contains($"({m.tile.x},{m.tile.y})"))) lines.Add($"내 솥 ({m.tile.x},{m.tile.y}) 화면 밖: {(m.last == "" ? "상태 모름" : m.last)}");
        // every pair from the bag with the prediction (good or not), for checking the numbers in play
        var counts = AIBody.InventoryCounts(server, player);
        var ing = new List<ObjectID>();
        foreach (var kv in counts) if (kv.Value > 0 && IsIngredient(server, kv.Key)) ing.Add(kv.Key);
        var preds = new List<string>();
        for (int i = 0; i < ing.Count && preds.Count < 8; i++)
            for (int j = i; j < ing.Count && preds.Count < 8; j++)
            {
                var p = Predict(server, ing[i], ing[j]);
                if (p != null) preds.Add($"{p.a}+{p.b}→{p.dish}(var {p.variation}) 배고픔 {p.cookedHunger} vs 날로 {p.rawA}+{p.rawB}, 효과 [{EffectsText(p.cooked, true)}] {(p.better ? "나음" : "아님")}");
            }
        foreach (var f in AIFood.InBag(server, player)) if (IsCookedFood(server, f.id, f.variation)) preds.Add($"가방 요리 {f.id}(var {f.variation}) x{f.count} 배고픔 +{f.hunger}");
        string txt = $"{AIAgent.Cur.Name} 요리 · 솥 [{string.Join(" / ", lines)}] · 재료 {IngredientsText(server, player)} · {string.Join(" / ", preds)}";
        AIPlayerSpikeMod.Log("COOK: " + txt);
        AIPlayerSpikeMod.Reply(txt.Length > 900 ? txt.Substring(0, 900) + "…" : txt);
    }
}

// Normal UI moves only. Queue acceptance is not a receipt; both bag consumption and pot state must agree.
public class CookSkill : AISkill, ILifeResult
{
    private readonly AICook.Selection _selected;
    private AICook.Selection _request;
    private readonly Entity _legacyPot;
    private readonly string _legacyMode;
    private readonly ObjectID _legacyA, _legacyB;
    private readonly int _legacyN;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _sentA, _sentB, _sentTake;
    private int _beforeA, _beforeB, _beforeReceived, _sentBCount, _removedA;
    private float _sentAt;
    private int2 _tile;
    public int ConfirmedWork { get; private set; }
    public int ConfirmedReceived { get; private set; }
    public int RequestedWork => _request?.Amount ?? _legacyN;
    public string ResultStage { get; private set; } = "unconfirmed";
    public CookSkill(AICook.Selection selection)
    {
        _selected = selection; Name = selection?.Mode == "take" ? "요리 꺼내기" : "요리 투입"; Key = selection?.Key ?? "cook:missing";
    }
    // Compatibility for old callers: resolve once through the same explicit producer at Start.
    public CookSkill(Entity pot, string mode, ObjectID a, ObjectID b, int n)
    {
        _legacyPot = pot; _legacyMode = mode; _legacyA = a; _legacyB = b; _legacyN = n;
        Name = mode == "take" ? "요리 꺼내기" : "요리 투입"; Key = $"cook:{mode}:{pot.Index}";
    }
    private bool Reject(string why) { Status = SkillStatus.Failed; Reason = why; Facts.Add(why); return false; }
    public override bool Start(World server, Entity player, float now)
    {
        _request = _selected;
        if (_request == null && _legacyPot != Entity.Null)
        {
            var pot = AICook.PotsInView(server, player).Find(x => x.entity == _legacyPot);
            if (pot != null)
            {
                var selection = new LifeSelection { Branch = "cook", Mode = _legacyMode, Item = _legacyA == ObjectID.None ? "" : _legacyA.ToString(), Second = _legacyB == ObjectID.None ? "" : _legacyB.ToString(), Where = $"pot:{pot.id}@{pot.tile.x},{pot.tile.y}", RequestedCount = _legacyN };
                _request = AICook.ExactCandidate(server, player, new AIObservation { pos = AIBody.Pos(server, player) }, "", selection)?.cook;
            }
        }
        if (_request == null) return Reject("정확한 요리 선택 조건을 만들 수 없음");
        string stale = AICook.Stale(server, player, _request); if (stale != null) return Reject(stale);
        Key = _request.Key;
        var em = server.EntityManager;
        var pos = em.GetComponentData<LocalTransform>(_request.Pot).Position; _tile = AIBody.TileOf(pos);
        _beforeA = AICook.MainCount(server, player, _request.A, _request.VarA);
        _beforeB = AICook.MainCount(server, player, _request.B, _request.VarB);
        _beforeReceived = AICook.MainCount(server, player, _request.Output, _request.OutputVariation);
        Facts.Add($"요청 시도량 {_request.Amount}, {_request.Mode}, 솥 {_request.Pot.Index}:{_request.Pot.Version}, 재료 {_request.A} v{_request.VarA} → {_request.B} v{_request.VarB}; 가방 확보는 별도 관측");
        if (math.distance(AIBody.Pos(server, player).xz, pos.xz) > AIBase.ChestReach)
        {
            _nav.Start(server, player, _tile, AIBase.ChestReach - 0.4f, now, _request.Pot); _navOn = true;
        }
        return true;
    }
    private bool SameSource(World w, Entity p, int slot, ContainedObjectsBuffer before, int need)
    {
        return AIInventory.Read(w, p, slot, out var now) && now.EqualsIgnoreAmount(before) && now.amount >= need;
    }
    private int OutputProduced(AICook.PotView pot)
    {
        int before = _request.BeforeOutput.objectID == _request.Output && _request.BeforeOutput.variation == _request.OutputVariation ? _request.BeforeOutput.amount : 0;
        return pot.outId == _request.Output && pot.outVar == _request.OutputVariation ? math.max(0, pot.outN - before) : 0;
    }
    private BodyInput EndObserved(World w, Entity p, string stage, int work, int received, string note)
    {
        ResultStage = stage; ConfirmedWork = work; ConfirmedReceived = received;
        string cursor = "";
        var em = w.EntityManager;
        if (em.HasComponent<CraftingCD>(p) && AIInventory.Read(w, p, em.GetComponentData<CraftingCD>(p).outputSlotIndex, out var item) && item.objectID != ObjectID.None) cursor = $"; 커서 잔류 {item.objectID} v{item.variation} {item.amount}(가방 확보 아님)";
        string result = $"요청/이번 시도 {_request.Amount} / 확인된 작업 {work} / main inventory 확보 {received} / 이번 미확인 {math.max(0, _request.Amount - (stage == "received" ? received : work))}: {note}{cursor}";
        Facts.Add(result);
        return Finish(SkillStatus.Done, result);
    }
    public override BodyInput Tick(World w, Entity p, float now)
    {
        if (_request == null) return Finish(SkillStatus.Failed, "요리 선택 payload 없음");
        if (!AICook.IsOwnPot(w, _request.Pot)) return Finish(SkillStatus.Failed, "선택한 자기 솥이 사라지거나 소유가 바뀜");
        var em = w.EntityManager; float3 cp = em.GetComponentData<LocalTransform>(_request.Pot).Position;
        if (_navOn)
        {
            if (math.distance(AIBody.Pos(w, p).xz, cp.xz) <= AIBase.ChestReach) { _nav.Dispose(w); _navOn = false; }
            else
            {
                var status = _nav.Tick(w, p, now, out float2 move);
                if (status == NavStatus.Failed) return Finish(SkillStatus.Failed, "선택한 솥으로 가지 못함: " + _nav.FailReason);
                if (status == NavStatus.Arrived) { _nav.Dispose(w); _navOn = false; }
                else return new BodyInput { move = move, slot = -1 };
            }
        }
        var pot = AICook.PotsInView(w, p).Find(x => x.entity == _request.Pot);
        if (pot == null) return Finish(SkillStatus.Failed, "선택한 솥이 현재 보이지 않음");
        var face = AIBody.DirTo(AIBody.Pos(w, p), cp.xz);
        if (!_sentA && !_sentB && !_sentTake)
        {
            string stale = AICook.Stale(w, p, _request); if (stale != null) return Finish(SkillStatus.Failed, stale);
            if (_request.Mode == "take")
            {
                int cursor = em.HasComponent<CraftingCD>(p) ? em.GetComponentData<CraftingCD>(p).outputSlotIndex : 50;
                if (!AICraft.QueueInventoryChange(w, p, Create.MoveOrDropAmount(_request.Pot, _request.OutputSlot, p, -1, cursor, _request.Amount, AIBody.Pos(w, p)))) return Finish(SkillStatus.Failed, "수령 UI 요청을 넣지 못함");
                _sentTake = true; _sentAt = now;
            }
            else if (_request.Mode == "fill")
            {
                if (!AICraft.QueueInventoryChange(w, p, Create.MoveOrDropAmount(p, _request.SlotA, _request.Pot, 1, -1, _request.Amount, AIBody.Pos(w, p)))) return Finish(SkillStatus.Failed, "입력1 UI 요청을 넣지 못함");
                _sentB = true; _sentBCount = _request.Amount; _beforeB = _beforeA; _sentAt = now;
            }
            else
            {
                if (!AICraft.QueueInventoryChange(w, p, Create.MoveOrDropAmount(p, _request.SlotA, _request.Pot, 0, -1, _request.Amount, AIBody.Pos(w, p)))) return Finish(SkillStatus.Failed, "입력0 UI 요청을 넣지 못함");
                _sentA = true; _sentAt = now;
            }
            return new BodyInput { face = face, slot = -1 };
        }
        if (_sentTake)
        {
            int received = AICook.MainCount(w, p, _request.Output, _request.OutputVariation) - _beforeReceived;
            int remaining = pot.outId == _request.Output && pot.outVar == _request.OutputVariation ? pot.outN : pot.outN == 0 ? 0 : _request.BeforeOutput.amount;
            int removed = _request.BeforeOutput.amount - remaining;
            if (received > 0 && received <= _request.Amount)
                return EndObserved(w, p, "received", received, received, $"솥 출력 잔류 {pot.outN}; 실제 출력 감소 {removed}, 요청량만 이동");
            if (now - _sentAt > 2f) ObserveOnce("take-pending", $"가방 수령 미확인: 증가 {received}, 출력 감소 {removed}, 솥 {pot.Text}; 재요청하지 않음");
            return new BodyInput { face = face, slot = -1 };
        }
        int produced = OutputProduced(pot);
        if (_sentA && !_sentB)
        {
            int removed = _beforeA - AICook.MainCount(w, p, _request.A, _request.VarA);
            int present = pot.inA == _request.A && pot.inVarA == _request.VarA ? pot.nA : 0;
            _removedA = math.min(_request.Amount, math.min(removed, present));
            if (_removedA > 0)
            {
                if (!pot.Empty && pot.nB > 0) return Finish(SkillStatus.Failed, "첫 투입 뒤 입력1이 다른 요청으로 바뀜; 자동 대체하지 않음");
                if (!SameSource(w, p, _request.SlotB, _request.SourceB, _removedA)) return Finish(SkillStatus.Failed, "첫 투입 뒤 선택한 둘째 재료 슬롯이 바뀜; 현재 입력0은 솥에 잔류");
                _beforeB = AICook.MainCount(w, p, _request.B, _request.VarB);
                if (!AICraft.QueueInventoryChange(w, p, Create.MoveOrDropAmount(p, _request.SlotB, _request.Pot, 1, -1, _removedA, AIBody.Pos(w, p)))) return Finish(SkillStatus.Failed, "입력0 일부 투입 뒤 입력1 요청을 넣지 못함; 입력0 잔류");
                _sentB = true; _sentBCount = _removedA; _sentAt = now;
                return new BodyInput { face = face, slot = -1 };
            }
            if (now - _sentAt > 2f) ObserveOnce("first-pending", $"입력0 투입 미확인: 가방 감소 {removed}, 입력0 {present}; 입력1을 미리/반복 보내지 않음");
            return new BodyInput { face = face, slot = -1 };
        }
        ObjectID bId = _request.Mode == "fill" ? _request.A : _request.B;
        int bVar = _request.Mode == "fill" ? _request.VarA : _request.VarB;
        int removedB = _beforeB - AICook.MainCount(w, p, bId, bVar);
        int presentB = pot.inB == bId && pot.inVarB == bVar ? pot.nB : 0;
        int confirmedB = math.min(_sentBCount, math.min(removedB, presentB + produced));
        int confirmedA = _request.Mode == "fill" ? _sentBCount : math.min(_removedA, (pot.inA == _request.A && pot.inVarA == _request.VarA ? pot.nA : 0) + produced);
        int confirmed = math.max(0, math.min(confirmedA, confirmedB));
        if (confirmed > 0) return EndObserved(w, p, "inserted", confirmed, 0, $"투입 단계, 음식 확보 아님. 실제 입력 [{pot.nA},{pot.nB}], 출력 {pot.outN}, 확인된 자동 생산 {produced}; 남은 빈 입력은 다음 fill 선택");
        if (now - _sentAt > 2f) ObserveOnce("pair-pending", $"둘째 투입 미확인: 가방 감소 {removedB}, 입력1 {presentB}, 출력증가 {produced}; 요청량 전체 성공으로 세지 않고 반복 발송하지 않음");
        return new BodyInput { face = face, slot = -1 };
    }
    public override string Progress(World server, Entity player) => _navOn ? "선택한 솥으로 이동" : $"요리 {_request?.Mode}: 이번 요청 {RequestedWork}, 확인 {ConfirmedWork}, 확보 {ConfirmedReceived}, {ResultStage}";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
