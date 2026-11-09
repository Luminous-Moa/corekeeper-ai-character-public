using Unity.Entities;
using Unity.Mathematics;

public static class Program
{
    public static int Checks, Failed;
    public static void Check(bool ok,string message)
    { Checks++; Console.WriteLine((ok?"PASS ":"FAIL ")+message); if(!ok) Failed++; }
    public static int Main()
    {
        Console.WriteLine("Maintenance production pipeline adapter");
        Run("repair pipeline",Cases.Repair);
        Run("upgrade pipeline",Cases.Upgrade);
        Run("strict queue",BoundaryCases.Queue);
        Run("exact boundaries",BoundaryCases.Exact);
        Run("repair outcomes",BoundaryCases.PartialRepair);
        Run("active gear capacity",BoundaryCases.Capacity);
        Run("stop recovery",RecoveryCases.Stops);
        Run("unapplied barriers",RecoveryCases.Barriers);
        Run("observation boundaries",RecoveryCases.Observation);
        Run("actual navigation",NavigationCases.Run);
        Run("fixed source evidence",SourceEvidence.Run);
        Run("pending identity regression",RecoveryCases.IdentityRegression);
        Run("world and owner isolation",BoundaryCases.Isolation);
        Run("integrated rendered view ownership",IntegrationCases.RenderedOwner);
        Run("integrated direct and fallback surface",IntegrationCases.DirectAndFallback);
        Run("integrated inventory slot barriers",IntegrationCases.InventoryBarriers);
        Run("integrated pending recovery and lifecycle",IntegrationCases.RecoveryAndLifecycle);
        Run("gap M3 repair beside a forge pending",GapCases.RepairBesidePending);
        Run("gap M1 walk to an unseen own forge",GapCases.WalkToUnseenForge);
        Run("gap M2 give up a lost pending",GapCases.GiveUpLostPending);
        Run("chest open mode: only the chest's work, close, hit, gone",ChestModeCases.Run);
        Run("review-5 G5: aux arrival and base area progress",GapCases.AuxArrival);
        Console.WriteLine($"MAINTENANCE CHECKS: {Checks-Failed} passed, {Failed} failed");
        return Failed;
    }
    public static void Run(string name,Action test){try{test();}catch(Exception e){Checks++;Failed++;Console.WriteLine("FAIL "+name+": "+e);}}
}
public enum EdgeCandidateRole { None }

// Surrounding feature producers are outside this selected pipeline.
public static class AIFarm
{public sealed class Selection{} public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new();}
// the bed branch (AIBed) has its own adapter in Tools/life-tests/BedCases.cs
public static class AIBed
{public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new();}
public static class AICook
{public sealed class Selection{} public static List<AILife.Entry> LifeEntries(World w,Entity p,AIObservation o,string key,LifeSelection s)=>new();}

public static partial class AIBody
{
    public static int MainEnd(EntityManager em,Entity p,int length)=>em.HasBuffer<InventoryBuffer>(p)?Math.Min(length,em.GetBuffer<InventoryBuffer>(p)[0].startIndex+em.GetBuffer<InventoryBuffer>(p)[0].size):Math.Min(40,length);
    public static float3 Pos(World w,Entity p)=>w.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(p).Position;
    public static int2 TileOf(float3 p)=>new((int)MathF.Round(p.x),(int)MathF.Round(p.z));
    public static float2 DirTo(float3 me,float2 p)=>new(p.x-me.x,p.y-me.z);
    public static Dictionary<ObjectID,int> InventoryCounts(World w,Entity p)
    {var result=new Dictionary<ObjectID,int>();for(int i=AIInventory.MainStart(w,p);i<AIInventory.MainEnd(w,p);i++)if(AIInventory.Read(w,p,i,out var x)&&x.objectID!=ObjectID.None){result.TryGetValue(x.objectID,out int n);result[x.objectID]=n+AIInventory.Quantity(x);}return result;}
    public static string InventoryDiff(Dictionary<ObjectID,int> before,Dictionary<ObjectID,int> after)=>string.Join(",",after.Select(x=>x.Key+":"+(x.Value-before.GetValueOrDefault(x.Key))));
}
