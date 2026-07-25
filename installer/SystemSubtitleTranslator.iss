#define MyAppName "System Subtitle Translator"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SystemSubtitleTranslator"
#define MyAppExeName "SystemSubtitleTranslator.exe"
#define PublishDir "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{38AF5F15-825B-4BB8-9E5B-0F9E9BD2303D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SystemSubtitleTranslator
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=SystemSubtitleTranslatorSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeepSeekPage: TInputQueryWizardPage;

function JsonEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

procedure InitializeWizard;
begin
  DeepSeekPage := CreateInputQueryPage(
    wpSelectDir,
    '语音识别和 DeepSeek 配置',
    '设置 SenseVoice 识别语言、DeepSeek API Key 和模型',
    '可以现在填写这些配置，也可以安装后修改用户配置目录中的 appsettings.local.json。');

  DeepSeekPage.Add('SenseVoice Language（en/auto/zh/ja/ko/yue）:', False);
  DeepSeekPage.Add('DeepSeek API Key（可留空）:', False);
  DeepSeekPage.Add('DeepSeek Model:', False);
  DeepSeekPage.Values[0] := 'en';
  DeepSeekPage.Values[2] := 'deepseek-v4-flash';
end;

procedure WriteDeepSeekConfig;
var
  ConfigDir: String;
  ConfigPath: String;
  AsrLanguage: String;
  ApiKey: String;
  Model: String;
  Json: String;
begin
  AsrLanguage := Trim(DeepSeekPage.Values[0]);
  ApiKey := Trim(DeepSeekPage.Values[1]);
  Model := Trim(DeepSeekPage.Values[2]);
  if AsrLanguage = '' then
    AsrLanguage := 'en';
  if Model = '' then
    Model := 'deepseek-v4-flash';

  ConfigDir := ExpandConstant('{userappdata}\SystemSubtitleTranslator');
  ConfigPath := ConfigDir + '\appsettings.local.json';
  ForceDirectories(ConfigDir);

  Json :=
    '{' + #13#10 +
    '  "Asr": {' + #13#10 +
    '    "Language": "' + JsonEscape(AsrLanguage) + '",' + #13#10 +
    '    "UseInverseTextNormalization": true,' + #13#10 +
    '    "NumThreads": 4,' + #13#10 +
    '    "VadThreshold": 0.4,' + #13#10 +
    '    "VadMinSilenceDuration": 0.3,' + #13#10 +
    '    "VadMinSpeechDuration": 0.25' + #13#10 +
    '  },' + #13#10 +
    '  "DeepSeek": {' + #13#10 +
    '    "ApiKey": "' + JsonEscape(ApiKey) + '",' + #13#10 +
    '    "Model": "' + JsonEscape(Model) + '"' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;

  SaveStringToFile(ConfigPath, Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteDeepSeekConfig;
end;
