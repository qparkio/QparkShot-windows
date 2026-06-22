using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QPARKShot.Helpers;
using QPARKShot.Services;

namespace QPARKShot;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }
    public static DispatcherQueue? MainDispatcherQueue { get; private set; }

    public App()
    {
        Logger.Init();
        Logger.Log("App ctor: begin");
        try
        {
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                Logger.LogException("App.UnhandledException", e.Exception);
                Logger.ShowFatal("QPARK Shot — unhandled error", e.Exception.ToString());
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Logger.LogException("AppDomain.UnhandledException", ex);
                    Logger.ShowFatal("QPARK Shot — fatal", ex.ToString());
                }
            };
            Logger.Log("App ctor: end");
        }
        catch (Exception ex)
        {
            Logger.LogException("App ctor", ex);
            Logger.ShowFatal("QPARK Shot — startup failed (ctor)", ex.ToString());
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Logger.Log("OnLaunched: begin");
        try
        {
            MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
            Logger.Log("OnLaunched: got dispatcher queue");

            try { ApplyTheme(); Logger.Log("OnLaunched: theme applied"); }
            catch (Exception ex) { Logger.LogException("ApplyTheme", ex); }

            SettingsStore.Shared.SettingsChanged += (_, _) =>
            {
                try { ApplyTheme(); } catch (Exception ex) { Logger.LogException("ApplyTheme(reactive)", ex); }
            };
            Logger.Log("OnLaunched: settings hook installed");

            MainWindowInstance = new MainWindow();
            Logger.Log("OnLaunched: MainWindow created");
            MainWindowInstance.Activate();
            Logger.Log("OnLaunched: MainWindow activated");

            // Wire capture pipeline
            CaptureService.Shared.HideMainWindow = async () =>
            {
                MainWindowInstance?.HideForCapture();
                await System.Threading.Tasks.Task.CompletedTask;
            };
            CaptureService.Shared.OnCaptured = item =>
            {
                MainWindowInstance?.ShowEditor(item.Id);
            };
            Logger.Log("OnLaunched: capture wired");

            // Hotkeys + Tray — non-fatal if they fail.
            try { HotkeyService.Shared.Start(); Logger.Log("OnLaunched: hotkeys started"); }
            catch (Exception ex) { Logger.LogException("HotkeyService.Start", ex); }

            try
            {
                TrayIconService.Shared.OnRequestGallery = () => MainWindowInstance?.ShowGallery();
                TrayIconService.Shared.OnRequestSettings = () => MainWindowInstance?.ShowSettings();
                TrayIconService.Shared.OnRequestAbout = () => MainWindowInstance?.ShowAbout();
                TrayIconService.Shared.OnRequestQuit = () => Exit();
                TrayIconService.Shared.Start(MainWindowInstance);
                Logger.Log("OnLaunched: tray started");
            }
            catch (Exception ex)
            {
                Logger.LogException("TrayIconService.Start", ex);
                // Continue without tray.
            }

            Logger.Log("OnLaunched: end (success)");
        }
        catch (Exception ex)
        {
            Logger.LogException("OnLaunched", ex);
            Logger.ShowFatal("QPARK Shot — startup failed", ex.ToString());
        }
    }

    private void ApplyTheme()
    {
        var theme = SettingsStore.Shared.Settings.ThemePreference;
        RequestedTheme = theme switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => RequestedTheme,
        };
    }
}
