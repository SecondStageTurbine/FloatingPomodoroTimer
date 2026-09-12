; Inno Setup script for Floating Pomodoro.
; 1. dotnet publish (see README) so dist\FloatingPomodoro.exe exists
; 2. ISCC installer.iss  ->  dist\FloatingPomodoroSetup.exe

#define AppName "Floating Pomodoro"
#define AppVersion "1.0.3"
#define AppExe "FloatingPomodoro.exe"

[Setup]
AppId={{7C1D7A2E-5B3F-4E7B-9C6A-2F0E9D3B1A11}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=dist
OutputBaseFilename=FloatingPomodoroSetup
VersionInfoVersion={#AppVersion}
SetupIconFile=FloatingPomodoro\Resources\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
; The widget cancels WM_CLOSE to stay in the tray, so a polite close cannot free the exe
; on upgrade. All state is written as it changes, so terminating loses nothing.
CloseApplications=force

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"
Name: "startup"; Description: "Launch {#AppName} at startup"

[Files]
Source: "dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FloatingPomodoro"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
