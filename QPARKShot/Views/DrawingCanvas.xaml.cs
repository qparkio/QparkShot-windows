using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using QPARKShot.Helpers;
using QPARKShot.Models;
using Windows.Foundation;
using Windows.UI;
using Bitmap = System.Drawing.Bitmap;
using Colors = Microsoft.UI.Colors;

namespace QPARKShot.Views;

/// <summary>
/// Image viewer + annotation surface. All annotations are stored in image-pixel
/// coordinates (model is source-of-truth, UI rebuilds shapes from model).
/// </summary>
public sealed partial class DrawingCanvas : UserControl
{
    public ToolType CurrentTool { get; set; } = ToolType.Freehand;
    public string CurrentColorHex { get; set; } = "#FF3B30";
    public double CurrentStrokeWidth { get; set; } = 4.0;
    public string TextInput { get; set; } = "Text Annotation";

    public List<Annotation> Annotations { get; private set; } = new();
    public Rect? CropRect { get; private set; } = null;

    private readonly Stack<List<Annotation>> _undoStack = new();
    private readonly Stack<List<Annotation>> _redoStack = new();

    private Annotation? _activeAnnotation;
    private Point _dragStart;

    public DrawingCanvas()
    {
        this.InitializeComponent();
    }

    public void LoadImage(Bitmap bitmap)
    {
        _ = LoadImageAsync(bitmap);
    }

    private async Task LoadImageAsync(Bitmap bitmap)
    {
        ImageContainer.Width = bitmap.Width;
        ImageContainer.Height = bitmap.Height;
        AnnotationCanvas.Width = bitmap.Width;
        AnnotationCanvas.Height = bitmap.Height;

        BackgroundImage.Source = await BitmapHelpers.ToBitmapImageAsync(bitmap);
        Annotations = new();
        CropRect = null;
        _undoStack.Clear();
        _redoStack.Clear();
        _undoStack.Push(SnapshotAnnotations());
        Rebuild();
    }

    public void Undo()
    {
        if (_undoStack.Count <= 1) return;
        _redoStack.Push(_undoStack.Pop());
        Annotations = SnapshotAnnotations(_undoStack.Peek());
        Rebuild();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var next = _redoStack.Pop();
        _undoStack.Push(next);
        Annotations = SnapshotAnnotations(next);
        Rebuild();
    }

    private List<Annotation> SnapshotAnnotations(List<Annotation>? source = null)
        => new(source ?? Annotations);

    private void RecordUndo()
    {
        _undoStack.Push(SnapshotAnnotations());
        _redoStack.Clear();
    }

    // ===== Pointer handlers =====

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(AnnotationCanvas).Position;
        _dragStart = pos;

