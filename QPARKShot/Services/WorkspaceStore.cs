using System.IO;
using QPARKShot.Helpers;
using QPARKShot.Models;
using QPARKShot.Localization;
namespace QPARKShot.Services;

public sealed class WorkspaceStore
{
    public static WorkspaceStore Shared { get; } = new();
    public string Section { get; set; } = "library";
    public string Search { get; set; } = "";
    public string? SelectedPath { get; set; }
    public event EventHandler? Changed;
    private CancellationTokenSource? _ocrCancellation;
    private readonly SemaphoreSlim _ocrGate = new(1);
    private int _loadGeneration;
    public string Status { get; private set; } = "";
    public void Notify(string? status = null) { if (status != null) Status = status; Changed?.Invoke(this, EventArgs.Empty); }
    public IEnumerable<LibraryEntry> DisplayedEntries()
    {
        var entries = GalleryIndexStore.Shared.Entries.Values.AsEnumerable();
        entries = Section switch
        {
            "missing" => entries.Where(e => e.Missing),
            "favorites" => entries.Where(e => !e.Missing && e.Favorite),
            "recent" => entries.Where(e => !e.Missing && e.CreatedAt >= DateTime.UtcNow.AddDays(-7)),
            _ => entries.Where(e => !e.Missing),
        };
        return entries.Where(e => e.Matches(Search, SettingsStore.Shared.Settings.Gallery.SearchIndexEnabled))
            .OrderByDescending(e => e.Favorite).ThenByDescending(e => e.CreatedAt).ToArray();
    }
    public async Task RefreshAsync()
    {
        var generation = ++_loadGeneration;
        var roots = new[] { AppPaths.DefaultPicturesDirectory, SettingsStore.Shared.Settings.Cleanup.SaveDirectory }
            .Concat(GalleryIndexStore.Shared.Entries.Values.Select(e => Path.GetDirectoryName(e.Path)!))
            .Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var files = new List<string>();
        var readable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await Task.Run(() =>
        {
            foreach (var root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    files.AddRange(Directory.GetFiles(root, "*.png")); readable.Add(root);
                }
                catch (Exception ex) { Logger.LogException("Read library folder", ex); }
            }
        });
        if (generation != _loadGeneration) return;
        GalleryIndexStore.Shared.Reconcile(files, readable);
        var displayed = DisplayedEntries().ToArray();
        if (!displayed.Any(e => e.Path == SelectedPath)) SelectedPath = displayed.FirstOrDefault()?.Path;
        Notify();
        StartOcr();
    }
    public void StartOcr()
    {
        _ocrCancellation?.Cancel(); _ocrCancellation?.Dispose();
        _ocrCancellation = new();
        _ = IndexAsync(_ocrCancellation.Token);
    }
    public void Stop() => _ocrCancellation?.Cancel();
    private async Task IndexAsync(CancellationToken token)
    {
        var acquired = false;
        try
        {
            await _ocrGate.WaitAsync(token); acquired = true;
            if (!SettingsStore.Shared.Settings.Gallery.OcrEnabled) { Notify(); return; }
            var signature = OcrService.Signature;
            foreach (var entry in GalleryIndexStore.Shared.Entries.Values.Where(e => !e.Missing).ToArray())
            {
                token.ThrowIfCancellationRequested();
                if (entry.OcrModifiedAt == entry.ModifiedAt && entry.OcrSignature == signature) continue;
                var path = entry.Path; var modified = entry.ModifiedAt;
                entry.OcrStatus = "indexing"; entry.OcrError = null; Notify();
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        var text = await OcrService.RecognizeAsync(path, token);
                        token.ThrowIfCancellationRequested();
                        if (!GalleryIndexStore.Shared.Entries.TryGetValue(path, out var current) || !ReferenceEquals(entry, current)
                            || current.ModifiedAt != modified || File.GetLastWriteTimeUtc(path) != modified || signature != OcrService.Signature) break;
                        entry.OcrText = text; entry.OcrModifiedAt = modified; entry.OcrSignature = signature;
                        entry.OcrStatus = string.IsNullOrWhiteSpace(text) ? "empty" : "ready";
                        GalleryIndexStore.Shared.Save(); break;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        entry.OcrStatus = "failed"; entry.OcrError = ex.Message;
                        if (!OcrService.HasPackageIdentity || ex is InvalidOperationException || attempt == 1) break;
                        await Task.Delay(200, token);
                    }
                }
                Notify();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Logger.LogException("OCR indexing", ex); Notify(ex.Message); }
        finally { if (acquired) _ocrGate.Release(); }
    }
    public void RetryOcr(string path)
    {
        if (GalleryIndexStore.Shared.Entries.TryGetValue(path, out var entry)) { entry.OcrModifiedAt = null; entry.OcrSignature = null; }
        StartOcr();
    }
}
