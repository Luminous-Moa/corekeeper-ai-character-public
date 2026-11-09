using System;
using System.Collections.Generic;
using System.Linq;

public static class ApproachSearchTests
{
    public static void Run()
    {
        var calls = new List<int>();
        var hit = new object();
        int success = -1;
        object Attempt(int tile, out string why) { calls.Add(tile); why = "tried " + tile; return tile == success ? hit : null; }
        var result = AIApproachSearch.FirstOfThree<int, object>(new List<int>(), Attempt, out int stand, out string why);
        Program.Check(result == null && calls.Count == 0 && stand == 0 && why == "설 수 있는 아는 칸이 없음", "approach: empty list keeps the initial reason and default stand");
        var stands = new List<int> { 10, 20, 30, 40 };
        success = 10;
        result = AIApproachSearch.FirstOfThree<int, object>(stands, Attempt, out stand, out why);
        Program.Check(ReferenceEquals(result, hit) && calls.SequenceEqual(new[] { 10 }) && stand == 10 && why == "tried 10", "approach: first success stops further attempts");
        calls.Clear(); success = 30;
        result = AIApproachSearch.FirstOfThree<int, object>(stands, Attempt, out stand, out why);
        Program.Check(ReferenceEquals(result, hit) && calls.SequenceEqual(new[] { 10, 20, 30 }) && stand == 30 && why == "tried 30", "approach: third succeeds after exactly two failures");
        calls.Clear(); success = 40;
        result = AIApproachSearch.FirstOfThree<int, object>(stands, Attempt, out stand, out why);
        Program.Check(result == null && calls.SequenceEqual(new[] { 10, 20, 30 }) && stand == 30 && why == "tried 30", "approach: fourth is not tried; last attempted stand/reason survive failure");
        calls.Clear();
        result = AIApproachSearch.FirstOfThree<int, object>(new[] { 7, 8 }, Attempt, out stand, out why);
        Program.Check(result == null && calls.SequenceEqual(new[] { 7, 8 }) && stand == 8 && why == "tried 8", "approach: short lists stop at their own length");
        var expected = new InvalidOperationException("attempt failed");
        object Throw(int tile, out string note) { note = "before throw"; throw expected; }
        bool propagated = false;
        try { AIApproachSearch.FirstOfThree<int, object>(stands, Throw, out _, out _); }
        catch (InvalidOperationException e) { propagated = ReferenceEquals(e, expected); }
        Program.Check(propagated, "approach: attempt exceptions propagate unchanged");
    }
}