        switch (CurrentTool)
        {
            case ToolType.Freehand:
                _activeAnnotation = new FreehandAnnotation
                {
                    ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth,
                    Points = new List<Point> { pos },
                };
                break;
            case ToolType.Arrow:
                _activeAnnotation = new ArrowAnnotation
                {
                    ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth,
                    Start = pos, End = pos,
                };
                break;
            case ToolType.Rectangle:
                _activeAnnotation = new RectangleAnnotation
                {
                    ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth,
                    Rect = new Rect(pos.X, pos.Y, 0, 0),
                };
                break;
            case ToolType.Text:
                var text = string.IsNullOrWhiteSpace(TextInput) ? "Text" : TextInput;
                Annotations.Add(new TextAnnotation
                {
                    ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth,
                    Position = pos, Text = text, FontSize = 18,
                });
                RecordUndo();
                Rebuild();
                _activeAnnotation = null;
                break;
            case ToolType.Select:
                CropRect = new Rect(pos.X, pos.Y, 0, 0);
                _activeAnnotation = null;
                break;
        }
        AnnotationCanvas.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeAnnotation == null && CurrentTool != ToolType.Select) return;
        var pos = e.GetCurrentPoint(AnnotationCanvas).Position;

        switch (_activeAnnotation)
        {
            case FreehandAnnotation fh:
                fh.Points.Add(pos);
                break;
            case ArrowAnnotation arrow:
                arrow.End = pos;
                break;
            case RectangleAnnotation r:
            {
                double x = Math.Min(_dragStart.X, pos.X);
                double y = Math.Min(_dragStart.Y, pos.Y);
                double w = Math.Abs(pos.X - _dragStart.X);
                double h = Math.Abs(pos.Y - _dragStart.Y);
                r.Rect = new Rect(x, y, w, h);
                break;
            }
        }

        if (CurrentTool == ToolType.Select)
        {
            double x = Math.Min(_dragStart.X, pos.X);
            double y = Math.Min(_dragStart.Y, pos.Y);
            double w = Math.Abs(pos.X - _dragStart.X);
            double h = Math.Abs(pos.Y - _dragStart.Y);
            CropRect = new Rect(x, y, w, h);
        }

        RebuildPreview();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        AnnotationCanvas.ReleasePointerCapture(e.Pointer);
        if (_activeAnnotation != null)
        {
            Annotations.Add(_activeAnnotation);
            _activeAnnotation = null;
            RecordUndo();
            Rebuild();
        }
        else if (CurrentTool == ToolType.Select)
        {
            Rebuild();
        }
    }

    // ===== Rendering of annotation overlay =====

    private void Rebuild()
    {
        AnnotationCanvas.Children.Clear();
        foreach (var a in Annotations) AddShape(a);
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        // Remove any "live" preview-only items (tagged with PreviewTag), then re-add current.
        for (int i = AnnotationCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (AnnotationCanvas.Children[i] is FrameworkElement fe && fe.Tag is string s && s == PreviewTag)
                AnnotationCanvas.Children.RemoveAt(i);
        }
        if (_activeAnnotation != null) AddShape(_activeAnnotation, isPreview: true);
        if (CropRect.HasValue && CropRect.Value.Width > 0 && CropRect.Value.Height > 0)
        {
            var dashed = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = CropRect.Value.Width,
                Height = CropRect.Value.Height,
                Stroke = new SolidColorBrush(Colors.Yellow),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Tag = PreviewTag,
            };
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(dashed, CropRect.Value.X);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(dashed, CropRect.Value.Y);
            AnnotationCanvas.Children.Add(dashed);
        }
    }

    private const string PreviewTag = "preview";

    private void AddShape(Annotation a, bool isPreview = false)
    {
        var brush = new SolidColorBrush(ColorHelpers.ToWinUI(ColorHelpers.FromHex(a.ColorHex)));
        UIElement? element = null;
        switch (a)
        {
            case FreehandAnnotation fh when fh.Points.Count >= 2:
            {
                var pl = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                foreach (var p in fh.Points) pl.Points.Add(p);
                element = pl;
                break;
            }
            case ArrowAnnotation arrow:
            {
                // Main line
                var line = new Line
                {
                    X1 = arrow.Start.X, Y1 = arrow.Start.Y,
                    X2 = arrow.End.X, Y2 = arrow.End.Y,
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(line, 0);
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(line, 0);
                if (isPreview) line.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(line);

                // Arrow head as a small polyline
                double angle = Math.Atan2(arrow.End.Y - arrow.Start.Y, arrow.End.X - arrow.Start.X);
                double headLen = Math.Max(8, a.StrokeWidth * 3);
                double left = angle + Math.PI - 0.4;
                double right = angle + Math.PI + 0.4;
                var head = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                    StrokeLineJoin = PenLineJoin.Round,
                };
                head.Points.Add(new Point(arrow.End.X + Math.Cos(left) * headLen, arrow.End.Y + Math.Sin(left) * headLen));
                head.Points.Add(new Point(arrow.End.X, arrow.End.Y));
                head.Points.Add(new Point(arrow.End.X + Math.Cos(right) * headLen, arrow.End.Y + Math.Sin(right) * headLen));
                if (isPreview) head.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(head);
                return; // already added both pieces
            }
            case RectangleAnnotation r:
            {
                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = r.Rect.Width,
                    Height = r.Rect.Height,
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                };
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(rect, r.Rect.X);
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(rect, r.Rect.Y);
                element = rect;
                break;
            }
            case TextAnnotation t:
            {
                var tb = new TextBlock
                {
                    Text = t.Text,
                    Foreground = brush,
                    FontSize = t.FontSize,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(tb, t.Position.X);
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(tb, t.Position.Y);
                element = tb;
                break;
            }
        }

        if (element != null)
        {
            if (isPreview && element is FrameworkElement fe) fe.Tag = PreviewTag;
            AnnotationCanvas.Children.Add(element);
        }
    }
}
