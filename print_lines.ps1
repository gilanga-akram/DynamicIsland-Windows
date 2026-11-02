$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
for ($i=188; $i -le 202; $i++) {
    Write-Output ("[$i] " + $lines[$i])
}
