Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinStationCheck {
    [DllImport("user32.dll")] public static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll")] public static extern IntPtr GetThreadDesktop(int dwThreadId);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, StringBuilder pvInfo, int nLength, out int lpnLengthNeeded);
}
"@

$hwinsta = [WinStationCheck]::GetProcessWindowStation()
$sb = New-Object System.Text.StringBuilder 256
$len = 0
[WinStationCheck]::GetUserObjectInformation($hwinsta, 2, $sb, 256, [ref]$len) | Out-Null
Write-Output "WindowStation: $($sb.ToString())"

$hdesk = [WinStationCheck]::GetThreadDesktop([System.Threading.Thread]::CurrentThread.ManagedThreadId)
$sbDesk = New-Object System.Text.StringBuilder 256
[WinStationCheck]::GetUserObjectInformation($hdesk, 2, $sbDesk, 256, [ref]$len) | Out-Null
Write-Output "Desktop: $($sbDesk.ToString())"
