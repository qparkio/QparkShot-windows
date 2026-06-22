using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using QPARKShot.Models;
using QPARKShot.Services;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using WinFormsFolderDialog = System.Windows.Forms.FolderBrowserDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

namespace QPARKShot.Views;

public partial class SettingsPage : Page
{
    public SettingsPage(string initialTab = "appearance")
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var idx = initialTab switch
            {
                "appearance" => 0,
                "hotkeys" => 1,
                "watermark" => 2,
                "storage" => 3,
                "buffer" => 4,
                "about" => 5,
                _ => 0,
            };
            TabList.SelectedIndex = idx;
        };
    }

    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.ShowGallery();

    private void OnTabChanged(object sender, SelectionChangedEventArgs e) => BuildTab();

    private void BuildTab()
    {
        ContentRoot.Children.Clear();
        var tag = (TabList.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "appearance";
        switch (tag)
        {
            case "appearance": BuildAppearance(); break;
            case "hotkeys": BuildHotkeys(); break;
            case "watermark": BuildWatermark(); break;
            case "storage": BuildStorage(); break;
            case "buffer": BuildBuffer(); break;
            case "about": BuildAbout(); break;
        }
    }

    // ===== Building blocks =====

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 12),
    };

    private static Border Card(UIElement content) => new()
    {
        Style = (Style)Application.Current.Resources["SettingsCardStyle"],
        Margin = new Thickness(0, 0, 0, 12),
        Child = content,
    };

    private static StackPanel V(params UIElement[] kids)
    {
        var sp = new StackPanel();
        foreach (var k in kids) sp.Children.Add(k);
        return sp;
    }

    private static StackPanel H(params UIElement[] kids)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var k in kids) sp.Children.Add(k);
        return sp;
    }

    private static TextBlock Label(string text, double size = 12, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Margin = new Thickness(0, 0, 0, 6),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = (Brush)Application.Current.Resources["SecondaryText"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
    };

    // ===== Appearance =====
    private void BuildAppearance()
    {
        var s = SettingsStore.Shared.Settings;
        var picker = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
        picker.Items.Add(new ComboBoxItem { Content = "System", Tag = "system", IsSelected = s.ThemePreference == "system" });
        picker.Items.Add(new ComboBoxItem { Content = "Light",  Tag = "light",  IsSelected = s.ThemePreference == "light" });
        picker.Items.Add(new ComboBoxItem { Content = "Dark",   Tag = "dark",   IsSelected = s.ThemePreference == "dark" });
        picker.SelectionChanged += (_, _) =>
        {
            var tag = (picker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "system";
            SettingsStore.Shared.Mutate(x => x.ThemePreference = tag);
        };

        ContentRoot.Children.Add(Header("Appearance"));
        ContentRoot.Children.Add(Card(V(
            Label("Theme", weight: FontWeights.SemiBold),
            picker,
            Hint("System follows the current Windows theme.")
        )));
    }

    // ===== Hotkeys =====
    private void BuildHotkeys()
    {
        ContentRoot.Children.Add(Header("Hotkeys"));
        ContentRoot.Children.Add(HotkeyCard("Selection Capture", SettingsStore.Shared.Settings.Hotkey, "Default: Ctrl + Shift + C"));
        ContentRoot.Children.Add(HotkeyCard("Full-Screen Capture", SettingsStore.Shared.Settings.FullScreenHotkey, "Off by default"));
        ContentRoot.Children.Add(CaptureModeCard());
    }

    private Border HotkeyCard(string title, HotkeyConfig cfg, string subtitle)
    {
        var toggle = new CheckBox { Content = title, IsChecked = cfg.Enabled, FontWeight = FontWeights.SemiBold };
        var ctrl  = new CheckBox { Content = "Ctrl",  IsChecked = cfg.Modifiers.Contains("control"), Margin = new Thickness(0, 0, 10, 0) };
        var shift = new CheckBox { Content = "Shift", IsChecked = cfg.Modifiers.Contains("shift"),   Margin = new Thickness(0, 0, 10, 0) };
        var alt   = new CheckBox { Content = "Alt",   IsChecked = cfg.Modifiers.Contains("option") || cfg.Modifiers.Contains("alt"), Margin = new Thickness(0, 0, 10, 0) };
        var win   = new CheckBox { Content = "Win",   IsChecked = cfg.Modifiers.Contains("command") || cfg.Modifiers.Contains("win"), Margin = new Thickness(0, 0, 10, 0) };
        var key   = new TextBox { Text = cfg.Key, MaxLength = 1, Width = 40, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };

        void Persist()
        {
            cfg.Enabled = toggle.IsChecked ?? false;
            var mods = new List<string>();
            if (ctrl.IsChecked ?? false) mods.Add("control");
            if (shift.IsChecked ?? false) mods.Add("shift");
            if (alt.IsChecked ?? false) mods.Add("option");
            if (win.IsChecked ?? false) mods.Add("command");
            cfg.Modifiers = mods;
            cfg.Key = (key.Text ?? "").Trim().ToUpperInvariant();
            if (cfg.Key.Length > 1) cfg.Key = cfg.Key.Substring(0, 1);
            SettingsStore.Shared.Save();
        }
        toggle.Checked += (_, _) => Persist(); toggle.Unchecked += (_, _) => Persist();
        ctrl.Checked  += (_, _) => Persist(); ctrl.Unchecked  += (_, _) => Persist();
        shift.Checked += (_, _) => Persist(); shift.Unchecked += (_, _) => Persist();
        alt.Checked   += (_, _) => Persist(); alt.Unchecked   += (_, _) => Persist();
        win.Checked   += (_, _) => Persist(); win.Unchecked   += (_, _) => Persist();
        key.TextChanged += (_, _) => Persist();

        var content = V(toggle, Hint(subtitle));
        var detail = H(ctrl, shift, alt, win, Label("Key: ", size: 11), key);
        detail.Margin = new Thickness(0, 8, 0, 0);
        content.Children.Add(detail);
        return Card(content);
    }

    private Border CaptureModeCard()
    {
        var s = SettingsStore.Shared.Settings;
        var modePicker = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        modePicker.Items.Add(new ComboBoxItem { Content = "Selection", Tag = "selection", IsSelected = s.Capture.Mode == "selection" });
        modePicker.Items.Add(new ComboBoxItem { Content = "Full Screen", Tag = "fullScreen", IsSelected = s.Capture.Mode == "fullScreen" });
        modePicker.SelectionChanged += (_, _) =>
        {
            s.Capture.Mode = (modePicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "selection";
            SettingsStore.Shared.Save();
        };

        var delayPicker = new ComboBox { Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (label, sec) in new[] { ("None", 0), ("3 s", 3), ("5 s", 5), ("10 s", 10) })
        {
            delayPicker.Items.Add(new ComboBoxItem { Content = label, Tag = sec, IsSelected = s.Capture.DelaySeconds == sec });
        }
        delayPicker.SelectionChanged += (_, _) =>
        {
            s.Capture.DelaySeconds = (int)((delayPicker.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            SettingsStore.Shared.Save();
        };

        return Card(V(
            Label("Default Capture Mode", weight: FontWeights.SemiBold),
            modePicker,
            new Border { Height = 8 },
            Label("Default Delay", weight: FontWeights.SemiBold),
            delayPicker,
            Hint("The tray menu always offers one-shot delay options regardless of this setting.")
        ));
    }

    // ===== Watermark =====
    private void BuildWatermark()
    {
        var s = SettingsStore.Shared.Settings.Watermark;
        ContentRoot.Children.Add(Header("Watermark"));

        var twoCol = new Grid();
        twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        Grid.SetColumn(left, 0);

        // Layout mode
        var layoutPicker = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
        layoutPicker.Items.Add(new ComboBoxItem { Content = "Single Position", Tag = "single", IsSelected = s.LayoutMode == "single" });
        layoutPicker.Items.Add(new ComboBoxItem { Content = "Tiled (Diagonal)", Tag = "tiled", IsSelected = s.LayoutMode == "tiled" });
        layoutPicker.SelectionChanged += (_, _) =>
        {
            s.LayoutMode = (layoutPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "single";
            SettingsStore.Shared.Save();
            BuildTab();
        };
        left.Children.Add(Card(V(Label("Layout", weight: FontWeights.SemiBold), layoutPicker)));

        // Text watermark
        var txtToggle = new CheckBox { Content = "Text watermark", IsChecked = s.Text.Enabled, FontWeight = FontWeights.SemiBold };
        var txtField = new TextBox { Text = s.Text.Text, Width = 240, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = s.Text.Enabled, Margin = new Thickness(0, 6, 0, 0) };
        txtToggle.Checked += (_, _) => { s.Text.Enabled = true; txtField.IsEnabled = true; SettingsStore.Shared.Save(); };
        txtToggle.Unchecked += (_, _) => { s.Text.Enabled = false; txtField.IsEnabled = false; SettingsStore.Shared.Save(); };
        txtField.TextChanged += (_, _) => { s.Text.Text = txtField.Text; SettingsStore.Shared.Save(); };
        left.Children.Add(Card(V(txtToggle, txtField)));

        // Logo watermark
        var logoToggle = new CheckBox { Content = "Logo watermark", IsChecked = s.Logo.Enabled, FontWeight = FontWeights.SemiBold };
        var logoPathBox = new TextBox { Text = s.Logo.Path, IsReadOnly = true, Width = 200, Margin = new Thickness(0, 0, 8, 0) };
        var browseBtn = new Button { Content = "Browse…", Padding = new Thickness(10, 4, 10, 4) };
        browseBtn.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                s.Logo.Path = dlg.FileName;
                logoPathBox.Text = dlg.FileName;
                SettingsStore.Shared.Save();
            }
        };
        logoToggle.Checked += (_, _) => { s.Logo.Enabled = true; SettingsStore.Shared.Save(); };
        logoToggle.Unchecked += (_, _) => { s.Logo.Enabled = false; SettingsStore.Shared.Save(); };
        var logoRow = H(logoPathBox, browseBtn);
        logoRow.Margin = new Thickness(0, 6, 0, 0);
        left.Children.Add(Card(V(logoToggle, logoRow)));

        // Position (single mode)
        if (s.LayoutMode == "single")
        {
            var posPicker = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (label, tag) in new[] {
                ("Bottom Right", "bottomRight"), ("Bottom Left", "bottomLeft"),
                ("Top Right", "topRight"),       ("Top Left", "topLeft"),
                ("Center", "center"),
            })
            {
                posPicker.Items.Add(new ComboBoxItem { Content = label, Tag = tag, IsSelected = s.Logo.PositionMode == tag });
            }
            posPicker.SelectionChanged += (_, _) =>
            {
                s.Logo.PositionMode = (posPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bottomRight";
                SettingsStore.Shared.Save();
            };
            left.Children.Add(Card(V(Label("Position", weight: FontWeights.SemiBold), posPicker)));
        }

        // Tiled options
        if (s.LayoutMode == "tiled")
        {
            var patternPicker = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (label, tag) in new[] { ("Aligned", "aligned"), ("Brick", "brick"), ("Chaos", "random") })
            {
                patternPicker.Items.Add(new ComboBoxItem { Content = label, Tag = tag, IsSelected = s.TilePattern == tag });
            }
            patternPicker.SelectionChanged += (_, _) =>
            {
                s.TilePattern = (patternPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "aligned";
                SettingsStore.Shared.Save();
            };

            var spacing = new Slider { Minimum = 80, Maximum = 400, Value = s.Spacing, Width = 240 };
            spacing.ValueChanged += (_, _) => { s.Spacing = spacing.Value; SettingsStore.Shared.Save(); };

            left.Children.Add(Card(V(
                Label("Pattern", weight: FontWeights.SemiBold), patternPicker,
                new Border { Height = 8 },
                Label("Spacing", weight: FontWeights.SemiBold), spacing
            )));
        }

        // Opacity + size
        var opacity = new Slider { Minimum = 10, Maximum = 100, Value = s.Logo.Opacity * 100, Width = 240 };
        opacity.ValueChanged += (_, _) => { s.Logo.Opacity = opacity.Value / 100.0; SettingsStore.Shared.Save(); };

        var size = new Slider { Minimum = 50, Maximum = 300, Value = s.Logo.Size, Width = 240 };
        size.ValueChanged += (_, _) => { s.Logo.Size = size.Value; SettingsStore.Shared.Save(); };

        left.Children.Add(Card(V(
            Label("Opacity", weight: FontWeights.SemiBold), opacity,
            new Border { Height = 8 },
            Label("Logo size (px)", weight: FontWeights.SemiBold), size
        )));

        twoCol.Children.Add(left);

        var preview = new WatermarkPreview { Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(preview, 1);
        twoCol.Children.Add(preview);

        ContentRoot.Children.Add(twoCol);
    }

    // ===== Storage =====
    private void BuildStorage()
    {
        var s = SettingsStore.Shared.Settings.Cleanup;
        ContentRoot.Children.Add(Header("Storage & Cleanup"));

        var savePath = new TextBox
        {
            Text = string.IsNullOrEmpty(s.SaveDirectory) ? "Default (Pictures\\QPARK Shot)" : s.SaveDirectory,
            IsReadOnly = true,
            Width = 320,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var browse = new Button { Content = "Browse…", Padding = new Thickness(10, 4, 10, 4) };
        browse.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                s.SaveDirectory = dlg.SelectedPath;
                savePath.Text = dlg.SelectedPath;
                SettingsStore.Shared.Save();
            }
        };

        var modePicker = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        modePicker.Items.Add(new ComboBoxItem { Content = "Never delete", Tag = "never", IsSelected = s.Mode == "never" });
        modePicker.Items.Add(new ComboBoxItem { Content = "Delete after duration", Tag = "afterDuration", IsSelected = s.Mode == "afterDuration" });
        modePicker.SelectionChanged += (_, _) =>
        {
            s.Mode = (modePicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "never";
            SettingsStore.Shared.Save();
        };

        var hours = new Slider { Minimum = 1, Maximum = 168, Value = s.DurationSeconds / 3600.0, Width = 280 };
        hours.ValueChanged += (_, _) => { s.DurationSeconds = hours.Value * 3600.0; SettingsStore.Shared.Save(); };

        var include = new CheckBox { Content = "Include manually saved files in cleanup", IsChecked = s.IncludeSavedFiles, Margin = new Thickness(0, 6, 0, 0) };
        include.Checked += (_, _) => { s.IncludeSavedFiles = true; SettingsStore.Shared.Save(); };
        include.Unchecked += (_, _) => { s.IncludeSavedFiles = false; SettingsStore.Shared.Save(); };

        ContentRoot.Children.Add(Card(V(
            Label("Save Folder", weight: FontWeights.SemiBold),
            H(savePath, browse)
        )));
        ContentRoot.Children.Add(Card(V(
            Label("Cleanup Policy", weight: FontWeights.SemiBold),
            modePicker,
            new Border { Height = 8 },
            Label("Retention (hours)", weight: FontWeights.SemiBold),
            hours, include
        )));
    }

    // ===== Buffer =====
    private void BuildBuffer()
    {
        var s = SettingsStore.Shared.Settings.Queue;
        ContentRoot.Children.Add(Header("Shot Buffer"));

        var toggle = new CheckBox { Content = "Show buffer panel in editor", IsChecked = s.PanelEnabled, FontWeight = FontWeights.SemiBold };
        toggle.Checked += (_, _) => { s.PanelEnabled = true; SettingsStore.Shared.Save(); };
        toggle.Unchecked += (_, _) => { s.PanelEnabled = false; SettingsStore.Shared.Save(); };
        var hint = Hint(
            "Keeps every screenshot you take in a vertical carousel on the left side of the editor. " +
            "Click any item to switch, hover for preview-with-watermark and remove actions. " +
            "The buffer lives only for the current session and is wiped on app restart.");

        var clearBtn = new Button { Content = "Clear buffer", IsEnabled = ShotQueueStore.Shared.Items.Count > 0, Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
        clearBtn.Click += (_, _) => { ShotQueueStore.Shared.ClearAll(); clearBtn.IsEnabled = false; };

        ContentRoot.Children.Add(Card(V(toggle, hint)));
        ContentRoot.Children.Add(Card(V(
            Label($"Buffer contains {ShotQueueStore.Shared.Items.Count} item(s)."),
            clearBtn
        )));
    }

    // ===== About =====
    private void BuildAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
        ContentRoot.Children.Add(Header("About QPARK Shot"));

        var box = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        box.Children.Add(new TextBlock
        {
            Text = "QPARK Shot", FontSize = 24, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        box.Children.Add(new TextBlock
        {
            Text = $"Version {version}", FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["SecondaryText"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 12),
        });

        var link = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        var hyperlink = new Hyperlink(new Run("QPARK.IO")) { NavigateUri = new Uri("https://qpark.io") };
        hyperlink.RequestNavigate += (_, e) => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true }); };
        link.Inlines.Add(hyperlink);
        box.Children.Add(link);

        box.Children.Add(new TextBlock
        {
            Text = "Professional screenshots workspace utility.",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["SecondaryText"],
        });
        box.Children.Add(new TextBlock
        {
            Text = "Copyright © 2026 QPARK. All rights reserved.",
            FontSize = 10,
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["SecondaryText"],
        });

        ContentRoot.Children.Add(Card(box));
    }
}
