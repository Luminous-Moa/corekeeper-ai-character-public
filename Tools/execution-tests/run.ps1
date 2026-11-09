param([switch]$Baseline)
$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $testRoot '../..')).Path
$generated = Join-Path $testRoot '.generated'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
$dataSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIDecisions.cs') -Raw -Encoding utf8
function Get-ClassText([string]$source, [string]$name) {
    $start = $source.IndexOf("public class $name`r`n")
    if ($start -lt 0) { $start = $source.IndexOf("public class $name`n") }
    if ($start -lt 0) { throw "Class $name was not found" }
    $open = $source.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced class $name" }
    return $source.Substring($start, $end - $start)
}
$classes = 'DecisionsSettings','DecisionsUsage','AIGoal','AICandidate' | ForEach-Object { Get-ClassText $dataSource $_ }
$data = "using System;`nusing System.Collections.Generic;`nusing Unity.Entities;`nusing Unity.Mathematics;`n" + ($classes -join "`n")
Set-Content -LiteralPath (Join-Path $generated 'Data.cs') -Value $data -Encoding utf8
$loop = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs') -Raw -Encoding utf8
$applyStart = $loop.IndexOf('    private static void Apply(World server, Entity player, float now, AICandidate c)')
$applyEnd = $loop.IndexOf('    // Left/right record', $applyStart)
if ($applyStart -lt 0 -or $applyEnd -lt 0) { throw 'Actual Apply method markers changed' }
$apply = "using Unity.Entities;`npublic static partial class AIBrain {`n" + $loop.Substring($applyStart, $applyEnd - $applyStart) + "`n}"
$observeStart = $loop.IndexOf('    public const float ExecutionReviewSeconds')
$observeEnd = $loop.IndexOf('    public static void Tick(', $observeStart)
if ($observeStart -lt 0 -or $observeEnd -lt 0) { throw 'Actual ObserveExecution method markers changed' }
$observe = $loop.Substring($observeStart, $observeEnd - $observeStart)
$apply = "using System;`n" + $apply.Substring(0, $apply.LastIndexOf('}')) + $observe + "`n}"
Set-Content -LiteralPath (Join-Path $generated 'Apply.cs') -Value $apply -Encoding utf8
$surfaceStart = $loop.IndexOf('    private static List<AICandidate> InventoryChoiceSurface(')
$surfaceEnd = $loop.IndexOf('    // ---- refactor S6a:', $surfaceStart)
$jsonStart = $loop.IndexOf('    private static string BuildJson(')
$jsonEnd = $loop.IndexOf('    private static string Trim(', $jsonStart)
if ($surfaceStart -lt 0 -or $surfaceEnd -lt 0 -or $jsonStart -lt 0 -or $jsonEnd -lt 0) { throw 'Actual menu surface/request methods changed' }
$surface = "using System;`nusing System.Collections.Generic;`nusing System.Text;`nusing Unity.Entities;`npublic static partial class AIBrain {`n" + $loop.Substring($surfaceStart, $surfaceEnd - $surfaceStart) + $loop.Substring($jsonStart, $jsonEnd - $jsonStart) + "`n}"
$budgetStart = $loop.IndexOf('    private static bool CallBudgetOk(')
$budgetEnd = $loop.IndexOf('    // ------------------------------------------------------------------ motion boundaries', $budgetStart)
if ($budgetStart -lt 0 -or $budgetEnd -lt 0) { throw 'Actual decision budget methods changed' }
$surface = $surface.Substring(0, $surface.LastIndexOf('}')) + $loop.Substring($budgetStart, $budgetEnd - $budgetStart) + "`n}"
Set-Content -LiteralPath (Join-Path $generated 'MenuSurface.cs') -Value $surface -Encoding utf8
$bodySource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIBody.cs') -Raw -Encoding utf8
$stopStart = $bodySource.IndexOf('    public static void Stop(World server, Entity player, float now, string reason, bool hard = false)')
$stopEnd = $bodySource.IndexOf('    private static bool IsDown(', $stopStart)
$endStart = $bodySource.IndexOf('    private static void EndSkill(')
$endEnd = $bodySource.IndexOf('    // Test-only commands', $endStart)
if ($stopStart -lt 0 -or $stopEnd -lt 0 -or $endStart -lt 0 -or $endEnd -lt 0) { throw 'Actual body method markers changed' }
$body = "using System;`nusing Unity.Entities;`nusing Unity.Mathematics;`nusing Unity.Transforms;`npublic static partial class AIBody {`n" + $bodySource.Substring($stopStart, $stopEnd - $stopStart) + $bodySource.Substring($endStart, $endEnd - $endStart) + "`n}"
Set-Content -LiteralPath (Join-Path $generated 'Body.cs') -Value $body -Encoding utf8
$guardSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/Core/AIBaseGuard.cs') -Raw -Encoding utf8
$guardStart = $guardSource.IndexOf('    public enum TailOnStop')
$guardEnd = $guardSource.IndexOf('    public static readonly string[] Kinds', $guardStart)
if ($guardStart -lt 0 -or $guardEnd -lt 0) { throw 'Actual tail-stop rule markers changed' }
$guard = "public static class AIBaseGuard {`n" + $guardSource.Substring($guardStart, $guardEnd - $guardStart) + "`npublic static string AddReason(string a, string b) => a + b;`n}"
Set-Content -LiteralPath (Join-Path $generated 'Guard.cs') -Value $guard -Encoding utf8
$actionSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIActions.cs') -Raw -Encoding utf8
$staleStart = $actionSource.IndexOf('    public static string Stale(World server, Entity player, AICandidate c)')
$staleEnd = $actionSource.IndexOf('    private static string MineStale(', $staleStart)
if ($staleStart -lt 0 -or $staleEnd -lt 0) { throw 'Actual Stale method markers changed' }
$stale = "using Unity.Entities;`npublic static partial class AIActions {`n" + $actionSource.Substring($staleStart, $staleEnd - $staleStart) + "`n}"
Set-Content -LiteralPath (Join-Path $generated 'Stale.cs') -Value $stale -Encoding utf8
$brainSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIBrain.cs') -Raw -Encoding utf8
$endedStart = $brainSource.IndexOf('    private static bool SkillRule(')
$endedEnd = $brainSource.IndexOf('    public static void HandleGoalCommand(', $endedStart)
$dropStart = $brainSource.IndexOf('    internal static void CancelPendingChoices(')
$dropEnd = $brainSource.IndexOf('    // The goal state and its life', $dropStart)
if ($endedStart -lt 0 -or $endedEnd -lt 0 -or $dropStart -lt 0 -or $dropEnd -lt 0) { throw 'Actual skill-ended callback markers changed' }
$ended = "using Unity.Entities;`nusing Unity.Mathematics;`npublic static partial class AIBrain {`n" + $brainSource.Substring($endedStart, $endedEnd - $endedStart) + $brainSource.Substring($dropStart, $dropEnd - $dropStart) + "`n}"
Set-Content -LiteralPath (Join-Path $generated 'Ended.cs') -Value $ended -Encoding utf8
$plannerSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIPlanner.cs') -Raw -Encoding utf8
$answerTypes = ($plannerSource -split "`n" | Where-Object { $_.StartsWith('[Serializable] public class Plan') }) -join "`n"
$planner = "using System;`nusing System.Text;`nusing System.Collections.Generic;`nusing Unity.Entities;`nusing UnityEngine;`nusing UnityEngine.Networking;`n" + $answerTypes + "`npublic static partial class AIPlanner {`n"
foreach ($markers in @(
    @('    public static void AddEvent(', '    public static void Enable('),
    @('    public static void OnGoalEnded(', '    // Back up after a down:'),
    @('    public static bool ReplanPending', '    // ------------------------------------------------------------------ per tick'),
    @('    private static void SetGoalFrom(', '    // What the answer does with the host'),
    @('    private static List<HostOrder> OnOrder(', '    // null when the answer can be applied.')))
{
    $a = $plannerSource.IndexOf($markers[0]); $b = $plannerSource.IndexOf($markers[1], $a)
    if ($a -lt 0 -or $b -lt 0) { throw 'Actual planner continuation/request markers changed' }
    $planner += $plannerSource.Substring($a, $b - $a)
}
$nextLine = ($plannerSource -split "`n" | Where-Object { $_.Contains('if (S._planNext) { S._planNext = false; StartNextStep(') })
if ($nextLine.Count -ne 1) { throw 'Actual next-step scheduler changed' }
$planner += "public static void TickNextStep(World server, Entity player, float now) {`n" + $nextLine + "`n}`n}"
$pendingLine = ($plannerSource -split "`n" | Where-Object { $_.Contains('if (_pendingTrigger != null && now >= _retryAt)') })
if ($pendingLine.Count -ne 1) { throw 'Actual pending report scheduler changed' }
$reportSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Assets/AIPlayerSpike/Scripts/AIPlanReport.cs') -Raw -Encoding utf8
$reportLines = $reportSource -split "`n" | Where-Object { $_.Contains('sb.Append(') -and ($_.Contains('expedition_observation') -or $_.Contains('_events.ConvertAll') -or $_.Contains('Orders.ReportFields') -or $_.Contains('last_answer_rejected')) }
if ($reportLines.Count -ne 4) { throw 'Actual expedition/event/order/rejection report fields changed' }
$planner = $planner.Substring(0, $planner.LastIndexOf('}')) + "public static void TickPending(World server, Entity player, float now) { if (_req == null) {`n" + $pendingLine + "`n} }`nprivate static string ReportFields() { var sb = new StringBuilder(); float nowT = Time.realtimeSinceStartup;`n" + ($reportLines -join "`n") + "`nreturn sb.ToString().TrimEnd(','); }`n}"
$orderApplyLines = $plannerSource -split "`n" | Where-Object {
    $_.Contains('bool refusedAll = ') -or $_.Contains('var followed = OnOrder(') -or
    $_.Contains('_rejected = completionConflict == null') -or $_.Contains('ReportCompletionConflict(server, player, completionConflict, completionConflictKey);')
}
if ($orderApplyLines.Count -ne 4) { throw 'Actual rejected-plan order application changed' }
$planner = $planner.Substring(0, $planner.LastIndexOf('}')) + "public static List<HostOrder> ApplyOrderBoundaryForTest(World server, Entity player, float now, PlanAnswerJ a, int cut, string cutNote, StaleAction act = StaleAction.Apply) {`n" + ($orderApplyLines[0..1] -join "`n") + "`nif (refusedAll) {`n" + ($orderApplyLines[2..3] -join "`n") + "`n}`nreturn followed; }`n}"
$bindStart = $plannerSource.IndexOf('        var carry = new List<HostOrder>(followed);')
$bindEnd = $plannerSource.IndexOf('        Applied++;', $bindStart)
if ($bindStart -lt 0 -or $bindEnd -lt 0) { throw 'Actual accepted-plan ownership and application markers changed' }
$planner = $planner.Substring(0, $planner.LastIndexOf('}')) + "public static void ApplyPlanForTest(World server, Entity player, float now, List<PlanStep> steps, List<HostOrder> followed, string src) { var a = new {why = `"fixture accepted reasoning`", decision = `"set`"};`n" + $plannerSource.Substring($bindStart,$bindEnd-$bindStart) + "`n}`n}"
Set-Content -LiteralPath (Join-Path $generated 'Planner.cs') -Value $planner -Encoding utf8
if ($Baseline) {
    $baselineGoals = & git -C $repoRoot show 11eb461:Assets/AIPlayerSpike/Scripts/AIGoals.cs
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read baseline AIGoals source' }
    Set-Content -LiteralPath (Join-Path $generated 'BaselineGoals.cs') -Value $baselineGoals -Encoding utf8
    $baselineOrders = & git -C $repoRoot show 11eb461:Assets/AIPlayerSpike/Scripts/Core/AIHostOrders.cs
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read baseline host orders source' }
    Set-Content -LiteralPath (Join-Path $generated 'BaselineHostOrders.cs') -Value $baselineOrders -Encoding utf8
    & dotnet run --project (Join-Path $testRoot 'ExecutionTests.csproj') -c Release -p:Baseline=true
} else {
    & dotnet run --project (Join-Path $testRoot 'ExecutionTests.csproj') -c Release -p:Baseline=false
}
exit $LASTEXITCODE

