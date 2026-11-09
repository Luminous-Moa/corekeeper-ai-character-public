using System.Linq;

public static class ThreatHistoryTests
{
    public static void Run()
    {
        var a = new AIThreatHistory<int>();
        var b = new AIThreatHistory<int>();
        Program.Check(!a.LastAssessedCannotCome(7), "history: unassessed enemy has no cannot-come record");
        a.Record(7, false);
        Program.Check(a.LastAssessedCannotCome(7) && !b.LastAssessedCannotCome(7), "history: false assessment is recorded only in its own instance");
        a.Record(7, false);
        Program.Check(a.LastAssessedCannotCome(7), "history: repeating a false assessment keeps the record");
        a.Record(7, true);
        Program.Check(!a.LastAssessedCannotCome(7), "history: true assessment removes the previous cannot-come record");
        for (int i = 0; i < 64; i++) a.Record(i, false);
        Program.Check(Enumerable.Range(0, 64).All(a.LastAssessedCannotCome), "history: 64 records remain");
        a.Record(64, false);
        Program.Check(Enumerable.Range(0, 65).All(a.LastAssessedCannotCome), "history: the 65th record does not clear (strictly greater than 64 before insertion)");
        a.Record(64, false);
        Program.Check(!Enumerable.Range(0, 64).Any(a.LastAssessedCannotCome) && a.LastAssessedCannotCome(64), "history: next record clears first, even when repeating an existing key");
        for (int i = 0; i < 64; i++) a.Record(i, false);
        a.Record(999, true);
        Program.Check(!Enumerable.Range(0, 65).Any(a.LastAssessedCannotCome) && !a.LastAssessedCannotCome(999), "history: a true record also applies the size clear before removal");
    }
}
