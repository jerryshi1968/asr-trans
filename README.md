# System Subtitle Translator

Win10/Win11 系统声音字幕小工具：采集默认扬声器输出，转成 16000Hz mono float 音频，送入项目内随附的 SenseVoice ASR 模块，再用 DeepSeek API 翻译成中文。

## 界面预览
![asr-trans demo](./docs/videos/for-github.mp4)

## 运行

先编辑 `appsettings.json`，或者新建不会提交到 Git 的 `appsettings.local.json`：

```json
{
  "Asr": {
    "Language": "en",
    "UseInverseTextNormalization": true,
    "NumThreads": 4,
    "VadThreshold": 0.4,
    "VadMinSilenceDuration": 0.3,
    "VadMinSpeechDuration": 0.25
  },
  "DeepSeek": {
    "ApiKey": "你的 DeepSeek API Key",
    "Model": "deepseek-v4-flash"
  }
}
```

```powershell
dotnet run
```

如果不设置 `DeepSeek:ApiKey`，程序仍会显示本地 ASR 原文，只是跳过翻译。

程序会读取项目运行目录、exe 同级目录以及 `%APPDATA%\SystemSubtitleTranslator` 下的 `appsettings.json` / `appsettings.local.json`。建议把真实 API Key 放进 `appsettings.local.json`，把 `appsettings.json` 当作可提交的模板。安装程序会把安装时填写的 DeepSeek Key 写入 `%APPDATA%\SystemSubtitleTranslator\appsettings.local.json`。

程序启动后会在窗口底部显示当前模式：配置了 `DeepSeek:ApiKey` 时使用远端 AI 翻译；未配置时只做本地识别。点击停止时，程序会询问是否把本轮从开始到停止之间的原文和中文译文保存为 `.txt` 文件。

## 管线

```text
Windows 系统声音 -> WASAPI Loopback -> 16kHz mono float[] -> SenseVoiceAsrEngine -> 原文 -> DeepSeek -> 中文字幕
```

## 依赖

- .NET 10 Windows Desktop Runtime/SDK
- NAudio 2.3.0
- `org.k2fsa.sherpa.onnx` 1.13.2，由 NuGet 还原 Sherpa/ONNX 运行时
- `third_party\Alife.Function.Auditory.SenseVoice` 中随项目保存的 Alife 模型下载辅助 DLL

## 安装包

先发布程序：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

然后用 Inno Setup 打开并编译：

```text
installer\SystemSubtitleTranslator.iss
```

安装时可以选择安装目录，并填写 SenseVoice 识别语言、DeepSeek API Key 和模型。真实配置会写入：

```text
%APPDATA%\SystemSubtitleTranslator\appsettings.local.json
```
