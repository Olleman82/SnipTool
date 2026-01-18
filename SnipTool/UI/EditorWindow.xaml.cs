using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SnipTool.UI;

public partial class EditorWindow : Window
{
    private interface IEditorAction
    {
        void Undo();
        void Redo();
    }

    private sealed class ElementAction : IEditorAction
    {
        private readonly System.Windows.Controls.Panel _panel;
        private readonly UIElement _element;

        public ElementAction(System.Windows.Controls.Panel panel, UIElement element)
        {
            _panel = panel;
            _element = element;
        }

        public void Undo()
        {
            if (_panel.Children.Contains(_element))
            {
                _panel.Children.Remove(_element);
            }
        }

        public void Redo()
        {
            if (!_panel.Children.Contains(_element))
            {
                _panel.Children.Add(_element);
            }
        }
    }

    private sealed class StrokeChangeAction : IEditorAction
    {
        private readonly InkCanvas _canvas;
        private readonly StrokeCollection _added;
        private readonly StrokeCollection _removed;

        public StrokeChangeAction(InkCanvas canvas, StrokeCollection added, StrokeCollection removed)
        {
            _canvas = canvas;
            _added = added;
            _removed = removed;
        }

        public void Undo()
        {
            foreach (var stroke in _added)
            {
                _canvas.Strokes.Remove(stroke);
            }

            _canvas.Strokes.Add(_removed);
        }

        public void Redo()
        {
            foreach (var stroke in _removed)
            {
                _canvas.Strokes.Remove(stroke);
            }

            _canvas.Strokes.Add(_added);
        }
    }
    private enum EditorTool
    {
        Pen,
        Arrow,
        Text,
        Mask,
        Eraser
    }

    private readonly string _filePath;
    private EditorTool _activeTool = EditorTool.Pen;
    private System.Windows.Point _startPoint;
    private Shape? _activeShape;
    private System.Windows.Controls.TextBox? _activeText;
    private double _imageWidth;
    private double _imageHeight;
    private System.Windows.Media.Color _strokeColor = System.Windows.Media.Colors.Black;
    private readonly Stack<IEditorAction> _undoStack = new();
    private readonly Stack<IEditorAction> _redoStack = new();
    private bool _isApplyingUndo;

