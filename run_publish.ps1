Stop-Process -Name "DynamicIsland" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

$projPath = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\DynamicIsland.csproj"
$outDir = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish_self_contained"

Write-Output "Starting publish..."
dotnet publish $projPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outDir

if ($LASTEXITCODE -eq 0) {
    Write-Output "PUBLISH_SUCCESS"
} else {
    Write-Output "PUBLISH_FAILED"
}
