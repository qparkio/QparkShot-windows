using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using QPARKShot.Helpers;
using QPARKShot.Models;

namespace QPARKShot.Services;

public sealed class CaptureService
{
    public static CaptureService Shared { get; } = new();

    public Func<Task>? HideMainWindow;
    public Action<ShotQueueItem>? OnCaptured;

    private CaptureService() { }

    public async Task TriggerCapture(string? modeOverride = null, int? delayOverride = null)
    {
        var settings = SettingsStore.Shared.Settings;
        var mode = modeOverride ?? settings.Capture.Mode;
        int delaySeconds = Math.Max(0, delayOverride ?? settings.Capture.DelaySeconds);

        if (HideMainWindow != null) await HideMainWindow();
        await Task.Delay(250);
        if (delaySeconds > 0) await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        Rectangle? region;
        if (mode == "selection")
        {
            region = await SelectionOverlayController.SelectRegionAsync();
            if (region == null) return;
        }
        else
        {
            region = ScreenInfo.PrimaryBounds();
        }

        var bitmap = CaptureRegion(region.Value);
        if (bitmap == null) return;

        var path = Path.Combine(Path.GetTempPath(), $"qpark-shot-{Guid.NewGuid()}.png");
        try { BitmapHelpers.SavePng(bitmap, path); }
        finally { bitmap.Dispose(); }

        var item = ShotQueueStore.Shared.Enqueue(path);
        OnCaptured?.Invoke(item);
    }

    private static Bitmap? CaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0) return null;
        try
        {
            var bmp = new Bitmap(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }
        catch { return null; }
    }
}

internal static class ScreenInfo
{
    public static Rectangle PrimaryBounds()
    {
        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        return new Rectangle(0, 0, w, h);
    }

    public static Rectangle VirtualScreenBounds()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return new Rectangle(x, y, w, h);
    }
}
