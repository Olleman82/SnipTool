using System;
using System.Windows;
using SnipTool.Models;

namespace SnipTool.UI;

public partial class WelcomeWindow : Window
{
    public event Action? OpenSettingsRequested;

    public WelcomeWindow(AppSettings settings)
    {
        InitializeComponent();
        PopulateHotkeys(settings);
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current is App app)
            {
                WindowThemeHelper.Apply(this, app.IsDarkMode);
            }
        };

        OpenSettingsButton.Click += (_, _) =>
        {
            OpenSettingsRequested?.Invoke();
            Close();
        };
        StartButton.Click += (_, _) => Close();
    }

    public bool DoNotShowAgain => DontShowAgainCheckBox.IsChecked == true;

    private void PopulateHotkeys(AppSettings settings)
    {
        RectHotkeyText.Text = settings.Hotkeys.Rectangle;
        WindowHotkeyText.Text = settings.Hotkeys.Window;
        FullscreenHotkeyText.Text = settings.Hotkeys.Fullscreen;
        RepeatHotkeyText.Text = settings.Hotkeys.CopyLast;
        VideoRegionHotkeyText.Text = settings.Hotkeys.VideoRegion;
        VideoWindowHotkeyText.Text = settings.Hotkeys.VideoWindow;
        VideoFullscreenHotkeyText.Text = settings.Hotkeys.VideoFullscreen;
        VideoStopHotkeyText.Text = settings.Hotkeys.VideoStop;
    }
}
