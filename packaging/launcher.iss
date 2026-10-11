#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PayloadDir
  #error PayloadDir must point to a clean launcher staging directory.
#endif
#ifndef ToolsDir
  #error ToolsDir must point to signature-verified prerequisite installers.
#endif
#ifndef ReleaseDir
  #error ReleaseDir must point to the installer output directory.
#endif

[Setup]
AppId={{58D33B1E-B32E-4DBA-A064-F8DF578E6B0F}
AppName=HYRO Launcher
AppVersion={#AppVersion}
AppPublisher=HYRO
AppPublisherURL=https://github.com/hyronguyen/hyrolauncher
AppSupportURL=https://github.com/hyronguyen/hyrolauncher/issues
AppUpdatesURL=https://github.com/hyronguyen/hyrolauncher/releases
DefaultDirName={localappdata}\Programs\HYROLauncher
DefaultGroupName=HYRO Launcher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
OutputDir={#ReleaseDir}
OutputBaseFilename=HYROLauncher-Setup-{#AppVersion}
SetupIconFile={#PayloadDir}\assets\app.ico
UninstallDisplayIcon={app}\HYROLauncher.exe
UninstallDisplayName=HYRO Launcher
Compression=lzma2/normal
SolidCompression=yes
LZMANumBlockThreads=2
WizardStyle=modern dark polar includetitlebar
WizardSizePercent=110
DisableWelcomePage=no
CloseApplications=yes
CloseApplicationsFilter=HYROLauncher.exe,Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.WinForms.dll,WebView2Loader.dll
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany=HYRO
VersionInfoDescription=HYRO Launcher offline installer
VersionInfoProductName=HYRO Launcher

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\HYROLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Microsoft.Web.WebView2.WinForms.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\WebView2Loader.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\HYROLauncher.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\ui\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\assets\*"; DestDir: "{app}\assets"; Excludes: "apikey\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\assets\apikey\*.txt"; DestDir: "{app}\assets\apikey"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PayloadDir}\assets\apikey\*.json"; DestDir: "{app}\assets\apikey"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#ToolsDir}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy
Source: "{#ToolsDir}\NDP48-x86-x64-AllOS-ENU.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\HYRO Launcher"; Filename: "{app}\HYROLauncher.exe"; WorkingDir: "{app}"; AppUserModelID: "HYRO.Launcher"
Name: "{autodesktop}\HYRO Launcher"; Filename: "{app}\HYROLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "HYRO.Launcher"

[Run]
Filename: "{app}\HYROLauncher.exe"; Description: "Launch HYRO Launcher"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebViewKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function HasDotNet48: Boolean;
var ReleaseValue: Cardinal;
begin
  Result := (RegQueryDWordValue(HKLM64, 'Software\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040));
  if not Result then
    Result := (RegQueryDWordValue(HKLM32, 'Software\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040));
end;

function RuntimeVersionAt(RootKey: Integer): Boolean;
var Version: String;
begin
  Result := RegQueryStringValue(RootKey, WebViewKey, 'pv', Version);
  if Result then Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function HasWebView2: Boolean;
begin
  Result := RuntimeVersionAt(HKLM32) or RuntimeVersionAt(HKCU32) or RuntimeVersionAt(HKLM64) or RuntimeVersionAt(HKCU64);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ResultCode: Integer;
begin
  Result := '';
  if not HasDotNet48 then begin
    WizardForm.PreparingLabel.Caption := 'Installing Microsoft .NET Framework 4.8...';
    ExtractTemporaryFile('NDP48-x86-x64-AllOS-ENU.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\NDP48-x86-x64-AllOS-ENU.exe'), '/q /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
      Result := 'Microsoft .NET Framework 4.8 could not be installed. Administrator permission is required for this Windows component.';
      Exit;
    end;
    Log('Microsoft .NET Framework prerequisite exit code: ' + IntToStr(ResultCode));
    if (ResultCode = 3010) or (ResultCode = 1641) then begin
      NeedsRestart := True;
      Result := 'Microsoft .NET Framework 4.8 is installed. Restart Windows, then run this installer again.';
      Exit;
    end;
    if (ResultCode <> 0) or not HasDotNet48 then begin
      Result := 'Microsoft .NET Framework 4.8 installation failed (code ' + IntToStr(ResultCode) + ').';
      Exit;
    end;
  end;
  if not HasWebView2 then begin
    WizardForm.PreparingLabel.Caption := 'Installing Microsoft Edge WebView2 Runtime...';
    ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
      Result := 'Microsoft Edge WebView2 Runtime could not be installed.';
      Exit;
    end;
    Log('Microsoft Edge WebView2 prerequisite exit code: ' + IntToStr(ResultCode));
    if (ResultCode <> 0) or not HasWebView2 then
      Result := 'Microsoft Edge WebView2 Runtime installation failed (code ' + IntToStr(ResultCode) + ').';
  end;
end;

// User-created cards, vault, library, artwork, profiles and API credentials are
// intentionally absent from [Files] and [UninstallDelete]. Updates and uninstall
// only affect the application payload; local progress remains in the install dir.
