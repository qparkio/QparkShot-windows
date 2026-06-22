using System;
using System.IO;
using System.Threading.Tasks;

namespace QPARKShot.Services;

/// <summary>
/// Mirror of macOS <c>MainGalleryView.performCleanup</c>:
/// drops PNGs older than the configured retention window.
/// </summary>
public static class CleanupService
{
    public static Task PerformAsync()
    {
        var s = SettingsStore.Shared.Settings.Cleanup;
        if (s.Mode != "afterDuration") return Task.CompletedTask;

        return Task.Run(() =>
        {
            var limit = DateTime.Now.AddSeconds(-s.DurationSeconds);

            if (s.IncludeSavedFiles)
            {
                var dir = ImageExportService.PicturesFolder();
                CleanFolder(dir, limit);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "QPARK Shot");
            CleanFolder(tempDir, limit);
        });
    }

    private static void CleanFolder(string folder, DateTime limit)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, "*.png"))
        {
            try
            {
                if (File.GetCreationTime(path) < limit)
                {
                    File.Delete(path);
                }
            }
            catch { }
        }
    }
}
