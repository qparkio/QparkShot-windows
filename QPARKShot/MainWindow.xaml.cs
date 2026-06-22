using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using QPARKShot.Helpers;
using QPARKShot.Views;
using WinRT.Interop;

namespace QPARKShot;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        Logger.Log("MainWindow ctor: begin");
        try
        {
            this.InitializeComponent();
            Title = "QPARK Shot";

            try { TryApplyMica(); Logger.Log("MainWindow: backdrop applied"); }
            catch (Exception ex) { Logger.LogException("TryApplyMica", ex); }

            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                    Logger.Log($"MainWindow: icon set from {iconPath}");
                }
                else
                {
                    Logger.Log($"MainWindow: icon NOT FOUND at {iconPath}");
                }
            }
            catch (Exception ex) { Logger.LogException("SetIcon", ex); }

            ShowGallery();
            Logger.Log("MainWindow ctor: end");
        }
        catch (Exception ex)
        {
            Logger.LogException("MainWindow ctor", ex);
            throw;
        }
    }

    public IntPtr Hwnd => WindowNative.GetWindowHandle(this);

    private void TryApplyMica()
    {
        // Mica needs Windows 11; fall back to Acrylic, then nothing.
        try { this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt }; return; } catch { }
        try { this.SystemBackdrop = new DesktopAcrylicBackdrop(); return; } catch { }
    }

    public void ShowGallery()
    {
        Logger.Log("ShowGallery");
        ContentFrame.Navigate(typeof(GalleryPage));
        EnsureVisible();
    }

    public void ShowSettings()
    {
        Logger.Log("ShowSettings");
        ContentFrame.Navigate(typeof(SettingsPage));
        EnsureVisible();
    }

    public void ShowEditor(Guid itemId)
    {
        Logger.Log($"ShowEditor {itemId}");
        ContentFrame.Navigate(typeof(EditorPage), itemId);
        EnsureVisible();
    }

    public void ShowAbout()
    {
        Logger.Log("ShowAbout");
        ContentFrame.Navigate(typeof(SettingsPage), "about");
        EnsureVisible();
    }

    public void HideForCapture()
    {
        AppWindow.Hide();
    }

    public void EnsureVisible()
    {
        AppWindow.Show();
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter?.Restore();
    }
}
