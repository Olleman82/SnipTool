using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using SnipTool.Models;

namespace SnipTool;

public partial class MainWindow : Window
{
    private readonly Action<AppSettings> _onSave;
    private AppSettings _settings;

    public MainWindow(AppSettings settings, Action<AppSettings> onSave)
    {
        InitializeComponent();
        _settings = settings;
        _onSave = onSave;

        SaveButton.Click += OnSave;
        CloseButton.Click += (_, _) => Hide();
        ThemeToggle.Checked += (_, _) => ApplyTheme(true);
        ThemeToggle.Unchecked += (_, _) => ApplyTheme(false);

        LoadSettings();
        SetVersionText();
    }

    private void LoadSettings()
    {
        SavePathBox.Text = _settings.SaveRootPath;
        TemplateBox.Text = _settings.FileNameTemplate;
        ClipboardBox.IsChecked = _settings.CopyToClipboardAfterSave;
        HotkeyRectBox.Text = _settings.Hotkeys.Rectangle;
        HotkeyWindowBox.Text = _settings.Hotkeys.Window;
        HotkeyFullBox.Text = _settings.Hotkeys.Fullscreen;
        HotkeyCopyBox.Text = _settings.Hotkeys.CopyLast;
        ThemeToggle.IsChecked = _settings.UseDarkMode;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _settings.SaveRootPath = SavePathBox.Text.Trim();
        _settings.FileNameTemplate = TemplateBox.Text.Trim();
        _settings.CopyToClipboardAfterSave = ClipboardBox.IsChecked == true;
        _settings.Hotkeys.Rectangle = HotkeyRectBox.Text.Trim();
        _settings.Hotkeys.Window = HotkeyWindowBox.Text.Trim();
        _settings.Hotkeys.Fullscreen = HotkeyFullBox.Text.Trim();
        _settings.Hotkeys.CopyLast = HotkeyCopyBox.Text.Trim();
        _settings.UseDarkMode = ThemeToggle.IsChecked == true;

        _onSave(_settings);
        Hide();
    }

    private static void ApplyTheme(bool darkMode)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ApplyTheme(darkMode);
        }
    }

    private void SetVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "okänd";
        VersionText.Text = $"Version {version}";
    }

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failures to open the browser.
        }
        e.Handled = true;
    }
}
