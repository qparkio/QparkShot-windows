using System;
using System.Windows;
using System.Windows.Media;
using QPARKShot.Helpers;
using QPARKShot.Services;

namespace QPARKShot;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }
    private static System.Threading.Mutex? _appMutex;

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
        // Single-instance protection check
        _appMutex = new System.Threading.Mutex(true, "QPARKShot_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            Logger.Log("Another instance of QPARK Shot is already running. Exiting.");
            Shutdown();
            return;
        }

        base.OnStartup(e);
        Logger.Log("OnStartup: begin");

        // Load and apply theme dynamically
        try
        {
            ApplyTheme(SettingsStore.Shared.Settings.ThemePreference);
            SettingsStore.Shared.SettingsChanged += (s, ev) =>
            {
                Dispatcher.Invoke(() => ApplyTheme(SettingsStore.Shared.Settings.ThemePreference));
            };
        }
        catch (Exception ex)
        {
            Logger.LogException("OnStartup ApplyTheme", ex);
        }

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
        try
        {
            if (_appMutex != null)
            {
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
            }
        }
        catch { }
        base.OnExit(e);
    }

    public static void ApplyTheme(string themePreference)
    {
        string actualTheme = themePreference;
        if (themePreference == "system")
        {
            actualTheme = IsWindowsDarkMode() ? "dark" : "light";
        }

        Logger.Log($"ApplyTheme: setting to {actualTheme}");

        var resources = Application.Current.Resources;
        if (actualTheme == "light")
        {
            resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
            resources["WindowForeground"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            resources["SecondaryText"] = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
            resources["CardFill"] = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0));
            resources["CardStroke"] = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0));
            resources["SidebarFill"] = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0));
            resources["DividerStroke"] = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0));
            resources["ControlBackground"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["ControlForeground"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            resources["ControlBorder"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD6));
            resources["ControlMouseOverBackground"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            resources["ControlSelectedBackground"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF));
            resources["QPARKAccent"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF));
            resources["IconButtonHover"] = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0));
        }
        else // dark
        {
            resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            resources["WindowForeground"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
            resources["SecondaryText"] = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
            resources["CardFill"] = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
            resources["CardStroke"] = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
            resources["SidebarFill"] = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0));
            resources["DividerStroke"] = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
            resources["ControlBackground"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
            resources["ControlForeground"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["ControlBorder"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
            resources["ControlMouseOverBackground"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
            resources["ControlSelectedBackground"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
            resources["QPARKAccent"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
            resources["IconButtonHover"] = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        }
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch
        {
            return true; // default to dark
        }
    }
}
