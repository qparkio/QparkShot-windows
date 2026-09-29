using QPARKShot.Models;
using System.Windows;
namespace QPARKShot.Services;

public sealed record EditorSnapshot(IReadOnlyList<Annotation> Annotations, Rect? CropRect);
public sealed class EditorDraft
{
    private readonly List<EditorSnapshot> _history = new() { new(Array.Empty<Annotation>(), null) };
    private int _position;
    private EditorSnapshot _saved;
    public EditorDraft() => _saved = _history[0];
    public EditorSnapshot Current => _history[_position];
    public bool CanUndo => _position > 0;
    public bool CanRedo => _position + 1 < _history.Count;
    public bool IsDirty => !ReferenceEquals(Current, _saved);
    public void Record(IEnumerable<Annotation> annotations, Rect? crop)
    {
        _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        _history.Add(new(annotations.Select(a => a.Copy()).ToArray(), crop));
        _position++;
    }
    public void Undo() { if (CanUndo) _position--; }
    public void Redo() { if (CanRedo) _position++; }
    public void MarkSaved(EditorSnapshot snapshot) => _saved = snapshot;
}
public sealed class EditorDraftStore
{
    public static EditorDraftStore Shared { get; } = new();
    private readonly Dictionary<Guid, EditorDraft> _drafts = new();
    public EditorDraft For(Guid id) => _drafts.TryGetValue(id, out var draft) ? draft : _drafts[id] = new();
    public bool HasUnsavedChanges => _drafts.Values.Any(d => d.IsDirty);
    public void Remove(Guid id) => _drafts.Remove(id);
    public void Clear() => _drafts.Clear();
}
