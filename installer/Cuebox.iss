; Cuebox 1.1.1
; Pick the app folder and a separate data folder.

#define MyAppName "Cuebox"
#define MyAppVersion "1.1.1"
#define MyAppExeName "Cuebox.exe"

[Setup]
AppId={{8C3E2A11-7B6F-4D2A-9E1C-6A4B0F8D2E90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Kynvyr
AppPublisherURL=https://github.com/binx-ux/Music-engine-
DefaultDirName={localappdata}\Cuebox
DefaultGroupName=Cuebox
DisableDirPage=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
AllowRootDirectory=no
AlwaysShowDirOnReadyPage=yes
OutputDir=output
OutputBaseFilename=CueboxSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
WizardSizePercent=120
SetupIconFile=..\src\UI\Assets\cuebox.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
InfoBeforeFile=info.txt
MinVersion=10.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "startmenu"; Description: "Start menu shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "info.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Cuebox"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenu
Name: "{autodesktop}\Cuebox"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Open Cuebox"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataPage: TInputDirWizardPage;

procedure InitializeWizard;
begin
  DataPage := CreateInputDirPage(wpSelectDir,
    'Data folder',
    'Where should Cuebox keep settings, music, logs, and pads?',
    'This can be a different drive from the app itself.',
    False, '');
  DataPage.Add('');
  DataPage.Values[0] := ExpandConstant('{userappdata}\Cuebox');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = DataPage.ID then
  begin
    if Trim(DataPage.Values[0]) = '' then
    begin
      MsgBox('Pick a data folder.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DataDir: String;
begin
  if CurStep = ssPostInstall then
  begin
    DataDir := Trim(DataPage.Values[0]);
    if DataDir = '' then
      DataDir := ExpandConstant('{userappdata}\Cuebox');
    ForceDirectories(DataDir);
    SaveStringToFile(ExpandConstant('{app}\data.path'), DataDir, False);
  end;
end;
