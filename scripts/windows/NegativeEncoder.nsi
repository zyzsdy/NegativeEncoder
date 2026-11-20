Unicode True
RequestExecutionLevel user
SetCompressor /SOLID lzma
AllowSkipFiles off

!include "MUI2.nsh"
!include "x64.nsh"

!define PRODUCT_NAME "消极压制 (NegativeEncoder Desktop)"
!define PUBLISHER "Ted Zyzsdy"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\NegativeEncoder.Desktop"
!define APP_EXE "NegativeEncoder.Desktop.exe"
!define LINK "消极压制 (NegativeEncoder Desktop).lnk"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\NegativeEncoder Desktop"
Var ExistingDir
BrandingText "MeowSound Idols"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${FILE_VERSION}"
VIAddVersionKey /LANG=2052 "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=2052 "CompanyName" "${PUBLISHER}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "© MeowSound Idols"
VIAddVersionKey /LANG=2052 "FileDescription" "${PRODUCT_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${FILE_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${PRODUCT_VERSION}"

!define MUI_ICON "install.ico"
!define MUI_UNICON "install.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "up.bmp"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "licence.txt"
!define MUI_PAGE_CUSTOMFUNCTION_PRE ChooseInstallDir
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\app\${APP_EXE}"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "此安装包需要 64 位 Windows。" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegStr $ExistingDir HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $ExistingDir != ""
    StrCpy $INSTDIR $ExistingDir
  ${EndIf}
FunctionEnd

Function ChooseInstallDir
  ${If} $ExistingDir != ""
    Abort ; Keep upgrades in the directory owned by the existing uninstall entry.
  ${EndIf}
FunctionEnd

Section
  ClearErrors
  SetOutPath "$INSTDIR\app"
  File "/oname=${APP_EXE}" "${APP_FILE}"
  File "..\..\LICENSE"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${LINK}" "$INSTDIR\app\${APP_EXE}"
  CreateShortcut "$DESKTOP\${LINK}" "$INSTDIR\app\${APP_EXE}"

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\app\${APP_EXE}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  ${If} ${Errors}
    SetErrorLevel 1
    Abort "安装未完成，请查看安装详情。"
  ${EndIf}
SectionEnd

Section "Uninstall"
  SetRegView 64
  ClearErrors
  Delete "$INSTDIR\app\${APP_EXE}"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP|MB_OK "无法删除主程序，请关闭消极压制后重试。" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  Delete "$INSTDIR\app\LICENSE"
  RMDir "$INSTDIR\app"
  Delete "$SMPROGRAMS\${LINK}"
  Delete "$DESKTOP\${LINK}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
