using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;

namespace QPARKShot.Views;

public partial class WatermarkPreview : UserControl
{
    private CancellationTokenSource? _renderCts;

    public WatermarkPreview()
    {
        InitializeComponent();
        Loaded += (_, _) => { SettingsStore.Shared.SettingsChanged += OnSettingsChanged; Refresh(); };
        Unloaded += (_, _) => SettingsStore.Shared.SettingsChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? s, EventArgs e) => Refresh();

    public void Refresh()
    {
        _renderCts?.Cancel();
        var cts = _renderCts = new CancellationTokenSource();
        var token = cts.Token;
        var ws = WatermarkSettings.FromStore(SettingsStore.Shared);

        Task.Run(() =>
        {
            // Render watermark on a mock blue gradient ~680×440 px.
            const int W = 680, H = 440;
            using var mock = new Bitmap(W, H);
            using (var g = Graphics.FromImage(mock))
            {
                using var grad = new LinearGradientBrush(
                    new System.Drawing.Point(0, 0), new System.Drawing.Point(W, H),
                    System.Drawing.Color.FromArgb(0x3B, 0x6F, 0xE3),
                    System.Drawing.Color.FromArgb(0x4F, 0x46, 0x90));
                g.FillRectangle(grad, 0, 0, W, H);
            }
            if (token.IsCancellationRequested) return;
            var rendered = WatermarkRenderer.Render(mock, Array.Empty<Annotation>(), null, ws);
            if (token.IsCancellationRequested || rendered == null) { rendered?.Dispose(); return; }

            var src = BitmapHelpers.ToBitmapSource(rendered);
            rendered.Dispose();
            Dispatcher.Invoke(() => { if (!token.IsCancellationRequested) PreviewImage.Source = src; });
        }, token);
    }
}
