; Script Inno Setup 6.3 ou plus récent. Installation PAR UTILISATEUR : aucun droit administrateur, aucun service Windows.
#define AppName "NEXUS Console"
#ifndef AppVersion
  #define AppVersion "0.12.0"
#endif

[Setup]
AppId={{B6E7A1D2-4C1F-4E57-9A53-7B2F6C0D8E11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=NEXUS
DefaultDirName={localappdata}\Programs\NexusConsole
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=NexusConsole-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\NexusConsole.exe
SetupIconFile=..\assets\nexus.ico
DisableProgramGroupPage=yes
CloseApplications=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\NexusConsole.exe"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\NexusConsole.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NexusConsole.exe"; Description: "Lancer NEXUS Console"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; retire le démarrage automatique éventuel ; les données (jeux, profils, réglages) restent dans %LOCALAPPDATA%\NexusConsole
Filename: "reg.exe"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v NexusConsole /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"
