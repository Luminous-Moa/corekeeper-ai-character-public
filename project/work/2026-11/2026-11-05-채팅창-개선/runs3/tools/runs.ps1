param([string]$src)
# runs.ps1 — histogram of horizontal and vertical run lengths of bright pixels (min RGB >= 150) in an image: the pixel
# font's dot size shows as the most common short run.
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($src); $hx = @{}; $hy = @{}
for ($y = 0; $y -lt $b.Height; $y += 1) { $r = 0; for ($x = 0; $x -lt $b.Width; $x++) { $c = $b.GetPixel($x, $y); $on = [Math]::Min($c.R, [Math]::Min($c.G, $c.B)) -ge 150; if ($on) { $r++ } elseif ($r -gt 0) { $hx[$r] = 1 + [int]$hx[$r]; $r = 0 } } }
for ($x = 0; $x -lt $b.Width; $x += 1) { $r = 0; for ($y = 0; $y -lt $b.Height; $y++) { $c = $b.GetPixel($x, $y); $on = [Math]::Min($c.R, [Math]::Min($c.G, $c.B)) -ge 150; if ($on) { $r++ } elseif ($r -gt 0) { $hy[$r] = 1 + [int]$hy[$r]; $r = 0 } } }
"horizontal runs: " + (($hx.Keys | Sort-Object | Select-Object -First 14 | ForEach-Object { "$_=$($hx[$_])" }) -join ' ')
"vertical runs:   " + (($hy.Keys | Sort-Object | Select-Object -First 14 | ForEach-Object { "$_=$($hy[$_])" }) -join ' ')
