using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QPARKShot.Helpers;

namespace QPARKShot.Services;

/// <summary>
/// System tray icon with the same menu as the macOS status bar:
/// Capture / Full Screen / Capture with Delay (3/5/10) / Gallery / Preferences / About / Quit.
/// Uses WinForms NotifyIcon — the most reliable cross-Windows-version path.
/// </summary>
public sealed class TrayIconService
{
    public static TrayIconService Shared { get; } = new();

    private NotifyIcon? _icon;

    public Action? OnRequestGallery;
    public Action? OnRequestSettings;
    public Action? OnRequestAbout;
    public Action? OnRequestQuit;

    private TrayIconService() { }

    public void Start()
    {
        if (_icon != null) return;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        Icon? icon = null;
        try
        {
            if (File.Exists(iconPath)) icon = new Icon(iconPath);
        }
        catch (Exception ex) { Logger.LogException("TrayIcon icon load", ex); }
        icon ??= SystemIcons.Application;

        _icon = new NotifyIcon
        {
            Icon = icon,
            Text = "QPARK Shot",
            Visible = true,
        };

        var menu = new ContextMenuStrip();

        menu.Items.Add("Capture Selected Area\tCtrl+Shift+C", null, async (_, _) =>
            await CaptureService.Shared.TriggerCapture(modeOverride: "selection"));

        menu.Items.Add("Capture Full Screen", null, async (_, _) =>
            await CaptureService.Shared.TriggerCapture(modeOverride: "fullScreen"));

        var delayItem = new ToolStripMenuItem("Capture with Delay");
        foreach (var sec in new[] { 3, 5, 10 })
        {
            int capturedSec = sec;
            delayItem.DropDownItems.Add($"{sec} seconds", null, async (_, _) =>
                await CaptureService.Shared.TriggerCapture(delayOverride: capturedSec));
        }
        menu.Items.Add(delayItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Gallery", null, (_, _) => OnRequestGallery?.Invoke());
        menu.Items.Add("Preferences", null, (_, _) => OnRequestSettings?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About QPARK Shot", null, (_, _) => OnRequestAbout?.Invoke());
        menu.Items.Add("Quit", null, (_, _) => OnRequestQuit?.Invoke());

        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) OnRequestGallery?.Invoke();
        };

        Logger.Log("Tray icon created");
    }

    public void Stop()
    {
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
