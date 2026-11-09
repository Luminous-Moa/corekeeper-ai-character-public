// Character-local read-only life navigation. Concrete observations and actions stay with their feature owners.
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static class AILife
{
    public sealed class Entry
    {
        public string Op = "", Id = "", Text = "";
        public AICandidate Candidate;
        public Func<Entry> Observe; // only the current page evaluates a counterpart prediction/route
    }
    private sealed class State
    {
        public World World; public Entity Player;
        public LifeCursor Cursor;
        public AIGoal Goal;
        public readonly LifeProgress Progress = new LifeProgress();
        public readonly Dictionary<string, LifeNav> Offered = new Dictionary<string, LifeNav>(StringComparer.Ordinal);
        public readonly Dictionary<AISkill, LifeSelection> Runs = new Dictionary<AISkill, LifeSelection>();
        public string Facts = "", Result = "";
        public bool ViewSuspended;
    }
    private static State S => AIAgent.Cur.Get<State>(AISlot.LifeTree);
    private static State Sync()
    {
        var s = S;
        if (s.Cursor == null) s.Cursor = new LifeCursor(AIAgent.Cur.Id, AIGoals.GoalRevision);
        if (!ReferenceEquals(s.Goal, AIGoals.Goal) || s.Cursor.GoalRevision != AIGoals.GoalRevision || s.Cursor.Owner != AIAgent.Cur.Id)
        {
            s.Goal = AIGoals.Goal;
            s.Cursor.Reset(AIAgent.Cur.Id, AIGoals.GoalRevision);
            s.Progress.Reset(s.Goal, AIGoals.GoalRevision);
            s.Offered.Clear(); s.Facts = s.Result = ""; s.ViewSuspended = false;
        }
        return s;
    }
    public static void ObserveContext(World world, Entity player) { var s = Sync(); s.World = world; s.Player = player; }
    public static void Reset() { AIAgent.Cur.Slots[AISlot.LifeTree] = null; }
    public static void OnGoalChanged() { Sync(); }
    public static LifeCursor Cursor => Sync().Cursor;
    public static bool BlocksInventoryView(World world, Entity player)
    { var s = Sync(); return s.Cursor.Open && !s.ViewSuspended; }
    // Changing the browsing owner revokes only old navigation tokens, never work receipts or a running action.
    public static void LeaveNavigation()
    {
        var s = Sync(); s.Cursor.Reset(AIAgent.Cur.Id, AIGoals.GoalRevision); s.Offered.Clear(); s.Facts = ""; s.ViewSuspended = false;
    }
    private static string Clean(string x) => AILifeGoalForm.Text(x) ?? "";
    private static string ItemName(string x) { x = Clean(x); int at = x.IndexOf("#v", StringComparison.Ordinal); return at < 0 ? x : x.Substring(0, at); }
    private static string Branch(AIGoal g) => g.kind == GoalKind.Farm ? "farm" : g.kind == GoalKind.Cook ? "cook" : g.kind == GoalKind.Craft ? "craft" : g.kind == GoalKind.Maintenance ? "maintenance" : g.kind == GoalKind.Store || g.kind == GoalKind.Take ? "inventory" : "";
    private static bool Selected(string selected, string specified) => Clean(specified) == "" || Clean(selected) == "" || ItemName(selected) == ItemName(specified);
    private static bool Matches(LifeSelection q, AIGoal g)
    {
        if (g == null || Branch(g) == "" || q.Branch != Branch(g)) return false;
        string gm = g.kind == GoalKind.Store ? "store" : g.kind == GoalKind.Take ? "take" : g.mode;
        if (!Selected(q.Mode, gm) || !Selected(q.Item, g.item == ObjectID.None ? "" : g.item.ToString()) || !Selected(q.Second, g.second)) return false;
        if (g.slot >= 0 && q.Slot >= 0 && g.slot != q.Slot) return false;
        if (q.Branch == "farm")
        {
            // The farm producer intersects every current tile/region with the original goal bounds.
            if (AILifeGoalForm.TryFarmBounds(g.where, out _, out _, out _, out _) && q.Where.StartsWith("tile:", StringComparison.Ordinal))
            {
                if (!AILifeGoalForm.TryFarmBounds(q.Where, out int x, out int y, out _, out _) || !AILifeGoalForm.InFarmWhere(g.where, x, y)) return false;
            }
            return true;
        }
        if (q.Branch == "inventory" && (Clean(g.where) == "" || g.where == "base"))
        {
            if (Clean(q.Where) == "") return Clean(g.where) == "";
            // an area step on the way to a chest (review I3/N7): "area:" under no where, "base:area:" under base
            if (AIChestAreas.IsArea(q.Where)) return Clean(g.where) == "";
            if (q.Where.StartsWith("base:", StringComparison.Ordinal) && AIChestAreas.IsArea(q.Where.Substring(5))) return g.where == "base";
            if (!q.Where.StartsWith("chest:", StringComparison.Ordinal) || !AILifeGoalForm.TryFarmBounds("tile:" + q.Where.Substring(6), out int x, out int y, out _, out _)) return false;
            return Clean(g.where) == "" || S.World != null && AIInventory.BaseChestContext(S.World, S.Player, new int2(x,y)); // explicit base uses the same observed-at-anchor context as its producer
        }
        // An area goal (chest_ids, review-2 N2), or a base area goal (review-4 F5): a chest or smaller area inside it
        // matches; under a base area the chest must also be in the base context, as its producer requires.
        string gw = Clean(g.where); bool baseArea = gw.StartsWith("base:", StringComparison.Ordinal) && AIChestAreas.IsArea(gw.Substring(5));
        if (q.Branch == "inventory" && AIChestAreas.Parse(baseArea ? gw.Substring(5) : gw, out var gmin, out int gsize) && Clean(q.Where) != "")
        {
            int2 t; bool chest = false;
            string qw = q.Where.StartsWith("base:", StringComparison.Ordinal) ? q.Where.Substring(5) : q.Where;
            if (AIChestAreas.Parse(qw, out var qmin, out _)) t = qmin;
            else if (qw.StartsWith("chest:", StringComparison.Ordinal) && AILifeGoalForm.TryFarmBounds("tile:" + qw.Substring(6), out int cx, out int cy, out _, out _)) { t = new int2(cx, cy); chest = true; }
            else return false;
            bool inside = t.x >= gmin.x && t.x < gmin.x + gsize && t.y >= gmin.y && t.y < gmin.y + gsize;
            return inside && (!baseArea || !chest || S.World != null && AIInventory.BaseChestContext(S.World, S.Player, t));
        }
        return Clean(g.where) == "" || Clean(q.Where) == "" || g.where == q.Where;
    }
    private static LifeSelection Bind(LifeSelection q)
    {
        var s = Sync(); q.GoalRevision = AIGoals.GoalRevision;
        q.MatchesGoal = Matches(q, s.Goal); q.Goal = q.MatchesGoal ? s.Goal : null;
        q.RequestedCount = q.MatchesGoal ? Remaining(q) : 1;
        return q;
    }
    // Producers call this after an actual slot/item identity resolves a cursor's unspecified fields.
    public static LifeSelection Resolve(LifeSelection selection) => Bind(selection.Copy());
    public static LifeSelection ForGoal(AIGoal g)
    {
        var q = new LifeSelection { Branch = Branch(g), Mode = g.kind == GoalKind.Store ? "store" : g.kind == GoalKind.Take ? "take" : Clean(g.mode),
            Item = g.item == ObjectID.None ? "" : g.item.ToString(), Second = Clean(g.second), Where = Clean(g.where), Slot = g.slot,
            Goal = g, GoalRevision = AIGoals.GoalRevision, MatchesGoal = ReferenceEquals(g, AIGoals.Goal) };
        q.RequestedCount = q.MatchesGoal ? Remaining(q) : 1;
        return q;
    }
    private static LifeSelection ForCursor()
    {
        var c = Cursor;
        return Bind(new LifeSelection { Branch = c.Branch, Mode = c.Mode, Item = c.Item, Second = c.Second, Where = c.Where, Slot = c.Slot });
    }
    // A narrowed choice still belongs to the original explicit quantity. Separate selections never share this key.
    private static LifeSelection ProgressKey(LifeSelection selection)
    {
        var q = selection.Copy(); var g = q.Goal as AIGoal;
        if (g == null) return q;
        q.Mode = Clean(g.mode);
        // An unspecified goal selector is a wildcard over the requested work, not a fresh quota per local choice.
        q.Item = g.item == ObjectID.None ? "" : g.item.ToString();
        q.Second = Clean(g.second);
        q.Where = Clean(g.where);
        if (g.slot >= 0) q.Slot = g.slot; else q.Slot = -1;
        return q;
    }
    public static int Remaining(LifeSelection selection)
    {
        var s = Sync(); var g = selection?.Goal as AIGoal;
        if (g == null || !selection.MatchesGoal || !ReferenceEquals(g, s.Goal)) return 1;
        if (g.count <= 0) return 0;
        if (selection.Branch == "inventory") return Math.Max(0, g.count - AIInventory.MovedForGoal(g));
        return s.Progress.Remaining(ProgressKey(selection), g.count);
    }
    public static bool RequestSatisfied(LifeSelection selection)
        => selection?.MatchesGoal == true && selection.Goal is AIGoal g && g.count > 0 && Remaining(selection) == 0;
    private static List<Entry> Entries(World world, Entity player, AIObservation observation, string currentKey)
    {
        var c = Cursor;
        if (c.Branch == "") return new List<Entry> {
            new Entry { Op = "branch", Id = "craft", Text = "제작 — 알려진 품목과 실제 제작대 선택" },
            new Entry { Op = "branch", Id = "cook", Text = "요리 — 재료·자기 솥·투입/수령 선택" },
            new Entry { Op = "branch", Id = "farm", Text = "농사 — 작업·종류·자기 밭 영역/칸 선택" },
            new Entry { Op = "branch", Id = "inventory", Text = "보관/꺼내기 — 상자(모두 공용)·정확 슬롯 선택" },
            new Entry { Op = "branch", Id = "maintenance", Text = "장비 정비 — 일반 수리·단일 레벨 업그레이드·자기 pending 회수" },
            new Entry { Op = "branch", Id = "rest", Text = "휴식 — 보이는 침대에서 자기(자는 동안 체력 회복)" } };
        var q = ForCursor();
        switch (c.Branch)
        {
            case "farm": return AIFarm.LifeEntries(world, player, observation, currentKey, q);
            case "cook": return AICook.LifeEntries(world, player, observation, currentKey, q);
            case "craft": return AICraft.LifeEntries(world, player, observation, currentKey, q);
            case "inventory": return AIInventory.LifeEntries(world, player, observation, currentKey, q);
            case "maintenance": return AIMaintenance.LifeEntries(world, player, observation, currentKey, q);
            case "rest": return AIBed.LifeEntries(world, player, observation, currentKey, q);
            default: return new List<Entry>();
        }
    }
    private static AICandidate Nav(State s, string op, string value, string text, bool opening = false)
    {
        var token = s.Cursor.Token(op, value); s.Offered[token.Key] = token;
        return new AICandidate { kind = "life_nav", key = token.Key, value = opening ? "life_open" : token.Key, desc = text + "（목록 조회만; 현재 몸 행동 유지）", lifeNav = token };
    }
    public static List<AICandidate> Compose(World world, Entity player, AIObservation observation, string currentKey, bool combat, List<AICandidate> actual, AICandidate direct = null)
    {
        ObserveContext(world, player);
        if (AIInventoryMenu.Active) return actual;
        var s = Sync(); s.Offered.Clear(); s.Facts = "";
        s.ViewSuspended = false;
        actual.RemoveAll(c => c.life != null && RequestSatisfied(c.life));
        if (!s.Cursor.Open) { actual.Add(Nav(s, "open", "", "생활 선택지 열기", true)); return actual; }
        var result = new List<AICandidate>(actual);
        // A fully specified goal remains executable without traversing the cursor.
        direct = direct ?? actual.Find(c => c.life != null && c.life.MatchesGoal && c.kind != "continue");
        if (direct != null && RequestSatisfied(direct.life)) direct = null;
        if (direct != null && string.IsNullOrEmpty(direct.value)) direct.value = "life_exec:" + direct.key;
        if (direct != null && !result.Exists(c => c.key == direct.key)) result.Add(direct);
        var all = Entries(world, player, observation, currentKey) ?? new List<Entry>();
        var selectable = new List<Entry>(); var facts = new List<string>();
        foreach (var e in all)
        {
            if (e.Candidate == null && string.IsNullOrEmpty(e.Op)) { if (!string.IsNullOrEmpty(e.Text)) facts.Add(e.Text); continue; }
            selectable.Add(e);
        }
        selectable.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id ?? a.Candidate?.key, b.Id ?? b.Candidate?.key));
        // Never change revision merely because observations redraw a depth. A vanished page is shown as empty with prev.
        int start = 0;
        for (int i = start; i < selectable.Count; i++)
        {
            var e = selectable[i]; e = e.Observe?.Invoke() ?? e;
            if (e.Candidate == null && string.IsNullOrEmpty(e.Op)) { if (!string.IsNullOrEmpty(e.Text)) facts.Add(e.Text); continue; }
            if (e.Candidate != null)
            {
                var candidate = e.Candidate;
                if (candidate.life == null) candidate.life = ForCursor();
                if (candidate.life.MatchesGoal && AIGoals.Goal.count > 0 && Remaining(candidate.life) == 0) { facts.Add("명시 요청량의 확인된 작업 충족; finish_goal 여부를 직접 선택"); continue; }
                if (!candidate.life.MatchesGoal) candidate.desc += " · 원래 단계 밖의 별도 행동; 목표 진척에 합산하지 않음";
                if (!result.Exists(c => c.key == candidate.key)) { candidate.value = "life_exec:" + candidate.key; result.Add(candidate); }
            }
            else result.Add(Nav(s, e.Op, e.Id, e.Text));
        }
        if (s.Cursor.Branch != "") result.Add(Nav(s, "back", "", "뒤로 가기"));
        result.Add(Nav(s, "root", "", "생활 루트로"));
        result.Add(Nav(s, "close", "", "생활 목록 닫기"));
        s.Facts = string.Join(" / ", facts);
        return result;
    }
    public static string NavStale(LifeNav nav)
    {
        if (AIInventoryMenu.Active) return "가방 선택 메뉴로 탐색 소유권이 바뀜";
        var s = Sync();
        return s.Cursor.Check(nav, AIAgent.Cur.Id, AIGoals.GoalRevision, (op, value) => s.Offered.ContainsKey($"life_nav:{s.Cursor.Revision}:{op}:{value}"));
    }
    public static string Navigate(LifeNav nav)
    {
        string why = NavStale(nav); if (why != null) return why;
        var s = Sync(); why = s.Cursor.Apply(nav, AIAgent.Cur.Id, AIGoals.GoalRevision);
        if (why == null) s.Offered.Clear();
        return why;
    }
    public static string ExecuteStale(AICandidate c)
    {
        if (c.life == null) return null;
        var s = Sync();
        if (c.life.GoalRevision != AIGoals.GoalRevision) return "생활 실행을 고른 뒤 목표가 바뀜";
        if (c.life.MatchesGoal && !ReferenceEquals(c.life.Goal, s.Goal)) return "생활 실행의 목표 참조가 바뀜";
        if (RequestSatisfied(c.life)) return "생활 요청량이 이미 확인되어 새 실행이 필요하지 않음";
        if (c.life.MatchesGoal && s.Goal.count > 0 && c.life.Branch != "craft" && c.life.OfferedCount > Remaining(c.life)) return "생활 요청 잔량이 선택 뒤 줄어듦; 수량 다시 조회 필요";
        return null;
    }
    public static string Stale(World world, Entity player, AICandidate c)
    {
        if (c.lifeNav == null && c.life == null) return null;
        return c.lifeNav != null ? NavStale(c.lifeNav) : ExecuteStale(c);
    }
    public static void Starting(AICandidate candidate, AISkill skill)
    {
        if (candidate.life != null) Sync().Runs[skill] = candidate.life.Copy();
    }
    public static void Ended(AISkill skill)
    {
        var s = Sync();
        if (!s.Runs.TryGetValue(skill, out var binding)) return;
        s.Runs.Remove(skill);
        if (!(skill is ILifeResult r)) return;
        var canonical = ProgressKey(binding); s.Progress.Record(skill.Run, canonical, r);
        int left = binding.MatchesGoal ? Remaining(binding) : 0;
        s.Result = $"{binding.Branch}/{binding.Mode}: 요청량 {(binding.Goal as AIGoal)?.count ?? binding.RequestedCount} / 이번 시도량 {r.RequestedWork} / 확인된 작업량 {r.ConfirmedWork} / 확보량 {r.ConfirmedReceived} / 잔량 {left}; {r.ResultStage}";
        AIBrain.ObserveExecution(s.Result, skill);
    }
    public static string Report()
    {
        var s = Sync(); var g = s.Goal;
        return $"현재 경로 {s.Cursor.Path} ({(s.Cursor.Open ? "열림" : "닫힘")}, 페이지 {s.Cursor.Page + 1}). 목록은 조회이며 몸 행동과 목표를 바꾸지 않음. 목표 count={g.count}, mode={g.mode}, second={g.second}, where={g.where}. {s.Result} {s.Facts}";
    }
}
