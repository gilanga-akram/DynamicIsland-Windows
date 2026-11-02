Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinEnum {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError=true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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

$targetPid = 10768
$found = 0

$proc = [WinEnum+EnumWindowsProc]{
    param($hwnd, $lparam)
    [uint32]$winProcId = 0
    [WinEnum]::GetWindowThreadProcessId($hwnd, [ref]$winProcId) | Out-Null
    if ($winProcId -eq $targetPid) {
        $sbText = New-Object System.Text.StringBuilder 256
        [WinEnum]::GetWindowText($hwnd, $sbText, 256) | Out-Null
        $sbClass = New-Object System.Text.StringBuilder 256
        [WinEnum]::GetClassName($hwnd, $sbClass, 256) | Out-Null
        $vis = [WinEnum]::IsWindowVisible($hwnd)
        $rect = New-Object WinEnum+RECT
        [WinEnum]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        Write-Output "HWND: $hwnd, Class: $($sbClass.ToString()), Title: '$($sbText.ToString())', Visible: $vis, Rect: ($($rect.Left), $($rect.Top), $($rect.Right), $($rect.Bottom))"
    }
    return $true
}

[WinEnum]::EnumWindows($proc, [IntPtr]::Zero) | Out-Null
Write-Output "Done scanning."
