using System;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QPARKShot.Models;
using QPARKShot.Services;

namespace QPARKShot.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string tabKey)
        {
            int idx = tabKey switch
            {
                "appearance" => 0,
                "hotkeys" => 1,
                "watermark" => 2,
                "storage" => 3,
                "buffer" => 4,
                "about" => 5,
                _ => 0
            };
            TabSelector.SelectedIndex = idx;
        }
        BuildTab();
    }

    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowGallery();
    private void OnTabChanged(object sender, SelectionChangedEventArgs e) => BuildTab();

    private void BuildTab()
    {
        ContentRoot.Children.Clear();
        switch (TabSelector.SelectedIndex)
        {
            case 0: BuildAppearance(); break;
            case 1: BuildHotkeys(); break;
            case 2: BuildWatermark(); break;
            case 3: BuildStorage(); break;
            case 4: BuildBuffer(); break;
            default: BuildAbout(); break;
        }
    }

    // ===== Helpers =====

    private static Border Card(UIElement content) => new()
    {
        Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14),
        Child = content,
    };

    private static TextBlock Header(string title) => new()
    {
        Text = title,
        FontSize = 16,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static StackPanel Section(string title, params UIElement[] cards)
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(Header(title));
        foreach (var c in cards) sp.Children.Add(c);
        return sp;
    }

    // ===== Appearance =====
    private void BuildAppearance()
    {
        var s = SettingsStore.Shared.Settings;
        var picker = new ComboBox { MinWidth = 160 };
        picker.Items.Add(new ComboBoxItem { Content = "System", Tag = "system", IsSelected = s.ThemePreference == "system" });
        picker.Items.Add(new ComboBoxItem { Content = "Light",  Tag = "light",  IsSelected = s.ThemePreference == "light" });
        picker.Items.Add(new ComboBoxItem { Content = "Dark",   Tag = "dark",   IsSelected = s.ThemePreference == "dark" });
        picker.SelectionChanged += (_, _) =>
        {
            var tag = (picker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "system";
            SettingsStore.Shared.Mutate(x => x.ThemePreference = tag);
        };
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = "Theme", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(picker);
        stack.Children.Add(new TextBlock
        {
            Text = "Follows the current Windows theme automatically when set to System.",
            FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        ContentRoot.Children.Add(Section("Appearance", Card(stack)));
    }

    // ===== Hotkeys =====
    private void BuildHotkeys()
    {
        ContentRoot.Children.Add(Section("Hotkeys",
            HotkeyCard("Selection Capture Hotkey", SettingsStore.Shared.Settings.Hotkey, "Ctrl + Shift + C by default"),
            HotkeyCard("Full-Screen Capture Hotkey", SettingsStore.Shared.Settings.FullScreenHotkey, "Off by default"),
            CaptureModeCard()));
    }

    private Border HotkeyCard(string title, HotkeyConfig cfg, string subtitle)
    {
        var stack = new StackPanel { Spacing = 8 };
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var toggle = new ToggleSwitch
        {
            IsOn = cfg.Enabled,
            OnContent = "On", OffContent = "Off",
        };
        titleStack.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(toggle);
        stack.Children.Add(titleStack);
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        var detail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
        var ctrl  = new CheckBox { Content = "Ctrl",  IsChecked = cfg.Modifiers.Contains("control") };
        var shift = new CheckBox { Content = "Shift", IsChecked = cfg.Modifiers.Contains("shift") };
        var alt   = new CheckBox { Content = "Alt",   IsChecked = cfg.Modifiers.Contains("option") || cfg.Modifiers.Contains("alt") };
        var win   = new CheckBox { Content = "Win",   IsChecked = cfg.Modifiers.Contains("command") || cfg.Modifiers.Contains("win") };
        var key   = new TextBox { Text = cfg.Key, MaxLength = 1, Width = 40 };
        detail.Children.Add(ctrl); detail.Children.Add(shift); detail.Children.Add(alt); detail.Children.Add(win); detail.Children.Add(new TextBlock { Text = "Key:", VerticalAlignment = VerticalAlignment.Center }); detail.Children.Add(key);
        stack.Children.Add(detail);

        void Persist()
        {
            cfg.Enabled = toggle.IsOn;
            var mods = new System.Collections.Generic.List<string>();
            if (ctrl.IsChecked ?? false) mods.Add("control");
            if (shift.IsChecked ?? false) mods.Add("shift");
            if (alt.IsChecked ?? false) mods.Add("option");
            if (win.IsChecked ?? false) mods.Add("command");
            cfg.Modifiers = mods;
            cfg.Key = (key.Text ?? "").Trim().ToUpperInvariant();
            if (cfg.Key.Length > 1) cfg.Key = cfg.Key.Substring(0, 1);
            SettingsStore.Shared.Save();
        }
        toggle.Toggled += (_, _) => Persist();
        ctrl.Checked  += (_, _) => Persist(); ctrl.Unchecked  += (_, _) => Persist();
        shift.Checked += (_, _) => Persist(); shift.Unchecked += (_, _) => Persist();
        alt.Checked   += (_, _) => Persist(); alt.Unchecked   += (_, _) => Persist();
        win.Checked   += (_, _) => Persist(); win.Unchecked   += (_, _) => Persist();
        key.TextChanged += (_, _) => Persist();

        return Card(stack);
    }

    private Border CaptureModeCard()
    {
        var s = SettingsStore.Shared.Settings;
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = "Capture Mode", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var modePicker = new ComboBox { MinWidth = 160 };
        modePicker.Items.Add(new ComboBoxItem { Content = "Selection", Tag = "selection", IsSelected = s.Capture.Mode == "selection" });
        modePicker.Items.Add(new ComboBoxItem { Content = "Full Screen", Tag = "fullScreen", IsSelected = s.Capture.Mode == "fullScreen" });
        modePicker.SelectionChanged += (_, _) =>
        {
            s.Capture.Mode = (modePicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "selection";
            SettingsStore.Shared.Save();
        };
        stack.Children.Add(modePicker);

        var delayRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        delayRow.Children.Add(new TextBlock { Text = "Default Delay:", VerticalAlignment = VerticalAlignment.Center });
        var delayPicker = new ComboBox { MinWidth = 100 };
        foreach (var (label, sec) in new[] { ("None", 0), ("3 s", 3), ("5 s", 5), ("10 s", 10) })
        {
            delayPicker.Items.Add(new ComboBoxItem { Content = label, Tag = sec, IsSelected = s.Capture.DelaySeconds == sec });
        }
        delayPicker.SelectionChanged += (_, _) =>
        {
            s.Capture.DelaySeconds = (int)((delayPicker.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            SettingsStore.Shared.Save();
        };
        delayRow.Children.Add(delayPicker);
        stack.Children.Add(delayRow);

        return Card(stack);
    }

    // ===== Watermark =====
    private void BuildWatermark()
    {
        var s = SettingsStore.Shared.Settings.Watermark;
        var stack = new StackPanel { Spacing = 8 };

        var layoutPicker = new ComboBox { MinWidth = 180 };
        layoutPicker.Items.Add(new ComboBoxItem { Content = "Single Location", Tag = "single", IsSelected = s.LayoutMode == "single" });
        layoutPicker.Items.Add(new ComboBoxItem { Content = "Tiled Diagonal", Tag = "tiled", IsSelected = s.LayoutMode == "tiled" });
        layoutPicker.SelectionChanged += (_, _) =>
        {
            s.LayoutMode = (layoutPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "single";
            SettingsStore.Shared.Save();
        };
        stack.Children.Add(new TextBlock { Text = "Layout Mode", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(layoutPicker);

        // Text watermark
        var txtToggle = new ToggleSwitch { Header = "Text watermark", IsOn = s.Text.Enabled };
        var txtField = new TextBox { Header = "Text", Text = s.Text.Text, IsEnabled = s.Text.Enabled };
        txtToggle.Toggled += (_, _) => { s.Text.Enabled = txtToggle.IsOn; txtField.IsEnabled = txtToggle.IsOn; SettingsStore.Shared.Save(); };
        txtField.TextChanged += (_, _) => { s.Text.Text = txtField.Text; SettingsStore.Shared.Save(); };
        var textStack = new StackPanel { Spacing = 6 };
        textStack.Children.Add(txtToggle); textStack.Children.Add(txtField);

        // Logo watermark
        var logoToggle = new ToggleSwitch { Header = "Logo watermark", IsOn = s.Logo.Enabled };
        var logoPath = new TextBox { Header = "Logo path", Text = s.Logo.Path, IsReadOnly = true };
        var browseBtn = new Button { Content = "Browse…" };
        browseBtn.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".png"); picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg");
            // unpackaged WinUI 3: requires HWND init.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowInstance!.Hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                s.Logo.Path = file.Path; logoPath.Text = file.Path; SettingsStore.Shared.Save();
            }
        };
        var logoStack = new StackPanel { Spacing = 6 };
        logoStack.Children.Add(logoToggle); logoStack.Children.Add(logoPath); logoStack.Children.Add(browseBtn);
        logoToggle.Toggled += (_, _) => { s.Logo.Enabled = logoToggle.IsOn; SettingsStore.Shared.Save(); };

        // Sliders
        var opacity = new Slider { Header = "Opacity", Minimum = 10, Maximum = 100, Value = s.Logo.Opacity * 100, MinWidth = 200 };
        opacity.ValueChanged += (_, _) => { s.Logo.Opacity = opacity.Value / 100.0; SettingsStore.Shared.Save(); };
        var size = new Slider { Header = "Logo size (px)", Minimum = 50, Maximum = 300, Value = s.Logo.Size, MinWidth = 200 };
        size.ValueChanged += (_, _) => { s.Logo.Size = size.Value; SettingsStore.Shared.Save(); };

        ContentRoot.Children.Add(Section("Watermark Settings",
            Card(stack),
            Card(textStack),
            Card(logoStack),
            Card(new StackPanel { Spacing = 6, Children = { opacity, size } })));
    }

    // ===== Storage =====
    private void BuildStorage()
    {
        var s = SettingsStore.Shared.Settings.Cleanup;
        var savePathBox = new TextBox { Header = "Save folder", Text = string.IsNullOrEmpty(s.SaveDirectory) ? "Default (Pictures\\QPARK Shot)" : s.SaveDirectory, IsReadOnly = true };
        var browse = new Button { Content = "Browse…" };
        browse.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowInstance!.Hwnd);
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null) { s.SaveDirectory = folder.Path; savePathBox.Text = folder.Path; SettingsStore.Shared.Save(); }
        };
        var saveStack = new StackPanel { Spacing = 6, Children = { savePathBox, browse } };

        var modePicker = new ComboBox { Header = "Cleanup policy", MinWidth = 220 };
        modePicker.Items.Add(new ComboBoxItem { Content = "Never delete", Tag = "never", IsSelected = s.Mode == "never" });
        modePicker.Items.Add(new ComboBoxItem { Content = "Delete after duration", Tag = "afterDuration", IsSelected = s.Mode == "afterDuration" });
        modePicker.SelectionChanged += (_, _) => { s.Mode = (modePicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "never"; SettingsStore.Shared.Save(); };

        var hours = new Slider { Header = "Retain (hours)", Minimum = 1, Maximum = 168, Value = s.DurationSeconds / 3600.0, MinWidth = 250 };
        hours.ValueChanged += (_, _) => { s.DurationSeconds = hours.Value * 3600.0; SettingsStore.Shared.Save(); };

        var includeSaved = new CheckBox { Content = "Include manually saved files in cleanup", IsChecked = s.IncludeSavedFiles };
        includeSaved.Checked += (_, _) => { s.IncludeSavedFiles = true; SettingsStore.Shared.Save(); };
        includeSaved.Unchecked += (_, _) => { s.IncludeSavedFiles = false; SettingsStore.Shared.Save(); };

        ContentRoot.Children.Add(Section("Storage & Cache",
            Card(saveStack),
            Card(new StackPanel { Spacing = 8, Children = { modePicker, hours, includeSaved } })));
    }

    // ===== Buffer =====
    private void BuildBuffer()
    {
        var s = SettingsStore.Shared.Settings.Queue;
        var toggle = new ToggleSwitch { Header = "Show buffer panel in editor", IsOn = s.PanelEnabled };
        toggle.Toggled += (_, _) => { s.PanelEnabled = toggle.IsOn; SettingsStore.Shared.Save(); };
        var hint = new TextBlock
        {
            Text = "Keeps every screenshot you take in a vertical carousel on the left side of the editor. Click any item to switch, hover for preview-with-watermark and remove-from-buffer actions. The buffer lives only for the current session and is wiped on app restart.",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        var clearBtn = new Button { Content = "Clear buffer", IsEnabled = ShotQueueStore.Shared.Items.Count > 0 };
        clearBtn.Click += (_, _) => { ShotQueueStore.Shared.ClearAll(); clearBtn.IsEnabled = false; };

        ContentRoot.Children.Add(Section("Shot Buffer",
            Card(new StackPanel { Spacing = 8, Children = { toggle, hint } }),
            Card(new StackPanel { Spacing = 6, Children = { new TextBlock { Text = $"Buffer contains {ShotQueueStore.Shared.Items.Count} item(s).", FontSize = 12 }, clearBtn } })));
    }

    // ===== About =====
    private void BuildAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";

        var box = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        box.Children.Add(new FontIcon { Glyph = "", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"), FontSize = 54, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] });
        box.Children.Add(new TextBlock { Text = "QPARK Shot", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        box.Children.Add(new TextBlock { Text = $"Version {version}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        box.Children.Add(new HyperlinkButton { Content = "QPARK.IO", NavigateUri = new Uri("https://qpark.io"), HorizontalAlignment = HorizontalAlignment.Center });
        box.Children.Add(new TextBlock { Text = "A professional screenshots workspace utility.", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap });

        ContentRoot.Children.Add(Section("About QPARK Shot", Card(box)));
    }
}
