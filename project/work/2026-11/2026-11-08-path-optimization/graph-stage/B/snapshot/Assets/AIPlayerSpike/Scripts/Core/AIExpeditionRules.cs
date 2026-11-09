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
    public int GoalRevision, CursorRevision;
}

public sealed class ExpeditionCursor
{
    public bool Open;
    public string Branch = "", Item = "";
    public int Revision;
    public string Check(ExpeditionNav n, string owner, string world, int goalRevision)
    {
        if (n == null || n.Owner != owner || n.World != world || n.GoalRevision != goalRevision || n.CursorRevision != Revision) return "원정 탐색 맥락이 바뀜";
        if (n.Op == "open") return Open ? "이미 열린 원정 목록" : null;
        if (!Open) return "원정 목록이 닫힘";
        if (n.Op == "branch") return n.Value == "gather" || n.Value == "travel" ? null : "알 수 없는 원정 갈래";
        if (n.Op == "item") return string.IsNullOrEmpty(Branch) || string.IsNullOrEmpty(n.Value) ? "품목 선택 경로가 없음" : null;
        return n.Op == "back" || n.Op == "root" || n.Op == "close" ? null : "알 수 없는 원정 탐색";
    }
    public void Apply(ExpeditionNav n)
    {
        switch (n.Op)
        {
            case "open": Open = true; Branch = Item = ""; break;
            case "close": Open = false; Branch = Item = ""; break;
            case "root": Branch = Item = ""; break;
            case "branch": Branch = n.Value; Item = ""; break;
            case "item": Item = n.Value; break;
            case "back": if (Item != "") Item = ""; else Branch = ""; break;
        }
        Revision++;
    }
}
