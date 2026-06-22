using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace QPARKShot.Views;

public sealed partial class EditorPage : Page
{
    private Guid _itemId;
    private Bitmap? _sourceBitmap;

    public EditorPage()
    {
        this.InitializeComponent();
        ToolPicker.SelectedIndex = 1; // Freehand
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
        {
            _itemId = id;
            ShotQueueStore.Shared.ActiveId = id;
            QueueSidebar.OnRequestOpen = ShowItem;
        }
        await LoadActiveAsync();
    }

    private async Task LoadActiveAsync()
    {
        var item = ShotQueueStore.Shared.Item(_itemId);
        if (item == null) return;

        _sourceBitmap = await Task.Run(() => BitmapHelpers.LoadBitmap(item.Path));
        if (_sourceBitmap == null) return;

        Canvas.LoadImage(_sourceBitmap);
    }

    private void ShowItem(Guid id)
    {
        _itemId = id;
        ShotQueueStore.Shared.ActiveId = id;
        _ = LoadActiveAsync();
    }

    // Toolbar handlers
    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowGallery();
    private void OnUndo(object sender, RoutedEventArgs e) => Canvas.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Canvas.Redo();

    private void OnToolChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = ((ComboBoxItem)ToolPicker.SelectedItem)?.Tag?.ToString();
        if (Enum.TryParse<ToolType>(tag, out var t)) Canvas.CurrentTool = t;
        TextInput.Visibility = (t == ToolType.Text) ? Visibility.Visible : Visibility.Collapsed;
        Canvas.TextInput = TextInput.Text;
    }
    private void OnColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        => Canvas.CurrentColorHex = ColorHelpers.ToHex(ColorHelpers.FromWinUI(args.NewColor));

    private void OnSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SizePicker.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Tag?.ToString(), out var w))
        {
            Canvas.CurrentStrokeWidth = w;
        }
    }

    // Render pipeline
    private Bitmap? RenderForExport()
    {
        if (_sourceBitmap == null) return null;
        var watermark = WatermarkSettings.FromStore(SettingsStore.Shared);
        return WatermarkRenderer.Render(_sourceBitmap, Canvas.Annotations, Canvas.CropRect, watermark);
    }

    private async void OnPreview(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;
        PreviewOverlay.Visibility = Visibility.Visible;
        PreviewSpinner.IsActive = true;
        PreviewImage.Source = null;
        var rendered = await Task.Run(() => RenderForExport());
        PreviewSpinner.IsActive = false;
        if (rendered != null)
        {
            PreviewImage.Source = await BitmapHelpers.ToBitmapImageAsync(rendered);
            rendered.Dispose();
        }
    }
    private void OnPreviewClose(object sender, RoutedEventArgs e) => PreviewOverlay.Visibility = Visibility.Collapsed;

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;
        var rendered = await Task.Run(() => RenderForExport());
        if (rendered == null) return;
        await ClipboardService.SetBitmapAsync(rendered);
        rendered.Dispose();
    }

    private async void OnShare(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;
        var rendered = await Task.Run(() => RenderForExport());
        if (rendered == null) return;
        var savedTemp = ImageExportService.SaveBitmap(rendered, isTemporary: true);
        rendered.Dispose();
        if (savedTemp == null) return;
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(savedTemp);
            // Share via context menu — fallback that always works on unpackaged WinUI 3.
            var launcher = new Windows.System.LauncherOptions { DisplayApplicationPicker = true };
            await Windows.System.Launcher.LaunchFileAsync(file, launcher);
        }
        catch { }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;
        var rendered = await Task.Run(() => RenderForExport());
        if (rendered == null) return;
        var saved = ImageExportService.SaveBitmap(rendered, isTemporary: false);
        rendered.Dispose();
        if (saved != null)
        {
            App.MainWindowInstance?.ShowGallery();
        }
    }
}
