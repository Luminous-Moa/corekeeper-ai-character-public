$ErrorActionPreference = 'Stop'
$repo = 'C:/Dev/CoreKeeperModSDK-bag-common'
$sourceCommit = (& git -C $repo rev-parse HEAD).Trim()
$sourceFiles = @('AILife.cs','Core/AILifeTree.cs','AIGoalFeatures.cs','AIInventory.cs','AIInventoryMenu.cs','AIBrain.Loop.cs','AIBrain.cs','AIActions.cs','AIDecisions.cs','Core/AIInventoryRules.cs','Core/AIHostOrders.cs','Core/AIPlanRules.cs','Core/AIGoalKinds.cs','Core/AIRunGate.cs')
$sourceHashes = @($sourceFiles | ForEach-Object { [ordered]@{ path=$_;sha256=(Get-FileHash -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/$_" -Algorithm SHA256).Hash } })
[ordered]@{sourceCommit=$sourceCommit;recordedUtc=[DateTime]::UtcNow.ToString('o');sources=$sourceHashes;limitations='Fake world/DB/physics dispatcher and network; actual menu, Loop Apply/public surface/request JSON, registry, inventory leaf and observations are linked/extracted'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'source-pin.json') -Encoding UTF8
$loop = Get-Content -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs" -Raw -Encoding utf8
$brain = Get-Content -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/AIBrain.cs" -Raw -Encoding utf8
$actions = Get-Content -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/AIActions.cs" -Raw -Encoding utf8
$data = Get-Content -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/AIDecisions.cs" -Raw -Encoding utf8
$features = Get-Content -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/AIGoalFeatures.cs" -Raw -Encoding utf8
function Slice([string]$text,[string]$start,[string]$end) {
    $a=$text.IndexOf($start); if($a -lt 0){throw "Missing $start"}; $b=$text.IndexOf($end,$a);if($b -lt 0){throw "Missing $end"};return $text.Substring($a,$b-$a)
}
$settings = Slice $data 'public class DecisionsSettings' 'public class DecisionsUsage'
$settings = $settings.Substring(0,$settings.LastIndexOf('[Serializable]'))
$apply = Slice $loop '    private static void Apply(World server, Entity player, float now, AICandidate c)' '    // Left/right record'
$surface = Slice $loop '    private static List<AICandidate> InventoryChoiceSurface(' '    // ---- refactor S6a:'
$json = Slice $loop '    private static string BuildJson(' '    private static string Trim('
$pending = Slice $brain '    internal static void CancelPendingChoices(' '    // The goal state and its life'
$actionClass = Slice $actions 'public sealed class AIAction' 'public static class AIActions'
$of = Slice $actions '    private static AIAction Of(' '    // why an answer'
$stale = Slice $actions '    public static string Stale(' '    private static string MineStale('
$inventoryLine = ($actions -split "`n" | Where-Object { $_.Contains('["inventory"]') } | Select-Object -First 1)
if(-not $inventoryLine){throw 'Actual inventory action registry entry missing'}
$generated = "using System;`nusing System.Collections.Generic;`nusing System.Text;`nusing Unity.Entities;`n" + $settings + "`npublic static partial class AIBrain {`n" + $apply+$surface+$json+$pending+"`n}`n" + $actionClass + "`npublic static class AIActions {`nprivate static readonly Dictionary<string, AIAction> Kinds = new Dictionary<string, AIAction> {`n"+$inventoryLine+"`n};`n"+$of+$stale+"`n}`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Production.cs'),$generated,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'FeatureEntry.cs'),"using System;using System.Collections.Generic;using Unity.Entities;public static class AIGoalFeatures {"+(Slice $features '    public static void AddCandidates(' '    public static bool TryDone(')+"}",[Text.UTF8Encoding]::new($false))
& dotnet run --project (Join-Path $PSScriptRoot 'Connected.csproj') -c Release
$code=$LASTEXITCODE
if((& git -C $repo rev-parse HEAD).Trim() -ne $sourceCommit){throw 'Source commit changed during connected validation'}
foreach($record in $sourceHashes){if((Get-FileHash -LiteralPath "$repo/Assets/AIPlayerSpike/Scripts/$($record.path)" -Algorithm SHA256).Hash -ne $record.sha256){throw "Source changed during validation: $($record.path)"}}
exit $code
