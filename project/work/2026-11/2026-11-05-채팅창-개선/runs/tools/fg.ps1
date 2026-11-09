Add-Type '[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint p);' -Name W -Namespace X
$p = 0; [X.W]::GetWindowThreadProcessId([X.W]::GetForegroundWindow(), [ref]$p) | Out-Null; (Get-Process -Id $p).ProcessName
