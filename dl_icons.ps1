$wc = New-Object System.Net.WebClient
$wc.Headers.Add("User-Agent", "curl/7.88.1")
try {
    $wc.DownloadFile("https://raw.githubusercontent.com/walkxcode/dashboard-icons/main/png/whatsapp.png", "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\assets\icon_WhatsApp.png")
    $wc.DownloadFile("https://raw.githubusercontent.com/walkxcode/dashboard-icons/main/png/gmail.png", "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\assets\icon_Email.png")
    Write-Host "Success downloaded WhatsApp and Gmail!"
} catch {
    Write-Host "Error: $_"
}
