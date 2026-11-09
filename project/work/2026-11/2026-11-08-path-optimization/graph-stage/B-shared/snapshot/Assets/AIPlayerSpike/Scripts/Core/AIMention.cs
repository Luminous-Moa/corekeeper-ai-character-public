// AIMention — "@name" mentions in the host's chat (자연어 명령, 2026-11-05 합의 0-합의.md). Unity-free (Tools/core-tests).
// - Parse: which members a chat line names with "@…" (or "@모두" for every member). A line without a mention is
//   ordinary chat and goes to nobody. A Korean particle right after the name still names it ("@클로드야" -> Claude):
//   the longest alias that starts the token wins.
// - Complete: the candidates for the "@…" being typed at the end of the input line (one letter is enough), and the
//   line with the first candidate filled in (Tab in the chat box).
using System;
using System.Collections.Generic;
using System.Text;

public sealed class MentionName
{
    public string Id;        // member id (claude, gpt, …) or "all"
    public string Display;   // what completion writes after "@" (the in-game name, or 모두)
    public string[] Aliases; // every way to write it (lower-case compare)
    public MentionName(string id, string display, params string[] aliases) { Id = id; Display = display; Aliases = aliases; }
}

public static class AIMention
{
    public const string AllId = "all";
    public static readonly MentionName All = new MentionName(AllId, "모두", "모두", "다들", "all", "everyone");

    // The member table as mention names: in-game name, id and the Korean/CLI names AIAgent.Find knows.
    public static List<MentionName> Members(IEnumerable<(string id, string name)> members)
    {
        var l = new List<MentionName>();
        foreach (var (id, name) in members)
        {
            var al = new List<string> { name, id };
            switch (id)
            {
                case "claude": al.Add("클로드"); break;
                case "gpt": al.AddRange(new[] { "챗지피티", "지피티", "codex" }); break;
                case "gemini": al.AddRange(new[] { "제미나이", "제미니" }); break;
                case "grok": al.Add("그록"); break;
            }
            l.Add(new MentionName(id, name, al.ToArray()));
        }
        return l;
    }

    private static bool TokenChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    // The member ids the line mentions, in the order of the table (no repeats). all: "@모두" was used (then every
    // member is in the list). Empty when the line has no mention.
    public static List<string> Parse(string text, IList<MentionName> members, out bool all)
    {
        all = false;
        var hit = new HashSet<string>();
        if (string.IsNullOrEmpty(text)) return new List<string>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '@') continue;
            if (i > 0 && TokenChar(text[i - 1])) continue;   // an e-mail like a@b is not a mention
            int j = i + 1;
            while (j < text.Length && TokenChar(text[j])) j++;
            string tok = text.Substring(i + 1, j - i - 1).ToLowerInvariant();
            if (tok.Length == 0) continue;
            var m = Match(tok, members);
            if (m == null) continue;
            if (m.Id == AllId) all = true; else hit.Add(m.Id);
        }
        var r = new List<string>();
        foreach (var m in members) if (all || hit.Contains(m.Id)) r.Add(m.Id);
        return r;
    }

    // The name a token starts with (the longest alias), "모두" included; null if none.
    private static MentionName Match(string tok, IList<MentionName> members)
    {
        MentionName best = null; int len = 0;
        foreach (var m in Concat(members))
            foreach (var a in m.Aliases)
            {
                string al = a.ToLowerInvariant();
                if (al.Length <= len || !tok.StartsWith(al, StringComparison.Ordinal)) continue;
                // an English alias needs a word end: "@claude야" yes (a Korean particle), "@allright" / "@claudex" no
                // (검수 작은 지적 6). A Korean alias keeps the prefix match ("@클로드야").
                if (tok.Length > al.Length && IsAscii(al) && IsAsciiWord(tok[al.Length])) continue;
                best = m; len = al.Length;
            }
        return best;
    }

    private static bool IsAscii(string s) { foreach (char c in s) if (c > 127) return false; return true; }
    private static bool IsAsciiWord(char c) => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-');

    private static IEnumerable<MentionName> Concat(IList<MentionName> members)
    {
        foreach (var m in members) yield return m;
        yield return All;
    }

    // The "@partial" being typed at the end of the line: start = index of '@', partial without '@'. false when the
    // end of the line is not inside a mention or nothing follows the '@' yet (one letter is needed).
    public static bool Typing(string input, out int start, out string partial)
    {
        start = -1; partial = null;
        if (string.IsNullOrEmpty(input)) return false;
        int j = input.Length;
        int i = j - 1;
        while (i >= 0 && TokenChar(input[i])) i--;
        if (i < 0 || input[i] != '@') return false;
        if (i > 0 && TokenChar(input[i - 1])) return false;
        if (i == j - 1) return false;
        start = i; partial = input.Substring(i + 1);
        return true;
    }

    // Candidates for the mention being typed: members whose any alias starts with it (table order), then 모두.
    public static List<MentionName> Candidates(string input, IList<MentionName> members)
    {
        var r = new List<MentionName>();
        if (!Typing(input, out _, out string p)) return r;
        string lp = p.ToLowerInvariant();
        foreach (var m in Concat(members))
            foreach (var a in m.Aliases)
                if (Prefix(a.ToLowerInvariant(), lp)) { r.Add(m); break; }
        return r;
    }

    // alias starts with partial, Hangul-aware for the last typed letter: while the IME is composing, the input line
    // holds "ㅋ" (a lone consonant) or "크" (a syllable still missing its final consonant) on the way to "클".
    public static bool Prefix(string alias, string partial)
    {
        if (partial.Length == 0 || partial.Length > alias.Length) return false;
        int n = partial.Length - 1;
        if (string.CompareOrdinal(alias, 0, partial, 0, n) != 0) return false;
        char p = partial[n], a = alias[n];
        if (p == a) return true;
        if (!IsSyllable(a)) return false;
        int ai = a - 0xAC00, aCho = ai / 588, aJung = ai % 588 / 28;
        if (p >= 0x3131 && p <= 0x314E) return ChoOf(p) == aCho;   // lone consonant = the syllable's first consonant
        if (IsSyllable(p))
        {
            int pi = p - 0xAC00;
            return pi % 28 == 0 && pi / 588 == aCho && pi % 588 / 28 == aJung;   // no final consonant yet
        }
        return false;
    }

    private static bool IsSyllable(char c) => c >= 0xAC00 && c <= 0xD7A3;

    // Compatibility jamo (ㄱ U+3131 …) -> the initial-consonant index of a syllable (ㄱ0 ㄲ1 ㄴ2 … ㅎ18); -1 if not one.
    private static readonly string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private static int ChoOf(char j) => Cho.IndexOf(j);

    // The line with the mention being typed replaced by "@<Display> ". null if nothing to complete.
    public static string Complete(string input, IList<MentionName> members)
    {
        var c = Candidates(input, members);
        if (c.Count == 0 || !Typing(input, out int start, out _)) return null;
        return input.Substring(0, start) + "@" + c[0].Display + " ";
    }

    // The hint line shown below the chat input box (실게임2 N5), font-safe letters only: "@Claude  @ChatGPT  (Tab)". null when there is nothing to show.
    public static string Hint(string input, IList<MentionName> members)
    {
        var c = Candidates(input, members);
        if (c.Count == 0) return null;
        var sb = new StringBuilder();
        foreach (var m in c) { if (sb.Length > 0) sb.Append("  "); sb.Append('@').Append(m.Display); }
        sb.Append("   (Tab)");
        return sb.ToString();
    }
}
