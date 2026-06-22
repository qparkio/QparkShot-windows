using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using QPARKShot.Helpers;
using QPARKShot.Models;
using Windows.Foundation;

namespace QPARKShot.Services;

/// <summary>
/// GDI+ port of macOS <c>renderAnnotatedImage</c>. Applies annotations, optional crop,
/// and watermark (single / tiled, text and/or logo) onto a source bitmap. Pure: no UI deps.
/// </summary>
public static class WatermarkRenderer
{
    public static Bitmap? Render(
        Bitmap source,
        IReadOnlyList<Annotation>? annotations,
        Rect? cropRect,
        WatermarkSettings watermark)
    {
        if (source == null) return null;

        Bitmap working = (Bitmap)source.Clone();

        // 1. Render annotations onto the bitmap (in image-pixel space).
        if (annotations != null && annotations.Count > 0)
        {
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var a in annotations) DrawAnnotation(g, a);
        }

        // 2. Crop, if requested.
        if (cropRect.HasValue && cropRect.Value.Width > 1 && cropRect.Value.Height > 1)
        {
            var c = cropRect.Value;
            var srcRect = new Rectangle(
                (int)Math.Round(c.X),
                (int)Math.Round(c.Y),
                (int)Math.Round(c.Width),
                (int)Math.Round(c.Height));
            srcRect.Intersect(new Rectangle(0, 0, working.Width, working.Height));
            if (srcRect.Width > 0 && srcRect.Height > 0)
            {
                var cropped = working.Clone(srcRect, working.PixelFormat);
                working.Dispose();
                working = cropped;
            }
        }

        // 3. Watermark.
        if (watermark.HasAnyWatermark)
        {
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            if (watermark.LayoutMode == "tiled")
            {
                DrawTiled(g, working.Width, working.Height, watermark);
            }
            else
            {
                DrawSingle(g, working.Width, working.Height, watermark);
            }
        }

