using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QPARKShot.Helpers;
using QPARKShot.Localization;
using QPARKShot.Models;
using QPARKShot.Services;
using Bitmap = System.Drawing.Bitmap;
namespace QPARKShot.Views;

public partial class EditorPage : UserControl, IDisposable
{
    private Guid _itemId;
    private Bitmap? _sourceBitmap;
    private int _loadGeneration;
    private bool _disposed, _exporting, _review;
    public EditorPage(Guid itemId, bool review = false)
    {
        InitializeComponent(); _itemId = itemId; _review = review;
        ShotQueueStore.Shared.ActiveId = itemId;
        QueueSidebar.OnRequestOpen = ShowItem;
        Canvas.Changed += (_, _) => { UndoButton.IsEnabled = Canvas.CanUndo; RedoButton.IsEnabled = Canvas.CanRedo; };
        Loaded += async (_, _) => await LoadActiveAsync();
        PreviewKeyDown += OnEditorKey;
        ApplySettings();
        ToolPanel.Visibility = review ? Visibility.Collapsed : Visibility.Visible;
        EditButton.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
        Canvas.ReadOnly = review;
        OnToolChanged(null!, null!); OnSizeChanged(null!, null!);
    }
    public void ApplySettings()
    {
        SidebarColumn.Width = new GridLength(SettingsStore.Shared.Settings.Queue.PanelEnabled ? 128 : 0);
        foreach (ComboBoxItem item in PresetPicker.Items) item.IsSelected = item.Tag?.ToString() == SettingsStore.Shared.Settings.Export.SelectedPresetID;
    }
    public async Task LoadActiveAsync()
    {
        var generation = ++_loadGeneration; var id = _itemId;
        _sourceBitmap?.Dispose(); _sourceBitmap = null; Canvas.ReleaseImage();
        LoadStatus.Text = L.T("status.loading");
        try
        {
            var item = ShotQueueStore.Shared.Item(id);
            var bitmap = item == null ? null : await Task.Run(() => BitmapHelpers.LoadBitmap(item.Path));
            if (_disposed || generation != _loadGeneration) { bitmap?.Dispose(); return; }
            if (bitmap == null) { LoadStatus.Text = L.T("status.unsupported_image"); return; }
            _sourceBitmap = bitmap;
            Canvas.LoadImage(bitmap, EditorDraftStore.Shared.For(id));
            LoadStatus.Text = "";
            if (_review) await UpdateReviewAsync();
        }
        catch (Exception ex) { if (!_disposed && generation == _loadGeneration) LoadStatus.Text = ex.Message; }
    }
    private async void ShowItem(Guid id)
    {
        _itemId = id; ShotQueueStore.Shared.ActiveId = id;
        PreviewOverlay.Visibility = Visibility.Collapsed;
        await LoadActiveAsync();
    }
    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowGallery();
    private void OnEdit(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowEditor(_itemId);
    private void OnUndo(object sender, RoutedEventArgs e) => Canvas.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Canvas.Redo();
    private void OnClearCrop(object sender, RoutedEventArgs e) => Canvas.ClearCrop();
    private void OnTextChanged(object sender, TextChangedEventArgs e) { if (Canvas != null) Canvas.AnnotationText = TextEntry.Text; }
    private void OnToolChanged(object sender, SelectionChangedEventArgs? e)
    {
        if (Canvas == null || TextEntry == null) return;
        if (ToolPicker.SelectedItem is ComboBoxItem item && Enum.TryParse<ToolType>(item.Tag?.ToString(), out var tool))
        { Canvas.CurrentTool = tool; TextEntry.Visibility = tool == ToolType.Text ? Visibility.Visible : Visibility.Collapsed; }
    }
    private void OnColorPick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
        ColorRectangle.Fill = new SolidColorBrush(color); Canvas.CurrentColorHex = ColorHelpers.ToHex(color);
    }
    private void OnSizeChanged(object sender, SelectionChangedEventArgs? e)
    {
        if (Canvas != null && SizePicker.SelectedItem is ComboBoxItem item && double.TryParse(item.Tag?.ToString(), out var width)) Canvas.CurrentStrokeWidth = width;
    }
    private async void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetPicker.SelectedItem is not ComboBoxItem item) return;
        var preset = item.Tag?.ToString() ?? "watermarked";
        if (SettingsStore.Shared.Settings.Export.SelectedPresetID == preset) return;
        SettingsStore.Shared.Mutate(s => s.Export.SelectedPresetID = preset);
        if (_review) await UpdateReviewAsync();
    }
    private async Task<Bitmap?> RenderAsync()
    {
        if (_sourceBitmap == null) return null;
        using var source = (Bitmap)_sourceBitmap.Clone();
        var snapshot = EditorDraftStore.Shared.For(_itemId).Current;
        var preset = SettingsStore.Shared.Settings.Export.SelectedPresetID;
        var watermark = WatermarkSettings.FromStore(SettingsStore.Shared);
        return await Task.Run(() => ImageExportService.Render(source, snapshot, preset, watermark));
    }
    private async Task UpdateReviewAsync()
    {
        var id = _itemId; var generation = _loadGeneration;
        using var rendered = await RenderAsync();
        if (_disposed || id != _itemId || generation != _loadGeneration || rendered == null) return;
        // Review uses the same final renderer as export, and retains the session sidebar.
        Canvas.LoadImage(rendered);
    }
    private async void OnPreview(object sender, RoutedEventArgs e) => await ExportActionAsync("preview");
    private void OnPreviewClose(object sender, RoutedEventArgs e) => PreviewOverlay.Visibility = Visibility.Collapsed;
    private async void OnCopy(object sender, RoutedEventArgs e) => await ExportActionAsync("copy");
    private async void OnSave(object sender, RoutedEventArgs e) => await ExportActionAsync("save");
    private async void OnPin(object sender, RoutedEventArgs e) => await ExportActionAsync("pin");
    private async void OnShare(object sender, RoutedEventArgs e) => await ExportActionAsync("share");
    public async Task ExportActionAsync(string action)
    {
        if (_exporting || _sourceBitmap == null) return;
        _exporting = true; var id = _itemId; var generation = _loadGeneration;
        var draft = EditorDraftStore.Shared.For(id); var snapshot = draft.Current;
        var preset = SettingsStore.Shared.Settings.Export.SelectedPresetID;
        var template = SettingsStore.Shared.Settings.Export.FilenameTemplate;
        try
        {
            WorkspaceStore.Shared.Notify(L.T("editor.exporting"));
            using var rendered = await RenderAsync();
            if (rendered == null || _disposed || id != _itemId || generation != _loadGeneration) return;
            switch (action)
            {
                case "preview": PreviewImage.Source = BitmapHelpers.ToBitmapSource(rendered); PreviewOverlay.Visibility = Visibility.Visible; break;
                case "copy": await ClipboardService.SetBitmapAsync(rendered); WorkspaceStore.Shared.Notify(L.T("review.copied")); break;
                case "pin": ShotActions.Pin(rendered); WorkspaceStore.Shared.Notify(L.T("review.pinned")); break;
                case "share":
                    var temporary = ImageExportService.SaveBitmap(rendered, true, preset, template);
                    await ShotActions.ShareAsync(temporary); break;
                default:
                    var path = ImageExportService.SaveBitmap(rendered, false, preset, template);
                    draft.MarkSaved(snapshot);
                    await WorkspaceStore.Shared.RefreshAsync();
                    WorkspaceStore.Shared.Notify(L.T("editor.saved") + " · " + path); break;
            }
        }
        catch (Exception ex) { Logger.LogException("Export", ex); WorkspaceStore.Shared.Notify(ex.Message); }
        finally { _exporting = false; }
    }
    private void OnEditorKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { _ = ExportActionAsync("save"); e.Handled = true; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C) { _ = ExportActionAsync("copy"); e.Handled = true; }
        if (!_review && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Canvas.Undo(); e.Handled = true; }
        if (!_review && ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) || (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z))) { Canvas.Redo(); e.Handled = true; }
        if (e.Key == Key.Escape) { PreviewOverlay.Visibility = Visibility.Collapsed; e.Handled = true; }
    }
    public void Dispose()
    {
        _disposed = true; ++_loadGeneration; _sourceBitmap?.Dispose(); _sourceBitmap = null; Canvas.ReleaseImage();
    }
}
