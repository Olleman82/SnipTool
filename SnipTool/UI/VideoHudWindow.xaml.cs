using System;
using System.Windows;

namespace SnipTool.UI;

public partial class VideoHudWindow : Window
{
    public event Action? PauseResumeRequested;
    public event Action? StopRequested;

    public VideoHudWindow()
    {
        InitializeComponent();
        PauseButton.Click += (_, _) => PauseResumeRequested?.Invoke();
        StopButton.Click += (_, _) => StopRequested?.Invoke();
    }

    public void UpdateStatus(bool isRecording, bool isPaused, bool isStopping, TimeSpan elapsed)
    {
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
