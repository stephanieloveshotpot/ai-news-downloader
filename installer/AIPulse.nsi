; AI Pulse — custom Windows installer (NSIS 3, Modern UI 2)
;
; Compile:  makensis /DVERSION=1.0.0 /DPAYLOAD=..\payload installer\AIPulse.nsi
;   PAYLOAD is the unzipped app bundle from the ai-news-desk repo:
;     PAYLOAD\app\AI Pulse.exe (+ resources\...)   the app with the scraper inside
;     PAYLOAD\legal\TERMS.txt, PRIVACY.txt           shown on the license pages
;
; Pages:  Welcome -> Terms of service (accept) -> Privacy policy (accept)
;         -> Startup options -> Install location -> Install -> Finish
;
; What it sets up on the PC:
;   %LOCALAPPDATA%\Programs\AI Pulse\        the app files (per-user, no admin needed)
;   %APPDATA%\ai-pulse\                      settings, cache, bookmarks (+ installer.json for first run)
;   Start menu folder "AI Pulse", desktop shortcut
;   HKCU ...\Uninstall\AIPulse               so it shows in "Installed apps"
;   HKCU ...\CurrentVersion\Run\AI Pulse     only if "launch with Windows" was ticked
;
; Silent mode (/S) is how the app's built-in updater upgrades itself:
;   "AI-Pulse-Setup-x.y.z.exe" /S /relaunch
; It keeps existing startup settings and, with /relaunch, starts the app again.

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef PAYLOAD
  !define PAYLOAD "..\payload"
!endif
!ifndef OUTDIR
  !define OUTDIR "..\dist"
!endif

!define APP_NAME      "AI Pulse"
!define APP_EXE       "AI Pulse.exe"
!define APP_ID        "AIPulse"
!define PUBLISHER     "Shako"
!define CONTACT       "mandarincat07@gmail.com"
!define URL           "https://github.com/stephanieloveshotpot/ai-news-desk"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
!define RUN_KEY       "Software\Microsoft\Windows\CurrentVersion\Run"
!define DATA_DIR      "$APPDATA\ai-pulse"

Name "${APP_NAME}"
OutFile "${OUTDIR}\AI-Pulse-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_ID}" "InstallDir"
BrandingText "${APP_NAME} ${VERSION} · ${PUBLISHER}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${APP_NAME} installer"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 ${PUBLISHER}"

!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

; ---------- look ----------
!define MUI_ICON "..\assets\icon.ico"
!define MUI_UNICON "..\assets\icon.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "..\assets\sidebar.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "..\assets\sidebar.bmp"
!define MUI_ABORTWARNING

; ---------- variables ----------
Var StartupDialog
Var ChkLaunchAtLogin
Var RadioPopLogin
Var RadioPopInterval
Var RadioPopNever
Var LaunchAtLogin
Var PopupMode

; ---------- installer pages ----------
!define MUI_WELCOMEPAGE_TITLE "Welcome to ${APP_NAME}"
!define MUI_WELCOMEPAGE_TEXT "${APP_NAME} collects the day's AI news from Hacker News, arXiv, company blogs and the tech press, and pops up when you sit down at your PC.$\r$\n$\r$\nNothing leaves your computer. The next two pages are the terms of service and the privacy policy; you'll need to accept both to continue."
!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_HEADER_TEXT "Terms of service"
!define MUI_PAGE_HEADER_SUBTEXT "Please read the terms before installing ${APP_NAME}."
!define MUI_LICENSEPAGE_TEXT_TOP "Scroll to read the whole agreement."
!define MUI_LICENSEPAGE_CHECKBOX
!define MUI_LICENSEPAGE_CHECKBOX_TEXT "I accept the terms of service"
!insertmacro MUI_PAGE_LICENSE "${PAYLOAD}\legal\TERMS.txt"

!define MUI_PAGE_HEADER_TEXT "Privacy policy"
!define MUI_PAGE_HEADER_SUBTEXT "How ${APP_NAME} handles your information: it stays on this PC."
!define MUI_LICENSEPAGE_TEXT_TOP "Scroll to read the whole policy."
!define MUI_LICENSEPAGE_CHECKBOX
!define MUI_LICENSEPAGE_CHECKBOX_TEXT "I accept the privacy policy"
!insertmacro MUI_PAGE_LICENSE "${PAYLOAD}\legal\PRIVACY.txt"

Page custom StartupOptionsCreate StartupOptionsLeave

!define MUI_DIRECTORYPAGE_TEXT_TOP "${APP_NAME} installs for your user account only, so no administrator password is needed. Change the folder if you want it somewhere else."
!insertmacro MUI_PAGE_DIRECTORY

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE "${APP_NAME} is installed"
!define MUI_FINISHPAGE_TEXT "The app lives in the system tray next to the clock. Closing its window hides it; use Quit in the tray menu to stop it.$\r$\n$\r$\nYou can change the pop-up schedule and startup behaviour any time in Settings."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Open ${APP_NAME} now"
!insertmacro MUI_PAGE_FINISH

