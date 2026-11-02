; Inno Setup Script for Dynamic Island Pro
; Multi-PC / Laptop Universal Installer (Self-Contained .NET 9)

#define MyAppName "Dynamic Island Pro"
#define MyAppVersion "1.4.1"
#define MyAppPublisher "Dynamic Island Project"
#define MyAppExeName "DynamicIsland.exe"

[Setup]
AppId={{D3F9E2A8-4C8B-4261-B9D1-9B3B1F876523}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Dynamic Island Pro
DefaultGroupName=Dynamic Island Pro
AllowNoIcons=yes
OutputDir=E:\Installer
OutputBaseFilename=DynamicIsland-Setup-v1.4.1-Win64
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "indonesian"; MessagesFile: "Languages\Indonesian.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
english.AutoStartDesc=Launch Dynamic Island automatically on Windows startup (Auto-Start)
indonesian.AutoStartDesc=Jalankan Dynamic Island otomatis saat Windows menyala (Auto-Start)
chinesesimplified.AutoStartDesc=Windows 开机时自动启动灵动岛 (Auto-Start)
russian.AutoStartDesc=Запускать Dynamic Island автоматически при запуске Windows (Auto-Start)
german.AutoStartDesc=Dynamic Island automatisch beim Windows-Start ausführen (Auto-Start)
polish.AutoStartDesc=Uruchamiaj Dynamic Island automatycznie podczas startu systemu Windows (Auto-Start)
japanese.AutoStartDesc=Windows 起動時に Dynamic Island を自動起動する (Auto-Start)
korean.AutoStartDesc=Windows 시작 시 Dynamic Island 자동 실행 (Auto-Start)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutoStartDesc}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish_self_contained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Assets\app.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "Assets\logo.png"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "Assets\saweria_qr.png"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "Assets\paypal_qr.png"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "Assets\Sounds\*"; DestDir: "{app}\Assets\Sounds"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app.ico"
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\DynamicIslandPro"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DynamicIslandPro"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM DynamicIsland.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;
