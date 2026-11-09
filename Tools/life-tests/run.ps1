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
    (Slice $loop '    public const float ExecutionReviewSeconds' '    public static void Tick(')
)
Generated 'Brain.cs' ($using + "public static partial class AIBrain {`n" + ($parts -join "`n") + "`n}")
$candidates = Source 'AIBrain.Candidates.cs'
Generated 'Candidates.cs' ($using + "public static partial class AIBrain {`n" + (Slice $candidates '    internal static List<AICandidate> AssembleLifePage(' '    private sealed class CandBuild') + "`n}")
$brain = Source 'AIBrain.cs'
Generated 'Ended.cs' ($using + "public static partial class AIBrain {`n" + (Slice $brain '    private static bool SkillRule(' '    public static void HandleGoalCommand(') + "`n}")
$actions = Source 'AIActions.cs'
$farmRow = ($actions -split "`n" | Where-Object { $_.Contains('["farm"] =') }) -join "`n"
if (!$farmRow) { throw 'Production farm dispatch row missing' }
$of = ($actions -split "`n" | Where-Object { $_.Contains('private static AIAction Of(') }) -join "`n"
Generated 'Actions.cs' ($using + (Block $actions 'public sealed class AIAction') + "`npublic static class AIActions {`nprivate static readonly Dictionary<string,AIAction> Kinds = new Dictionary<string,AIAction>{`n" + $farmRow + "`n};`n" + $of + "`n" + (Block $actions '    public static AISkill Make(') + "`n" + (Block $actions '    public static string Stale(') + "`n}")
Generated 'Planner.cs' ($using + "public static partial class AIPlanner {`n" + (Block (Source 'AIPlanner.cs') '    private static void SetGoalFrom(') + "`n}")
Generated 'RunGate.cs' (Source 'Core/AIRunGate.cs')
Generated 'InventoryGoalForm.cs' (Source 'Core/AIInventoryGoalForm.cs')
Generated 'Goals.cs' (Source 'AIGoals.cs')
$gather = Source 'AIGathering.cs'
$harvest = @(
    (Block $gather '    public static string HarvestCandidateLimit('),
    (Block $gather '    public static string HarvestLimit('),
    (Block $gather '    private static string UnknownDestruction('),
    (($gather -split "`n" | Where-Object { $_.Contains('private static bool HasExplicitLimit =>') }) -join "`n")
)
Generated 'GatherHarvestLimit.cs' ($using + "public static partial class AIGathering {`n" + ($harvest -join "`n") + "`n}")
Generated 'ExpeditionLimit.cs' (Block (Source 'Core/AIExpeditionRules.cs') 'public sealed class ExpeditionLimit')
& dotnet run --project (Join-Path $testRoot 'LifeTests.csproj') -c Release
exit $LASTEXITCODE
