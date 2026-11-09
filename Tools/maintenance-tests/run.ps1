$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $testRoot '../..')).Path
$generated = Join-Path $testRoot '.generated'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
function Source([string]$name) { Get-Content -LiteralPath (Join-Path $repoRoot "Assets/AIPlayerSpike/Scripts/$name") -Raw -Encoding UTF8 }
function Block([string]$source, [string]$marker) {
    $start = $source.IndexOf($marker)
    if ($start -lt 0) { throw "Production marker missing: $marker" }
    $open = $source.IndexOf('{', $start)
    $depth = 1; $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced production block: $marker" }
    $source.Substring($start, $end - $start)
}
function Slice([string]$source, [string]$startMarker, [string]$endMarker) {
    $start = $source.IndexOf($startMarker); $end = $source.IndexOf($endMarker, $start + 1)
    if ($start -lt 0 -or $end -lt 0) { throw "Production slice missing: $startMarker" }
    $source.Substring($start, $end - $start)
}
function Generated([string]$file, [string]$text) { Set-Content -LiteralPath (Join-Path $generated $file) -Value $text -Encoding utf8 }
$using = "using System;`nusing System.Collections.Generic;`nusing Unity.Entities;`nusing Unity.Mathematics;`nusing UnityEngine;`n"
$data = Source 'AIDecisions.cs'
$classes = 'DecisionsSettings','DecisionsUsage','AIGoal','AICandidate','DProb','DAnswer','DUsage','DResp','DInDetails' | ForEach-Object { Block $data "public class $_" }
# Multiline declarations use a newline immediately after the class name.
Generated 'Data.cs' ($using + ($classes -join "`n"))
$loop = Source 'AIBrain.Loop.cs'
$parts = @(
    (Slice $loop '    private static void HandleResponse(' '    // Left/right record'),
    (Slice $loop '    private static void ApplyOrHold(' '    private static void TryApplyHeld('),
    (Block $loop '    private static void Switch('),
    (Block $loop '    private static bool CostAllows('),
    (Block $loop '    private static List<AICandidate> InventoryChoiceSurface('),
    (Block $loop '    private static bool InventoryMenuControl('),
    (Slice $loop '    public const float ExecutionReviewSeconds' '    public static void Tick(')
)
Generated 'Brain.cs' ($using + "public static partial class AIBrain {`n" + ($parts -join "`n") + "`n}")
$candidates = Source 'AIBrain.Candidates.cs'
Generated 'Candidates.cs' ($using + "public static partial class AIBrain {`n" + (Slice $candidates '    internal static List<AICandidate> AssembleLifePage(' '    private sealed class CandBuild') + "`n}")
$brain = Source 'AIBrain.cs'
Generated 'Ended.cs' ($using + "public static partial class AIBrain {`n" + (Slice $brain '    private static bool SkillRule(' '    public static void HandleGoalCommand(') + "`n}")
$actions = Source 'AIActions.cs'
$farmRow = ($actions -split "`n" | Where-Object { $_.Contains('["maintain"] =') }) -join "`n"
if (!$farmRow) { throw 'Production maintenance dispatch row missing' }
$of = ($actions -split "`n" | Where-Object { $_.Contains('private static AIAction Of(') }) -join "`n"
Generated 'Actions.cs' ($using + (Block $actions 'public sealed class AIAction') + "`npublic static class AIActions {`nprivate static readonly Dictionary<string,AIAction> Kinds = new Dictionary<string,AIAction>{`n" + $farmRow + "`n};`n" + $of + "`n" + (Block $actions '    public static AISkill Make(') + "`n" + (Block $actions '    public static string Stale(') + "`n}")
Generated 'Planner.cs' ($using + "public static partial class AIPlanner {`n" + (Block (Source 'AIPlanner.cs') '    private static void SetGoalFrom(') + "`n}")
Generated 'RunGate.cs' (Source 'Core/AIRunGate.cs')
Generated 'InventoryGoalForm.cs' (Source 'Core/AIInventoryGoalForm.cs')
Generated 'Goals.cs' (Source 'AIGoals.cs')
$normalCreate = Get-Content -LiteralPath 'C:/Dev/_ref/corekeeper-decomp/Pug.Other/Inventory/Create.cs' -Raw -Encoding UTF8
$normalMethods = @('public static InventoryChangeData DropItem(', 'public static InventoryChangeData MoveAmount(', 'public static InventoryChangeData MoveOrDropItem(', 'public static InventoryChangeData Swap(', 'public static CraftActionData Craft(', 'public static CraftActionData RepairOrReinforce(', 'public static CraftActionData Upgrade(', 'public static InventoryChangeData QuickStack(', 'public static InventoryChangeData Sort(', 'public static InventoryChangeData QuickStackToNearbyChests(') | ForEach-Object { Block $normalCreate $_ }
Generated 'NormalCreate.cs' ("using Unity.Entities;`nusing Unity.Mathematics;`nnamespace Inventory { public static class Create {`n" + ($normalMethods -join "`n") + "`n}}")
$sourceRoot = 'C:/Dev/.head/sessions/' + [char]0xCF54 + [char]0xC5B4 + [char]0xD0A4 + [char]0xD37C + '-AI-' + [char]0xCE90 + [char]0xB9AD + [char]0xD130 + '/work/2026-11/2026-11-07-' + [char]0xC0DD + [char]0xD65C + [char]0xD2B8 + [char]0xB9AC + [char]0xAD6C + [char]0xD604 + '/next/queries/'
$repairSource = Get-Content -LiteralPath (Join-Path $sourceRoot 'repair-source.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$upgradeSource = Get-Content -LiteralPath (Join-Path $sourceRoot 'upgrade-source.json') -Raw -Encoding UTF8 | ConvertFrom-Json
@{objectId=$repairSource.value.objectId;variation=$repairSource.value.variation;repairMax=$repairSource.value.authoring.selectedData.DurabilityAuthoring.maxDurability;repairLevel=$repairSource.value.authoring.selectedData.AreaLevelAuthoring.level;samePrimaryAcrossVariation=$repairSource.value.dynamicRepairResolution.samePrefabForEveryInt32Variation;repairOrigin=$repairSource.origin;upgradeOrigin=$upgradeSource.origin;upgrade=$upgradeSource.value.sourceSpecificConditions} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $generated 'source-evidence.json') -Encoding utf8
& dotnet run --project (Join-Path $testRoot 'MaintenanceTests.csproj') -c Release
exit $LASTEXITCODE
