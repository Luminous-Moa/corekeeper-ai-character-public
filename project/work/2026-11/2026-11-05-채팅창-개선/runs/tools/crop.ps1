param([string]$src, [string]$dst, [int]$x, [int]$y, [int]$w, [int]$h)
# crop.ps1 — cut a rectangle out of a screenshot at its own pixels (no scaling), for judging the pixel font.
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($src)
$c = $b.Clone((New-Object System.Drawing.Rectangle $x, $y, $w, $h), $b.PixelFormat)
$c.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); "$dst ${w}x${h}"
