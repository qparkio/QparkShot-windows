using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;

namespace QPARKShot.Views;

public partial class ShotQueueSidebar : UserControl
{
    public Action<Guid>? OnRequestOpen;

    public ShotQueueSidebar()
    {
        InitializeComponent();
        ShotQueueStore.Shared.Items.CollectionChanged += OnItemsChanged;
        ShotQueueStore.Shared.PropertyChanged += OnStorePropertyChanged;
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) =>
        {
            ShotQueueStore.Shared.Items.CollectionChanged -= OnItemsChanged;
            ShotQueueStore.Shared.PropertyChanged -= OnStorePropertyChanged;
        };
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(Rebuild));

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShotQueueStore.ActiveId))
            Dispatcher.BeginInvoke(new Action(Rebuild));
    }

    private void Rebuild()
    {
        HeaderText.Text = $"Buffer · {ShotQueueStore.Shared.Items.Count}";
        ClearAllButton.Visibility = ShotQueueStore.Shared.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        QueueItems.Items.Clear();
        foreach (var item in ShotQueueStore.Shared.Items)
        {
            QueueItems.Items.Add(BuildItemCard(item));
        }
    }

    private FrameworkElement BuildItemCard(ShotQueueItem item)
    {
        bool isActive = ShotQueueStore.Shared.ActiveId == item.Id;

        var root = new Grid { Margin = new Thickness(0, 0, 0, 8), Tag = item.Id, Cursor = Cursors.Hand };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var thumbBorder = new Border
        {
            Width = 104,
            Height = 66,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
            BorderBrush = isActive
                ? (Brush)Application.Current.Resources["QPARKAccent"]
                : new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(isActive ? 2 : 1),
            ClipToBounds = true,
        };
        Grid.SetRow(thumbBorder, 0);

        var thumbGrid = new Grid();
        var thumb = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill };
        thumbGrid.Children.Add(thumb);

        var hoverActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            Visibility = Visibility.Collapsed,
        };

        var ws = WatermarkSettings.FromStore(SettingsStore.Shared);
        if (ws.HasAnyWatermark)
        {
            var previewBtn = new Button
            {
                Content = new TextBlock { Text = "👁", FontSize = 10, Foreground = Brushes.White },
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "Preview with watermark",
            };
            previewBtn.Click += (_, _) => PreviewWatermark(item);
            hoverActions.Children.Add(previewBtn);
        }

        var deleteBtn = new Button
        {
            Content = new TextBlock { Text = "🗑", FontSize = 10, Foreground = Brushes.White },
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xD1, 0x30, 0x30)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            ToolTip = "Remove from buffer",
        };
        deleteBtn.Click += (_, _) => DeleteItem(item);
        hoverActions.Children.Add(deleteBtn);

        thumbGrid.Children.Add(hoverActions);
        thumbBorder.Child = thumbGrid;

        root.MouseEnter += (_, _) => hoverActions.Visibility = Visibility.Visible;
        root.MouseLeave += (_, _) => hoverActions.Visibility = Visibility.Collapsed;
        root.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is Button) return;
            ShotQueueStore.Shared.ActiveId = item.Id;
            OnRequestOpen?.Invoke(item.Id);
        };
        root.PreviewMouseMove += (sender, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
                    DragDrop.DoDragDrop(root, data, DragDropEffects.Copy);
                }
                catch (Exception ex) { Logger.LogException("Sidebar drag", ex); }
            }
        };

        root.Children.Add(thumbBorder);

        var timeText = new TextBlock
        {
            Text = item.CapturedAt.ToString("HH:mm:ss"),
            FontSize = 9,
            Foreground = (Brush)Application.Current.Resources["SecondaryText"],
            Margin = new Thickness(2, 4, 0, 0),
        };
        Grid.SetRow(timeText, 1);
        root.Children.Add(timeText);

        _ = LoadThumbAsync(thumb, item.Path);
        return root;
    }

    private async Task LoadThumbAsync(System.Windows.Controls.Image target, string path)
    {
        await Task.Run(() =>
        {
            using var t = BitmapHelpers.Thumbnail(path, 240);
            if (t == null) return;
            var src = BitmapHelpers.ToBitmapSource(t);
            Dispatcher.Invoke(() => target.Source = src);
        });
    }

    private async void PreviewWatermark(ShotQueueItem item)
    {
        var bmp = await Task.Run(() => BitmapHelpers.LoadBitmap(item.Path));
        if (bmp == null) return;
        var ws = WatermarkSettings.FromStore(SettingsStore.Shared);
        var rendered = await Task.Run(() => WatermarkRenderer.Render(bmp, Array.Empty<Annotation>(), null, ws));
        bmp.Dispose();
        if (rendered == null) return;

        var window = new Window
        {
            Title = $"Preview · {Path.GetFileName(item.Path)}",
            Owner = Window.GetWindow(this),
            Width = 900,
            Height = 700,
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A)),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10),
        };
        scroll.Content = new Image
        {
            Source = BitmapHelpers.ToBitmapSource(rendered),
            Stretch = Stretch.Uniform,
        };
        window.Content = scroll;
        rendered.Dispose();
        window.ShowDialog();
    }

    private void DeleteItem(ShotQueueItem item)
    {
        var wasActive = (ShotQueueStore.Shared.ActiveId == item.Id);
        var nextId = ShotQueueStore.Shared.Remove(item.Id);
        if (wasActive)
        {
            if (nextId.HasValue) OnRequestOpen?.Invoke(nextId.Value);
            else App.MainWindowInstance?.ShowGallery();
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"All {ShotQueueStore.Shared.Items.Count} screenshots will be removed from the buffer. " +
            "Files saved to Pictures are not affected.",
            "Clear buffer?",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
        {
            ShotQueueStore.Shared.ClearAll();
            App.MainWindowInstance?.ShowGallery();
        }
    }
}
