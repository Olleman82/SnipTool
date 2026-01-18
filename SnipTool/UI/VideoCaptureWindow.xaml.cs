using System;
using System.Windows;
using System.Windows.Threading;
using SnipTool.Services;

namespace SnipTool.UI;

public partial class VideoCaptureWindow : Window
{
    private readonly App _app;
    private readonly VideoCaptureService _videoService;
    private readonly DispatcherTimer _timer;

    public VideoCaptureWindow(App app, VideoCaptureService videoService)
    {
        InitializeComponent();
        _app = app;
        _videoService = videoService;

        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this, _app.IsDarkMode);

        StartButton!.Click += (_, _) => StartRecording();
        StopButton!.Click += (_, _) => StopRecording();
        PauseButton!.Click += (_, _) => TogglePause();
        DiscardButton!.Click += (_, _) => DiscardRecording();
        CloseButton!.Click += (_, _) => Close();

        _videoService.RecordingStateChanged += OnRecordingStateChanged;
        _videoService.RecordingCompleted += OnRecordingCompleted;
        _videoService.RecordingFailed += OnRecordingFailed;
        _videoService.StatusChanged += OnStatusChanged;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += (_, _) => UpdateTimer();

        Loaded += (_, _) => RefreshState();
        Closed += (_, _) =>
        {
            _videoService.RecordingStateChanged -= OnRecordingStateChanged;
            _videoService.RecordingCompleted -= OnRecordingCompleted;
            _videoService.RecordingFailed -= OnRecordingFailed;
            _videoService.StatusChanged -= OnStatusChanged;
            _timer.Stop();
        };

        IncludeAudioCheck!.IsChecked = _app.VideoIncludeAudioDefault;
    }

    private void StartRecording()
    {
        ResetTimer();
        var includeAudio = IncludeAudioCheck.IsChecked == true;
        if (SourceRegion.IsChecked == true)
        {
            StatusText.Text = "Select a region to record";
            _app.BeginVideoRegionCapture(includeAudio);
            return;
        }

        if (SourceWindow.IsChecked == true)
        {
            _app.StartVideoWindowRecording(includeAudio);
            return;
        }

        _app.StartVideoRecording(includeAudio);
        RefreshState();
    }

    private void StopRecording()
    {
        _app.StopVideoRecording();
        RefreshState();
    }

    private void TogglePause()
    {
        if (_videoService.IsPaused)
        {
            _app.ResumeVideoRecording();
        }
        else
        {
            _app.PauseVideoRecording();
        }
    }

    private void DiscardRecording()
    {
        if (_videoService.DeleteLastRecording())
        {
            PathText.Text = "Recording discarded";
        }
        RefreshState();
    }

    private void RefreshState()
    {
        if (_videoService.IsRecording)
        {
            StatusText.Text = _videoService.IsStopping
                ? "Stopping..."
                : _videoService.IsPaused ? "Paused" : "Recording";
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = !_videoService.IsStopping;
            PauseButton.IsEnabled = true;
            PauseButton.Content = _videoService.IsPaused ? "Resume" : "Pause";
            DiscardButton.IsEnabled = false;
            RecordingDot.Visibility = Visibility.Visible;
            PathText.Text = _videoService.CurrentPath ?? string.Empty;
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }
        else
        {
            StatusText.Text = "Ready to record";
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            PauseButton.Content = "Pause";
            DiscardButton.IsEnabled = !string.IsNullOrWhiteSpace(_videoService.LastCompletedPath);
            RecordingDot.Visibility = Visibility.Collapsed;
            TimerText.Text = string.Empty;
            _timer.Stop();
            PathText.Text = _videoService.LastCompletedPath == null
                ? string.Empty
                : $"Last recording: {_videoService.LastCompletedPath}";
        }
    }

    private void OnRecordingStateChanged(bool isRecording)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!isRecording)
            {
                ResetTimer();
            }
            RefreshState();
        });
    }

    private void OnRecordingCompleted(string path)
    {
        Dispatcher.BeginInvoke(() =>
        {
            PathText.Text = string.IsNullOrWhiteSpace(path) ? string.Empty : $"Saved: {path}";
            ResetTimer();
        });
    }

    private void OnRecordingFailed(string error)
    {
        Dispatcher.BeginInvoke(() =>
        {
            PathText.Text = string.IsNullOrWhiteSpace(error) ? "Recording failed" : error;
            ResetTimer();
        });
    }

    private void OnStatusChanged(ScreenRecorderLib.RecorderStatus status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RefreshState();
        });
    }

    private void UpdateTimer()
    {
        if (!_videoService.IsRecording)
        {
            TimerText.Text = string.Empty;
            return;
        }

        var elapsed = _videoService.Elapsed;
        TimerText.Text = elapsed.ToString(@"mm\:ss");
    }

    private void ResetTimer()
    {
        TimerText.Text = string.Empty;
    }
}
