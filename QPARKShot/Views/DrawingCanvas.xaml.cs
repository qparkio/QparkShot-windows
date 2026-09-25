using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;
using Bitmap = System.Drawing.Bitmap;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace QPARKShot.Views;

public partial class DrawingCanvas : UserControl
{
    public ToolType CurrentTool { get; set; } = ToolType.Freehand;
    public string CurrentColorHex { get; set; } = "#FF3B30";
    public double CurrentStrokeWidth { get; set; } = 4.0;
    public string TextInput { get; set; } = "Text Annotation";

    public List<Annotation> Annotations { get; private set; } = new();
    public WpfRect? CropRect { get; private set; }

    private EditorDraft _draft = new();
    private Bitmap? _bitmap;
    public event EventHandler? Changed;
    public bool ReadOnly { get; set; }
    public bool CanUndo => _draft.CanUndo;
    public bool CanRedo => _draft.CanRedo;
    public void ReleaseImage() { _bitmap?.Dispose(); _bitmap = null; BackgroundImage.Source = null; }
    public void ClearCrop() { if (CropRect == null) return; CropRect = null; RecordUndo(); Rebuild(); }

    private Annotation? _activeAnnotation;
    private WpfPoint _dragStart;
    private const string PreviewTag = "preview";

    public DrawingCanvas()
    {
        InitializeComponent();
    }

    public void LoadImage(Bitmap bitmap, EditorDraft? draft = null)
    {
        ReleaseImage();
        _bitmap = (Bitmap)bitmap.Clone();
        _draft = draft ?? new();
        var src = BitmapHelpers.ToBitmapSource(bitmap);
        BackgroundImage.Source = src;
        ImageContainer.Width = bitmap.Width;
        ImageContainer.Height = bitmap.Height;
        AnnotationCanvas.Width = bitmap.Width;
        AnnotationCanvas.Height = bitmap.Height;

        Restore();
    }

    private void Restore()
    {
        Annotations = _draft.Current.Annotations.Select(a => a.Copy()).ToList();
        CropRect = _draft.Current.CropRect;
        Rebuild();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Undo() { _draft.Undo(); Restore(); }
    public void Redo() { _draft.Redo(); Restore(); }
    private void RecordUndo()
    {
        _draft.Record(Annotations, CropRect);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ===== Pointer handlers =====

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly || _bitmap == null) return;
        var raw = e.GetPosition(AnnotationCanvas);
        var pos = new WpfPoint(Math.Clamp(raw.X, 0, _bitmap.Width), Math.Clamp(raw.Y, 0, _bitmap.Height));
        _dragStart = pos;

        switch (CurrentTool)
        {
            case ToolType.Freehand:
                _activeAnnotation = new FreehandAnnotation
                {
                    ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth,
                    Points = new List<WpfPoint> { pos },
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
                    Rect = new WpfRect(pos.X, pos.Y, 0, 0),
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
            case ToolType.Redact:
            case ToolType.Blur:
                _activeAnnotation = new AreaAnnotation { Rect = new WpfRect(pos.X, pos.Y, 0, 0), Blur = CurrentTool == ToolType.Blur, ColorHex = "#000000" };
                break;
            case ToolType.Callout:
                Annotations.Add(new CalloutAnnotation { Position = pos, Number = Annotations.OfType<CalloutAnnotation>().Select(c => c.Number).DefaultIfEmpty(0).Max() + 1, ColorHex = CurrentColorHex, StrokeWidth = CurrentStrokeWidth });
                RecordUndo(); Rebuild(); _activeAnnotation = null;
                break;
            case ToolType.Select:
                CropRect = new WpfRect(pos.X, pos.Y, 0, 0);
                _activeAnnotation = null;
                break;
        }
        AnnotationCanvas.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (ReadOnly || _bitmap == null) return;
        var raw = e.GetPosition(AnnotationCanvas);
        var pos = new WpfPoint(Math.Clamp(raw.X, 0, _bitmap.Width), Math.Clamp(raw.Y, 0, _bitmap.Height));

        switch (_activeAnnotation)
        {
            case AreaAnnotation area:
                area.Rect = new WpfRect(_dragStart, pos);
                break;
            case FreehandAnnotation fh: fh.Points.Add(pos); break;
            case ArrowAnnotation arrow: arrow.End = pos; break;
            case RectangleAnnotation r:
            {
                double x = Math.Min(_dragStart.X, pos.X);
                double y = Math.Min(_dragStart.Y, pos.Y);
                double w = Math.Abs(pos.X - _dragStart.X);
                double h = Math.Abs(pos.Y - _dragStart.Y);
                r.Rect = new WpfRect(x, y, w, h);
                break;
            }
        }

        if (CurrentTool == ToolType.Select)
        {
            double x = Math.Min(_dragStart.X, pos.X);
            double y = Math.Min(_dragStart.Y, pos.Y);
            double w = Math.Abs(pos.X - _dragStart.X);
            double h = Math.Abs(pos.Y - _dragStart.Y);
            CropRect = new WpfRect(x, y, w, h);
        }

        RebuildPreview();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly) return;
        AnnotationCanvas.ReleaseMouseCapture();
        if (_activeAnnotation != null)
        {
            Annotations.Add(_activeAnnotation);
            _activeAnnotation = null;
            RecordUndo();
            Rebuild();
        }
        else if (CurrentTool == ToolType.Select)
        {
            if (CropRect is { Width: < 2 } || CropRect is { Height: < 2 }) CropRect = null;
            RecordUndo();
            Rebuild();
        }
    }

