$csPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($csPath, [System.Text.Encoding]::UTF8)

# Normalize line endings
$content = $content.Replace("`r`n", "`n")

$oldBlock = @"
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

$newBlock = @"
            // 4. Enforce Window State (Dynamic Island Stays Visible On Top During Game)
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (isBlocking)
            {
                // Collapse if currently open so it stays sleek and doesn't block gameplay
                if (_isExpanded && !_isPinned)
                {
                    CollapseIsland();
                }

                // Dynamic Island stays visible on top during gaming
                IslandBorder.Visibility = Visibility.Visible;
                if (Settings != null) this.Topmost = Settings.AlwaysOnTop;
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
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

$oldBlock = $oldBlock.Replace("`r`n", "`n")
$newBlock = $newBlock.Replace("`r`n", "`n")

if ($content.Contains($oldBlock)) {
    $content = $content.Replace($oldBlock, $newBlock)
    Write-Output "MainWindow.xaml.cs updated successfully."
} else {
    Write-Output "Old block not found."
}

$content = $content.Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($csPath, $content, [System.Text.Encoding]::UTF8)
