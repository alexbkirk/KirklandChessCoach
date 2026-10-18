; ChessCoach installer. Built by tools\build-installer.ps1 (which fills ..\build\app first).

#define MyAppName "ChessCoach"
; toolsuild-installer.ps1 passes /DMyAppVersion=<Version from Directory.Build.props>; this is only the fallback.
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppExe "ChessCoach.exe"

[Setup]
AppId={{6F1C2B7E-3D4A-4B8E-9C21-7A5E0D9B4C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=ChessCoach
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Installs for the current user only, so no administrator password is needed.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=ChessCoach-Setup
SetupIconFile=..\src\ChessCoach.Desktop\Assets\chesscoach.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\build\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Your games and settings live in %LOCALAPPDATA%\ChessCoach and are kept on purpose.
Type: filesandordirs; Name: "{app}\python\Lib\site-packages\__pycache__"
