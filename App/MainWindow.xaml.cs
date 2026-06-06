using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DocumentationImageHelper.Editor;

namespace DocumentationImageHelper;

/// <summary>
/// Main editor window. Owns the image document and translates toolbar choices
/// and mouse gestures on the canvas into baked edits on the image.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ImageDocument _doc = new();
    private readonly List<ToggleButton> _toolButtons = new();

    private ToolType _tool = ToolType.None;
    private Color _color = Colors.Red;

    // In-progress drawing state.
    private bool _drawing;
    private Point _start;
    private bool _shiftStraight;
    private List<Point> _strokePoints = new();
    private Shape? _preview;

    // Text entry state.
    private TextBox? _activeTextBox;

    // Middle-button panning state.
    private bool _panning;
    private Point _panStart;
    private double _panOffsetX;
    private double _panOffsetY;

    /// <summary>The line and shape outline thickness taken from the toolbar slider.</summary>
    private double Size => SizeSlider.Value;

    /// <summary>The font size for the text tool, taken from its own toolbar dropdown.</summary>
    private double TextFontSize =>
        double.TryParse((FontSizeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var value)
            ? value
            : 24;

    public MainWindow()
    {
        InitializeComponent();

        _toolButtons.AddRange(new[] { TextTool, LineTool, RectTool, CircleTool, OvalTool, CropTool });

        _doc.Changed += (_, _) => RefreshFromDocument();
        RefreshFromDocument();
    }

    // Reflects the current document state into the UI.
    private void RefreshFromDocument()
    {
        ImageView.Source = _doc.Current;

        if (_doc.Current != null)
        {
            Overlay.Width = _doc.Current.PixelWidth;
            Overlay.Height = _doc.Current.PixelHeight;
            Placeholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            Overlay.Width = 0;
            Overlay.Height = 0;
            Placeholder.Visibility = Visibility.Visible;
        }

        UndoButton.IsEnabled = _doc.CanUndo;
        RedoButton.IsEnabled = _doc.CanRedo;
    }

    // Toolbar: clipboard and history

    private void OnPasteClick(object sender, RoutedEventArgs e) => PasteFromClipboard();

    private void PasteFromClipboard()
    {
        CommitActiveText();

        var image = ClipboardService.TryGetImage();
        if (image == null)
        {
            Placeholder.Text = "Clipboard is not an image";
            Placeholder.Visibility = Visibility.Visible;
            return;
        }

        _doc.Reset(DrawingService.Normalize(image));
        Zoom.ScaleX = Zoom.ScaleY = 1;
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        CommitActiveText();
        _doc.Undo();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        CommitActiveText();
        _doc.Redo();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        CommitActiveText();
        if (_doc.Current != null)
            ClipboardService.SetImage(_doc.Current);
    }

    // Toolbar: tool and style selection

    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
            return;

        CommitActiveText();

        // Keep the tool buttons mutually exclusive.
        foreach (var other in _toolButtons)
        {
            if (!ReferenceEquals(other, button))
                other.IsChecked = false;
        }

        _tool = Enum.Parse<ToolType>((string)button.Tag);
    }

    private void OnToolUnchecked(object sender, RoutedEventArgs e)
    {
        // When no tool button is active, fall back to pan/zoom only.
        if (_toolButtons.All(b => b.IsChecked != true))
            _tool = ToolType.None;
    }

    private void OnColorChanged(object sender, SelectionChangedEventArgs e)
    {
        var name = (ColorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        _color = name switch
        {
            "Red" => Colors.Red,
            "Green" => Colors.Green,
            "Blue" => Colors.Blue,
            "White" => Colors.White,
            "Black" => Colors.Black,
            _ => _color
        };
    }

    // Keyboard shortcuts

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // While typing, let the text box handle its own keys.
        if (_activeTextBox != null)
            return;

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        switch (e.Key)
        {
            case Key.V:
                PasteFromClipboard();
                e.Handled = true;
                break;
            case Key.C:
                if (_doc.Current != null)
                    ClipboardService.SetImage(_doc.Current);
                e.Handled = true;
                break;
            case Key.Z:
                _doc.Undo();
                e.Handled = true;
                break;
            case Key.Y:
                _doc.Redo();
                e.Handled = true;
                break;
        }
    }

    // Canvas drawing

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_doc.HasImage || _tool == ToolType.None)
            return;

        var point = ClampToImage(e.GetPosition(Overlay));

        if (_tool == ToolType.Text)
        {
            BeginTextEntry(point);
            e.Handled = true;
            return;
        }

        _drawing = true;
        _start = point;
        _strokePoints = new List<Point> { point };
        _shiftStraight = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        Overlay.CaptureMouse();
        CreatePreview();
        e.Handled = true;
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || _preview == null)
            return;

        var point = ClampToImage(e.GetPosition(Overlay));

        switch (_tool)
        {
            case ToolType.Stroke when _shiftStraight:
                UpdateLinePreview(_start, point);
                break;
            case ToolType.Stroke:
                _strokePoints.Add(point);
                ((Polyline)_preview).Points.Add(point);
                break;
            case ToolType.Rectangle:
            case ToolType.Crop:
                UpdateBoundsPreview(MakeRect(_start, point));
                break;
            case ToolType.Oval:
                UpdateBoundsPreview(MakeRect(_start, point));
                break;
            case ToolType.Circle:
                double radius = Distance(_start, point);
                UpdateBoundsPreview(new Rect(_start.X - radius, _start.Y - radius, radius * 2, radius * 2));
                break;
        }
    }

    private void OnOverlayMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing)
            return;

        _drawing = false;
        Overlay.ReleaseMouseCapture();

        var point = ClampToImage(e.GetPosition(Overlay));
        RemovePreview();

        var current = _doc.Current;
        if (current == null)
            return;

        BitmapSource? result = null;

        switch (_tool)
        {
            case ToolType.Stroke when _shiftStraight:
                result = DrawingService.DrawLine(current, _start, point, _color, Size);
                break;
            case ToolType.Stroke:
                result = DrawingService.DrawStroke(current, _strokePoints, _color, Size);
                break;
            case ToolType.Rectangle:
            {
                var rect = MakeRect(_start, point);
                if (rect.Width >= 1 && rect.Height >= 1)
                    result = DrawingService.DrawRectangle(current, rect, _color, Size);
                break;
            }
            case ToolType.Oval:
            {
                var rect = MakeRect(_start, point);
                if (rect.Width >= 1 && rect.Height >= 1)
                {
                    var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                    result = DrawingService.DrawEllipse(current, center, rect.Width / 2, rect.Height / 2, _color, Size);
                }
                break;
            }
            case ToolType.Circle:
            {
                double radius = Distance(_start, point);
                if (radius >= 1)
                    result = DrawingService.DrawEllipse(current, _start, radius, radius, _color, Size);
                break;
            }
            case ToolType.Crop:
                result = CropTo(current, MakeRect(_start, point));
                break;
        }

        if (result != null)
            _doc.Commit(result);

        e.Handled = true;
    }

    // Builds the cropped bitmap, clamping the region to the image bounds.
    private static BitmapSource? CropTo(BitmapSource source, Rect rect)
    {
        int x = Math.Max(0, (int)Math.Round(rect.X));
        int y = Math.Max(0, (int)Math.Round(rect.Y));
        int w = Math.Min(source.PixelWidth - x, (int)Math.Round(rect.Width));
        int h = Math.Min(source.PixelHeight - y, (int)Math.Round(rect.Height));

        if (w < 1 || h < 1)
            return null;

        return DrawingService.Crop(source, new Int32Rect(x, y, w, h));
    }

    // Live preview helpers

    private void CreatePreview()
    {
        var brush = new SolidColorBrush(_color);

        switch (_tool)
        {
            case ToolType.Stroke when _shiftStraight:
                _preview = new Line
                {
                    Stroke = brush,
                    StrokeThickness = Size,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    X1 = _start.X,
                    Y1 = _start.Y,
                    X2 = _start.X,
                    Y2 = _start.Y
                };
                break;
            case ToolType.Stroke:
            {
                var polyline = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = Size,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                polyline.Points.Add(_start);
                _preview = polyline;
                break;
            }
            case ToolType.Crop:
                // Crop ignores colour and thickness; show a thin dashed marquee.
                _preview = new Rectangle
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                break;
            case ToolType.Rectangle:
                _preview = new Rectangle { Stroke = brush, StrokeThickness = Size };
                break;
            case ToolType.Oval:
            case ToolType.Circle:
                _preview = new Ellipse { Stroke = brush, StrokeThickness = Size };
                break;
        }

        if (_preview != null)
            Overlay.Children.Add(_preview);
    }

    private void UpdateLinePreview(Point a, Point b)
    {
        if (_preview is not Line line)
            return;
        line.X2 = b.X;
        line.Y2 = b.Y;
    }

    // Positions and sizes a rectangle or ellipse preview from a bounding rectangle.
    private void UpdateBoundsPreview(Rect rect)
    {
        if (_preview == null)
            return;
        Canvas.SetLeft(_preview, rect.X);
        Canvas.SetTop(_preview, rect.Y);
        _preview.Width = rect.Width;
        _preview.Height = rect.Height;
    }

    private void RemovePreview()
    {
        if (_preview != null)
        {
            Overlay.Children.Remove(_preview);
            _preview = null;
        }
    }

    // Text entry

    private void BeginTextEntry(Point origin)
    {
        CommitActiveText();

        var box = new TextBox
        {
            MinWidth = 24,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(_color),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = TextFontSize,
            AcceptsReturn = false,
            Tag = origin
        };

        Canvas.SetLeft(box, origin.X);
        Canvas.SetTop(box, origin.Y);

        box.LostKeyboardFocus += (_, _) => CommitActiveText();
        box.KeyDown += OnTextBoxKeyDown;

        Overlay.Children.Add(box);
        _activeTextBox = box;
        box.Focus();
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitActiveText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelActiveText();
            e.Handled = true;
        }
    }

    // Bakes the active text box into the image, if it holds any text.
    private void CommitActiveText()
    {
        if (_activeTextBox == null)
            return;

        var box = _activeTextBox;
        _activeTextBox = null; // cleared first so the LostFocus handler does not re-enter
        Overlay.Children.Remove(box);

        if (!string.IsNullOrEmpty(box.Text) && _doc.Current != null)
        {
            var origin = (Point)box.Tag;
            _doc.Commit(DrawingService.DrawText(_doc.Current, box.Text, origin, _color, box.FontSize));
        }
    }

    private void CancelActiveText()
    {
        if (_activeTextBox == null)
            return;
        var box = _activeTextBox;
        _activeTextBox = null;
        Overlay.Children.Remove(box);
    }

    // Zoom and pan

    private void OnScrollerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_doc.HasImage)
            return;

        double oldScale = Zoom.ScaleX;
        double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        double newScale = Math.Clamp(oldScale * factor, 0.1, 16);
        if (Math.Abs(newScale - oldScale) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        // Keep the point under the cursor fixed while zooming.
        Point mouse = e.GetPosition(Scroller);
        double absX = Scroller.HorizontalOffset + mouse.X;
        double absY = Scroller.VerticalOffset + mouse.Y;
        double relX = absX / oldScale;
        double relY = absY / oldScale;

        Zoom.ScaleX = Zoom.ScaleY = newScale;
        Scroller.UpdateLayout();

        Scroller.ScrollToHorizontalOffset(relX * newScale - mouse.X);
        Scroller.ScrollToVerticalOffset(relY * newScale - mouse.Y);
        e.Handled = true;
    }

    private void OnScrollerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton != MouseButtonState.Pressed)
            return;

        _panning = true;
        _panStart = e.GetPosition(Scroller);
        _panOffsetX = Scroller.HorizontalOffset;
        _panOffsetY = Scroller.VerticalOffset;
        Scroller.Cursor = Cursors.ScrollAll;
        Mouse.Capture(Scroller);
        e.Handled = true;
    }

    private void OnScrollerPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning)
            return;

        var point = e.GetPosition(Scroller);
        Scroller.ScrollToHorizontalOffset(_panOffsetX - (point.X - _panStart.X));
        Scroller.ScrollToVerticalOffset(_panOffsetY - (point.Y - _panStart.Y));
        e.Handled = true;
    }

    private void OnScrollerPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning || e.MiddleButton != MouseButtonState.Released)
            return;

        _panning = false;
        Scroller.ReleaseMouseCapture();
        Scroller.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    // Geometry helpers

    // Keeps a point within the image bounds so edits never fall outside the bitmap.
    private Point ClampToImage(Point point)
    {
        if (_doc.Current == null)
            return point;
        double x = Math.Clamp(point.X, 0, _doc.Current.PixelWidth);
        double y = Math.Clamp(point.Y, 0, _doc.Current.PixelHeight);
        return new Point(x, y);
    }

    private static Rect MakeRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
