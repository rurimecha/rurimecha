#ifndef RuriVersion
  #error Pass /DRuriVersion=<version>.
#endif
#ifndef DesktopDir
  #error Pass /DDesktopDir=<published Windows client directory>.
#endif
#ifndef CoreDir
  #error Pass /DCoreDir=<unpacked Windows Core package directory>.
#endif

[Setup]
AppId=Rurimecha.Desktop
AppName=Rurimecha
AppVersion={#RuriVersion}
AppPublisher=Rurimecha
AppMutex=RurimechaDesktop
DefaultDirName={localappdata}\Programs\Rurimecha
DefaultGroupName=Rurimecha
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\Rurimecha.exe
OutputBaseFilename=rurimecha-windows-x64-setup-{#RuriVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#DesktopDir}\Rurimecha.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DesktopDir}\third_party\*"; DestDir: "{app}\third_party"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#CoreDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rurimecha"; Filename: "{app}\Rurimecha.exe"
Name: "{group}\Reset Ruri administrator password"; Filename: "{app}\reset-admin-password.cmd"; Parameters: """{localappdata}\Rurimecha\data"""; WorkingDir: "{app}"

[Run]
Filename: "{app}\Rurimecha.exe"; Description: "Open Rurimecha"; Flags: nowait postinstall skipifsilent
