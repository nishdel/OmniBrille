#ifndef AppVersion
  #define AppVersion "0.6.0-preview.1"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.6.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\packages"
#endif

#define AppName "OmniBrille"
#define AppExeName "OmniBrille.exe"
#define AppPublisher "OmniBrille Contributors"
#define AppUrl "https://github.com/nishdel/OmniBrille"

[Setup]
AppId={{B191365D-A12A-4931-99DC-A19445B91651}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\Programs\OmniBrille
DefaultGroupName=OmniBrille
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=OmniBrille-{#AppVersion}-win-x64-setup
SetupIconFile=..\src\OmniBrille.Desktop\Assets\OmniBrille.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#NumericVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=OmniBrille Windows installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#NumericVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
AllowNoIcons=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OmniBrille"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "Spatial navigation companion for OmniSorSe"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch OmniBrille"; Flags: nowait postinstall skipifsilent
