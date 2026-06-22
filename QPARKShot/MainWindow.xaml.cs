using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using QPARKShot.Helpers;
using QPARKShot.Views;

namespace QPARKShot;

public partial class MainWindow : Window
{
    public IntPtr Hwnd { get; private set; }

    public MainWindow()
    {
        Logger.Log("MainWindow ctor: begin");
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            TryApplyMica();
            TrySetIcon();
        };

        Loaded += (s, e) => ShowGallery();
        Logger.Log("MainWindow ctor: end");
    }

    private void TrySetIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                Logger.Log("MainWindow: icon set");
            }
        }
        catch (Exception ex) { Logger.LogException("SetIcon", ex); }
    }

    /// <summary>Apply Mica backdrop on Windows 11 22H2+; silently no-op on older OS.</summary>
    private void TryApplyMica()
    {
        try
        {
            // Enable dark mode for the title bar
            int useDark = 1;
            DwmSetWindowAttribute(Hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            // Round corners
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(Hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            // Apply Mica (Win11 22H2+)
            int backdrop = DWMSBT_MAINWINDOW;
            int hr = DwmSetWindowAttribute(Hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            if (hr == 0)
            {
                Background = System.Windows.Media.Brushes.Transparent;
            }
        }
        catch (Exception ex)
        {
            Logger.LogException("Mica", ex);
        }
    }

    public void ShowGallery()
    {
        Logger.Log("ShowGallery");
        ContentFrame.Navigate(new GalleryPage());
        EnsureVisible();
    }

    public void ShowSettings()
    {
        Logger.Log("ShowSettings");
        ContentFrame.Navigate(new SettingsPage());
        EnsureVisible();
    }

    public void ShowAbout()
    {
        Logger.Log("ShowAbout");
        ContentFrame.Navigate(new SettingsPage("about"));
        EnsureVisible();
    }

    public void ShowEditor(Guid itemId)
    {
        Logger.Log($"ShowEditor {itemId}");
        ContentFrame.Navigate(new EditorPage(itemId));
        EnsureVisible();
    }

    public void HideForCapture() => Hide();

    public void EnsureVisible()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; Topmost = false;
    }

    // --- DWM P/Invoke for Mica + rounded corners ---
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
