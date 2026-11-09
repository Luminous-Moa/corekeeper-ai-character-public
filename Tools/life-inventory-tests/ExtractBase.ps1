$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Assets/AIPlayerSpike/Scripts/AIBase.cs') -Raw -Encoding UTF8
function Extract-Method([string]$signature) {
  $start = $source.IndexOf($signature)
  if ($start -lt 0) { throw "Frozen source method not found: $signature" }
  $brace = $source.IndexOf('{', $start)
  $depth = 1
  $end = $brace + 1
  while ($depth -gt 0) { if ($source[$end] -eq '{') { $depth++ }; if ($source[$end] -eq '}') { $depth-- }; $end++ }
  return $source.Substring($start, $end - $start)
}
$storable = Extract-Method 'public static List<(int slot, ObjectID id, int amount)> Storable('
$stack = (Extract-Method 'private static bool MayStackStored(').Replace('private static bool MayStackStored(', 'public static bool MayStackStored(')
$known = (Extract-Method 'private static bool MayStoreKnownChest(').Replace('private static bool MayStoreKnownChest(', 'public static bool MayStoreKnownChest(')
$target = Extract-Method 'public static ObjectID ChestTarget('
$text = "// Verbatim method slices extracted from frozen AIBase.cs; supporting calls are stubs.`nusing Unity.Entities;`nusing ChestMem = AIBase.ChestMem;`nusing ChestView = AIBase.ChestView;`nusing Unity.Mathematics;`npublic static class ReviewBase {`n" + $storable + "`n" + $target + "`n" + $stack + "`n" + $known + @'

private static bool StorableKind(World w,ObjectID id,int variation)=>AIBase.StorableKind(w,id,variation);
private static ObjectID ChestItem(World w,Entity p)=>ObjectID.Chest;
private static List<AIBase.ChestMem> BaseChests()=>new();
}
public static class AIBrain {public static AIGoal Goal=>AIGoals.Goal;}
'@
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'BaseSourceSlices.cs') -Value $text -Encoding utf8





