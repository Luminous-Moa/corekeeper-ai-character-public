// B1 (refactor final review 10-최종검수.md): AIPlanReport.Esc must leave no raw U+0000-U+001F in the report JSON.
// AIPlanReport uses Unity types and is not linked here, so this file holds a copy of Esc. The first check guards the
// copy: it compares the method text of the copy with AIPlanReport.cs (comment lines and whitespace ignored), so the
// copy cannot drift from the mod. Then: report-shaped JSON built with the copy from text with \t, \r, \u0001 (as the
// S1 reader decodes them) parses with Node's JSON.parse (what bridge.mjs does with the body) and System.Text.Json;
// the rf-S7 Esc (OldEsc) fails on the same text; and normal strings give the same bytes as the rf-S7 Esc.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

public static class ReportEscTests
{
    // ---- copy of AIPlanReport.Esc (must match the source text; checked below) ----
    private static string Esc(string s)
    {
        var t = (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
        for (int i = 0; i < t.Length; i++)
            if (t[i] < ' ')
            {
                var c = t.ToCharArray();
                for (int j = i; j < c.Length; j++) if (c[j] < ' ') c[j] = ' ';
                return new string(c);
            }
        return t;
    }

    // AIPlanReport.Esc at rf-S7 (before B1)
    static string OldEsc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");

    public static void Run()
    {
        string src = Path.Combine(Repo.Root, "Assets", "AIPlayerSpike", "Scripts", "AIPlanReport.cs");
        string self = Path.Combine(Repo.Root, "Tools", "core-tests", "ReportEscTests.cs");
        string a = MethodText(File.ReadAllText(src)), b = MethodText(File.ReadAllText(self));
        Program.Check(a != null && a == b, "the Esc copy here matches AIPlanReport.Esc (source text)");

        // what the S1 reader gives for "Copper\tOre", "Copper\rOre", "Copper\u0001Ore"
        var plan = ExpJson.Parse("{\"item\":\"Copper\\tOre\",\"cr\":\"Copper\\rOre\",\"u\":\"Copper\\u0001Ore\"}") as Dictionary<string, object>;
        string tab = ExpJson.GetS(plan, "item"), cr = ExpJson.GetS(plan, "cr"), u1 = ExpJson.GetS(plan, "u");
        Program.Check(tab == "Copper\tOre" && cr == "Copper\rOre" && u1 == "Copper\u0001Ore", "the reader decodes \\t, \\r, \\u0001 to control characters (B1 premise)");
        string bad = $"모르는 품목 '{tab}' / '{cr}' / '{u1}' / \"따옴표\" \\ 끝\r\n";
        var ctl = new StringBuilder("all:"); for (char ch = '\0'; ch < ' '; ch++) ctl.Append(ch);

        string fixedJson = Report(Esc, "gather " + tab, ctl.ToString(), bad);
        string oldJson = Report(OldEsc, "gather " + tab, ctl.ToString(), bad);
        bool noCtl = true; foreach (char ch in fixedJson) if (ch < ' ') noCtl = false;
        Program.Check(noCtl, "report built from \\t, \\r, \\u0001 and all of U+0000-U+001F has no raw control character");
        Program.Check(StjOk(fixedJson), "that report parses with System.Text.Json");
        string node = NodeParse(fixedJson);
        Program.Check(node == "ok", $"that report parses with Node JSON.parse ({node})");
        string nodeOld = NodeParse(oldJson);
        Program.Check(nodeOld.StartsWith("bad"), $"the rf-S7 Esc on the same text fails Node JSON.parse ({nodeOld})");
        Program.Check(Esc("a\tb\rc\u0001d\ne") == "a b c d e", "control characters become one space each, like \\n");

        // normal text: same bytes as before
        var normal = new[] { "", null, "CopperOre 3", "구리 광석 3개 모으기", "그는 \"이동\"이라고\n말함", "C:\\path\\x", "자유 시간 시작(목표 없음)", "ü é 🙂 ~ \u007f" };
        bool same = true;
        foreach (var s in normal) if (Esc(s) != OldEsc(s)) same = false;
        Program.Check(same, "strings without control characters escape exactly as the rf-S7 Esc");
        Program.Check(Report(Esc, "구리 광석", "목표 끝", "모르는 품목 'Coper'") == Report(OldEsc, "구리 광석", "목표 끝", "모르는 품목 'Coper'"), "a normal report is byte-identical to rf-S7");
    }

    // the report fields that carry LLM- or host-derived text (AIPlanReport.ReportJson: goal.text, events, last_answer_rejected)
    static string Report(Func<string, string> esc, string goalText, string ev, string rejected) =>
        $"{{\"trigger\":\"retry\",\"goal\":{{\"source\":\"{esc("llm")}\",\"text\":\"{esc(goalText)}\"}},\"why_ended\":\"{esc("")}\",\"events\":[\"{esc(ev)}\"],\"last_answer_rejected\":\"{esc(rejected)}\"}}";

    static bool StjOk(string json) { try { using (JsonDocument.Parse(json)) return true; } catch { return false; } }

    static string NodeParse(string json)
    {
        try
        {
            var psi = new ProcessStartInfo("node", "-e \"let s='';process.stdin.setEncoding('utf8');process.stdin.on('data',d=>s+=d).on('end',()=>{try{JSON.parse(s);console.log('ok')}catch(e){console.log('bad: '+e.message)}})\"")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            };
            using (var p = Process.Start(psi))
            {
                p.StandardInput.Write(json); p.StandardInput.Close();
                string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
                return o;
            }
        }
        catch (Exception e) { return "node not run: " + e.Message; }
    }

    // text of "private static string Esc(string s)" up to its closing "    }", comment lines and whitespace dropped
    static string MethodText(string file)
    {
        var lines = file.Replace("\r\n", "\n").Split('\n');
        int i = Array.FindIndex(lines, l => l.Trim() == "private static string Esc(string s)");
        if (i < 0) return null;
        var sb = new StringBuilder();
        for (; i < lines.Length; i++)
        {
            string t = lines[i].Trim();
            if (!t.StartsWith("//")) foreach (char ch in t) if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            if (lines[i] == "    }") return sb.ToString();
        }
        return null;
    }
}
