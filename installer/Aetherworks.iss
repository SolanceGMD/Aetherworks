#define MyAppName "Aetherworks"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Atherworks"
#define MyAppExeName "Aetherworks.exe"

[Setup]
AppId={{A7B7D57B-5E44-4B2A-9E1D-2C8A6C7D9F31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/SolanceGMD/Aetherworks
AppSupportURL=https://github.com/SolanceGMD/Aetherworks/issues
AppUpdatesURL=https://github.com/SolanceGMD/Aetherworks
DefaultDirName={autopf32}\Aetherworks
DefaultGroupName=Aetherworks
UninstallDisplayName=Aetherworks
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
OutputDir=..\artifacts
OutputBaseFilename=Aetherworks-Setup-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes

[Dirs]
Name: "C:\AetherworksApps"; Permissions: users-modify

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Aetherworks"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Aetherworks"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Aetherworks"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Intentionally do not delete C:\AetherworksApps or any installed games.
