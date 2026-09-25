using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using QPARKShot.Helpers;
using QPARKShot.Localization;
using QPARKShot.Models;
using QPARKShot.Services;
namespace QPARKShot.Views;

public partial class GalleryPage : UserControl
{
    public ObservableCollection<LibraryEntry> Screenshots { get; } = new();
    private string? _inspectedPath;
    private string? _inspectedSection;
    private TextBlock? _ocrStatus;
    private TextBox? _ocrText;
    private Point _dragStart;
    public GalleryPage()
    {
        InitializeComponent(); GalleryItems.ItemsSource = Screenshots;
        Loaded += OnLoaded; Unloaded += OnUnloaded;
        GalleryItems.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(GalleryItems);
        GalleryItems.PreviewMouseMove += (_, e) =>
        {
            var current = e.GetPosition(GalleryItems);
            if (e.LeftButton != MouseButtonState.Pressed || (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)) return;
            if (GalleryItems.SelectedItem is LibraryEntry { Missing: false } entry && File.Exists(entry.Path))
                DragDrop.DoDragDrop(GalleryItems, new DataObject(DataFormats.FileDrop, new[] { entry.Path }), DragDropEffects.Copy);
        };
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WorkspaceStore.Shared.Changed += OnChanged;
        ShotQueueStore.Shared.Items.CollectionChanged += OnQueueChanged;
        RefreshView();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WorkspaceStore.Shared.Changed -= OnChanged;
        ShotQueueStore.Shared.Items.CollectionChanged -= OnQueueChanged;
    }
    private void OnChanged(object? sender, EventArgs e) => RefreshView();
    private void OnQueueChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshView();
    public void RefreshView()
    {
        var store = WorkspaceStore.Shared;
        Heading.Text = L.T(store.Section == "session" ? "workspace.current_session" : "workspace." + store.Section);
        var values = store.Section == "session"
            ? ShotQueueStore.Shared.Items.Where(i => Path.GetFileName(i.Path).Contains(store.Search, StringComparison.CurrentCultureIgnoreCase))
                .Select(i => new LibraryEntry { Path = i.Path, CreatedAt = i.CapturedAt.ToUniversalTime() }).Reverse().ToArray()
            : store.DisplayedEntries().ToArray();
        var selected = store.SelectedPath;
        if (!Screenshots.Select(s => s.Path).SequenceEqual(values.Select(s => s.Path)))
        {
            Screenshots.Clear(); foreach (var value in values) Screenshots.Add(value);
        }
        var target = Screenshots.FirstOrDefault(e => e.Path == selected) ?? Screenshots.FirstOrDefault();
        GalleryItems.SelectedItem = target;
        store.SelectedPath = target?.Path;
        EmptyState.Visibility = values.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Text = L.T(store.Search.Length > 0 ? "workspace.no_matches" : store.Section == "session" ? "workspace.empty_session" : store.Section == "missing" ? "workspace.no_missing" : "workspace.no_shots");
        UpdateInspector();
    }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        WorkspaceStore.Shared.SelectedPath = (GalleryItems.SelectedItem as LibraryEntry)?.Path;
        UpdateInspector();
    }
    private void UpdateInspector()
    {
        if (GalleryItems.SelectedItem is not LibraryEntry entry) { Inspector.Children.Clear(); _inspectedPath = null; return; }
        if (_inspectedPath != entry.Path || _inspectedSection != WorkspaceStore.Shared.Section)
        {
            _inspectedPath = entry.Path; _inspectedSection = WorkspaceStore.Shared.Section; _ocrStatus = null; _ocrText = null; Inspector.Children.Clear();
            Inspector.Children.Add(new TextBlock { Text = entry.FileName, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
            if (WorkspaceStore.Shared.Section == "session")
            {
                AddAction("common.edit", () => OpenSelected(false));
                AddAction("common.preview", () => OpenSelected(true));
                AddAction("common.delete", RemoveSession);
                return;
            }
            if (entry.Missing)
            {
                Inspector.Children.Add(new TextBlock { Text = L.T("workspace.missing_hint"), TextWrapping = TextWrapping.Wrap });
                AddAction("common.locate", () => Locate(entry));
                AddAction("common.forget", () => { GalleryIndexStore.Shared.Forget(entry.Path); RefreshView(); }); return;
            }
            AddAction("common.edit", () => OpenSelected(false));
            AddAction("common.preview", () => OpenSelected(true));
            var favorite = new CheckBox { Content = L.T("common.favorite"), IsChecked = entry.Favorite, Margin = new Thickness(0, 12, 0, 8) };
            favorite.Click += (_, _) => { entry.Favorite = favorite.IsChecked == true; GalleryIndexStore.Shared.Save(); GalleryItems.Items.Refresh(); RefreshView(); };
            Inspector.Children.Add(favorite);
            Inspector.Children.Add(new TextBlock { Text = L.T("inspector.tags"), Margin = new Thickness(0, 10, 0, 6) });
            var tags = new TextBox { Text = string.Join(", ", entry.Tags), ToolTip = L.T("inspector.tags_hint") };
            tags.LostKeyboardFocus += (_, _) => { entry.Tags = tags.Text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList(); GalleryIndexStore.Shared.Save(); RefreshView(); };
            Inspector.Children.Add(tags);
            AddAction("common.show_in_finder", () => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{entry.Path}\""));
            AddAction("common.move_to_trash", () => Trash(entry));
            Inspector.Children.Add(new TextBlock { Text = L.T("inspector.ocr"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 6) });
            _ocrStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
            _ocrText = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Inspector.Children.Add(_ocrStatus); Inspector.Children.Add(_ocrText);
            AddAction("common.retry", () => WorkspaceStore.Shared.RetryOcr(entry.Path));
        }
        if (_ocrStatus != null)
        {
            _ocrStatus.Text = !SettingsStore.Shared.Settings.Gallery.OcrEnabled ? L.T("status.ocr_disabled") : entry.OcrError ?? L.T(entry.OcrStatus switch { "ready" => "status.ready", "empty" => "status.no_text", _ => "status.indexing" });
            if (_ocrText != null) _ocrText.Text = entry.OcrText;
        }
    }
    private void AddAction(string key, Action action)
    {
        var button = new Button { Content = L.T(key), Margin = new Thickness(0, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => { try { action(); } catch (Exception ex) { WorkspaceStore.Shared.Notify(ex.Message); } };
        Inspector.Children.Add(button);
    }
    private void OpenSelected(bool review)
    {
        if (GalleryItems.SelectedItem is not LibraryEntry { Missing: false } entry) return;
        var item = ShotQueueStore.Shared.Enqueue(entry.Path);
        if (review) App.MainWindowInstance?.ShowReview(item.Id); else App.MainWindowInstance?.ShowEditor(item.Id);
    }
    private void OnOpen(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is FrameworkElement f && f.DataContext is LibraryEntry) OpenSelected(false); }
    private void OnListKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OpenSelected(false); e.Handled = true; }
        if (e.Key == Key.Delete && GalleryItems.SelectedItem is LibraryEntry entry)
        { if (WorkspaceStore.Shared.Section == "session") RemoveSession(); else if (!entry.Missing) Trash(entry); e.Handled = true; }
    }
    private void RemoveSession()
    {
        if (GalleryItems.SelectedItem is not LibraryEntry entry) return;
        var item = ShotQueueStore.Shared.Items.FirstOrDefault(i => i.Path == entry.Path);
        if (item == null) return;
        if (MessageBox.Show(Window.GetWindow(this), L.T("windows.remove_warning"), "QPARK Shot", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            ShotQueueStore.Shared.Remove(item.Id);
    }
    private async void Trash(LibraryEntry entry)
    {
        if (MessageBox.Show(Window.GetWindow(this), L.T("common.move_to_trash") + "?\n" + entry.FileName, "QPARK Shot", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        try
        {
            FileSystem.DeleteFile(entry.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            GalleryIndexStore.Shared.Forget(entry.Path);
            await WorkspaceStore.Shared.RefreshAsync();
            WorkspaceStore.Shared.Notify(L.T("status.moved_to_trash"));
        }
        catch (Exception ex) { WorkspaceStore.Shared.Notify(ex.Message); }
    }
    private void Locate(LibraryEntry entry)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PNG|*.png" };
        if (dialog.ShowDialog() != true) return;
        using var bitmap = BitmapHelpers.LoadBitmap(dialog.FileName);
        if (bitmap == null) throw new IOException(L.T("status.unsupported_image"));
        GalleryIndexStore.Shared.Relink(entry.Path, dialog.FileName); _inspectedPath = null; RefreshView();
        WorkspaceStore.Shared.StartOcr();
    }
    private async void OnRefresh(object sender, RoutedEventArgs e) => await WorkspaceStore.Shared.RefreshAsync();
}
public sealed class ThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is not string path || !File.Exists(path)) return null;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 420; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image;
        }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
