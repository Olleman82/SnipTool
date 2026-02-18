# SnipTool

SnipTool is a Windows tray app for fast screenshot bursts: press hotkey, drag a region, and the image is saved immediately.

## Features
- Global hotkeys for region, window, and fullscreen capture
- Burst/session workflow for 5-30 captures in a row
- Automatic naming with incrementing counters
- Optional clipboard copy after save
- Toast actions (undo last capture, open folder)
- Built-in screenshot editor and library view

## Default Hotkeys
- `PrintScreen`: region capture
- `Alt+PrintScreen`: active window
- `Ctrl+PrintScreen`: fullscreen
- `Shift+PrintScreen`: repeat last region
- `Ctrl+Shift+R`: start region video recording
- `Ctrl+Shift+W`: start window video recording
- `Ctrl+Shift+F`: start fullscreen video recording
- `Ctrl+Shift+S`: stop video recording

## Requirements
- Windows 10/11
- .NET 8 SDK (for local build)

## Build From Source
```powershell
dotnet restore .\SnipTool\SnipTool.csproj
dotnet build .\SnipTool\SnipTool.csproj -c Debug -p:Platform=x64
dotnet run --project .\SnipTool\SnipTool.csproj -c Debug -p:Platform=x64
```

## Configuration
- Default save path is `%USERPROFILE%\Pictures\SnipTool`
- Settings are managed from the app UI
- Capture files are stored locally on disk

## Privacy
- SnipTool stores captures only on the local machine
- Clipboard copying is optional and controlled via settings
- No cloud upload is required for core functionality

## Project Layout
- `SnipTool/`: WPF application
- `SnipTool/UI/`: windows and overlay UI
- `SnipTool/Services/`: capture, hotkeys, settings, session logic
- `SnipTool/Models/`: app settings and models

## Contributing
Issues and pull requests are welcome.
