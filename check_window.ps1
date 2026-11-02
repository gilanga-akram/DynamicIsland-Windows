Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WinCheck {
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@

$procs = Get-Process -Name DynamicIsland -ErrorAction SilentlyContinue
foreach ($p in $procs) {
    Write-Output "PID: $($p.Id), MainWindowHandle: $($p.MainWindowHandle)"
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $visible = [WinCheck]::IsWindowVisible($p.MainWindowHandle)
        $rect = New-Object WinCheck+RECT
        $res = [WinCheck]::GetWindowRect($p.MainWindowHandle, [ref]$rect)
        Write-Output "Visible: $visible, Rect: Left=$($rect.Left), Top=$($rect.Top), Right=$($rect.Right), Bottom=$($rect.Bottom)"
    }
}
