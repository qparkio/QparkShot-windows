using System;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace QPARKShot.Views;

public sealed partial class ShotQueueSidebar : UserControl
{
    public Action<Guid>? OnRequestOpen;

    public ShotQueueSidebar()
    {
        this.InitializeComponent();
        QueueRepeater.ItemsSource = ShotQueueStore.Shared.Items;
        ShotQueueStore.Shared.Items.CollectionChanged += OnItemsChanged;
        ShotQueueStore.Shared.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShotQueueStore.ActiveId)) Rebuild();
        };
        QueueRepeater.ItemTemplate = BuildTemplate();
        Loaded += (_, _) => Rebuild();
    }

    private DataTemplate BuildTemplate()
    {
        // Lightweight inline template builder to avoid extra XAML files.
        const string xaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Grid x:Name="ItemRoot" Margin="0,0,0,8" CanDrag="True">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <Grid Grid.Row="0" Width="104" Height="66" CornerRadius="5" Background="#33000000">
      <Image x:Name="Thumb" Stretch="UniformToFill"/>
      <Border x:Name="ActiveBorder" BorderBrush="{ThemeResource AccentTextFillColorPrimaryBrush}"
              BorderThickness="2" CornerRadius="5"/>
      <StackPanel x:Name="HoverActions" Orientation="Horizontal" Spacing="4"
                  VerticalAlignment="Top" HorizontalAlignment="Right" Margin="4"
                  Visibility="Collapsed">
        <Button x:Name="PreviewBtn" Padding="4" Background="#80000000">
          <FontIcon Glyph="&#xE7B3;" FontFamily="Segoe Fluent Icons" FontSize="9" Foreground="White"/>
        </Button>
        <Button x:Name="DeleteBtn" Padding="4" Background="#FFD13030">
          <FontIcon Glyph="&#xE74D;" FontFamily="Segoe Fluent Icons" FontSize="9" Foreground="White"/>
        </Button>
      </StackPanel>
    </Grid>
    <TextBlock Grid.Row="1" x:Name="TimeText" FontSize="9"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}"
               HorizontalAlignment="Left" Margin="2,2,0,0"/>
  </Grid>
