#define AppName "StageJack"
#define AppVersion "0.6.0"
#define AppPublisher "StageJack"
#define AppExeName "StageJack.exe"

[Setup]
AppId={{D8F49F70-9CB5-4984-9775-1C5B8FAE7C0B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/thriftshoppin/stagejack
AppSupportURL=https://github.com/thriftshoppin/stagejack/issues
AppUpdatesURL=https://github.com/thriftshoppin/stagejack/releases
DefaultDirName={localappdata}\Programs\StageJack
DefaultGroupName=StageJack
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist\installer
OutputBaseFilename=StageJack-Setup-{#AppVersion}
SetupIconFile=..\assets\StageJack.ico
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\StageJack.exe
UninstallDisplayName=StageJack
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
VersionInfoVersion=0.6.0.0
VersionInfoProductVersion=0.6.0
VersionInfoProductName=StageJack
VersionInfoDescription=StageJack Setup

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\StageJack.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\NAudio.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\NAudio.Wasapi.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\NAudio.LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Rajdhani-Regular.ttf"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Rajdhani-SemiBold.ttf"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Rajdhani-OFL.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\StageJack"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\StageJack"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch StageJack"; Flags: postinstall skipifsilent nowait

[Code]
function InitializeSetup(): Boolean;
var
  ReleaseVersion: Cardinal;
  RegistryRoot: Integer;
begin
  RegistryRoot := HKLM;
  if IsWin64 then
    RegistryRoot := HKLM64;
  Result := RegQueryDWordValue(RegistryRoot,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', ReleaseVersion) and (ReleaseVersion >= 528040);
  if not Result then
    MsgBox('StageJack requires Microsoft .NET Framework 4.8 or later. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48, then run this installer again.',
      mbInformation, MB_OK);
end;
