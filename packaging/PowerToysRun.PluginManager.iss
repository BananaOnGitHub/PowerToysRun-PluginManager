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
var
  DeleteInstallerCheckBox: TNewCheckBox;
  RecycleInstallerRadio: TNewRadioButton;
  PermanentlyDeleteInstallerRadio: TNewRadioButton;

procedure UpdateDeleteInstallerOptions(Sender: TObject);
begin
  RecycleInstallerRadio.Enabled := DeleteInstallerCheckBox.Checked;
  PermanentlyDeleteInstallerRadio.Enabled := DeleteInstallerCheckBox.Checked;
end;

procedure InitializeWizard;
begin
  DeleteInstallerCheckBox := TNewCheckBox.Create(WizardForm);
  DeleteInstallerCheckBox.Parent := WizardForm.FinishedPage;
  DeleteInstallerCheckBox.Left := WizardForm.FinishedLabel.Left;
  DeleteInstallerCheckBox.Top := WizardForm.FinishedLabel.Top +
    WizardForm.FinishedLabel.Height + ScaleY(18);
  DeleteInstallerCheckBox.Width := WizardForm.FinishedLabel.Width;
  DeleteInstallerCheckBox.Height := ScaleY(18);
  DeleteInstallerCheckBox.Caption := 'Delete this installer after Setup closes';
  DeleteInstallerCheckBox.Checked := False;
  DeleteInstallerCheckBox.Visible := not WizardSilent;
  DeleteInstallerCheckBox.OnClick := @UpdateDeleteInstallerOptions;

  RecycleInstallerRadio := TNewRadioButton.Create(WizardForm);
  RecycleInstallerRadio.Parent := WizardForm.FinishedPage;
  RecycleInstallerRadio.Left := DeleteInstallerCheckBox.Left + ScaleX(20);
  RecycleInstallerRadio.Top := DeleteInstallerCheckBox.Top + ScaleY(23);
  RecycleInstallerRadio.Width := DeleteInstallerCheckBox.Width - ScaleX(20);
  RecycleInstallerRadio.Height := ScaleY(18);
  RecycleInstallerRadio.Caption := 'Move it to the Recycle Bin';
  RecycleInstallerRadio.Checked := True;
  RecycleInstallerRadio.Enabled := False;
  RecycleInstallerRadio.Visible := not WizardSilent;

  PermanentlyDeleteInstallerRadio := TNewRadioButton.Create(WizardForm);
  PermanentlyDeleteInstallerRadio.Parent := WizardForm.FinishedPage;
  PermanentlyDeleteInstallerRadio.Left := RecycleInstallerRadio.Left;
  PermanentlyDeleteInstallerRadio.Top := RecycleInstallerRadio.Top + ScaleY(22);
  PermanentlyDeleteInstallerRadio.Width := RecycleInstallerRadio.Width;
  PermanentlyDeleteInstallerRadio.Height := ScaleY(18);
  PermanentlyDeleteInstallerRadio.Caption := 'Delete it permanently';
  PermanentlyDeleteInstallerRadio.Enabled := False;
  PermanentlyDeleteInstallerRadio.Visible := not WizardSilent;
end;

procedure ScheduleInstallerDeletion;
var
  DeleteMode: String;
  ResultCode: Integer;
begin
  DeleteMode := Lowercase(ExpandConstant('{param:DELETEINSTALLER|}'));
  if (not WizardSilent) and DeleteInstallerCheckBox.Checked then
  begin
    if PermanentlyDeleteInstallerRadio.Checked then
      DeleteMode := 'permanent'
    else
      DeleteMode := 'recycle';
  end;

  if (DeleteMode <> 'recycle') and (DeleteMode <> 'permanent') then
    Exit;

  if not Exec(
    ExpandConstant('{app}\PowerToysRun.PluginManager.Bootstrapper.exe'),
    Format('--delete-installer "%s" %s', [ExpandConstant('{srcexe}'), DeleteMode]),
    '', SW_HIDE, ewNoWait, ResultCode) then
  begin
    SuppressibleMsgBox(
      'Setup could not schedule deletion of the installer: ' + SysErrorMessage(ResultCode),
      mbError,
      MB_OK,
      IDOK);
  end;
end;

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

  if CurStep = ssDone then
    ScheduleInstallerDeletion;
end;
