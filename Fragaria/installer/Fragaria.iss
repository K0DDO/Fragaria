; Fragaria Windows Installer — Inno Setup 6
; Сборка: build-installer.bat (на Windows)

#define MyAppName "Fragaria"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Fragaria"
#define MyAppURL "https://github.com"
#define MyAppExeName "Fragaria.exe"
#define PublishDir "..\dist\Fragaria"

[Setup]
AppId={{F8A3C2E1-9B4D-4F6A-8E2C-1D5B7A9C3E4F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=..\dist
OutputBaseFilename=FragariaSetup
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Fragaria — виртуальный аудио-микшер
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon,'Fragaria'}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "launchafter"; Description: "Запустить Fragaria после установки"; GroupDescription: "Дополнительно:"; Flags: checkedonce
Name: "autostart"; Description: "Запускать Fragaria при входе в Windows"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent; Tasks: launchafter

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Fragaria"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Создаём папку для пресетов и записей }
    ForceDirectories(ExpandConstant('{userappdata}\Fragaria\presets'));
    ForceDirectories(ExpandConstant('{userappdata}\Fragaria\recordings'));
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Fragaria"
