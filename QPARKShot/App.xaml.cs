using System;
using System.Windows;
using QPARKShot.Helpers;
using QPARKShot.Services;

namespace QPARKShot;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        Logger.Init();
        Logger.Log("App ctor: begin");

        this.DispatcherUnhandledException += (s, e) =>
        {
            Logger.LogException("App.DispatcherUnhandledException", e.Exception);
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Log("OnStartup: begin");

        try
        {
            MainWindowInstance = new MainWindow();
            MainWindow = MainWindowInstance;
            MainWindowInstance.Show();
            Logger.Log("OnStartup: MainWindow shown");
        }
        catch (Exception ex)
        {
            Logger.LogException("OnStartup MainWindow", ex);
            Logger.ShowFatal("QPARK Shot — startup failed", ex.ToString());
            Shutdown(1);
            return;
        }

        // Capture pipeline wiring
        CaptureService.Shared.HideMainWindow = async () =>
        {
            MainWindowInstance?.Dispatcher.Invoke(() => MainWindowInstance.Hide());
            await System.Threading.Tasks.Task.CompletedTask;
        };
        CaptureService.Shared.OnCaptured = item =>
        {
            MainWindowInstance?.Dispatcher.Invoke(() => MainWindowInstance.ShowEditor(item.Id));
        };
        Logger.Log("OnStartup: capture wired");

        // Hotkeys
        try { HotkeyService.Shared.Start(MainWindowInstance!); Logger.Log("OnStartup: hotkeys started"); }
        catch (Exception ex) { Logger.LogException("HotkeyService.Start", ex); }

        // Tray icon
        try
        {
            TrayIconService.Shared.OnRequestGallery = () => MainWindowInstance?.Dispatcher.Invoke(() => MainWindowInstance.ShowGallery());
            TrayIconService.Shared.OnRequestSettings = () => MainWindowInstance?.Dispatcher.Invoke(() => MainWindowInstance.ShowSettings());
            TrayIconService.Shared.OnRequestAbout = () => MainWindowInstance?.Dispatcher.Invoke(() => MainWindowInstance.ShowAbout());
            TrayIconService.Shared.OnRequestQuit = () => Dispatcher.Invoke(() => Shutdown());
            TrayIconService.Shared.Start();
            Logger.Log("OnStartup: tray started");
        }
        catch (Exception ex)
        {
            Logger.LogException("TrayIconService.Start", ex);
        }

        Logger.Log("OnStartup: end (success)");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { HotkeyService.Shared.Stop(); } catch { }
        try { TrayIconService.Shared.Stop(); } catch { }
        base.OnExit(e);
    }
}
