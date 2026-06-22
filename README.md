# QPARK Shot — Windows

Nativный Windows-порт [QPARK Shot](../qpark-shot/) на C# + WinUI 3 (Windows App SDK, .NET 8). Полный паритет с macOS-версией 1.1.0: захват (selection / full screen / delay), in-memory очередь скриншотов, редактор с аннотациями, watermark, drag-out, сохранение, hotkeys, tray icon.

## Prerequisites

- **Windows 10 1809+ / Windows 11** (x64)
- **.NET 8 SDK** (`dotnet --version` ≥ 8.0)
- **Visual Studio 2022** с workload «Windows App SDK» (или просто `dotnet` CLI + PowerShell 7)
- Для MSI: `dotnet tool install --global wix` (WiX v4)

## Dev

```powershell
# Restore + run from CLI
dotnet restore QPARKShot.sln
dotnet run --project QPARKShot
```

Из Visual Studio — открой `QPARKShot.sln`, F5.

## Release build

```powershell
pwsh scripts/build_release.ps1   # → build/Release/QPARKShot.exe (+ DLLs)
pwsh scripts/build_msi.ps1       # → build/Msi/QPARKShot-1.1.0.msi
```

Сборка **только на Windows** — кросс-компиляция со Swift/AppKit невозможна.

## Структура

```
QPARKShot/
├── App.xaml(.cs)              # Entry, theme, lifecycle hooks
├── MainWindow.xaml(.cs)       # Backdrop (Mica) + Frame navigation
├── Views/
│   ├── GalleryPage            # Сетка миниатюр
│   ├── EditorPage             # Тулбар + Canvas + sidebar
│   ├── SettingsPage           # 6 табов
│   ├── DrawingCanvas          # Freehand/arrow/rect/text + crop
│   ├── ShotQueueSidebar       # Карусель буфера слева
│   └── ScreenshotCard         # Карточка в галерее
├── Services/
│   ├── SettingsStore          # JSON в %APPDATA%\QPARK Shot\
│   ├── ShotQueueStore         # in-memory очередь
│   ├── CaptureService         # screencap pipeline
│   ├── SelectionOverlayController  # custom fullscreen overlay
│   ├── HotkeyService          # Win32 RegisterHotKey + WM_HOTKEY
│   ├── TrayIconService        # NotifyIcon
│   ├── WatermarkRenderer      # GDI+ — портирован 1-в-1 из macOS
│   ├── ClipboardService       # SetBitmap
│   ├── ImageExportService     # PNG save
│   └── CleanupService         # retention policy
├── Models/                    # POCO mirrors
└── Helpers/                   # Bitmap / Color / P/Invoke
```

## Известные ограничения (Phase 1)

- **Multi-monitor selection**: overlay покрывает virtual desktop; работает, но при сложных HiDPI-сетапах могут быть погрешности — допилим в 1.2.
- **InkCanvas vs custom polyline**: freehand сейчас на обычной `Polyline`. Поведение слегка proще macOS-`PKCanvas`, но экспорт идентичен (GDI+).
- **Share**: на unpackaged WinUI 3 нет родного DataTransferManager — фоллбэк через `LauncherOptions.DisplayApplicationPicker`. В 1.2 добавим packaged MSIX-сборку с правильным share UI.

## Settings parity

Файл `%APPDATA%\QPARK Shot\settings.json` имеет ту же схему, что и macOS-`UserDefaults` blob. Можно копировать настройки между ОС вручную.

## Verification checklist

См. [docs/feature_parity.md](docs/feature_parity.md).
