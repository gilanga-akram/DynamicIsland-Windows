$git = "C:\Users\PC\AppData\Local\Microsoft\WinGet\Packages\Git.MinGit_Microsoft.Winget.Source_8wekyb3d8bbwe\cmd\git.exe"
$projectDir = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island"

Set-Location $projectDir

if (-not (Test-Path ".git")) {
    & $git init -b main
    & $git config user.name "RIOT"
    & $git config user.email "riot@local.dev"
}

# Create .gitignore to avoid committing huge build binaries or temp files
$gitignoreContent = @"
bin/
obj/
publish/
publish_self_contained/
.vs/
*.user
"@

Set-Content -Path ".gitignore" -Value $gitignoreContent -Encoding UTF8

& $git add .
& $git commit -m "feat: Dynamic Island Pro v1.4.1 release"
& $git log -1
