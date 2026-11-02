# Dynamic Island Pro for Windows 10 & 11

![Dynamic Island Pro](Assets/logo.png)

A modern, fluid, macOS-inspired Dynamic Island and Floating Dock suite built natively with **C# / WPF on .NET 9**. Designed for productivity, gaming performance, media control, and minimal visual footprint.

---

## ✨ Features

- **Dynamic Island Hardware Notch**:
  - Seamless Apple hardware-style cutout fused to the top screen bezel.
  - Idle state: Deep pure notch black (`#000000`).
  - Expanded state: Translucent macOS crystal frosted glass with smooth cubic-ease spring animations.
  - Auto-dodging (`Mode Ngumpet`): Automatically lifts above browser tabs so you never lose visibility.
- **Smart Game Detection**:
  - Automatically detects full-screen games (FiveM / GTA, Steam, DirectX titles).
  - Keeps the Dynamic Island neatly on top as a compact notch while auto-hiding the Floating Dock to prevent input clipping.
- **Floating Dock with Launchpad**:
  - Fluid macOS-style glass dock with application launcher, real-time battery status, weather, and clock.
  - Adaptive horizontal layout that dynamically expands based on pinned applications without clipping tray indicators.
- **Real-Time Synced Lyrics & Media Controls**:
  - Apple Music & Spotify real-time synchronized karaoke scroller with manual offset calibration (-2s to +2s).
- **Integrated Productivity & Wellness**:
  - Pomodoro focus timer with stand reminder milestones.
  - Islamic Prayer Times (Jadwal Sholat Kemenag RI) with visual-only island banner alerts (zero intrusive adzan audio).
  - Quick System Action tiles (Lock, Sleep, Mute, Color Picker, Snip Tool, Clipboard History).
- **Native Hardware Control**:
  - Wi-Fi network switcher and native Windows Radio Bluetooth detection without PowerShell or UAC pop-ups.

---

## 🚀 Installation

### Option 1: Standalone Windows Installer
Download the latest pre-compiled installer:
- **[DynamicIsland-Setup-v1.4.1-Win64.exe](https://github.com/YourUsername/DynamicIsland-Windows/releases/latest)**

### Option 2: Windows Package Manager (`winget`)
```cmd
winget install RIOT.DynamicIslandPro
```

---

## ⌨️ Shortcuts & Hotkeys

| Shortcut | Action |
| :--- | :--- |
| `Alt + N` | Expand / Collapse Dynamic Island |
| `Left Click` (Notch) | Toggle expanded control deck |
| `Right Click` (Notch) | Open Settings window |
| `Mouse Scroll` (Notch) | Cycle through Island modes (Clock, Music, Lyrics) |

---

## 🛡️ Privacy & Security

Dynamic Island Pro processes all hardware stats, network lists, and prayer calculations strictly locally in memory. No telemetry or personal information is ever collected or sent to external servers. See [PRIVACY.md](PRIVACY.md) for details.

---

## 📄 License
This project is released for personal and community productivity use. Built by **RIOT**.
