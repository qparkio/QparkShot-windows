using System.Collections.Generic;

namespace QPARKShot.Models;

/// <summary>
/// POCO mirror of the macOS UserDefaults JSON blob under key
/// "flutter.qpark_shot.app_settings.v1". Persisted to %APPDATA%\QPARK Shot\settings.json.
/// </summary>
public sealed class AppSettings
{
    public string ThemePreference { get; set; } = "system";   // system | light | dark
    public HotkeyConfig Hotkey { get; set; } = new() { Enabled = true, Key = "C", Modifiers = new() { "control", "shift" } };
    public HotkeyConfig FullScreenHotkey { get; set; } = new() { Enabled = false, Key = "F", Modifiers = new() { "control", "shift" } };
    public WatermarkConfig Watermark { get; set; } = new();
    public CleanupConfig Cleanup { get; set; } = new();
    public QueueConfig Queue { get; set; } = new();
    public CaptureConfig Capture { get; set; } = new();
    public ExportConfig Export { get; set; } = new();
    public GalleryConfig Gallery { get; set; } = new();
    public LocalizationConfig Localization { get; set; } = new();
}

public sealed class HotkeyConfig
{
    public bool Enabled { get; set; }
    public string Key { get; set; } = "";
    public List<string> Modifiers { get; set; } = new();
}

public sealed class WatermarkConfig
{
    public string LayoutMode { get; set; } = "single";       // single | tiled
    public double Spacing { get; set; } = 150.0;
    public string TilePattern { get; set; } = "aligned";     // aligned | brick | random
    public double TileRandomness { get; set; } = 0.45;
    public WatermarkTextConfig Text { get; set; } = new();
    public WatermarkLogoConfig Logo { get; set; } = new();
}

public sealed class WatermarkTextConfig
{
    public bool Enabled { get; set; }
    public string Text { get; set; } = "QPARK Shot";
    public string Color { get; set; } = "#FFFFFF";
}

public sealed class WatermarkLogoConfig
{
    public bool Enabled { get; set; }
    public string Path { get; set; } = "";
    public double Size { get; set; } = 120.0;
    public double Opacity { get; set; } = 0.5;
    public string PositionMode { get; set; } = "bottomRight"; // bottomRight | bottomLeft | topRight | topLeft | center
}

public sealed class CleanupConfig
{
    public string Mode { get; set; } = "never";              // never | afterDuration
    public bool IncludeSavedFiles { get; set; } = false;
    public double DurationSeconds { get; set; } = 24 * 3600.0;
    public string SaveDirectory { get; set; } = "";
}

public sealed class QueueConfig
{
    public bool PanelEnabled { get; set; } = true;
}

public sealed class CaptureConfig
{
    public string Mode { get; set; } = "selection";          // selection | fullScreen
    public System.Drawing.Rectangle? LastRegion { get; set; }
    public int DelaySeconds { get; set; } = 0;                // 0, 3, 5, 10
}

public sealed class ExportConfig
{
    public string SelectedPresetID { get; set; } = "watermarked";
    public string FilenameTemplate { get; set; } = "{date}_{time}_{preset}";
    public string DefaultQuickAction { get; set; } = "edit";
}
public sealed class GalleryConfig
{
    public bool OcrEnabled { get; set; } = true;
    public bool SearchIndexEnabled { get; set; } = true;
}
public sealed class LocalizationConfig
{
    public string AppLanguageCode { get; set; } = LocalLanguage();
    public List<string> TextRecognitionLanguageCodes { get; set; } = new() { LocalLanguage(), "en" };
    private static string LocalLanguage() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "zh" => "zh-Hans", "pt" => "pt-BR", var code => code
    };
}
