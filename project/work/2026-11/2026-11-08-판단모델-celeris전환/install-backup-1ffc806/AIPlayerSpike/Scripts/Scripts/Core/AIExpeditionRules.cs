using System;
using System.Collections.Generic;

public sealed class ExpeditionLimit
{
    public int OrderId, Maximum;
    public string Item;
    public long Acquired;
}

// Which observed resource opportunities are worth an automatic upper report when the character leaves a site
// (user agreement 2026-11-07: "관련 자원 기회"; scope decision 2026-11-08). Only the report is filtered: every
// opportunity stays a choice for the action model. Relevant = an item of the current goal/plan or its crafting
// materials, or a valuable kind. Revisit cost is not estimated.
public static class AIReportRelevance
{
    public sealed class ItemFacts
    {
        public string Type = "";      // the game's ObjectType name
        public int Rarity;            // the game's Rarity value (Poor -1, Common 0, Uncommon 1, Rare 2, Epic 3, Legendary 4)
        public string[] Requires = new string[0];   // ObjectID names of the crafting ingredients
    }
    // Game data reader, installed by the runtime (AIPlayerSpikeMod.EarlyInit). Unknown items return null.
    public static Func<string, ItemFacts> Info = _ => null;

    // Valuable kinds — one place, draft for the user's review (2026-11-08):
    //  1. ore items (ObjectID name ending in "Ore": the raw material of every metal tier);
    //  2. ObjectType Valuable (sellable treasures: fossils, trinkets, pearls ...);
    //  3. rarity Rare or higher, except placeable decorations/blocks and non-obtainable objects
    //     (gems such as Ancient Gemstone, boss materials, rare food/plants, rare tools and gear).
    public const int ValuableRarity = 2;
    private static readonly HashSet<string> NotValuableTypes = new HashSet<string>(StringComparer.Ordinal) { "PlaceablePrefab", "NonObtainable" };
    public static string ValuableKind(string item)
    {
        if (string.IsNullOrEmpty(item) || item == "None") return null;
        if (item.EndsWith("Ore", StringComparison.Ordinal)) return "광석";
        var info = Info(item);
        if (info == null) return null;
        if (info.Type == "Valuable") return "귀중품";
        if (info.Rarity >= ValuableRarity && !NotValuableTypes.Contains(info.Type)) return "희귀도 " + info.Rarity;
        return null;
    }

    // The goal/plan items and the ingredients of their recipes (a few levels: armour -> bar -> ore).
    public static HashSet<string> PlanItems(IEnumerable<string> roots, int depth = 3)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new List<string>();
        foreach (var root in roots) if (!string.IsNullOrEmpty(root) && root != "None" && result.Add(root)) frontier.Add(root);
        for (int level = 0; level < depth && frontier.Count > 0; level++)
        {
            var next = new List<string>();
            foreach (var item in frontier)
            {
                var info = Info(item);
                if (info?.Requires == null) continue;
                foreach (var need in info.Requires) if (!string.IsNullOrEmpty(need) && result.Add(need)) next.Add(need);
            }
            frontier = next;
        }
        return result;
    }

    // why: "계획 품목" or the valuable kind; null when the opportunity is not reported automatically. The upper LLM's
    // changes (user decision 2026-11-08) override the draft rule for a kind until it changes them again.
    public static string Why(string item, HashSet<string> planItems, IDictionary<string, bool> valuable = null)
    {
        if (!string.IsNullOrEmpty(item) && planItems != null && planItems.Contains(item)) return "계획 품목";
        if (valuable != null && !string.IsNullOrEmpty(item) && valuable.TryGetValue(item, out bool on)) return on ? "상위 지정 값진 종류" : null;
        return ValuableKind(item);
    }

    // Opportunity facts are "<item>@x,y" (검수 F4: one per resource).
    public static string ItemOf(string fact)
    {
        string f = fact ?? ""; int at = f.IndexOf('@');
        return at < 0 ? f : f.Substring(0, at);
    }
}

