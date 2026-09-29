namespace QPARKShot.Models;

public sealed class LibraryEntry
{
    public string Path { get; set; } = "";
    public string FileName => System.IO.Path.GetFileName(Path);
    public bool Favorite { get; set; }
    public List<string> Tags { get; set; } = new();
    public string OcrText { get; set; } = "";
    public string OcrStatus { get; set; } = "queued";
    public string? OcrError { get; set; }
    public string? OcrSignature { get; set; }
    public DateTime? OcrModifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool Missing { get; set; }
    public bool Matches(string query, bool metadata) => string.IsNullOrWhiteSpace(query) ||
        (metadata ? string.Join(" ", FileName, OcrText, string.Join(" ", Tags)) : FileName)
            .Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
