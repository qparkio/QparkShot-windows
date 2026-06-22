using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QPARKShot.Services;

namespace QPARKShot.Views;

public partial class GalleryPage : Page
{
    public ObservableCollection<string> Screenshots { get; } = new();

    public GalleryPage()
    {
        InitializeComponent();
        GalleryItems.ItemsSource = Screenshots;
        Loaded += async (_, _) =>
        {
            await LoadRecentAsync();
            _ = CleanupService.PerformAsync();
        };
    }

    public async Task LoadRecentAsync()
    {
        try
        {
            var customDir = SettingsStore.Shared.Settings.Cleanup.SaveDirectory;
            var pics = !string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir)
                ? customDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "QPARK Shot");
            var temp = Path.Combine(Path.GetTempPath(), "QPARK Shot");

            var paths = await Task.Run(() =>
            {
                var list = new List<(string Path, DateTime When)>();
                foreach (var dir in new[] { pics, temp })
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var p in Directory.EnumerateFiles(dir, "*.png"))
                    {
                        try { list.Add((p, File.GetCreationTime(p))); } catch { }
                    }
                }
                return list.OrderByDescending(x => x.When).Select(x => x.Path).ToList();
            });

            Screenshots.Clear();
            foreach (var p in paths) Screenshots.Add(p);
            EmptyState.Visibility = Screenshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logger.LogException("GalleryPage.LoadRecentAsync", ex);
        }
    }

    private async void OnCapture(object sender, RoutedEventArgs e)
    {
        try
        {
            await CaptureService.Shared.TriggerCapture();
        }
        catch (Exception ex)
        {
            Logger.LogException("GalleryPage.OnCapture", ex);
        }
    }

    private void OnSettings(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowSettings();

    private async void OnClearCache(object sender, RoutedEventArgs e)
    {
        var temp = Path.Combine(Path.GetTempPath(), "QPARK Shot");
        if (Directory.Exists(temp))
        {
            foreach (var p in Directory.EnumerateFiles(temp, "*.png"))
            {
                try { File.Delete(p); } catch { }
            }
        }
        await LoadRecentAsync();
    }
}
