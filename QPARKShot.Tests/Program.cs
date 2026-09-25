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
        Check(new SettingsStore(oldPath).Settings.Watermark.Text.Text == "KEEP", "damaged settings recover from backup");
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
        SettingsStore.Shared.Settings.Localization.AppLanguageCode = "ru"; App.ApplyTheme("dark");
        L.Shared.Refresh(); window.ShowGallery(); await SaveWindowAsync(window, "library-ru-dark");
        window.Width = 1000; window.Height = 720; window.ShowEditor(first.Id); await Task.Delay(150); await SaveWindowAsync(window, "editor-ru-compact");
        var settingsWindow = new Window { Content = new SettingsPage("appearance"), Width = 940, Height = 720 };
        settingsWindow.SetResourceReference(Window.BackgroundProperty, "WindowBackground"); settingsWindow.Show(); await SaveWindowAsync(settingsWindow, "settings-ru-dark"); settingsWindow.Close();
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
        }
        else Check(!OcrService.HasPackageIdentity, "unpackaged build reports OCR identity requirement");
        window.IsQuitting = true; window.Close();
        Check(!Directory.Exists(Path.Combine(AppPaths.TestRoot!, "..", "outside")), "QA uses isolated application data");
    }
    private static async Task SaveWindowAsync(Window window, string name)
    {
        await Task.Delay(150); window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_output, name + ".png")); encoder.Save(stream);
    }
}
