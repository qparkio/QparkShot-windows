using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;
using QPARKShot.Services;
namespace QPARKShot.Localization;

public sealed class L : INotifyPropertyChanged
{
    public static L Shared { get; } = new();
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = Load();
    public static readonly string[] Languages = { "en", "es", "zh-Hans", "ja", "fr", "ru", "uk", "kk", "ar", "de", "it", "pt-BR" };
    public string Language => SettingsStore.Shared.Settings.Localization.AppLanguageCode;
    public string this[string key] => T(key);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new("Item[]"));
    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var row))
            row = Strings.Values.FirstOrDefault(value => value.GetValueOrDefault("en") == key);
        return row?.GetValueOrDefault(Shared.Language) ?? row?.GetValueOrDefault("en") ?? key;
    }
    public static string Format(string key, params object[] values)
    {
        var result = T(key);
        foreach (var value in values)
        {
            var match = System.Text.RegularExpressions.Regex.Match(result, "%(@|ld|d|s)");
            if (match.Success) result = result[..match.Index] + value + result[(match.Index + match.Length)..];
        }
        return result;
    }
    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("QPARKShot.Localization.strings.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
    }
}
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; }
    public TrExtension(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = L.Shared, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
