Unicode true

!ifndef APP_NAME
  !define APP_NAME "HoursBooking"
!endif

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif

!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR define is required"
!endif

!ifndef OUT_DIR
  !error "OUT_DIR define is required"
!endif

!ifndef OUT_FILE
  !define OUT_FILE "HoursBooking-Setup.exe"
!endif

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUT_DIR}${OUT_FILE}"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
RequestExecutionLevel admin

!define REG_UNINSTALL "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${REG_UNINSTALL}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${REG_UNINSTALL}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${REG_UNINSTALL}" "Publisher" "HoursBooking"
  WriteRegStr HKLM "${REG_UNINSTALL}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${REG_UNINSTALL}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\HoursBooking.App.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\HoursBooking.App.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${REG_UNINSTALL}"
SectionEnd
