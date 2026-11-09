param([string]$out, [int]$seconds = 60, [double]$scale = 0.35, [int]$intervalMs = 50)
# cap.ps1 <dir> <seconds> : screen capture loop (primary screen), one JPEG per ~intervalMs, file name = local wall clock
# HHmmss_fff at the moment CopyFromScreen started (same clock as the probe's wall=). Log of capture cost in <dir>\cap.txt.
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class D { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[D]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $out | Out-Null
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$w = [int]($b.Width * $scale); $h = [int]($b.Height * $scale)
$small = New-Object System.Drawing.Bitmap $w, $h
$gs = [System.Drawing.Graphics]::FromImage($small)
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | ? { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters 1
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]70)
$sw = [System.Diagnostics.Stopwatch]::StartNew(); $n = 0; $costs = New-Object System.Collections.Generic.List[double]
while ($sw.Elapsed.TotalSeconds -lt $seconds) {
  $t0 = $sw.Elapsed.TotalMilliseconds
  $name = (Get-Date).ToString('HHmmss_fff')
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $gs.DrawImage($bmp, 0, 0, $w, $h)
  $small.Save((Join-Path $out "$name.jpg"), $codec, $ep)
  $n++; $c = $sw.Elapsed.TotalMilliseconds - $t0; $costs.Add($c)
  $wait = $intervalMs - $c; if ($wait -gt 0) { Start-Sleep -Milliseconds ([int]$wait) }
}
$costs.Sort(); "frames $n in $seconds s; capture ms p50 $($costs[[int]($costs.Count*0.5)]) p95 $($costs[[int]($costs.Count*0.95)]) screen $($b.Width)x$($b.Height) scale $scale" | Tee-Object -FilePath (Join-Path $out 'cap.txt') -Append
