# QPARK Shot — Windows

Native Windows port of [QPARK Shot](../qpark-shot/) (macOS app) — built on **WPF / .NET 8**. Feature-parity with the macOS 1.1.0 release: tray-icon capture pipeline, in-memory shot queue, annotation editor, watermarks, drag-out, settings persistence, hotkeys.

> The first iteration of this project used WinUI 3 + Windows App SDK 1.5. WinUI 3 in *unpackaged self-contained* mode turned out to be fragile across machines (bootstrap auto-init, XBF asset deployment, MRT/PRI resource loading). We rewrote the UI on WPF (.NET 8) — same C# language, services and models reused, all the WinUI-specific pain points gone. WPF is mature (in production since 2006), self-contained deployment Just Works.

## Requirements

- **End users**: Windows 10 1809+ or Windows 11. No .NET runtime install needed — the installer ships everything.
- **Developers**: Visual Studio 2022 (Windows) or `dotnet 8 SDK` + PowerShell 7. To build the installer locally: `choco install nsis`.

## Architecture

```
QPARKShot/
├── App.xaml(.cs)              # Application entry, tray + hotkeys wiring
├── MainWindow.xaml(.cs)       # Frame navigation host + Mica backdrop (Win11)
├── Views/
│   ├── GalleryPage            # Grid of saved screenshots
│   ├── EditorPage             # Toolbar + canvas + buffer sidebar + preview overlay
│   ├── SettingsPage           # 6 tabs (Appearance/Hotkeys/Watermark/Storage/Buffer/About)
│   ├── DrawingCanvas          # Freehand / Arrow / Rectangle / Text / Crop
│   ├── ShotQueueSidebar       # Vertical carousel of in-memory buffer
│   ├── ScreenshotCard         # Card item in the gallery (drag-out, context menu)
│   └── WatermarkPreview       # Live preview of watermark settings
├── Services/
│   ├── SettingsStore          # JSON persistence in %APPDATA%\QPARK Shot\settings.json
│   ├── ShotQueueStore         # In-memory queue (ObservableCollection)
│   ├── CaptureService         # Selection / Full-screen / Delay capture pipeline
│   ├── SelectionOverlayController  # Borderless transparent overlay for region select
│   ├── HotkeyService          # Win32 RegisterHotKey + WM_HOTKEY via HwndSource
│   ├── TrayIconService        # WinForms NotifyIcon (rock-solid across Win10+11)
│   ├── WatermarkRenderer      # Pure GDI+ render — text + logo, single / tiled
│   ├── ImageExportService     # PNG save (Pictures\QPARK Shot or custom folder)
│   ├── ClipboardService       # WPF Clipboard.SetImage
│   └── CleanupService         # Retention policy (delete after N hours)
├── Models/                    # AppSettings, ShotQueueItem, Annotation, WatermarkSettings
└── Helpers/                   # Bitmap / Color helpers, Win32 P/Invoke, Logger
```

## Building locally

```powershell
# Restore + build + publish (self-contained, no external runtime needed)
dotnet restore QPARKShot.sln
dotnet publish QPARKShot\QPARKShot.csproj `
  -c Release -r win-x64 --self-contained true `
  -o build\Release

# Run from the publish folder
.\build\Release\QPARKShot.exe
```

### Installer (NSIS)

```powershell
# One-time NSIS install:
choco install nsis -y

# Build the installer:
mkdir build\Installer
& 'C:\Program Files (x86)\NSIS\makensis.exe' /V2 scripts\installer.nsi
# → build\Installer\QPARKShot-Setup-1.1.0.exe
```

Installs to `%LOCALAPPDATA%\Programs\QPARK Shot\` (per-user, no admin), with Start Menu + Desktop shortcuts, and uninstaller entry in Add/Remove Programs.

## CI

GitHub Actions workflow `.github/workflows/build-windows.yml` runs on every push to `main` on `windows-latest`:

1. Restore + publish self-contained x64 build
2. Install NSIS via Chocolatey
3. Compile the installer
4. Upload two artifacts:
   - `QPARKShot-Setup` — `.exe` installer
   - `QPARKShot-Windows-Portable` — unzipped publish folder

## Settings file location

`%APPDATA%\QPARK Shot\settings.json` — same JSON schema as the macOS version (under `UserDefaults`). You can copy this file between macOS and Windows to sync settings.

## Debug log

If the app crashes silently or behaves unexpectedly, check **`%TEMP%\qparkshot-debug.log`** — every startup step and exception is logged there.

## Feature parity with macOS 1.1.0

| Feature | Status |
|---------|--------|
| Tray icon with capture / preferences / quit menu | ✅ |
| Capture: selection, full screen, timed delay (3/5/10 s) | ✅ |
| Global hotkeys (selection + full-screen, independently configurable) | ✅ |
| In-memory shot queue + sidebar carousel | ✅ |
| Per-item preview-with-watermark, delete from buffer, drag-out | ✅ |
| Editor: freehand, arrow, rectangle, text, undo / redo | ✅ |
| Crop tool | ✅ |
| Watermark — text + logo, single position (5 corners) and tiled (aligned/brick/random with -30° rotation) | ✅ |
| Live watermark preview card in settings | ✅ |
| Mica backdrop (Win11 22H2+), dark theme | ✅ |
| Settings tabs: Appearance / Hotkeys / Watermark / Storage / Buffer / About | ✅ |
| Cleanup policy (never / after N hours) | ✅ |
| Drag-out from gallery and from buffer carousel | ✅ |
| Copy to clipboard, save to disk | ✅ |
| Settings persistence in JSON (`%APPDATA%\QPARK Shot\`) | ✅ |

## Replaced files (vs prior WinUI 3 iteration)

For maintainers: the WPF rewrite touches the UI layer and a few service adapters. The services / models / helpers below were either kept or only had types swapped (e.g., `Windows.Foundation.Point` → `System.Windows.Point`):

**Replaced entirely (WPF XAML + code-behind):**
- `App.xaml(.cs)`, `MainWindow.xaml(.cs)`
- All files under `Views/`

**Rewritten (WinUI-specific APIs swapped for WPF):**
- `Services/TrayIconService.cs` (H.NotifyIcon → `System.Windows.Forms.NotifyIcon`)
- `Services/SelectionOverlayController.cs` (WinUI Window → WPF Window)
- `Services/HotkeyService.cs` (message-only HWND → WPF `HwndSource`)
- `Services/ClipboardService.cs` (DataPackage → `System.Windows.Clipboard.SetImage`)
- `Helpers/BitmapHelpers.cs` (`BitmapImage` via async stream → `ToBitmapSource` sync)
- `Helpers/ColorHelpers.cs` (Windows.UI.Color → System.Windows.Media.Color)
- `Models/Annotation.cs` (Windows.Foundation.Point/Rect → System.Windows.Point/Rect)

**Unchanged or minimal edits:**
- `Services/SettingsStore.cs` — JSON persistence singleton
- `Services/ShotQueueStore.cs` — in-memory queue
- `Services/WatermarkRenderer.cs` — pure GDI+ rendering (framework-agnostic)
- `Services/CaptureService.cs` — capture pipeline
- `Services/ImageExportService.cs` — PNG save
- `Services/CleanupService.cs` — retention sweep
- `Models/AppSettings.cs`, `ShotQueueItem.cs`, `WatermarkSettings.cs` — POCOs
- `Helpers/Logger.cs`, `NativeMethods.cs`
- `scripts/installer.nsi` — NSIS spec
