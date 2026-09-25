using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QPARKShot.Helpers;
using QPARKShot.Localization;
using QPARKShot.Services;
using QPARKShot.Views;
namespace QPARKShot;

public partial class MainWindow : Window
{
    public IntPtr Hwnd { get; private set; }
    public bool IsQuitting { get; set; }
    private GalleryPage? _gallery;
    private EditorPage? _editor;
    private Window? _preferences;
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            var corner = 2; DwmSetWindowAttribute(Hwnd, 33, ref corner, sizeof(int));
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icon)) Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(icon));
            UpdateAppearance();
        };
        Loaded += async (_, _) => { ShowGallery(); await WorkspaceStore.Shared.RefreshAsync(); };
        WorkspaceStore.Shared.Changed += OnWorkspaceChanged;
        SettingsStore.Shared.SettingsChanged += OnSettingsChanged;
        PreviewKeyDown += OnKeyDown;
        Closed += (_, _) =>
        {
            WorkspaceStore.Shared.Changed -= OnWorkspaceChanged;
            SettingsStore.Shared.SettingsChanged -= OnSettingsChanged;
            _editor?.Dispose();
        };
    }
    private void OnWorkspaceChanged(object? sender, EventArgs e) => StatusText.Text = WorkspaceStore.Shared.Status;
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        UpdateAppearance();
        _editor?.ApplySettings();
    }
    public void UpdateAppearance()
    {
        var dark = App.IsDarkTheme;
        var value = dark ? 1 : 0;
        if (Hwnd != IntPtr.Zero) DwmSetWindowAttribute(Hwnd, 20, ref value, sizeof(int));
        FlowDirection = L.Shared.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        L.Shared.Refresh();
    }
    public void ShowGallery()
    {
        _editor?.Dispose(); _editor = null;
        _gallery ??= new GalleryPage();
        ContentHost.Content = _gallery;
        _gallery.RefreshView();
        EnsureVisible();
    }
    public void ShowSession()
    {
        WorkspaceStore.Shared.Section = "session";
        SectionList.SelectedIndex = 1;
        ShowGallery();
    }
    public void ShowSettings() => ShowPreferences("appearance");
    public void ShowAbout() => ShowPreferences("about");
    private void ShowPreferences(string tab)
    {
        if (_preferences != null) { _preferences.Activate(); return; }
        _preferences = new Window
        {
            Title = L.T("common.settings"), Owner = this, Width = 940, Height = 720, MinWidth = 880, MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new SettingsPage(tab), FlowDirection = FlowDirection,
        };
        _preferences.SetResourceReference(BackgroundProperty, "WindowBackground");
        _preferences.SetResourceReference(ForegroundProperty, "WindowForeground");
        _preferences.Closed += (_, _) => { _preferences = null; WorkspaceStore.Shared.StartOcr(); _ = WorkspaceStore.Shared.RefreshAsync(); };
        _preferences.Show();
    }
    public void ShowEditor(Guid id) => OpenShot(id, false);
    public void ShowReview(Guid id) => OpenShot(id, true);
    private void OpenShot(Guid id, bool review)
    {
        _editor?.Dispose();
        _editor = new EditorPage(id, review);
        ContentHost.Content = _editor;
        EnsureVisible();
    }
    public void HideForCapture() { _preferences?.Hide(); Hide(); }
    public void EnsureVisible()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
    public bool ConfirmClearSession()
    {
        if (ShotQueueStore.Shared.Items.Count == 0) return true;
        var message = L.T("workspace.clear_session_message");
        if (EditorDraftStore.Shared.HasUnsavedChanges) message += "\n\n" + L.T("workspace.unsaved_draft_warning");
        if (MessageBox.Show(this, message, L.T("workspace.clear_session_title"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return false;
        _editor?.Dispose(); _editor = null;
        ShotQueueStore.Shared.ClearAll(); ShowSession(); return true;
    }
    public bool ConfirmQuit() => (ShotQueueStore.Shared.Items.Count == 0 && !EditorDraftStore.Shared.HasUnsavedChanges) ||
        MessageBox.Show(this, L.T("windows.quit_warning"), "QPARK Shot", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
    private void OnClearSession(object sender, RoutedEventArgs e) => ConfirmClearSession();
    private void OnSettings(object sender, RoutedEventArgs e) => ShowSettings();
    private void OnSectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentHost == null) return;
        WorkspaceStore.Shared.Section = (SectionList.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "library";
        ShowGallery();
    }
    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        WorkspaceStore.Shared.Search = SearchBox.Text;
        _gallery?.RefreshView();
    }
    private async void OnCapture(object sender, RoutedEventArgs e) => await CaptureService.Shared.TriggerCapture();
    private void OnCaptureMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var (key, mode) in new[] { ("capture.selected_area", "selection"), ("capture.full_screen", "fullScreen"), ("capture.window", "window"), ("capture.repeat_area", "repeatArea") })
        {
            var item = new MenuItem { Header = L.T(key), IsEnabled = mode != "repeatArea" || SettingsStore.Shared.Settings.Capture.LastRegion != null };
            item.Click += async (_, _) => await CaptureService.Shared.TriggerCapture(mode);
            menu.Items.Add(item);
        }
        foreach (var seconds in new[] { 3, 5, 10 })
        {
            var item = new MenuItem { Header = L.T("capture.with_delay") + " · " + L.Format("capture.seconds_format", seconds) };
            item.Click += async (_, _) => await CaptureService.Shared.TriggerCapture(delayOverride: seconds); menu.Items.Add(item);
        }
        menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!IsQuitting) { e.Cancel = true; Hide(); }
        else base.OnClosing(e);
    }
}
