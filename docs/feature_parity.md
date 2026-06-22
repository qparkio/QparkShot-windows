# Feature parity — macOS 1.1.0 ↔ Windows 1.1.0

Чек-лист для проверки на Windows-машине после первой сборки.

| Feature | macOS reference | Windows status |
|---------|----------------|----------------|
| Tray-icon с меню | `setupStatusItem` ([AppDelegate.swift:112](../qpark-shot/Sources/AppDelegate.swift)) | `TrayIconService` |
| Selection capture | `screencapture -i` | `SelectionOverlayController` + `Graphics.CopyFromScreen` |
| Full-screen capture | `screencapture -m` | `CaptureService` mode=`fullScreen` |
| Timed delay (3/5/10) | `-T` flag | `CaptureService.delayOverride` + tray submenu |
| Global hotkey: selection | Carbon `RegisterEventHotKey` | `HotkeyService` (`RegisterHotKey` Win32) |
| Global hotkey: full-screen | Same | Same |
| In-memory queue (sidebar carousel) | `ShotQueueStore` | `ShotQueueStore` (mirror) |
| Per-item preview-with-watermark | `eye` button | `OnPreviewClick` → `ContentDialog` |
| Per-item delete from buffer | trash icon | `OnDeleteClick` |
| Clear buffer with confirm | `NSAlert` | `ContentDialog` |
| Editor: freehand | DrawingCanvas | `FreehandAnnotation` |
| Editor: arrow | DrawingCanvas | `ArrowAnnotation` |
| Editor: rectangle | DrawingCanvas | `RectangleAnnotation` |
| Editor: text | DrawingCanvas | `TextAnnotation` |
| Editor: crop | `cropRect` | `DrawingCanvas.CropRect` |
| Undo / redo | Stack snapshots | Stack snapshots |
| Watermark: text + logo | `renderAnnotatedImage` | `WatermarkRenderer` (GDI+) |
| Watermark: tiled (aligned/brick/random) | Yes | Yes (port of -30° rotation + jitter) |
| Watermark: 5 positions (single) | Yes | Yes |
| Live preview card в Settings | `WatermarkPreviewView` | TBD (Phase 1 — основные toggles, full live preview в 1.1.1) |
| Copy to clipboard | `NSPasteboard` | `ClipboardService.SetBitmapAsync` |
| Share | `NSSharingServicePicker` | `Launcher.LaunchFileAsync` (fallback) |
| Save to disk | `~/Pictures/QPARK Shot` | `%USERPROFILE%\Pictures\QPARK Shot` |
| Custom save folder | settings | settings |
| Cleanup (afterDuration) | `performCleanup` | `CleanupService.PerformAsync` |
| Drag-out (gallery + sidebar) | `.onDrag` + `NSItemProvider` | `DragStarting` + `SetStorageItems` |
| Theme (light/dark/system) | `appKitAppearanceName` | `Application.RequestedTheme` |
| Frosted window backdrop | `NSVisualEffectView` | `MicaBackdrop` |
| Settings JSON schema | `flutter.qpark_shot.app_settings.v1` | `%APPDATA%\QPARK Shot\settings.json` (same keys) |
| Version 1.1.0 | `MARKETING_VERSION` | `AssemblyVersion` |

## Manual QA

После запуска `QPARKShot.exe`:

1. Дважды снять скрин (`Ctrl+Shift+C`) → в редакторе слева 2 миниатюры, активна последняя.
2. Кликнуть по предыдущей → переключение, аннотации сброшены.
3. Включить watermark (Settings → Watermark, text=«TEST», logo выкл), нажать `eye` на миниатюре — preview-dialog c наложением.
4. `trash` на миниатюре — удаляет, активная переключается на соседа.
5. Tray → Capture Full Screen → весь экран в очереди.
6. Tray → Capture with Delay → 5 s → пауза → захват.
7. Drag миниатюру в Explorer/Telegram — файл копируется/прикрепляется.
8. Save в редакторе — PNG появляется в `Pictures\QPARK Shot\`.
9. Закрыть и переоткрыть приложение — Settings сохранены в `%APPDATA%\QPARK Shot\settings.json`.
10. About → версия `1.1.0`.
