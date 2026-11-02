# Privacy Policy for Dynamic Island Pro

*Last updated: October 31, 2026*

This Privacy Policy explains how **Dynamic Island Pro** ("the Software") handles user information and device permissions.

---

### 1. Zero Personal Data Collection
Dynamic Island Pro **does not collect, harvest, store, or transmit any personal identifiable information (PII)**. There are no tracking scripts, third-party analytics SDKs, advertising frameworks, or user profiling mechanisms embedded within the application.

### 2. Local-Only Processing
All features within Dynamic Island Pro execute purely on the local machine:
- **System Metrics & Hardware State**: CPU, RAM, GPU utilization, master volume level, battery percentage, Wi-Fi SSIDs, and Bluetooth radio state are queried solely via official Windows Runtime and Win32 APIs for on-screen HUD rendering.
- **Media Information**: Song titles, artist metadata, and album artwork are retrieved locally via Windows System Media Transport Controls (SMTC) or the Apple Music / Spotify local sessions.
- **Prayer Schedules**: Calculations are computed purely through local ephemeris astronomical math using the city coordinates selected by the user. No GPS coordinates or live location tracking are gathered.
- **Local Configuration**: Application settings and dock shortcuts are saved locally in the user's roaming AppData directory (`settings.json` and `dock_config.json`).

### 3. Network Usage
Network activity is strictly limited to user-initiated actions:
- Downloading lyrics text for the active media track.
- Fetching local public weather condition metrics for the designated city.
- No device telemetry, crash logs, or identifiable logs are ever sent over the network.

### 4. Contact & Support
If you have questions regarding this policy or the software's architecture, you may reach out via the official project repository or direct support links within the application.
