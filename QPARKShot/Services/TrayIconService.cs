using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QPARKShot.Helpers;
using QPARKShot.Localization;

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

        RebuildMenu();
        SettingsStore.Shared.SettingsChanged += OnSettingsChanged;
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OnRequestGallery?.Invoke(); };
    }
    private void OnSettingsChanged(object? sender, EventArgs e) => RebuildMenu();
    private void RebuildMenu()
    {
        if (_icon == null) return;
        var old = _icon.ContextMenuStrip;
        var menu = new ContextMenuStrip();

        menu.Items.Add(L.T("capture.selected_area"), null, async (_, _) =>
            await CaptureService.Shared.TriggerCapture(modeOverride: "selection"));

        menu.Items.Add(L.T("capture.full_screen"), null, async (_, _) =>
            await CaptureService.Shared.TriggerCapture(modeOverride: "fullScreen"));

        menu.Items.Add(L.T("capture.window"), null, async (_, _) => await CaptureService.Shared.TriggerCapture("window"));
        var repeat = menu.Items.Add(L.T("capture.repeat_area"), null, async (_, _) => await CaptureService.Shared.TriggerCapture("repeatArea"));
        repeat.Enabled = SettingsStore.Shared.Settings.Capture.LastRegion != null;
        var delayItem = new ToolStripMenuItem(L.T("capture.with_delay"));
        foreach (var sec in new[] { 3, 5, 10 })
        {
            int capturedSec = sec;
            delayItem.DropDownItems.Add(L.Format("capture.seconds_format", sec), null, async (_, _) =>
                await CaptureService.Shared.TriggerCapture(delayOverride: capturedSec));
        }
        menu.Items.Add(delayItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("workspace.library"), null, (_, _) => OnRequestGallery?.Invoke());
        menu.Items.Add(L.T("common.settings"), null, (_, _) => OnRequestSettings?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("menu.about"), null, (_, _) => OnRequestAbout?.Invoke());
        menu.Items.Add(L.T("common.quit"), null, (_, _) => OnRequestQuit?.Invoke());

        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    public void Stop()
    {
        SettingsStore.Shared.SettingsChanged -= OnSettingsChanged;
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