        return working;
    }

    private static void DrawAnnotation(Graphics g, Annotation a)
    {
        var color = ColorHelpers.FromHex(a.ColorHex);
        using var pen = new Pen(color, (float)a.StrokeWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

        switch (a)
        {
            case FreehandAnnotation fh when fh.Points.Count >= 2:
            {
                var pts = new PointF[fh.Points.Count];
                for (int i = 0; i < fh.Points.Count; i++)
                    pts[i] = new PointF((float)fh.Points[i].X, (float)fh.Points[i].Y);
                g.DrawLines(pen, pts);
                break;
            }
            case ArrowAnnotation arrow:
                pen.CustomEndCap = new AdjustableArrowCap(4, 6);
                g.DrawLine(pen,
                    new PointF((float)arrow.Start.X, (float)arrow.Start.Y),
                    new PointF((float)arrow.End.X, (float)arrow.End.Y));
                break;
            case RectangleAnnotation r:
                g.DrawRectangle(pen,
                    (float)r.Rect.X, (float)r.Rect.Y,
                    (float)r.Rect.Width, (float)r.Rect.Height);
                break;
            case TextAnnotation t:
                using (var brush = new SolidBrush(color))
                using (var font = new Font("Segoe UI", (float)t.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    g.DrawString(t.Text, font, brush, (float)t.Position.X, (float)t.Position.Y);
                }
                break;
        }
    }

    private static void DrawSingle(Graphics g, int w, int h, WatermarkSettings ws)
    {
        Bitmap? logo = LoadLogo(ws);
        var (lw, lh) = LogoSize(logo, ws);

        int padding = 20;
        var pos = ws.PositionMode switch
        {
            "topLeft"     => new PointF(padding, padding),
            "topRight"    => new PointF(w - lw - padding, padding),
            "bottomLeft"  => new PointF(padding, h - lh - padding),
            "center"      => new PointF((w - lw) / 2f, (h - lh) / 2f),
            _             => new PointF(w - lw - padding, h - lh - padding),
        };

        if (ws.LogoEnabled && logo != null)
        {
            DrawImageWithOpacity(g, logo, pos.X, pos.Y, lw, lh, (float)ws.Opacity);
        }
        if (ws.TextEnabled && !string.IsNullOrEmpty(ws.Text))
        {
            var textColor = ColorHelpers.FromHex(ws.TextColorHex);
            float fontSize = Math.Max(12f, lh * 0.18f);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.FromArgb((int)(ws.Opacity * 255), textColor));
            var textSize = g.MeasureString(ws.Text, font);
            float ty = ws.LogoEnabled
                ? pos.Y + lh + 6
                : pos.Y;
            float tx = ws.LogoEnabled
                ? pos.X + (lw - textSize.Width) / 2f
                : pos.X;
            g.DrawString(ws.Text, font, brush, tx, ty);
        }
        logo?.Dispose();
    }

    private static void DrawTiled(Graphics g, int w, int h, WatermarkSettings ws)
    {
        Bitmap? logo = LoadLogo(ws);
        var (lw, lh) = LogoSize(logo, ws);

        // Rotate by -30° around the center, then tile across the diagonal-extended region.
        var state = g.Save();
        g.TranslateTransform(w / 2f, h / 2f);
        g.RotateTransform(-30f);
        g.TranslateTransform(-w / 2f, -h / 2f);

        double spacing = Math.Max(60, ws.Spacing);
        // Extend the region so rotated coverage is still full.
        int extra = (int)(Math.Max(w, h) * 0.5);
        int xStart = -extra;
        int yStart = -extra;
        int xEnd = w + extra;
        int yEnd = h + extra;

        int row = 0;
        for (double y = yStart; y < yEnd; y += spacing, row++)
        {
            double offset = ws.TilePattern switch
            {
                "brick" => (row % 2 == 0 ? 0 : spacing / 2.0),
                _ => 0,
            };
            for (double x = xStart; x < xEnd; x += spacing)
            {
                double jx = 0, jy = 0;
                if (ws.TilePattern == "random")
                {
                    var seed = (int)(x * 31 + y);
                    var rnd = new Random(seed);
                    jx = (rnd.NextDouble() - 0.5) * spacing * ws.TileRandomness;
                    jy = (rnd.NextDouble() - 0.5) * spacing * ws.TileRandomness;
                }
                float px = (float)(x + offset + jx);
                float py = (float)(y + jy);

                if (ws.LogoEnabled && logo != null)
                {
                    DrawImageWithOpacity(g, logo, px, py, lw, lh, (float)ws.Opacity);
                }
                if (ws.TextEnabled && !string.IsNullOrEmpty(ws.Text))
                {
                    var textColor = ColorHelpers.FromHex(ws.TextColorHex);
                    float fontSize = Math.Max(14f, lh * 0.22f);
                    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Color.FromArgb((int)(ws.Opacity * 255), textColor));
                    g.DrawString(ws.Text, font, brush, px, py + (ws.LogoEnabled ? lh + 4 : 0));
                }
            }
        }

        g.Restore(state);
        logo?.Dispose();
    }

    private static Bitmap? LoadLogo(WatermarkSettings ws)
    {
        if (!ws.LogoEnabled || string.IsNullOrWhiteSpace(ws.LogoPath)) return null;
        if (!File.Exists(ws.LogoPath)) return null;
        return BitmapHelpers.LoadBitmap(ws.LogoPath);
    }

    private static (int w, int h) LogoSize(Bitmap? logo, WatermarkSettings ws)
    {
        double targetMax = Math.Max(40, ws.LogoSize);
        if (logo == null) return ((int)targetMax, (int)targetMax);
        double scale = targetMax / Math.Max(logo.Width, logo.Height);
        return ((int)(logo.Width * scale), (int)(logo.Height * scale));
    }

    private static void DrawImageWithOpacity(Graphics g, Image img, float x, float y, int w, int h, float opacity)
    {
        if (Math.Abs(opacity - 1f) < 0.001f)
        {
            g.DrawImage(img, new RectangleF(x, y, w, h));
            return;
        }
        var attr = new System.Drawing.Imaging.ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0f, 1f) };
        attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        g.DrawImage(img,
            new Rectangle((int)x, (int)y, w, h),
            0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
    }
}
