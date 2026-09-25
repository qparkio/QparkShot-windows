# Build MSIX from a Mac

Edit the sources on macOS and push a branch, or start the `Build Windows` workflow using `gh workflow run build-windows.yml --ref <branch>`. Compilation, WPF checks and MSIX packaging run on Windows Server 2022 in GitHub Actions. Download the `QPARKShot-Windows-1.2.0` artifact to the Mac after completion.

The artifact contains `MSIX/QPARKShot-1.2.0-x64-Preview.msix`, the public test certificate and `QA` evidence. The Preview is a self-signed test build with a separate identity. The internal validation package includes the regression executable; the delivered Preview does not.

## Microsoft Store package

Reserve QPARK Shot in Partner Center and copy these fields from Product management → Product identity:

- `Package/Identity/Name` → workflow input `identity_name`
- `Package/Identity/Publisher` → `publisher`
- `Package/Properties/PublisherDisplayName` → `publisher_display_name`

Start the workflow with all three inputs to generate `Store/QPARKShot-1.2.0-x64-Store.msix`. No CA certificate is required for this Store-only package. Microsoft signs the submitted package after certification. Do not submit the Preview identity or its test certificate.

The Store package has only the main application and the `runFullTrust` capability required for desktop capture, hotkeys and local files. It does not request elevation or download runtime prerequisites; .NET is included.

## Local Windows commands

```powershell
dotnet publish QPARKShot/QPARKShot.csproj -c Release -r win-x64 --self-contained true -o build/Release
./scripts/build-msix.ps1 -TestPackage
# Store (use the exact Partner Center values):
./scripts/build-msix.ps1 -OutputDirectory build/Store -IdentityName '<Name>' -Publisher '<Publisher>' -PublisherDisplayName '<Display name>'
```

To test a Preview on a dedicated Windows account, trust only the supplied **public** `.cer` in Local Machine → Trusted People, then install its `.msix`. Windows OCR language components are managed in Windows Settings → Language & region. Preview and Store have different identities; testing the Preview does not establish Store approval or an update path for the Store app.

## Required interactive checks

Install on a supported Windows client, exercise capture with 100/150/200% scaling and multiple monitors, verify native sharing/clipboard and hotkeys, then upgrade an existing 1.1 NSIS installation with real settings and a backup of its library. Verify preferences, gallery paths and saved PNGs after the transition. Run Windows App Certification Kit before submission.

The build scripts never submit to Partner Center or publish a GitHub release.

Sources: [MSIX identity](https://learn.microsoft.com/en-us/windows/apps/publish/view-app-identity-details), [Store signing](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements), [AppData compatibility](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes), [Windows OCR](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr).
