using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QPARKShot;
using QPARKShot.Localization;
using QPARKShot.Models;
using QPARKShot.Services;
using QPARKShot.Views;
using QPARKShot.Helpers;
using Bitmap = System.Drawing.Bitmap;

internal static class Program
{
    private static readonly List<string> Passed = new();
    private static string _output = "";
    [STAThread]
    public static int Main(string[] args)
    {
        _output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "QPARKShot-QA"));
        Directory.CreateDirectory(_output);
        AppPaths.TestRoot = Path.Combine(_output, "isolated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(AppPaths.TestRoot);
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var exitCode = 1;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try { await RunAsync(); exitCode = 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(_output, "failure.txt"), ex.ToString()); Console.Error.WriteLine(ex); }
            finally
            {
                WorkspaceStore.Shared.Stop();
                File.WriteAllText(Path.Combine(_output, "results.json"), JsonSerializer.Serialize(new { passed = Passed, success = exitCode == 0, packaged = OcrService.HasPackageIdentity }, new JsonSerializerOptions { WriteIndented = true }));
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        Dispatcher.Run();
        return exitCode;
    }
    private static void Check(bool condition, string scenario)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + scenario);
        Passed.Add(scenario); Console.WriteLine("PASS " + scenario);
    }
    private static async Task RunAsync()
    {
        var oldPath = Path.Combine(AppPaths.TestRoot!, "old-settings.json");
        File.WriteAllText(oldPath, """{"themePreference":"light","watermark":{"text":{"enabled":true,"text":"KEEP"}},"cleanup":{"saveDirectory":"C:\\OldPictures"},"capture":{"delaySeconds":5}}""");
        var migrated = new SettingsStore(oldPath);
        Check(migrated.Settings.Watermark.Text.Text == "KEEP" && migrated.Settings.Cleanup.SaveDirectory == "C:\\OldPictures", "1.1 settings preserved");
        Check(migrated.Settings.Export.DefaultQuickAction == "edit" && migrated.Settings.Gallery.OcrEnabled, "new settings receive compatible defaults");
        migrated.Save(); var reread = new SettingsStore(oldPath);
        Check(reread.Settings.Capture.DelaySeconds == 5 && reread.Settings.Watermark.Text.Enabled, "settings round trip");
        File.WriteAllText(oldPath, "broken JSON");
        var recovered = new SettingsStore(oldPath);
        Check(recovered.Settings.Watermark.Text.Text == "KEEP", "damaged settings recover from backup");
        recovered.Save();
        Check(new SettingsStore(oldPath + ".bak").Settings.Watermark.Text.Text == "KEEP"
            && Directory.GetFiles(AppPaths.TestRoot!, "old-settings.json.corrupt-*").Any(p => File.ReadAllText(p) == "broken JSON"),
            "saving recovered settings preserves the good backup and damaged primary evidence");
        var draft = new EditorDraft();
        var arrow = new ArrowAnnotation { Start = new(5, 6), End = new(80, 90) };
        var crop = new Rect(10, 10, 100, 80);
        draft.Record(new[] { arrow }, crop); arrow.End = new(999, 999);
        Check(((ArrowAnnotation)draft.Current.Annotations[0]).End.X == 80, "draft snapshots do not share mutable annotations");
        draft.Undo(); Check(draft.Current.CropRect == null && draft.Current.Annotations.Count == 0, "undo restores crop and annotations together");
        draft.Redo(); Check(draft.Current.CropRect == crop && draft.Current.Annotations.Count == 1, "redo restores crop and annotations together");
        draft.MarkSaved(draft.Current); Check(!draft.IsDirty, "saved draft is clean");
        draft.Undo(); Check(draft.IsDirty, "undo after save is unsaved"); draft.Redo(); Check(!draft.IsDirty, "redo to saved snapshot is clean");
        Check(HotkeyService.Validate(new() { Enabled = true, Key = "C", Modifiers = new() { "shift" } }, new()) != null, "invalid shortcut rejected");
        Check(HotkeyService.Validate(new() { Enabled = true, Key = "C", Modifiers = new() { "control" } }, new() { Enabled = true, Key = "C", Modifiers = new() { "control" } }) == "settings.shortcut_duplicate", "duplicate shortcut rejected");
        Check(!AppPaths.IsOwnedTemporary(Path.Combine(Path.GetTempPath(), "another-app.png")), "cleanup does not own unrelated temporary files");
        Directory.CreateDirectory(AppPaths.DefaultPicturesDirectory);
        using var source = new Bitmap(800, 500);
        using (var g = System.Drawing.Graphics.FromImage(source))
        {
            g.Clear(System.Drawing.Color.White);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(35, 57, 78));
            using var font = new System.Drawing.Font("Arial", 48, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            g.DrawString("QPARK SHOT 2026", font, brush, 50, 90);
            using var smaller = new System.Drawing.Font("Arial", 26, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            g.DrawString("Capture. Edit. Save.\nA quiet workspace for your screenshots.", smaller, brush, 50, 190);
        }
        var path = ImageExportService.SaveBitmap(source, false, "clean", "Regression");
        var path2 = ImageExportService.SaveBitmap(source, false, "clean", "Regression");
        Check(path != path2 && File.Exists(path) && File.Exists(path2), "export never overwrites an existing PNG");
        Check(!ImageExportService.MakeFileName("../CON:*", "clean", DateTime.Now).Contains('/'), "filename template cannot escape output directory");
        using var rendered = ImageExportService.Render(source, new(new Annotation[] { new AreaAnnotation { Rect = new Rect(30, 30, 80, 60) } }, new Rect(20, 20, 200, 150)), "clean", WatermarkSettings.Disabled)!;
        Check(rendered.Width == 200 && rendered.Height == 150 && rendered.GetPixel(20, 20).R == 0, "redaction is composited before crop in image coordinates");
        using var blurred = WatermarkRenderer.Render(source, new[] { new AreaAnnotation { Rect = new Rect(40, 80, 500, 90), Blur = true } }, null, WatermarkSettings.Disabled);
        Check(blurred != null && blurred.Width == source.Width, "blur render succeeds without modifying dimensions");
        using var thumbnail = BitmapHelpers.Thumbnail(path, 1000);
        Check(thumbnail?.Width == 800, "small thumbnail remains usable after source disposal");
        Directory.CreateDirectory(AppPaths.TemporaryDirectory);
        var protectedTemp = Path.Combine(AppPaths.TemporaryDirectory, "active.png"); source.Save(protectedTemp);
        var expiredTemp = Path.Combine(AppPaths.TemporaryDirectory, "expired.png"); source.Save(expiredTemp);
        File.SetLastWriteTimeUtc(protectedTemp, DateTime.UtcNow.AddDays(-10)); File.SetLastWriteTimeUtc(expiredTemp, DateTime.UtcNow.AddDays(-10));
        var active = ShotQueueStore.Shared.Enqueue(protectedTemp);
        SettingsStore.Shared.Settings.Cleanup.Mode = "afterDuration";
        await CleanupService.PerformAsync();
        Check(File.Exists(protectedTemp) && !File.Exists(expiredTemp), "cleanup preserves active captures while removing expired owned temp files");
        ShotQueueStore.Shared.Remove(active.Id);
        SettingsStore.Shared.Settings.Cleanup.Mode = "never";
        var index = new GalleryIndexStore(Path.Combine(AppPaths.TestRoot!, "index.json"));
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPaths.DefaultPicturesDirectory };
        index.Reconcile(new[] { path }, roots); index.Entries[path].Favorite = true; index.Entries[path].Tags = new() { "support" }; index.Save();
        index.Reconcile(Array.Empty<string>(), new HashSet<string>());
        Check(!index.Entries[path].Missing, "unavailable roots preserve library records");
        index.Reconcile(Array.Empty<string>(), roots);
        Check(index.Entries[path].Missing && index.Entries[path].Favorite, "missing file retains favorite and tags");
        index.Relink(path, path2);
        Check(index.Entries[path2].Favorite && index.Entries[path2].Tags.Contains("support"), "relink preserves metadata");
        Check(index.Entries[path2].Matches("support", true) && !index.Entries[path2].Matches("support", false), "metadata search respects preference");
        SettingsStore.Shared.Settings.Gallery.OcrEnabled = false;
        SettingsStore.Shared.Settings.Localization.AppLanguageCode = "en";
        SettingsStore.Shared.Settings.ThemePreference = "light";
        App.ApplyTheme("light");
        var window = new MainWindow(); App.MainWindowInstance = window; Application.Current.MainWindow = window;
        window.Show(); await Task.Delay(300); await WorkspaceStore.Shared.RefreshAsync();
        var first = ShotQueueStore.Shared.Enqueue(path);
        var second = ShotQueueStore.Shared.Enqueue(path2);
        EditorDraftStore.Shared.For(first.Id).Record(new[] { arrow }, crop);
        Check(!EditorDraftStore.Shared.For(second.Id).CanUndo && EditorDraftStore.Shared.For(first.Id).CanUndo, "drafts are independent per screenshot");
        await SaveWindowAsync(window, "library-en-light");
        WorkspaceStore.Shared.Search = "missing-query";
        window.ShowEditor(first.Id); await Task.Delay(250); window.ShowGallery();
        Check(WorkspaceStore.Shared.Search == "missing-query", "return from editor preserves library search");
        WorkspaceStore.Shared.Search = ""; window.ShowGallery();
        window.ShowEditor(first.Id); await Task.Delay(250); await SaveWindowAsync(window, "editor-en-light");
        var editor = (EditorPage)((ContentControl)window.FindName("ContentHost")).Content;
        var canvas = (DrawingCanvas)editor.FindName("Canvas");
        Check(canvas.CropRect == crop && canvas.Annotations.Count == 1, "editor restores saved-in-session draft");
        canvas.Undo(); Check(canvas.CropRect == null, "editor crop undo is wired"); canvas.Redo();
        await editor.ExportActionAsync("save"); Check(!EditorDraftStore.Shared.For(first.Id).IsDirty, "editor save marks the rendered snapshot saved");
        window.ShowReview(first.Id); await Task.Delay(250); await SaveWindowAsync(window, "review-en-light");
        window.ShowGallery();
        SettingsStore.Shared.Settings.Localization.AppLanguageCode = "ru"; SettingsStore.Shared.Settings.ThemePreference = "dark"; App.ApplyTheme("dark");
        SettingsStore.Shared.Save(); window.ShowGallery();
        var gallery = (GalleryPage)((ContentControl)window.FindName("ContentHost")).Content;
        var inspector = (StackPanel)gallery.FindName("Inspector");
        Check(inspector.Children.OfType<Button>().Any(b => Equals(b.Content, L.T("common.edit"))), "language change refreshes existing inspector actions");
        await SaveWindowAsync(window, "library-ru-dark");
        window.Width = 1000; window.Height = 720; window.ShowEditor(first.Id); await Task.Delay(150); await SaveWindowAsync(window, "editor-ru-compact");
        var settingsWindow = new Window { Content = new SettingsPage("appearance"), Width = 940, Height = 720 };
        settingsWindow.SetResourceReference(Window.BackgroundProperty, "WindowBackground"); settingsWindow.SetResourceReference(Window.ForegroundProperty, "WindowForeground"); settingsWindow.Show(); await SaveWindowAsync(settingsWindow, "settings-ru-dark"); settingsWindow.Close();
        var watermarkWindow = new Window { Content = new SettingsPage("watermark"), Width = 940, Height = 720 };
        watermarkWindow.SetResourceReference(Window.BackgroundProperty, "WindowBackground"); watermarkWindow.SetResourceReference(Window.ForegroundProperty, "WindowForeground");
        watermarkWindow.Show(); await SaveWindowAsync(watermarkWindow, "watermark-ru-dark"); watermarkWindow.Close();
        foreach (var code in L.Languages)
        {
            SettingsStore.Shared.Settings.Localization.AppLanguageCode = code;
            Check(L.T("workspace.library") != "workspace.library" && L.T("editor.tool_blur") != "editor.tool_blur", "localized core actions: " + code);
        }
        SettingsStore.Shared.Settings.Localization.AppLanguageCode = "ar"; window.ShowGallery(); window.UpdateAppearance(); await SaveWindowAsync(window, "library-ar");
        if (OcrService.HasPackageIdentity)
        {
            SettingsStore.Shared.Settings.Localization.TextRecognitionLanguageCodes = new() { "en" };
            var text = await OcrService.RecognizeAsync(path, CancellationToken.None);
            Check(text.Contains("QPARK", StringComparison.OrdinalIgnoreCase), "installed MSIX recognizes local fixture with Windows OCR");
            await SaveStoreScreenshotsAsync(window, first, second, text);
        }
        else Check(!OcrService.HasPackageIdentity, "unpackaged build reports OCR identity requirement");
        window.IsQuitting = true; window.Close();
        Check(!Directory.Exists(Path.Combine(AppPaths.TestRoot!, "..", "outside")), "QA uses isolated application data");
    }
    private static async Task SaveStoreScreenshotsAsync(MainWindow window, ShotQueueItem first, ShotQueueItem second, string recognizedText)
    {
        WorkspaceStore.Shared.Stop();
        SettingsStore.Shared.Settings.Gallery.OcrEnabled = true;
        SettingsStore.Shared.Settings.ThemePreference = "dark";
        App.ApplyTheme("dark");
        window.Width = 1440; window.Height = 900;
        // Use only the generated fixtures; no customer files are included in Store assets.
        var library = GalleryIndexStore.Shared;
        foreach (var key in library.Entries.Keys.Where(key => key != first.Path && key != second.Path).ToArray())
            library.Entries.Remove(key);
        foreach (var entry in library.Entries.Values)
        { entry.OcrText = recognizedText; entry.OcrStatus = "ready"; entry.OcrError = null; }
        library.Entries[first.Path].Favorite = true;
        library.Entries[first.Path].Tags = new() { "notes", "support" };
        EditorDraftStore.Shared.For(first.Id).Record(new Annotation[]
        {
            new RectangleAnnotation { Rect = new Rect(40, 86, 515, 62), ColorHex = "#0A84FF", StrokeWidth = 4 },
            new ArrowAnnotation { Start = new(630, 270), End = new(490, 205), ColorHex = "#FF6B45", StrokeWidth = 5 },
            new CalloutAnnotation { Position = new(610, 100), Number = 1, ColorHex = "#0A84FF", StrokeWidth = 5 },
        }, null);
        foreach (var code in L.Languages)
        {
            var directory = Path.Combine(_output, "store", code); Directory.CreateDirectory(directory);
            SettingsStore.Shared.Settings.Localization.AppLanguageCode = code;
            window.UpdateAppearance(); WorkspaceStore.Shared.Notify("");
            WorkspaceStore.Shared.Section = "library"; WorkspaceStore.Shared.Search = ""; WorkspaceStore.Shared.SelectedPath = first.Path;
            window.ShowGallery();
            await SaveWindowAsync(window, Path.Combine("store", code, "01-library"), 2);
            window.ShowEditor(first.Id); await Task.Delay(150);
            await SaveWindowAsync(window, Path.Combine("store", code, "02-editor"), 2);
            window.ShowReview(first.Id); await Task.Delay(150);
            await SaveWindowAsync(window, Path.Combine("store", code, "03-review"), 2);
        }
    }
    private static async Task SaveWindowAsync(Window window, string name, double scale = 1)
    {
        await Task.Delay(150); window.UpdateLayout();
        // Include the root Window so inherited RTL transforms and translucent brushes
        // are composed against the actual window background.
        var bitmap = new RenderTargetBitmap((int)(window.ActualWidth * scale), (int)(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen())
            drawing.DrawRectangle(window.Background, null, new Rect(0, 0, window.ActualWidth, window.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_output, name + ".png")); encoder.Save(stream);
    }
}
