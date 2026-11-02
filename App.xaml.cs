// Language: C#
// Filename: App.xaml.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Application lifecycle, single-instance takeover, and explicit window launch

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DynamicIsland
{
    public partial class App : Application
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPro");

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Takeover / Clean up any stale background instances
            try
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id != current.Id)
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }

            // 2. Exception logging to %APPDATA%\DynamicIslandPro\app.log
            try { Directory.CreateDirectory(AppDataDir); } catch { }
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            base.OnStartup(e);

            // 3. Explicitly construct and display MainWindow
            try
            {
                Console.WriteLine("[DIAG] Constructing MainWindow...");
                var win = new MainWindow();
                MainWindow = win;
                Console.WriteLine("[DIAG] Calling win.Show()...");
                win.Show();
                Console.WriteLine($"[DIAG] Dynamic Island started successfully. PID: {Process.GetCurrentProcess().Id}");
                File.AppendAllText(Path.Combine(AppDataDir, "app.log"), $"[{DateTime.Now}] Dynamic Island started successfully. PID: {Process.GetCurrentProcess().Id}\n");

                if (e.Args.Length > 0 && e.Args[0] == "--settings")
                {
                    win.OpenSettingsWindow();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Startup Exception: {ex}");
                File.AppendAllText(Path.Combine(AppDataDir, "app.log"), $"[{DateTime.Now}] Fatal MainWindow Startup Error: {ex}\n");
                MessageBox.Show($"Gagal memulai Dynamic Island:\n{ex.Message}", "Dynamic Island Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDataDir, "app.log"), $"[{DateTime.Now}] Dispatcher Error: {e.Exception}\n");
            }
            catch { }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDataDir, "app.log"), $"[{DateTime.Now}] Domain Error: {e.ExceptionObject}\n");
            }
            catch { }
        }
    }
}
