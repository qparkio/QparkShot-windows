using System.IO;
using Microsoft.VisualBasic.FileIO;
using QPARKShot.Helpers;
namespace QPARKShot.Services;

public static class CleanupService
{
    private static bool _running;
    public static Task PerformAsync()
    {
        if (_running || SettingsStore.Shared.Settings.Cleanup.Mode != "afterDuration") return Task.CompletedTask;
        _running = true;
        try
        {
            var settings = SettingsStore.Shared.Settings.Cleanup;
            var limit = DateTime.UtcNow.AddSeconds(-Math.Max(3600, settings.DurationSeconds));
            var folders = settings.IncludeSavedFiles ? new[] { AppPaths.TemporaryDirectory, ImageExportService.PicturesFolder() } : new[] { AppPaths.TemporaryDirectory };
            // Execute each check/delete together on the UI dispatcher so capture and favorite updates cannot race it.
            foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var path in Directory.GetFiles(folder, "*.png"))
                {
                    if (ShotQueueStore.Shared.Items.Any(i => i.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    if (GalleryIndexStore.Shared.Entries.TryGetValue(path, out var entry) && entry.Favorite) continue;
                    if (File.GetLastWriteTimeUtc(path) >= limit) continue;
                    try
                    {
                        if (AppPaths.IsOwnedTemporary(path)) File.Delete(path);
                        else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                    }
                    catch (Exception ex) { Logger.LogException("Cleanup", ex); WorkspaceStore.Shared.Notify(ex.Message); }
                }
            }
        }
        catch (Exception ex) { Logger.LogException("Cleanup folder", ex); WorkspaceStore.Shared.Notify(ex.Message); }
        finally { _running = false; }
        return Task.CompletedTask;
    }
}
