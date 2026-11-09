// AIGoals — the goal a character works on and its life: set (host /ai goal, upper LLM), clear, free time, the done
// observations and the goal command. Observed completion does not end a selected action; the AI chooses completion.
// AIBrain keeps forwarders with the old names (AIBrain.Goal, SetGoal, ClearGoal, HandleGoalCommand, TryItemPublic), so
// the feature modules and the planner call as before. The brain state a goal change touches (decision revision,
// request, self-defence, block note) stays in AIBrain and is reached through the forwarding members below.
// CheckAfterBuild receives the completed build facts; request scheduling stays in AIBrain.Send.
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class AIGoals
{
    // "/ai goal 없음" words (also used by the chat command to stop every member, 검수 F3)
    public static bool IsNoGoalWord(string w) { w = (w ?? "").ToLowerInvariant(); return w == "없음" || w == "none" || w == "off" || w == "해제"; }

    // ---- per-character state (multi-AI): AIAgent.Cur's Slots[15]
    private sealed class State
    {
        public AIGoal Goal = new AIGoal();
        public int GoalRevision;
        public float _lastGoalCheck;
        public string Completion = "", Blocked = "";
        // free time (no goal, upper LLM unavailable; 2026-11-04 막힘-멈춤-정리 item 1)
        public float _freeCheck; public string _freeWhy = "";
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Goals);
    private static State S => StateOf(AIAgent.Cur);

    public static AIGoal Goal { get => S.Goal; set => S.Goal = value; }
    // Changes only when the goal is set or cleared (see AIBrain.GoalRevision).
    public static int GoalRevision => S.GoalRevision;
    public static string FreeWhy => S._freeWhy;
    public static string Observation => string.Join(" / ", new[] { S.Completion, S.Blocked });
    public static bool CompletionObserved => !string.IsNullOrEmpty(S.Completion);
    private static float _lastGoalCheck { get => S._lastGoalCheck; set => S._lastGoalCheck = value; }

    // ---- the brain state a goal change touches (same names as in AIBrain, so the moved code reads as before)
    private static int Revision { get => AIBrain.Revision; set => AIBrain.Revision = value; }
    private static int Calls => AIBrain.Calls;
    private static bool Verbose { get => AIBrain.Verbose; set => AIBrain.Verbose = value; }
    private static bool _sd { get => AIBrain._sd; set => AIBrain._sd = value; }
    private static bool _needCall { get => AIBrain._needCall; set => AIBrain._needCall = value; }
    private static string _needReason { get => AIBrain._needReason; set => AIBrain._needReason = value; }
    private static int _hpAtLastCall { get => AIBrain._hpAtLastCall; set => AIBrain._hpAtLastCall = value; }
    private static void AbortRequest(string why) => AIBrain.AbortRequest(why);
    private static void SetBlockNote(string note, bool toolShort) => AIBrain.SetBlockNote(note, toolShort);
    private static bool KeyMissing() => AIBrain.KeyMissing();
    private static DecisionsUsage Usage() => AIBrain.Usage();
    private static DecisionsSettings Settings() => AIBrain.Settings();
    private static string StatusText(World server, Entity player) => AIBrain.StatusText(server, player);
    private static string Trim(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";

    // e.Why is the sentence every log and record keeps; e.Outcome / e.Cls say the result as values (refactor S2b: the
    // experience record uses them; a null Cls is still classified from the sentence, OUTCOME-MISMATCH under the test switch)
    public static void ClearGoal(GoalEnd e)
    {
        string why = e.Why;
        AIBrain.CancelPendingChoices("goal cleared");
        if (Goal.kind == GoalKind.None) { _sd = false; return; }
        AIPlayerSpikeMod.Log($"GOAL cleared ({why}): was '{Goal.Text}' rev={Revision}");
        bool real = Goal.IsReal;
        if (real) AIExperience.GoalEnding(Goal, e);   // experience memory: this goal run ends (AIExperience.cs); free time is no goal run
        Goal = new AIGoal();
        S.Completion = S.Blocked = "";
        // free time is not a goal anyone set: ending it does not make an upper-LLM answer on the way stale
        Revision++; if (real) S.GoalRevision++;
        AILife.OnGoalChanged();
    }

    // ---- free time (2026-11-04 막힘-멈춤-정리 item 1, HEAD decision): no goal and the member's upper LLM cannot give
    // one now (AIPlanner.FreeTimeReason: usage refused, bridge down, an answer pending > 20 s, a retry due later).
    // The decision model gets the free-time set (BuildCandidates: follow the host or a visible teammate, explore nearby,
    // help a teammate, go home and store the bag when there is a chest, eat when hungry, combat options, wait) and
    // chooses; the program picks no default action. A goal from the upper LLM or the host replaces it (SetGoal). The
    // goal revision the upper LLM's answers are checked against does not change (a pending answer stays valid).
    internal static void FreeTime(World server, Entity player, float now)
    {
        if (now - S._freeCheck < 0.5f) return;
        S._freeCheck = now;
        if (Goal.kind == GoalKind.None)
        {
            string why = AIPlanner.FreeTimeReason(now);
            if (why == null) return;
            if (KeyMissing() || Usage().usd >= Settings().monthlyCapUsd) return;   // no decisions possible (Send would clear it again)
            var em = server.EntityManager;
            if (em.HasComponent<HealthCD>(player) && em.GetComponentData<HealthCD>(player).health <= 0) return;   // down
            S._freeWhy = why;
            Goal = new AIGoal { kind = GoalKind.Free, source = "free", startTime = now };
            AILife.OnGoalChanged();
            _sd = false;   // a self-defence episode goes on as a fight inside free time (combat options are offered)
            Revision++;
            AbortRequest("free time");
            _needCall = true; _needReason = "자유 시간 시작: " + why;
            _hpAtLastCall = -1;
            AIPlayerSpikeMod.Log($"GOAL free time rev={Revision}: {why}");
            AIPlayerSpikeMod.Reply($"[{AIAgent.Cur.Name}] 자유 시간: {Trim(why, 90)}. 위쪽 LLM 목표가 오기 전까지 행동 판단기가 고릅니다.", ChatKind.Status);
            AIPlanner.AddEvent($"자유 시간 시작({Trim(why, 60)})");
        }
        else if (Goal.kind == GoalKind.Free)
        {
            string why = AIPlanner.FreeTimeReason(now);
            if (why != null) S._freeWhy = why;
        }
    }

    // ------------------------------------------------------------------ goal command

    public static void HandleGoalCommand(World server, Entity player, string[] parts)
    {
        float now = Time.realtimeSinceStartup;
        if (parts.Length < 3)
        {
            AIPlayerSpikeMod.Reply(StatusText(server, player));
            return;
        }
        string a = parts[2].ToLowerInvariant();
        if (AIGoals.IsNoGoalWord(a))
        {
            ClearGoal(new GoalEnd("/ai goal 없음", "cancelled"));
            AIPlanner.Disable("/ai goal 없음");
            AIBody.Stop(server, player, now, "목표 해제", true);
            AIPlayerSpikeMod.Reply("AI 목표 해제: 제자리에 섭니다. 판단 호출 없음.");
            return;
        }
        if (a == "자세히" || a == "verbose")
        {
            Verbose = !Verbose;
            AIPlayerSpikeMod.Reply($"AI 판단 알림 자세히: {(Verbose ? "켬" : "끔")}");
            return;
        }
        if (KeyMissing())
        {
            AIPlayerSpikeMod.Reply("AI: OPENAI_API_KEY 를 찾지 못했습니다. 키를 저장한 뒤 게임을 다시 켜 주세요.");
            return;
        }
        if (a == "알아서" || a == "auto")
        {
            AIPlanner.Enable(server, player);
            return;
        }
        GoalKind kind;
        ObjectID item = ObjectID.None; string nm = ""; int n = 1;
        if (InventoryHostKind(a, out var inventoryKind))
        {
            if (parts.Length < 4 || (!string.Equals(parts[3], "none", StringComparison.OrdinalIgnoreCase) && !TryItem(parts[3], out item, out nm)))
            { AIPlayerSpikeMod.Reply("사용법: /ai goal equip|unequip|hold|take|drop|give <ObjectID|none> [count=N] [where=대상] [slot=N]"); return; }
            if (!InventoryArgs(parts, 4, out n, out int keep, out int slot, out string destination, out string error)) { AIPlayerSpikeMod.Reply(error); return; }
            if (n < 0) n = 1;
            string form = AIInventoryGoalForm.Check(new PlanStep(AIGoalKinds.Of(inventoryKind).WireName, item == ObjectID.None ? null : item.ToString(), n, destination, keep, slot));
            if (form != null) { AIPlayerSpikeMod.Reply("가방·장비: " + form); return; }
            string bad = AIInventory.CheckGoal(server, player, inventoryKind, item, n, destination, keep, slot);
            if (bad != null) { AIPlayerSpikeMod.Reply("가방·장비: " + bad); return; }
            SetGoal(server, player, inventoryKind, item, nm, n, "host", destination, keep, slot);
            AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}.", HostGoalKind); return;
        }
        if ((AIGoalKinds.IsHostWord(GoalKind.Craft, a)) && parts.Length >= 4)
        {
            if (!TryItem(parts[3], out item, out nm)) { AIPlayerSpikeMod.Reply("사용법: /ai goal 만들기 <ObjectID> [N]"); return; }
            if (parts.Length >= 5 && !int.TryParse(parts[4].TrimEnd('개'), out n)) n = 1;
            SetGoal(server, player, GoalKind.Craft, item, nm, n, "host");
            AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}. OpenAI Decisions로 행동을 고릅니다.", HostGoalKind);
            return;
        }
        else if ((AIGoalKinds.IsHostWord(GoalKind.Goto, a)) && parts.Length >= 4)
        {
            // 제작대-놓기 시험 명령: /ai goal goto <장소>
            string bad = AIPlaceGoal.CheckGoto(server, player, parts[3]);
            if (bad != null) { AIPlayerSpikeMod.Reply($"AI goto: {bad}"); return; }
            SetGoal(server, player, GoalKind.Goto, ObjectID.None, "", 1, "host", parts[3]);
            AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}.", HostGoalKind);
            return;
        }
        else if ((AIGoalKinds.IsHostWord(GoalKind.Place, a)) && parts.Length >= 4)
        {
            // /ai goal place <ObjectID> [장소] [N]
            if (!TryItem(parts[3], out item, out nm)) { AIPlayerSpikeMod.Reply("사용법: /ai goal place <ObjectID> [장소] [N]"); return; }
            string wh = parts.Length >= 5 ? parts[4] : "base";
            if (parts.Length >= 6 && !int.TryParse(parts[5].TrimEnd('개'), out n)) n = 1;
            string bad = AIPlaceGoal.CheckPlace(server, player, parts[3], n, wh);
            if (bad != null) { AIPlayerSpikeMod.Reply($"AI place: {bad}"); return; }
            SetGoal(server, player, GoalKind.Place, item, nm, n, "host", wh);
            AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}.", HostGoalKind);
            return;
        }
        else if (AIGoalKinds.IsHostWord(GoalKind.Explore, a)) kind = GoalKind.Explore;
        else if (AIGoalKinds.IsHostWord(GoalKind.Follow, a)) kind = GoalKind.Follow;
        else if (AIGoalKinds.IsHostWord(GoalKind.Home, a)) kind = GoalKind.Home;
        else if (AIGoalKinds.IsHostWord(GoalKind.Farm, a)) { kind = GoalKind.Farm; n = 0; }
        else if (AIGoalKinds.IsHostWord(GoalKind.Cook, a)) { kind = GoalKind.Cook; n = 0; }
        else if (AIGoalKinds.IsHostWord(GoalKind.Store, a))
        {
            kind = GoalKind.Store;
            n = 0; // no count on the legacy store command means the existing generic bag tidy
            if (parts.Length >= 4 && !TryItem(parts[3], out item, out nm)) { AIPlayerSpikeMod.Reply("사용법: /ai goal 보관 [ObjectID]"); return; }
            if (parts.Length >= 4)
            {
                if (!InventoryArgs(parts, 4, out n, out int keep, out int slot, out string destination, out string error)) { AIPlayerSpikeMod.Reply(error); return; }
                string form = AIInventoryGoalForm.Check(new PlanStep("store", item.ToString(), n, destination, keep, slot));
                if (form != null) { AIPlayerSpikeMod.Reply("가방·장비: " + form); return; }
                string bad = AIInventory.CheckGoal(server, player, kind, item, n, destination, keep, slot);
                if (bad != null) { AIPlayerSpikeMod.Reply("가방·장비: " + bad); return; }
                SetGoal(server, player, kind, item, nm, n, "host", destination, keep, slot);
                AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}.", HostGoalKind); return;
            }
        }
        else
        {
            if (!TryItem(parts[2], out item, out nm))
            {
                AIPlayerSpikeMod.Reply("사용법: /ai goal 흙 5 · 구리 3 · 주석 3 · 나무 2 · <ObjectID> N · 탐험 · 따라와 · 보관 [ObjectID] · 기지로 · 농사 · 요리 · goto <장소> · place <ObjectID> [장소] [N] · 알아서 · 없음 · 자세히");
                return;
            }
            if (parts.Length >= 4 && !int.TryParse(parts[3].TrimEnd('개'), out n)) n = 1;
            kind = GoalKind.Gather;
        }
        SetGoal(server, player, kind, item, nm, n, "host");
        AIPlayerSpikeMod.Reply($"AI 목표: {Goal.Text}. OpenAI Decisions로 행동을 고릅니다. /ai goal 로 진행 보기, /ai stop 으로 해제.", HostGoalKind);
    }

    // The one way a goal is set: the host's /ai goal and the upper LLM (AIPlanner) both come here.
    // source: "host" or "llm:<member>#<report>". A host goal also drops any upper-LLM answer still on the way
    // (Revision changes, AIPlanner checks it).
    public static void SetGoal(World server, Entity player, GoalKind kind, ObjectID item, string itemName, int count, string source) => SetGoal(server, player, kind, item, itemName, count, source, null);

    public static void SetGoal(World server, Entity player, GoalKind kind, ObjectID item, string itemName, int count, string source, string where)
        => SetGoal(server, player, kind, item, itemName, count, source, where, -1, -1);

    public static void SetGoal(World server, Entity player, GoalKind kind, ObjectID item, string itemName, int count, string source, string where, int keep, int slot, string mode = null, string second = null, int maxCount = -1, int maxOrder = -1)
    {
        float now = Time.realtimeSinceStartup;
        var g = new AIGoal { startTime = now, kind = kind, source = source, mode = AILifeGoalForm.Text(mode) ?? "", second = AILifeGoalForm.Text(second) ?? "" };
        g.maxCount = kind == GoalKind.Gather ? maxCount : -1;
        g.maxOrder = maxOrder;
        if (kind == GoalKind.Gather) g.where = AILifeGoalForm.Text(where) ?? "";
        if (kind == GoalKind.Cook && g.second != "" && TryItemPublic(g.second, out ObjectID secondId, out _)) g.second = secondId.ToString();
        if (kind == GoalKind.Gather || kind == GoalKind.Craft || kind == GoalKind.Place)
        {
            g.item = item; g.itemName = itemName; g.count = math.max(1, count);
            AIBody.InventoryCounts(server, player).TryGetValue(item, out g.startCount);
            if (kind == GoalKind.Craft) g.startCount = AICraft.MainCount(server, player, item);
        }
        if (kind == GoalKind.Farm || kind == GoalKind.Cook)
        { g.item = item; g.itemName = itemName; g.count = math.max(0, count); g.where = AILifeGoalForm.Text(where) ?? ""; }
        if (kind == GoalKind.Maintenance)
        { g.item = item; g.itemName = itemName; g.count = AIMaintenanceRules.CountFor(count); g.where = AILifeGoalForm.Text(where) ?? ""; g.slot = slot; }
        if (kind == GoalKind.Craft) g.where = AILifeGoalForm.Text(where) ?? "";
        // 제작대-놓기: the place of a goto / place goal (no where = base for place); a shape's count is its tiles
        if (kind == GoalKind.Goto || kind == GoalKind.Place) { g.where = AIPlaces.Parse(where, out var pr, out _) ? pr.ToString() : (where ?? ""); AIPlaceGoal.OnGoalSet(server, player, g, pr); }
        if (kind == GoalKind.Store) { g.item = item; g.itemName = itemName; }   // optional: store only this kind
        if (AIGoalKinds.IsInventory(kind) || kind == GoalKind.Store)
        { g.item = item; g.itemName = itemName; g.count = math.max(0, count); g.where = where ?? ""; g.keep = keep; g.slot = slot; }
        if (kind == GoalKind.Store && g.item == ObjectID.None && slot >= 0)
        {
            g.item = AIBody.SlotItem(server, player, slot, out _);
            if (g.item != ObjectID.None) g.itemName = g.item.ToString(); // the same explicit source also drives chest preparation
        }
        if (Goal.kind == GoalKind.Free) AIPlayerSpikeMod.Log($"GOAL free time ends after {now - Goal.startTime:F0}s: a goal came ({source})");
        AIExperience.GoalStarting(server, player, Goal.IsReal ? Goal : new AIGoal(), g);   // experience memory: a new goal run (the running one is replaced; free time is none)
        AIExpedition.Observe(server, player); // flush the old work's last observed acquisition before replacing its identity
        Goal = g;
        S.Completion = S.Blocked = "";
        SetBlockNote("", false);   // blocks seen for the previous goal are not this goal's (a stale "도구 부족" added a pick step)
        _sd = false; // a goal replaces a self-defence episode
        Revision++; S.GoalRevision++;
        AILife.OnGoalChanged();
        if (AIInventory.OwnsGoal(g)) AIInventory.GoalSet(server, player, g);
        AIBrain.CancelPendingChoices("new goal");
        _needCall = true;
        _needReason = "새 목표";
        _hpAtLastCall = -1;
        AIPlayerSpikeMod.Log($"GOAL set rev={Revision} source={source}: {g.Text} (start count {g.startCount})");
        if (GoalSource.IsHost(source)) AIPlanner.OnHostGoal(server, player, g.Text);
    }

    // The /ai goal confirmation line (합의 4): the host goal is reported to the upper LLM (host_goal, whatever the mode) and
    // it answers with its own say, so the program line is a status line (shown with /ai verbose).
    private static ChatKind HostGoalKind => ChatKind.Status;   // a host goal is always reported to the upper LLM now (watching)

    private static bool InventoryHostKind(string word, out GoalKind kind)
    {
        foreach (var info in AIGoalKinds.All)
            if (AIGoalKinds.IsInventory(info.Kind) && AIGoalKinds.IsHostWord(info.Kind, word)) { kind = info.Kind; return true; }
        kind = GoalKind.None; return false;
    }

    private static bool InventoryArgs(string[] parts, int first, out int count, out int keep, out int slot, out string where, out string error)
    {
        count = -1; keep = slot = -1; where = null; error = null;
        for (int i = first; i < parts.Length; i++)
        {
            string arg = parts[i];
            if (arg.StartsWith("where=")) { where = arg.Substring(6); continue; }
            string key = arg.StartsWith("count=") ? "count" : arg.StartsWith("keep=") ? "keep" : arg.StartsWith("slot=") ? "slot" : null;
            string value = key == null ? arg.TrimEnd('개') : arg.Substring(key.Length + 1);
            if (!int.TryParse(value, out int n) || n < 0 || n > int.MaxValue) { error = "가방·장비: 수량과 슬롯은 0..2147483647 정수로 지정하세요(count=N, keep=N, slot=N)."; return false; }
            if (key == "keep") keep = n; else if (key == "slot") slot = n; else count = n;
        }
        return true;
    }

    // Item name (Korean alias or ObjectID name) -> ObjectID, for the upper LLM's goals too.
    public static bool TryItemPublic(string s, out ObjectID id, out string name) => TryItem(s, out id, out name);

    private static bool TryItem(string s, out ObjectID id, out string name)
    {
        name = s;
        switch (s.ToLowerInvariant())
        {
            case "흙": case "dirt": id = ObjectID.WallDirtBlock; name = "흙벽 블록"; return true;
            case "구리": case "copper": id = ObjectID.CopperOre; name = "구리 광석"; return true;
            case "주석": case "tin": id = ObjectID.TinOre; name = "주석 광석"; return true;
            case "나무": case "wood": id = ObjectID.Wood; name = "나무"; return true;
        }
        return Enum.TryParse(s, true, out id) && id != ObjectID.None;
    }

    // Observations wake the model without clearing its goal or selected action.
    public static bool CheckDone(World server, Entity player, float now)
    {
        if (!Goal.IsReal || now - _lastGoalCheck < 0.5f) return false;
        _lastGoalCheck = now;
        string observed = "";
        var completion = AIGoalKinds.Of(Goal.kind)?.Completion;
        if (completion == GoalCompletion.FollowTimeout && now - Goal.startTime >= 180f)
            observed = "따르기를 3분 이상 이어 감. 계속 따를지 직접 결정할 수 있음";
        else if (completion == GoalCompletion.Feature && AIGoalFeatures.TryDone(server, player, Goal.kind, out string done))
            observed = done;
        else if (completion == GoalCompletion.Inventory)
        {
            AIBody.InventoryCounts(server, player).TryGetValue(Goal.item, out int count);
            if (Goal.kind == GoalKind.Craft) count = AICraft.MainCount(server, player, Goal.item);
            long received = Goal.kind == GoalKind.Gather ? AIExpedition.Gathered(Goal, count - Goal.startCount) : count - Goal.startCount;
            if (received >= Goal.count)
                observed = $"요청 수량 확보 ({received}/{Goal.count}). 추가 작업 또는 단계 완료를 직접 선택";
        }
        if (S.Completion != observed)
        {
            S.Completion = observed;
            if (observed != "") AIBrain.ObserveExecution("목표 조건 관측: " + observed);
        }
        if (AIInventory.OwnsGoal(Goal) && observed != "") Goal.note = observed;
        return false;
    }

    public static bool CheckAfterBuild(World server, Entity player, float now, GoalBuildFacts facts)
    {
        string blocked = "";
        bool inventoryDone = AIInventory.OwnsGoal(Goal) && AIInventory.GoalDone(server, player, out _);
        if (Goal.IsReal && !inventoryDone && !facts.InventorySelectionPending && !facts.InCombat && !facts.FrontierPending && !facts.HasProgress)
        {
            if (AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Feature)
                blocked = AIGoalFeatures.StallNote(server, player, Goal.kind);
            else if (AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Inventory)
                blocked = $"목표 진척 후보 없음. {facts.GatherFacts} {facts.PrepNote} {facts.BlockNote}";
            else if (Goal.kind == GoalKind.Explore && facts.ExploreOptions == 0)
                blocked = $"새 탐색 후보 없음. {facts.ExploreFrontierNote} {facts.BlockNote}";
        }
        if (S.Blocked != blocked)
        {
            S.Blocked = blocked;
            if (blocked != "") AIBrain.ObserveExecution(blocked);
        }
        if (AIInventory.OwnsGoal(Goal) && blocked != "" && !AIInventory.GoalDone(server, player, out _)) Goal.note = blocked;
        return false;
    }

    public static void FinishChosen(World server, Entity player, float now)
    {
        if (!Goal.IsReal) return;
        string text = Goal.Text, source = Goal.source;
        string detail = CompletionObserved ? S.Completion : "행동 판단기가 현재 단계를 완료로 선택";
        EndGoal(server, player, now, new GoalEnd("AI chose step completion", "completed"),
            EndOrder.ClearStopReport, "AI가 단계 완료를 선택", "goal_done", text, detail, source);
    }

    // Explicit step completion clears its record, stops the chosen body run and advances/reports the plan.
    public static void EndGoal(World server, Entity player, float now, GoalEnd e, EndOrder order, string stopReason, string trigger, string text, string reportWhy, string source)
    {
        ClearGoal(e);
        if (order == EndOrder.ClearStopReport)
        {
            AIBody.Stop(server, player, now, stopReason, true);
            AIPlanner.OnGoalEnded(server, player, trigger, text, reportWhy, source);
        }
        else
        {
            AIPlanner.OnGoalEnded(server, player, trigger, text, reportWhy, source);
            AIBody.Stop(server, player, now, stopReason, true);
        }
    }
}

public enum EndOrder { ClearStopReport, ClearReportStop }

// Who set a goal: the AIGoal.source string as written ("host", "llm:<member>#<report>", "free"). The rules are the ones
// the C# checks used (refactor S3); the experience record (AIExpCore) keeps its own copy because exp-sim links it alone.
public static class GoalSource
{
    public static bool IsHost(string source) => source == "host";
    public static bool IsLlm(string source) => (source ?? "").StartsWith("llm");
}

public struct GoalBuildFacts
{
    public bool InventorySelectionPending; // navigating a choice is not an observed execution blockage or completion
    public bool HasProgress, InCombat, FrontierPending;
    public int ExploreOptions, ExploreNoPath;
    public string ExploreFrontierNote, SkippedNote, PrepNote, GatherFacts, BlockNote;
    public float FailMemorySeconds;
}
