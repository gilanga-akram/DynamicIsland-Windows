$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

for ($i=130; $i -le 160; $i++) {
    if ($list[$i].Contains('<Style x:Key="ToolCardBtn"')) {
        # Find closing </Style>
        $endIdx = -1
        for ($j=$i; $j -le $i+30; $j++) {
            if ($list[$j].Contains('</Style>')) {
                $endIdx = $j
                break
            }
        }
        if ($endIdx -ne -1) {
            $glassStyle = @'
        <!-- Quick Tool Card Button Style (macOS Crystal Frosted Glass matching Dock) -->
        <Style x:Key="ToolCardBtn" TargetType="Button">
            <Setter Property="Background" Value="#25FFFFFF" />
            <Setter Property="BorderBrush" Value="#40FFFFFF" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Padding" Value="8,10" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="toolBorder" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="12" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#40FFFFFF" />
                                <Setter Property="BorderBrush" Value="#70FFFFFF" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
'@
            $list.RemoveRange($i, $endIdx - $i + 1)
            $list.Insert($i, $glassStyle)
            Write-Output "Successfully replaced ToolCardBtn Style at line $i"
            break
        }
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
