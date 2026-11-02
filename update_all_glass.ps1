$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$content = [System.IO.File]::ReadAllText($path)

# 1. Update HeaderIconBtn default Foreground from #8E8E93 to #F0F5FF (Crisp bright white)
$content = $content.Replace('<Setter Property="Foreground" Value="#8E8E93" />', '<Setter Property="Foreground" Value="#F0F5FF" />')

# 2. In ViewTimer, update dark borders and backgrounds
# Card 0: Background="#0C0D12" -> Background="#20FFFFFF", BorderBrush="#1F222E" -> BorderBrush="#38FFFFFF"
$content = $content.Replace('Background="#0C0D12"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#1F222E"', 'BorderBrush="#38FFFFFF"')

# Stand milestone pill: Background="#161822" BorderBrush="#262A38" -> Background="#25FFFFFF" BorderBrush="#40FFFFFF"
$content = $content.Replace('Background="#161822"', 'Background="#25FFFFFF"')
$content = $content.Replace('BorderBrush="#262A38"', 'BorderBrush="#40FFFFFF"')

# Mode capsules:
$content = $content.Replace('Background="#1E202B"', 'Background="#28FFFFFF"')
$content = $content.Replace('BorderBrush="#353A4C"', 'BorderBrush="#45FFFFFF"')
$content = $content.Replace('Background="#14151D"', 'Background="#18FFFFFF"')
$content = $content.Replace('BorderBrush="#202330"', 'BorderBrush="#30FFFFFF"')
$content = $content.Replace('BorderBrush="#2C3040"', 'BorderBrush="#40FFFFFF"')

# Card 1: Background="#12131A" BorderBrush="#222634" -> Background="#20FFFFFF" BorderBrush="#38FFFFFF"
$content = $content.Replace('Background="#12131A"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#222634"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#1A1D28"', 'Background="#22FFFFFF"')

# Preset pill buttons in Timer: Background="#1C1C24" -> Background="#25FFFFFF"
$content = $content.Replace('Background="#1C1C24"', 'Background="#25FFFFFF"')

# ViewHealth dark backgrounds
$content = $content.Replace('Background="#141824"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#222C44"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('BorderBrush="#262C40"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#18181F"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#2B2B38"', 'BorderBrush="#38FFFFFF"')

# ViewTodo dark backgrounds
$content = $content.Replace('Background="#161622"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#252535"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('BorderBrush="#242436"', 'BorderBrush="#38FFFFFF"')

# ViewPerf dark backgrounds
$content = $content.Replace('Background="#16161C"', 'Background="#20FFFFFF"')
$content = $content.Replace('Background="#1F2A38"', 'Background="#28FFFFFF"')

# ViewGameMode dark backgrounds
$content = $content.Replace('Background="#121624"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#1E2A44"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#141822"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#222838"', 'BorderBrush="#38FFFFFF"')

# ViewShelf dark backgrounds
$content = $content.Replace('Background="#101018"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#2A2A38"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#1C1C28"', 'Background="#2AFFFFFF"')
$content = $content.Replace('BorderBrush="#2C2C3C"', 'BorderBrush="#40FFFFFF"')
$content = $content.Replace('Background="#151522"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#2A2A3E"', 'BorderBrush="#38FFFFFF"')

# ViewClipboard dark backgrounds
$content = $content.Replace('Background="#14141C"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#242432"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#12121A"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#1E1E28"', 'BorderBrush="#38FFFFFF"')

# ViewWeather dark backgrounds
$content = $content.Replace('Background="#222834"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#323B4C"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#2C3444"', 'Background="#28FFFFFF"')

# ViewTaskbar dark backgrounds
$content = $content.Replace('Background="#121218"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#252530"', 'BorderBrush="#38FFFFFF"')
$content = $content.Replace('Background="#101016"', 'Background="#20FFFFFF"')
$content = $content.Replace('BorderBrush="#202028"', 'BorderBrush="#38FFFFFF"')

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml updated all views with frosted glass & bright icons."
