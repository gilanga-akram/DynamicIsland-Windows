$xamlPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$csPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'

# 1. Update MainWindow.xaml
$xamlContent = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)
$xamlContent = $xamlContent.Replace("`r`n", "`n")
$xamlContent = $xamlContent.Replace(
    '<Path x:Name="NotchFillPath" Fill="#3C1A2638" IsHitTestVisible="False" SnapsToDevicePixels="True"/>',
    '<Path x:Name="NotchFillPath" Fill="#000000" IsHitTestVisible="False" SnapsToDevicePixels="True"/>'
)
$xamlContent = $xamlContent.Replace(
    '<Path x:Name="NotchBorderPath" Stroke="#40FFFFFF" StrokeThickness="1.2" IsHitTestVisible="False" SnapsToDevicePixels="True"/>',
    '<Path x:Name="NotchBorderPath" Stroke="#25252C" StrokeThickness="1" IsHitTestVisible="False" SnapsToDevicePixels="True"/>'
)
$xamlContent = $xamlContent.Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($xamlPath, $xamlContent, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml updated."

# 2. Update MainWindow.xaml.cs
$csContent = [System.IO.File]::ReadAllText($csPath, [System.Text.Encoding]::UTF8)
$csContent = $csContent.Replace("`r`n", "`n")

$oldDecl = @"
        // Brushes for macOS Crystal Frosted Glass (Collapsed & Expanded - Matches Dock)
        private static readonly Brush _glassFillCollapsed = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#552A384A"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#3C1A2638"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );
        private static readonly Brush _glassBorderCollapsed = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#65FFFFFF"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#25FFFFFF"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );
        private static readonly System.Windows.Media.Effects.DropShadowEffect _glassShadowCollapsed = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 14,
            ShadowDepth = 3,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 0.35
        };
"@

$newDecl = @"
        // Brushes for Apple Hardware Notch (Collapsed / Idle: Pure Black) vs macOS Crystal Frosted Glass (Expanded: Matches Dock)
        private static readonly Brush _notchFillCollapsed = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Pure Hardware Notch Black
        private static readonly Brush _notchBorderCollapsed = new SolidColorBrush(Color.FromArgb(255, 37, 37, 44)); // #25252C
"@

$oldMethod = @"
        public void UpdateIslandVisualGlass(bool isExpanded)
        {
            if (NotchFillPath != null)
            {
                NotchFillPath.Fill = isExpanded ? _glassFillExpanded : _glassFillCollapsed;
            }

            if (NotchBorderPath != null)
            {
                NotchBorderPath.Stroke = isExpanded ? _glassBorderExpanded : _glassBorderCollapsed;
                NotchBorderPath.StrokeThickness = 1.2;
            }

            if (IslandBorder != null)
            {
                IslandBorder.Effect = isExpanded ? _glassShadowExpanded : _glassShadowCollapsed;
            }
        }
"@

$newMethod = @"
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
"@

$oldDecl = $oldDecl.Replace("`r`n", "`n")
$newDecl = $newDecl.Replace("`r`n", "`n")
$oldMethod = $oldMethod.Replace("`r`n", "`n")
$newMethod = $newMethod.Replace("`r`n", "`n")

if ($csContent.Contains($oldDecl)) {
    $csContent = $csContent.Replace($oldDecl, $newDecl)
    Write-Output "Declarations replaced."
} else {
    Write-Output "Old declarations not found."
}

if ($csContent.Contains($oldMethod)) {
    $csContent = $csContent.Replace($oldMethod, $newMethod)
    Write-Output "Method replaced."
} else {
    Write-Output "Old method not found."
}

$csContent = $csContent.Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($csPath, $csContent, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml.cs updated."
