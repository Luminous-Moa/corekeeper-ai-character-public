# This is a verbatim production prefix: cursor lifecycle, matching, binding, Resolve and quantity calculations.
$lifeSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Assets/AIPlayerSpike/Scripts/AILife.cs') -Raw -Encoding UTF8
$lifeEnd = $lifeSource.IndexOf('    private static List<Entry> Entries(')
if ($lifeEnd -lt 0) { throw 'Production AILife boundary not found' }
$lifeBindingSource = $lifeSource.Substring(0,$lifeEnd) + "}`n"
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'LifeBindingSource.cs'),$lifeBindingSource,[System.Text.UTF8Encoding]::new($false))