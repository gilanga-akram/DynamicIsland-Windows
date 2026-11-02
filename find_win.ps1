Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinFindFile {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left; public int Top; public int Right; public int Bottom;
    }
}
"@

$matches = [WinFindFile]::EnumWindows({
    param($hwnd, $lparam)
    $sb = New-Object System.Text.StringBuilder 256
    [WinFindFile]::GetWindowText($hwnd, $sb, 256) | Out-Null
    $cls = New-Object System.Text.StringBuilder 256
    [WinFindFile]::GetClassName($hwnd, $cls, 256) | Out-Null
    $t = $sb.ToString()
    $c = $cls.ToString()
    [uint32]$procId = 0
    [WinFindFile]::GetWindowThreadProcessId($hwnd, [ref]$procId) | Out-Null
    
    if ($t -like "*Dynamic*" -or $c -like "*HwndWrapper*") {
        $vis = [WinFindFile]::IsWindowVisible($hwnd)
        $rect = New-Object WinFindFile+RECT
        [WinFindFile]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        Write-Output "MATCH: HWND=$hwnd, PID=$procId, Title='$t', Class='$c', Visible=$vis, Bounds=($($rect.Left), $($rect.Top), $($rect.Right), $($rect.Bottom))"
    }
    return $true
}, [IntPtr]::Zero)
Write-Output "Scan done."
