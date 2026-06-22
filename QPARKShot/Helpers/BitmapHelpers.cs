using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace QPARKShot.Helpers;

public static class BitmapHelpers
{
    /// <summary>Mirror of macOS <c>loadImageForRendering</c>.</summary>
    public static Bitmap? LoadBitmap(string path)
    {
        try
        {
            // Decode without locking the file.
            using var raw = new Bitmap(path);
            return new Bitmap(raw);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Mirror of macOS <c>makeThumbnailImage</c>. Returns a downscaled clone.</summary>
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

    /// <summary>Save Bitmap as PNG.</summary>
    public static bool SavePng(Bitmap bitmap, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bitmap.Save(path, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Encode bitmap to PNG bytes (for clipboard / share streams).</summary>
    public static byte[] ToPngBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>Convert GDI+ <see cref="Bitmap"/> to WinUI <see cref="BitmapImage"/>.</summary>
    public static async System.Threading.Tasks.Task<BitmapImage> ToBitmapImageAsync(Bitmap bitmap)
    {
        var bytes = ToPngBytes(bitmap);
        var image = new BitmapImage();
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using (var writer = new Windows.Storage.Streams.DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        await image.SetSourceAsync(stream);
        return image;
    }
}
