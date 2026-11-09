// Read-only life navigation and selector contracts. No game or Unity dependencies.
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class LifeNav
{
    public string Owner { get; }
    public int GoalRevision { get; }
    public int CursorRevision { get; }
    public string Op { get; }
    public string Value { get; }
    public string Key => $"life_nav:{CursorRevision}:{Op}:{Value}";
    public LifeNav(string owner, int goalRevision, int cursorRevision, string op, string value = "")
    { Owner = owner; GoalRevision = goalRevision; CursorRevision = cursorRevision; Op = op; Value = value ?? ""; }
}

public sealed class LifeCursor
{
    public string Owner;
    public int GoalRevision, Revision;
    public bool Open;
    public string Branch = "", Mode = "", Item = "", Second = "", Where = "";
    public int Slot = -1, Page;
    // Selection history matters: storage selects where before item, cooking selects it last.
    private readonly List<string> _selected = new List<string>();
    public LifeCursor(string owner, int goalRevision) { Owner = owner; GoalRevision = goalRevision; }
    public LifeNav Token(string op, string value = "") => new LifeNav(Owner, GoalRevision, Revision, op, value);
    public string Path => "생활/" + string.Join("/", new[] { Branch, Mode, Item, Second, Where, Slot < 0 ? "" : "slot:" + Slot });
    private void Clear() { Branch = Mode = Item = Second = Where = ""; Slot = -1; Page = 0; _selected.Clear(); }
    public void Reset(string owner, int goalRevision) { Owner = owner; GoalRevision = goalRevision; Clear(); Open = false; Revision++; }
    public string Check(LifeNav nav, string owner, int goalRevision, Func<string, string, bool> allowed = null)
    {
        if (nav == null || Owner != owner || nav.Owner != owner) return "생활 조회 캐릭터가 바뀜";
        if (GoalRevision != goalRevision || nav.GoalRevision != goalRevision) return "생활 조회 목표 수정 번호가 바뀜";
        if (nav.CursorRevision != Revision) return "생활 탐색 경로가 바뀜; 새 목록 필요";
        switch (nav.Op)
        {
            case "open": if (Open || nav.Value != "") return "생활 열기 값이 유효하지 않음"; break;
            case "branch": if (!Open || (nav.Value != "farm" && nav.Value != "cook" && nav.Value != "craft" && nav.Value != "inventory" && nav.Value != "maintenance")) return "생활 갈래가 유효하지 않음"; break;
            case "mode": case "item": case "second": case "where": if (!Open || nav.Value.Length == 0) return "생활 선택값이 비었음"; break;
            case "slot": if (!Open || !int.TryParse(nav.Value, out int slot) || slot < 0) return "생활 슬롯이 유효하지 않음"; break;
            case "page": if (!Open || !int.TryParse(nav.Value, out int page) || page < 0) return "생활 페이지가 유효하지 않음"; break;
            case "back": if (!Open || _selected.Count == 0 || nav.Value != "") return "생활 부모 경로가 없음"; break;
            case "root": case "close": if (!Open || nav.Value != "") return "생활 경로 이동값이 유효하지 않음"; break;
            default: return "알 수 없는 생활 조회 동작";
        }
        if (allowed != null && !allowed(nav.Op, nav.Value)) return "현재 목록에 없는 생활 선택값; 다시 조회 필요";
        return null;
    }
    public string Apply(LifeNav nav, string owner, int goalRevision, Func<string, string, bool> allowed = null)
    {
        string why = Check(nav, owner, goalRevision, allowed); if (why != null) return why;
        switch (nav.Op)
        {
            case "open": Clear(); Open = true; break;
            case "root": Clear(); Open = true; break;
            case "close": Clear(); Open = false; break;
            case "page": Page = int.Parse(nav.Value, CultureInfo.InvariantCulture); break;
            case "back": string last = _selected[_selected.Count - 1]; _selected.RemoveAt(_selected.Count - 1); Set(last, ""); Page = 0; break;
            default:
                if (nav.Op == "branch") Clear();
                else if (nav.Op == "mode") { Item = Second = Where = ""; Slot = -1; _selected.RemoveAll(x => x != "branch"); }
                Set(nav.Op, nav.Value); _selected.Remove(nav.Op); _selected.Add(nav.Op); Page = 0; break;
        }
        Revision++; return null;
    }
    private void Set(string op, string value)
    {
        switch (op) { case "branch": Branch = value; break; case "mode": Mode = value; break; case "item": Item = value; break; case "second": Second = value; break; case "where": Where = value; break; case "slot": Slot = value == "" ? -1 : int.Parse(value, CultureInfo.InvariantCulture); break; }
    }
}

public sealed class LifeSelection
{
    public string Branch = "", Mode = "", Item = "", Second = "", Where = "";
    public int Slot = -1, RequestedCount, OfferedCount, GoalRevision;
    public object Goal;
    public bool MatchesGoal;
    public string Identity => $"{Branch}|{Mode}|{Item}|{Second}|{Where}|{Slot}";
    public LifeSelection Copy() => (LifeSelection)MemberwiseClone();
}

public interface ILifeResult
{
    int ConfirmedWork { get; }
    int ConfirmedReceived { get; }
    int RequestedWork { get; }
    string ResultStage { get; }
}

