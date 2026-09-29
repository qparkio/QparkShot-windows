using System.IO;
namespace QPARKShot.Services;

public static class AppPaths
{
    public static string? TestRoot { get; set; }
    public static string SettingsDirectory => TestRoot is { } root ? Path.Combine(root, "Settings") :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QPARK Shot");
    public static string TemporaryDirectory => Path.Combine(TestRoot ?? Path.GetTempPath(), "QPARK Shot");
    public static string DefaultPicturesDirectory => TestRoot is { } root ? Path.Combine(root, "Pictures") :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "QPARK Shot");
    public static bool IsOwnedTemporary(string path)
    {
        var full = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(full);
        return string.Equals(parent, TemporaryDirectory, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(parent?.TrimEnd(Path.DirectorySeparatorChar), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
             && Path.GetFileName(full).StartsWith("qpark-shot-", StringComparison.OrdinalIgnoreCase)
             && Path.GetExtension(full).Equals(".png", StringComparison.OrdinalIgnoreCase));
    }
}
