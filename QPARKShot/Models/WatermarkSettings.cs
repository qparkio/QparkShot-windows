using QPARKShot.Services;

namespace QPARKShot.Models;

/// <summary>
/// Immutable snapshot of watermark settings at render time
/// (so background rendering doesn't race with UI edits).
/// </summary>
public sealed record WatermarkSettings(
    string LayoutMode,
    double Spacing,
    string TilePattern,
    double TileRandomness,
    bool TextEnabled,
    string Text,
    string TextColorHex,
    bool LogoEnabled,
    string LogoPath,
    double LogoSize,
    double Opacity,
    string PositionMode
)
{
    public static WatermarkSettings FromStore(SettingsStore store)
    {
        var s = store.Settings;
        return new WatermarkSettings(
            LayoutMode: s.Watermark.LayoutMode,
            Spacing: s.Watermark.Spacing,
            TilePattern: s.Watermark.TilePattern,
            TileRandomness: s.Watermark.TileRandomness,
            TextEnabled: s.Watermark.Text.Enabled,
            Text: s.Watermark.Text.Text,
            TextColorHex: s.Watermark.Text.Color,
            LogoEnabled: s.Watermark.Logo.Enabled,
            LogoPath: s.Watermark.Logo.Path,
            LogoSize: s.Watermark.Logo.Size,
            Opacity: s.Watermark.Logo.Opacity,
            PositionMode: s.Watermark.Logo.PositionMode
        );
    }

    public bool HasAnyWatermark => TextEnabled || LogoEnabled;
}
