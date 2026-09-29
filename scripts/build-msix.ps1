param(
    [string]$PublishDirectory = 'build/Release',
    [string]$OutputDirectory = 'build/MSIX',
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [switch]$TestPackage,
    [switch]$IncludeTests
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$tools = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" | Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (!$tools) { throw 'Windows SDK MakeAppx.exe was not found.' }
if (!(Test-Path "$PublishDirectory/QPARKShot.exe")) { throw 'Publish QPARKShot before packaging.' }
if ($TestPackage) {
    $IdentityName = 'QPARK.Shot.Preview'
    $Publisher = 'CN=QPARK Shot Test'
    $PublisherDisplayName = 'QPARK'
    $displayName = 'QPARK Shot Preview'
} else {
    if (!$IdentityName -or !$Publisher -or !$PublisherDisplayName) { throw 'All three Product identity values from Partner Center are required for a Store package.' }
    if ($IdentityName -eq 'QPARK.Shot.Preview' -or $Publisher -eq 'CN=QPARK Shot Test') { throw 'Test identity cannot be used for Store submissions.' }
    $displayName = 'QPARK Shot'
}
# Each build starts with an empty stage, including when the output directory is reused.
$stage = Join-Path $OutputDirectory ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path "$stage/app", $OutputDirectory | Out-Null
Copy-Item "$PublishDirectory/*" "$stage/app" -Recurse -Force
Copy-Item 'packaging/Assets' $stage -Recurse -Force
[xml]$manifest = Get-Content 'packaging/AppxManifest.xml' -Raw
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Properties.DisplayName = $displayName
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifest.Package.Applications.Application.VisualElements.DisplayName = $displayName
if ($TestPackage -and $IncludeTests) {
    Copy-Item 'build/Tests' "$stage/tests" -Recurse -Force
    $testApp = $manifest.Package.Applications.Application.CloneNode($true)
    $testApp.Id = 'RegressionTests'
    $testApp.Executable = 'tests\QPARKShot.Tests.exe'
    $testApp.VisualElements.DisplayName = 'QPARK Shot Regression Tests'
    [void]$manifest.Package.Applications.AppendChild($testApp)
}
$manifest.Save((Join-Path (Resolve-Path $stage) 'AppxManifest.xml'))
$suffix = if ($TestPackage) { 'Preview' } else { 'Store' }
$package = Join-Path $OutputDirectory "QPARKShot-1.2.0-x64-$suffix.msix"
& $tools.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx validation or packaging failed.' }
if ($TestPackage) {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName 'QPARK Shot CI only' -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
    try {
        $signtool = Join-Path $tools.Directory.FullName 'signtool.exe'
        & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $package
        if ($LASTEXITCODE -ne 0) { throw 'Test package signing failed.' }
        Export-Certificate -Cert $certificate -FilePath "$OutputDirectory/QPARKShot-Test.cer" | Out-Null
        # The public certificate is imported only by the isolated installation smoke test.
    } finally { Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" }
}
& $tools.FullName unpack /p $package /d "$OutputDirectory/verification" /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX unpack verification failed.' }
Get-FileHash $package -Algorithm SHA256 | Format-List
Write-Output "Package: $package"
