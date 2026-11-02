Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinAllThreads {
    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

$proc = Get-Process -Id 10768
$foundTotal = 0

foreach ($t in $proc.Threads) {
    $tid = $t.Id
    $cb = [WinAllThreads+EnumThreadDelegate]{
        param($hwnd, $lparam)
        $sb = New-Object System.Text.StringBuilder 256
        [WinAllThreads]::GetWindowText($hwnd, $sb, 256) | Out-Null
        $cls = New-Object System.Text.StringBuilder 256
        [WinAllThreads]::GetClassName($hwnd, $cls, 256) | Out-Null
        $vis = [WinAllThreads]::IsWindowVisible($hwnd)
        Write-Output "TID $tid -> HWND: $hwnd, Class: '$($cls.ToString())', Title: '$($sb.ToString())', Vis: $vis"
        $script:foundTotal++
        return $true
    }
    [WinAllThreads]::EnumThreadWindows($tid, $cb, [IntPtr]::Zero) | Out-Null
}

Write-Output "Finished checking all threads. Total windows: $foundTotal"
