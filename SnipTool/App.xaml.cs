using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Windows.Interop;
using SnipTool.Models;
using SnipTool.Services;
using SnipTool.UI;

namespace SnipTool;

public partial class App : System.Windows.Application
{
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private SessionManager? _sessionManager;
    private CaptureService? _captureService;
    private HotkeyManager? _hotkeyManager;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private OverlayWindow? _overlay;
    private bool _isCapturing;
    private LogService? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _log = new LogService();
        _log.Info("App starting");

        _settings.SaveRootPath = @"D:\Screenshots";
        _settingsService.Save(_settings);
        Directory.CreateDirectory(_settings.SaveRootPath);
        ApplyTheme(_settings.UseDarkMode);

        _sessionManager = new SessionManager(_settings);
        _captureService = new CaptureService();
        _overlay = new OverlayWindow();
        _overlay.SelectionCompleted += OnSelectionCompleted;
        _overlay.SelectionCanceled += () => _isCapturing = false;

        DispatcherUnhandledException += (_, args) =>
        {
            _log?.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _log?.Error("AppDomain unhandled exception", args.ExceptionObject as Exception);
        };

        var source = CreateHotkeySource();
        _hotkeyManager = new HotkeyManager(source);
        _hotkeyManager.HotkeyPressed += HandleHotkey;
        _hotkeyManager.RegisterFromSettings(_settings);

        _settingsWindow = new MainWindow(_settings, SaveSettings);
        _settingsWindow.Hide();

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "SnipTool"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("New burst", null, (_, _) => _sessionManager?.StartNewSession());
        menu.Items.Add("Open last folder", null, (_, _) => OpenFolder(_sessionManager?.GetLastFolder()));
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    public void ApplyTheme(bool darkMode)
    {
        var dictionaries = Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(darkMode ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        });
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/sniptool.ico", UriKind.Absolute);
            var resource = GetResourceStream(uri);
            if (resource?.Stream != null)
            {
                return new Icon(resource.Stream);
            }
        }
        catch
        {
            // Fall back to system icon.
        }

        return SystemIcons.Application;
    }

    private void ShowSettings()
    {
        if (_settingsWindow == null)
        {
            return;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void SaveSettings(AppSettings settings)
    {
        _settingsService?.Save(settings);
        _hotkeyManager?.RegisterFromSettings(settings);
    }

    private void HandleHotkey(HotkeyAction action)
    {
        if (_isCapturing)
        {
            return;
        }

        _log?.Info($"Hotkey: {action}");
        switch (action)
        {
            case HotkeyAction.Rectangle:
                StartRectangleCapture();
                break;
            case HotkeyAction.Window:
                CaptureForegroundWindow();
                break;
            case HotkeyAction.Fullscreen:
                CaptureFullscreen();
                break;
            case HotkeyAction.CopyLast:
                CopyLastToClipboard();
                break;
        }
    }

    private void StartRectangleCapture()
    {
        if (_overlay == null)
        {
            return;
        }

        _isCapturing = true;
        _log?.Info("Rectangle capture start");
        _overlay.PrepareAndShow();
    }

    private void OnSelectionCompleted(Rectangle rect)
    {
        _isCapturing = false;
        SaveCapture(rect);
    }

    private void CaptureFullscreen()
    {
        _isCapturing = true;
        var rect = GetVirtualScreenRect();
        _log?.Info($"Fullscreen capture: {rect}");
        SaveCapture(rect);
        _isCapturing = false;
    }

    private void CaptureForegroundWindow()
    {
        _isCapturing = true;
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            _log?.Warn("Window capture failed: no foreground window");
            _isCapturing = false;
            return;
        }

        var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            _log?.Warn("Window capture failed: invalid bounds");
            _isCapturing = false;
            return;
        }

        _log?.Info($"Window capture: {bounds}");
        SaveCapture(bounds);
        _isCapturing = false;
    }

    private void SaveCapture(Rectangle rect)
    {
        if (_captureService == null || _sessionManager == null || _settings == null)
        {
            return;
        }

        try
        {
            using var bmp = _captureService.CaptureRectangle(rect);
            var path = _sessionManager.GetNextFilePath(".png");
            _captureService.SaveBitmap(bmp, path);
            _sessionManager.RegisterSavedFile(path);

            if (_settings.CopyToClipboardAfterSave)
            {
                _captureService.CopyToClipboard(bmp);
            }

            _log?.Info($"Saved: {path}");
            ShowToast($"Saved: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _log?.Error("SaveCapture failed", ex);
        }
    }

    private void ShowToast(string message)
    {
        if (_settings == null)
        {
            return;
        }

        var toast = new ToastWindow();
        toast.UndoRequested += () =>
        {
            if (_sessionManager?.UndoLast() == true)
            {
                toast.Close();
            }
        };
        toast.OpenFolderRequested += () =>
        {
            OpenFolder(_sessionManager?.GetLastFolder());
            toast.Close();
        };
        toast.ShowToast(message, _settings.ToastDurationMs);
    }

    private void OpenFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void CopyLastToClipboard()
    {
        var path = _sessionManager?.GetLastFile();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var bmp = new Bitmap(path);
            _captureService?.CopyToClipboard(bmp);
            _log?.Info($"Copied to clipboard: {path}");
            ShowToast($"Copied: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _log?.Error("CopyLastToClipboard failed", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _log?.Info("App exit");
        base.OnExit(e);
    }

    private static HwndSource CreateHotkeySource()
    {
        var parameters = new HwndSourceParameters("SnipToolHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0x800000
        };
        return new HwndSource(parameters);
    }

    private static Rectangle GetVirtualScreenRect()
    {
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new Rectangle(x, y, width, height);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
