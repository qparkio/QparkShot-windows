# macOS 1.2 → Windows 1.2

| Area | Windows implementation |
|---|---|
| Workspace | Persistent Library, Current Session, Favorites, Recent, Missing; retained search and selection |
| Library metadata | Local index, favorites, tags, filename/OCR search, relinking and forgetting missing files |
| OCR | Local Windows.Media.Ocr, sequential backlog, cancellation and timestamp/language invalidation; requires MSIX and an installed OCR language |
| Navigation | Separate preferences window, retained drafts, review/editor return to workspace |
| Editor | Freehand, arrows, rectangles, text, numbered callouts, redaction, blur, crop; shared crop/annotation undo/redo |
| Export | Clean, Watermarked and Support presets; filename template; collision-safe PNG; copy, Windows Share and floating pin |
| Capture | Selection, primary full screen, window, last region, delay; reentry protection and cancellation recovery |
| Shortcuts | Two configurable global shortcuts, invalid/duplicate validation and registration feedback |
| Cleanup | Owned temp files only; saved files to Recycle Bin; excludes current session and favorites |
| Appearance | Current camera icon, light/dark/system, 12 imported language tables, RTL for Arabic |
| Windows distribution | Self-contained .NET 8 WPF x64, MSIX preview and parameterized Store package |

Differences are platform-specific: Windows OCR instead of Apple Vision, Windows Share instead of NSSharingServicePicker, Recycle Bin instead of Trash, Win32 capture/shortcuts instead of macOS APIs. Blur uses a reduced-resolution bilinear filter; opaque redaction is available when hiding confidential content.

See `windows-1.2-port.md` for compatibility, scope and verification boundaries and `msix.md` for packaging instructions. Runtime results and screenshots are emitted by GitHub Actions; platform support is not implied by source parity alone.
