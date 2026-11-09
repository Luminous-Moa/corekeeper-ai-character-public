$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
& dotnet run --project (Join-Path $testRoot 'GatherTests.csproj') -c Release
exit $LASTEXITCODE
