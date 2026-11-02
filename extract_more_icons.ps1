Add-Type -AssemblyName System.Drawing

$apps = @(
  @{ Name='Outlook'; Exe='C:\Program Files\Microsoft Office\Root\Office16\OUTLOOK.EXE'; Out='icon_Outlook.png' },
  @{ Name='CapCut'; Exe='C:\Users\PC\AppData\Local\CapCut\Apps\CapCut.exe'; Out='icon_CapCut.png' },
  @{ Name='Telegram'; Exe='C:\Users\PC\AppData\Roaming\Telegram Desktop\Telegram.exe'; Out='icon_Telegram.png' },
  @{ Name='Paint'; Exe='C:\Windows\System32\mspaint.exe'; Out='icon_Paint.png' },
  @{ Name='Calculator'; Exe='C:\Windows\System32\calc.exe'; Out='icon_Calculator.png' },
  @{ Name='SnippingTool'; Exe='C:\Windows\System32\SnippingTool.exe'; Out='icon_SnippingTool.png' }
)

foreach ($item in $apps) {
  $target = $item.Exe
  $outName = $item.Out
  if (Test-Path $target) {
    try {
      $ico = [System.Drawing.Icon]::ExtractAssociatedIcon($target)
      if ($ico) {
        $bmp = $ico.ToBitmap()
        $dest = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\assets\$outName"
        $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $ico.Dispose()
        Write-Host "Extracted: $outName"
      }
    } catch {
      Write-Host "Error extracting $target : $_"
    }
  } else {
    Write-Host "Target not found: $target"
  }
}
