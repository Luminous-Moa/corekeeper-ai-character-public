param([string]$text)
# Sends a chat line to Core Keeper only if Core Keeper is foreground AND the chat input box is visibly open.
$s = Join-Path (Split-Path $MyInvocation.MyCommand.Path) 'ui.ps1'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class FG2 { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[FG2]::SetProcessDPIAware() | Out-Null
function IsFg { $fg=[FG2]::GetForegroundWindow(); $p=0; [FG2]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null; (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName -eq 'CoreKeeper' }
function ChatOpen {
  $bmp = New-Object System.Drawing.Bitmap 1, 1; $g = [System.Drawing.Graphics]::FromImage($bmp)
  $ok = $true
  foreach ($pt in @(@(330,1550),@(600,1560))) { $g.CopyFromScreen($pt[0], $pt[1], 0, 0, $bmp.Size); $c = $bmp.GetPixel(0, 0)
    if (-not ($c.R -lt 60 -and $c.G -ge 55 -and $c.G -le 95 -and $c.B -ge 50 -and $c.B -le 90 -and [Math]::Abs($c.G - $c.B) -lt 15)) { $ok = $false } }
  return $ok
}
if (-not (IsFg)) { "NOT SENT: Core Keeper not foreground"; exit }
if (-not (ChatOpen)) { & $s key 13 | Out-Null; Start-Sleep -Milliseconds 500 }
if (-not (IsFg)) { "NOT SENT: Core Keeper not foreground"; exit }
if (-not (ChatOpen)) { "NOT SENT: chat box did not open"; exit }
& $s type $text | Out-Null
Start-Sleep -Milliseconds 200
& $s key 13 | Out-Null
"sent $text"

