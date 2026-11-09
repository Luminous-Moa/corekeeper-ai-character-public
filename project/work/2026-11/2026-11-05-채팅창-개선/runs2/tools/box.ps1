param([string]$src)
# box.ps1 — the chat input box in a full screenshot: the extent of its dark teal fill (R<70, G 60..95, B 55..90, |G-B|<15)
# in the lower-left area (x < 1400, y 1400..1700).
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($src); $x0 = 9999; $x1 = -1; $y0 = 9999; $y1 = -1
for ($y = 1400; $y -lt 1700; $y += 1) { for ($x = 0; $x -lt 1400; $x += 1) { $c = $b.GetPixel($x, $y)
  if ($c.R -lt 70 -and $c.G -ge 60 -and $c.G -le 95 -and $c.B -ge 55 -and $c.B -le 90 -and [Math]::Abs($c.G - $c.B) -lt 15) { if ($x -lt $x0) { $x0 = $x }; if ($x -gt $x1) { $x1 = $x }; if ($y -lt $y0) { $y0 = $y }; if ($y -gt $y1) { $y1 = $y } } } }
"fill x $x0..$x1 ($($x1 - $x0 + 1) px), y $y0..$y1 ($($y1 - $y0 + 1) px)"
