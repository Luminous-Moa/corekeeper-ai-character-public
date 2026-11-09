[Console]::OutputEncoding = [Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
$evidence = Join-Path $folder 'evidence'
$rows = @()
foreach ($name in @('20261108-080811-1-Player.log', '20261108-080811-2-Player-prev.log')) {
    $lines = Get-Content -LiteralPath (Join-Path $evidence $name) -Encoding UTF8
    $frontiers = @($lines | Where-Object { $_ -match 'frontier search:.*offered 2179' })
    if ($frontiers.Count -ne 1) { throw "Expected one final frontier line in $name" }
    $line = $frontiers[0]
    $matches = [regex]::Matches($line, '\((-?\d+),(-?\d+)\) cost \d+ nov \d+')
    $targets = @($matches | ForEach-Object { $_.Groups[1].Value + ',' + $_.Groups[2].Value })
    if ($targets.Count -ne 2179) { throw "Unexpected target count $($targets.Count)" }
    $unique = [Collections.Generic.HashSet[string]]::new()
    foreach ($target in $targets) { [void]$unique.Add($target) }
    $cache = [Collections.Generic.Dictionary[string,int]]::new()
    $phases = @()
    # Source AIBrain.Loop.cs 1110-1123: use the most optimistic case, every cached route valid forever.
    # Replay only PlanC cache lookup/store. No Unity, path search, model calls, game process or elapsed-time simulation.
    for ($phase = 1; $phase -le 2; $phase++) {
        $miss = 0; $hit = 0; $clears = 0
        foreach ($target in $targets) {
            if ($cache.ContainsKey($target)) { $hit++ }
            else {
                $miss++
                if ($cache.Count -gt 64) { $cache.Clear(); $clears++ }
                $cache[$target] = 1
            }
        }
        $phases += @{ phase = $phase; requests = $targets.Count; hits = $hit; misses = $miss; clears = $clears; final_cache_count = $cache.Count }
    }
    $retained = [Collections.Generic.HashSet[string]]::new()
    foreach ($target in $targets) { [void]$retained.Add($target) }
    $retainedHits = 0
    foreach ($target in $targets) { if ($retained.Contains($target)) { $retainedHits++ } }
    $rows += @{ file = $name; unique_targets = $unique.Count; phases = $phases; same_build_retained_second_pass_hits = $retainedHits }
}
$out = @{ purpose = 'Exact cache-policy replay with captured target order; no game/pathfinding/hang reproduction'; source = 'AIBrain.Loop.cs:1110-1123, AIBrain.Candidates.cs:689-700 and1157-1163'; caveat = 'The second pass is source-derived, not observed from a stack trace; additional dig candidates, time expiry and edge pass omitted'; results = $rows }
$json = $out | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $folder 'cache-replay.json'), $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
