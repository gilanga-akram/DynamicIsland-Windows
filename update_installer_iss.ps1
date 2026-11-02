$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\installer.iss'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^OutputBaseFilename=') {
        $lines[$i] = 'OutputBaseFilename=DynamicIsland-Setup-v1.4.1-Win64'
    }
    if ($lines[$i] -match '^#define MyAppVersion') {
        $lines[$i] = '#define MyAppVersion "1.4.1"'
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "installer.iss updated to v1.4.1"
