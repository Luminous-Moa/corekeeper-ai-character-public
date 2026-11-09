$ErrorActionPreference='Stop'
$repo= (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$source=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repo 'Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs') -Raw
$generated=Join-Path $PSScriptRoot '.generated'
New-Item -ItemType Directory -Force $generated | Out-Null
# Preserve production initializer fields through desc. Descriptive prose depends on
# game-world formatting; it does not affect purpose/progress or report suppression.
function Slice([string]$list){
 $mark=$list+'.Add(new AICandidate {'
 $start=$source.IndexOf($mark)
 if($start -lt 0){throw "Missing candidate producer $list"}
 $start=$source.IndexOf('new AICandidate {',$start)
 $end=$source.IndexOf('desc =',$start)
 if($end -lt 0){throw "Missing candidate description $list"}
 return $source.Substring($start,$end-$start).TrimEnd().TrimEnd(',')+' };'
}
$walk=Slice 'lExplore';$frontier=Slice 'lDigExplore';$ore=Slice 'lRemember'
$loop=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repo 'Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs') -Raw
$progress=[regex]::Match($loop,'HasProgress\s*=\s*cands\.Exists\(c => c\.progress\)')
if(!$progress.Success){throw 'HasProgress production predicate changed'}
$header=@'
using System;
using System.Collections.Generic;
using Unity.Mathematics;
public static class CandidateSlices {
 static AIGoal Goal=>AIGoals.Goal;
 public static AICandidate Walk(){var e=(t:new int2(8,0),dir:"east");string k="walk";return 
'@
$text=$header+$walk+@'
 }
 public static AICandidate Frontier(){var f=(t:new int2(12,0),dir:"east");string k="frontier";bool isWall=true;return 
'@+$frontier+@'
 }
 public static AICandidate Ore(ObjectID id){var r=(p:new int2(20,0),o:(ore:id,seenUnix:0L));string k="ore";bool gatherOre=Goal.kind==GoalKind.Gather;var wanted=new HashSet<ObjectID>{Goal.item};return 
'@+$ore+@'
 }
 public static GoalBuildFacts Facts(List<AICandidate> cands)=>new GoalBuildFacts {
'@+$progress.Value+@'
 ,GatherFacts="observed target resource exhausted",BlockNote="no known task route"};
}
'@
Set-Content -Encoding UTF8 -LiteralPath (Join-Path $generated 'CandidateSlices.cs') -Value $text
& powershell -NoProfile -File (Join-Path $repo 'Tools/execution-tests/run.ps1')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& dotnet run --project (Join-Path $PSScriptRoot 'CandidateProgressTests.csproj') -c Release
exit $LASTEXITCODE