; ---------- uninstaller pages ----------
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---------- defaults for silent installs (no pages are shown) ----------
Function .onInit
  StrCpy $LaunchAtLogin ${BST_CHECKED}
  StrCpy $PopupMode "login"
FunctionEnd

; ---------- startup options page ----------
Function StartupOptionsCreate
  !insertmacro MUI_HEADER_TEXT "Startup options" "Choose when ${APP_NAME} should appear. You can change this later in Settings or from the tray icon."
  nsDialogs::Create 1018
  Pop $StartupDialog
  ${If} $StartupDialog == error
    Abort
  ${EndIf}

  ${NSD_CreateCheckbox} 0 0 100% 12u "Launch ${APP_NAME} when Windows starts"
  Pop $ChkLaunchAtLogin
  ${NSD_Check} $ChkLaunchAtLogin

  ${NSD_CreateLabel} 0 24u 100% 12u "Pop up the news window:"
  Pop $0

  ${NSD_CreateRadioButton} 10u 40u 100% 12u "When I log in to Windows (recommended)"
  Pop $RadioPopLogin
  ${NSD_Check} $RadioPopLogin
  ${NSD_CreateRadioButton} 10u 56u 100% 12u "Every 4 hours (adjustable in Settings)"
  Pop $RadioPopInterval
  ${NSD_CreateRadioButton} 10u 72u 100% 12u "Never, I'll open it from the tray icon"
  Pop $RadioPopNever

  ${NSD_CreateLabel} 0 100u 100% 24u "${APP_NAME} sits in the system tray. Closing the window hides it; use Quit in the tray menu to stop it completely."
  Pop $0

  nsDialogs::Show
FunctionEnd

Function StartupOptionsLeave
  ${NSD_GetState} $ChkLaunchAtLogin $LaunchAtLogin
  ${NSD_GetState} $RadioPopLogin $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $PopupMode "login"
  ${Else}
    ${NSD_GetState} $RadioPopInterval $0
    ${If} $0 == ${BST_CHECKED}
      StrCpy $PopupMode "interval"
    ${Else}
      StrCpy $PopupMode "never"
    ${EndIf}
  ${EndIf}
FunctionEnd

; ---------- install ----------
Section "Install"
  SetDetailsPrint both

  ; Stop a running copy so files can be replaced (upgrade in place).
  DetailPrint "Closing any running ${APP_NAME}…"
  ${If} ${Silent}
    Sleep 2000   ; when the app updates itself it is already quitting; let it finish
  ${EndIf}
  nsExec::ExecToLog 'taskkill /F /IM "${APP_EXE}" /T'
  Pop $0
  Sleep 800

  ; 1. Program files
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "${PAYLOAD}\app\*.*"
  WriteUninstaller "$INSTDIR\Uninstall ${APP_NAME}.exe"
  WriteRegStr HKCU "Software\${APP_ID}" "InstallDir" "$INSTDIR"

  ; 2. Data folder the app expects, plus the first-run choices.
  ;    A silent run over an existing install is the app updating itself:
  ;    keep the person's startup settings exactly as they are.
  CreateDirectory "${DATA_DIR}"
  ${If} ${Silent}
  ${AndIf} ${FileExists} "${DATA_DIR}\settings.json"
    DetailPrint "Upgrade: keeping existing startup settings"
    Goto shortcuts
  ${EndIf}
  ${If} $LaunchAtLogin == ${BST_CHECKED}
    StrCpy $1 "true"
    WriteRegStr HKCU "${RUN_KEY}" "${APP_NAME}" '"$INSTDIR\${APP_EXE}" --hidden'
  ${Else}
    StrCpy $1 "false"
    DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
  ${EndIf}
  FileOpen $2 "${DATA_DIR}\installer.json" w
  FileWrite $2 '{"launchAtLogin": $1, "popupMode": "$PopupMode", "termsAccepted": true, "installerVersion": "${VERSION}"}'
  FileClose $2

  ; 3. Shortcuts
  shortcuts:
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall ${APP_NAME}.exe"
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  ; 4. "Installed apps" entry
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKCU "${UNINST_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINST_KEY}" "Contact" "${CONTACT}"
  WriteRegStr   HKCU "${UNINST_KEY}" "URLInfoAbout" "${URL}"
  WriteRegStr   HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall ${APP_NAME}.exe"'
  WriteRegStr   HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall ${APP_NAME}.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" $0
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1

  ; 5. The in-app updater runs "setup.exe /S /relaunch": start the new version.
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/relaunch" $1
  ${IfNot} ${Errors}
    Exec '"$INSTDIR\${APP_EXE}"'
  ${EndIf}
SectionEnd

; ---------- uninstall ----------
Section "Uninstall"
  nsExec::ExecToLog 'taskkill /F /IM "${APP_EXE}" /T'
  Pop $0
  Sleep 800

  DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"

  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  ; Settings and bookmarks are kept unless the person asks, so a reinstall picks them up.
  IfSilent +3
  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your ${APP_NAME} settings and bookmarks?$\r$\n($\"${DATA_DIR}$\")" IDNO +2
  RMDir /r "${DATA_DIR}"
SectionEnd