// While a corridor run is moving (2026-11-08 이동·전달 개선 항목6): which resource sightings wake the action model and
// which common resources are offered. Goal/plan items and valuable kinds (AIReportRelevance) wake it on a new sighting;
// a common kind is offered only when its own interval has passed and the kind is seen again. Not choosing an offered
// kind lengthens its interval; choosing it restores the first interval. Values are a proposal for the user's review.
public sealed class TravelResourcePacer
{
    public const float FirstInterval = 30f;   // seconds before a common kind is first offered while moving
    public const float Growth = 2f;           // offered but not chosen: interval x Growth
    public const float MaxInterval = 480f;    // ... up to 8 minutes
    public const int SameSightingTiles = 3;   // a relevant item within this many tiles of one already announced is the same sighting
    public const bool ResetOnGoalChange = true;   // a new goal/plan revision restarts every kind at FirstInterval
    private sealed class Kind { public float Interval = FirstInterval, NextAt; public bool Due, Offered; }
    private readonly Dictionary<string, Kind> kinds = new Dictionary<string, Kind>(StringComparer.Ordinal);
    private readonly HashSet<string> learned = new HashSet<string>(StringComparer.Ordinal);
    public const int MaxSightingsPerItem = 256;   // 검수 S4: remembered relevant tiles per item (oldest dropped)
    private readonly Dictionary<string, List<(int x, int y)>> announced = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
    private int revision = int.MinValue;

    public void Sync(int goalRevision)
    {
        if (goalRevision == revision) return;
        bool first = revision == int.MinValue; revision = goalRevision;
        if (!first && ResetOnGoalChange) { kinds.Clear(); announced.Clear(); }
    }
    // A kind that has been an actual gather choice (so a sighting of it can be offered).
    public void Learn(string item) { if (!string.IsNullOrEmpty(item) && item != "None") learned.Add(item); }
    public float IntervalOf(string item) => kinds.TryGetValue(item, out var k) ? k.Interval : FirstInterval;

    // Sightings while moving. Returns the call reasons (empty: no call).
    public List<string> Observe(float now, IEnumerable<(string item, int x, int y)> visible, Func<string, string> relevantWhy)
    {
        var reasons = new List<string>(); var seenCommon = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in visible)
        {
            if (string.IsNullOrEmpty(v.item) || v.item == "None") continue;
            string why = relevantWhy(v.item);
            if (why != null)
            {
                if (!announced.TryGetValue(v.item, out var seen)) announced[v.item] = seen = new List<(int, int)>();
                bool same = seen.Exists(a => Math.Abs(a.x - v.x) + Math.Abs(a.y - v.y) <= SameSightingTiles);
                if (!seen.Contains((v.x, v.y))) { seen.Add((v.x, v.y)); if (seen.Count > MaxSightingsPerItem) seen.RemoveAt(0); }
                if (!same) reasons.Add($"새로 보인 관련 자원 {v.item}({why}) @{v.x},{v.y}");   // the same vein/pile extends the sighting
                continue;
            }
            if (!learned.Contains(v.item) || !seenCommon.Add(v.item)) continue;
            if (!kinds.TryGetValue(v.item, out var k)) { kinds[v.item] = new Kind { NextAt = now + FirstInterval }; continue; }
            if (!k.Due && now >= k.NextAt) { k.Due = true; reasons.Add($"흔한 자원 {v.item} 다시 보임(간격 {k.Interval:F0}초 지남)"); }
        }
        return reasons;
    }
    // Whether a common kind may be in the choices while moving. An unseen kind starts its interval now.
    public bool Offerable(string item, float now)
    {
        if (!kinds.TryGetValue(item, out var k)) { kinds[item] = new Kind { NextAt = now + FirstInterval }; return false; }
        return k.Due;
    }
    public void Sent(IEnumerable<string> offeredItems)
    {
        foreach (var item in offeredItems) if (item != null && kinds.TryGetValue(item, out var k) && k.Due) k.Offered = true;
    }
    public void Answered(float now, string chosenItem)
    {
        foreach (var pair in kinds)
        {
            var k = pair.Value; if (!k.Offered) continue;
            k.Interval = pair.Key == chosenItem ? FirstInterval : Math.Min(MaxInterval, k.Interval * Growth);
            k.NextAt = now + k.Interval; k.Due = false; k.Offered = false;
        }
    }
}

