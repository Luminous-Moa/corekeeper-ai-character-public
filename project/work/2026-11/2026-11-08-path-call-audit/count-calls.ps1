[Console]::OutputEncoding = [Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
$evidence = Join-Path (Split-Path $PSScriptRoot -Parent) '2026-11-08-gemini-freeze-diagnosis/evidence'
$results = @()
foreach ($name in @('20261108-080811-1-Player.log','20261108-080811-2-Player-prev.log')) {
    $lines = Get-Content -LiteralPath (Join-Path $evidence $name) -Encoding UTF8
    $send = @($lines | Where-Object { $_ -match 'DECISION #1 SEND' })
    if ($send.Count -ne 1) { throw 'Expected one first SEND' }
    $options = @($send[0] -split ' \| c\d+ ' | Select-Object -Skip 1)
    $wall = @($options | Where-Object { $_ -match '^파 들어가기 — 보이는 목표 광석 ' })
    $object = @($options | Where-Object { $_ -match '^파 들어가기 — 부술 물체 ' })
    $ore = @($options | Where-Object { $_ -match '^기억한 광석 쪽으로 ' })
    $dig = $wall.Count + $object.Count + $ore.Count
    $targets = @()
    foreach ($s in $wall + $ore) {
        $m = [regex]::Match($s, '\((-?\d+),(-?\d+)\)')
        if (!$m.Success) { throw 'Missing coordinate' }
        $targets += $m.Groups[1].Value + ',' + $m.Groups[2].Value
    }
    foreach ($s in $object) {
        $m = [regex]::Match($s, '옆 \((-?\d+),(-?\d+)\)')
        if (!$m.Success) { throw 'Missing object stand coordinate' }
        $targets += $m.Groups[1].Value + ',' + $m.Groups[2].Value
    }
    $unique = [Collections.Generic.HashSet[string]]::new()
    foreach ($target in $targets) { [void]$unique.Add($target) }
    $frontier = @($lines | Where-Object { $_ -match 'frontier search:.*offered 2179' })[0]
    $fTargets = [regex]::Matches($frontier, '\((-?\d+),(-?\d+)\) cost \d+ nov \d+')
    $overlap = 0
    foreach ($m in $fTargets) { if ($unique.Contains($m.Groups[1].Value + ',' + $m.Groups[2].Value)) { $overlap++ } }
    $failedOre = @($lines | Where-Object { $_ -match 'BRAIN: blocked targets: 기억한 광석 \(32,8\)' })
    if ($failedOre.Count -lt 1) { throw 'Expected the failed remembered ore evidence' }
    $remembered = $ore.Count + 1
    $firstPhase = $wall.Count + 4 * $object.Count + $remembered
    $sum = $firstPhase + $dig
    if ($sum -ne 707 -or $options.Count -ne 452) { throw 'Unexpected observed counts' }
    $results += @{
        file = $name; total_choices = $options.Count
        visible_wall_dig = $wall.Count; enclosed_object_dig = $object.Count
        successful_remembered_ore = $ore.Count; failed_remembered_ore = 1
        generation_plan_requests = $firstPhase; final_dig_risk_plan_requests = $dig
        source_derived_total = $sum; observed_misses = 707; observed_hits = 0
        final_dig_distinct_coordinates = $unique.Count
        frontier_destinations = $fTargets.Count; frontier_overlap_with_first_dig = $overlap
        formula = "$($wall.Count) + 4*$($object.Count) + $remembered + $dig = $sum"
    }
}
$json = @{ results = $results; caveat = 'Callsite attribution reconstructed from final descriptions plus source and the sole failed ore. No per-call stack trace exists. Frontier overlap compares targets from separate builds, not total same-build uniqueness.' } | ConvertTo-Json -Depth 7
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'call-counts.json'),$json,[Text.UTF8Encoding]::new($false))
Write-Output $json
