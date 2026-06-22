; QPARK Shot — NSIS installer (per-user, no admin required)
; Compile with: makensis scripts/installer.nsi
; Expects build artifacts at build/Release/ relative to this script.

Unicode true
SetCompressor /SOLID lzma

!define APP_NAME      "QPARK Shot"
!define APP_VERSION   "1.1.0"
!define APP_PUBLISHER "QPARK"
!define APP_EXE       "QPARKShot.exe"
!define APP_URL       "https://qpark.io"
!define APP_REG_KEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\QPARKShot"

Name "${APP_NAME}"
OutFile "..\build\Installer\QPARKShot-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\QPARK Shot"
InstallDirRegKey HKCU "Software\QPARK\QPARKShot" "InstallDir"
RequestExecutionLevel user
BrandingText "${APP_PUBLISHER} — ${APP_NAME} ${APP_VERSION}"
ShowInstDetails show
ShowUninstDetails show

!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON   "..\QPARKShot\Assets\AppIcon.ico"
!define MUI_UNICON "..\QPARKShot\Assets\AppIcon.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\app\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section "QPARK Shot" SecMain
    SectionIn RO

    ; Put all runtime files into a subfolder so the install root stays clean.
    SetOutPath "$INSTDIR\app"
    File /r "..\build\Release\*.*"

    ; Start Menu shortcut.
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
                    "$INSTDIR\app\${APP_EXE}" "" "$INSTDIR\app\${APP_EXE}" 0

    ; Desktop shortcut.
    CreateShortCut  "$DESKTOP\${APP_NAME}.lnk" \
                    "$INSTDIR\app\${APP_EXE}" "" "$INSTDIR\app\${APP_EXE}" 0

    SetOutPath "$INSTDIR"

    ; Registry: install dir + uninstaller entry in Add/Remove Programs.
    WriteRegStr HKCU "Software\QPARK\QPARKShot" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "Software\QPARK\QPARKShot" "Version" "${APP_VERSION}"

    WriteRegStr HKCU "${APP_REG_KEY}" "DisplayName"      "${APP_NAME}"
    WriteRegStr HKCU "${APP_REG_KEY}" "DisplayVersion"   "${APP_VERSION}"
    WriteRegStr HKCU "${APP_REG_KEY}" "DisplayIcon"      "$INSTDIR\app\${APP_EXE}"
    WriteRegStr HKCU "${APP_REG_KEY}" "Publisher"        "${APP_PUBLISHER}"
    WriteRegStr HKCU "${APP_REG_KEY}" "URLInfoAbout"     "${APP_URL}"
    WriteRegStr HKCU "${APP_REG_KEY}" "InstallLocation"  "$INSTDIR"
    WriteRegStr HKCU "${APP_REG_KEY}" "UninstallString"  '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${APP_REG_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegDWORD HKCU "${APP_REG_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${APP_REG_KEY}" "NoRepair" 1

    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
    ; Stop running instance if any.
    nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'

    ; Remove shortcuts.
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    RMDir  "$SMPROGRAMS\${APP_NAME}"
    Delete "$DESKTOP\${APP_NAME}.lnk"

    ; Remove install dir.
    RMDir /r "$INSTDIR"

    ; Clean registry.
    DeleteRegKey HKCU "${APP_REG_KEY}"
    DeleteRegKey HKCU "Software\QPARK\QPARKShot"
SectionEnd
