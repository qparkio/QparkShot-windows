# Build a Release exe of QPARK Shot (Windows port).
# Usage: pwsh scripts/build_release.ps1

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))

$OutDir = "build/Release"
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "==> dotnet publish (Release, win-x64, unpackaged WinUI 3)"
dotnet publish QPARKShot\QPARKShot.csproj `
    -c Release -r win-x64 --self-contained false `
    -o $OutDir

Write-Host ""
Write-Host "==> Done"
Write-Host "    Exe:  $((Resolve-Path $OutDir).Path)\QPARKShot.exe"
Write-Host "    Runtime needed on target: Windows App SDK 1.5 + .NET 8 Desktop Runtime"
