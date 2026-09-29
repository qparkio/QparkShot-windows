$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Run installer integration checks only on a disposable Windows GitHub runner.'
}
Set-Location (Split-Path $PSScriptRoot -Parent)
$qa = New-Item -ItemType Directory -Force build/QA/installer
$testRoot = Join-Path $env:TEMP ('QPARKShot-Installer-QA-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $testRoot 'App with spaces'
$settings = Join-Path $env:APPDATA 'QPARK Shot'
$reg = 'HKCU:\Software\QPARK\QPARKShot'
$uninstallReg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\QPARKShot'
if ((Test-Path $settings) -or (Test-Path $reg) -or (Test-Path $uninstallReg)) {
    throw 'Runner has existing QPARK Shot data; refusing to overwrite it.'
}
$installer = (Resolve-Path build/GitHub/QPARKShot-Setup-1.2.0.exe).Path
$passed = [Collections.Generic.List[string]]::new()
$app = $null
$success = $false
function Check($condition, [string]$name) {
    if (!$condition) { throw "FAILED: $name" }
    $passed.Add($name)
    Write-Output "PASS $name"
}
function Run-Setup([string]$path, [string]$arguments, [int]$expected = 0) {
    $process = Start-Process -FilePath $path -ArgumentList $arguments -PassThru
    if (!$process.WaitForExit(120000)) { Stop-Process -Id $process.Id; throw 'Installer timed out.' }
    if ($process.ExitCode -ne $expected) { throw "Installer exit code $($process.ExitCode), expected $expected." }
}
function Start-App {
    $script:app = Start-Process "$install/app/QPARKShot.exe" -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $script:app.Refresh()
        if ($script:app.HasExited) { throw 'Installed application exited during startup.' }
    } while ($script:app.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    Check ($script:app.MainWindowHandle -ne 0) 'Installed EXE opens its main window'
}
try {
    New-Item -ItemType Directory -Force $testRoot | Out-Null
    Run-Setup $installer "/S /D=$install"
    Check ((Get-Item "$install/app/QPARKShot.exe").VersionInfo.ProductVersion -like '1.2.0*') 'Clean install has version 1.2.0'
    Check ((Get-ItemProperty $uninstallReg).DisplayVersion -eq '1.2.0') 'Add/Remove Programs registers version 1.2.0'
    Check (!(Get-ChildItem "$install/app" -Recurse -Filter 'QPARKShot.Tests*')) 'Installer contains no regression executable'
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'QPARK Shot/QPARK Shot.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $target = $shell.CreateShortcut($shortcut).TargetPath
    @{ shortcut = $shortcut; target = $target; expected = "$install\app\QPARKShot.exe" } | ConvertTo-Json | Set-Content "$qa/shortcut.json"
    # The runner TEMP path can contain RUNNER~1 while the shell expands it to runneradmin.
    $fileSystem = New-Object -ComObject Scripting.FileSystemObject
    Check ((Test-Path $target) -and $fileSystem.GetFile($target).ShortPath -eq $fileSystem.GetFile("$install\app\QPARKShot.exe").ShortPath) 'Start Menu shortcut targets installed application'
    Start-App
    Run-Setup $installer '/S' 2
    Run-Setup "$install/Uninstall.exe" "/S _?=$install" 2
    $app.Refresh()
    Check (!$app.HasExited) 'Install and uninstall refuse to kill a running session'
    Stop-Process -Id $app.Id
    $app.WaitForExit()
    $app = $null
    Run-Setup "$install/Uninstall.exe" "/S _?=$install"
    Check (!(Test-Path "$install/app/QPARKShot.exe") -and !(Test-Path $uninstallReg)) 'Clean uninstall removes application and registration'

    $legacy = Join-Path $testRoot 'QPARKShot-Setup-1.1.0.exe'
    Invoke-WebRequest 'https://github.com/qparkio/QparkShot-windows/releases/download/v1.1.0/QPARKShot-Setup-1.1.0.exe' -OutFile $legacy
    Check ((Get-FileHash $legacy).Hash.ToLowerInvariant() -eq '628138d43b6b6e1c103891451902a435814bf3d96aaa2eda0663ff916268b259') 'Published 1.1 installer matches recorded SHA-256'
    Run-Setup $legacy "/S /D=$install"
    Check ((Get-ItemProperty $uninstallReg).DisplayVersion -eq '1.1.0') 'Published 1.1 installer registered before upgrade'
    New-Item -ItemType Directory -Force $settings, "$testRoot/Pictures" | Out-Null
    $legacySettings = @{
        themePreference = 'dark'; hotkey = @{ enabled = $false }
        watermark = @{ text = @{ enabled = $true; text = 'NSIS-UPGRADE-KEEP' } }
        cleanup = @{ mode = 'never'; saveDirectory = "$testRoot/Pictures" }
        gallery = @{ ocrEnabled = $false }
    } | ConvertTo-Json -Depth 5
    $legacySettings | Set-Content "$settings/settings.json" -Encoding utf8
    $preserved = @("$settings/settings.json", "$testRoot/Pictures/keep.png", "$install/keep.png", "$install/app/keep.png")
    foreach ($path in $preserved | Select-Object -Skip 1) { Copy-Item QPARKShot/Assets/Logo.png $path }
    $hashes = @{}
    foreach ($path in $preserved) { $hashes[$path] = (Get-FileHash $path).Hash }
    Run-Setup $installer '/S'
    Check ((Get-ItemProperty $reg).InstallDir -eq $install) 'Upgrade reuses the existing custom install directory'
    Check ((Get-Item "$install/app/QPARKShot.exe").VersionInfo.ProductVersion -like '1.2.0*') 'Upgrade replaces 1.1 executable with 1.2.0'
    Start-App
    Stop-Process -Id $app.Id
    $app.WaitForExit()
    $app = $null
    foreach ($path in $preserved) { Check ((Get-FileHash $path).Hash -eq $hashes[$path]) "Upgrade preserves $([IO.Path]::GetRelativePath($testRoot, $path))" }
    Run-Setup "$install/Uninstall.exe" "/S _?=$install"
    foreach ($path in $preserved) { Check ((Get-FileHash $path).Hash -eq $hashes[$path]) "Uninstall preserves $([IO.Path]::GetRelativePath($testRoot, $path))" }
    Check (!(Test-Path "$install/app/QPARKShot.exe") -and !(Test-Path $reg) -and !(Test-Path $uninstallReg)) 'Uninstall after upgrade removes the application and registry entries'
    Check (!(Test-Path $shortcut)) 'Uninstall removes Start Menu shortcut'
    $success = $true
} catch {
    $_ | Out-String | Set-Content "$qa/failure.txt"
    throw
} finally {
    if ($app -and !$app.HasExited) { Stop-Process -Id $app.Id }
    @{ success = $success; passed = @($passed.ToArray()) } | ConvertTo-Json -Depth 4 | Set-Content "$qa/results.json"
    if (Test-Path "$env:TEMP/qparkshot-debug.log") { Copy-Item "$env:TEMP/qparkshot-debug.log" "$qa/startup.log" }
    # Leave test data and failed installations for the disposable runner to discard.
}
