[Setup]
AppName=Brilliant Executor
AppVersion=2.0.0
AppPublisher=Brilliant Executor
AppPublisherURL=https://github.com/akatsukixpain12-stack/Brilliant-executor
AppSupportURL=https://github.com/akatsukixpain12-stack/Brilliant-executor/issues
DefaultDirName={autopf}\Brilliant Executor
DefaultGroupName=Brilliant Executor
OutputBaseFilename=BrilliantExecutor-Setup-v2.0.0
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
LicenseFile=LICENSE.txt
WizardStyle=modern
UninstallDisplayIcon={app}\Syntax Executor.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checked

[Files]
Source: "release_files\Syntax Executor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "release_files\Syntax.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Brilliant Executor"; Filename: "{app}\Syntax Executor.exe"; Comment: "Execute Lua scripts in Roblox"
Name: "{group}\{cm:UninstallProgram,Brilliant Executor}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Brilliant Executor"; Filename: "{app}\Syntax Executor.exe"; Tasks: desktopicon; Comment: "Execute Lua scripts in Roblox"

[Run]
Filename: "{app}\Syntax Executor.exe"; Description: "{cm:LaunchProgram,Brilliant Executor}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function IsComponentInstalled(ComponentName: string): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v10.0');
end;

procedure InitializeWizard;
begin
  if not IsComponentInstalled('dotnet') then
  begin
    MsgBox('.NET 10 Runtime is required but not installed. The installer will continue, but you must install .NET 10 from https://dotnet.microsoft.com/download before running Brilliant Executor.', mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    try
      DelTree(ExpandConstant('{app}'), True, True, True);
    except
    end;
  end;
end;
