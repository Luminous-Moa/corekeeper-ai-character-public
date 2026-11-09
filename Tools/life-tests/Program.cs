using System;
public static class Program
{
    public static int Passed,Failed;
    public static void Check(bool condition,string message) { Console.WriteLine($"[{(condition?"OK":"FAIL")}] {message}");if(condition)Passed++;else Failed++; }
    private static void BaseLifeContext() { var f=FarmFixture.Create(6,4,6); AIPlanner.SetStep(f.World,f.Player,new PlanStep("take","SeedA",1,"base")); AILife.ObserveContext(f.World,f.Player); AIInventory.AtBaseContext=false; var q=new LifeSelection{Branch="inventory",Mode="take",Item="SeedA",Where="chest:100,0"}; Check(!AILife.Resolve(q).MatchesGoal,"production life binding never credits remote chest as explicitbase"); AIInventory.AtBaseContext=true; Check(AILife.Resolve(q).MatchesGoal,"production life binding accepts producer confirmed anchor chest context"); AIPlanner.SetStep(f.World,f.Player,new PlanStep("take","SeedA",1)); AIInventory.AtBaseContext=false; Check(AILife.Resolve(q).MatchesGoal,"unspecified chest location still matches all allowed owned chests"); }
    public static int Main()
    {
        FarmCases.Run(); BedCases.Run(); BrainCases.Run(); BaseLifeContext();
        Console.WriteLine($"LIFE CHECKS: {Passed} passed, {Failed} failed");return Failed==0?0:1;
    }
}

