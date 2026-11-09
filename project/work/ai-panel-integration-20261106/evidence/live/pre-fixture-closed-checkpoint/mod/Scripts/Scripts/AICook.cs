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
        public ObjectID inA, inB, outId; public int nA, nB, outN, outVar, outIdx; public float timeLeft = -1f;
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
        if (buf.Length > 0) { v.inA = buf[0].objectID; v.nA = v.inA == ObjectID.None ? 0 : buf[0].amount; }
        if (buf.Length > 1) { v.inB = buf[1].objectID; v.nB = v.inB == ObjectID.None ? 0 : buf[1].amount; }
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
        var best = BestPair(server, player);
        var own = PotsInView(server, player).Find(v => Own(v) != null);
        if (own != null)
        {
            if (own.outN > 0 || !own.Empty || best != null) return false;
            text = $"요리할 더 나은 재료 짝이 가방에 없고 솥 {own.tile.x},{own.tile.y}이 비었음(가방 재료: {IngredientsText(server, player)})";
            return true;
        }
        if (best != null) return false;
        foreach (var m in S.pots.Values) if (m.outN > 0 || m.busy) return false;   // a remembered pot still had food / was cooking
        text = $"요리할 더 나은 재료 짝이 가방에 없음(가방 재료: {IngredientsText(server, player)})";
        return true;
    }

    public static string StallNote(World server, Entity player)
    {
        var best = BestPair(server, player);
        return $"내 솥 {S.pots.Count}개[{string.Join("; ", new List<PotMem>(S.pots.Values).ConvertAll(m => $"({m.tile.x},{m.tile.y}) {(m.last == "" ? "상태 모름" : m.last)}"))}], 가방 재료 {IngredientsText(server, player)}, 나은 짝 {(best == null ? "없음" : best.Text)}, 준비: {AIBrain.PrepNote}";
    }

    // ------------------------------------------------------------------ candidates (added to the craft list in BuildCandidates)

    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey)
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
        if (BestPair(server, player) != null) return null;
        foreach (var m in S.pots.Values) if (m.outN > 0 || m.busy) return null;
        return "가방에 요리하면 더 나은 재료 짝이 없고, 솥에 꺼낼 것·요리 중인 것도 없음(cook 참고)";
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

// Cooking at an own pot (CookSkill): walk next to it, open it (own pot only), then
// - put: move ingredient a into input slot 0 and b into slot 1, n of each (client MoveOrDropAmount, as moving a stack
//   part into the open pot); the pot cooks by itself. Done when both inputs show up in the pot.
// - take: move the output slot's dish into the bag (as TakeSkill). Done when the bag has more of it.
public class CookSkill : AISkill
{
    private readonly Entity _pot;
    private readonly string _mode;
    private readonly ObjectID _a, _b;
    private readonly int _n;
    private readonly AINavigator _nav = new AINavigator();
    private bool _navOn, _sent;
    private float _sentAt;
    private int _before;
    private Dictionary<ObjectID, int> _invBefore;
    private int2 _tile;
    public CookSkill(Entity pot, string mode, ObjectID a, ObjectID b, int n)
    {
        Name = mode == "take" ? "요리 꺼내기" : "요리하기"; _pot = pot; _mode = mode; _a = a; _b = b; _n = math.max(1, n); Key = $"cook:{mode}:{pot.Index}";
    }

