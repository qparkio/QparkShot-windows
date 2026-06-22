using System;
using System.Drawing;
using System.IO;
using QPARKShot.Helpers;

namespace QPARKShot.Services;

public static class ImageExportService
{
    public static string PicturesFolder()
    {
        var customDir = SettingsStore.Shared.Settings.Cleanup.SaveDirectory;
        if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir)) return customDir;
        var pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pics, "QPARK Shot");
    }

    /// <summary>Mirror of macOS <c>saveImage(image:isTemporary:)</c>.</summary>
    public static string? SaveBitmap(Bitmap bitmap, bool isTemporary)
    {
        string folder = isTemporary
            ? Path.Combine(Path.GetTempPath(), "QPARK Shot")
            : PicturesFolder();
        Directory.CreateDirectory(folder);

        var filename = $"qpark-shot-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..8]}.png";
        var path = Path.Combine(folder, filename);
        return BitmapHelpers.SavePng(bitmap, path) ? path : null;
    }
}
