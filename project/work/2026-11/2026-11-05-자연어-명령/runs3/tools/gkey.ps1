param([int]$vk, [int]$hold = 70)
# Presses one key only if Core Keeper is the foreground window.
Add-Type @"
using System; using System.Runtime.InteropServices;
public class K { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint c, uint t); }
"@
$fg=[K]::GetForegroundWindow(); $fpid=0; [K]::GetWindowThreadProcessId($fg, [ref]$fpid) | Out-Null
$name=(Get-Process -Id $fpid -ErrorAction SilentlyContinue).ProcessName
if ($name -ne 'CoreKeeper') { "NOT PRESSED: foreground is $name"; exit }
$sc=[byte][K]::MapVirtualKey($vk,0); [K]::keybd_event([byte]$vk,$sc,0,[IntPtr]::Zero); Start-Sleep -Milliseconds $hold; [K]::keybd_event([byte]$vk,$sc,2,[IntPtr]::Zero); "pressed $vk"
