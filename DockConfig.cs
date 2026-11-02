// Language: C#
// Filename: DockConfig.cs
// Runs on: Windows 10/11 .NET 9
// Targets: Dock item persistence — load/save JSON config for customizable dock

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland
{
    public class DockConfigItem
    {
        public string Name     { get; set; } = "";
        // Executable path, protocol URI (spotify:), URL, or UWP shell: URI
        public string Path     { get; set; } = "";
        // Optional fallback if primary path fails
        public string? Fallback { get; set; }
        // Builtin asset name like "Assets/icon_Explorer.png" — null = use exe icon or emoji
        public string? IconAsset { get; set; }
        // Hex background color for the icon tile, null = transparent
        public string? IconBg  { get; set; }
    }

    public class DockConfig
    {
        private static readonly string ConfigDir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "dock_config.json");

        public List<DockConfigItem> Items { get; set; } = new();

        // ── Load from disk, fall back to defaults ──
        public static DockConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<DockConfig>(json);
                    if (cfg != null && cfg.Items.Count > 0) return cfg;
                }
            }
            catch { }
            var defaults = GetDefault();
            defaults.Save(); // write defaults so file exists for next run
            return defaults;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(ConfigPath,
                    JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static DockConfig GetDefault() => new()
        {
            Items = new List<DockConfigItem>
            {
                new() { Name = "File Explorer",   Path = "explorer.exe",   IconAsset = "Assets/icon_Explorer.png" },
                new() { Name = "Spotify",         Path = "spotify:",       IconAsset = "Assets/icon_Spotify.png",     Fallback = "https://open.spotify.com" },
                new() { Name = "Microsoft Edge",  Path = "msedge.exe",     IconAsset = "Assets/icon_Edge.png",        Fallback = "https://bing.com" },
                new() { Name = "Email",           Path = "mailto:",        IconAsset = "Assets/icon_Email.png" },
                new() { Name = "WhatsApp",        Path = "whatsapp:",      IconAsset = "Assets/icon_WhatsApp.png",    Fallback = "https://web.whatsapp.com" },
                new() { Name = "Discord",         Path = "discord:",       IconAsset = "Assets/icon_Discord.png",     Fallback = "https://discord.com" },
                new() { Name = "Musik",           Path = "shell:AppsFolder\\AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", Fallback = "mswindowsmusic:" },
                new() { Name = "Catatan",         Path = "notepad.exe",    IconAsset = "Assets/icon_Notepad.png" },
                new() { Name = "Kalender",        Path = "outlookcal:" },
                new() { Name = "Pengaturan",      Path = "ms-settings:" },
                new() { Name = "Google Chrome",   Path = "chrome.exe",     IconAsset = "Assets/icon_Chrome.png",      Fallback = "https://google.com" },
                new() { Name = "Antigravity",     Path = "",               IconAsset = "Assets/icon_Antigravity.png" },
            }
        };
    }
}
