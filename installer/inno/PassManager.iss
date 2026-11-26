; -----------------------------------------------------------------------------
; PassManager - Inno Setup installer (produces a setup .exe)
;
; Built by build.ps1:
;   ISCC.exe /DAppVersion=1.0.0 /DSourceDir=<publish dir> /DOutputDir=<artifacts dir> PassManager.iss
;
; - Installs per-user (no admin rights) by default; the user may choose "all users".
; - Never deletes the user's vault on uninstall unless the user explicitly confirms.
; -----------------------------------------------------------------------------

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

#define AppName "PassManager"
#define AppExeName "PassManager.exe"
#define AppPublisher "PassManager"
; Must match SingleInstanceGuard.MutexName in the application.
#define AppMutexName "PassManager_7F3A2C1E5B4D4E8A9C6F2D1B0A9E8F7C"

[Setup]
AppId={{6B1F4E2A-8C3D-4F5E-9A7B-1C2D3E4F5A6B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (C) 2026 {#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=PassManager-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
AppMutex={#AppMutexName}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Secure offline password manager"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DataDir := ExpandConstant('{localappdata}\PassManager');
  if (not DirExists(DataDir)) or UninstallSilent then
    Exit;

  if MsgBox('Do you also want to permanently delete your encrypted vault, settings and logs?' + #13#10#13#10 +
            DataDir + #13#10#13#10 +
            'Choose "No" to keep your vault (recommended if you plan to reinstall). This cannot be undone.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(DataDir, True, True, True);
end;
