#ifndef MyVersion
  #define MyVersion "0.3.0"
#endif
#ifndef MyArch
  #define MyArch "x64"
#endif
#ifndef SourceRoot
  #define SourceRoot AddBackslash(SourcePath) + "..\artifacts\installer-input"
#endif
#ifndef OutputRoot
  #define OutputRoot AddBackslash(SourcePath) + "..\artifacts\installer"
#endif

#define MyAppName "PowerToys Run Plugin Manager"
#define MyAppPublisher "BananaOnGitHub and contributors"
#define MyAppUrl "https://github.com/BananaOnGitHub/PowerToysRun-PluginManager"
#define MyAppExeName "PowerToysRun.PluginManager.exe"

[Setup]
AppId={{1D5AFD60-9E2D-43AD-AF3B-A09D76D04925}
AppName={#MyAppName}
AppVersion={#MyVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={localappdata}\PowerToysRunPluginManager\App
LicenseFile={#SourcePath}\..\LICENSE
OutputDir={#OutputRoot}
OutputBaseFilename=PowerToysRun-PluginManager-Setup-win-{#MyArch}
SetupIconFile={#SourcePath}\..\src\PowerToysRun.PluginManager.App\Assets\plugin-manager.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=*.exe,*.dll
MinVersion=10.0.19041
#if MyArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceRoot}\App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Bootstrap\*"; DestDir: "{app}\Bootstrap"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallRun]
Filename: "{app}\PowerToysRun.PluginManager.Bootstrapper.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveBootstrapPlugin"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'Installing the PowerToys Run launcher...';
    if (not Exec(
      ExpandConstant('{app}\PowerToysRun.PluginManager.Bootstrapper.exe'),
      '--install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or
      (ResultCode <> 0) then
    begin
      MsgBox(
        'The manager was installed, but its PowerToys Run launcher could not be applied. ' +
        'Exit PowerToys and run this installer again.',
        mbError,
        MB_OK);
    end;
  end;
end;
