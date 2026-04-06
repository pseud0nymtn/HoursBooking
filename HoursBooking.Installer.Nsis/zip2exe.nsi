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
InstallDir "$LOCALAPPDATA\${APP_NAME}"
RequestExecutionLevel user

Page directory
Page instfiles

Section "Extract and Launch"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}*"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\HoursBooking.App.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\HoursBooking.App.exe"

  ExecShell "open" "$INSTDIR\HoursBooking.App.exe"
SectionEnd
