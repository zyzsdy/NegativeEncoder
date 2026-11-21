Unicode True
RequestExecutionLevel user
SetCompressor /SOLID lzma
AllowSkipFiles off

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "nsDialogs.nsh"

!define PRODUCT_NAME "NegativeEncoder"
!define PUBLISHER "Ted Zyzsdy"
!define WINDOWS_KEY "Software\Microsoft\Windows\CurrentVersion"
!define UNINSTALL_KEY "${WINDOWS_KEY}\Uninstall\消极压制"
!define APP_PATHS_KEY "${WINDOWS_KEY}\App Paths\NegativeEncoder.exe"
!define PREVIEW_KEY "${WINDOWS_KEY}\Uninstall\NegativeEncoder.Desktop"
!define APP "NEbin\NegativeEncoder.exe"
!define LINK "NegativeEncoder.lnk"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\NegativeEncoder"
Var ExistingDir
Var PreviewDir
Var LegacyGroup
Var MachineRequest
Var AllUsers
Var AllUsersRadio
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
!define MUI_PAGE_CUSTOMFUNCTION_PRE SkipCompletedPage
!insertmacro MUI_PAGE_WELCOME
!define MUI_PAGE_CUSTOMFUNCTION_PRE SkipCompletedPage
!insertmacro MUI_PAGE_LICENSE "licence.txt"
Page custom ChooseInstallScope LeaveInstallScope
!define MUI_PAGE_CUSTOMFUNCTION_PRE ChooseInstallDir
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP}"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

; 5.0 used the 32-bit HKLM uninstall view. Later releases used 64-bit HKCU.
; SHCTX keeps the uninstall entry and App Paths in the scope we detected.
Function ReadInstallDir
  ReadRegStr $ExistingDir SHCTX "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $ExistingDir != ""
    Return
  ${EndIf}

  ; 5.0 has no InstallLocation and records an unquoted uninstaller path.
  ReadRegStr $0 SHCTX "${UNINSTALL_KEY}" "UninstallString"
  StrCpy $1 $0 1
  ${If} $1 == '"'
    StrCpy $0 $0 -1 1
  ${EndIf}
  ${GetFileName} "$0" $1
  ${If} $1 == "uninst.exe"
    ${GetParent} "$0" $ExistingDir
    Return
  ${EndIf}

  ; App Paths can contain either the EXE (5.1) or the install root (5.2).
  ReadRegStr $ExistingDir SHCTX "${APP_PATHS_KEY}" ""
  ${GetFileName} "$ExistingDir" $1
  ${If} $1 == "NegativeEncoder.exe"
    ${GetParent} "$ExistingDir" $ExistingDir
    ${GetParent} "$ExistingDir" $ExistingDir
  ${Else}
    StrCpy $1 $ExistingDir 1 -1
    ${If} $1 == "\"
      StrCpy $ExistingDir $ExistingDir -1
    ${EndIf}
  ${EndIf}
FunctionEnd

; Select the uninstall view before reading App Paths, which Windows can share
; between views. Keep machine-wide upgrades registered in their original view.
!macro MachineScope
  SetShellVarContext all
  SetRegView 32
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  ${If} $0 == ""
    SetRegView 64
  ${EndIf}
!macroend

!macro RequireMachineAccess verb executable arguments
  UserInfo::GetAccountType
  Pop $0
  ${If} $0 != "Admin"
    StrCpy $1 ""
    IfSilent 0 +2
    StrCpy $1 "/S"
    HideWindow
    ClearErrors
    ExecShellWait "runas" "${executable}" '${arguments} $1' SW_SHOWNORMAL $0
    ${If} ${Errors}
      MessageBox MB_ICONSTOP|MB_OK "需要管理员权限才能${verb}所有用户的 NegativeEncoder。" /SD IDOK
      SetErrorLevel 740
    ${Else}
      SetErrorLevel $0
    ${EndIf}
    Quit
  ${EndIf}
!macroend

