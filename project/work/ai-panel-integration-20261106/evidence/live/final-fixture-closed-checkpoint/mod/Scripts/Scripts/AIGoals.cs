// AIGoals — the goal a character works on and its life: set (host /ai goal, upper LLM), clear, free time, the done
// checks by observation, the goal command (refactor plan 2-계획.md S3). Moved out of AIBrain with the code text kept;
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
        // free time (no goal, upper LLM unavailable; 2026-11-04 막힘-멈춤-정리 item 1)
        public float _freeCheck; public string _freeWhy = "";
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Goals);
    private static State S => StateOf(AIAgent.Cur);

    public static AIGoal Goal { get => S.Goal; set => S.Goal = value; }
    // Changes only when the goal is set or cleared (see AIBrain.GoalRevision).
    public static int GoalRevision => S.GoalRevision;
    public static string FreeWhy => S._freeWhy;
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
        if (Goal.kind == GoalKind.None) return;
        AIPlayerSpikeMod.Log($"GOAL cleared ({why}): was '{Goal.Text}' rev={Revision}");
        bool real = Goal.IsReal;
        if (real) AIExperience.GoalEnding(Goal, e);   // experience memory: this goal run ends (AIExperience.cs); free time is no goal run
        Goal = new AIGoal();
        // free time is not a goal anyone set: ending it does not make an upper-LLM answer on the way stale
        Revision++; if (real) S.GoalRevision++;
        AbortRequest("goal cleared");
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
            if (why != null) { S._freeWhy = why; return; }
            if (AIPlanner.KeepsFreeTime) return;   // a report is on the way or due: a goal may still come
            string t = $"{now - Goal.startTime:F0}s";
            AIPlayerSpikeMod.Log($"GOAL free time ends after {t}: the upper LLM is available and asks nothing now (e.g. it answered hold, or auto is off)");
            ClearGoal(new GoalEnd("free time over", "unknown"));
            AIBody.Stop(server, player, now, "자유 시간 끝");
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
            AIBody.Stop(server, player, now, "목표 해제");
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
        else if (AIGoalKinds.IsHostWord(GoalKind.Farm, a)) kind = GoalKind.Farm;
        else if (AIGoalKinds.IsHostWord(GoalKind.Cook, a)) kind = GoalKind.Cook;
        else if (AIGoalKinds.IsHostWord(GoalKind.Store, a))
        {
            kind = GoalKind.Store;
            if (parts.Length >= 4 && !TryItem(parts[3], out item, out nm)) { AIPlayerSpikeMod.Reply("사용법: /ai goal 보관 [ObjectID]"); return; }
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
    {
        float now = Time.realtimeSinceStartup;
        var g = new AIGoal { startTime = now, kind = kind, source = source };
        if (kind == GoalKind.Gather || kind == GoalKind.Craft || kind == GoalKind.Place)
        {
            g.item = item; g.itemName = itemName; g.count = math.max(1, count);
            AIBody.InventoryCounts(server, player).TryGetValue(item, out g.startCount);
        }
        // 제작대-놓기: the place of a goto / place goal (no where = base for place); a shape's count is its tiles
        if (kind == GoalKind.Goto || kind == GoalKind.Place) { g.where = AIPlaces.Parse(where, out var pr, out _) ? pr.ToString() : (where ?? ""); AIPlaceGoal.OnGoalSet(server, player, g, pr); }
        if (kind == GoalKind.Store) { g.item = item; g.itemName = itemName; }   // optional: store only this kind
        if (Goal.kind == GoalKind.Free) AIPlayerSpikeMod.Log($"GOAL free time ends after {now - Goal.startTime:F0}s: a goal came ({source})");
        AIExperience.GoalStarting(server, player, Goal.IsReal ? Goal : new AIGoal(), g);   // experience memory: a new goal run (the running one is replaced; free time is none)
        Goal = g;
        SetBlockNote("", false);   // blocks seen for the previous goal are not this goal's (a stale "도구 부족" added a pick step)
        _sd = false; // a goal replaces a self-defence episode
        Revision++; S.GoalRevision++;
        AbortRequest("new goal");
        _needCall = true;
        _needReason = "새 목표";
        _hpAtLastCall = -1;
        AIPlayerSpikeMod.Log($"GOAL set rev={Revision} source={source}: {g.Text} (start count {g.startCount})");
        if (GoalSource.IsHost(source)) AIPlanner.OnHostGoal(server, player, g.Text);
    }

    // The /ai goal confirmation line (합의 4): the host goal is reported to the upper LLM (host_goal, whatever the mode) and
    // it answers with its own say, so the program line is a status line (shown with /ai verbose).
    private static ChatKind HostGoalKind => ChatKind.Status;   // a host goal is always reported to the upper LLM now (watching)

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

    // Goal done by observation (was in AIBrain.Tick; refactor S3). true: the goal ended, the tick stops here.
    public static bool CheckDone(World server, Entity player, float now)
    {
        // An upper-LLM follow goal has no natural end: after 3 minutes it counts as done so the LLM decides again
        // (first slice; the draft's "recheck" comes later). Host follow goals are not touched.
        if (AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.FollowTimeout && GoalSource.IsLlm(Goal.source) && now - Goal.startTime >= 180f)
        {
            string t = Goal.Text, src = Goal.source;
            AIPlayerSpikeMod.Log($"GOAL done rev={Revision}: {t} (3 minutes, upper-LLM follow) after {now - Goal.startTime:F1}s");
            EndGoal(server, player, now, new GoalEnd("goal done", "completed"), EndOrder.ClearStopReport, "목표 완료", "goal_done", t, "3분 따라감", src);
            return true;
        }

        // Feature completion keeps the original observation cadence and end order.
        if ((AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Feature) && (now - _lastGoalCheck >= 0.5f || AIBody.Current == null || AIBody.Current.Status != SkillStatus.Running))
        {
            _lastGoalCheck = now;
            string doneB = "";
            if (AIGoalFeatures.TryDone(server, player, Goal.kind, out doneB))
            {
                string t = Goal.Text, src = Goal.source;
                AIPlayerSpikeMod.Log($"GOAL done rev={Revision}: {t} ({doneB}) after {now - Goal.startTime:F1}s, calls this session {Calls}");
                AIPlayerSpikeMod.Reply($"AI 목표 완료: {t} ({doneB}, {now - Goal.startTime:F0}초). 제자리에 섭니다.", ChatKind.Status);
                EndGoal(server, player, now, new GoalEnd("goal done", "completed"), EndOrder.ClearReportStop, "목표 완료", "goal_done", t, doneB, src);
                return true;
            }
        }

        // goal completion (observed inventory)
        if ((AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Inventory) && (now - _lastGoalCheck >= 0.5f || AIBody.Current == null || AIBody.Current.Status != SkillStatus.Running))   // also right after a skill ends (a 2nd craft was sent 0.2 s after the 1st finished)
        {
            _lastGoalCheck = now;
            AIBody.InventoryCounts(server, player).TryGetValue(Goal.item, out int c);
            if (c < Goal.startCount)
            {
                // fewer than at the start (lost on a down, used up, stored): the goal is "N more than at the start";
                // counting from the lower number keeps it N, not N + what was lost (a Furnace 1 goal made 3 after a
                // down dropped 2, ai2 2026-11-04)
                AIPlayerSpikeMod.Log($"GOAL rev={Revision}: {Goal.item} count fell below the start ({Goal.startCount} -> {c}); counting from {c}");
                Goal.startCount = c;
            }
            if (c - Goal.startCount >= Goal.count)
            {
                string t = Goal.Text;
                AIPlayerSpikeMod.Log($"GOAL done rev={Revision}: {t} ({c - Goal.startCount}/{Goal.count}) after {now - Goal.startTime:F1}s, calls this session {Calls}");
                AIPlayerSpikeMod.Reply($"AI 목표 완료: {t} ({c - Goal.startCount}/{Goal.count}, {now - Goal.startTime:F0}초, 호출 {Calls}회). 제자리에 섭니다.", ChatKind.Status);
                string src = Goal.source, doneText = $"완료 ({c - Goal.startCount}/{Goal.count})";   // before ClearGoal (it was "1/0" for the upper LLM)
                EndGoal(server, player, now, new GoalEnd("goal done", "completed"), EndOrder.ClearReportStop, "목표 완료", "goal_done", t, doneText, src);
                return true;
            }
        }
        return false;
    }

    // Build facts are consumed now, not retained as a cache.
    public static bool CheckAfterBuild(World server, Entity player, float now, GoalBuildFacts facts)
    {
        if ((AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Feature) && !facts.HasProgress && !facts.InCombat
            && !(AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running))
        {
            // store / home (AIBase): no way to reach base, no chest with room and none to make or place; farm (AIFarm):
            // no seed, no water, no tool that can be made, nothing ripe
            string why = AIGoalFeatures.StallNote(server, player, Goal.kind);
            AIPlayerSpikeMod.Log($"GOAL stalled rev={Revision}: {Goal.Text}: {why}");
            AIPlayerSpikeMod.Reply($"AI 목표 멈춤: {Goal.Text}. {Trim(why, 220)}", ChatKind.Status);
            string srcB = Goal.source, txtB = Goal.Text;
            EndGoal(server, player, now, new GoalEnd("stalled: " + why, "blocked", Goal.kind == GoalKind.Home || Goal.kind == GoalKind.Store ? "no_place" : null), EndOrder.ClearStopReport, "목표 진행 불가", "goal_stalled", txtB, why, srcB);   // farm / cook: the note decides
            return true;
        }
        // not while the remembered-frontier search still runs in the background: a gather goal with nothing in view
        // ended "탐험할 새 구역 없음" with the search at 0 tiles, and the search found 4 frontiers 0.6 s later
        // (2026-11-04 넷이 함께 플레이, Claude Wood 10). The explore end check already waits for it; the brain asks
        // again when the search is done.
        if ((AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Inventory) && !facts.HasProgress && !facts.InCombat && !facts.FrontierPending)
        {
            // Nothing that can move the gather goal forward: stop with the observed reasons instead of waiting forever.
            AIBody.InventoryCounts(server, player).TryGetValue(Goal.item, out int have);
            // A craft goal whose way (walk to a remembered station, a chest) was left out after two failures reads "만들
            // 방법이 없음 — … 아는 길 65칸" although the way exists and it was the walk that failed (2026-11-04 memory
            // test: stuck twice at (-61,52) on the way to a remembered Furnace 45 tiles away). Say what was left out.
            string leftOut = string.IsNullOrEmpty(facts.SkippedNote) ? "" : $"되풀이 실패로 {facts.FailMemorySeconds:F0}초 동안 뺀 행동: {Trim(facts.SkippedNote, 260)} / ";
            // The gather text was fixed ("보이는 목표 품목 벽 0개 … 탐험할 새 구역 없음") and said "nothing new to explore"
            // also when exploring was never looked at (visible goal options left out or tool-short). Now it lists what
            // this build looked at (facts.GatherFacts, BuildCandidates).
            string why = Goal.kind == GoalKind.Craft ? $"만들 방법이 없음 — {leftOut}{facts.PrepNote}{(string.IsNullOrEmpty(facts.GatherFacts) ? "" : " · 재료 모으기: " + facts.GatherFacts)}" : $"모을 방법이 없음 — {leftOut}{(string.IsNullOrEmpty(facts.GatherFacts) ? "목표 일을 할 후보 없음" : facts.GatherFacts)}{(string.IsNullOrEmpty(facts.BlockNote) ? "" : " · " + facts.BlockNote)}";
            AIPlayerSpikeMod.Log($"GOAL stalled rev={Revision}: {Goal.Text} ({have - Goal.startCount}/{Goal.count}): {why}");
            AIPlayerSpikeMod.Reply($"AI 목표 멈춤: {Goal.Text} ({have - Goal.startCount}/{Goal.count}). {Trim(why, 220)}", ChatKind.Status);
            string srcS = Goal.source, txtS = Goal.Text;
            EndGoal(server, player, now, new GoalEnd("stalled: " + why, "blocked", Goal.kind == GoalKind.Craft ? "cannot_craft" : null), EndOrder.ClearStopReport, "목표 진행 불가", "goal_stalled", txtS, why, srcS);   // gather: the note decides (tool_weak / no_source)
            return true;
        }
        if (Goal.kind == GoalKind.Explore && facts.ExploreOptions == 0 && !facts.InCombat && !facts.FrontierPending && !(AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running && (AIBody.Current is TunnelSkill || AIBody.Current is GotoSkill)))
        {
            // Nothing left to explore (no walk or dig target that would show a not-yet-seen cell): end the goal
            // (user decision 2026-11-04) instead of offering filler actions.
            string why = $"걷거나 파서 새 구역이 보일 곳이 없음 (본 구역 {AIMemory.SeenCount}칸{(facts.ExploreNoPath > 0 ? $", 길이 없어 잠시 뺀 걷기 목표 {facts.ExploreNoPath}곳" : "")}{(string.IsNullOrEmpty(facts.ExploreFrontierNote) ? "" : ", 기억한 탐색 끝 찾기: " + facts.ExploreFrontierNote)}{(string.IsNullOrEmpty(facts.BlockNote) ? "" : " · " + Trim(facts.BlockNote, 100))})";
            AIPlayerSpikeMod.Log($"GOAL ended rev={Revision}: {Goal.Text}: nothing left to explore: {why}");
            AIPlayerSpikeMod.Reply($"AI 탐험 끝: {why}. 제자리에 섭니다.", ChatKind.Status);
            string srcE = Goal.source, txtE = Goal.Text;
            EndGoal(server, player, now, new GoalEnd("nothing left to explore", "exhausted", ""), EndOrder.ClearReportStop, "탐험할 곳 없음", "explore_ended", txtE, why, srcE);
            return true;
        }
        return false;
    }

    // The one way a goal ends with a report to the upper LLM (refactor S3): clear (experience record), then the body stop
    // and the planner report in the order each ending place always used (ClearStopReport: LLM follow done, the stalls,
    // only-wait; ClearReportStop: store/home/farm/cook done, gather/craft done, explore ended). Unifying the order is a
    // later, separate change (HEAD decision).
    public static void EndGoal(World server, Entity player, float now, GoalEnd e, EndOrder order, string stopReason, string trigger, string text, string reportWhy, string source)
    {
        ClearGoal(e);
        if (order == EndOrder.ClearStopReport)
        {
            AIBody.Stop(server, player, now, stopReason);
            AIPlanner.OnGoalEnded(server, player, trigger, text, reportWhy, source);
        }
        else
        {
            AIPlanner.OnGoalEnded(server, player, trigger, text, reportWhy, source);
            AIBody.Stop(server, player, now, stopReason);
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
    public bool HasProgress, InCombat, FrontierPending;
    public int ExploreOptions, ExploreNoPath;
    public string ExploreFrontierNote, SkippedNote, PrepNote, GatherFacts, BlockNote;
    public float FailMemorySeconds;
}