// The upper LLM's valuable-kind changes on disk (user decision 2026-11-08): per character and world, next to
// travel-memory.json as valuable-kinds.json: {"version":1,"kinds":{"CopperOre":false,"TinOre":true}}. A missing,
// unreadable or other-version file means no changes (the default rule: ores incl. copper/tin, Valuable, Rare+).
public static class ValuableStore
{
    public const int Version = 1;
    public const string FileName = "valuable-kinds.json";
    private static readonly System.Text.RegularExpressions.Regex Entry =
        new System.Text.RegularExpressions.Regex("\"([A-Za-z][A-Za-z0-9_]{0,63})\"\\s*:\\s*(true|false)");
    public static string Write(IDictionary<string, bool> kinds)
    {
        var keys = new List<string>(kinds.Keys); keys.Sort(StringComparer.Ordinal);
        var parts = new List<string>(); foreach (var k in keys) parts.Add("\"" + k + "\":" + (kinds[k] ? "true" : "false"));
        return "{\"version\":" + Version + ",\"kinds\":{" + string.Join(",", parts) + "}}";
    }
    public static bool TryRead(string text, out Dictionary<string, bool> kinds)
    {
        kinds = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();
        if (!t.StartsWith("{") || !t.EndsWith("}") || !System.Text.RegularExpressions.Regex.IsMatch(t, "\"version\"\\s*:\\s*" + Version + "\\s*,")) return false;
        int k = t.IndexOf("\"kinds\"", StringComparison.Ordinal); if (k < 0) return false;
        int open = t.IndexOf('{', k), close = t.IndexOf('}', open < 0 ? k : open);
        if (open < 0 || close < 0) return false;
        string body = t.Substring(open + 1, close - open - 1).Trim();
        if (body.Length == 0) return true;
        int matched = 0;
        foreach (System.Text.RegularExpressions.Match m in Entry.Matches(body)) { kinds[m.Groups[1].Value] = m.Groups[2].Value == "true"; matched++; }
        if (matched != body.Split(',').Length) { kinds.Clear(); return false; }   // a damaged entry: treat the file as broken
        return true;
    }
}

// 토큰 절감 ③ (2026-11-08): compact texts of the expedition record.
public static class AIFieldText
{
    // The closing sentence of every discovery observation of a gather group; written once where results are joined.
    public const string DiscoveryTail = " 현재 선택 묶음에는 추가하지 않음. 원래 작업을 계속하면서 다음 현장 판단에 제공.";
    public const int ResultObservations = 3, ResultChars = 240;
    // The last result: the run's end and its last few observations, the repeated tail once, bounded length.
    public static string Result(string name, string reason, IList<string> observations)
    {
        var last = new List<string>(); bool tail = false;
        for (int i = Math.Max(0, observations.Count - ResultObservations); i < observations.Count; i++)
        {
            string o = observations[i] ?? "";
            if (o.Contains(DiscoveryTail)) { tail = true; o = o.Replace(DiscoveryTail, ""); }
            last.Add(o);
        }
        int skipped = Math.Max(0, observations.Count - ResultObservations);
        string text = name + ": " + reason + (skipped > 0 ? "; 앞선 관측 " + skipped + "건 생략" : "") + (last.Count > 0 ? "; " + string.Join(" / ", last) : "") + (tail ? DiscoveryTail : "");
        return text.Length <= ResultChars ? text : text.Substring(0, ResultChars) + "…";
    }
    // The chosen work as a short name (the full description is in the choices).
    public static string Choice(string kind, string item, int x, int y, int resources)
    {
        string what = kind == "gather_group" ? "묶음 채집" : kind == "mine" || kind == "edgemat" ? "캐기" : kind == "root" ? "뿌리 캐기"
            : kind == "break" ? "부수기" : kind == "pickup" || kind == "edgepickup" ? "줍기" : kind == "farm" ? "수확" : kind == "dig" ? "파들어가기" : kind;
        return what + " " + item + (resources > 1 ? " " + resources + "곳" : " (" + x + "," + y + ")");
    }
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
