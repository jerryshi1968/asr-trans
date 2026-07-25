namespace SystemSubtitleTranslator;

public sealed class SubtitlePipeline : IDisposable
{
    readonly AsrOptions asrOptions = AsrOptions.Load();
    readonly DeepSeekTranslator translator = new();
    readonly SemaphoreSlim translateGate = new(1, 1);
    SenseVoiceAsrEngine? model;
    WasapiSystemAudioSource? audioSource;
    bool started;

    public event Action<string>? StatusChanged;
    public event Action<string>? Recognized;
    public event Action<string, string>? Translated;
    public event Action<string>? TranslationSkipped;

    public string TranslationModeText =>
        translator.IsConfigured
            ? $"翻译模式：远端 AI 翻译 DeepSeek ({translator.Model})"
            : "翻译模式：本地识别模式，未启用翻译";
    public string AsrModeText => $"识别模式：SenseVoice ({asrOptions.Language})";

    public Task StartAsync()
    {
        if (started)
            return Task.CompletedTask;

        if (!SenseVoiceAsrEngine.ModelsExists)
            StatusChanged?.Invoke("SenseVoice 或 Silero VAD 模型不存在，Alife 会尝试下载或初始化模型。");

        model = new SenseVoiceAsrEngine(asrOptions);

        model.Recognized += OnRecognized;
        audioSource = new WasapiSystemAudioSource();
        audioSource.SamplesAvailable += samples => {
            SenseVoiceAsrEngine? currentModel = model;
            if (currentModel == null)
                return;
            ThreadPool.QueueUserWorkItem(_ => {
                try
                {
                    currentModel.AcceptWaveform(samples);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"ASR 处理失败: {ex.Message}");
                }
            });
        };
        audioSource.Start();

        started = true;
        StatusChanged?.Invoke($"正在监听默认输出设备: {audioSource.DeviceName}；{AsrModeText}");
        if (!translator.IsConfigured)
            TranslationSkipped?.Invoke($"未读取到 DeepSeek:ApiKey，只显示本地识别原文。配置来源: {translator.ConfigurationSource}");
        else
            TranslationSkipped?.Invoke($"{TranslationModeText}。配置来源: {translator.ConfigurationSource}");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (!started)
            return;

        if (model != null)
            model.Recognized -= OnRecognized;
        audioSource?.Dispose();
        audioSource = null;
        model?.Dispose();
        model = null;
        started = false;
        StatusChanged?.Invoke("已停止");
    }

    public void Dispose()
    {
        Stop();
        translateGate.Dispose();
    }

    void OnRecognized(string text)
    {
        Recognized?.Invoke(text);
        _ = TranslateLatestAsync(text);
    }

    async Task TranslateLatestAsync(string text)
    {
        if (!translator.IsConfigured)
            return;

        await translateGate.WaitAsync();

        try
        {
            string translated = await translator.TranslateToChineseAsync(text);
            Translated?.Invoke(text, translated);
        }
        catch (Exception ex)
        {
            TranslationSkipped?.Invoke($"DeepSeek 翻译失败: {ex.Message}");
        }
        finally
        {
            translateGate.Release();
        }
    }
}
