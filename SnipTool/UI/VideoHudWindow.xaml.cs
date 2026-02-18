using System;
using System.Windows;
using System.Windows.Media;
using SnipTool.Services;

namespace SnipTool.UI;

public partial class VideoHudWindow : Window
{
    private VideoCaptureService? _videoService;
    private bool _isRecording;
    private bool _isPaused;
    private bool _isStopping;

    public event Action? PauseResumeRequested;
    public event Action? StopRequested;

    public VideoHudWindow()
    {
        InitializeComponent();
        PauseButton.Click += (_, _) => PauseResumeRequested?.Invoke();
        StopButton.Click += (_, _) => StopRequested?.Invoke();
        
        CompositionTarget.Rendering += OnRendering;
    }

    public void LinkService(VideoCaptureService service)
    {
        _videoService = service;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_videoService == null || !_isRecording || _isStopping) return;
        
        // Update timer every frame (very efficient in WPF)
        var elapsed = _videoService.Elapsed;
        TimerText.Text = elapsed.ToString(@"mm\:ss");
    }

    public void UpdateStatus(bool isRecording, bool isPaused, bool isStopping, TimeSpan elapsed)
    {
        _isRecording = isRecording;
        _isPaused = isPaused;
        _isStopping = isStopping;

        if (!isRecording)
        {
            Hide();
            return;
        }

        StatusText.Text = isStopping ? "Stopping" : isPaused ? "Paused" : "Recording";
        RecDot.Visibility = isStopping ? Visibility.Collapsed : Visibility.Visible;
        TimerText.Text = elapsed.ToString(@"mm\:ss");
        PauseButton.IsEnabled = !isStopping;
        StopButton.IsEnabled = !isStopping;
        PauseButton.Content = isPaused ? "Resume" : "Pause";

        if (!IsVisible)
        {
            Show();
        }

        PositionTopRight();
    }

    private void PositionTopRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Top + 16;
    }
}
