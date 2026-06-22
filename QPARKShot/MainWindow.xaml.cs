using System;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using QPARKShot.Views;
using WinRT.Interop;

namespace QPARKShot;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        Title = "QPARK Shot";
        TryApplyMica();
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        }
        catch { }

        ShowGallery();
    }

    public IntPtr Hwnd => WindowNative.GetWindowHandle(this);

    private void TryApplyMica()
    {
        // WinUI 3 modern backdrop. Falls back silently on older Windows.
        try
        {
            this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch
        {
            try { this.SystemBackdrop = new DesktopAcrylicBackdrop(); } catch { }
        }
    }

    public void ShowGallery()
    {
        ContentFrame.Navigate(typeof(GalleryPage));
        EnsureVisible();
    }

    public void ShowSettings()
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        EnsureVisible();
    }

    public void ShowEditor(Guid itemId)
    {
        ContentFrame.Navigate(typeof(EditorPage), itemId);
        EnsureVisible();
    }

    public void ShowAbout()
    {
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
        // Bring to foreground.
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter?.Restore();
    }
}
