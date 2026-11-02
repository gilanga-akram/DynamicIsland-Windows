$csPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($csPath, [System.Text.Encoding]::UTF8)

# Normalize CRLF
$content = $content.Replace("`r`n", "`n")

# 1. Faster game check timer
$content = $content.Replace(
    "// Background Fullscreen / Game Detection Timer (every 1 second)`n            _gameCheckTimer = new DispatcherTimer`n            {`n                Interval = TimeSpan.FromSeconds(1)`n            };",
    "// Background Fullscreen / Game Detection Timer (fast 250ms check)`n            _gameCheckTimer = new DispatcherTimer`n            {`n                Interval = TimeSpan.FromMilliseconds(250)`n            };"
)

# 2. Behind game window enforcement in UpdateGameModeVisuals
$oldWindowState = @"
            // 4. Enforce Window State
            if (isBlocking)
            {
                // Collapse if currently open
                if (_isExpanded && !_isPinned)
                {
                    CollapseIsland();
                }

                if (Settings.GameAction == "AutoHide")
                {
                    IslandBorder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    IslandBorder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                IslandBorder.Visibility = Visibility.Visible;
            }
"@

$newWindowState = @"
            // 4. Enforce Window State (Stay Behind Game)
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (isBlocking)
            {
                // Collapse if currently open
                if (_isExpanded && !_isPinned)
                {
                    CollapseIsland();
                }

                if (this.Topmost) this.Topmost = false;
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }

                IslandBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                IslandBorder.Visibility = Visibility.Visible;
                if (Settings != null) this.Topmost = Settings.AlwaysOnTop;
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }
"@

$oldWindowState = $oldWindowState.Replace("`r`n", "`n")
$newWindowState = $newWindowState.Replace("`r`n", "`n")

if ($content.Contains($oldWindowState)) {
    $content = $content.Replace($oldWindowState, $newWindowState)
    Write-Output "Enforce Window State replaced successfully."
} else {
    Write-Output "Old window state block not found."
}

# Convert back to CRLF
$content = $content.Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($csPath, $content, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml.cs updated."
