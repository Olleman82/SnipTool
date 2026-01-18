using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SnipTool.Models;

namespace SnipTool;

public partial class MainWindow : Window
{
    private readonly Action<AppSettings> _onSave;
    private readonly Func<BurstStatus> _getBurstStatus;
    private readonly Action _startNewBurst;
    private readonly Action _endBurst;
    private AppSettings _settings;
    private AboutWindow? _aboutWindow;

    public MainWindow(
        AppSettings settings,
        Action<AppSettings> onSave,
        Func<BurstStatus> getBurstStatus,
        Action startNewBurst,
        Action endBurst)
    {
        InitializeComponent();
        _settings = settings;
        _onSave = onSave;
        _getBurstStatus = getBurstStatus;
        _startNewBurst = startNewBurst;
        _endBurst = endBurst;

        SaveButton.Click += OnSave;
        CloseButton.Click += (_, _) => Hide();
        AboutButton.Click += (_, _) => ShowAbout();
        LibraryButton.Click += (_, _) => ShowLibrary();
        VideoButton.Click += (_, _) => ShowVideoCapture();
        ThemeToggle.Checked += (_, _) => ApplyTheme(true);
        ThemeToggle.Unchecked += (_, _) => ApplyTheme(false);
        NewBurstButton.Click += (_, _) => { _startNewBurst(); RefreshBurstStatus(); };
        EndBurstButton.Click += (_, _) => { _endBurst(); RefreshBurstStatus(); };
        OpenPrintSettingsButton.Click += (_, _) => OpenPrintScreenSettings();
        ApplyRecommendedHotkeysButton.Click += (_, _) => ApplyRecommendedHotkeys();
        Closing += (_, e) => { e.Cancel = true; Hide(); };
        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this, _settings.UseDarkMode);

        SetupHotkeyCapture();
        LoadSettings();
        RefreshBurstStatus();
    }

    public void RefreshBurstStatus()
    {
        var status = _getBurstStatus();
        if (status.IsActive)
        {
            BurstStatusText.Text = $"Active since {status.StartedAt:HH:mm} ({status.Count:D3})";
            EndBurstButton.Visibility = Visibility.Visible;
        }
        else
        {
            BurstStatusText.Text = "Inactive (saving to root)";
            EndBurstButton.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadSettings()
    {
        SavePathBox.Text = _settings.SaveRootPath;
        TemplateBox.Text = _settings.FileNameTemplate;
        ClipboardBox.IsChecked = _settings.CopyToClipboardAfterSave;
        VideoAudioBox.IsChecked = _settings.VideoIncludeAudio;
        HotkeyRectBox.Text = _settings.Hotkeys.Rectangle;
        HotkeyWindowBox.Text = _settings.Hotkeys.Window;
        HotkeyFullBox.Text = _settings.Hotkeys.Fullscreen;
        HotkeyCopyBox.Text = _settings.Hotkeys.CopyLast;
        HotkeyVideoRegionBox.Text = _settings.Hotkeys.VideoRegion;
        HotkeyVideoWindowBox.Text = _settings.Hotkeys.VideoWindow;
        HotkeyVideoFullscreenBox.Text = _settings.Hotkeys.VideoFullscreen;
        HotkeyVideoStopBox.Text = _settings.Hotkeys.VideoStop;
        ThemeToggle.IsChecked = _settings.UseDarkMode;
    }

    private void SetupHotkeyCapture()
    {
        var boxes = new[]
        {
            HotkeyRectBox,
            HotkeyWindowBox,
            HotkeyFullBox,
            HotkeyCopyBox,
            HotkeyVideoRegionBox,
            HotkeyVideoWindowBox,
            HotkeyVideoFullscreenBox,
            HotkeyVideoStopBox
        };
        foreach (var box in boxes)
        {
            box.PreviewKeyDown += OnHotkeyKeyDown;
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
        }
    }

    private void OnHotkeyKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox box)
        {
            return;
        }

        if (e.Key == Key.Tab)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back)
        {
            box.Text = string.Empty;
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        parts.Add(NormalizeKeyName(key));
        box.Text = string.Join("+", parts);
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftShift || key == Key.RightShift
            || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LWin || key == Key.RWin;
    }

    private static string NormalizeKeyName(Key key)
    {
        if (key == Key.PrintScreen)
        {
            return "PrintScreen";
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            var digit = (char)('0' + (key - Key.D0));
            return digit.ToString();
        }

        return key.ToString();
    }

    private void OpenPrintScreenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true });
        }
        catch
        {
            // Ignore if Settings can't be opened.
        }
    }

    private void ApplyRecommendedHotkeys()
    {
        HotkeyRectBox.Text = "PrintScreen";
        HotkeyWindowBox.Text = "Alt+PrintScreen";
        HotkeyFullBox.Text = "Ctrl+PrintScreen";
        HotkeyCopyBox.Text = "Shift+PrintScreen";
        HotkeyVideoRegionBox.Text = "Ctrl+Shift+R";
        HotkeyVideoWindowBox.Text = "Ctrl+Shift+W";
        HotkeyVideoFullscreenBox.Text = "Ctrl+Shift+F";
        HotkeyVideoStopBox.Text = "Ctrl+Shift+S";
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _settings.SaveRootPath = SavePathBox.Text.Trim();
        _settings.FileNameTemplate = TemplateBox.Text.Trim();
        _settings.CopyToClipboardAfterSave = ClipboardBox.IsChecked == true;
        _settings.VideoIncludeAudio = VideoAudioBox.IsChecked == true;
        _settings.Hotkeys.Rectangle = HotkeyRectBox.Text.Trim();
        _settings.Hotkeys.Window = HotkeyWindowBox.Text.Trim();
        _settings.Hotkeys.Fullscreen = HotkeyFullBox.Text.Trim();
        _settings.Hotkeys.CopyLast = HotkeyCopyBox.Text.Trim();
        _settings.Hotkeys.VideoRegion = HotkeyVideoRegionBox.Text.Trim();
        _settings.Hotkeys.VideoWindow = HotkeyVideoWindowBox.Text.Trim();
        _settings.Hotkeys.VideoFullscreen = HotkeyVideoFullscreenBox.Text.Trim();
        _settings.Hotkeys.VideoStop = HotkeyVideoStopBox.Text.Trim();
        _settings.UseDarkMode = ThemeToggle.IsChecked == true;

        _onSave(_settings);
        Hide();
    }

    private static void ApplyTheme(bool darkMode)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ApplyTheme(darkMode);
            if (app.MainWindow is Window window)
            {
                WindowThemeHelper.Apply(window, darkMode);
            }
        }
    }

    private void ShowAbout()
    {
        _aboutWindow ??= new AboutWindow { Owner = this };
        _aboutWindow.Closed -= OnAboutClosed;
        _aboutWindow.Closed += OnAboutClosed;
        _aboutWindow.Show();
        _aboutWindow.Activate();
        if (System.Windows.Application.Current is App app)
        {
            WindowThemeHelper.Apply(_aboutWindow, app.IsDarkMode);
        }
    }

    private void OnAboutClosed(object? sender, EventArgs e)
    {
        _aboutWindow = null;
    }

    private void ShowLibrary()
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ShowLibrary();
        }
    }

    private void ShowVideoCapture()
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ShowVideoCapture();
        }
    }
}
