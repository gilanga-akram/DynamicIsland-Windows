Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinDump3 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left; public int Top; public int Right; public int Bottom;
    }
}
"@

$pids = @(Get-Process -Name DynamicIsland -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
Write-Output "Scanning for PIDs: $($pids -join ', ')"

$callback = [WinDump3+EnumWindowsProc]{
    param($hwnd, $lparam)
    [uint32]$procId = 0
    [WinDump3]::GetWindowThreadProcessId($hwnd, [ref]$procId) | Out-Null
    if ($pids -contains $procId) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinDump3]::GetWindowText($hwnd, $sb, 256) | Out-Null
        $cls = New-Object System.Text.StringBuilder 256
        [WinDump3]::GetClassName($hwnd, $cls, 256) | Out-Null
        $vis = [WinDump3]::IsWindowVisible($hwnd)
        $rect = New-Object WinDump3+RECT
        [WinDump3]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        Write-Output "Found Window: PID=$procId, HWND=$hwnd, Class='$($cls.ToString())', Title='$($sb.ToString())', Visible=$vis, Bounds=($($rect.Left), $($rect.Top), $($rect.Right), $($rect.Bottom))"
    }
    return $true
}

[WinDump3]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
Write-Output "Scan complete."