// Goal-owned counters, with run deduplication. The caller supplies the selection that actually ran.
public sealed class LifeProgress
{
    private object _goal;
    private int _revision;
    private readonly Dictionary<string, int> _work = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _received = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<int> _runs = new HashSet<int>();
    public void Reset(object goal, int revision) { _goal = goal; _revision = revision; _work.Clear(); _received.Clear(); _runs.Clear(); }
    public bool Record(int run, LifeSelection s, ILifeResult result)
    {
        if (s == null || result == null || !s.MatchesGoal || s.Goal == null || !ReferenceEquals(s.Goal, _goal) || s.GoalRevision != _revision || !_runs.Add(run)) return false;
        _work[s.Identity] = Work(s) + Math.Max(0, result.ConfirmedWork);
        _received[s.Identity] = Received(s) + Math.Max(0, result.ConfirmedReceived); return true;
    }
    public int Work(LifeSelection s) => s != null && _work.TryGetValue(s.Identity, out int n) ? n : 0;
    public int Received(LifeSelection s) => s != null && _received.TryGetValue(s.Identity, out int n) ? n : 0;
    public int Remaining(LifeSelection s, int count) => Math.Max(0, count - ((s.Branch == "cook" && s.Mode == "take" || s.Branch == "craft") ? Received(s) : Work(s)));
}

public static class AILifeGoalForm
{
    public const string InvalidSelector = "\u0001invalid-selector-type";
    public static string Text(string value) => string.IsNullOrEmpty(value) || value == "null" ? null : value;
    public static bool IsLife(string d) => d == "farm" || d == "cook" || d == "craft" || d == "store" || d == "take" || d == "maintain";
    public static int CountFor(string d, int count) => d == "maintain" ? AIMaintenanceRules.CountFor(count) : (d == "farm" || d == "cook") && count < 0 ? 0 : count;
    public static string Check(PlanStep s)
    {
        string mode = Text(s.Mode), second = Text(s.Second), item = Text(s.Item), where = Text(s.Where);
        if (mode == InvalidSelector || second == InvalidSelector) return "mode/second는 문자열이어야 함";
        if (s.Do == "maintain") return AIMaintenanceRules.Check(s);
        if (!IsLife(s.Do)) return mode != null || second != null ? "mode/second는 생활 목표에서만 사용" : null;
        if (s.Do != "farm" && s.Do != "cook")
        {
            if (mode != null || second != null) return "이 생활 목표는 mode/second를 받지 않음";
            return s.Do == "craft" && where != null && where != "hand" && !Facility(where, "station") ? "제작 where는 hand 또는 station:<ID>@x,y" : null;
        }
        if (s.Count < -1 || s.Count == 0 || s.Count > int.MaxValue) return "생활 count는 지정하면 1..int.MaxValue 정수";
        if (s.Count > 0 && mode == null) return "생활 count에는 mode 지정 필요";
        if (s.Keep >= 0 || s.Slot >= 0) return "농사/요리는 keep/slot을 받지 않음";
        if (s.Do == "farm")
        {
            if (second != null) return "농사는 second를 받지 않음";
            if (mode != null && mode != "hoe" && mode != "plant" && mode != "water" && mode != "fill" && mode != "harvest") return "알 수 없는 농사 mode";
            if (mode == null && item != null) return "농사 item에는 구체 mode 지정 필요";
            if ((mode == "hoe" || mode == "fill") && item != null) return "hoe/fill은 item을 받지 않음";
            if (mode == "fill" && s.Count > 1) return "물 채우기 count는 1만 가능";
            if (where != null && where != "farm" && !TryFarmBounds(where, out _, out _, out _, out _)) return "농사 where는 farm, tile:x,y 또는 rect:x1,y1,x2,y2";
        }
        else
        {
            if (mode != null && mode != "put" && mode != "fill" && mode != "take" && mode != "wait") return "알 수 없는 요리 mode";
            if (second != null && mode != "put") return "요리 second는 put에서만 사용";
            if (mode == "wait" && (s.Count > 0 || item != null)) return "요리 wait은 count/item을 받지 않음";
            if (where != null && !Facility(where, "pot")) return "요리 where는 pot:<ID>@x,y";
        }
        return null;
    }
    public static bool Facility(string text, string prefix)
    {
        if (text == null || !text.StartsWith(prefix + ":", StringComparison.Ordinal)) return false;
        int at = text.IndexOf('@');
        return at > prefix.Length + 1 && Pair(text.Substring(at + 1), out _, out _);
    }
    private static bool Pair(string s, out int x, out int y) { x = y = 0; var p = s.Split(','); return p.Length == 2 && Integer(p[0], out x) && Integer(p[1], out y); }
    private static bool Integer(string s, out int value) => int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    public static bool TryFarmBounds(string where, out int x1, out int y1, out int x2, out int y2)
    {
        x1 = y1 = x2 = y2 = 0;
        if (where == null) return false;
        if (where.StartsWith("tile:", StringComparison.Ordinal)) { if (!Pair(where.Substring(5), out x1, out y1)) return false; x2 = x1; y2 = y1; return true; }
        if (!where.StartsWith("rect:", StringComparison.Ordinal)) return false;
        var p = where.Substring(5).Split(',');
        if (p.Length != 4 || !Integer(p[0], out int a) || !Integer(p[1], out int b) || !Integer(p[2], out int c) || !Integer(p[3], out int d)) return false;
        x1 = Math.Min(a, c); y1 = Math.Min(b, d); x2 = Math.Max(a, c); y2 = Math.Max(b, d); return true;
    }
    public static bool InFarmWhere(string where, int x, int y) => Text(where) == null || where == "farm" || TryFarmBounds(where, out int x1, out int y1, out int x2, out int y2) && x >= x1 && x <= x2 && y >= y1 && y <= y2;
    public static string TileHash(string text)
    {
        unchecked { uint hash = 2166136261; foreach (char c in text ?? "") { hash ^= c; hash *= 16777619; } return hash.ToString("x8", CultureInfo.InvariantCulture); }
    }
}
