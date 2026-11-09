param([string]$cmd, [string]$a1, [string]$a2)
# kc.ps1 — keyboard into Core Keeper only when it is the foreground window (자연어 명령 live run).
#   open                 Enter (opens the chat box)
#   type "<text>"        characters (SendKeys, Unicode; no IME)
#   vk <code> [holdMs]   one virtual key (Tab 9, Enter 13, Esc 27, Hangul 21, Back 8)
#   say "<text>"         open, type, Enter
#   ime "<keys>"         Hangul mode on (VK 0x15), the given letter keys as virtual keys (IME composes), Hangul off
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class KC { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint c, uint t); }
"@
function IsFg { $fg = [KC]::GetForegroundWindow(); $p = 0; [KC]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null; (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName -eq 'CoreKeeper' }
function Vk([int]$vk, [int]$hold = 50) { $sc = [byte][KC]::MapVirtualKey($vk, 0); [KC]::keybd_event([byte]$vk, $sc, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds $hold; [KC]::keybd_event([byte]$vk, $sc, 2, [IntPtr]::Zero); Start-Sleep -Milliseconds 60 }
function Esc([string]$c) { if ('+^%~(){}[]'.Contains($c)) { "{$c}" } else { $c } }
function TypeText([string]$t) { foreach ($ch in $t.ToCharArray()) { [System.Windows.Forms.SendKeys]::SendWait((Esc ([string]$ch))); Start-Sleep -Milliseconds 35 } }
if (-not (IsFg)) { "NOT SENT: Core Keeper not foreground"; exit 1 }
switch ($cmd) {
  'open' { Vk 13; Start-Sleep -Milliseconds 400; 'opened' }
  'type' { TypeText $a1; "typed $a1" }
  'vk' { $h = if ($a2) { [int]$a2 } else { 50 }; Vk ([int]$a1) $h; "vk $a1" }
  'say' { Vk 13; Start-Sleep -Milliseconds 400; if (-not (IsFg)) { 'NOT SENT'; exit 1 }; TypeText $a1; Start-Sleep -Milliseconds 200; Vk 13; "said $a1" }
  'ime' { Vk 0x15; Start-Sleep -Milliseconds 200; foreach ($ch in $a1.ToUpper().ToCharArray()) { Vk ([int][char]$ch) }; Start-Sleep -Milliseconds 150; "ime keys $a1" }
  'imeoff' { Vk 0x15; 'hangul toggled' }
}
