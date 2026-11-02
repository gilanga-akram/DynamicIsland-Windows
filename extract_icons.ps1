Add-Type -AssemblyName System.Drawing

$apps = @(
  @{ Name='Explorer'; Path='C:\Windows\explorer.exe' },
  @{ Name='Chrome'; Path='C:\Program Files (x86)\Google\Chrome\Application\chrome.exe' },
  @{ Name='Edge'; Path='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' },
  @{ Name='Spotify'; Path='C:\Users\PC\AppData\Roaming\Spotify\Spotify.exe' },
  @{ Name='Discord'; Path='C:\Users\PC\AppData\Local\Discord\app-1.0.9266\Discord.exe' },
  @{ Name='Antigravity'; Path='C:\Users\PC\AppData\Local\Programs\antigravity\Antigravity.exe' },
  @{ Name='Notepad'; Path='C:\Windows\notepad.exe' }
)

foreach ($item in $apps) {
  $p = $item.Path
  $n = $item.Name
  if (Test-Path $p) {
    $ico = [System.Drawing.Icon]::ExtractAssociatedIcon($p)
    if ($ico) {
      $bmp = $ico.ToBitmap()
      $dst = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\assets\icon_$n.png"
      $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
      $bmp.Dispose()
      $ico.Dispose()
      Write-Host "Extracted: $n -> $dst"
    }
  } else {
    Write-Host "Missing: $p"
  }
}
