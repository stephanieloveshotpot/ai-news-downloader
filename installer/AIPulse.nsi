; AI Pulse — Windows installer (NSIS 3, Modern UI 2)
;
; Compile:  makensis /DVERSION=1.0.0 /DPAYLOAD=..\payload installer\AIPulse.nsi
;   PAYLOAD is the unzipped app bundle from the ai-news-desk repo:
;     PAYLOAD\app\AI Pulse.exe (+ resources\...)   the app with the scraper inside
;
; The installer itself is one small dark window with a progress bar. The
; welcome, terms, privacy and startup choices are AI Pulse's own first-run
; setup, which opens as soon as the files are in place (AI Pulse.exe --setup).
;
; Modes:
;   (no switches)        install, then open AI Pulse's setup
;   /update /relaunch    the in-app updater: same window titled "Updating",
;                        keeps everything, then starts the new version
;   /S [/relaunch]       silent (CI smoke test, update-on-quit)
;   /D=C:\path           install somewhere else (must be the last switch)
;
; makensis /DPREVIEW builds a look-only installer that shows the window and
; progress but writes nothing (for screenshots and design work).
;
; What it sets up on the PC:
;   %LOCALAPPDATA%\Programs\AI Pulse\        the app files (per-user, no admin needed)
;   Start menu folder "AI Pulse", desktop shortcut
;   HKCU ...\Uninstall\AIPulse               so it shows in "Installed apps"
; Launch-at-login is set by the app itself (HKCU ...\Run\AI Pulse).

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user
ManifestDPIAware true

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

; colours (match the app's dark theme)
!define BG    "0F1411"
!define CARD  "161D19"
!define INK   "E8EEE8"
!define MUTED "85958C"

Name "${APP_NAME}"
Caption "${APP_NAME} Setup"
UninstallCaption "Remove ${APP_NAME}"
OutFile "${OUTDIR}\AI-Pulse-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_ID}" "InstallDir"
BrandingText " "
ShowInstDetails nevershow
ShowUninstDetails nevershow
AutoCloseWindow true

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${APP_NAME} installer"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 ${PUBLISHER}"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

Var IsUpdate

; ---------- look ----------
!define MUI_ICON "..\assets\icon.ico"
!define MUI_UNICON "..\assets\icon.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "..\assets\header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "..\assets\header.bmp"
!define MUI_HEADERIMAGE_BITMAP_STRETCH "AspectFitHeight"
!define MUI_HEADERIMAGE_UNBITMAP_STRETCH "AspectFitHeight"
!define MUI_BGCOLOR "${BG}"
!define MUI_TEXTCOLOR "${INK}"
!define MUI_HEADER_TRANSPARENT_TEXT
!define MUI_INSTFILESPAGE_COLORS "${INK} ${CARD}"
!define MUI_INSTFILESPAGE_PROGRESSBAR "smooth"
!define MUI_ABORTWARNING
!define MUI_ABORTWARNING_TEXT "Stop installing ${APP_NAME}?"

; ---------- the one page ----------
!define MUI_PAGE_CUSTOMFUNCTION_SHOW ProgressShow
!define MUI_INSTFILESPAGE_FINISHHEADER_TEXT "Done"
!define MUI_INSTFILESPAGE_FINISHHEADER_SUBTEXT "Opening ${APP_NAME}…"
!insertmacro MUI_PAGE_INSTFILES

!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.ProgressShow
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.ProgressShow
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; Paints the window in the app's colours, hides the Back/Next buttons and the
; divider lines, and sets the header text for install or update.
!macro DarkWindow
  SetCtlColors $HWNDPARENT ${INK} ${BG}
  FindWindow $0 "#32770" "" $HWNDPARENT
  SetCtlColors $0 ${INK} ${BG}
  ; header title/subtitle, branding text, divider lines
  GetDlgItem $1 $HWNDPARENT 1037
  SetCtlColors $1 ${INK} ${BG}
  GetDlgItem $1 $HWNDPARENT 1038
  SetCtlColors $1 ${MUTED} ${BG}
  GetDlgItem $1 $HWNDPARENT 1028
  SetCtlColors $1 ${MUTED} ${BG}
  GetDlgItem $1 $HWNDPARENT 1035
  ShowWindow $1 ${SW_HIDE}
  GetDlgItem $1 $HWNDPARENT 1036
  ShowWindow $1 ${SW_HIDE}
  GetDlgItem $1 $HWNDPARENT 1256
  ShowWindow $1 ${SW_HIDE}
  GetDlgItem $1 $HWNDPARENT 1045
  ShowWindow $1 ${SW_HIDE}
  ; inner page: status line, progress bar, (hidden) details list
  GetDlgItem $1 $0 1006
  SetCtlColors $1 ${INK} ${BG}
  GetDlgItem $1 $0 1016
  SetCtlColors $1 ${INK} ${CARD}
  ; flat progress bar in the app's accent colour (needs the theme switched off)
  GetDlgItem $1 $0 1004
  System::Call 'uxtheme::SetWindowTheme(p r1, w " ", w " ")'
  SendMessage $1 0x409 0 0x00FF8C7A      ; PBM_SETBARCOLOR  #7A8CFF (COLORREF is BGR)
  SendMessage $1 0x2001 0 0x00262C22     ; PBM_SETBKCOLOR   #222C26
  ; dark title bar and dark Cancel button (Windows 10 1809+; ignored elsewhere)
  System::Call 'dwmapi::DwmSetWindowAttribute(p $HWNDPARENT, i 20, *i 1, i 4)'
  GetDlgItem $1 $HWNDPARENT 2
  System::Call 'uxtheme::SetWindowTheme(p r1, w "DarkMode_Explorer", p 0)'
!macroend

Function ProgressShow
  !insertmacro DarkWindow
  GetDlgItem $1 $HWNDPARENT 3       ; Back
  ShowWindow $1 ${SW_HIDE}
  GetDlgItem $1 $HWNDPARENT 1       ; Next
  ShowWindow $1 ${SW_HIDE}
  ${If} $IsUpdate == 1
    !insertmacro MUI_HEADER_TEXT "Updating ${APP_NAME}" "Installing version ${VERSION}. ${APP_NAME} reopens by itself in a moment."
  ${Else}
    !insertmacro MUI_HEADER_TEXT "Installing ${APP_NAME}" "Version ${VERSION} · this takes a few seconds, then ${APP_NAME} opens."
  ${EndIf}
FunctionEnd

Function un.ProgressShow
  !insertmacro DarkWindow
FunctionEnd

Function .onInit
  StrCpy $IsUpdate 0
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/update" $1
  ${IfNot} ${Errors}
    StrCpy $IsUpdate 1
  ${EndIf}
FunctionEnd

; ---------- install ----------
Section "Install"
  SetDetailsPrint textonly

  ; Stop a running copy so files can be replaced (upgrade in place). When the
  ; app updates itself it is already quitting; give it a moment first.
  DetailPrint "Closing ${APP_NAME}…"
  ${If} ${Silent}
  ${OrIf} $IsUpdate == 1
    Sleep 1200
  ${EndIf}
!ifndef PREVIEW
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}" /T'
  Pop $0
