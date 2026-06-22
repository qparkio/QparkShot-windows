using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QPARKShot.Helpers;
using QPARKShot.Services;

namespace QPARKShot.Views;

public partial class ScreenshotCard : UserControl
{
    private string? _path;
    private Point _dragStart;
    private bool _isDragging;

    public ScreenshotCard()
    {
        InitializeComponent();
        DataContextChanged += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _path = DataContext as string;
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;

        NameText.Text = Path.GetFileName(_path);

        await Task.Run(() =>
        {
            using var thumb = BitmapHelpers.Thumbnail(_path, 240);
            if (thumb == null) return;
            var src = BitmapHelpers.ToBitmapSource(thumb);
            Dispatcher.Invoke(() => Thumb.Source = src);
        });
    }

    private void OnHoverEnter(object sender, MouseEventArgs e) => HoverOverlay.Visibility = Visibility.Visible;
    private void OnHoverExit(object sender, MouseEventArgs e) => HoverOverlay.Visibility = Visibility.Collapsed;

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging || string.IsNullOrEmpty(_path)) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _dragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(p.Y - _dragStart.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = true;
            try
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { _path });
                DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
            }
            catch (Exception ex) { Logger.LogException("DragDrop", ex); }
        }
    }

    private void OnTapped(object sender, RoutedEventArgs e)
    {
        if (_isDragging) { _isDragging = false; return; }
        if (string.IsNullOrEmpty(_path)) return;
        var item = ShotQueueStore.Shared.Enqueue(_path);
        App.MainWindowInstance?.ShowEditor(item.Id);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_path)) return;
        try { File.Delete(_path); } catch { }
        var gallery = FindAncestor<GalleryPage>(this);
        if (gallery != null) await gallery.LoadRecentAsync();
    }

    private async void OnCopyClipboard(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_path)) return;
        var bmp = await Task.Run(() => BitmapHelpers.LoadBitmap(_path));
        if (bmp != null) { await ClipboardService.SetBitmapAsync(bmp); bmp.Dispose(); }
    }

    private void OnShowInExplorer(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        Process.Start("explorer.exe", $"/select,\"{_path}\"");
    }

    private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
    {
        var p = System.Windows.Media.VisualTreeHelper.GetParent(d);
        while (p != null && p is not T) p = System.Windows.Media.VisualTreeHelper.GetParent(p);
        return p as T;
    }
}
