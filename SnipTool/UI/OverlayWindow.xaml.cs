using System;
using DrawingRect = System.Drawing.Rectangle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SnipTool.UI;

public partial class OverlayWindow : Window
{
    private System.Windows.Point _start;
    private bool _isSelecting;

public event Action<DrawingRect>? SelectionCompleted;
    public event Action? SelectionCanceled;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    public void PrepareAndShow()
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var width = SystemParameters.VirtualScreenWidth;
        var height = SystemParameters.VirtualScreenHeight;

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        SelectionRect.Visibility = Visibility.Collapsed;
        _isSelecting = false;
        Show();
        Activate();
        Focus();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _isSelecting = true;
        CaptureMouse();

        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _start.X;
        var deltaY = current.Y - _start.Y;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            var size = Math.Min(Math.Abs(deltaX), Math.Abs(deltaY));
            deltaX = Math.Sign(deltaX) * size;
            deltaY = Math.Sign(deltaY) * size;
        }

        var x = deltaX < 0 ? _start.X + deltaX : _start.X;
        var y = deltaY < 0 ? _start.Y + deltaY : _start.Y;
        var w = Math.Abs(deltaX);
        var h = Math.Abs(deltaY);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        ReleaseMouseCapture();
        _isSelecting = false;

        var x = Canvas.GetLeft(SelectionRect);
        var y = Canvas.GetTop(SelectionRect);
        var w = SelectionRect.Width;
        var h = SelectionRect.Height;

        if (w < 2 || h < 2)
        {
            CloseOverlay();
            SelectionCanceled?.Invoke();
            return;
        }

        var rect = ToScreenPixels(new Rect(x, y, w, h));
        CloseOverlay();
        SelectionCompleted?.Invoke(rect);
    }

    private DrawingRect ToScreenPixels(Rect rect)
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

        var left = (int)Math.Round((Left + rect.Left) * transform.M11);
        var top = (int)Math.Round((Top + rect.Top) * transform.M22);
        var width = (int)Math.Round(rect.Width * transform.M11);
        var height = (int)Math.Round(rect.Height * transform.M22);

        return new DrawingRect(left, top, Math.Max(1, width), Math.Max(1, height));
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOverlay();
            SelectionCanceled?.Invoke();
        }
    }

    private void CloseOverlay()
    {
        SelectionRect.Visibility = Visibility.Collapsed;
        Hide();
    }
}
