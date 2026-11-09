using System;
using System.Collections.Generic;

public sealed class ExpeditionLimit
{
    public int OrderId, Maximum;
    public string Item;
    public long Acquired;
}

// Facts and navigation only. The model retains every choice about side work, completion and return.
public sealed class ExpeditionLedger
{
    private readonly Dictionary<string, long> acquired = new Dictionary<string, long>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> previous = new Dictionary<string, int>(StringComparer.Ordinal);
    private bool observed;
    private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> reviewed = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<int, List<string>> reports = new Dictionary<int, List<string>>();
    public long Acquired(string item) => acquired.TryGetValue(item, out long n) ? n : 0;
    public void ObserveInventory(IDictionary<string, int> inventory, bool gathering)
    {
        foreach (var kv in inventory)
        {
            previous.TryGetValue(kv.Key, out int before);
            if (observed && gathering && kv.Value > before)
                acquired[kv.Key] = Acquired(kv.Key) + (long)kv.Value - before;
        }
        previous.Clear(); foreach (var kv in inventory) previous[kv.Key] = kv.Value;
        observed = true;
    }
    public bool ShouldReport(string fact) => !string.IsNullOrEmpty(fact) && !reviewed.Contains(fact) && reported.Add(fact);
    public bool Reviewed(string fact) => reviewed.Contains(fact);
    public void Sent(int id, IEnumerable<string> facts) { reports[id] = new List<string>(facts); }
    public void Accepted(int id)
    {
        if (!reports.TryGetValue(id, out var facts)) return;
        foreach (var fact in facts) reviewed.Add(fact);
        reports.Remove(id);
    }
}

public sealed class ExpeditionNav
{
    public string Owner, World, Op, Value;
    public string Lower, Upper;
    public int GoalRevision, CursorRevision;
}

public sealed class ExpeditionCursor
{
    public bool Open;
    public string Branch = "", Item = "";
    public int Revision;
    public readonly List<ExpeditionRange> Ranges = new List<ExpeditionRange>();
    private readonly List<ExpeditionRange> itemParents = new List<ExpeditionRange>();
    public string Check(ExpeditionNav n, string owner, string world, int goalRevision)
    {
        if (n == null || n.Owner != owner || n.World != world || n.GoalRevision != goalRevision || n.CursorRevision != Revision) return "원정 탐색 맥락이 바뀜";
        if (n.Op == "open") return Open ? "이미 열린 원정 목록" : null;
        if (!Open) return "원정 목록이 닫힘";
        if (n.Op == "branch") return n.Value == "gather" || n.Value == "travel" ? null : "알 수 없는 원정 갈래";
        if (n.Op == "item") return string.IsNullOrEmpty(Branch) || string.IsNullOrEmpty(n.Value) ? "품목 선택 경로가 없음" : null;
        if (n.Op == "range") return string.IsNullOrEmpty(Branch) || string.IsNullOrEmpty(n.Lower) || string.IsNullOrEmpty(n.Upper)
            || StringComparer.Ordinal.Compare(n.Lower, n.Upper) > 0 ? "대상 범위가 없음" : null;
        return n.Op == "back" || n.Op == "root" || n.Op == "close" ? null : "알 수 없는 원정 탐색";
    }
    public void Apply(ExpeditionNav n)
    {
        switch (n.Op)
        {
            case "open": Open = true; Branch = Item = ""; Ranges.Clear(); itemParents.Clear(); break;
            case "close": Open = false; Branch = Item = ""; Ranges.Clear(); itemParents.Clear(); break;
            case "root": Branch = Item = ""; Ranges.Clear(); itemParents.Clear(); break;
            case "branch": Branch = n.Value; Item = ""; Ranges.Clear(); itemParents.Clear(); break;
            case "item": Item = n.Value; itemParents.Clear(); itemParents.AddRange(Ranges); Ranges.Clear(); break;
            case "range": Ranges.Add(new ExpeditionRange(n.Lower, n.Upper)); break;
            case "back":
                if (Ranges.Count > 0) Ranges.RemoveAt(Ranges.Count - 1);
                else if (Item != "") { Item = ""; Ranges.AddRange(itemParents); itemParents.Clear(); }
                else Branch = "";
                break;
        }
        Revision++;
    }
}

public sealed class ExpeditionRange
{
    public readonly string Lower, Upper;
    public ExpeditionRange(string lower, string upper) { Lower = lower; Upper = upper; }
    public bool Contains(string key) => StringComparer.Ordinal.Compare(key, Lower) >= 0 && StringComparer.Ordinal.Compare(key, Upper) <= 0;
}