    public EditorWindow(string filePath)
    {
        InitializeComponent();
        _filePath = filePath;

        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current is App app)
            {
                WindowThemeHelper.Apply(this, app.IsDarkMode);
            }
        };

        LoadImage();
        ConfigureInk();
        WireEvents();
        SetActiveTool(EditorTool.Pen);
        SetActiveColor(System.Windows.Media.Colors.Black);
        UpdateUndoRedoButtons();

        FileNameText.Text = filePath;
    }

    private void WireEvents()
    {
        PenToolButton.Checked += (_, _) => SetActiveTool(EditorTool.Pen);
        ArrowToolButton.Checked += (_, _) => SetActiveTool(EditorTool.Arrow);
        TextToolButton.Checked += (_, _) => SetActiveTool(EditorTool.Text);
        MaskToolButton.Checked += (_, _) => SetActiveTool(EditorTool.Mask);
        EraserToolButton.Checked += (_, _) => SetActiveTool(EditorTool.Eraser);

        ColorBlackButton.Checked += (_, _) => SetActiveColor(System.Windows.Media.Colors.Black);
        ColorWhiteButton.Checked += (_, _) => SetActiveColor(System.Windows.Media.Colors.White);
        ColorRedButton.Checked += (_, _) => SetActiveColor(System.Windows.Media.Color.FromRgb(226, 74, 74));
        ColorBlueButton.Checked += (_, _) => SetActiveColor(System.Windows.Media.Color.FromRgb(58, 168, 255));
        ColorYellowButton.Checked += (_, _) => SetActiveColor(System.Windows.Media.Color.FromRgb(242, 201, 76));

        SizeSlider.ValueChanged += (_, _) => UpdateInkSettings();

        ShapeLayer.MouseLeftButtonDown += OnSurfaceMouseDown;
        ShapeLayer.MouseMove += OnSurfaceMouseMove;
        ShapeLayer.MouseLeftButtonUp += OnSurfaceMouseUp;

        PreviewKeyDown += OnPreviewKeyDown;

        UndoButton.Click += (_, _) => Undo();
        RedoButton.Click += (_, _) => Redo();
        SaveButton.Click += (_, _) => SaveTo(_filePath);
        SaveCopyButton.Click += (_, _) => SaveTo(GetCopyPath());
        CloseButton.Click += (_, _) => Close();
    }

    private void LoadImage()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(_filePath);
        image.EndInit();
        image.Freeze();

        _imageWidth = image.PixelWidth;
        _imageHeight = image.PixelHeight;

        BaseImage.Source = image;
        BaseImage.Width = _imageWidth;
        BaseImage.Height = _imageHeight;
        DrawingSurface.Width = _imageWidth;
        DrawingSurface.Height = _imageHeight;
        ShapeLayer.Width = _imageWidth;
        ShapeLayer.Height = _imageHeight;
        InkLayer.Width = _imageWidth;
        InkLayer.Height = _imageHeight;
    }

    private void ConfigureInk()
    {
        InkLayer.Background = System.Windows.Media.Brushes.Transparent;
        InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        UpdateInkSettings();
        InkLayer.Strokes.StrokesChanged += OnStrokesChanged;
    }

    private void UpdateInkSettings()
    {
        var attributes = new DrawingAttributes
        {
            Color = _strokeColor,
            Width = SizeSlider.Value,
            Height = SizeSlider.Value,
            FitToCurve = true
        };
        InkLayer.DefaultDrawingAttributes = attributes;
    }

    private void SetActiveTool(EditorTool tool)
    {
        _activeTool = tool;

        PenToolButton.IsChecked = tool == EditorTool.Pen;
        ArrowToolButton.IsChecked = tool == EditorTool.Arrow;
        TextToolButton.IsChecked = tool == EditorTool.Text;
        MaskToolButton.IsChecked = tool == EditorTool.Mask;
        EraserToolButton.IsChecked = tool == EditorTool.Eraser;

        InkLayer.IsHitTestVisible = tool == EditorTool.Pen || tool == EditorTool.Eraser;
        ShapeLayer.IsHitTestVisible = tool != EditorTool.Pen && tool != EditorTool.Eraser;

        if (tool == EditorTool.Eraser)
        {
            InkLayer.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }
        else if (tool == EditorTool.Pen)
        {
            InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        }
        else
        {
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }
    }

    private void SetActiveColor(System.Windows.Media.Color color)
    {
        _strokeColor = color;

        ColorBlackButton.IsChecked = color == System.Windows.Media.Colors.Black;
        ColorWhiteButton.IsChecked = color == System.Windows.Media.Colors.White;
        ColorRedButton.IsChecked = color == System.Windows.Media.Color.FromRgb(226, 74, 74);
        ColorBlueButton.IsChecked = color == System.Windows.Media.Color.FromRgb(58, 168, 255);
        ColorYellowButton.IsChecked = color == System.Windows.Media.Color.FromRgb(242, 201, 76);

        UpdateInkSettings();
    }

    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool == EditorTool.Pen || _activeTool == EditorTool.Eraser)
        {
            return;
        }

        _startPoint = e.GetPosition(ShapeLayer);

        if (_activeTool == EditorTool.Text)
        {
            CreateTextEditor(_startPoint);
            return;
        }

        ShapeLayer.CaptureMouse();

        if (_activeTool == EditorTool.Mask)
        {
            var rect = CreateBlurRectangle(_startPoint, _startPoint);
            _activeShape = rect;
            ShapeLayer.Children.Add(rect);
        }
        else if (_activeTool == EditorTool.Arrow)
        {
            var path = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(_strokeColor),
                StrokeThickness = SizeSlider.Value,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = new SolidColorBrush(_strokeColor)
            };
            _activeShape = path;
            ShapeLayer.Children.Add(path);
        }
    }

    private void OnSurfaceMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_activeShape == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(ShapeLayer);

        if (_activeShape is System.Windows.Shapes.Rectangle rect)
        {
            var x = Math.Min(current.X, _startPoint.X);
            var y = Math.Min(current.Y, _startPoint.Y);
            var width = Math.Abs(current.X - _startPoint.X);
            var height = Math.Abs(current.Y - _startPoint.Y);

            UpdateBlurRectangle(rect, x, y, width, height);
        }
        else if (_activeShape is System.Windows.Shapes.Path path)
        {
            path.Data = BuildArrowGeometry(_startPoint, current);
        }
    }

    private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeShape == null)
        {
            return;
        }

        var finished = _activeShape;
        _activeShape = null;
        ShapeLayer.ReleaseMouseCapture();

        if (finished != null)
        {
            PushAction(new ElementAction(ShapeLayer, finished));
        }
    }

    private Geometry BuildArrowGeometry(System.Windows.Point start, System.Windows.Point end)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < 1)
        {
            return Geometry.Empty;
        }

        direction.Normalize();
        var arrowLength = Math.Min(22, length * 0.3);
        var arrowWidth = arrowLength * 0.5;

        var arrowBase = end - direction * arrowLength;
        var perpendicular = new Vector(-direction.Y, direction.X);
        var left = arrowBase + perpendicular * arrowWidth;
        var right = arrowBase - perpendicular * arrowWidth;

        var lineFigure = new PathFigure(start, new[] { new LineSegment(end, true) }, false);
        var headFigure = new PathFigure(end, new[]
        {
            new LineSegment(left, true),
            new LineSegment(right, true),
            new LineSegment(end, true)
        }, true);

        var geometry = new PathGeometry();
        geometry.Figures.Add(lineFigure);
        geometry.Figures.Add(headFigure);
        return geometry;
    }

    private void CreateTextEditor(System.Windows.Point position)
    {
        if (_activeText != null)
        {
            FinalizeTextEditor(_activeText);
        }

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = string.Empty,
            FontSize = 12 + SizeSlider.Value,
            Foreground = new SolidColorBrush(_strokeColor),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = new SolidColorBrush(_strokeColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2, 6, 2),
            MinWidth = 80
        };

        textBox.LostKeyboardFocus += (_, _) => FinalizeTextEditor(textBox);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                FinalizeTextEditor(textBox);
            }
        };

        _activeText = textBox;
        ShapeLayer.Children.Add(textBox);
        Canvas.SetLeft(textBox, position.X);
        Canvas.SetTop(textBox, position.Y);
        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
    }

    private void FinalizeTextEditor(System.Windows.Controls.TextBox textBox)
    {
        if (!ShapeLayer.Children.Contains(textBox))
        {
            return;
        }

        var text = textBox.Text.Trim();
        var left = Canvas.GetLeft(textBox);
        var top = Canvas.GetTop(textBox);
        ShapeLayer.Children.Remove(textBox);
        _activeText = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var block = new TextBlock
        {
            Text = text,
            FontSize = textBox.FontSize,
            Foreground = textBox.Foreground
        };

        ShapeLayer.Children.Add(block);
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, top);
        PushAction(new ElementAction(ShapeLayer, block));
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.Z)
        {
            e.Handled = true;
            Undo();
        }
        else if (e.Key == Key.Y)
        {
            e.Handled = true;
            Redo();
        }
    }

    private void PushAction(IEditorAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _isApplyingUndo = true;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        _isApplyingUndo = false;
        UpdateUndoRedoButtons();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _isApplyingUndo = true;
        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        _isApplyingUndo = false;
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
    }

    private void OnStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (_isApplyingUndo)
        {
            return;
        }

        if (e.Added.Count == 0 && e.Removed.Count == 0)
        {
            return;
        }

        var added = CloneStrokes(e.Added);
        var removed = CloneStrokes(e.Removed);
        PushAction(new StrokeChangeAction(InkLayer, added, removed));
    }

    private static StrokeCollection CloneStrokes(StrokeCollection strokes)
    {
        var clone = new StrokeCollection();
        foreach (var stroke in strokes)
        {
            clone.Add(stroke.Clone());
        }

        return clone;
    }

    private System.Windows.Shapes.Rectangle CreateBlurRectangle(System.Windows.Point start, System.Windows.Point end)
    {
        var rect = new System.Windows.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 0, 0, 0)),
            StrokeThickness = 1
        };

        var brush = new VisualBrush(BaseImage)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };

        rect.Fill = brush;
        rect.Effect = new BlurEffect { Radius = Math.Max(4, SizeSlider.Value * 2) };
        UpdateBlurRectangle(rect, start.X, start.Y, 1, 1);
        return rect;
    }

    private static void UpdateBlurRectangle(System.Windows.Shapes.Rectangle rect, double x, double y, double width, double height)
    {
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = width;
        rect.Height = height;

        if (rect.Fill is VisualBrush brush)
        {
            brush.Viewbox = new Rect(x, y, Math.Max(1, width), Math.Max(1, height));
            brush.Viewport = new Rect(x, y, Math.Max(1, width), Math.Max(1, height));
        }
    }

    private void SaveTo(string path)
    {
        if (_imageWidth <= 0 || _imageHeight <= 0)
        {
            return;
        }

        DrawingSurface.Measure(new System.Windows.Size(_imageWidth, _imageHeight));
        DrawingSurface.Arrange(new Rect(0, 0, _imageWidth, _imageHeight));
        DrawingSurface.UpdateLayout();

        var render = new RenderTargetBitmap(
            (int)_imageWidth,
            (int)_imageHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        render.Render(DrawingSurface);

        BitmapEncoder encoder = CreateEncoder(path);
        encoder.Frames.Add(BitmapFrame.Create(render));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    private static BitmapEncoder CreateEncoder(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".jpg" || ext == ".jpeg")
        {
            return new JpegBitmapEncoder { QualityLevel = 92 };
        }

        return new PngBitmapEncoder();
    }

    private string GetCopyPath()
    {
        var directory = System.IO.Path.GetDirectoryName(_filePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var name = System.IO.Path.GetFileNameWithoutExtension(_filePath);
        var ext = System.IO.Path.GetExtension(_filePath);
        var baseName = $"{name}_edit";
        var candidate = System.IO.Path.Combine(directory, baseName + ext);
        var counter = 1;

        while (File.Exists(candidate))
        {
            candidate = System.IO.Path.Combine(directory, $"{baseName}_{counter}{ext}");
            counter++;
        }

        return candidate;
    }
}
