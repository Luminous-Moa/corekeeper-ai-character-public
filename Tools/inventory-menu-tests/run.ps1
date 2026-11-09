# Actual AIInventoryMenu over the life-inventory adapter world. The shared stub file is reused; only the menu token
# field that the actual menu writes is added to the generated copy.
$ErrorActionPreference = 'Stop'
$life = Join-Path $PSScriptRoot '../life-inventory-tests'
& (Join-Path $life 'ExtractBase.ps1')
& (Join-Path $life 'ExtractLifeBinding.ps1')
$generated = Join-Path $PSScriptRoot '.generated'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
$stubs = Get-Content -LiteralPath (Join-Path $life 'Stubs.cs') -Raw -Encoding utf8
$marker = 'public ObjectID itemId;public int itemVariation,itemCount;public bool progress;}'
if (-not $stubs.Contains($marker)) { throw 'AICandidate stub marker changed' }
$stubs = $stubs.Replace($marker, 'public ObjectID itemId;public int itemVariation,itemCount;public bool progress;public AIInventoryMenuChoice inventoryMenu;}')
Set-Content -LiteralPath (Join-Path $generated 'Stubs.cs') -Value $stubs -Encoding utf8
dotnet run --project (Join-Path $PSScriptRoot 'InventoryMenuTests.csproj') -c Release
exit $LASTEXITCODE
