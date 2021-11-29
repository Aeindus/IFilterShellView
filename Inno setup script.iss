; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "IFilterShellView"
#define MyAppVersion "5.2.7"
#define MyAppPublisher "Aeindus, Inc."
#define MyAppURL "https://github.com/Aeindus/IFilterShellView"
#define MyAppExeName "IFilterShellView.exe"
#define MyAppOutputDir "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Setup"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{660279D3-EDE5-44F2-A868-4A8CD4759EC9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName=IFilterShellView Data
DisableProgramGroupPage=yes
LicenseFile=D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\LICENSE
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=IFilterShellView Setup
SetupIconFile=D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\IFilterShellView\Resources\icon_application.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#MyAppVersion}
OutputDir={#MyAppOutputDir}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\IFilterShellView.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\IFilterShellView.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\IFilterShellView.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\IFilterShellView.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\IFilterShellView.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\Interop.SHDocVw.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\ModernWpf.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\ModernWpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\Local Projects\Github Projects\Aeindus Group\IFilterShellView\bin\Publish\SciChart.Wpf.UI.Transitionz.dll"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

