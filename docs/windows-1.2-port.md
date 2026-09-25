# Windows 1.2 port and MSIX

Reference: the current working source of `qpark-shot` for macOS, including its September 2026 changes. The macOS working tree is a read-only reference. This change applies to the Windows WPF application and its packaging workflow.

## Purpose and scope

Replace the Windows 1.1 gallery/editor navigation with a persistent workspace: Library, Current Session, Favorites, Recent and Missing. Bring over per-capture drafts, crop-aware undo/redo, review and export presets, local OCR, localized settings, current branding, safer cleanup and observable errors.

Keep the existing Windows screenshot/hotkey/tray platform implementation, PNG files, watermark configuration and `%APPDATA%\QPARK Shot\settings.json` contract. Add defaults for fields absent from old settings. Do not reset user data or rewrite the macOS project.

The scope includes the application, its JSON stores, capture and selection overlays, rendering, export, settings and menus, the Windows build workflow, MSIX assets/manifest and regression harness. There is no server, database, account, telemetry or external OCR service.

## Intentional behavior changes

- Selecting a library card selects its inspector; double click or Enter opens the editor.
- Navigation retains library filters and per-image drafts. Settings open in their own window.
- Crop and annotations share history. Preview, copy, share, pin and PNG export use one renderer.
- Redaction, blur and numbered callouts supplement the existing annotation tools.
- Saved-file deletion and cleanup use the Recycle Bin; cleanup excludes active session paths and favorites.
- A failed capture restores the main window. Duplicate capture requests are ignored until the active request completes.
- Export failures stay visible and duplicate filenames never overwrite previous files.
- Windows OCR runs locally, serially across the complete library. Package identity and installed Windows OCR languages are required; unavailable languages are reported. Cancellation, file timestamps and language signatures prevent stale results being committed.
- The primary Store distribution format is MSIX. NSIS remains a source-level option for an ordinary installer; Windows OCR requires the packaged version.

## Data and compatibility

Settings and the new library index are atomically replaced with backups. Existing settings fields remain readable, and new fields have defaults. Screenshots remain at their current paths. Favorites, tags and missing-file records are retained when a storage root is unavailable. Relinking preserves metadata. QA uses explicit isolated folders and never points at a real user's gallery.

The MSIX full-trust app retains the old Roaming AppData path so Windows can read pre-package settings through its AppData compatibility behavior. Upgrade and uninstall behavior still needs a Windows client check; a runner installation is not proof for every supported Windows version. Existing NSIS installations are not automatically uninstalled by MSIX.

## Verification and completion

`QPARKShot.Tests` is a Windows STA regression executable with application resources. It checks old settings, backups, draft isolation, history, filenames, rendering, cleanup ownership, index reconciliation/relinking, UI navigation and localized layouts. It writes machine-readable results and WPF-rendered PNGs to the chosen QA folder.

The Windows workflow compiles and runs the harness, publishes self-contained x64 binaries, packages an internal MSIX containing the app and harness, installs it, activates the real app, and runs the harness under package identity including Windows OCR. The final Preview MSIX excludes the harness. A failed check fails the workflow and retains available evidence.

Store identity must come from Partner Center. Without those values the result is a test-signed Preview, not a Store submission. Store upload, certification and publishing are separate actions. Multi-monitor/mixed-DPI capture, real shortcut conflicts and sharing to third-party applications require interactive Windows validation.
