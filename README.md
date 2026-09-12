# Floating Pomodoro

A tiny always-on-top Pomodoro widget for Windows. C# / .NET 8 / WPF, no dependencies beyond the SDK.

```
Pomodoro/
├── BibleBuild.txt          the build guide this was built from
├── FloatingPomodoro/       the app (open the .csproj in VS / Rider, or use the CLI)
├── installer.iss           Inno Setup script (optional installer)
└── dist/                   self-contained single-file build (created by publish)
```

## Build and run

```powershell
cd FloatingPomodoro
dotnet build -c Release
.\bin\Release\net8.0-windows\FloatingPomodoro.exe
```

Self-contained single exe (no .NET install needed on the target machine):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\dist
```

Logic self-check (cycle, stats, settings clamping): `FloatingPomodoro.exe --selftest` returns exit code 0 on pass.

Installer: install [Inno Setup 6](https://jrsoftware.org/isinfo.php), run `ISCC installer.iss` after publishing. Output: `dist\FloatingPomodoroSetup.exe`.

## Using it

| Action | How |
| --- | --- |
| Start / pause | ▶ button, Space (widget focused), Ctrl+Alt+Space (global), tray menu |
| Reset / skip | ↻ / ⏭ buttons, R / S keys, Ctrl+Alt+R / Ctrl+Alt+Right |
| Move | drag anywhere on the widget |
| Mini mode | double-click the widget, or M |
| Hide / show | Esc, tray icon click, Ctrl+Alt+P |
| Everything else | right-click the widget or the ⋮ button, or the tray icon |

Closing the widget keeps the app in the tray (change in Settings › General). Exit via the menu.

Global hotkeys are Ctrl+Alt+key. If another program already owns one, the app registers Ctrl+Alt+Shift+key instead. Settings › General lists what actually got bound on your machine.

Tasks, statistics and settings each open in their own small window. Pick a task in the Tasks window to show it under the timer and have completed Pomodoros counted against it.

## Data

Everything is local JSON in `%LOCALAPPDATA%\FloatingPomodoro\` (`settings.json`, `tasks.json`, `history.json`). Delete the folder to reset. No account, no network.

## Not in v1 (by design)

Cloud sync, integrations, custom hotkey bindings, a tick sound, code signing (unsigned builds trigger SmartScreen on other machines).
