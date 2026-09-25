using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QPARKShot.Models;
namespace QPARKShot.Services;

public static class ImageExportService
{
    public static string PicturesFolder() => string.IsNullOrWhiteSpace(SettingsStore.Shared.Settings.Cleanup.SaveDirectory)
        ? AppPaths.DefaultPicturesDirectory : SettingsStore.Shared.Settings.Cleanup.SaveDirectory;
    public static string MakeFileName(string template, string preset, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(template)) template = "{date}_{time}_{preset}";
        var name = template.Replace("{date}", now.ToString("yyyyMMdd")).Replace("{time}", now.ToString("HHmmss_fff"))
            .Replace("{preset}", preset).Replace("{uuid}", Guid.NewGuid().ToString("N")[..8]);
        foreach (var c in Path.GetInvalidFileNameChars().Concat("/\\:?%*|\"<>")) name = name.Replace(c, '-');
        name = name.Trim().TrimEnd('.');
        if (name.Length > 160) name = name[..160];
        if (string.IsNullOrWhiteSpace(name)) name = "Screenshot";
        if (System.Text.RegularExpressions.Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) name = "Screenshot_" + name;
        return name + ".png";
    }
    public static Bitmap? Render(Bitmap source, EditorSnapshot snapshot, string preset, WatermarkSettings watermark) =>
        WatermarkRenderer.Render(source, snapshot.Annotations, snapshot.CropRect,
            preset == "clean" ? watermark with { TextEnabled = false, LogoEnabled = false } : watermark);
    public static string SaveBitmap(Bitmap bitmap, bool isTemporary) =>
        SaveBitmap(bitmap, isTemporary, SettingsStore.Shared.Settings.Export.SelectedPresetID, SettingsStore.Shared.Settings.Export.FilenameTemplate);
    public static string SaveBitmap(Bitmap bitmap, bool isTemporary, string preset, string template)
    {
        var folder = isTemporary ? AppPaths.TemporaryDirectory : PicturesFolder();
        Directory.CreateDirectory(folder);
        var name = MakeFileName(template, preset, DateTime.Now);
        for (var suffix = 0; ; suffix++)
        {
            var path = Path.Combine(folder, suffix == 0 ? name : Path.GetFileNameWithoutExtension(name) + $"_{suffix}.png");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(path)) { continue; }
            try { using (stream) bitmap.Save(stream, ImageFormat.Png); return path; }
            catch { stream.Dispose(); File.Delete(path); throw; }
        }
    }
}
