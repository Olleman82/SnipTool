# SnipTool

SnipTool is a lightweight Windows tray app for ultra‑fast burst screenshots. The flow is simple: global hotkey → drag a rectangle → auto‑save. It’s optimized for taking 5–30 clips in a row with minimal friction.

## Features
- Global hotkeys for rectangle, window, and fullscreen capture
- Burst sessions that group clips into a dated segment folder
- Auto‑save with fast sequential naming
- Optional copy‑to‑clipboard after save
- Toast with quick actions (Undo / Open folder)
- Light + Dark theme toggle

## Defaults (dev)
- Save root: `D:\Screenshots`
- Filename template: `HHmmss_###.png`
- Hotkeys: `Ctrl+Shift+1/2/3` (rectangle/window/fullscreen), `Ctrl+Shift+C` (copy last)

## Build & Run
Requirements:
- Windows
- .NET 8 SDK

```bash
D:\dotnet\dotnet.exe build SnipTool\SnipTool.csproj -c Debug
D:\Appar\snip-tool\SnipTool\bin\Debug\net8.0-windows\SnipTool.exe
```

## Project Layout
- `SnipTool/` – WPF app (.NET 8)
- `SnipTool/UI/` – overlay and toast windows
- `SnipTool/Services/` – capture, hotkeys, settings
- `SnipTool/Models/` – settings model

## License
TBD