</DataTemplate>
""";
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        HeaderText.Text = $"Buffer · {ShotQueueStore.Shared.Items.Count}";
        ClearAllButton.Visibility = ShotQueueStore.Shared.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Bind code-behind reactions to elements within each container.
        // Because we used ItemsRepeater without container hooks, walk the children once layout has run.
        DispatcherQueue.TryEnqueue(() =>
        {
            for (int i = 0; i < ShotQueueStore.Shared.Items.Count; i++)
            {
                var item = ShotQueueStore.Shared.Items[i];
                var element = QueueRepeater.TryGetElement(i);
                if (element is FrameworkElement root)
                {
                    Wire(root, item);
                }
            }
        });
    }

    private void Wire(FrameworkElement root, ShotQueueItem item)
    {
        var thumb = root.FindName("Thumb") as Image;
        var activeBorder = root.FindName("ActiveBorder") as Border;
        var hover = root.FindName("HoverActions") as StackPanel;
        var previewBtn = root.FindName("PreviewBtn") as Button;
        var deleteBtn = root.FindName("DeleteBtn") as Button;
        var timeText = root.FindName("TimeText") as TextBlock;

        if (timeText != null) timeText.Text = item.CapturedAt.ToString("HH:mm:ss");
        if (activeBorder != null) activeBorder.BorderThickness =
            ShotQueueStore.Shared.ActiveId == item.Id ? new Thickness(2) : new Thickness(0);

        // Hover overlay
        root.PointerEntered -= RootHoverOn;
        root.PointerExited -= RootHoverOff;
        root.Tag = (item.Id, hover);
        root.PointerEntered += RootHoverOn;
        root.PointerExited += RootHoverOff;

        // Tap → open
        root.Tapped -= OnItemTapped;
        root.Tapped += OnItemTapped;

        // Drag-out
        root.DragStarting -= OnItemDragStarting;
        root.DragStarting += OnItemDragStarting;

        // Preview / delete
        if (previewBtn != null)
        {
            previewBtn.Click -= OnPreviewClick;
            previewBtn.Click += OnPreviewClick;
            previewBtn.Tag = item;
            var ws = WatermarkSettings.FromStore(SettingsStore.Shared);
            previewBtn.Visibility = ws.HasAnyWatermark ? Visibility.Visible : Visibility.Collapsed;
        }
        if (deleteBtn != null)
        {
            deleteBtn.Click -= OnDeleteClick;
            deleteBtn.Click += OnDeleteClick;
            deleteBtn.Tag = item;
        }

        // Thumbnail
        if (thumb?.Source == null)
        {
            _ = LoadThumbAsync(thumb, item.Path);
        }
    }

    private async Task LoadThumbAsync(Image? target, string path)
    {
        if (target == null) return;
        await Task.Run(async () =>
        {
            using var thumb = BitmapHelpers.Thumbnail(path, 240);
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
                target.Source = bmp;
            });
        });
    }

    private void RootHoverOn(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ValueTuple<Guid, StackPanel?> tup && tup.Item2 != null)
            tup.Item2.Visibility = Visibility.Visible;
    }
    private void RootHoverOff(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ValueTuple<Guid, StackPanel?> tup && tup.Item2 != null)
            tup.Item2.Visibility = Visibility.Collapsed;
    }

    private void OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ValueTuple<Guid, StackPanel?> tup)
        {
            ShotQueueStore.Shared.ActiveId = tup.Item1;
            OnRequestOpen?.Invoke(tup.Item1);
        }
    }

    private async void OnItemDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement fe && fe.Tag is ValueTuple<Guid, StackPanel?> tup)
        {
            var item = ShotQueueStore.Shared.Item(tup.Item1);
            if (item == null || !File.Exists(item.Path)) return;
            var deferral = args.GetDeferral();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(item.Path);
                args.Data.SetStorageItems(new IStorageItem[] { file });
                args.Data.RequestedOperation = DataPackageOperation.Copy;
            }
            catch { }
            finally { deferral.Complete(); }
        }
    }

    private async void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is ShotQueueItem item)
        {
            var bitmap = await Task.Run(() => BitmapHelpers.LoadBitmap(item.Path));
            if (bitmap == null) return;
            var ws = WatermarkSettings.FromStore(SettingsStore.Shared);
            var rendered = await Task.Run(() =>
                WatermarkRenderer.Render(bitmap, Array.Empty<Annotation>(), null, ws));
            bitmap.Dispose();
            if (rendered != null)
            {
                var dialog = new ContentDialog
                {
                    Title = $"Preview · {Path.GetFileName(item.Path)}",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot,
                };
                var img = new Image
                {
                    Stretch = Stretch.Uniform,
                    Source = await BitmapHelpers.ToBitmapImageAsync(rendered),
                    MaxWidth = 800,
                    MaxHeight = 600,
                };
                dialog.Content = img;
                rendered.Dispose();
                await dialog.ShowAsync();
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is ShotQueueItem item)
        {
            var wasActive = (ShotQueueStore.Shared.ActiveId == item.Id);
            var nextId = ShotQueueStore.Shared.Remove(item.Id);
            if (wasActive)
            {
                if (nextId.HasValue) OnRequestOpen?.Invoke(nextId.Value);
                else App.MainWindowInstance?.ShowGallery();
            }
        }
    }

    private async void OnClearAll(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear screenshot buffer?",
            Content = $"All {ShotQueueStore.Shared.Items.Count} screenshots will be removed from the buffer. Files saved to Pictures are not affected.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ShotQueueStore.Shared.ClearAll();
            App.MainWindowInstance?.ShowGallery();
        }
    }
}
