$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path $PSScriptRoot -Parent)
$publish = (Resolve-Path 'build/Release').Path
$version = ([xml](Get-Content QPARKShot/QPARKShot.csproj -Raw)).Project.PropertyGroup.Version
$compiler = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
if (!(Test-Path $compiler)) { throw 'Install NSIS before building the standalone installer.' }
if (!(Test-Path "$publish/QPARKShot.exe")) { throw 'Publish the Windows x64 application first.' }
if (Get-ChildItem $publish -Recurse -Filter 'QPARKShot.Tests*') { throw 'Test harness must not be shipped.' }
New-Item -ItemType Directory -Force build/Installer, build/GitHub | Out-Null

# List exact application files instead of recursively deleting a user-selected install folder.
$uninstall = @(Get-ChildItem $publish -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($publish, $_.FullName).Replace('$', '$$')
    'Delete "$INSTDIR\app\' + $relative + '"'
})
$uninstall += @(Get-ChildItem $publish -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($publish, $_.FullName).Replace('$', '$$')
    'RMDir "$INSTDIR\app\' + $relative + '"'
})
$uninstall += 'RMDir "$INSTDIR\app"'
$uninstall | Set-Content build/Installer/uninstall-files.nsh -Encoding utf8
& $compiler /V3 scripts/installer.nsi
if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed.' }
$installer = "QPARKShot-Setup-$version.exe"
Copy-Item "build/Installer/$installer" build/GitHub -Force
Copy-Item docs/releases/1.2.0.md build/GitHub/README.txt -Force
$hash = (Get-FileHash "build/GitHub/$installer" -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $installer" | Set-Content build/GitHub/SHA256SUMS.txt -Encoding ascii
Write-Output "GitHub installer: build/GitHub/$installer"
