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
using QPARKShot.Helpers;
using QPARKShot.Localization;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using WinFormsFolderDialog = System.Windows.Forms.FolderBrowserDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

namespace QPARKShot.Views;

public partial class SettingsPage : UserControl
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
                "about" => 7,
                "export" => 5,
                "index" => 6,
                _ => 0,
            };
            TabList.SelectedIndex = idx;
        };
    }

    private void OnBack(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private void OnTabChanged(object sender, SelectionChangedEventArgs e) => BuildTab();

    private void BuildTab()
    {
        if (ContentRoot == null) return;
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
            case "export": BuildExport(); break;
            case "index": BuildIndex(); break;
        }
    }

    // ===== Building blocks =====

    private static TextBlock Header(string text) => new()
    {
        Text = L.T(text),
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
        Text = L.T(text),
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Margin = new Thickness(0, 0, 0, 6),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Hint(string text)
    {
        var tb = new TextBlock
        {
            Text = L.T(text),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        return tb;
    }

    // ===== Appearance =====
    private void BuildAppearance()
    {
        var s = SettingsStore.Shared.Settings;
        var picker = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
        picker.Items.Add(new ComboBoxItem { Content = L.T("settings.theme_system"), Tag = "system", IsSelected = s.ThemePreference == "system" });
        picker.Items.Add(new ComboBoxItem { Content = L.T("settings.theme_light"),  Tag = "light",  IsSelected = s.ThemePreference == "light" });
        picker.Items.Add(new ComboBoxItem { Content = L.T("settings.theme_dark"),   Tag = "dark",   IsSelected = s.ThemePreference == "dark" });
        picker.SelectionChanged += (_, _) =>
        {
            var tag = (picker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "system";
            SettingsStore.Shared.Mutate(x => x.ThemePreference = tag);
        };

        var language = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var code in L.Languages)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(code);
            language.Items.Add(new ComboBoxItem { Content = culture.NativeName, Tag = code, IsSelected = s.Localization.AppLanguageCode == code });
        }
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedItem is not ComboBoxItem item) return;
            s.Localization.AppLanguageCode = item.Tag.ToString()!; SettingsStore.Shared.Save(); L.Shared.Refresh();
            if (Window.GetWindow(this) is { } window) window.FlowDirection = L.Shared.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Dispatcher.BeginInvoke(BuildTab);
        };
        ContentRoot.Children.Add(Header(L.T("settings.general")));
        ContentRoot.Children.Add(Card(V(Label(L.T("settings.language")), language)));
        ContentRoot.Children.Add(Card(V(
            Label(L.T("settings.appearance"), weight: FontWeights.SemiBold),
            picker,
            Hint("System follows the current Windows theme.")
        )));
    }

    // ===== Hotkeys =====
    private void BuildHotkeys()
    {
        ContentRoot.Children.Add(Header(L.T("settings.capture")));
        ContentRoot.Children.Add(HotkeyCard(L.T("settings.selection_shortcut"), SettingsStore.Shared.Settings.Hotkey, "Default: Ctrl + Shift + C"));
        ContentRoot.Children.Add(HotkeyCard(L.T("settings.fullscreen_shortcut"), SettingsStore.Shared.Settings.FullScreenHotkey, "Off by default"));
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

        var error = Hint("");
        void Persist()
        {
            var enabled = toggle.IsChecked ?? false;
            var mods = new List<string>();
            if (ctrl.IsChecked ?? false) mods.Add("control");
            if (shift.IsChecked ?? false) mods.Add("shift");
            if (alt.IsChecked ?? false) mods.Add("option");
            if (win.IsChecked ?? false) mods.Add("command");
            var proposed = new HotkeyConfig { Enabled = enabled, Key = key.Text.Trim().ToUpperInvariant(), Modifiers = mods };
            var other = ReferenceEquals(cfg, SettingsStore.Shared.Settings.Hotkey) ? SettingsStore.Shared.Settings.FullScreenHotkey : SettingsStore.Shared.Settings.Hotkey;
            var validation = HotkeyService.Validate(proposed, other);
            error.Text = validation == null ? "" : L.T(validation);
            if (validation != null) return;
            cfg.Enabled = enabled; cfg.Modifiers = mods; cfg.Key = proposed.Key;
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
        content.Children.Add(error);
        return Card(content);
    }

    private Border CaptureModeCard()
    {
        var s = SettingsStore.Shared.Settings;
        var modePicker = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        modePicker.Items.Add(new ComboBoxItem { Content = "Selection", Tag = "selection", IsSelected = s.Capture.Mode == "selection" });
        modePicker.Items.Add(new ComboBoxItem { Content = L.T("capture.full_screen"), Tag = "fullScreen", IsSelected = s.Capture.Mode == "fullScreen" });
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
        ContentRoot.Children.Add(Header(L.T("settings.watermark")));

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
        var txtToggle = new CheckBox { Content = L.T("settings.watermark_text"), IsChecked = s.Text.Enabled, FontWeight = FontWeights.SemiBold };
        var txtField = new TextBox { Text = s.Text.Text, Width = 200, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = s.Text.Enabled, Margin = new Thickness(0, 6, 8, 0) };
        
        var colorRect = new System.Windows.Shapes.Rectangle
        {
            Width = 24, Height = 24,
            Stroke = System.Windows.Media.Brushes.Gray,
            StrokeThickness = 1,
            RadiusX = 4, RadiusY = 4,
            Cursor = System.Windows.Input.Cursors.Hand,
            IsEnabled = s.Text.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
        };
        try
        {
            colorRect.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.Text.Color));
        }
        catch
        {
            colorRect.Fill = System.Windows.Media.Brushes.White;
        }

        colorRect.MouseLeftButtonUp += (sender, args) =>
        {
            if (!s.Text.Enabled) return;
            var dlg = new WinFormsColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
            };
            if (colorRect.Fill is SolidColorBrush currentBrush)
            {
                dlg.Color = System.Drawing.Color.FromArgb(currentBrush.Color.A, currentBrush.Color.R, currentBrush.Color.G, currentBrush.Color.B);
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                colorRect.Fill = new SolidColorBrush(c);
                s.Text.Color = ColorHelpers.ToHex(c);
                SettingsStore.Shared.Save();
            }
        };

        txtToggle.Checked += (_, _) => { s.Text.Enabled = true; txtField.IsEnabled = true; colorRect.IsEnabled = true; SettingsStore.Shared.Save(); };
        txtToggle.Unchecked += (_, _) => { s.Text.Enabled = false; txtField.IsEnabled = false; colorRect.IsEnabled = false; SettingsStore.Shared.Save(); };
        txtField.TextChanged += (_, _) => { s.Text.Text = txtField.Text; SettingsStore.Shared.Save(); };

        var textWatermarkRow = H(txtField, colorRect);
        left.Children.Add(Card(V(txtToggle, textWatermarkRow)));

        // Logo watermark
        var logoToggle = new CheckBox { Content = L.T("settings.watermark_logo"), IsChecked = s.Logo.Enabled, FontWeight = FontWeights.SemiBold };
        var logoPathBox = new TextBox { Text = s.Logo.Path, IsReadOnly = true, Width = 200, Margin = new Thickness(0, 0, 8, 0) };
        var browseBtn = new Button { Content = L.T("settings.choose_folder"), Padding = new Thickness(10, 4, 10, 4) };
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
                (L.T("settings.position.bottom_right"), "bottomRight"), (L.T("settings.position.bottom_left"), "bottomLeft"),
                (L.T("settings.position.top_right"), "topRight"),       (L.T("settings.position.top_left"), "topLeft"),
                (L.T("settings.position.center"), "center"),
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
            foreach (var (label, tag) in new[] { (L.T("settings.tile.aligned"), "aligned"), (L.T("settings.tile.brick"), "brick"), ("Chaos", "random") })
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
            Label(L.T("settings.opacity"), weight: FontWeights.SemiBold), opacity,
            new Border { Height = 8 },
            Label(L.T("settings.size"), weight: FontWeights.SemiBold), size
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
        ContentRoot.Children.Add(Header(L.T("settings.storage")));

        var savePath = new TextBox
        {
            Text = string.IsNullOrEmpty(s.SaveDirectory) ? "Default (Pictures\\QPARK Shot)" : s.SaveDirectory,
            IsReadOnly = true,
            Width = 320,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var browse = new Button { Content = L.T("settings.choose_folder"), Padding = new Thickness(10, 4, 10, 4) };
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
        modePicker.Items.Add(new ComboBoxItem { Content = L.T("settings.cleanup_never"), Tag = "never", IsSelected = s.Mode == "never" });
        modePicker.Items.Add(new ComboBoxItem { Content = L.T("settings.cleanup_duration"), Tag = "afterDuration", IsSelected = s.Mode == "afterDuration" });
        modePicker.SelectionChanged += (_, _) =>
        {
            s.Mode = (modePicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "never";
            SettingsStore.Shared.Save();
        };

        var hours = new Slider { Minimum = 1, Maximum = 168, Value = s.DurationSeconds / 3600.0, Width = 280 };
        hours.ValueChanged += (_, _) => { s.DurationSeconds = hours.Value * 3600.0; SettingsStore.Shared.Save(); };

        var include = new CheckBox { Content = L.T("settings.cleanup_saved"), IsChecked = s.IncludeSavedFiles, Margin = new Thickness(0, 6, 0, 0) };
        include.Checked += (_, _) => { s.IncludeSavedFiles = true; SettingsStore.Shared.Save(); };
        include.Unchecked += (_, _) => { s.IncludeSavedFiles = false; SettingsStore.Shared.Save(); };

        ContentRoot.Children.Add(Card(V(
            Label(L.T("settings.save_location"), weight: FontWeights.SemiBold),
            H(savePath, browse)
        )));
        ContentRoot.Children.Add(Card(V(
            Label(L.T("settings.cleanup"), weight: FontWeights.SemiBold),
            modePicker,
            new Border { Height = 8 },
            Label(L.T("settings.cleanup_age"), weight: FontWeights.SemiBold),
            hours, include
        )));
    }

    // ===== Buffer =====
    private void BuildBuffer()
    {
        var s = SettingsStore.Shared.Settings.Queue;
        ContentRoot.Children.Add(Header(L.T("workspace.current_session")));

        var toggle = new CheckBox { Content = L.T("settings.queue_panel"), IsChecked = s.PanelEnabled, FontWeight = FontWeights.SemiBold };
        toggle.Checked += (_, _) => { s.PanelEnabled = true; SettingsStore.Shared.Save(); };
        toggle.Unchecked += (_, _) => { s.PanelEnabled = false; SettingsStore.Shared.Save(); };
        var hint = Hint(
            "Keeps every screenshot you take in a vertical carousel on the left side of the editor. " +
            "Click any item to switch, hover for preview-with-watermark and remove actions. " +
            "The buffer lives only for the current session and is wiped on app restart.");

        var clearBtn = new Button { Content = L.T("workspace.clear_session"), IsEnabled = ShotQueueStore.Shared.Items.Count > 0, Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
        clearBtn.Click += (_, _) => { if (App.MainWindowInstance?.ConfirmClearSession() == true) clearBtn.IsEnabled = false; };

        ContentRoot.Children.Add(Card(V(toggle, hint)));
        ContentRoot.Children.Add(Card(V(
            Label($"Buffer contains {ShotQueueStore.Shared.Items.Count} item(s)."),
            clearBtn
        )));
    }

    private void BuildExport()
    {
        var settings = SettingsStore.Shared.Settings.Export;
        ContentRoot.Children.Add(Header(L.T("settings.export")));
        var preset = Picker(new[] { ("clean", "export.preset.clean"), ("watermarked", "export.preset.watermarked"), ("support", "export.preset.support") }, settings.SelectedPresetID, value => settings.SelectedPresetID = value);
        var action = Picker(new[] { ("edit", "settings.open_editor"), ("overlay", "settings.show_review") }, settings.DefaultQuickAction, value => settings.DefaultQuickAction = value);
        var template = new TextBox { Text = settings.FilenameTemplate, MinWidth = 260 };
        template.LostKeyboardFocus += (_, _) => { settings.FilenameTemplate = template.Text; SettingsStore.Shared.Save(); };
        ContentRoot.Children.Add(Card(V(Label(L.T("settings.after_capture")), action)));
        ContentRoot.Children.Add(Card(V(Label(L.T("settings.export_preset")), preset)));
        ContentRoot.Children.Add(Card(V(Label(L.T("settings.filename_template")), template, Hint("{date} · {time} · {preset} · {uuid}"))));
    }
    private static ComboBox Picker((string Value, string Key)[] values, string selected, Action<string> changed)
    {
        var picker = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (value, key) in values) picker.Items.Add(new ComboBoxItem { Content = L.T(key), Tag = value, IsSelected = value == selected });
        picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is ComboBoxItem item) { changed(item.Tag.ToString()!); SettingsStore.Shared.Save(); } };
        return picker;
    }
    private void BuildIndex()
    {
        var settings = SettingsStore.Shared.Settings;
        ContentRoot.Children.Add(Header(L.T("settings.index")));
        var enabled = new CheckBox { Content = L.T("settings.enable_ocr"), IsChecked = settings.Gallery.OcrEnabled };
        enabled.Click += (_, _) => { settings.Gallery.OcrEnabled = enabled.IsChecked == true; SettingsStore.Shared.Save(); WorkspaceStore.Shared.StartOcr(); };
        var searchable = new CheckBox { Content = L.T("settings.search_metadata"), IsChecked = settings.Gallery.SearchIndexEnabled, Margin = new Thickness(0, 10, 0, 0) };
        searchable.Click += (_, _) => { settings.Gallery.SearchIndexEnabled = searchable.IsChecked == true; SettingsStore.Shared.Save(); };
        ContentRoot.Children.Add(Card(V(enabled, searchable)));
        ContentRoot.Children.Add(Label(L.T("settings.ocr_languages")));
        if (!OcrService.HasPackageIdentity) ContentRoot.Children.Add(Hint(L.T("windows.ocr_msix")));
        foreach (var code in L.Languages)
        {
            var check = new CheckBox { Content = System.Globalization.CultureInfo.GetCultureInfo(code).NativeName,
                IsChecked = settings.Localization.TextRecognitionLanguageCodes.Contains(code), Margin = new Thickness(0, 5, 0, 5) };
            check.Click += (_, _) =>
            {
                if (check.IsChecked == true) settings.Localization.TextRecognitionLanguageCodes.Add(code);
                else settings.Localization.TextRecognitionLanguageCodes.RemoveAll(value => value == code);
                settings.Localization.TextRecognitionLanguageCodes = settings.Localization.TextRecognitionLanguageCodes.Distinct().ToList();
                SettingsStore.Shared.Save(); WorkspaceStore.Shared.StartOcr();
            };
            ContentRoot.Children.Add(check);
        }
    }

    // ===== About =====
    private void BuildAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
        ContentRoot.Children.Add(Header(L.T("menu.about")));

        var box = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        box.Children.Add(new TextBlock
        {
            Text = "QPARK Shot", FontSize = 24, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var versionLabel = new TextBlock
        {
            Text = $"Version {version}", FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 12),
        };
        versionLabel.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        box.Children.Add(versionLabel);

        var link = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        var hyperlink = new Hyperlink(new Run("QPARK.IO")) { NavigateUri = new Uri("https://qpark.io") };
        hyperlink.RequestNavigate += (_, e) => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true }); };
        link.Inlines.Add(hyperlink);
        box.Children.Add(link);

        var descLabel = new TextBlock
        {
            Text = "Professional screenshots workspace utility.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        descLabel.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        box.Children.Add(descLabel);

        var copyLabel = new TextBlock
        {
            Text = "Copyright © 2026 QPARK. All rights reserved.",
            FontSize = 10,
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        copyLabel.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        box.Children.Add(copyLabel);

        ContentRoot.Children.Add(Card(box));
    }
}
