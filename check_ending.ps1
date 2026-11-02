$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$raw = [System.IO.File]::ReadAllText($path)
if ($raw.Contains("`r`n")) {
    Write-Output "File is CRLF"
} else {
    Write-Output "File is LF only"
}
