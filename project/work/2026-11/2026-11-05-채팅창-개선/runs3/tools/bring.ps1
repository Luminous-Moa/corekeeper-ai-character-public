Add-Type @"
using System; using System.Runtime.InteropServices;
public class B {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
}
"@
$h = (Get-Process CoreKeeper | ? { $_.MainWindowHandle -ne 0 } | select -First 1).MainWindowHandle
$fg = [B]::GetForegroundWindow(); $t1 = [B]::GetWindowThreadProcessId($fg, [IntPtr]::Zero); $t2 = [B]::GetCurrentThreadId()
[B]::AttachThreadInput($t2, $t1, $true) | Out-Null
[B]::ShowWindow($h, 9) | Out-Null; [B]::BringWindowToTop($h) | Out-Null; [B]::SetForegroundWindow($h) | Out-Null
[B]::AttachThreadInput($t2, $t1, $false) | Out-Null
Start-Sleep -Milliseconds 300
$p = 0; $f = [B]::GetForegroundWindow(); "fg is game: $($f -eq $h)"