Function FindInstallation
  SetRegView 64
  ; Elevation can switch users. The relaunched installer only consults HKLM.
  ${If} $MachineRequest == ""
    ReadRegStr $PreviewDir HKCU "${PREVIEW_KEY}" "InstallLocation"
    Call ReadInstallDir
    ${If} $ExistingDir != ""
      Return
    ${EndIf}
  ${EndIf}

  !insertmacro MachineScope
  Call ReadInstallDir
  ${If} $ExistingDir != ""
  ${OrIf} $MachineRequest != ""
    StrCpy $AllUsers 1
  ${Else}
    SetShellVarContext current
    SetRegView 64
    StrCpy $ExistingDir $PreviewDir
  ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "此安装包需要 64 位 Windows。" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  ${GetParameters} $0
  ${GetOptions} $0 "/MachineUpgrade" $1
  ${IfNot} ${Errors}
    StrCpy $MachineRequest "/MachineUpgrade"
  ${Else}
    ${GetOptions} $0 "/MachineInstall" $1
    ${IfNot} ${Errors}
      StrCpy $MachineRequest "/MachineInstall"
    ${EndIf}
  ${EndIf}
  Call FindInstallation
  ${If} $ExistingDir != ""
    StrCpy $INSTDIR $ExistingDir
    ${If} $MachineRequest != "/MachineUpgrade"
      MessageBox MB_YESNO|MB_ICONQUESTION "检测到 NegativeEncoder 已安装在：$\r$\n$ExistingDir$\r$\n$\r$\n是否在此目录更新？如需更换安装位置，请先卸载现有版本。" /SD IDYES IDYES update
      SetErrorLevel 1
      Quit
    ${EndIf}
    update:
    ${If} $AllUsers == 1
      !insertmacro RequireMachineAccess "更新" "$EXEPATH" "/MachineUpgrade"
    ${EndIf}
  ${ElseIf} $MachineRequest == "/MachineUpgrade"
    MessageBox MB_ICONSTOP|MB_OK "找不到系统级 NegativeEncoder 安装记录。" /SD IDOK
    SetErrorLevel 1
    Abort
  ${ElseIf} $MachineRequest == "/MachineInstall"
    !insertmacro RequireMachineAccess "安装" "$EXEPATH" "/MachineInstall"
    StrCpy $INSTDIR "$PROGRAMFILES64\${PRODUCT_NAME}"
  ${EndIf}
  ReadRegStr $LegacyGroup SHCTX "${UNINSTALL_KEY}" "NSIS:StartMenuDir"
FunctionEnd

Function SkipCompletedPage
  ; Fresh all-users installs resume at the directory page after UAC.
  ${If} $MachineRequest == "/MachineInstall"
    Abort
  ${EndIf}
FunctionEnd

Function ChooseInstallScope
  ${If} $ExistingDir != ""
  ${OrIf} $MachineRequest != ""
    Abort
  ${EndIf}

  !insertmacro MUI_HEADER_TEXT "选择安装范围" "选择哪些用户可以使用 NegativeEncoder。"
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    MessageBox MB_ICONSTOP|MB_OK "无法显示安装范围选择页。"
    SetErrorLevel 1
    Quit
  ${EndIf}
  ${NSD_CreateRadioButton} 0 12u 100% 16u "仅为当前用户安装"
  Pop $0
  ${NSD_CreateLabel} 12u 30u -12u 20u "默认目录：%LOCALAPPDATA%\${PRODUCT_NAME}"
  Pop $1
  ${NSD_CreateRadioButton} 0 58u 100% 16u "为所有用户安装（需要管理员权限）"
  Pop $AllUsersRadio
  ${NSD_CreateLabel} 12u 76u -12u 20u "默认目录：$PROGRAMFILES64\${PRODUCT_NAME}"
  Pop $1
  ${If} $AllUsers == 1
    ${NSD_Check} $AllUsersRadio
  ${Else}
    ${NSD_Check} $0
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Function LeaveInstallScope
  ${NSD_GetState} $AllUsersRadio $0
  ${If} $0 == ${BST_CHECKED}
    !insertmacro RequireMachineAccess "安装" "$EXEPATH" "/MachineInstall"
    SetShellVarContext all
    ${If} $AllUsers != 1
      StrCpy $INSTDIR "$PROGRAMFILES64\${PRODUCT_NAME}"
      StrCpy $AllUsers 1
    ${EndIf}
  ${ElseIf} $AllUsers == 1
    SetShellVarContext current
    StrCpy $INSTDIR "$LOCALAPPDATA\${PRODUCT_NAME}"
    StrCpy $AllUsers 0
  ${EndIf}
