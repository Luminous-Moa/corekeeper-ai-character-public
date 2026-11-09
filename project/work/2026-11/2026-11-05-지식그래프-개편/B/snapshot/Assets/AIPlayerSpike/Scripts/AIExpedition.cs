using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

// Runtime glue for choices and observed expedition facts. No autonomous task/quantity policy.
public static class AIExpedition
{
    private sealed class WorkRecord
    {
        public AIGoal Goal;
        public long Start, Acquired;
        public string Location, Selection;
        public string Text => Goal.Text + "; 출처 " + Goal.source + "; 실제 누적 " + Acquired
            + "; 목표 부족 " + Math.Max(0, Goal.count - Acquired) + "; 작업 장소 " + Location
            + "; 선택 대상 " + Selection + ". 재개 여부·남은 목표는 상위 판단, 현재 상태 재확인 필요";
    }
    private sealed class State
    {
        public string World = "";
        public AIGoal Goal;
        public readonly ExpeditionLedger Ledger = new ExpeditionLedger();
        public ExpeditionCursor Cursor = new ExpeditionCursor();
        public readonly Dictionary<string, ExpeditionNav> Offered = new Dictionary<string, ExpeditionNav>();
        public readonly List<string> Opportunities = new List<string>();
        public readonly List<string> OpportunityDescriptions = new List<string>();
        public readonly Dictionary<AIGoal, long> GoalStarts = new Dictionary<AIGoal, long>();
        public AICandidate Current, Suspended;
        public AISkill Skill;
        public World Server; public Entity Player;
        public float SideStarted = -1, SideSeconds;
        public string LastResult = "", ReturnContext = "", LastLocation = "", UpperGuidance = "", LimitFacts = "";
        public readonly Dictionary<AIGoal, WorkRecord> Work = new Dictionary<AIGoal, WorkRecord>();
        public readonly Dictionary<string, ExpeditionLimit> Limits = new Dictionary<string, ExpeditionLimit>();
        public readonly Dictionary<string, long> OrderAcquired = new Dictionary<string, long>();
        public readonly HashSet<int> BodyOwners = new HashSet<int>();
        public string LimitOwnerError = "";
        public int BrowseTargets;
        public readonly TravelResourcePacer Pacer = new TravelResourcePacer();
        public List<AICandidate> LastChoices = new List<AICandidate>();   // 토큰 절감 ①: the choices the last build offered
        public bool FieldPaused;   // the last choices came from the chest open mode (no field build)
        public readonly HashSet<string> CatalogIds = new HashSet<string>(StringComparer.Ordinal);   // 재검수 R6: resources in the browse catalog
        // The upper LLM's valuable-kind changes (user decision 2026-11-08): item -> valuable or not; kept until changed.
        public readonly Dictionary<string, bool> Valuable = new Dictionary<string, bool>(StringComparer.Ordinal);
    }
    private static State Sync()
    {
        var s = AIAgent.Cur.Get<State>(AISlot.Expedition);
        if (s.World != AIPlayerSpikeMod.WorldId)
        { s = new State { World = AIPlayerSpikeMod.WorldId }; AIAgent.Cur.Slots[AISlot.Expedition] = s; LoadValuable(s); }
        if (!ReferenceEquals(s.Goal, AIGoals.Goal))
        {
            if (s.Goal != null && s.Work.TryGetValue(s.Goal, out var previous))
            {
                previous.Acquired = s.Ledger.Acquired(s.Goal.item.ToString()) - previous.Start;
            }
            s.Goal = AIGoals.Goal; s.Cursor = new ExpeditionCursor(); s.Offered.Clear(); s.Suspended = null; s.BrowseTargets = 0;
            if (!s.GoalStarts.ContainsKey(s.Goal)) s.GoalStarts[s.Goal] = s.Ledger.Acquired(s.Goal.item.ToString());
            if (s.Goal.kind == GoalKind.Gather && !s.Work.ContainsKey(s.Goal))
                s.Work[s.Goal] = new WorkRecord { Goal = s.Goal, Start = s.GoalStarts[s.Goal], Location = s.Goal.where, Selection = "아직 선택하지 않음" };
            s.LimitOwnerError = "";
            if (s.Goal.maxCount >= 0)
            {
                var owners = AIPlanner.Cur.Orders.Carried(s.Goal.source);
                var owner = owners.Find(o => o.Id == s.Goal.maxOrder);
                if (s.Goal.maxOrder < 0 && owners.Count == 1) owner = owners[0];
                if (owner == null) s.LimitOwnerError = "명시 상한의 원래 부탁을 확정하지 못함. max_order와 따르는 원부탁을 상위가 다시 지정해야 함";
                else
                {
                    string key = owner.Id + "|" + s.Goal.item;
                    if (s.Limits.TryGetValue(key, out var old) && old.Maximum != s.Goal.maxCount)
                        s.LimitOwnerError = "같은 원부탁의 사용자 명시 상한을 새 계획만으로 변경할 수 없음";
                    else if (old == null) s.Limits[key] = new ExpeditionLimit {OrderId=owner.Id, Item=s.Goal.item.ToString(), Maximum=s.Goal.maxCount};
                }
            }
        }
        return s;
    }
    private static bool Gather(AICandidate c) => c != null && (c.kind == "gather_group" || c.kind == "mine" || c.kind == "root" || c.kind == "break" || c.kind == "pickup" || c.kind == "edgepickup" || c.kind == "farm" && c.actionMode == "harvest");
    private static bool Receives(AICandidate c) => Gather(c) || c != null && (c.kind == "dig" || c.kind == "edgemat");
    private static bool Travel(AICandidate c) => c != null && (c.kind == "explore" || c.kind == "travel" || c.kind == "travel_turn" || c.kind == "dig" || c.kind == "retreat");
    private static bool MainWork(AICandidate c) => c != null && (c.progress || AIGoals.Goal.kind == GoalKind.Explore && Travel(c));
    public static bool Active => Sync().Cursor.Open;
    public static ExpeditionQuery BeginBuild() => new ExpeditionQuery(Sync().Cursor, AIAgent.Cur.Id, AIPlayerSpikeMod.WorldId, AIGoals.GoalRevision);
    public static void Observe(World server, Entity player)
    {
        var s = Sync(); s.Server = server; s.Player = player;
        var inv = new Dictionary<string, int>(); foreach (var kv in AIBody.InventoryCounts(server, player)) inv[kv.Key.ToString()] = kv.Value;
        var before = new Dictionary<string,long>(); foreach (var kv in inv) before[kv.Key] = s.Ledger.Acquired(kv.Key);
        s.Ledger.ObserveInventory(inv, s.Skill != null && Receives(s.Current));
        foreach (var kv in before)
        {
            long delta = s.Ledger.Acquired(kv.Key) - kv.Value;
            if (delta <= 0) continue;
            foreach (int owner in s.BodyOwners)
            { string key = owner + "|" + kv.Key; s.OrderAcquired.TryGetValue(key,out long prior); s.OrderAcquired[key] = prior + delta; }
        }
        if (s.Work.TryGetValue(s.Goal, out var work)) work.Acquired = s.Ledger.Acquired(s.Goal.item.ToString()) - work.Start;
    }
    public static long Gathered(AIGoal goal, int net)
    {
        var s = Sync();
        return Math.Max(Math.Max(0, net), s.Ledger.Acquired(goal.item.ToString()) - (s.GoalStarts.TryGetValue(goal, out long begin) ? begin : 0));
    }
    public static void EnforceExplicitLimit(World server, Entity player, float now)
    {
        var s = Sync();
        if (ActiveLimits().Count == 0 || !Receives(s.Current)
            || s.Skill == null || s.Skill != AIBody.Current || s.Skill.Status != SkillStatus.Running) return;
        string why = AIGathering.LimitReason(server, player, s.Current);
        if (why == null) return;
        AIBody.Stop(server, player, now, "사용자 명시 확보 조건: " + why);
        AIBrain.ObserveExecution("명시 확보 조건으로 추가 몸 입력 보류: " + why + ". 원래 목표는 유지하며 가능한 다른 행동·상위 조건 조정을 판단.");
    }
    public static string BindPlanLimits(List<PlanStep> steps, List<HostOrder> carry)
    {
        var s = Sync();
        var additions = new Dictionary<string,ExpeditionLimit>();
        for (int i=0; i<steps.Count; i++)
        {
            var step=steps[i]; if (step.MaxCount < 0) continue;
            HostOrder owner = step.MaxOrder > 0 ? carry.Find(o=>o.Id==step.MaxOrder && o.Open) : carry.Count==1 ? carry[0] : null;
            if (owner == null) return "명시 max_count의 원부탁이 모호함. 따르는 원부탁의 max_order를 지정해야 하며, 원부탁 없이 임의 상한을 만들 수 없음";
            if (!AIGoals.TryItemPublic(step.Item ?? "",out ObjectID item,out _)) return "명시 상한 품목을 확인하지 못함: "+step.Item;
            string key=owner.Id+"|"+item;
            s.Limits.TryGetValue(key,out var old); if (old==null) additions.TryGetValue(key,out old);
            if (old!=null && old.Maximum!=step.MaxCount)
                return "원부탁 #"+owner.Id+"의 사용자 상한 "+old.Maximum+"을 계획으로 변경할 수 없음. 사용자의 명시 조건 변경을 먼저 반영해야 함";
            if (old==null) additions[key]=new ExpeditionLimit {OrderId=owner.Id,Item=item.ToString(),Maximum=step.MaxCount};
            steps[i]=new PlanStep(step.Do,step.Item,step.Count,step.Where,step.Keep,step.Slot,step.Mode,step.Second,step.MaxCount,owner.Id);
        }
        foreach (var addition in additions) s.Limits[addition.Key]=addition.Value;
        return null;
    }
    public static IReadOnlyList<ExpeditionLimit> ActiveLimits()
    {
        var s=Sync(); var active=new List<ExpeditionLimit>();
        var owners=AIPlanner.Cur.Orders.Carried(s.Goal.source);
        foreach (var pair in s.Limits)
        {
            var limit=pair.Value; var owner=AIPlanner.Cur.Orders.Find(limit.OrderId);
            if (owner==null || !owner.Open) continue;
            if (!owners.Exists(o=>o.Id==limit.OrderId) && !(s.Skill!=null && s.Skill.Status==SkillStatus.Running && s.BodyOwners.Contains(limit.OrderId))) continue;
            s.OrderAcquired.TryGetValue(pair.Key,out long acquired); limit.Acquired=acquired; active.Add(limit);
        }
        // A direct caller with unresolved ownership never silently gets an unlimited execution.
        if (s.LimitOwnerError!="" && s.Goal.maxCount>=0)
            active.Add(new ExpeditionLimit {OrderId=-1,Item=s.Goal.item.ToString(),Maximum=s.Goal.maxCount,Acquired=Gathered(s.Goal,0)});
        return active;
    }
    private static AICandidate Nav(State s, string op, string value, string description, string lower = null, string upper = null)
    {
        var n = new ExpeditionNav { Owner = AIAgent.Cur.Id, World = s.World, GoalRevision = AIGoals.GoalRevision, CursorRevision = s.Cursor.Revision, Op = op, Value = value, Lower = lower, Upper = upper };
        string key = "expedition_nav:" + n.CursorRevision + ":" + op + ":" + value;
        s.Offered[key] = n;
        return new AICandidate { kind = "expedition_nav", key = key, expeditionNav = n, desc = description };
    }
    // A choice surface built without Compose (상자 열림 모드) still becomes the choices the compact report refers to.
    // Its compact field observation is not repeated from the last ordinary build (merge review L-2).
    public static void NoteChoices(List<AICandidate> choices) { var s = Sync(); s.LastChoices = choices ?? new List<AICandidate>(); s.FieldPaused = true; }
    public static List<AICandidate> Compose(World server, Entity player, AIObservation o, List<AICandidate> list, ExpeditionQuery query = null, bool travelMoving = false)
    {
        var result = ComposeChoices(server, player, o, list, query, travelMoving);
        Sync().FieldPaused = false;
        Sync().LastChoices = result;
        return result;
    }
    private static List<AICandidate> ComposeChoices(World server, Entity player, AIObservation o, List<AICandidate> list, ExpeditionQuery query, bool travelMoving)
    {
        Observe(server, player); var s = Sync(); s.Opportunities.Clear(); s.OpportunityDescriptions.Clear();
        // 항목6: while a corridor run moves, a common resource kind is offered only when its interval is due.
        s.Pacer.Sync(AIGoals.GoalRevision);
        foreach (var c in list) if (Gather(c)) s.Pacer.Learn(c.itemId.ToString());
        if (travelMoving)
        {
            // User decision (2026-11-08, F2): before its interval a common kind is not a choice in any call while moving.
            // A choice without a known item (검수 F2b) is never held back.
            var planItems = PlanItems(s); float now = Time.realtimeSinceStartup;
            list.RemoveAll(c => Gather(c) && c.itemId != ObjectID.None && AIReportRelevance.Why(c.itemId.ToString(), planItems, s.Valuable) == null && !s.Pacer.Offerable(c.itemId.ToString(), now));
        }
        if (query == null)
        {
            // Compatibility callers already have concrete candidates. The normal build passes its cheap catalog.
            query = BeginBuild();
            foreach (var candidate in list)
                if (Gather(candidate) || Travel(candidate) && candidate.kind != "retreat" && candidate.kind != "travel_turn" && candidate.entity == Entity.Null && candidate.life == null)
                    query.Add(candidate.key, Gather(candidate) ? "gather" : "travel", Gather(candidate) ? candidate.itemId.ToString() : candidate.kind,
                        candidate.tile.x, candidate.tile.y, candidate.desc);
        }
        if (query.Revision != s.Cursor.Revision || query.Owner != AIAgent.Cur.Id || query.World != s.World || query.GoalRevision != AIGoals.GoalRevision)
            throw new InvalidOperationException("Expedition context changed during candidate preparation");
        s.BrowseTargets = query.Count;
        s.CatalogIds.Clear();
        foreach (var target in query.Targets) if (target.Branch == "gather") s.CatalogIds.Add(target.Item + "@" + target.X + "," + target.Y);
        s.LimitFacts = AIGathering.LimitNote(server, player);
        if (s.LimitOwnerError!="" && s.Ledger.ShouldReport("limit-owner:"+s.Goal.source+":"+s.LimitOwnerError))
            AIPlanner.RequestReplan(server,player,s.LimitOwnerError);
        var location = AIBody.TileOf(o.pos); s.LastLocation = location.x + "," + location.y;
        // 검수 F4: one fact per resource ("<item>@x,y"), so a group and its members, a regrouped vein or a changed pile
        // count are the same facts.
        var opportunityFacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in list) if (Gather(c))
        {
            bool any = false;
            foreach (var fact in ResourceIds(c)) if (opportunityFacts.Add(fact)) { s.Opportunities.Add(fact); any = true; }
            if (any) s.OpportunityDescriptions.Add(c.desc);
        }
        // Navigation defers route evaluation, never the fact that the character observed a resource.
        // Keep these identities in the existing report/acknowledgement ledger when their leaf is not prepared.
        var concreteKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in list) if (Gather(candidate)) concreteKeys.Add(candidate.key);
        foreach (var target in query.Targets)
            if (target.Branch == "gather" && !concreteKeys.Contains(target.Key))
            {
                string fact = target.Item + "@" + target.X + "," + target.Y;
                if (opportunityFacts.Add(fact))
                {
                    s.Opportunities.Add(fact);
                    s.OpportunityDescriptions.Add(target.Description + " · 관측한 대상, 경로·실행 조건은 대상 선택 후 확인");
                }
            }
        s.Opportunities.Sort(StringComparer.Ordinal);
        list.RemoveAll(c => AIGathering.LimitReason(server, player, c) != null);
        if (s.Suspended != null && AIActions.Stale(server, player, s.Suspended) == null && !list.Exists(c => c.key == "expedition_resume"))
            list.Add(new AICandidate { kind = "expedition_resume", key = "expedition_resume", resume = s.Suspended, progress = true, desc = "원작업 재개 — " + s.Suspended.desc + ". 이미 처리한 대상은 실제 상태를 다시 확인. 원래 부탁·누적량 유지." });
        s.Offered.Clear();
        if (AIInventoryMenu.Active || AILife.BlocksInventoryView(server, player)) return list;
        // Only registered world leaves move into the existing browser. Explicit destinations, current work,
        // urgent reactions and visible collection groups keep their direct entries.
        var browseable = list.FindAll(c => query.Contains(c.key) && c.kind != "gather_group" && c.kind != "continue"
            && c.entity == Entity.Null && c.life == null
            && !(c.progress && (s.Goal.kind == GoalKind.Goto || s.Goal.kind == GoalKind.Home)));
        list.RemoveAll(c => browseable.Contains(c));
        if (!s.Cursor.Open)
        { list.Add(Nav(s, "open", "", "채집·이동 목록 펼치기 — 대상 종류와 장소를 찾아보기. 현재 몸 행동은 계속.")); return list; }
        list.Add(Nav(s, "close", "", "채집·이동 목록 닫기 — 현재 행동·부탁 유지"));
        if (s.Cursor.Branch == "")
        {
            list.Add(Nav(s, "branch", "gather", "채집 대상 찾아보기"));
            list.Add(Nav(s, "branch", "travel", "통로·알고 있는 목적지 찾아보기")); return list;
        }
        list.Add(Nav(s, "back", "", "채집·이동 목록 뒤로"));
        list.Add(Nav(s, "root", "", "채집·이동 목록 처음으로"));
        // Reserve the later inventory entry as well as the controls already in list.
        query.Seal(Math.Min(query.MaxLeaves, Math.Max(2, 255 - list.Count - 1)));
        foreach (var entry in query.Entries)
        {
            if (entry.Target == null) list.Add(Nav(s, entry.Op, entry.Value, entry.Text, entry.Lower, entry.Upper));
            else
            {
                var candidate = browseable.Find(c => c.key == entry.Target.Key);
                if (candidate != null) list.Add(candidate);
            }
        }
        return list;
    }
    public static string Stale(AICandidate c)
    {
        var s = Sync();
        if (c.kind == "expedition_resume") return c.resume == s.Suspended ? null : "재개할 원작업이 바뀜";
        if (c.expeditionNav == null) return null;
        string why = s.Cursor.Check(c.expeditionNav, AIAgent.Cur.Id, s.World, AIGoals.GoalRevision);
        if (why != null) return why;
        return s.Offered.TryGetValue(c.key, out var n) && ReferenceEquals(n, c.expeditionNav) ? null : "현재 목록에 없는 원정 경로";
    }
    public static string Navigate(AICandidate c)
    {
        string why = Stale(c); if (why != null) return why;
        Sync().Cursor.Apply(c.expeditionNav); return null;
    }
    public static AICandidate Resolve(AICandidate c) => c.kind == "expedition_resume" ? c.resume : c;
    public static void Starting(World server, Entity player, AICandidate c, AISkill skill, float now)
    {
        Observe(server, player); var s = Sync();
        if (Receives(c) && MainWork(c) && s.Work.TryGetValue(s.Goal, out var work))
        { work.Selection = AIFieldText.Choice(c.kind, c.itemId.ToString(), c.tile.x, c.tile.y, c.resources?.Length ?? 1); work.Location = string.IsNullOrEmpty(s.Goal.where) ? "tile:" + c.tile.x + "," + c.tile.y : s.Goal.where; }
        if (!MainWork(c) && MainWork(s.Current) && s.Skill != null && s.Skill.Status == SkillStatus.Running && s.Suspended == null) s.Suspended = s.Current;
        if (MainWork(c))
        { if (s.SideStarted >= 0) s.SideSeconds += now - s.SideStarted; s.SideStarted = -1; s.Suspended = null; }
        else if (s.Goal.IsReal && s.SideStarted < 0) s.SideStarted = now;
        Choosing(server, player, c);
        if (s.Goal.kind == GoalKind.Home || s.Goal.kind == GoalKind.Goto)
            s.ReturnContext = "목적 " + s.Goal.Text + "; 후속 " + PlanText() + "; 출발 " + s.LastLocation;
        s.Current = c; s.Skill = skill;
        s.BodyOwners.Clear(); foreach (var owner in AIPlanner.Cur.Orders.Carried(s.Goal.source)) s.BodyOwners.Add(owner.Id);
    }
    // A branch turn, continue, or a restart/reverse offered by the running corridor run keeps the same exploration
    // around the current place. Only a new departure (a new exploration start, a destination walk, a return, an
    // unseen target) is a choice to leave the site.
    private static IEnumerable<string> ResourceIds(AICandidate c)
        => c.resources != null && c.resources.Length > 0 ? c.resources : new[] { c.itemId + "@" + c.tile.x + "," + c.tile.y };
    internal static bool SameExploration(AICandidate c) => c != null && (c.kind == "travel_turn" || c.kind == "continue"
        || c.key != null && (c.key.StartsWith("travel:restart:", StringComparison.Ordinal) || c.key.StartsWith("travel:reverse:", StringComparison.Ordinal)));
    private static HashSet<string> PlanItems(State s)
    {
        var roots = new List<string>();
        void Add(string item) { if (!string.IsNullOrEmpty(item) && AIGoals.TryItemPublic(item, out ObjectID id, out _)) roots.Add(id.ToString()); }
        if (s.Goal != null && s.Goal.IsReal) { if (s.Goal.item != ObjectID.None) roots.Add(s.Goal.item.ToString()); Add(s.Goal.second); }
        var plan = AIPlanner.Cur.Plan;
        if (plan.Active)
        {
            if (plan.Current != null) { Add(plan.Current.Item); Add(plan.Current.Second); }
            foreach (var step in plan.Rest()) { Add(step.Item); Add(step.Second); }
        }
        return AIReportRelevance.PlanItems(roots);
    }
    public static void Choosing(World server, Entity player, AICandidate c)
    {
        var s = Sync();
        if (SameExploration(c) || !Travel(c)) return;
        bool leaving = c.kind == "explore" || c.kind == "travel" || c.kind == "retreat" || !AIVision.CanSee(AIBody.Pos(server, player), c.tile);
        if (!leaving) return;
        // Only goal/plan items and valuable kinds are reported; each opportunity is reported once until reviewed.
        var planItems = PlanItems(s);
        var fresh = new List<string>(); var reasons = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var fact in s.Opportunities)
        {
            if (s.Ledger.Reviewed(fact)) continue;
            string item = AIReportRelevance.ItemOf(fact), why = AIReportRelevance.Why(item, planItems, s.Valuable);
            if (why == null || !s.Ledger.ShouldReport("leave:" + fact)) continue;
            fresh.Add(fact); reasons.Add(item + "=" + why);
        }
        if (fresh.Count > 0)
            AIPlanner.RequestReplan(server, player, "현장 이탈 선택 전에 미검토 자원 기회: " + string.Join(", ", fresh) + "; 관련 근거 " + string.Join(", ", reasons)
                + "; 위치 " + s.LastLocation + ". 재방문 이동이 필요해짐. 유효한 현장 행동은 계속 판단.");
    }
    public static void Ended(AISkill skill)
    {
        var s = Sync(); if (s.Skill != skill) return;
        if (s.Server != null) Observe(s.Server, s.Player);
        s.LastResult = AIFieldText.Result(skill.Name, skill.Reason, skill.ExecutionObservations);
        s.Skill = null;
    }
    public static void AfterBuild(World server, Entity player, List<AICandidate> candidates, bool frontierPending)
    {
        var s = Sync();
        if (s.Goal.kind != GoalKind.Gather || Active || s.BrowseTargets > 0 || AIInventoryMenu.Active || frontierPending || AIGoals.CompletionObserved) return;
        bool alternative = candidates.Exists(c => c.progress && c.kind != "finish_goal" && c.kind != "request_plan");
        if (alternative || s.Skill != null && s.Skill.Status == SkillStatus.Running) return;
        string blocked = AIGoals.Observation;
        if (string.IsNullOrWhiteSpace(blocked.Replace("/", ""))) return;
        string fact = "premise:" + s.Goal.Text + ":" + s.LastLocation + ":" + blocked;
        if (s.Ledger.ShouldReport(fact)) AIPlanner.RequestReplan(server, player, "현재 채집 전제 상실과 맡은 범위의 실행 대안 부재: " + blocked + ". " + Report());
    }
    // 항목6 travel pacing: sightings while moving, what the request offered, what the answer chose.
    // Where this character's valuable-kind changes are kept (installed by the runtime; null: not kept on disk).
    public static Func<string> ValuablePath = () => null;
    private static void LoadValuable(State s)
    {
        s.Valuable.Clear();
        try
        {
            string path = ValuablePath();
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            if (ValuableStore.TryRead(System.IO.File.ReadAllText(path), out var kinds)) foreach (var kv in kinds) s.Valuable[kv.Key] = kv.Value;
            else AIPlayerSpikeMod.Log("EXPEDITION: valuable-kind file unreadable, default kinds used: " + path);
        }
        catch (Exception e) { s.Valuable.Clear(); AIPlayerSpikeMod.Log("EXPEDITION: valuable-kind file not read (" + e.GetType().Name + "), default kinds used"); }
    }
    private static void SaveValuable(State s)
    {
        try
        {
            string path = ValuablePath(); if (string.IsNullOrEmpty(path)) return;
            string tmp = path + ".tmp";
            System.IO.File.WriteAllText(tmp, ValuableStore.Write(s.Valuable));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            System.IO.File.Move(tmp, path);
        }
        catch (Exception e) { AIPlayerSpikeMod.Log("EXPEDITION: valuable-kind file not saved (" + e.GetType().Name + ")"); }
    }
    // The plan answer's "valuable": {"<ObjectID>": true|false}. Unknown names are ignored; the last state is kept
    // (also on disk: ValuablePath).
    public static string SetValuable(Dictionary<string, bool> changes)
    {
        if (changes == null || changes.Count == 0) return "";
        var s = Sync(); var applied = new List<string>();
        foreach (var kv in changes)
        {
            if (!AIGoals.TryItemPublic(kv.Key ?? "", out ObjectID id, out _)) continue;
            s.Valuable[id.ToString()] = kv.Value; applied.Add(id + (kv.Value ? "+" : "-"));
        }
        if (applied.Count > 0) SaveValuable(s);
        return string.Join(" ", applied);
    }
    public static List<string> TravelSightings(float now, IEnumerable<(string item, int x, int y)> visible)
    {
        var s = Sync(); s.Pacer.Sync(AIGoals.GoalRevision); var planItems = PlanItems(s);
        return s.Pacer.Observe(now, visible, item => AIReportRelevance.Why(item, planItems, s.Valuable));
    }
    public static void LowerSent(List<AICandidate> sent)
    {
        var items = new List<string>(); foreach (var c in sent) if (Gather(c)) items.Add(c.itemId.ToString());
        Sync().Pacer.Sent(items);
    }
    public static void LowerAnswered(AICandidate chosen, float now)
    {
        var c = chosen != null && chosen.kind == "expedition_resume" ? chosen.resume : chosen;
        Sync().Pacer.Answered(now, c != null && Gather(c) ? c.itemId.ToString() : null);
    }
    public static void Sent(int id) { var s = Sync(); s.Ledger.Sent(id, s.Opportunities); }
    public static void Accepted(int id, string guidance = null) { var s = Sync(); s.Ledger.Accepted(id); if (guidance != null) s.UpperGuidance = guidance; }
    private static string PlanText()
    {
        var p = AIPlanner.Cur.Plan;
        return p.Active ? string.Join(" → ", p.Rest().ConvertAll(x => x.Text)) : "후속 계획 미지정: 현장 단계 완료 후 상위 판단";
    }
    // The upper LLM's report (unchanged) and the action model's input (토큰 절감 ①: the resources already in the
    // choices are counted by kind and referred to the choices; only those outside the choices are listed briefly).
    public static string Report() => ReportText(false);
    public static string PromptReport() => ReportText(true);
    private static string FieldObservation(State s, bool compact)
    {
        if (!compact) return string.Join(" / ", s.OpportunityDescriptions);
        if (s.FieldPaused) return "상자 열림 중이라 현장 자원 목록은 싣지 않음(상자를 닫으면 다시 관측)";
        var inChoices = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in s.LastChoices) if (Gather(c)) foreach (var id in ResourceIds(c)) inChoices.Add(id);
        var counted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var outside = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var elsewhere = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var fact in s.Opportunities)
        {
            string item = AIReportRelevance.ItemOf(fact);
            if (inChoices.Contains(fact)) { counted.TryGetValue(item, out int n); counted[item] = n + 1; }
            else
            {
                var into = s.CatalogIds.Contains(fact) ? outside : elsewhere;
                if (!into.TryGetValue(item, out var l)) into[item] = l = new List<string>(); l.Add(fact.Substring(item.Length + 1));
            }
        }
        var parts = new List<string>();
        if (counted.Count > 0) { var c = new List<string>(); foreach (var kv in counted) c.Add(kv.Key + " " + kv.Value + "곳"); parts.Add("선택지에 있음(선택지 참조): " + string.Join(", ", c)); }
        string Places(SortedDictionary<string, List<string>> d)
        { var o = new List<string>(); foreach (var kv in d) o.Add(kv.Key + " @" + string.Join(" @", kv.Value.GetRange(0, Math.Min(8, kv.Value.Count))) + (kv.Value.Count > 8 ? " 외 " + (kv.Value.Count - 8) + "곳" : "")); return string.Join("; ", o); }
        if (outside.Count > 0) parts.Add("선택지 밖(채집·이동 목록 안): " + Places(outside));
        if (elsewhere.Count > 0) parts.Add("지금 선택지에 없음(가방·생활 메뉴 중 등): " + Places(elsewhere));
        return parts.Count == 0 ? "없음" : string.Join(". ", parts);
    }
    private static string ReportText(bool compact)
    {
        var s = Sync(); float side = s.SideSeconds + (s.SideStarted < 0 ? 0 : Time.realtimeSinceStartup - s.SideStarted);
        if (s.Server != null) s.LimitFacts = AIGathering.LimitNote(s.Server,s.Player);
        var work = new List<string>(); foreach (var record in s.Work.Values) work.Add(record.Text);
        var limits = new List<string>(); foreach (var limit in ActiveLimits()) limits.Add("원부탁#"+limit.OrderId+" "+limit.Item+" 최대 "+limit.Maximum+" / 실제확보 "+limit.Acquired);
        return "원정: 원래 부탁 " + string.Join(" / ", AIPlanner.Cur.Orders.OpenAll().ConvertAll(x => x.Text))
            + "; 현재 작업 " + s.Goal.Text + "; 곁일 누적 " + side.ToString("F0") + "초; 재개 대상 " + (s.Suspended?.desc ?? "현재 작업 맥락 유지")
            + "; 이번 접속 관측 채집 확보 " + s.Goal.item + " " + s.Ledger.Acquired(s.Goal.item.ToString()) + "; 마지막 결과 " + s.LastResult
            + "; 귀환 맥락 " + s.ReturnContext + "; 다음 작업 " + PlanText()
            + "; 탐색 목록 대상 " + s.BrowseTargets + "개, 실행 경로·조건은 선택 단계에서 확인"
            + "; 상위 판단·재량 조건 " + s.UpperGuidance + "; 원정 채집 작업 기록 " + string.Join(" / ", work)
            + "; 명시 수량 조건 " + string.Join(" / ",limits) + "; 실행 경계 " + s.LimitFacts + "; 조건 소유 확인 " + s.LimitOwnerError
            + "; 마지막 현장 관측(" + s.LastLocation + ", 이후 상태는 재확인) " + FieldObservation(s, compact)
            + ". 곁일·추가 채집·귀환 여부는 판단모델이 결정. 원래 부탁·급함·가방·위험·누적 지연을 함께 고려. 도착은 보관/제작/부탁 완료와 별개.";
    }
}
