using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using QPARKShot.Models;

namespace QPARKShot.Services;

/// <summary>
/// In-memory queue of captured screenshots for the current session.
/// Mirror of macOS <c>ShotQueueStore</c>.
/// </summary>
public sealed class ShotQueueStore : ObservableObject
{
    public static ShotQueueStore Shared { get; } = new();

    public ObservableCollection<ShotQueueItem> Items { get; } = new();

    private Guid? _activeId;
    public Guid? ActiveId
    {
        get => _activeId;
        set => SetProperty(ref _activeId, value);
    }

    private static readonly string TempPrefix = Path.GetTempPath();

    private ShotQueueStore() { }

    /// <summary>Append a captured shot. Returns the (possibly existing) item.</summary>
    public ShotQueueItem Enqueue(string path, DateTime? capturedAt = null)
    {
        var existing = Items.FirstOrDefault(i => i.Path == path);
        if (existing != null)
        {
            ActiveId = existing.Id;
            return existing;
        }
        var item = new ShotQueueItem(Guid.NewGuid(), path, capturedAt ?? DateTime.Now);
        Items.Add(item);
        ActiveId = item.Id;
        return item;
    }

    public ShotQueueItem? Item(Guid id) => Items.FirstOrDefault(i => i.Id == id);

    /// <summary>Remove an item; advance ActiveId to neighbour or null. Returns new ActiveId.</summary>
    public Guid? Remove(Guid id)
    {
        int idx = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id) { idx = i; break; }
        }
        if (idx < 0) return ActiveId;

        var removed = Items[idx];
        Items.RemoveAt(idx);

        // Delete temp PNGs only (user-saved files are left alone).
        if (removed.Path.StartsWith(TempPrefix, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(removed.Path); } catch { }
        }

        if (ActiveId == id)
        {
            if (Items.Count == 0)
            {
                ActiveId = null;
            }
            else
            {
                int next = Math.Min(idx, Items.Count - 1);
                ActiveId = Items[next].Id;
            }
        }
        return ActiveId;
    }

    public void ClearAll()
    {
        foreach (var item in Items.ToList())
        {
            if (item.Path.StartsWith(TempPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(item.Path); } catch { }
            }
        }
        Items.Clear();
        ActiveId = null;
    }
}
