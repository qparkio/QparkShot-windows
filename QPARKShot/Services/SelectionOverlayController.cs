using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using GdiRectangle = System.Drawing.Rectangle;

namespace QPARKShot.Services;

/// <summary>
/// Opens a borderless top-most window spanning the entire virtual desktop,
/// lets the user drag a selection rectangle, returns the region in screen pixels.
/// Returns null on cancel (ESC, click without drag).
/// </summary>
public static class SelectionOverlayController
{
    public static Task<GdiRectangle?> SelectRegionAsync()
    {
        var tcs = new TaskCompletionSource<GdiRectangle?>();
        var bounds = ScreenInfo.VirtualScreenBounds();

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
            Topmost = true,
            ShowInTaskbar = false,
            Title = "QPARK Shot — Select Region",
            Left = bounds.X,
            Top = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Cursor = Cursors.Cross,
        };

        var canvas = new Canvas { Background = Brushes.Transparent };
        window.Content = canvas;

        var rect = new WpfRectangle
        {
            Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 122, 255)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 122, 255)),
            Width = 0,
            Height = 0,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(rect, 0);
        Canvas.SetTop(rect, 0);
        canvas.Children.Add(rect);

        var hint = new TextBlock
        {
            Text = "Drag to select • ESC to cancel",
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            FontSize = 13,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(hint, 24);
        Canvas.SetTop(hint, 24);
        canvas.Children.Add(hint);

        WpfPoint start = new(0, 0);
        bool dragging = false;

        canvas.MouseLeftButtonDown += (s, e) =>
        {
            start = e.GetPosition(canvas);
            dragging = true;
            Canvas.SetLeft(rect, start.X);
            Canvas.SetTop(rect, start.Y);
            rect.Width = 0;
            rect.Height = 0;
            canvas.CaptureMouse();
        };
        canvas.MouseMove += (s, e) =>
        {
            if (!dragging) return;
            var p = e.GetPosition(canvas);
            double x = Math.Min(start.X, p.X);
            double y = Math.Min(start.Y, p.Y);
            double w = Math.Abs(p.X - start.X);
            double h = Math.Abs(p.Y - start.Y);
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            rect.Width = w;
            rect.Height = h;
        };
        canvas.MouseLeftButtonUp += (s, e) =>
        {
            if (!dragging) return;
            dragging = false;
            canvas.ReleaseMouseCapture();

            double x = Canvas.GetLeft(rect);
            double y = Canvas.GetTop(rect);
            double w = rect.Width;
            double h = rect.Height;

            if (w < 4 || h < 4)
            {
                tcs.TrySetResult(null);
            }
            else
            {
                // Convert from window-local DIPs to screen pixels using the window DPI.
                var dpi = VisualTreeHelper.GetDpi(window);
                int sx = bounds.X + (int)Math.Round(x * dpi.DpiScaleX);
                int sy = bounds.Y + (int)Math.Round(y * dpi.DpiScaleY);
                int sw = (int)Math.Round(w * dpi.DpiScaleX);
                int sh = (int)Math.Round(h * dpi.DpiScaleY);
                tcs.TrySetResult(new GdiRectangle(sx, sy, sw, sh));
            }
            window.Close();
        };

        window.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                tcs.TrySetResult(null);
                window.Close();
            }
        };

        window.Closed += (s, e) =>
        {
            if (!tcs.Task.IsCompleted) tcs.TrySetResult(null);
        };

        window.Show();
        window.Activate();
        window.Focus();

        return tcs.Task;
    }
}
