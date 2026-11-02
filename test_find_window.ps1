Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WinFindWindow {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

$hwnd = [WinFindWindow]::FindWindow($null, "Dynamic Island Pro")
Write-Output "FindWindow result: $hwnd"

if ($hwnd -ne [IntPtr]::Zero) {
    $vis = [WinFindWindow]::IsWindowVisible($hwnd)
    $rect = New-Object WinFindWindow+RECT
    [WinFindWindow]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    Write-Output "Visible: $vis, Bounds: ($($rect.Left), $($rect.Top), $($rect.Right), $($rect.Bottom))"
}
