using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using QPARKShot.Helpers;
using QPARKShot.Models;

namespace QPARKShot.Services;

/// <summary>
/// Equivalent of macOS <c>AppDelegate.triggerCaptureFlow</c>: hide window,
/// optionally delay, capture (selection / full-screen), drop PNG in TEMP,
/// enqueue + open editor.
/// </summary>
public sealed class CaptureService
{
    public static CaptureService Shared { get; } = new();

    /// <summary>Called by the host (MainWindow) to hide UI before capture.</summary>
    public Func<Task>? HideMainWindow;

    /// <summary>Called after capture to restore UI and route to editor.</summary>
    public Action<ShotQueueItem>? OnCaptured;

    private CaptureService() { }

    public async Task TriggerCapture(string? modeOverride = null, int? delayOverride = null)
    {
        var settings = SettingsStore.Shared.Settings;
        var mode = modeOverride ?? settings.Capture.Mode;
        int delaySeconds = Math.Max(0, delayOverride ?? settings.Capture.DelaySeconds);

        if (HideMainWindow != null)
        {
            await HideMainWindow();
        }

        // Small grace time so the window animation finishes off-screen.
        await Task.Delay(250);
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        Rectangle? region = null;
        if (mode == "selection")
        {
            region = await SelectionOverlayController.SelectRegionAsync();
            if (region == null)
            {
                return; // user cancelled
            }
        }
        else
        {
            region = ScreenInfo.PrimaryBounds();
        }

        var bitmap = CaptureRegion(region.Value);
        if (bitmap == null) return;

        var path = Path.Combine(Path.GetTempPath(), $"qpark-shot-{Guid.NewGuid()}.png");
        try
        {
            BitmapHelpers.SavePng(bitmap, path);
        }
        finally
        {
            bitmap.Dispose();
        }

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
        catch
        {
            return null;
        }
    }
}

internal static class ScreenInfo
{
    /// <summary>Returns the primary monitor bounds in physical pixels.</summary>
    public static Rectangle PrimaryBounds()
    {
        // System.Windows.Forms isn't referenced; use SystemMetrics via P/Invoke.
        int w = GetSystemMetrics(SM_CXSCREEN);
        int h = GetSystemMetrics(SM_CYSCREEN);
        return new Rectangle(0, 0, w, h);
    }

    /// <summary>Virtual screen rect across all monitors.</summary>
    public static Rectangle VirtualScreenBounds()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new Rectangle(x, y, w, h);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
}
