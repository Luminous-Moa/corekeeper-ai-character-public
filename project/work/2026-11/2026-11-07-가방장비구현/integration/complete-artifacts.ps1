$ErrorActionPreference='Stop'
$repo='C:/Dev/CoreKeeperModSDK-bag-common'
$official='C:/Dev/CoreKeeperModSDK'
$out=$PSScriptRoot
$input=Get-Content -LiteralPath "$out/unity-input.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$sha=$input.sourceCommit
$candidate=$input.candidate
$codeRoot="$repo/Assets/AIPlayerSpike/Scripts"
$mainRoot="$official/Assets/AIPlayerSpike/Scripts"
$install='C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike'
$code=(Get-Content -LiteralPath "$out/unity-exit.json" -Raw -Encoding UTF8|ConvertFrom-Json).exitCode
function Files([string]$root,[string]$filter='*') { @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $filter | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($root.Length).TrimStart('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }) }
$before = $input
$checks = @($before.codeFiles | ForEach-Object { $path=$_.path; [ordered]@{path=$path;sha256=$_.sha256;sourceUnchanged=((Get-FileHash -LiteralPath "$codeRoot/$path").Hash -eq $_.sha256);packageMatches=((Get-FileHash -LiteralPath "$candidate/AIPlayerSpike/Scripts/Scripts/$path").Hash -eq $_.sha256)} })
$installAfter = Files $install
$mainAfter = Files $mainRoot '*.cs'
$manifest = [ordered]@{sourceCommit=$sha;unityExitCode=$code;codeChecks=$checks;installedUnchanged=(@(Compare-Object ($before.installedFiles|ConvertTo-Json -Compress) ($installAfter|ConvertTo-Json -Compress)).Count -eq 0);officialCodeUnchanged=(@(Compare-Object ($before.officialCodeFiles|ConvertTo-Json -Compress) ($mainAfter|ConvertTo-Json -Compress)).Count -eq 0);officialCommitUnchanged=((& git -C $official rev-parse HEAD).Trim() -eq $before.officialCommit);compilerDllSha256=(Get-FileHash -LiteralPath "$repo/Library/ScriptAssemblies/AIPlayerSpike.dll").Hash}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath "$out/artifact-manifest.json" -Encoding UTF8
if ($checks.Where({!$_.sourceUnchanged -or !$_.packageMatches}).Count -gt 0 -or !$manifest.installedUnchanged -or !$manifest.officialCodeUnchanged -or !$manifest.officialCommitUnchanged) { throw 'Evidence mismatch; inspect artifact-manifest.json' }
$zip = "$out/AIPlayerSpike-integrated-7dc9c7e.zip"
Compress-Archive -LiteralPath "$candidate/AIPlayerSpike" -DestinationPath $zip
[ordered]@{sourceCommit=$sha;zip=$zip;sha256=(Get-FileHash -LiteralPath $zip).Hash;bytes=(Get-Item -LiteralPath $zip).Length;payload='SDK raw C# sources and ModManifest; compiler DLL is verification-only'} | ConvertTo-Json | Set-Content -LiteralPath "$out/package.json" -Encoding UTF8
