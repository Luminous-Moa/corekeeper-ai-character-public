param([string]$dir, [string]$from, [string]$to, [int]$cx = 490, [int]$cy = 307, [int]$w = 320, [int]$h = 200, [int]$cols = 5, [string]$out)
# montage.ps1 <capdir> <HHmmss_fff from> <HHmmss_fff to> : crops (cx,cy,w,h) of each frame in the range into one grid PNG, labelled
Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $dir -Filter *.jpg | Where-Object { $_.BaseName -ge $from -and $_.BaseName -le $to } | Sort-Object Name
$n = $files.Count; if ($n -eq 0) { "no frames"; exit }
$rows = [math]::Ceiling($n / $cols)
$bmp = New-Object System.Drawing.Bitmap ($cols * $w), ($rows * ($h + 18))
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Black)
$font = New-Object System.Drawing.Font 'Consolas', 11
for ($i = 0; $i -lt $n; $i++) {
  $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
  $x = ($i % $cols) * $w; $y = [math]::Floor($i / $cols) * ($h + 18)
  $src = New-Object System.Drawing.Rectangle ($cx - $w / 2), ($cy - $h / 2), $w, $h
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle $x, ($y + 18), $w, $h), $src, [System.Drawing.GraphicsUnit]::Pixel)
  $g.DrawString($files[$i].BaseName, $font, [System.Drawing.Brushes]::Yellow, $x + 2, $y + 1)
  $img.Dispose()
}
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); "$n frames -> $out"
