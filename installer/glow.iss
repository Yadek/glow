; Glow installer. Copies Glow.exe, makes shortcuts, registers autostart, and
; removes everything (incl. AppData + Run key) on uninstall.
; Compile: ISCC.exe installer\glow.iss /DAppVersion=1.0.0 /DSourceExe=path\to\Glow.exe

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

; Path to the published single-file exe. Override with /DSourceExe in CI.
#ifndef SourceExe
  #define SourceExe "..\src\Glow\bin\Release\net8.0-windows\win-x64\publish\Glow.exe"
#endif

#define AppName "Glow"
#define AppPublisher "Glow"
#define AppExe "Glow.exe"
#define AppId "{{B7A1F2C0-9E3D-4A7B-8C21-5F0E3A9D1C44}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
OutputDir=Output
OutputBaseFilename=Glow-Setup-{#AppVersion}
PrivilegesRequired=admin
SetupIconFile=..\src\Glow\glow.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "autostart"; Description: "{cm:AutostartDesc}"; GroupDescription: "{cm:AutostartGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Silent autostart for the current user. Quoted path handles spaces in {app}.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "Glow"; ValueData: """{app}\{#AppExe}"""; \
    Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Interactive install: optional "launch now" checkbox on the finish page.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent runasoriginaluser
; Silent install (used by the in-app auto-updater): always relaunch as the user.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
; Make sure the running instance is closed before files are removed.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillGlow"

[UninstallDelete]
; Wipe any leftover per-user config and the (now empty) install folder.
Type: filesandordirs; Name: "{userappdata}\Glow"
Type: filesandordirs; Name: "{localappdata}\Glow"
Type: dirifempty; Name: "{app}"

[CustomMessages]
english.AutostartDesc=Start Glow automatically when Windows starts
english.AutostartGroup=Startup:
english.AdditionalIcons=Additional shortcuts:
russian.AutostartDesc=Запускать Glow автоматически при старте Windows
russian.AutostartGroup=Автозагрузка:
russian.AdditionalIcons=Дополнительные ярлыки:

[Code]
// Close a running Glow before overwriting its files (e.g. during auto-update).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM Glow.exe /F', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

// On uninstall, remove the autostart value and the app's settings key so nothing
// is left in the registry.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'Glow');
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Glow');
  end;
end;
