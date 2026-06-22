using System;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using WinRT.Interop;

namespace QPARKShot.Services;

/// <summary>
/// Opens a full-screen transparent overlay window, lets the user drag a rectangle,
/// and returns the selected region in screen pixel space. Returns null on cancel (ESC).
/// </summary>
public static class SelectionOverlayController
{
    public static Task<Rectangle?> SelectRegionAsync()
    {
        var tcs = new TaskCompletionSource<Rectangle?>();
        var window = new Window
        {
            Title = "QPARK Shot — Select Region",
        };

        // Borderless, topmost, full virtual screen.
        var bounds = ScreenInfo.VirtualScreenBounds();
        var appWindow = window.AppWindow;
        var overlapped = appWindow.Presenter as OverlappedPresenter;
        overlapped?.SetBorderAndTitleBar(false, false);
        overlapped?.Maximize();
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        appWindow.IsShownInSwitchers = false;

        // Translucent dark canvas + selection rectangle.
        var canvas = new Canvas
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(110, 0, 0, 0)),
        };
        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 122, 255)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 122, 255)),
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
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 255, 255, 255)),
            FontSize = 13,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(hint, 24);
        Canvas.SetTop(hint, 24);
        canvas.Children.Add(hint);

        Point start = new(0, 0);
        bool dragging = false;

        canvas.PointerPressed += (s, e) =>
        {
            var p = e.GetCurrentPoint(canvas).Position;
            start = p;
            dragging = true;
            Canvas.SetLeft(rect, p.X);
            Canvas.SetTop(rect, p.Y);
            rect.Width = 0;
            rect.Height = 0;
            canvas.CapturePointer(e.Pointer);
        };
        canvas.PointerMoved += (s, e) =>
        {
            if (!dragging) return;
            var p = e.GetCurrentPoint(canvas).Position;
            double x = Math.Min(start.X, p.X);
            double y = Math.Min(start.Y, p.Y);
            double w = Math.Abs(p.X - start.X);
            double h = Math.Abs(p.Y - start.Y);
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            rect.Width = w;
            rect.Height = h;
        };
        canvas.PointerReleased += (s, e) =>
        {
            if (!dragging) return;
            dragging = false;
            canvas.ReleasePointerCapture(e.Pointer);
            var x = Canvas.GetLeft(rect);
            var y = Canvas.GetTop(rect);
            var w = rect.Width;
            var h = rect.Height;

            if (w < 4 || h < 4)
            {
                tcs.TrySetResult(null);
            }
            else
            {
                // Convert from window-local DIPs to screen pixels. Window is full virtual screen,
                // so window-local already equals screen pixels (assuming PerMonitorV2 DPI).
                var screenRect = new Rectangle(
                    bounds.X + (int)Math.Round(x),
                    bounds.Y + (int)Math.Round(y),
                    (int)Math.Round(w),
                    (int)Math.Round(h));
                tcs.TrySetResult(screenRect);
            }
            window.Close();
        };

        // ESC cancels.
        canvas.KeyDown += (s, e) =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                tcs.TrySetResult(null);
                window.Close();
                e.Handled = true;
            }
        };

        canvas.IsTabStop = true;
        window.Content = canvas;
        window.Activated += (s, e) =>
        {
            canvas.Focus(FocusState.Programmatic);
        };
        window.Closed += (s, e) =>
        {
            if (!tcs.Task.IsCompleted) tcs.TrySetResult(null);
        };

        window.Activate();
        return tcs.Task;
    }
}
