$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

# 1. Define fields for glass brushes
$classDefIdx = -1
for ($i=40; $i -le 80; $i++) {
    if ($list[$i].Contains('public partial class MainWindow : Window')) {
        $classDefIdx = $i
        break
    }
}

if ($classDefIdx -ne -1) {
    $fieldCode = @'
        // Brushes for Apple Hardware Notch (Collapsed) vs macOS Crystal Frosted Glass (Expanded - Matches Dock)
        private static readonly Brush _notchFillCollapsed = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Pure Hardware Notch
        private static readonly Brush _notchBorderCollapsed = new SolidColorBrush(Color.FromArgb(255, 37, 37, 44)); // #25252C

        private static readonly Brush _glassFillExpanded = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#48384B62"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#2E1A2638"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );

        private static readonly Brush _glassBorderExpanded = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#85FFFFFF"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );

        private static readonly DropShadowEffect _glassShadowExpanded = new DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 6,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 0.42
        };
'@
    $list.Insert($classDefIdx + 1, $fieldCode)
    Write-Output "Inserted glass brush fields in MainWindow class definition"
}

# 2. Add UpdateIslandVisualGlass method
$pomoIdx = -1
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('public void UpdateIslandVisibility(bool enabled)')) {
        $pomoIdx = $i
        break
    }
}

if ($pomoIdx -ne -1) {
    $glassMethod = @'
        public void UpdateIslandVisualGlass(bool isExpanded)
        {
            if (NotchFillPath != null)
            {
                NotchFillPath.Fill = isExpanded ? _glassFillExpanded : _notchFillCollapsed;
            }

            if (NotchBorderPath != null)
            {
                NotchBorderPath.Stroke = isExpanded ? _glassBorderExpanded : _notchBorderCollapsed;
                NotchBorderPath.StrokeThickness = isExpanded ? 1.2 : 1.0;
            }

            if (IslandBorder != null)
            {
                IslandBorder.Effect = isExpanded ? _glassShadowExpanded : null;
            }
        }

'@
    $list.Insert($pomoIdx, $glassMethod)
    Write-Output "Inserted UpdateIslandVisualGlass method"
}

# 3. Call UpdateIslandVisualGlass(true) in ExpandIsland()
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('private void ExpandIsland()')) {
        for ($j=$i; $j -le $i+15; $j++) {
            if ($list[$j].Contains('_isExpanded = true;')) {
                $list.Insert($j + 1, "            UpdateIslandVisualGlass(true);")
                Write-Output "Added UpdateIslandVisualGlass(true) in ExpandIsland"
                break
            }
        }
        break
    }
}

# 4. Call UpdateIslandVisualGlass(false) in CollapseIsland()
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('private void CollapseIsland()')) {
        for ($j=$i; $j -le $i+15; $j++) {
            if ($list[$j].Contains('_isExpanded = false;')) {
                $list.Insert($j + 1, "            UpdateIslandVisualGlass(false);")
                Write-Output "Added UpdateIslandVisualGlass(false) in CollapseIsland"
                break
            }
        }
        break
    }
}

# 5. Call UpdateIslandVisualGlass(false) in Window_Loaded
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('UpdateIslandVisibility(Settings?.EnableDynamicIsland ?? true);')) {
        $list.Insert($i + 1, "            UpdateIslandVisualGlass(false);")
        Write-Output "Added UpdateIslandVisualGlass(false) in Window_Loaded"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml.cs UPDATED SUCCESSFULLY WITH GLASS DOCK VISUALS"
