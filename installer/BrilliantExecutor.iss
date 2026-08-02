; ============================================================================
; Brilliant Executor Installer Script
; Requires: Inno Setup 6+ (https://jrsoftware.org/isinfo.php)
; Build: iscc installer/BrilliantExecutor.iss
; ============================================================================

#define MyAppName "Brilliant Executor"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Brilliant"
#define MyAppExeName "Syntax Executor.exe"
#define MyAppAssocName "Lua Script"
#define MyAppAssocExt ".lua"

[Setup]
AppId={{B7914E7A-9C48-4C9A-A3F5-8D9F4B9E2B51}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Brilliant Executor
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\publish\installer
OutputBaseFilename=BrilliantExecutor-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} - Roblox Executor
MinVersion=10.0
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Launch {#MyAppName} on Windows startup"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files - copy from the build output directory
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\Syntax.dll"; DestDir: "{app}"; Flags: ignoreversion; Check: FileExists
Source: "..\publish\Lua.xshd"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*.dll"; DestDir: "{app}"; Excludes: "Syntax.dll"; Flags: ignoreversion recursesubdirs
Source: "..\publish\Scripts\*"; DestDir: "{app}\Scripts"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File association for .lua files
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppName}.lua"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppName}.lua"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppName}.lua\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppName}.lua\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if Roblox is installed
function IsRobloxInstalled(): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Roblox') or
            RegKeyExists(HKEY_CURRENT_USER, 'SOFTWARE\Roblox') or
            FileExists(ExpandConstant('{autopf}\Roblox\Versions\RobloxPlayerBeta.exe')) or
            FileExists(ExpandConstant('{userappdata}\Roblox\Versions\RobloxPlayerBeta.exe')) or
            DirExists(ExpandConstant('{appdata}\Roblox'));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Warn if Roblox is not detected
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not IsRobloxInstalled() then
    begin
      MsgBox('Roblox was not detected on this system.' + #13#10 +
             'Please make sure Roblox is installed and running before attaching the executor.',
             mbInformation, MB_OK);
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\workspace"