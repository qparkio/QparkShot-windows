using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QPARKShot.Services;

namespace QPARKShot.Views;

public sealed partial class GalleryPage : Page
{
    public ObservableCollection<string> Screenshots { get; } = new();

    public GalleryPage()
    {
        this.InitializeComponent();
        GalleryRepeater.ItemsSource = Screenshots;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await LoadRecentAsync();
        _ = CleanupService.PerformAsync();
    }

    private async Task LoadRecentAsync()
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

    private async void OnCapture(object sender, RoutedEventArgs e)
    {
        await CaptureService.Shared.TriggerCapture();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.ShowSettings();
    }

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
