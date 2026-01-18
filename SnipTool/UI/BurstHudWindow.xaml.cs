using System.Windows;
using SnipTool.Models;

namespace SnipTool.UI;

public partial class BurstHudWindow : Window
{
    public BurstHudWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(BurstStatus status)
    {
        if (!status.IsActive)
        {
            Hide();
            return;
        }

        StatusText.Text = $"Burst active - {status.Count:D3}";
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
