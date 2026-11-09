using System.Text.Json;

public static class SourceEvidence
{
    public static void Run()
    {
        string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".generated","source-evidence.json"));
        using var document=JsonDocument.Parse(File.ReadAllText(path));var evidence=document.RootElement;
        Program.Check(evidence.GetProperty("objectId").GetInt32()==8555&&evidence.GetProperty("variation").GetInt32()==0&&evidence.GetProperty("repairMax").GetInt32()==90&&evidence.GetProperty("repairLevel").GetInt32()==6,"fixed supplied source 8555:0 evidence matches fixture primary durability ninety and prefab level six");
        Program.Check(evidence.GetProperty("samePrimaryAcrossVariation").GetBoolean(),"fixed repair source documents selected primary stability across variation rather than a per-level durability formula");
        var upgrade=evidence.GetProperty("upgrade");var costs=upgrade.GetProperty("costsForSourceVariation");Boundary.Reset();
        Program.Check(upgrade.GetProperty("nextLevelForSourceVariation").GetInt32()==7&&upgrade.GetProperty("costIndexForSourceVariation").GetInt32()==7&&costs[0].GetProperty("item").GetInt32()==(int)Boundary.UpgradeCosts[0].id&&costs[0].GetProperty("amount").GetInt32()==Boundary.UpgradeCosts[0].need&&costs[1].GetProperty("item").GetInt32()==(int)Boundary.UpgradeCosts[1].id&&costs[1].GetProperty("amount").GetInt32()==Boundary.UpgradeCosts[1].need,"public helper boundary returns the provided source's level-seven IronBar ten and AncientCoin132 rows, not an invented cost formula");
        Console.WriteLine("BOUNDARY: fixed source rows and real public call arguments are checked; the native game cost helper and consumer are not executed by this adapter.");
    }
}
