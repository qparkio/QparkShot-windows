<p align="center">
  <img src="QPARKShot/Assets/Logo.png" width="96" height="96" alt="QPARK Shot app icon">
</p>

<h1 align="center">QPARK Shot for Windows</h1>

<p align="center">Native screenshot capture, annotation, watermarking and a searchable local library. C#, WPF and .NET 8.</p>

## Windows 1.2

QPARK Shot 1.2 brings the current macOS workspace and editing workflow to Windows.

- [Download the Windows 1.2.0 EXE installer](https://github.com/qparkio/QparkShot-windows/releases/download/v1.2.0/QPARKShot-Setup-1.2.0.exe) — self-contained, per-user installation.
- [Get QPARK Shot from Microsoft Store](https://apps.microsoft.com/detail/9NVF4TS6Z0C7) — signed installation, automatic updates and local Windows OCR.
- [Release notes and SHA-256 checksums](https://github.com/qparkio/QparkShot-windows/releases/tag/v1.2.0).

The standalone EXE is unsigned and Windows may show an unknown-publisher or SmartScreen warning. OCR requires the Store version; the standalone version supports capture, editing, exports, the local library, tags and filename search. Save current captures and quit from the tray before updating. The installer preserves settings and screenshots when upgrading from 1.1.

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
- .NET 8 SDK and Windows SDK to build from source on Windows. Both installers include the .NET runtime.

CI covers regression checks, installed-MSIX launch/OCR, standalone installation, upgrade from the published 1.1 installer and uninstall data preservation. Multi-monitor capture, mixed DPI and native sharing still need interactive Windows client validation. See [verification boundaries](docs/windows-1.2-port.md).

## Build from a Mac

Edit on macOS and push to a `codex/**` branch. The [Build Windows workflow](.github/workflows/build-windows.yml) compiles and tests on Windows Server 2022, verifies the standalone installer and an internal MSIX, and uploads the EXE, checksums, a clean Preview and QA evidence. Download the `QPARKShot-Windows-1.2.0` artifact from that run. Only files in its `GitHub` directory are standalone release assets.

The Preview uses a test identity and a self-signed certificate. It is intended for a dedicated Windows test account. The downloadable `.cer` contains only the public certificate. Follow the [MSIX build and installation guide](docs/msix.md).

For a Microsoft Store package, supply the three exact Product identity values from Partner Center to the workflow. The Store MSIX is unsigned for upload; Microsoft signs it after certification. No paid signing certificate is needed for this Store-only route. Preview packages are not Store submissions.

## Build on Windows

Open `QPARKShot.sln` in Visual Studio 2022, or run:

```powershell
dotnet run --project QPARKShot.Tests/QPARKShot.Tests.csproj -c Release -- build/QA
dotnet publish QPARKShot/QPARKShot.csproj -c Release -r win-x64 --self-contained true -o build/Release
./scripts/build-msix.ps1 -TestPackage
# With NSIS installed:
./scripts/build-installer.ps1
```

Generated output is under `build/`, ignored by Git. The regression harness uses isolated settings and image folders. The CI artifact includes results and WPF-rendered interface screenshots.

`scripts/build-installer.ps1` packages the published app with NSIS and writes the EXE, release notes and checksums to `build/GitHub`. `scripts/test-installer.ps1` runs only on disposable Windows GitHub runners and checks clean installation, upgrade from 1.1, running-app protection and uninstall data preservation. An unpackaged executable cannot use the Windows OCR API.

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
scripts/build-installer.ps1 Standalone EXE packaging and checksums
scripts/installer.nsi       Per-user NSIS installer
scripts/test-installer.ps1  Disposable-runner install/upgrade/uninstall checks
.github/workflows/         Windows CI
```

See the [port scope](docs/windows-1.2-port.md), [feature mapping](docs/feature_parity.md) and [MSIX guide](docs/msix.md).

## License and contact

[MIT License](LICENSE). Copyright © 2026 QPARK.

Questions, bug reports and security concerns: [work@qpark.io](mailto:work@qpark.io).
