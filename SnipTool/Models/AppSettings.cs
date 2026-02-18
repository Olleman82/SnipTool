using System;
using System.IO;

namespace SnipTool.Models;

public sealed class AppSettings
{
    public string SaveRootPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "SnipTool");
    public string FileNameTemplate { get; set; } = "{date}_{time}_{counter}";
    public bool CopyToClipboardAfterSave { get; set; } = true;
    public bool PlaySoundOnSave { get; set; } = false;
    public int ToastDurationMs { get; set; } = 2500;
    public bool UseDarkMode { get; set; } = false;
    public bool VideoIncludeAudio { get; set; } = false;
    public bool HasSeenWelcomeSplash { get; set; } = false;
    public HotkeySettings Hotkeys { get; set; } = new();
}

public sealed class HotkeySettings
{
    public string Rectangle { get; set; } = "PrintScreen";
    public string Window { get; set; } = "Alt+PrintScreen";
    public string Fullscreen { get; set; } = "Ctrl+PrintScreen";
    public string CopyLast { get; set; } = "Shift+PrintScreen";
    public string VideoRegion { get; set; } = "Ctrl+Shift+R";
    public string VideoWindow { get; set; } = "Ctrl+Shift+W";
    public string VideoFullscreen { get; set; } = "Ctrl+Shift+F";
    public string VideoStop { get; set; } = "Ctrl+Shift+S";
}
