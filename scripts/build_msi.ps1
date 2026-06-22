# Build an MSI installer for QPARK Shot (Windows port) using WiX v4.
# Prereqs:
#   dotnet tool install --global wix
#   dotnet workload install wix  (one-time)
# Usage: pwsh scripts/build_msi.ps1

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))

$Version = "1.1.0"
$OutDir = "build/Msi"
$PublishDir = "build/Release"

# 1. Ensure Release publish exists
if (-not (Test-Path "$PublishDir\QPARKShot.exe"))
{
    Write-Host "==> No Release build found, running build_release.ps1"
    & "$PSScriptRoot/build_release.ps1"
}

# 2. Wipe + recreate output
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# 3. Run WiX v4 (`wix build` from .NET tool)
$msiPath = "$OutDir/QPARKShot-$Version.msi"
Write-Host "==> Building $msiPath via WiX"
wix build scripts/qparkshot.wxs `
    -define "PublishDir=$((Resolve-Path $PublishDir).Path)" `
    -o $msiPath

Write-Host ""
Write-Host "==> Done"
Write-Host "    MSI: $((Resolve-Path $msiPath).Path)"
Write-Host ""
Write-Host "    NOTE: unsigned MSI. SmartScreen may warn on first install."
Write-Host "    For distribution: signtool sign /fd SHA256 /tr <ts> ... $msiPath"
