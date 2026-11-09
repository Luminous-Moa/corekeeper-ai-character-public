param([switch]$Baseline)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
function ProductionSource([string]$file) {
 if($Baseline){
  $value=& git -C $repoRoot show "e1cedcf:Assets/AIPlayerSpike/Scripts/$file"
  if($LASTEXITCODE -ne 0){throw 'Could not read pre-fix production source'}
  return ($value -join "`n")
 }
 return Get-Content -LiteralPath (Join-Path $repoRoot "Assets/AIPlayerSpike/Scripts/$file") -Raw -Encoding utf8
}
$source=ProductionSource 'AITunnel.cs'
$start=$source.IndexOf('public class TunnelSkill : AISkill')
if($start -lt 0){throw 'Actual TunnelSkill class marker changed'}
$imports=($source -split "`n" | Where-Object {$_.StartsWith('using ')}) -join "`n"
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot '.generated') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $PSScriptRoot '.generated/TunnelSkill.cs') -Value ($imports+"`n"+$source.Substring($start)) -Encoding utf8
foreach($file in @('AIMineSkill.cs','AIBreakSkills.cs')){Set-Content -LiteralPath (Join-Path $PSScriptRoot ".generated/$file") -Value (ProductionSource $file) -Encoding utf8}
if($Baseline){dotnet run --project (Join-Path $PSScriptRoot 'NativeTests.csproj') -c Release -- --baseline}
else{dotnet run --project (Join-Path $PSScriptRoot 'NativeTests.csproj') -c Release}
exit $LASTEXITCODE
