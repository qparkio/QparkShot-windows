using System;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace QPARKShot.Helpers;

public static class ColorHelpers
{
    /// <summary>"#RRGGBB" or "#AARRGGBB" → GDI+ Color. Falls back to White.</summary>
    public static GdiColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return GdiColor.White;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];

        try
        {
            if (hex.Length == 6)
            {
                int rgb = Convert.ToInt32(hex, 16);
                return GdiColor.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            }
            if (hex.Length == 8)
            {
                long argb = Convert.ToInt64(hex, 16);
                return GdiColor.FromArgb(
                    (int)((argb >> 24) & 0xFF),
                    (int)((argb >> 16) & 0xFF),
                    (int)((argb >> 8) & 0xFF),
                    (int)(argb & 0xFF));
            }
        }
        catch { }
        return GdiColor.White;
    }

    public static string ToHex(GdiColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    public static string ToHex(WpfColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static WpfColor ToWpf(GdiColor c) => WpfColor.FromArgb(c.A, c.R, c.G, c.B);
    public static GdiColor FromWpf(WpfColor c) => GdiColor.FromArgb(c.A, c.R, c.G, c.B);
}
