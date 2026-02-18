using System;
using System.Windows;
using System.Windows.Threading;

namespace SnipTool.UI;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer = new();

    public event Action? UndoRequested;
    public event Action? OpenFolderRequested;
    public event Action? EditRequested;

    public ToastWindow()
    {
        InitializeComponent();
        EditButton.Click += (_, _) => EditRequested?.Invoke();
        UndoButton.Click += (_, _) => UndoRequested?.Invoke();
        OpenButton.Click += (_, _) => OpenFolderRequested?.Invoke();
        _timer.Tick += (_, _) => Close();
    }

    public void ShowToast(string message, int durationMs, bool showEdit)
    {
        MessageText.Text = message;
        EditButton.Visibility = showEdit ? Visibility.Visible : Visibility.Collapsed;
        _timer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _timer.Start();
        Show();
        UpdateLayout();
        PositionAtBottomRight();
    }

    private void PositionAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 16;
        Top = workArea.Bottom - ActualHeight - 16;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
