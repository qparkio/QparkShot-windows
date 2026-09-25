using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using QPARKShot.Helpers;
using QPARKShot.Models;
namespace QPARKShot.Services;

public sealed class SettingsStore : ObservableObject
{
    public static SettingsStore Shared { get; } = new();
    private readonly string _path;
    private bool _primaryCorrupt;
    public AppSettings Settings { get; private set; } = new();
    public event EventHandler? SettingsChanged;
    public string? LastError { get; private set; }
    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppPaths.SettingsDirectory, "settings.json");
        Load();
    }
    public void Load()
    {
        _primaryCorrupt = false;
        foreach (var path in new[] { _path, _path + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonFile.Options) ?? throw new JsonException("Settings root is null.");
                Settings.Watermark ??= new(); Settings.Watermark.Text ??= new(); Settings.Watermark.Logo ??= new();
                Settings.Cleanup ??= new(); Settings.Queue ??= new(); Settings.Capture ??= new();
                Settings.Hotkey ??= new(); Settings.FullScreenHotkey ??= new();
                Settings.Export ??= new(); Settings.Gallery ??= new(); Settings.Localization ??= new();
                Settings.Hotkey.Modifiers ??= new(); Settings.FullScreenHotkey.Modifiers ??= new();
                Settings.Localization.TextRecognitionLanguageCodes ??= new() { "en" };
                return;
            }
            catch (Exception ex) { if (path == _path) _primaryCorrupt = true; LastError = ex.Message; Logger.LogException("Settings load", ex); }
        }
    }
    public void Save()
    {
        try
        {
            // Preserve both the recoverable backup and the damaged primary for diagnosis.
            JsonFile.Save(_path, Settings, _primaryCorrupt ? _path + ".corrupt-" + Guid.NewGuid().ToString("N") : null);
            _primaryCorrupt = false;
            LastError = null;
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { LastError = ex.Message; Logger.LogException("Settings save", ex); throw; }
    }
    public void Mutate(Action<AppSettings> mutator) { mutator(Settings); Save(); }
}
