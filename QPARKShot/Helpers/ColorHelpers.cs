using System;
using System.Drawing;
using WinUIColor = Windows.UI.Color;

namespace QPARKShot.Helpers;

public static class ColorHelpers
{
    /// <summary>"#RRGGBB" or "#AARRGGBB" → GDI+ Color. Falls back to White on parse failure.</summary>
    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.White;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];

        try
        {
            if (hex.Length == 6)
            {
                int rgb = Convert.ToInt32(hex, 16);
                return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            }
            if (hex.Length == 8)
            {
                long argb = Convert.ToInt64(hex, 16);
                return Color.FromArgb(
                    (int)((argb >> 24) & 0xFF),
                    (int)((argb >> 16) & 0xFF),
                    (int)((argb >> 8) & 0xFF),
                    (int)(argb & 0xFF));
            }
        }
        catch { }
        return Color.White;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static WinUIColor ToWinUI(Color c) => WinUIColor.FromArgb(c.A, c.R, c.G, c.B);
    public static Color FromWinUI(WinUIColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);
}
