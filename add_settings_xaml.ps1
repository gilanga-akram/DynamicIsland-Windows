$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\SettingsWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) {
    $list.Add($l)
}

$headerIdx = -1
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('TxtModulesHeaderTitle')) {
        $headerIdx = $i
        break
    }
}

if ($headerIdx -ne -1) {
    $islandModuleBlock = @"
                                    <!-- Dynamic Island (Poni Atas) Switch -->
                                    <Grid Margin="0,10">
                                        <StackPanel VerticalAlignment="Center" Margin="0,0,60,0">
                                            <TextBlock x:Name="TxtDynamicIslandModuleTitle" Text="Dynamic Island (Poni Atas)" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="13"/>
                                            <TextBlock x:Name="TxtDynamicIslandModuleDesc" Text="Tampilkan Dynamic Island di bagian atas layar. Matikan switch ini jika Anda hanya ingin menggunakan Dock saja tanpa Dynamic Island." Foreground="#9CA3AF" FontSize="11" Margin="0,2,0,0" TextWrapping="Wrap"/>
                                        </StackPanel>
                                        <CheckBox x:Name="ToggleEnableDynamicIsland" Style="{StaticResource ModernToggleSwitch}" IsChecked="True" HorizontalAlignment="Right" VerticalAlignment="Center" Click="ToggleEnableDynamicIsland_Click"/>
                                    </Grid>
                                    <Border Height="1" Background="#24252D"/>
"@
    $list.Insert($headerIdx + 1, $islandModuleBlock)
    Write-Output "Added Dynamic Island toggle below header at line $headerIdx"
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
Write-Output "SUCCESS WRITING SettingsWindow.xaml"
