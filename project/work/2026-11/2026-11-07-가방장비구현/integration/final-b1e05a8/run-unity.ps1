$ErrorActionPreference = 'Stop'
$repo = 'C:/Dev/CoreKeeperModSDK-bag-common'
$official = 'C:/Dev/CoreKeeperModSDK'
$out = $PSScriptRoot
$sha = (& git -C $repo rev-parse HEAD).Trim()
if ($sha -ne 'b1e05a8fe4cb17e49ab97066ced9df1efd228ef6') { throw 'Unexpected source pin' }
if (Get-Process Unity,CoreKeeper -ErrorAction SilentlyContinue) { throw 'Existing Unity/game process; do not interfere' }
$candidate = Join-Path $out 'candidate-b1e05a8'
if (Test-Path -LiteralPath $candidate) { throw 'Candidate already exists' }
function Files([string]$root,[string]$filter='*') { @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $filter | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($root.Length).TrimStart('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }) }
$install = 'C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike'
$codeRoot = "$repo/Assets/AIPlayerSpike/Scripts"
$mainRoot = "$official/Assets/AIPlayerSpike/Scripts"
$input = [ordered]@{ sourceCommit=$sha;officialCommit=(& git -C $official rev-parse HEAD).Trim();startedUtc=[DateTime]::UtcNow.ToString('o');codeFiles=(Files $codeRoot '*.cs');officialCodeFiles=(Files $mainRoot '*.cs');installedFiles=(Files $install);helperSha256=(Get-FileHash -LiteralPath "$repo/Assets/HeadlessTools/Editor/BagCandidateExport.cs").Hash; sdkFiles=(Files "$repo/Assets/Plugins/CoreKeeper" '*.dll');candidate=$candidate }
$input | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath "$out/unity-input.json" -Encoding UTF8
$settings = @('Assets/AIPlayerSpike/AIPlayerSpike.asset','Assets/AIPlayerSpike/Data/AIPlayerSpike.asset','Assets/AIPlayerSpike/AIPlayerSpike.asmdef','Packages/dev.pugstorm.mod/SDK/Editor/ModBuilder.cs','Packages/dev.pugstorm.mod/SDK/Editor/ModBuilderSettings.cs','ProjectSettings/ProjectSettings.asset','Packages/manifest.json','Packages/packages-lock.json') | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath "$repo/$_").Hash} }
$settings | ConvertTo-Json | Set-Content -LiteralPath "$out/unity-settings.json" -Encoding UTF8
$log = "$out/unity-export.log"
$proc = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe' -ArgumentList @('-batchmode','-quit','-disable-assembly-updater','-projectPath',$repo,'-executeMethod','BagCandidateExport.Build','-bagCandidateOutput',$candidate,'-logFile',$log) -WindowStyle Hidden -PassThru
[ordered]@{pid=$proc.Id;sourceCommit=$sha;startedUtc=[DateTime]::UtcNow.ToString('o');log=$log} | ConvertTo-Json | Set-Content -LiteralPath "$out/unity-start.json" -Encoding UTF8
$proc.WaitForExit()
$code = $proc.ExitCode
[ordered]@{sourceCommit=$sha;pid=$proc.Id;exitCode=$code;finishedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath "$out/unity-exit.json" -Encoding UTF8
if ($code -ne 0) { throw "Unity exited $code" }
$before = $input
$checks = @($before.codeFiles | ForEach-Object { $path=$_.path; [ordered]@{path=$path;sha256=$_.sha256;sourceUnchanged=((Get-FileHash -LiteralPath "$codeRoot/$path").Hash -eq $_.sha256);packageMatches=((Get-FileHash -LiteralPath "$candidate/AIPlayerSpike/Scripts/Scripts/$path").Hash -eq $_.sha256)} })
$installAfter = Files $install
$mainAfter = Files $mainRoot '*.cs'
$manifest = [ordered]@{sourceCommit=$sha;unityExitCode=$code;codeChecks=$checks;installedUnchanged=(@(Compare-Object ($before.installedFiles|ConvertTo-Json -Compress) ($installAfter|ConvertTo-Json -Compress)).Count -eq 0);officialCodeUnchanged=(@(Compare-Object ($before.officialCodeFiles|ConvertTo-Json -Compress) ($mainAfter|ConvertTo-Json -Compress)).Count -eq 0);officialCommitUnchanged=((& git -C $official rev-parse HEAD).Trim() -eq $before.officialCommit);compilerDllSha256=(Get-FileHash -LiteralPath "$repo/Library/ScriptAssemblies/AIPlayerSpike.dll").Hash}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath "$out/artifact-manifest.json" -Encoding UTF8
if ($checks.Where({!$_.sourceUnchanged -or !$_.packageMatches}).Count -gt 0 -or !$manifest.installedUnchanged -or !$manifest.officialCodeUnchanged -or !$manifest.officialCommitUnchanged) { throw 'Evidence mismatch; inspect artifact-manifest.json' }
$zip = "$out/AIPlayerSpike-integrated-b1e05a8.zip"
Compress-Archive -LiteralPath "$candidate/AIPlayerSpike" -DestinationPath $zip
[ordered]@{sourceCommit=$sha;zip=$zip;sha256=(Get-FileHash -LiteralPath $zip).Hash;bytes=(Get-Item -LiteralPath $zip).Length;payload='SDK raw C# sources and ModManifest; compiler DLL is verification-only'} | ConvertTo-Json | Set-Content -LiteralPath "$out/package.json" -Encoding UTF8
