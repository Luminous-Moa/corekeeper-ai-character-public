param([string]$out, [double]$seconds = 12, [int]$everyMs = 250, [int]$x = 0, [int]$y = 380, [int]$w = 900, [int]$h = 1120, [int]$saveEvery = 4, [int]$band = 0)
# fadecap.ps1 — screen crops of the chat block every $everyMs for $seconds: per frame the count of bright pixels
# (R,G,B all >= 200, i.e. chat letters) in the whole crop and in horizontal bands of $band px (0 = none).
# Writes <out>ade.csv (ms since start, bright count, sum of min(R,G,B)/1000 over the crop, band counts) and every $saveEvery-th frame as <out><ms>.png.
Add-Type -AssemblyName System.Drawing
Add-Type '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();' -Name D -Namespace Dpi
Add-Type @"
namespace Cnt { public static class C { public static long[] Count(byte[] b, int stride, int w, int h, int band) {
  int nb = band > 0 ? (h + band - 1) / band : 0; var r = new long[2 + nb]; long s = 0;
  for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int o = y * stride + x * 4;
    int m = System.Math.Min(b[o], System.Math.Min(b[o + 1], b[o + 2])); s += m; if (m >= 200) { r[0]++; if (band > 0) r[2 + y / band]++; } }
  r[1] = s / 1000;
  return r; } } }
"@
[Dpi.D]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $out | Out-Null
$csv = New-Object System.Collections.Generic.List[string]
$sw = [Diagnostics.Stopwatch]::StartNew(); $i = 0; [DateTimeOffset]::Now.ToUnixTimeMilliseconds() | Set-Content (Join-Path $out "start.txt")
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
while ($sw.Elapsed.TotalSeconds -lt $seconds) {
  $t = [int]$sw.ElapsedMilliseconds
  $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
  $d = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $n = $d.Stride * $h; $buf = New-Object byte[] $n
  [Runtime.InteropServices.Marshal]::Copy($d.Scan0, $buf, 0, $n); $stride = $d.Stride; $bmp.UnlockBits($d)
  $r = [Cnt.C]::Count($buf, $stride, $w, $h, $band)
  $csv.Add("$t," + ($r -join ','))
  if ($i % $saveEvery -eq 0) { $bmp.Save((Join-Path $out ('f{0:D5}.png' -f $t)), [System.Drawing.Imaging.ImageFormat]::Png) }
  $i++
  $wait = $everyMs - ([int]$sw.ElapsedMilliseconds - $t); if ($wait -gt 0) { Start-Sleep -Milliseconds $wait }
}
$csv | Set-Content (Join-Path $out 'fade.csv')
"frames $i -> $out"
