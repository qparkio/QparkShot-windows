using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using QPARKShot.Models;
namespace QPARKShot.Services;

public static class OcrService
{
    public static bool HasPackageIdentity
    {
        get { try { _ = Windows.ApplicationModel.Package.Current.Id; return true; } catch { return false; } }
    }
    public static string Signature => "windows-ocr-v1:" + string.Join(",", SettingsStore.Shared.Settings.Localization.TextRecognitionLanguageCodes);
    public static async Task<string> RecognizeAsync(string path, CancellationToken token)
    {
        if (!HasPackageIdentity) throw new InvalidOperationException(Localization.L.T("windows.ocr_msix"));
        var codes = SettingsStore.Shared.Settings.Localization.TextRecognitionLanguageCodes.ToArray();
        var available = OcrEngine.AvailableRecognizerLanguages;
        var languages = codes.Select(code => available.FirstOrDefault(l => l.LanguageTag.Equals(code, StringComparison.OrdinalIgnoreCase)
            || l.LanguageTag.StartsWith(code.Split('-')[0] + "-", StringComparison.OrdinalIgnoreCase)))
            .Where(l => l != null).DistinctBy(l => l!.LanguageTag).ToArray();
        if (languages.Length == 0) throw new InvalidOperationException(Localization.L.T("windows.ocr_languages"));
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var scale = Math.Min(1d, OcrEngine.MaxImageDimension / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform { ScaledWidth = Math.Max(1u, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1u, (uint)(decoder.PixelHeight * scale)) };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var results = new List<string>();
        foreach (var language in languages)
        {
            token.ThrowIfCancellationRequested();
            var engine = OcrEngine.TryCreateFromLanguage(language!);
            if (engine == null) continue;
            var result = await engine.RecognizeAsync(bitmap);
            token.ThrowIfCancellationRequested();
            results.AddRange(result.Lines.Select(line => line.Text));
        }
        return string.Join(Environment.NewLine, results.Distinct());
    }
}
