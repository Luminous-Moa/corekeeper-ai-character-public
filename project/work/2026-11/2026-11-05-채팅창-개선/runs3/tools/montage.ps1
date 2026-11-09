param([string]$out, [double]$scale = 0.5, [Parameter(ValueFromRemainingArguments = $true)] [string[]]$files)
# montage.ps1 — images side by side (scaled), for looking at several crops at once.
Add-Type -AssemblyName System.Drawing
$imgs = $files | ForEach-Object { [System.Drawing.Image]::FromFile($_) }
$w = [int](($imgs | Measure-Object -Property Width -Sum).Sum * $scale) + 10 * $imgs.Count; $h = [int](($imgs | Measure-Object -Property Height -Maximum).Maximum * $scale)
$b = New-Object System.Drawing.Bitmap $w, $h; $g = [System.Drawing.Graphics]::FromImage($b); $g.Clear([System.Drawing.Color]::DarkRed); $x = 0
foreach ($i in $imgs) { $g.DrawImage($i, $x, 0, [int]($i.Width * $scale), [int]($i.Height * $scale)); $x += [int]($i.Width * $scale) + 10 }
$b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); "$out ${w}x${h}"
