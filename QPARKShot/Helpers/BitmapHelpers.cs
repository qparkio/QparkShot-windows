using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace QPARKShot.Helpers;

public static class BitmapHelpers
{
    public static Bitmap? LoadBitmap(string path)
    {
        try
        {
            using var raw = new Bitmap(path);
            return new Bitmap(raw);
        }
        catch { return null; }
    }

    public static Bitmap? Thumbnail(string path, int maxPixelSize)
    {
        var src = LoadBitmap(path);
        if (src == null) return null;
        try
        {
            int w = src.Width;
            int h = src.Height;
            double scale = (double)maxPixelSize / Math.Max(w, h);
            if (scale >= 1.0) return src;
            int tw = Math.Max(1, (int)(w * scale));
            int th = Math.Max(1, (int)(h * scale));
            var thumb = new Bitmap(tw, th, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(thumb))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, tw, th);
            }
            return thumb;
        }
        finally
        {
            src.Dispose();
        }
    }

    public static bool SavePng(Bitmap bitmap, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bitmap.Save(path, ImageFormat.Png);
            return true;
        }
        catch { return false; }
    }

    public static byte[] ToPngBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>Convert GDI+ Bitmap → WPF BitmapSource (frozen, safe to use on UI thread).</summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
