param([string]$ArtifactDir = $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$m=Get-Content -LiteralPath (Join-Path $ArtifactDir 'manifest.json') -Encoding UTF8 -Raw|ConvertFrom-Json
$hud=$m.screens|Where-Object id -eq 'CKHUD-01'
$font=New-Object System.Drawing.Font('Arial',13,[System.Drawing.FontStyle]::Bold)
foreach($s in $m.evidence){
 $bitmap=[System.Drawing.Bitmap]::FromFile([System.IO.Path]::GetFullPath((Join-Path $ArtifactDir $s.source)))
 $g=[System.Drawing.Graphics]::FromImage($bitmap)
 $sets=@(@{elements=$s.elements;prefix='';color=[System.Drawing.Color]::Gold;bg=[System.Drawing.Color]::FromArgb(101,75,0)})
 if($s.common_regions.Count -gt 0){$sets+=@{elements=$hud.elements;prefix='H';color=[System.Drawing.Color]::DeepSkyBlue;bg=[System.Drawing.Color]::FromArgb(7,89,133)}}
 foreach($set in $sets){
  $pen=[System.Drawing.Pen]::new($set.color,2);$brush=[System.Drawing.SolidBrush]::new($set.bg)
  foreach($e in $set.elements){
   $b=$e.bbox;$label=$set.prefix+$e.number;$ly=[Math]::Max(32,[int]$b[1]-22)
   if(@($set.elements.parent) -contains $e.id){$ly-=22}
   $g.DrawRectangle($pen,[int]$b[0],[int]$b[1],[int]$b[2],[int]$b[3])
   $g.FillRectangle($brush,[int]$b[0],$ly,[Math]::Max(34,$label.Length*12),22)
   $g.DrawString($label,$font,[System.Drawing.Brushes]::White,[single]($b[0]+3),[single]$ly)
  };$pen.Dispose();$brush.Dispose()
 };$g.Dispose();$bitmap.Save((Join-Path $ArtifactDir $s.annotation_png),[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose()
};$font.Dispose()
Write-Output ('Rendered '+$m.evidence.Count+' PNGs.')