    // ===== Rendering =====

    private void Rebuild()
    {
        AnnotationCanvas.Children.Clear();
        if (_bitmap != null)
        {
            using var rendered = WatermarkRenderer.Render(_bitmap, Annotations, null, WatermarkSettings.Disabled);
            if (rendered != null) BackgroundImage.Source = BitmapHelpers.ToBitmapSource(rendered);
        }
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        for (int i = AnnotationCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (AnnotationCanvas.Children[i] is FrameworkElement fe &&
                fe.Tag is string s && s == PreviewTag)
            {
                AnnotationCanvas.Children.RemoveAt(i);
            }
        }
        if (_activeAnnotation != null) AddShape(_activeAnnotation, isPreview: true);

        if (CropRect.HasValue && CropRect.Value.Width > 0 && CropRect.Value.Height > 0)
        {
            var dashed = new WpfRectangle
            {
                Width = CropRect.Value.Width,
                Height = CropRect.Value.Height,
                Stroke = Brushes.Yellow,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Tag = PreviewTag,
            };
            Canvas.SetLeft(dashed, CropRect.Value.X);
            Canvas.SetTop(dashed, CropRect.Value.Y);
            AnnotationCanvas.Children.Add(dashed);
        }
    }

    private void AddShape(Annotation a, bool isPreview)
    {
        var brush = new SolidColorBrush(ColorHelpers.ToWpf(ColorHelpers.FromHex(a.ColorHex)));
        switch (a)
        {
            case AreaAnnotation area:
                var areaShape = new WpfRectangle { Width = area.Rect.Width, Height = area.Rect.Height, Fill = area.Blur ? Brushes.Gray : Brushes.Black, Opacity = area.Blur ? 0.6 : 1, Tag = PreviewTag };
                Canvas.SetLeft(areaShape, area.Rect.X); Canvas.SetTop(areaShape, area.Rect.Y);
                AnnotationCanvas.Children.Add(areaShape);
                break;
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
                if (isPreview) pl.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(pl);
                break;
            }
            case ArrowAnnotation arrow:
            {
                var line = new Line
                {
                    X1 = arrow.Start.X, Y1 = arrow.Start.Y,
                    X2 = arrow.End.X,   Y2 = arrow.End.Y,
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                if (isPreview) line.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(line);

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
                head.Points.Add(new WpfPoint(arrow.End.X + Math.Cos(left) * headLen, arrow.End.Y + Math.Sin(left) * headLen));
                head.Points.Add(new WpfPoint(arrow.End.X, arrow.End.Y));
                head.Points.Add(new WpfPoint(arrow.End.X + Math.Cos(right) * headLen, arrow.End.Y + Math.Sin(right) * headLen));
                if (isPreview) head.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(head);
                break;
            }
            case RectangleAnnotation r:
            {
                var rect = new WpfRectangle
                {
                    Width = r.Rect.Width,
                    Height = r.Rect.Height,
                    Stroke = brush,
                    StrokeThickness = a.StrokeWidth,
                };
                Canvas.SetLeft(rect, r.Rect.X);
                Canvas.SetTop(rect, r.Rect.Y);
                if (isPreview) rect.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(rect);
                break;
            }
            case TextAnnotation t:
            {
                var tb = new TextBlock
                {
                    Text = t.Text,
                    Foreground = brush,
                    FontSize = t.FontSize,
                    FontWeight = FontWeights.SemiBold,
                };
                Canvas.SetLeft(tb, t.Position.X);
                Canvas.SetTop(tb, t.Position.Y);
                if (isPreview) tb.Tag = PreviewTag;
                AnnotationCanvas.Children.Add(tb);
                break;
            }
        }
    }
}
