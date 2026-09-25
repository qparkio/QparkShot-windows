<p align="center">
  <img src="QPARKShot/Assets/Logo.png" width="96" height="96" alt="QPARK Shot app icon">
</p>

<h1 align="center">QPARK Shot for Windows</h1>

<p align="center">Native screenshot capture, annotation, watermarking and a searchable local library. C#, WPF and .NET 8.</p>

## Windows 1.2

The 1.2 source update brings the current macOS workspace and editing workflow to Windows. The published GitHub release is still the older 1.1.0 NSIS installer; 1.2 MSIX Preview builds are available as GitHub Actions artifacts. Store submission and certification are separate steps.

### Capture and edit

- Capture a selected area, the primary screen, a window or the last selected area, with an optional delay.
- Configure global shortcuts for area and full-screen capture. Invalid, duplicate and unavailable shortcuts are reported.
- Keep multiple captures in the current session and switch between independent editing drafts.
- Draw freehand, arrows, rectangles and text; add numbered callouts, opaque redaction or blur.
- Crop with undo/redo shared across crop and annotations.
- Preview, copy, pin above other windows, share through Windows Share or save the rendered PNG.
- Choose clean, watermarked or support export presets and a filename template. Existing files are never overwritten.

### Workspace and library

- Library, Current Session, Favorites, Recent and Missing sections with a file inspector.
- Search filenames, tags and text recognized locally by Windows OCR.
- Keep favorites, tags and missing-file metadata; locate moved files without losing metadata.
- Return from the editor without losing the library search or per-image draft.
- Open settings in a separate window.
- Send saved files to the Recycle Bin. Scheduled cleanup protects active captures and favorites.

### Appearance and watermark

- Light, Dark and System themes; updated QPARK Shot branding.
- Core interface translations in 12 languages, including Russian and Arabic. Windows-specific help text falls back to English where a translation is absent.
- Text and logo watermarks with position, opacity, size and diagonal tiling controls.
- Live watermark preview and consistent rendering for all export actions.
- Close the window to keep the app in the tray; use Quit to exit. Unsaved captures/drafts prompt before quitting.

## Requirements

- Windows 10 version 1809 (build 17763) or later, x64.
- Packaged installation and installed Windows OCR language components for local text recognition.
- .NET 8 SDK and Windows SDK to build from source on Windows. The delivered MSIX includes the .NET runtime.

Multi-monitor capture, mixed DPI, native sharing and the transition from an existing NSIS installation require interactive validation on a Windows client before public release. See [verification boundaries](docs/windows-1.2-port.md).

## Build from a Mac

Edit on macOS and push to a `codex/**` branch. The [Build Windows workflow](.github/workflows/build-windows.yml) compiles and tests on Windows Server 2022, installs an internal MSIX for launch/OCR checks, and uploads a clean Preview plus QA evidence. Download the `QPARKShot-Windows-1.2.0` artifact from that run.

The Preview uses a test identity and a self-signed certificate. It is intended for a dedicated Windows test account. The downloadable `.cer` contains only the public certificate. Follow the [MSIX build and installation guide](docs/msix.md).

For a Microsoft Store package, supply the three exact Product identity values from Partner Center to the workflow. The Store MSIX is unsigned for upload; Microsoft signs it after certification. No paid signing certificate is needed for this Store-only route. Preview packages are not Store submissions.

## Build on Windows

Open `QPARKShot.sln` in Visual Studio 2022, or run:

```powershell
dotnet run --project QPARKShot.Tests/QPARKShot.Tests.csproj -c Release -- build/QA
dotnet publish QPARKShot/QPARKShot.csproj -c Release -r win-x64 --self-contained true -o build/Release
./scripts/build-msix.ps1 -TestPackage
```

Generated output is under `build/`, ignored by Git. The regression harness uses isolated settings and image folders. The CI artifact includes results and WPF-rendered interface screenshots.

The legacy NSIS script remains available in `scripts/installer.nsi` for an ordinary installer. It is not built by the MSIX workflow, and an unpackaged executable cannot use the Windows OCR API.

## Data and privacy

Screenshots and recognized text stay on the PC. There is no analytics SDK, account, backend or external OCR service. Sharing starts only when the user chooses the Windows Share action.

- Saved PNGs: `%USERPROFILE%\Pictures\QPARK Shot`, or a user-selected directory.
- Temporary captures: the app-owned `%TEMP%\QPARK Shot` directory.
- Settings and library index: `%APPDATA%\QPARK Shot\settings.json` and `library-index.json`, subject to MSIX AppData virtualization.
- Diagnostic log: `%TEMP%\qparkshot-debug.log`.

Existing Windows 1.1 settings fields are preserved, new settings receive defaults, and JSON writes retain a backup. PNGs stay at their existing paths. Per-image editing drafts belong to the current running session and are not persisted after quitting. The complete macOS settings file is not a portable Windows configuration.

## Project structure

```text
QPARKShot/                 WPF application, services, models and localized resources
QPARKShot.Tests/           Windows STA regression and UI evidence harness
packaging/                 MSIX manifest template and public logo assets
scripts/build-msix.ps1      Validated Preview/Store packaging
scripts/test-msix.ps1       Isolated runner installation and packaged OCR checks
scripts/installer.nsi       Legacy NSIS option
.github/workflows/         Windows CI
```

See the [port scope](docs/windows-1.2-port.md), [feature mapping](docs/feature_parity.md) and [MSIX guide](docs/msix.md).

## License and contact

[MIT License](LICENSE). Copyright © 2026 QPARK.

Questions, bug reports and security concerns: [work@qpark.io](mailto:work@qpark.io).
