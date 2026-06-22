using System;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace QPARKShot.Services;

/// <summary>
/// Tray icon (NotifyIcon) with the same menu items as the macOS status-bar:
/// Capture / Full Screen / Capture with Delay 3-5-10 / Gallery / Preferences / About / Quit.
/// </summary>
public sealed class TrayIconService
{
    public static TrayIconService Shared { get; } = new();

    private TaskbarIcon? _icon;

    public Action? OnRequestGallery;
    public Action? OnRequestSettings;
    public Action? OnRequestAbout;
    public Action? OnRequestQuit;

    private TrayIconService() { }

    public void Start(Window owner)
    {
        if (_icon != null) return;

        _icon = new TaskbarIcon
        {
            ToolTipText = "QPARK Shot",
            IconSource = new Microsoft.UI.Xaml.Controls.BitmapIconSource
            {
                UriSource = new Uri("ms-appx:///Assets/AppIcon.ico"),
                ShowAsMonochrome = false,
            },
        };

        var menu = new MenuFlyout();

        var capture = new MenuFlyoutItem { Text = "Capture Selected Area" };
        capture.Click += async (_, _) => await CaptureService.Shared.TriggerCapture(modeOverride: "selection");
        menu.Items.Add(capture);

        var fullScreen = new MenuFlyoutItem { Text = "Capture Full Screen" };
        fullScreen.Click += async (_, _) => await CaptureService.Shared.TriggerCapture(modeOverride: "fullScreen");
        menu.Items.Add(fullScreen);

        var delaySub = new MenuFlyoutSubItem { Text = "Capture with Delay" };
        foreach (var sec in new[] { 3, 5, 10 })
        {
            var item = new MenuFlyoutItem { Text = $"{sec} seconds" };
            int captured = sec;
            item.Click += async (_, _) => await CaptureService.Shared.TriggerCapture(delayOverride: captured);
            delaySub.Items.Add(item);
        }
        menu.Items.Add(delaySub);

        menu.Items.Add(new MenuFlyoutSeparator());

        var gallery = new MenuFlyoutItem { Text = "Open Gallery" };
        gallery.Click += (_, _) => OnRequestGallery?.Invoke();
        menu.Items.Add(gallery);

        var prefs = new MenuFlyoutItem { Text = "Preferences" };
        prefs.Click += (_, _) => OnRequestSettings?.Invoke();
        menu.Items.Add(prefs);

        menu.Items.Add(new MenuFlyoutSeparator());

        var about = new MenuFlyoutItem { Text = "About QPARK Shot" };
        about.Click += (_, _) => OnRequestAbout?.Invoke();
        menu.Items.Add(about);

        var quit = new MenuFlyoutItem { Text = "Quit" };
        quit.Click += (_, _) => OnRequestQuit?.Invoke();
        menu.Items.Add(quit);

        _icon.ContextFlyout = menu;
        _icon.LeftClickCommand = new RelayCommandLite(() => OnRequestGallery?.Invoke());
        _icon.ForceCreate();
    }

    public void Stop()
    {
        _icon?.Dispose();
        _icon = null;
    }
}

internal sealed class RelayCommandLite : System.Windows.Input.ICommand
{
    private readonly Action _action;
    public RelayCommandLite(Action action) { _action = action; }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
    public event EventHandler? CanExecuteChanged;
}
