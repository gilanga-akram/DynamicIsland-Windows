$logPath = "C:\Users\PC\.gemini\antigravity\brain\2cac9276-16d9-4723-8202-32b2ff4964de\.system_generated\logs\transcript_full.jsonl"
$lines = [System.IO.File]::ReadAllLines($logPath)

foreach ($line in $lines) {
    if ($line.Contains("TabTimer") -and $line.Contains("TabTools") -and $line.Contains("MainWindow.xaml")) {
        try {
            $json = $line | ConvertFrom-Json
            if ($json.tool_calls) {
                foreach ($tc in $json.tool_calls) {
                    if ($tc.name -eq "write_to_file" -and $tc.args.TargetFile.EndsWith("MainWindow.xaml")) {
                        [System.IO.File]::WriteAllText("C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\prev_xaml.txt", $tc.args.CodeContent)
                        Write-Host "Found previous MainWindow.xaml!"
                        exit
                    }
                }
            }
        } catch { }
    }
}
