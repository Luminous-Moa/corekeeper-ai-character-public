// AIChatLog — the world's recent talk the upper LLM reads (자연어 명령, 2026-11-05 합의 0-합의.md). Unity-free
// (Tools/core-tests). One log per hosted world, shared by every AI character:
//   - the host's chat lines that mention a member ("@Claude 구리 캐 와", "@모두 …"); a line without a mention is ordinary
//     chat between people and is not kept,
//   - every AI character's say (what the upper LLM answered, shown as "[Name] …").
// Each report carries the last few lines (ReportJson): who spoke, to whom, how long ago, the text, and whether the
// line is new since this character's previous report. An AI's say never calls another character's LLM by itself; the
// others read it in their next report (periodic, about two minutes).
using System.Collections.Generic;
using System.Text;

public sealed class ChatLine
{
    public int Seq;
    public float At;            // seconds (the caller's clock, Time.realtimeSinceStartup in the mod)
    public string FromId;       // "host" or a member id
    public string From;         // shown name (호스트 / Claude …)
    public List<string> To;     // member ids the line names (empty for an AI's say); all members for @모두
    public bool ToAll;
    public string Text;
}

public sealed class AIChatLog
{
    public const int Keep = 40;
    public const int InReport = 10;
    private readonly List<ChatLine> _lines = new List<ChatLine>();
    private int _seq;
    public int LastSeq => _seq;
    public IReadOnlyList<ChatLine> Lines => _lines;

    public ChatLine Add(float at, string fromId, string from, List<string> to, bool toAll, string text)
    {
        var l = new ChatLine { Seq = ++_seq, At = at, FromId = fromId, From = from, To = to ?? new List<string>(), ToAll = toAll, Text = text ?? "" };
        _lines.Add(l);
        while (_lines.Count > Keep) _lines.RemoveAt(0);
        return l;
    }

    public void Clear() { _lines.Clear(); }

    // The "chat" array of a report for member meId: the last InReport lines, oldest first. seenSeq: the last line this
    // member's previous report carried (lines after it are "new"). esc: the report's string escaper.
    public string ReportJson(float now, string meId, int seenSeq, System.Func<string, string> esc, System.Func<string, string> nameOf)
    {
        var sb = new StringBuilder("[");
        int from = System.Math.Max(0, _lines.Count - InReport);
        for (int i = from; i < _lines.Count; i++)
        {
            var l = _lines[i];
            if (i > from) sb.Append(',');
            string to = l.ToAll ? "모두" : l.To.Count == 0 ? "" : string.Join(",", l.To.ConvertAll(id => nameOf(id)));
            bool me = l.FromId == meId;
            bool toMe = l.ToAll || l.To.Contains(meId);
            sb.Append("{\"from\":\"").Append(esc(me ? l.From + "(나)" : l.From)).Append('"');
            if (to.Length > 0) sb.Append(",\"to\":\"").Append(esc(to)).Append('"');
            if (toMe && !me && l.FromId == "host") sb.Append(",\"to_me\":true");
            sb.Append(",\"ago_s\":").Append(System.Math.Max(0, (int)(now - l.At)));
            if (l.Seq > seenSeq && !me) sb.Append(",\"new\":true");
            sb.Append(",\"text\":\"").Append(esc(l.Text)).Append("\"}");
        }
        sb.Append(']');
        return sb.ToString();
    }
}

// What the chat window shows (합의 4). Say from the upper LLM, errors and limits, and answers to the host's /ai
// commands stay; the program's own status lines (skill ended, goal set/done/stopped, down, self-defence, "thinking",
// free time, the upper LLM's goal) go to the log only, and to the chat as well when the host turned verbose on.
public enum ChatKind { Say, Error, Command, Status }

public static class ChatPolicy
{
    public static bool Show(ChatKind k, bool verbose) => k != ChatKind.Status || verbose;

    // The game font has no arrows and a few other signs (실게임2 N5: "투구→갑옷" showed as "투구?갑옷"): every chat line
    // the mod shows (say included) is written with signs the font has.
    public static string FontSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '→': case '⇒': case '➜': case '➔': case '➡': sb.Append('>'); break;   // → ⇒ ➜ ➔ ➡
                case '←': case '⇐': case '⬅': sb.Append('<'); break;                       // ← ⇐ ⬅
                case '↔': sb.Append("<>"); break;                                                   // ↔
                case '…': sb.Append("..."); break;                                                  // …
                case '–': case '—': sb.Append('-'); break;                                  // – —
                case '“': case '”': sb.Append('"'); break;                                 // “ ”
                case '‘': case '’': sb.Append('\''); break;                             // ‘ ’
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}

// The same kind of error or limit notice is shown at most once per character in five minutes (실게임2 N6: the action
// judge's HTTP 503 retries showed 8 lines in 21 minutes); the rest go to the log only.
public sealed class ChatRepeatGate
{
    public const float Gap = 300f;
    private readonly System.Collections.Generic.Dictionary<string, float> _last = new System.Collections.Generic.Dictionary<string, float>();
    public int Hidden { get; private set; }
    public bool Allow(string key, float now)
    {
        if (_last.TryGetValue(key, out float t) && now - t < Gap) { Hidden++; return false; }
        _last[key] = now;
        return true;
    }
}
