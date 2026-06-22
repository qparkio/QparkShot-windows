using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;
using Bitmap = System.Drawing.Bitmap;

namespace QPARKShot.Views;

public partial class EditorPage : Page
{
    private Guid _itemId;
    private Bitmap? _sourceBitmap;

    public EditorPage(Guid itemId)
    {
        InitializeComponent();
        _itemId = itemId;
        ShotQueueStore.Shared.ActiveId = itemId;
        QueueSidebar.OnRequestOpen = ShowItem;
        
        // Sync initial combobox selections to Canvas safely after InitializeComponent
        OnToolChanged(null!, null);
        OnSizeChanged(null!, null);

        Loaded += async (_, _) => await LoadActiveAsync();
    }

    private async Task LoadActiveAsync()
    {
        var item = ShotQueueStore.Shared.Item(_itemId);
        if (item == null) return;
        _sourceBitmap?.Dispose();
        _sourceBitmap = await Task.Run(() => BitmapHelpers.LoadBitmap(item.Path));
        if (_sourceBitmap == null) return;
        Canvas.LoadImage(_sourceBitmap);
    }

    private async void ShowItem(Guid id)
    {
        _itemId = id;
        ShotQueueStore.Shared.ActiveId = id;
        await LoadActiveAsync();
    }

    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowGallery();
    private void OnUndo(object sender, RoutedEventArgs e) => Canvas.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Canvas.Redo();

    private void OnToolChanged(object sender, SelectionChangedEventArgs? e)
    {
        if (Canvas == null || TextInput == null || ToolPicker == null) return;
        if (ToolPicker.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
            Enum.TryParse<ToolType>(tag, out var t))
        {
            Canvas.CurrentTool = t;
            TextInput.Visibility = (t == ToolType.Text) ? Visibility.Visible : Visibility.Collapsed;
            Canvas.TextInput = TextInput.Text;
        }
    }

    private void OnColorPick(object sender, MouseButtonEventArgs e)
    {
        var dlg = new System.Windows.Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
        };
        if (ColorRectangle.Fill is SolidColorBrush b)
        {
            dlg.Color = System.Drawing.Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B);
        }
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
            ColorRectangle.Fill = new SolidColorBrush(c);
            Canvas.CurrentColorHex = ColorHelpers.ToHex(c);
        }
    }

    private void OnSizeChanged(object sender, SelectionChangedEventArgs? e)
    {
        if (Canvas == null || SizePicker == null) return;
        if (SizePicker.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Tag?.ToString(), out var w))
        {
            Canvas.CurrentStrokeWidth = w;
        }
    }

    private Bitmap? RenderForExport()
    {
        if (_sourceBitmap == null) return null;
        var ws = WatermarkSettings.FromStore(SettingsStore.Shared);
        return WatermarkRenderer.Render(_sourceBitmap, Canvas.Annotations, Canvas.CropRect, ws);
    }

    private async void OnPreview(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;
        PreviewOverlay.Visibility = Visibility.Visible;
        PreviewSpinner.Visibility = Visibility.Visible;
        PreviewImage.Source = null;
        var rendered = await Task.Run(() => RenderForExport());
        PreviewSpinner.Visibility = Visibility.Collapsed;
        if (rendered != null)
        {
            PreviewImage.Source = BitmapHelpers.ToBitmapSource(rendered);
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
