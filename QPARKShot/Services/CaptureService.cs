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

    public Action? RestoreMainWindow;
    public bool IsCapturing { get; private set; }
    public async Task TriggerCapture(string? modeOverride = null, int? delayOverride = null)
    {
        if (IsCapturing) return;
        IsCapturing = true;
        var captured = false;
        try
        {
            var settings = SettingsStore.Shared.Settings;
            var mode = modeOverride ?? settings.Capture.Mode;
            int delaySeconds = Math.Clamp(delayOverride ?? settings.Capture.DelaySeconds, 0, 10);
            WorkspaceStore.Shared.Notify(Localization.L.T("status.capturing"));
            if (HideMainWindow != null) await HideMainWindow();
            await Task.Delay(250 + delaySeconds * 1000);
            Rectangle? region = mode switch
            {
                "selection" => await SelectionOverlayController.SelectRegionAsync(),
                "window" => await SelectionOverlayController.SelectRegionAsync(windowCapture: true),
                "repeatArea" => settings.Capture.LastRegion,
                _ => ScreenInfo.PrimaryBounds(),
            };
            if (region == null) { WorkspaceStore.Shared.Notify(Localization.L.T("status.capture_cancelled")); return; }
            if (!ScreenInfo.VirtualScreenBounds().Contains(region.Value))
                throw new InvalidOperationException(Localization.L.T("status.capture_failed"));
            await Task.Delay(120);
            using var bitmap = CaptureRegion(region.Value) ?? throw new IOException(Localization.L.T("status.capture_failed"));
            Directory.CreateDirectory(AppPaths.TemporaryDirectory);
            var path = Path.Combine(AppPaths.TemporaryDirectory, $"qpark-shot-{Guid.NewGuid()}.png");
            if (!BitmapHelpers.SavePng(bitmap, path)) throw new IOException(Localization.L.T("status.capture_failed"));
            if (mode == "selection") { settings.Capture.LastRegion = region; SettingsStore.Shared.Save(); }
            var item = ShotQueueStore.Shared.Enqueue(path);
            OnCaptured?.Invoke(item);
            captured = true;
            WorkspaceStore.Shared.Notify(Localization.L.T("review.captured"));
        }
        catch (Exception ex) { Logger.LogException("Capture", ex); WorkspaceStore.Shared.Notify(ex.Message); }
        finally { IsCapturing = false; if (!captured) RestoreMainWindow?.Invoke(); }
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
