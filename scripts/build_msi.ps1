# Build an MSI installer for QPARK Shot using WiX v4.
# Prereq: dotnet tool install --global wix --version 4.0.5
# Usage: pwsh scripts/build_msi.ps1

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))

$Version = "1.1.0"
$OutDir = "build/Msi"
$PublishDir = "build/Release"

if (-not (Test-Path "$PublishDir/QPARKShot.exe"))
{
    Write-Host "==> No Release build found, running build_release.ps1"
    & "$PSScriptRoot/build_release.ps1"
}

if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# WiX v4 globs are relative to the wxs file's directory, so copy the publish
# output into scripts/release-payload/ for harvesting.
$Payload = "scripts/release-payload"
if (Test-Path $Payload) { Remove-Item -Recurse -Force $Payload }
Copy-Item -Recurse $PublishDir $Payload

$msiPath = "$OutDir/QPARKShot-$Version.msi"
Write-Host "==> Building $msiPath via WiX"
wix build scripts/qparkshot.wxs -o $msiPath

Remove-Item -Recurse -Force $Payload

Write-Host ""
Write-Host "==> Done"
Write-Host "    MSI: $((Resolve-Path $msiPath).Path)"
Write-Host ""
Write-Host "    NOTE: unsigned MSI. SmartScreen may warn on first install."
