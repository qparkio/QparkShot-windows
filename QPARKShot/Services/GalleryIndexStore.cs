using System.IO;
using System.Text.Json;
using QPARKShot.Helpers;
using QPARKShot.Models;
namespace QPARKShot.Services;

public sealed class GalleryIndexStore
{
    public static GalleryIndexStore Shared { get; } = new();
    private readonly string _path;
    private bool _primaryCorrupt;
    public Dictionary<string, LibraryEntry> Entries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public event EventHandler? Changed;
    public GalleryIndexStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppPaths.SettingsDirectory, "library-index.json");
        foreach (var candidate in new[] { _path, _path + ".bak" })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, LibraryEntry>>(File.ReadAllText(candidate), JsonFile.Options)
                    ?? throw new JsonException("Library index root is null.");
                Entries = new(entries, StringComparer.OrdinalIgnoreCase);
                break;
            }
            catch (Exception ex) { if (candidate == _path) _primaryCorrupt = true; Logger.LogException("Library index load", ex); }
        }
    }
    public void Reconcile(IEnumerable<string> paths, IReadOnlySet<string> readableRoots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) continue;
                seen.Add(file.FullName);
                if (!Entries.TryGetValue(file.FullName, out var entry)) Entries[file.FullName] = entry = new() { Path = file.FullName };
                entry.Tags ??= new(); entry.OcrText ??= "";
                entry.CreatedAt = file.CreationTimeUtc; entry.ModifiedAt = file.LastWriteTimeUtc; entry.Missing = false;
            }
            catch (Exception ex) { Logger.LogException("Library file", ex); }
        }
        foreach (var entry in Entries.Values)
        {
            // An unavailable drive or a different export folder must not erase metadata.
            if (readableRoots.Contains(Path.GetDirectoryName(entry.Path)!) && !seen.Contains(entry.Path)) entry.Missing = true;
        }
        Save();
    }
    public void Save()
    {
        JsonFile.Save(_path, Entries, _primaryCorrupt ? _path + ".corrupt-" + Guid.NewGuid().ToString("N") : null);
        _primaryCorrupt = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Forget(string path) { Entries.Remove(path); Save(); }
    public void Relink(string oldPath, string newPath)
    {
        if (!File.Exists(newPath)) throw new FileNotFoundException(newPath);
        if (Entries.ContainsKey(newPath) && !oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This image is already in the library.");
        var entry = Entries[oldPath]; Entries.Remove(oldPath);
        entry.Path = Path.GetFullPath(newPath); entry.Missing = false; entry.OcrModifiedAt = null;
        entry.CreatedAt = File.GetCreationTimeUtc(newPath); entry.ModifiedAt = File.GetLastWriteTimeUtc(newPath);
        Entries[entry.Path] = entry; Save();
    }
}
