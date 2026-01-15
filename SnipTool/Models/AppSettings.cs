using System;

namespace SnipTool.Models;

public sealed class AppSettings
{
    public string SaveRootPath { get; set; } = @"D:\Screenshots";
    public string FileNameTemplate { get; set; } = "HHmmss_###";
    public bool CopyToClipboardAfterSave { get; set; } = true;
    public bool PlaySoundOnSave { get; set; } = false;
    public int ToastDurationMs { get; set; } = 2500;
    public bool UseDarkMode { get; set; } = false;
    public HotkeySettings Hotkeys { get; set; } = new();
}

public sealed class HotkeySettings
{
    public string Rectangle { get; set; } = "Ctrl+Shift+1";
    public string Window { get; set; } = "Ctrl+Shift+2";
    public string Fullscreen { get; set; } = "Ctrl+Shift+3";
    public string CopyLast { get; set; } = "Ctrl+Shift+C";
}
