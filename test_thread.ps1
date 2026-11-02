Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinThread {
    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

$threadId = 21192
$count = 0
$cb = [WinThread+EnumThreadDelegate]{
    param($hwnd, $lparam)
    $sb = New-Object System.Text.StringBuilder 256
    [WinThread]::GetWindowText($hwnd, $sb, 256) | Out-Null
    $cls = New-Object System.Text.StringBuilder 256
    [WinThread]::GetClassName($hwnd, $cls, 256) | Out-Null
    $vis = [WinThread]::IsWindowVisible($hwnd)
    Write-Output "Thread HWND: $hwnd, Class: '$($cls.ToString())', Title: '$($sb.ToString())', Vis: $vis"
    $script:count++
    return $true
}

[WinThread]::EnumThreadWindows($threadId, $cb, [IntPtr]::Zero) | Out-Null
Write-Output "Total thread windows: $count"
