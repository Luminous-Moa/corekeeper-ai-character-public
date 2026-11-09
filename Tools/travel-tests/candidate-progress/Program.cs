using System;
using System.Collections.Generic;
public static class CandidateProgressProgram
{
    static int tests, failed;
    static void Check(bool ok,string name) { tests++; if(!ok)failed++; Console.WriteLine((ok?"PASS ":"FAIL ")+name); }
    static void Goal(GoalKind kind)
    {
        Harness.Reset();
        AIGoals.SetGoal(Harness.World,Harness.Player,kind,ObjectID.CopperOre,"copper",20,"llm:test#1");
    }
    static List<AICandidate> Generic() => new List<AICandidate> { CandidateSlices.Walk(),CandidateSlices.Frontier() };
    static void Report(List<AICandidate> page)
    {
        AIExpedition.Compose(Harness.World,Harness.Player,new AIObservation(),page);
        AIGoals.CheckAfterBuild(Harness.World,Harness.Player,1,CandidateSlices.Facts(page));
        AIExpedition.AfterBuild(Harness.World,Harness.Player,page,false);
    }
    public static void Main()
    {
        foreach(var kind in new[]{GoalKind.Gather,GoalKind.Home,GoalKind.Goto})
        {
            Goal(kind);var page=Generic();
            Check(page.Count==2 && page[0].kind=="explore" && page[1].kind=="dig",kind+" retains both general exploration candidates");
            Check(!CandidateSlices.Facts(page).HasProgress,kind+" general exploration does not assert task progress");
        }
        Goal(GoalKind.Explore);var explore=Generic();
        Check(explore.TrueForAll(c=>c.progress)&&CandidateSlices.Facts(explore).HasProgress,"Explore counts both walking and remembered-frontier exploration");
        Goal(GoalKind.Gather);var exhausted=Generic();Report(exhausted);Report(exhausted);
        Check(AIGoals.Observation.Contains("목표 진척 후보 없음")&&AIPlanner.Replans==1,"production frontier candidate -> HasProgress -> premise-loss report once");
        Check(AIGoals.Goal.kind==GoalKind.Gather&&exhausted.Exists(c=>c.key=="frontier"),"report preserves goal and optional exploration");
        Goal(GoalKind.Gather);var remembered=Generic();remembered.Add(CandidateSlices.Ore(ObjectID.CopperOre));Report(remembered);
        Check(CandidateSlices.Facts(remembered).HasProgress&&AIPlanner.Replans==0,"remembered target ore remains a real gathering alternative and suppresses false exhaustion");
        Goal(GoalKind.Gather);var other=Generic();other.Add(CandidateSlices.Ore(ObjectID.Wood));Report(other);
        Check(!CandidateSlices.Facts(other).HasProgress&&AIPlanner.Replans==1,"remembered unrelated material does not suppress target exhaustion");
        Console.WriteLine($"Candidate progress production checks: {tests-failed}/{tests}");Environment.ExitCode=failed;
    }
}
