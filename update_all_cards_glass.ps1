$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$content = [System.IO.File]::ReadAllText($path)

# 1. Update CardBg and CardBorder resources
$oldCardBg = '<SolidColorBrush x:Key="CardBg" Color="#101014" />'
$newCardBg = '<SolidColorBrush x:Key="CardBg" Color="#22FFFFFF" />'
$oldCardBorder = '<SolidColorBrush x:Key="CardBorder" Color="#202026" />'
$newCardBorder = '<SolidColorBrush x:Key="CardBorder" Color="#38FFFFFF" />'

$content = $content.Replace($oldCardBg, $newCardBg)
$content = $content.Replace($oldCardBorder, $newCardBorder)

# 2. Add reusable GlassCardBrush & GlassCardBorderBrush to resources
$resInsertTarget = '<SolidColorBrush x:Key="CardBg" Color="#22FFFFFF" />'
$glassBrushes = @"
        <SolidColorBrush x:Key="GlassCardBg" Color="#22FFFFFF" />
        <SolidColorBrush x:Key="GlassCardBorder" Color="#38FFFFFF" />
        <SolidColorBrush x:Key="CardBg" Color="#22FFFFFF" />
"@
$content = $content.Replace($resInsertTarget, $glassBrushes)

# 3. Replace all dark card backgrounds with frosted glass colors:
# Replace #14141B with #20FFFFFF (Frosted Glass Card matching Dock)
# Replace #23232F with #38FFFFFF (Crisp translucent white border)
# Replace #0C0C10 with #20FFFFFF
# Replace #1C1C24 with #38FFFFFF
# Replace #1E1E28 dividers with #25FFFFFF
$content = $content.Replace('Background="#14141B"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#23232F"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#0C0C10"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#1C1C24"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Fill="#1E1E28"', 'Fill="#25FFFFFF"')

# Album art inner border: replace #20202A / #2E2E3C with glass
$content = $content.Replace('Background="#20202A"', 'Background="#2AFFFFFF"')
$content = $content.Replace('BorderBrush="#2E2E3C"', 'BorderBrush="#40FFFFFF"')

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml updated all cards to Frosted Glass."
