param([string]$cmd, [string]$a1, [string]$a2, [string]$a3)
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class U {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint c, uint t);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[U]::SetProcessDPIAware() | Out-Null
$dir = Split-Path $MyInvocation.MyCommand.Path
function Key([byte]$vk, [int]$holdMs = 60) {
  $sc = [byte][U]::MapVirtualKey($vk, 0)
  [U]::keybd_event($vk, $sc, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds $holdMs
  [U]::keybd_event($vk, $sc, 2, [IntPtr]::Zero)
}
switch ($cmd) {
  'shot' {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
    $scale = if ($a1) { [double]$a1 } else { 0.5 }
    $w = [int]($b.Width * $scale); $h = [int]($b.Height * $scale)
    $small = New-Object System.Drawing.Bitmap $bmp, $w, $h
    $p = Join-Path $dir 'shot.png'; $small.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    "$p screen=$($b.Width)x$($b.Height) scale=$scale"
  }
  'click' { [U]::SetCursorPos([int]$a1, [int]$a2); Start-Sleep -Milliseconds 120
    [U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80; [U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero); "clicked $a1,$a2" }
  'gclick' {
    # Click only if Core Keeper is the foreground window (avoid clicking into the user's other apps).
    Add-Type @"
using System; using System.Runtime.InteropServices;
public class F { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); }
"@
    $fg=[F]::GetForegroundWindow(); $fpid=0; [F]::GetWindowThreadProcessId($fg, [ref]$fpid) | Out-Null
    $name=(Get-Process -Id $fpid -ErrorAction SilentlyContinue).ProcessName
    if ($name -ne 'CoreKeeper') { "NOT CLICKED: foreground is $name"; break }
    [U]::SetCursorPos([int]$a1, [int]$a2); Start-Sleep -Milliseconds 120
    [U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80; [U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero); "gclicked $a1,$a2" }
  'wheel' { [U]::SetCursorPos([int]$a1, [int]$a2); Start-Sleep -Milliseconds 100
    $n = [int]$a3; $d = if ($n -lt 0) { [uint32]120 } else { [uint32]([uint32]::MaxValue - 119) }
    for ($i = 0; $i -lt [Math]::Abs($n); $i++) { [U]::mouse_event(0x0800, 0, 0, $d, [IntPtr]::Zero); Start-Sleep -Milliseconds 80 }; "wheel $a3" }
  'key' { $hold = if ($a2) { [int]$a2 } else { 60 }; Key ([byte][int]$a1) $hold; "key $a1" }
  'type' { foreach ($ch in $a1.ToCharArray()) { [System.Windows.Forms.SendKeys]::SendWait([string]$ch); Start-Sleep -Milliseconds 40 }; "typed" }
  'focus' { $p = Get-Process -Name $a1 -ErrorAction SilentlyContinue | ? { $_.MainWindowHandle -ne 0 } | Select -First 1
    if ($p) { [U]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; "focused $($p.Id)" } else { "no window" } }
}
# focus2 <process>: the Alt-key trick so Windows lets this script bring the window to the front
if ($cmd -eq 'focus2') {
  Add-Type @"
using System; using System.Runtime.InteropServices;
public class F2 { [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); }
"@
  $p = Get-Process -Name $a1 -ErrorAction SilentlyContinue | ? { $_.MainWindowHandle -ne 0 } | Select -First 1
  if (-not $p) { "no window"; return }
  [U]::keybd_event(0x12, 0, 0, [IntPtr]::Zero); [U]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
  [F2]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
  [U]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 400
  $fg = [F2]::GetForegroundWindow(); $fpid = 0; [F2]::GetWindowThreadProcessId($fg, [ref]$fpid) | Out-Null
  "foreground now: $((Get-Process -Id $fpid).ProcessName)"
}
