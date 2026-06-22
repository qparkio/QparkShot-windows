using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using QPARKShot.Models;

namespace QPARKShot.Services;

/// <summary>
/// Singleton mirror of macOS <c>SettingsStore</c>. Persists the full
/// <see cref="AppSettings"/> blob to <c>%APPDATA%\QPARK Shot\settings.json</c>.
/// </summary>
public sealed class SettingsStore : ObservableObject
{
    public static SettingsStore Shared { get; } = new();

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QPARK Shot");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private AppSettings _settings = new();
    public AppSettings Settings
    {
        get => _settings;
        private set => SetProperty(ref _settings, value);
    }

    public event EventHandler? SettingsChanged;

    private SettingsStore()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts());
                if (loaded != null)
                {
                    Settings = loaded;
                    OnPropertyChanged(string.Empty);
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch
        {
            // ignore — keep defaults
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, JsonOpts());
            File.WriteAllText(SettingsPath, json);
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>Same approach as macOS: mutate, call <c>Save()</c>.</summary>
    public void Mutate(Action<AppSettings> mutator)
    {
        mutator(Settings);
        Save();
    }

    private static JsonSerializerOptions JsonOpts() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
