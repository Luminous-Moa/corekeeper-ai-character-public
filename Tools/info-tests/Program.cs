using System;
using System.Collections.Generic;
using System.Text.Json;

static class Program
{
    static void Assert(bool value, string why) { if (!value) throw new Exception(why); Console.WriteLine("PASS " + why); }
    static void Main()
    {
        var chat = new AIChatLog();
        for (int i = 0; i < 70; i++) chat.Add(i, "host", "호스트", new List<string> { "claude" }, false, "message" + i);
        using (var j = JsonDocument.Parse(chat.ReportJson(80, "claude", 0, s => s, s => s)))
        {
            Assert(chat.Lines.Count == 70 && j.RootElement.GetArrayLength() == 70, "70 chat lines retained and sent");
            Assert(j.RootElement[0].GetProperty("text").GetString() == "message0", "oldest pending chat remains");
        }
        var book = new ExpBook();
        for (int i = 0; i < 30; i++) book.Add(new ExpRun {run="r"+i,key="gather:Item"+i,source="llm",count=200,got=200,outcome="completed",play1=i,secs=20});
        var report = ExpReport.Build(book, new ExpCond(), 40, "gather:Item29", new[] { "gather:Item29" });
        using (var j = JsonDocument.Parse(report))
        {
            Assert(j.RootElement.GetProperty("recent").GetArrayLength() == 30, "all 30 runs survive old recent6 limit");
            Assert(j.RootElement.GetProperty("by_goal").GetArrayLength() == 30, "all 30 keys survive old maxKeys6 limit, including noncandidate history");
            Assert(report.Length > 2200 && report.Contains("Item0") && report.Contains("Item29"), "full report beyond old 2200 chars includes first and last");
        }
        Assert(ExpRun.Trim(new string('x', 1000), 120).Length == 1000, "experience evidence text remains complete");
        var fights = new AIFightLog();
        for (int i = 0; i < 20; i++) { var f = new FightRecord {At=i, MaxHp=100, EndHp=100}; f.Enemies["Enemy"+i]=1; fights.Add(f); }
        using (var j = JsonDocument.Parse(fights.ReportJson(5000, s => s))) Assert(j.RootElement.GetArrayLength() == 20, "all twenty fights remain after the former ten-minute cutoff");
    }
}
