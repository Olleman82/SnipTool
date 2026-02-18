using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
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
    private VideoCaptureService? _videoCaptureService;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private OverlayWindow? _overlay;
    private UI.BurstHudWindow? _burstHud;
    private UI.LibraryWindow? _libraryWindow;
    private UI.VideoCaptureWindow? _videoWindow;
    private UI.VideoHudWindow? _videoHud;
    private UI.WelcomeWindow? _welcomeWindow;
    private Forms.ToolStripMenuItem? _startVideoItem;
    private Forms.ToolStripMenuItem? _stopVideoItem;
    private bool _isCapturing;
    private Rectangle? _lastRect;
    private LogService? _log;
    private bool _isVideoSelecting;
    private bool _pendingVideoAudio;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _log = new LogService();
        var assembly = Assembly.GetExecutingAssembly();
        var buildTime = File.GetLastWriteTime(assembly.Location);
        _log.Info($"App starting | {assembly.GetName().Version} | {buildTime:yyyy-MM-dd HH:mm:ss}");

        Directory.CreateDirectory(_settings.SaveRootPath);
        ApplyTheme(_settings.UseDarkMode);

        _sessionManager = new SessionManager(_settings);
        _captureService = new CaptureService();
        _videoCaptureService = new VideoCaptureService(_log);
        _videoCaptureService.RecordingStateChanged += isRecording =>
            Dispatcher.BeginInvoke(() => OnVideoRecordingStateChanged(isRecording));
        _videoCaptureService.StatusChanged += status =>
            Dispatcher.BeginInvoke(() => OnVideoStatusChanged(status));
        _videoCaptureService.RecordingFailed += message =>
            Dispatcher.BeginInvoke(() => ShowToast($"Recording failed: {message}"));
        _videoCaptureService.RecordingCompleted += path =>
            Dispatcher.BeginInvoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    ShowToast($"Saved video: {System.IO.Path.GetFileName(path)}");
                    _sessionManager?.RegisterSavedFile(path);
                    _libraryWindow?.Dispatcher.BeginInvoke(new Action(() => _libraryWindow.RefreshLibrary()));
                }
            });
        _overlay = new OverlayWindow();
        _overlay.SelectionCompleted += OnSelectionCompleted;
        _overlay.SelectionCanceled += () =>
        {
            _isCapturing = false;
            _isVideoSelecting = false;
        };

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

        _settingsWindow = new MainWindow(_settings, SaveSettings, GetBurstStatus, StartNewBurst, EndBurst);
        MainWindow = _settingsWindow;
        _settingsWindow.Hide();

        SetupTrayIcon();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ShowWelcomeIfNeeded));
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
        menu.Items.Add("New burst", null, (_, _) => StartNewBurst());
        menu.Items.Add("End burst", null, (_, _) => EndBurst());
        menu.Items.Add("Copy last to clipboard", null, (_, _) => CopyLastToClipboard());
        menu.Items.Add("Open last folder", null, (_, _) => OpenFolder(_sessionManager?.GetLastFolder()));
        menu.Items.Add("Library", null, (_, _) => ShowLibrary());
        _startVideoItem = new Forms.ToolStripMenuItem("Start video recording", null, (_, _) => StartVideoRecording(_settings?.VideoIncludeAudio ?? false));
        _stopVideoItem = new Forms.ToolStripMenuItem("Stop video recording", null, (_, _) => StopVideoRecording());
        menu.Items.Add(_startVideoItem);
        menu.Items.Add(_stopVideoItem);
        menu.Items.Add("Video capture...", null, (_, _) => ShowVideoCapture());
        menu.Items.Add("Quick guide", null, (_, _) => ShowWelcomeGuide(markAsSeen: false));
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
        UpdateVideoMenuState();
    }

    public void ApplyTheme(bool darkMode)
    {
        IsDarkMode = darkMode;
        var dictionaries = Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(darkMode ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        });
        if (_settingsWindow != null)
        {
            WindowThemeHelper.Apply(_settingsWindow, darkMode);
        }
        if (_welcomeWindow != null)
        {
            WindowThemeHelper.Apply(_welcomeWindow, darkMode);
        }
        if (_libraryWindow != null)
        {
            WindowThemeHelper.Apply(_libraryWindow, darkMode);
        }
        if (_videoWindow != null)
        {
            WindowThemeHelper.Apply(_videoWindow, darkMode);
        }
        if (_videoHud != null)
        {
            WindowThemeHelper.Apply(_videoHud, darkMode);
        }
    }

    public bool IsDarkMode { get; private set; }

    public BurstStatus GetBurstStatus()
    {
        if (_sessionManager == null)
        {
            return new BurstStatus(false, DateTime.Now, null, 0);
        }

        return new BurstStatus(_sessionManager.IsActive, _sessionManager.SessionStart, _sessionManager.SessionFolder, _sessionManager.Counter);
    }

    public void StartNewBurst()
    {
        _sessionManager?.StartNewSession();
        UpdateBurstHud();
        _settingsWindow?.RefreshBurstStatus();
    }

    public void EndBurst()
    {
        _sessionManager?.EndSession();
        UpdateBurstHud();
        _settingsWindow?.RefreshBurstStatus();
    }

    private void UpdateBurstHud()
    {
        if (_sessionManager == null)
        {
            return;
        }

        _burstHud ??= new UI.BurstHudWindow();
        _burstHud.UpdateStatus(new BurstStatus(_sessionManager.IsActive, _sessionManager.SessionStart, _sessionManager.SessionFolder, _sessionManager.Counter));
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
            _settingsWindow = new MainWindow(_settings ?? new AppSettings(), SaveSettings, GetBurstStatus, StartNewBurst, EndBurst);
            _settingsWindow.Hide();
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
        WindowThemeHelper.Apply(_settingsWindow, IsDarkMode);
    }

    private void ShowWelcomeIfNeeded()
    {
        if (_settings?.HasSeenWelcomeSplash == false)
        {
            ShowWelcomeGuide(markAsSeen: true);
        }
    }

    private void ShowWelcomeGuide(bool markAsSeen)
    {
        if (_settings == null)
        {
            return;
        }

        if (_welcomeWindow != null)
        {
            _welcomeWindow.Activate();
            return;
        }

        _welcomeWindow = new UI.WelcomeWindow(_settings);
        _welcomeWindow.OpenSettingsRequested += ShowSettings;
        _welcomeWindow.Closed += (_, _) =>
        {
            if (markAsSeen && _settingsService != null && _settings != null && _welcomeWindow?.DoNotShowAgain == true)
            {
                _settings.HasSeenWelcomeSplash = true;
                _settingsService.Save(_settings);
            }

            _welcomeWindow = null;
        };
        _welcomeWindow.Show();
        WindowThemeHelper.Apply(_welcomeWindow, IsDarkMode);
        _welcomeWindow.Activate();
    }

    public void ShowLibrary()
    {
        if (_settings == null)
        {
            return;
        }

        if (_libraryWindow != null)
        {
            _libraryWindow.Activate();
            return;
        }

        _libraryWindow = new UI.LibraryWindow(_settings)
        {
            Owner = _settingsWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _libraryWindow.Closed += (_, _) => _libraryWindow = null;
        WindowThemeHelper.Apply(_libraryWindow, IsDarkMode);
        _libraryWindow.ShowDialog();
    }

    public void ShowVideoCapture()
    {
        if (_videoCaptureService == null)
        {
            return;
        }

        if (_videoWindow != null)
        {
            _videoWindow.Activate();
            return;
        }

        _videoWindow = new UI.VideoCaptureWindow(this, _videoCaptureService)
        {
            Owner = _settingsWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _videoWindow.Closed += (_, _) => _videoWindow = null;
        WindowThemeHelper.Apply(_videoWindow, IsDarkMode);
        _videoWindow.Show();
    }

    public bool VideoIncludeAudioDefault => _settings?.VideoIncludeAudio == true;

    public void StartVideoRecording(bool includeAudio)
    {
        if (_videoCaptureService == null || _sessionManager == null)
        {
            return;
        }

        EnsureVideoWindowVisible();
        StartVideoHud();
        var path = _sessionManager.GetNextFilePath(".mp4");
        var started = _videoCaptureService.StartFullscreenRecording(path, includeAudio);
        if (started)
        {
            ShowToast($"Recording started: {System.IO.Path.GetFileName(path)}");
        }
        else
        {
            ShowToast("Recording failed to start");
        }
    }

    public void StartVideoWindowRecording(bool includeAudio)
    {
        if (_videoCaptureService == null || _sessionManager == null)
        {
            return;
        }

        EnsureVideoWindowVisible();
        StartVideoHud();
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            ShowToast("No active window to record");
            return;
        }

        var path = _sessionManager.GetNextFilePath(".mp4");
        var started = _videoCaptureService.StartWindowRecording(hwnd, path, includeAudio);
        if (started)
        {
            ShowToast($"Recording started: {System.IO.Path.GetFileName(path)}");
        }
        else
        {
            ShowToast("Recording failed to start");
        }
    }

    public void BeginVideoRegionCapture(bool includeAudio)
    {
        if (_overlay == null || _videoCaptureService == null)
        {
            return;
        }

        if (_videoCaptureService.IsRecording)
        {
            ShowToast("Recording already in progress");
            return;
        }

        EnsureVideoWindowVisible();
        StartVideoHud();
        _pendingVideoAudio = includeAudio;
        _isVideoSelecting = true;
        _isCapturing = true;
        _overlay.PrepareAndShow();
    }

    public void StopVideoRecording()
    {
        _videoCaptureService?.StopRecording();
        UpdateVideoHud();
    }

    public void PauseVideoRecording()
    {
        _videoCaptureService?.Pause();
        UpdateVideoHud();
    }

    public void ResumeVideoRecording()
    {
        _videoCaptureService?.Resume();
        UpdateVideoHud();
    }

    private void EnsureVideoWindowVisible()
    {
        if (_videoCaptureService == null)
        {
            return;
        }

        if (_videoWindow == null)
        {
            _videoWindow = new UI.VideoCaptureWindow(this, _videoCaptureService)
            {
                Owner = _settingsWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            _videoWindow.Closed += (_, _) => _videoWindow = null;
            WindowThemeHelper.Apply(_videoWindow, IsDarkMode);
            _videoWindow.Show();
            return;
        }

        _videoWindow.Show();
        _videoWindow.Activate();
    }

    private void StartVideoHud()
    {
        EnsureVideoHud();
        if (_videoHud == null || _videoCaptureService == null)
        {
            return;
        }

        _videoHud.LinkService(_videoCaptureService);
        UpdateVideoHud();
    }

    private void EnsureVideoHud()
    {
        if (_videoHud != null)
        {
            return;
        }

        _videoHud = new UI.VideoHudWindow();
        WindowThemeHelper.Apply(_videoHud, IsDarkMode);
        _videoHud.PauseResumeRequested += () =>
        {
            if (_videoCaptureService?.IsPaused == true)
            {
                ResumeVideoRecording();
            }
            else
            {
                PauseVideoRecording();
            }
        };
        _videoHud.StopRequested += () => StopVideoRecording();
    }

    private void HideVideoHud()
    {
        _videoHud?.Dispatcher.BeginInvoke(() => _videoHud.Hide());
    }

    private void UpdateVideoHud()
    {
        if (_videoCaptureService == null || _videoHud == null)
        {
            return;
        }

        if (!_videoHud.Dispatcher.CheckAccess())
        {
            _videoHud.Dispatcher.BeginInvoke(UpdateVideoHud);
            return;
        }

        if (!_videoCaptureService.IsRecording)
        {
            _videoHud.Hide();
            return;
        }

        var elapsed = _videoCaptureService.Elapsed;
        _videoHud.UpdateStatus(_videoCaptureService.IsRecording, _videoCaptureService.IsPaused, _videoCaptureService.IsStopping, elapsed);
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
                RepeatLastRegion();
                break;
            case HotkeyAction.VideoRegion:
                BeginVideoRegionCapture(_settings?.VideoIncludeAudio == true);
                break;
            case HotkeyAction.VideoWindow:
                StartVideoWindowRecording(_settings?.VideoIncludeAudio == true);
                break;
            case HotkeyAction.VideoFullscreen:
                StartVideoRecording(_settings?.VideoIncludeAudio == true);
                break;
            case HotkeyAction.VideoStop:
                StopVideoRecording();
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
        if (_isVideoSelecting)
        {
            _isVideoSelecting = false;
            StartVideoRegionFromRect(rect, _pendingVideoAudio);
            return;
        }

        SaveCapture(rect);
    }

    private void StartVideoRegionFromRect(Rectangle rect, bool includeAudio)
    {
        if (_videoCaptureService == null || _sessionManager == null)
        {
            return;
        }

        var screen = Forms.Screen.FromRectangle(rect);
        var bounds = screen.Bounds;
        var relativeRect = new Rectangle(rect.Left - bounds.Left, rect.Top - bounds.Top, rect.Width, rect.Height);
        var sourceRect = new ScreenRecorderLib.ScreenRect(relativeRect.Left, relativeRect.Top, relativeRect.Width, relativeRect.Height);
        var path = _sessionManager.GetNextFilePath(".mp4");
        var started = _videoCaptureService.StartRegionRecording(screen.DeviceName, sourceRect, path, includeAudio);
        if (started)
        {
            ShowToast($"Recording started: {System.IO.Path.GetFileName(path)}");
        }
        else
        {
            ShowToast("Recording failed to start");
        }
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
            _lastRect = rect;
            using var bmp = _captureService.CaptureRectangle(rect);
            var path = _sessionManager.GetNextFilePath(".png");
            _captureService.SaveBitmap(bmp, path);
            _sessionManager.RegisterSavedFile(path);

            if (_settings.CopyToClipboardAfterSave)
            {
                _captureService.CopyToClipboard(bmp);
            }

            _log?.Info($"Saved: {path}");
            UpdateBurstHud();
            _settingsWindow?.RefreshBurstStatus();
            ShowToast($"Saved: {Path.GetFileName(path)}", path, extraDurationMs: 2000);
        }
        catch (Exception ex)
        {
            _log?.Error("SaveCapture failed", ex);
        }
    }

    private void ShowToast(string message, string? editPath = null, int extraDurationMs = 0)
    {
        if (_settings == null)
        {
            return;
        }

        var toast = new ToastWindow();
        toast.EditRequested += () =>
        {
            if (!string.IsNullOrWhiteSpace(editPath))
            {
                OpenEditor(editPath);
                toast.Close();
            }
        };
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
        toast.ShowToast(message, _settings.ToastDurationMs + extraDurationMs, !string.IsNullOrWhiteSpace(editPath));
    }

    private void OpenEditor(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var editor = new EditorWindow(filePath);
        WindowThemeHelper.Apply(editor, IsDarkMode);
        editor.Show();
        editor.Activate();
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

    private void RepeatLastRegion()
    {
        if (_isCapturing)
        {
            return;
        }

        if (_lastRect == null)
        {
            ShowToast("No previous region yet");
            return;
        }

        SaveCapture(_lastRect.Value);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _videoCaptureService?.StopRecording();
        _log?.Info("App exit");
        base.OnExit(e);
    }

    private void OnVideoRecordingStateChanged(bool isRecording)
    {
        Dispatcher.Invoke(UpdateVideoMenuState);
    }

    private void OnVideoStatusChanged(ScreenRecorderLib.RecorderStatus status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (status == ScreenRecorderLib.RecorderStatus.Idle)
            {
                _videoHud?.Hide();
            }

            UpdateVideoHud();
        });
    }

    private void UpdateVideoMenuState()
    {
        if (_videoCaptureService == null)
        {
            return;
        }

        if (_startVideoItem != null)
        {
            _startVideoItem.Enabled = !_videoCaptureService.IsRecording;
        }

        if (_stopVideoItem != null)
        {
            _stopVideoItem.Enabled = _videoCaptureService.IsRecording;
        }
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
