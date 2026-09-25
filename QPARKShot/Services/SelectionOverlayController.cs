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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public static Task<GdiRectangle?> SelectRegionAsync(bool windowCapture = false)
    {
        var tcs = new TaskCompletionSource<GdiRectangle?>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            Text = Localization.L.T(windowCapture ? "windows.capture_window_hint" : "windows.capture_hint"),
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

            if (windowCapture)
            {
                GetCursorPos(out var cursor);
                window.Hide();
                var target = GetAncestor(WindowFromPoint(cursor), 2);
                if (target != IntPtr.Zero && GetWindowRect(target, out var area))
                {
                    var selected = GdiRectangle.FromLTRB(area.Left, area.Top, area.Right, area.Bottom);
                    selected.Intersect(bounds);
                    tcs.TrySetResult(selected.Width > 0 && selected.Height > 0 ? selected : null);
                }
                else tcs.TrySetResult(null);
                window.Close();
                return;
            }
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
                var topLeft = canvas.PointToScreen(new WpfPoint(x, y));
                var bottomRight = canvas.PointToScreen(new WpfPoint(x + w, y + h));
                tcs.TrySetResult(GdiRectangle.FromLTRB((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y),
                    (int)Math.Round(bottomRight.X), (int)Math.Round(bottomRight.Y)));
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

        window.Loaded += (_, _) => SetWindowPos(new System.Windows.Interop.WindowInteropHelper(window).Handle,
            new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
        window.Show();
        window.Activate();
        window.Focus();

        return tcs.Task;
    }
}
