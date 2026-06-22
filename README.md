<p align="center">
  <img src="QPARKShot/Assets/Logo.png" width="96" height="96" alt="QPARK Shot app icon">
</p>

<h1 align="center">QPARK Shot for Windows</h1>

<p align="center">
  Native Windows screenshot capture, annotation, watermarking, and local gallery app built with C#, WPF, and .NET 8.
</p>

<p align="center">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%201809%2B%20%2F%2011-0078D4">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8-512BD4">
  <img alt="WPF" src="https://img.shields.io/badge/WPF-Native-blueviolet">
  <img alt="Version" src="https://img.shields.io/badge/version-1.1.0-blue">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green">
</p>

## Overview

QPARK Shot is a local-first screenshot utility for Windows. It runs from the system tray, captures selected areas or the full screen, opens captures in an editor, and lets you annotate, crop, watermark, copy, share, or save the final PNG.

The app is intentionally simple from an infrastructure point of view: no analytics SDKs, no ad networks, no backend services, and no third-party runtime dependencies.

## Features

### Capture

- Capture a selected screen area with a built-in transparent overlay.
- Capture the full screen from the tray-icon menu or an optional global hotkey.
- Add a capture delay of 3, 5, or 10 seconds.
- Configure separate hotkeys for selection capture and full-screen capture.

### Edit

- Draw freehand lines, arrows, rectangles, and text annotations.
- Crop screenshots before export.
- Undo and redo annotation changes.
- Preview the exported image before saving.
- Copy the final image directly to the clipboard.
- Share the final image through the native Windows "Open with…" dialog.

### Watermark

- Add a text watermark with configurable color, opacity, size, and position.
- Add a logo watermark from a user-selected image file.
- Use a single-position watermark or a tiled diagonal layout.
- Preview watermark output live in Preferences before saving.

### Gallery and Storage

- Save PNG files to `%USERPROFILE%\Pictures\QPARK Shot` by default.
- Choose a custom save folder in Preferences.
- Browse recent screenshots in a local gallery.
- Open, copy, share, drag, or delete saved screenshots from the gallery.
- Keep current-session captures in an optional editor buffer sidebar.
- Automatically clean temporary files based on local cleanup preferences.

### Appearance

- Light, Dark, and System themes that follow the current Windows mode.
- Mica backdrop and rounded corners on Windows 11 22H2 or later.
- Close-to-tray behaviour: closing the window minimizes to the tray; real quit via the tray-icon menu.
- Single-instance enforcement: a second launch focuses the existing one instead of opening a duplicate.

## Requirements

- Windows 10 version 1809 (build 17763) or later, x64.
- .NET 8 SDK (only for building from source — the installer ships a self-contained runtime).
- Visual Studio 2022 (any edition) or the `dotnet` CLI in PowerShell 7.
- NSIS 3.x (only to rebuild the installer locally — `choco install nsis`).

## Install

The easiest way is the prebuilt installer:

1. Download `QPARKShot-Setup-1.1.0.exe` from the [latest release](https://github.com/qparkio/QparkShot-windows/releases/latest).
2. Run it. The installer is per-user and does not require administrator rights.
3. The app is installed into `%LOCALAPPDATA%\Programs\QPARK Shot\` with Start Menu and Desktop shortcuts.

To uninstall, use **Settings → Apps → Installed apps → QPARK Shot → Uninstall**.

## Build and Run

Open the solution in Visual Studio 2022:

```text
QPARKShot.sln
```

Select the **QPARKShot** project and press **F5**.

Command-line release build (self-contained, win-x64):

```powershell
dotnet restore QPARKShot.sln
dotnet publish QPARKShot\QPARKShot.csproj `
  -c Release -r win-x64 --self-contained true `
  -o build\Release
```

Generated build output is written under `./build`, which is ignored by Git.

### Build the installer

```powershell
choco install nsis -y
mkdir build\Installer
& 'C:\Program Files (x86)\NSIS\makensis.exe' /V2 scripts\installer.nsi
# → build\Installer\QPARKShot-Setup-1.1.0.exe
```

The GitHub Actions workflow `.github/workflows/build-windows.yml` automates both steps on every push and uploads the installer as a build artifact.

## Permissions

QPARK Shot does not require administrator rights or any special permissions on Windows. Screen capture uses the standard `BitBlt` GDI API, which works for any process running in the current user session.

Global hotkeys are registered with `RegisterHotKey` (Win32) and may silently fail to register if the chosen combination is already claimed by another application (for example, Windows Snip & Sketch on `Win+Shift+S`). If this happens, change the shortcut in **Preferences → Hotkeys**.

## Privacy

Screenshots stay on the user's PC. QPARK Shot does not collect personal data, does not include analytics, and does not transmit captured screen content.

Saved screenshots are written locally to `%USERPROFILE%\Pictures\QPARK Shot` unless the user chooses another folder. Temporary captures are stored in `%TEMP%` and can be cleared by the app's cleanup preferences.

Application settings are persisted in `%APPDATA%\QPARK Shot\settings.json`. The schema is identical to the macOS version, so the file is portable between OSes.

## Diagnostics

If the app misbehaves, look at the debug log:

```text
%TEMP%\qparkshot-debug.log
```

Every startup phase and every caught exception is recorded there. This is the single most useful artefact to attach to a bug report.

## Project Structure

```text
QPARKShot.sln                Solution file
QPARKShot/                   Application source
  App.xaml(.cs)              Entry point, tray, hotkeys, theme system
  MainWindow.xaml(.cs)       Frame-navigation host with Mica backdrop
  Views/                     WPF Pages and UserControls
  Services/                  Capture, hotkeys, tray, watermark, persistence
  Models/                    POCO mirrors of the macOS settings schema
  Helpers/                   Bitmap, color, P/Invoke utilities
  Assets/                    App icon and logo
scripts/                     NSIS installer script
.github/workflows/           CI build pipeline
```

The public repository intentionally keeps the source tree small. Local helper scripts, generated build output, packaged installers, signing material, and environment files are excluded by `.gitignore`.

## Before Publishing

Before pushing or tagging a public release, verify the repository contains only source files and public assets:

```powershell
git status --short
git check-ignore -v build scripts/build-output "build/Installer" || true
```

For distribution outside a tightly trusted group, sign the installer with a code-signing certificate (Microsoft SmartScreen warns on the first launch of any unsigned executable until enough users run it).

## License

QPARK Shot for Windows is open source under the [MIT License](LICENSE).

Copyright (c) 2026 QPARK.

## Contact

Questions, bug reports, and security concerns: [work@qpark.io](mailto:work@qpark.io)