FunctionEnd

Function ChooseInstallDir
  ${If} $ExistingDir != ""
    Abort ; Keep upgrades in the directory owned by the existing uninstall entry.
  ${EndIf}
FunctionEnd

Section
  ClearErrors
  SetOutPath "$INSTDIR\NEbin"
  File "/oname=NegativeEncoder.exe" "${APP_FILE}"
  File "..\..\LICENSE"
  ; Replace the old uninstaller in place: the 5.x uninstaller recursively
  ; removes the entire install root, including Libs and user files.
  WriteUninstaller "$INSTDIR\uninst.exe"
  CreateShortcut "$SMPROGRAMS\${LINK}" "$INSTDIR\${APP}"
  CreateShortcut "$DESKTOP\${LINK}" "$INSTDIR\${APP}"

  WriteRegStr SHCTX "${APP_PATHS_KEY}" "" "$INSTDIR\${APP}"
  WriteRegStr SHCTX "${APP_PATHS_KEY}" "Path" "$INSTDIR\NEbin"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninst.exe"'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\uninst.exe" /S'
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoRepair" 1
  ${If} ${Errors}
    SetErrorLevel 1
    Abort "安装未完成，请查看安装详情。"
  ${EndIf}

  ${If} $LegacyGroup != ""
    DeleteRegValue SHCTX "${UNINSTALL_KEY}" "NSIS:StartMenuDir"
    ; The 5.x installers created shortcuts for the installing user even
    ; when their uninstall entry was machine-wide.
    SetShellVarContext current
    SetRegView 64
    ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
    ${If} $0 == ""
    ${OrIf} $0 == $INSTDIR
      Delete "$SMPROGRAMS\$LegacyGroup\消极压制.lnk"
      Delete "$SMPROGRAMS\$LegacyGroup\Uninstall.lnk"
      RMDir "$SMPROGRAMS\$LegacyGroup"
      Delete "$DESKTOP\消极压制.lnk"
    ${EndIf}
  ${EndIf}
  ${If} $PreviewDir != ""
    ; Remove only files owned by the earlier Desktop preview.
    Delete "$PreviewDir\app\NegativeEncoder.Desktop.exe"
    ${IfNot} ${FileExists} "$PreviewDir\app\NegativeEncoder.Desktop.exe"
      Delete "$PreviewDir\Uninstall.exe"
    ${EndIf}
    ${If} ${FileExists} "$PreviewDir\app\NegativeEncoder.Desktop.exe"
    ${OrIf} ${FileExists} "$PreviewDir\Uninstall.exe"
      SetErrorLevel 1
      Abort "请关闭 NegativeEncoder Desktop 后重新运行安装程序。"
    ${EndIf}
    Delete "$PreviewDir\app\LICENSE"
    RMDir "$PreviewDir\app"
    RMDir "$PreviewDir"
    SetShellVarContext current
    SetRegView 64
    Delete "$SMPROGRAMS\消极压制 (NegativeEncoder Desktop).lnk"
    Delete "$DESKTOP\消极压制 (NegativeEncoder Desktop).lnk"
    DeleteRegKey HKCU "${PREVIEW_KEY}"
  ${EndIf}
SectionEnd

Function un.onInit
  !insertmacro MachineScope
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == $INSTDIR
    !insertmacro RequireMachineAccess "卸载" "$INSTDIR\uninst.exe" ""
  ${Else}
    SetShellVarContext current
    SetRegView 64
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ClearErrors
  Delete "$INSTDIR\${APP}"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP|MB_OK "无法删除主程序，请关闭消极压制后重试。" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  Delete "$INSTDIR\NEbin\LICENSE"
  RMDir "$INSTDIR\NEbin"
  Delete "$SMPROGRAMS\${LINK}"
  Delete "$DESKTOP\${LINK}"
  DeleteRegKey SHCTX "${UNINSTALL_KEY}"
  DeleteRegKey SHCTX "${APP_PATHS_KEY}"
  Delete "$INSTDIR\uninst.exe"
  RMDir "$INSTDIR"
SectionEnd
