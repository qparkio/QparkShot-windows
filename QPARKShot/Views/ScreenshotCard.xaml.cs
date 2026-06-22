using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using QPARKShot.Helpers;
using QPARKShot.Services;
using Windows.Storage;

namespace QPARKShot.Views;

public sealed partial class ScreenshotCard : UserControl
{
    private string? _path;

    public ScreenshotCard()
    {
        this.InitializeComponent();
        PointerEntered += (_, _) => HoverOverlay.Visibility = Visibility.Visible;
        PointerExited += (_, _) => HoverOverlay.Visibility = Visibility.Collapsed;
        DataContextChanged += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _path = DataContext as string;
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;

        NameText.Text = Path.GetFileName(_path);

        // Load a downscaled bitmap for the UI.
        await Task.Run(() =>
        {
            using var thumb = BitmapHelpers.Thumbnail(_path, 240);
            if (thumb == null) return;
            var bytes = BitmapHelpers.ToPngBytes(thumb);
            DispatcherQueue.TryEnqueue(async () =>
            {
                var bmp = new BitmapImage();
                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                using (var writer = new Windows.Storage.Streams.DataWriter(stream))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
                stream.Seek(0);
                await bmp.SetSourceAsync(stream);
                Thumb.Source = bmp;
            });
        });
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_path)) return;
        var item = ShotQueueStore.Shared.Enqueue(_path);
        App.MainWindowInstance?.ShowEditor(item.Id);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_path)) return;
        try { File.Delete(_path); } catch { }
        // Tell the parent gallery to refresh.
        if (this.FindAscendant<GalleryPage>() is { } g)
        {
            var method = typeof(GalleryPage).GetMethod("LoadRecentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method?.Invoke(g, null) is Task t) await t;
        }
    }

    private async void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        var deferral = args.GetDeferral();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(_path);
            args.Data.SetStorageItems(new IStorageItem[] { file });
            args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
        catch { }
        finally
        {
            deferral.Complete();
        }
    }
}

internal static class VisualTreeHelperExt
{
    public static T? FindAscendant<T>(this DependencyObject from) where T : DependencyObject
    {
        var p = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(from);
        while (p != null && p is not T) p = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(p);
        return p as T;
    }
}
