using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QPARKShot.Services;

namespace QPARKShot;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }
    public static DispatcherQueue? MainDispatcherQueue { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled: {e.Message}");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ApplyTheme();
        SettingsStore.Shared.SettingsChanged += (_, _) => ApplyTheme();

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();

        HotkeyService.Shared.Start();
        TrayIconService.Shared.OnRequestGallery = () => MainWindowInstance?.ShowGallery();
        TrayIconService.Shared.OnRequestSettings = () => MainWindowInstance?.ShowSettings();
        TrayIconService.Shared.OnRequestAbout = () => MainWindowInstance?.ShowAbout();
        TrayIconService.Shared.OnRequestQuit = () => Exit();
        TrayIconService.Shared.Start(MainWindowInstance);

        CaptureService.Shared.HideMainWindow = async () =>
        {
            MainWindowInstance?.HideForCapture();
            await System.Threading.Tasks.Task.CompletedTask;
        };
        CaptureService.Shared.OnCaptured = item =>
        {
            MainWindowInstance?.ShowEditor(item.Id);
        };
    }

    private void ApplyTheme()
    {
        var theme = SettingsStore.Shared.Settings.ThemePreference;
        RequestedTheme = theme switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => RequestedTheme, // system follows OS already
        };
    }
}