    public override bool Start(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!AICook.IsPot(server, _pot)) { Status = SkillStatus.Failed; Reason = "요리 솥이 없음"; return false; }
        if (!AICook.IsOwnPot(server, _pot)) { Status = SkillStatus.Failed; Reason = "이 캐릭터가 놓은 솥이 아님(다른 플레이어의 솥은 쓰지 않음)"; return false; }
        _invBefore = AIBody.InventoryCounts(server, player);
        _invBefore.TryGetValue(_a, out _before);
        float3 cp = em.GetComponentData<LocalTransform>(_pot).Position;
        _tile = AIBody.TileOf(cp);
        Facts.Add($"{_mode} at pot ({cp.x:F1},{cp.z:F1}): a={_a} b={_b} n={_n}; bag had {_a} {_before}");
        if (math.distance(AIBody.Pos(server, player).xz, cp.xz) > AIBase.ChestReach)
        {
            _nav.Start(server, player, _tile, AIBase.ChestReach - 0.4f, now, _pot);
            _navOn = true;
        }
        return true;
    }

    private static int BagSlot(World server, Entity player, ObjectID id, int need, int skipSlot = -1)
    {
        var buf = server.EntityManager.GetBuffer<ContainedObjectsBuffer>(player, true);
        int best = -1, bestN = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].objectID != id || i == skipSlot) continue;
            if (buf[i].amount >= need) return i;
            if (buf[i].amount > bestN) { best = i; bestN = buf[i].amount; }
        }
        return best;
    }

    public override BodyInput Tick(World server, Entity player, float now)
    {
        var em = server.EntityManager;
        if (!AICook.IsPot(server, _pot)) { AICook.Forget(_tile, "gone while cooking"); return Finish(SkillStatus.Failed, "솥이 사라짐"); }
        float3 cp = em.GetComponentData<LocalTransform>(_pot).Position;
        if (_navOn)
        {
            if (math.distance(AIBody.Pos(server, player).xz, cp.xz) <= AIBase.ChestReach) { _nav.Dispose(server); _navOn = false; }
            else
            {
                var st = _nav.Tick(server, player, now, out float2 move);
                if (st == NavStatus.Failed) return Finish(SkillStatus.Failed, "솥으로 가지 못함: " + _nav.FailReason);
                if (st == NavStatus.Arrived) { _nav.Dispose(server); _navOn = false; }
                else return new BodyInput { move = move, slot = -1 };
            }
        }
        var pot = AICook.PotsInView(server, player).Find(v => v.entity == _pot);
        if (pot == null) return Finish(SkillStatus.Failed, "솥 옆인데 솥이 보이지 않음");
        float3 me = AIBody.Pos(server, player);
        var face = AIBody.DirTo(me, cp.xz);
        if (!_sent)
        {
            if (_mode == "take")
            {
                if (pot.outN <= 0) return Finish(SkillStatus.Failed, $"열어 보니 출력 칸이 비었음({pot.Text})");
                int out_ = em.HasComponent<CraftingCD>(player) ? em.GetComponentData<CraftingCD>(player).outputSlotIndex : 50;
                _invBefore.TryGetValue(pot.outId, out _before);
                if (!AICraft.QueueInventoryChange(server, player, Create.MoveOrDropAmount(_pot, pot.outIdx, player, -1, out_, pot.outN, me)))
                    return Finish(SkillStatus.Failed, "옮기기 요청을 넣지 못함(UIActionBuffer 없음)");
                AIPlayerSpikeMod.Log($"SKILL 요리 꺼내기: take {pot.outId} x{pot.outN} (variation {pot.outVar}) from pot slot {pot.outIdx} (client MoveOrDropAmount)");
                _n0 = pot.outN; _outId = pot.outId;
            }
            else if (_mode == "fill")
            {
                if (!(pot.nA > 0 && pot.nB == 0)) return Finish(SkillStatus.Failed, $"열어 보니 첫째 칸만 찬 상태가 아님({pot.Text})");
                if (BagSlot(server, player, _a, 1) < 0) return Finish(SkillStatus.Failed, $"가방에 {_a}이(가) 없음");
                _invBefore.TryGetValue(_a, out _before);
                _n0 = math.min(_n, pot.nA); _bId = _a; _pendingB = true;
                _sent = true; _sentAt = now - 1f;   // slot 1 move goes out on the next tick (below)
                return new BodyInput { face = face, slot = -1 };
            }
            else
            {
                if (!pot.Empty) return Finish(SkillStatus.Failed, $"열어 보니 입력 칸이 비어 있지 않음({pot.Text})");
                int n = _n;
                int sa = BagSlot(server, player, _a, _a == _b ? 2 * n : n);
                if (sa < 0) return Finish(SkillStatus.Failed, $"가방에 {_a}이(가) 없음");
                var buf = em.GetBuffer<ContainedObjectsBuffer>(player, true);
                int sb = _a == _b && buf[sa].amount >= 2 ? sa : BagSlot(server, player, _b, n, _a == _b ? sa : -1);
                if (sb < 0) return Finish(SkillStatus.Failed, $"가방에 {_b}이(가) 더 없음");
                n = _a == _b && sa == sb ? math.min(n, buf[sa].amount / 2) : math.min(n, math.min(buf[sa].amount, buf[sb].amount));
                if (n <= 0) return Finish(SkillStatus.Failed, "넣을 재료 수가 0");
                // slot 0 now, slot 1 on a later tick: both queued in one frame got the same tick and only the first
                // reached the pot (ai2 2026-11-04 C2: "입력 [Mushroom 2, 빈 칸]"); StoreSkill also sends one move per ~0.1 s
                if (!AICraft.QueueInventoryChange(server, player, Create.MoveOrDropAmount(player, sa, _pot, 0, -1, n, me)))
                    return Finish(SkillStatus.Failed, "옮기기 요청을 넣지 못함(UIActionBuffer 없음)");
                AIPlayerSpikeMod.Log($"SKILL 요리하기: put {_a} x{n} (bag slot {sa}) into pot slot 0 (client MoveOrDropAmount); {_b} into slot 1 next");
                _n0 = n; _bId = _b; _pendingB = true;
            }
            _sent = true; _sentAt = now;
            return new BodyInput { face = face, slot = -1 };
        }
        if (_mode == "take")
        {
            var inv = AIBody.InventoryCounts(server, player);
            inv.TryGetValue(_outId, out int c);
            if (c > _before) return Finish(SkillStatus.Done, $"솥에서 {_outId} {c - _before}개 꺼냄 · 가방 {AIBody.InventoryDiff(_invBefore, inv)}");
            if (now - _sentAt > 2f) return Finish(SkillStatus.Failed, $"2초 동안 가방이 늘지 않음({_outId} {_before}→{c}); 솥 {pot.Text}");
            return new BodyInput { face = face, slot = -1 };
        }
        // put, second input: once slot 0 holds the first one (or after 0.15 s), on a later tick than the first move
        if (_pendingB && (pot.nA > 0 || now - _sentAt >= 0.15f))
        {
            int sb2 = BagSlot(server, player, _bId, _n0);
            if (sb2 < 0) return Finish(SkillStatus.Failed, $"두 번째 재료 {_bId}이(가) 가방에 없음({pot.Text})");
            if (!AICraft.QueueInventoryChange(server, player, Create.MoveOrDropAmount(player, sb2, _pot, 1, -1, _n0, me)))
                return Finish(SkillStatus.Failed, "옮기기 요청을 넣지 못함(UIActionBuffer 없음)");
            AIPlayerSpikeMod.Log($"SKILL 요리하기: put {_bId} x{_n0} (bag slot {sb2}) into pot slot 1 (client MoveOrDropAmount)");
            _pendingB = false; _sentAt = now;
            return new BodyInput { face = face, slot = -1 };
        }
        if (_pendingB) return new BodyInput { face = face, slot = -1 };
        // put: both inputs in the pot (or already cooked into the output)
        if ((pot.nA > 0 && pot.nB > 0) || pot.outN > 0)
        {
            AIPlayerSpikeMod.Log($"COOK: started at ({_tile.x},{_tile.y}): {pot.Text}");
            return Finish(SkillStatus.Done, $"솥에 {_a}·{_b} {_n0}개씩 넣음, 요리 시작({pot.Text}) · 가방 {AIBody.InventoryDiff(_invBefore, AIBody.InventoryCounts(server, player))}");
        }
        if (now - _sentAt > 2f) return Finish(SkillStatus.Failed, $"2초 뒤 솥 입력이 둘 다 차지 않음({pot.Text}) · 가방 {AIBody.InventoryDiff(_invBefore, AIBody.InventoryCounts(server, player))}");
        return new BodyInput { face = face, slot = -1 };
    }
    private int _n0;
    private ObjectID _outId, _bId;
    private bool _pendingB;

    public override string Progress(World server, Entity player) => _navOn ? "솥으로 가는 중" : _mode == "take" ? "꺼내는 중" : "넣는 중";
    public override void Cleanup(World server) { _nav.Dispose(server); }
}
