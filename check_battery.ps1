[Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null
$props = @("System.Devices.BatteryPercent", "System.ItemNameDisplay", "System.Devices.Aep.IsConnected")
$devs = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync("", $props).GetAwaiter().GetResult()

$found = 0
foreach ($d in $devs) {
    $bat = $d.Properties["System.Devices.BatteryPercent"]
    if ($null -ne $bat) {
        Write-Host "FOUND BATTERY -> Name: $($d.Name) | Battery: $bat %"
        $found++
    }
}

if ($found -eq 0) {
    Write-Host "No Windows.Devices.BatteryPercent property found on enumerated devices."
}

# Also check Bluetooth and HID devices
Get-PnpDevice -Class "Bluetooth", "HIDClass", "Media" -Status OK | Where-Object { $_.FriendlyName -match "Barracuda|Razer|Mouse|Headset|Wireless" } | Format-Table InstanceId, FriendlyName, Class
