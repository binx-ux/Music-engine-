; Cuebox installer (Inno Setup 6)
; Does not install an unsigned kernel driver.

#define MyAppName "Cuebox"
#define MyAppVersion "1.0.0"
#define MyAppExeName "Cuebox.exe"

[Setup]
AppId={{8C3E2A11-7B6F-4D2A-9E1C-6A4B0F8D2E90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Cuebox
DefaultGroupName=Cuebox
OutputDir=output
OutputBaseFilename=CueboxSetup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
SetupIconFile=..\src\UI\Assets\cuebox.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\src\UI\bin\Release\net8.0-windows\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Cuebox"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Cuebox"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Open Cuebox"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Cuebox"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  MsgBox('Cuebox talks to Windows audio devices only. To send the mix into Roblox or Discord, install a virtual audio cable first. Cuebox does not install an unsigned driver.', mbInformation, MB_OK);
end;