// Observed identity only. A target becomes an executable choice after its normal producer checks the current route.
public sealed class ExpeditionTarget
{
    public readonly string Key, Branch, Item, Description, Order;
    public readonly int X, Y;
    public readonly object Source;
    public ExpeditionTarget(string key, string branch, string item, int x, int y, string description, object source = null)
    {
        Key = key; Branch = branch; Item = item; X = x; Y = y; Description = description; Source = source;
        Order = ((long)x - int.MinValue).ToString("X8") + ((long)y - int.MinValue).ToString("X8") + ":" + key;
    }
}

public sealed class ExpeditionBrowseEntry
{
    public string Op, Value, Text, Lower, Upper, Order;
    public ExpeditionTarget Target;
}

// A single build snapshots the existing cursor, catalogs cheap identities, then prepares only its visible leaves.
// Range boundaries use stable identities, so a changed observation cannot silently retarget a navigation response.
public sealed class ExpeditionQuery
{
    public readonly bool Open;
    public readonly string Branch, Item;
    public readonly int Revision;
    public readonly string Owner, World;
    public readonly int GoalRevision;
    private readonly ExpeditionRange range;
    private readonly Dictionary<string, ExpeditionTarget> targets = new Dictionary<string, ExpeditionTarget>(StringComparer.Ordinal);
    private readonly HashSet<string> prepared = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ExpeditionBrowseEntry> entries = new List<ExpeditionBrowseEntry>();
    public IEnumerable<ExpeditionTarget> Targets => targets.Values;
    public IReadOnlyList<ExpeditionBrowseEntry> Entries => entries;
    public int Count => targets.Count;
    public int MaxLeaves { get; private set; } = 255;
    public ExpeditionQuery(ExpeditionCursor cursor, string owner = "", string world = "", int goalRevision = 0)
    {
        Open = cursor.Open; Branch = cursor.Branch; Item = cursor.Item; Revision = cursor.Revision;
        Owner = owner; World = world; GoalRevision = goalRevision;
        if (cursor.Ranges.Count > 0) range = cursor.Ranges[cursor.Ranges.Count - 1];
    }
    public void Add(string key, string branch, string item, int x, int y, string description, object source = null)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("A target needs its stable candidate key", nameof(key));
        if (branch != "gather" && branch != "travel") throw new ArgumentException("Unknown expedition branch", nameof(branch));
        if (!targets.ContainsKey(key)) targets.Add(key, new ExpeditionTarget(key, branch, item ?? "", x, y, description ?? key, source));
    }
    public bool Contains(string key) => key != null && targets.ContainsKey(key);
    public bool ShouldPrepare(string key) => key != null && prepared.Contains(key);
    public void Seal(int maxLeaves)
    {
        if (maxLeaves < 2) throw new ArgumentOutOfRangeException(nameof(maxLeaves), "Navigation needs room for two lossless subranges");
        MaxLeaves = maxLeaves;
        entries.Clear(); prepared.Clear();
        if (!Open || Branch == "") return;
        var rows = new List<ExpeditionBrowseEntry>();
        var items = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets.Values)
        {
            if (target.Branch != Branch) continue;
            ExpeditionBrowseEntry row;
            if (Item == "")
            {
                if (!items.Add(target.Item)) continue;
                row = new ExpeditionBrowseEntry { Op = "item", Value = target.Item, Order = target.Item, Text = "목록에서 " + target.Item + " 살펴보기" };
            }
            else
            {
                if (target.Item != Item) continue;
                row = new ExpeditionBrowseEntry { Target = target, Order = target.Order, Text = target.Description };
            }
            if (range == null || range.Contains(row.Order)) rows.Add(row);
        }
        rows.Sort((a, b) => StringComparer.Ordinal.Compare(a.Order, b.Order));
        if (rows.Count > maxLeaves)
        {
            int half = rows.Count / 2;
            AddRange(rows, 0, half);
            AddRange(rows, half, rows.Count - half);
            return;
        }
        entries.AddRange(rows);
        foreach (var entry in entries) if (entry.Target != null) prepared.Add(entry.Target.Key);
    }
    private void AddRange(List<ExpeditionBrowseEntry> rows, int start, int count)
    {
        var first = rows[start]; var last = rows[start + count - 1];
        string firstName = first.Target == null ? first.Value : $"({first.Target.X},{first.Target.Y})";
        string lastName = last.Target == null ? last.Value : $"({last.Target.X},{last.Target.Y})";
        entries.Add(new ExpeditionBrowseEntry { Op = "range", Lower = first.Order, Upper = last.Order,
            Value = first.Order.Length + ":" + first.Order + last.Order,
            Text = $"{firstName}부터 {lastName}까지 대상 {count}개 펼치기 — 현재 관측 목록 범위, 실행 조건은 선택한 대상에서 확인" });
    }
}