!endif
  Sleep 400

!ifdef PREVIEW
  DetailPrint "Copying ${APP_NAME}…"
  ${For} $9 1 12
    Sleep 450
    IntOp $8 $9 * 2400
    FindWindow $7 "#32770" "" $HWNDPARENT
    GetDlgItem $7 $7 1004
    SendMessage $7 ${PBM_SETPOS} $8 0
  ${Next}
  DetailPrint "Adding shortcuts…"
  Sleep 3000
!else
  ; 1. Program files
  DetailPrint "Copying ${APP_NAME}…"
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "${PAYLOAD}\app\*.*"
  WriteUninstaller "$INSTDIR\Uninstall ${APP_NAME}.exe"
  WriteRegStr HKCU "Software\${APP_ID}" "InstallDir" "$INSTDIR"
  CreateDirectory "${DATA_DIR}"

  ; 2. Shortcuts
  DetailPrint "Adding shortcuts…"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall ${APP_NAME}.exe"
  ${IfNot} $IsUpdate == 1
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  ${EndIf}

  ; 3. "Installed apps" entry
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

  ; 4. Start the app: the new version after an update, or the first-run setup
  ;    after a fresh install. A plain silent install starts nothing.
  DetailPrint "Starting ${APP_NAME}…"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/relaunch" $1
  ${IfNot} ${Errors}
    Exec '"$INSTDIR\${APP_EXE}"'
  ${ElseIfNot} ${Silent}
    Exec '"$INSTDIR\${APP_EXE}" --setup'
  ${EndIf}
!endif
SectionEnd

; ---------- uninstall ----------
Section "Uninstall"
  SetDetailsPrint textonly
  DetailPrint "Closing ${APP_NAME}…"
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}" /T'
  Pop $0
  Sleep 800

  DetailPrint "Removing ${APP_NAME}…"
  DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"

  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  ; Settings and bookmarks are kept unless the person asks, so a reinstall picks them up.
  IfSilent +3
  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your ${APP_NAME} settings, bookmarks and saved articles?$\r$\n($\"${DATA_DIR}$\")" IDNO +2
  RMDir /r "${DATA_DIR}"
SectionEnd
