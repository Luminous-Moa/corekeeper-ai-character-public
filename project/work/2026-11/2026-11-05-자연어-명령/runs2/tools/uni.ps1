param([string]$text, [switch]$enter)
# uni.ps1 — open the chat box, type text as Unicode packets (KEYEVENTF_UNICODE, not through the IME), Enter.
Add-Type @"
using System; using System.Runtime.InteropServices;
public class UI2 {
  [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit, Size=40)] public struct IN { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KI ki; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, IN[] i, int size);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static void Ch(char c) { var a = new IN[2]; a[0].type = 1; a[0].ki.scan = c; a[0].ki.flags = 4; a[1].type = 1; a[1].ki.scan = c; a[1].ki.flags = 4 | 2; SendInput(2, a, Marshal.SizeOf(typeof(IN))); }
  public static void Vk(ushort v) { var a = new IN[2]; a[0].type = 1; a[0].ki.vk = v; a[1].type = 1; a[1].ki.vk = v; a[1].ki.flags = 2; SendInput(2, a, Marshal.SizeOf(typeof(IN))); }
}
"@
$fg = [UI2]::GetForegroundWindow(); $p = 0; [UI2]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
if ((Get-Process -Id $p).ProcessName -ne 'CoreKeeper') { 'NOT SENT: not foreground'; exit 1 }
[UI2]::Vk(13); Start-Sleep -Milliseconds 400
foreach ($c in $text.ToCharArray()) { [UI2]::Ch($c); Start-Sleep -Milliseconds 30 }
Start-Sleep -Milliseconds 200
if ($enter) { [UI2]::Vk(13) }
"sent: $text"
