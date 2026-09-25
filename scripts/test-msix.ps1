$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$qa = New-Item -ItemType Directory -Force build/QA
$certificate = Import-Certificate -FilePath build/MSIX/QPARKShot-Test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
$package = $null
try {
    Add-AppxPackage -Path build/MSIX/QPARKShot-1.2.0-x64-Preview.msix
    $package = Get-AppxPackage -Name QPARK.Shot.Preview
    if (!$package) { throw 'Package registration was not found.' }
    $manifest = Get-AppxPackageManifest $package
    $package | Select-Object Name, Version, PackageFamilyName, Status | ConvertTo-Json | Set-Content build/QA/installed-package.json
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PackageLauncher {
 [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IApplicationActivationManager {
  int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string app, [MarshalAs(UnmanagedType.LPWStr)] string args, uint options, out uint pid);
  int ActivateForFile(IntPtr a, IntPtr b, IntPtr c, out uint pid);
  int ActivateForProtocol(IntPtr a, IntPtr b, out uint pid);
 }
 public static uint Start(string app, string args) {
  var manager = (IApplicationActivationManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
  uint pid; Marshal.ThrowExceptionForHR(manager.ActivateApplication(app, args, 0, out pid)); return pid;
 }
}
'@
    $appPid = [PackageLauncher]::Start("$($package.PackageFamilyName)!App", '')
    Start-Sleep -Seconds 5
    $process = Get-Process -Id $appPid -ErrorAction Stop
    if ($process.HasExited) { throw 'Installed application exited unexpectedly.' }
    "Installed app launched, PID $appPid" | Set-Content build/QA/installed-launch.txt
    Stop-Process -Id $appPid
    $resultDir = Join-Path $env:TEMP 'QPARKShot-Package-QA'
    $testPid = [PackageLauncher]::Start("$($package.PackageFamilyName)!RegressionTests", "`"$resultDir`"")
    $testProcess = Get-Process -Id $testPid -ErrorAction SilentlyContinue
    if ($testProcess -and !$testProcess.WaitForExit(180000)) { Stop-Process -Id $testPid; throw 'Packaged regression checks timed out.' }
    Copy-Item $resultDir build/QA/packaged -Recurse -Force
    $results = Get-Content "$resultDir/results.json" -Raw | ConvertFrom-Json
    if (!$results.success -or !$results.packaged) { throw 'Packaged regression or OCR checks failed. See QA artifact.' }
} catch {
    $_ | Out-String | Set-Content build/QA/msix-install-failure.txt
    throw
} finally {
    if ($package) { Remove-AppxPackage -Package $package.PackageFullName }
    Remove-Item "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)" -ErrorAction SilentlyContinue
}
