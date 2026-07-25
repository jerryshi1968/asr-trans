using System.IO;
using Alife.Function.AIModelUtility;
using SherpaOnnx;

namespace SystemSubtitleTranslator;

public sealed class SenseVoiceAsrEngine : IDisposable
{
    const string SenseVoiceId = "pengzhendong/sherpa-onnx-sense-voice-zh-en-ja-ko-yue";
    const string VadId = "pengzhendong/silero-vad";

    readonly OfflineRecognizer recognizer;
    readonly VoiceActivityDetector vad;

    public event Action<string>? Recognized;

    public string Language { get; }

    public static bool ModelsExists
    {
        get
        {
            string senseVoicePath = Path.Combine(AIModelUtility.ModelScopeModelPath, SenseVoiceId.Replace(".", "___"));
            string vadPath = Path.Combine(AIModelUtility.ModelScopeModelPath, VadId.Replace(".", "___"));
            return File.Exists(Path.Combine(senseVoicePath, "model.int8.onnx"))
                   && File.Exists(Path.Combine(vadPath, "silero_vad.onnx"));
        }
    }

    public SenseVoiceAsrEngine(AsrOptions options)
    {
        Language = options.Language;

        string senseVoicePath = AIModelUtility.EnsureModelExisting(SenseVoiceId);
        string vadModelPath = AIModelUtility.EnsureModelExisting(VadId, "silero_vad.onnx");

        OfflineRecognizerConfig config = new();
        config.ModelConfig.SenseVoice.Model = Path.Combine(senseVoicePath, "model.int8.onnx");
        config.ModelConfig.SenseVoice.Language = options.Language;
        config.ModelConfig.SenseVoice.UseInverseTextNormalization = options.UseInverseTextNormalization ? 1 : 0;
        config.ModelConfig.Tokens = Path.Combine(senseVoicePath, "tokens.txt");
        config.ModelConfig.NumThreads = options.NumThreads;
        config.ModelConfig.Debug = 0;
        recognizer = new OfflineRecognizer(config);

        VadModelConfig vadConfig = new();
        vadConfig.SileroVad.Model = vadModelPath;
        vadConfig.SileroVad.Threshold = options.VadThreshold;
        vadConfig.SileroVad.MinSilenceDuration = options.VadMinSilenceDuration;
        vadConfig.SileroVad.MinSpeechDuration = options.VadMinSpeechDuration;
        vadConfig.SampleRate = 16000;
        vad = new VoiceActivityDetector(vadConfig, bufferSizeInSeconds: 30);
    }

    public void AcceptWaveform(float[] samples)
    {
        lock (vad)
        {
            vad.AcceptWaveform(samples);
            while (!vad.IsEmpty())
            {
                SpeechSegment segment = vad.Front();
                if (segment.Samples is { Length: > 0 })
                    ProcessSegment(segment.Samples);
                vad.Pop();
            }
        }
    }

    public void Dispose()
    {
        recognizer.Dispose();
        vad.Dispose();
    }

    void ProcessSegment(float[] samples)
    {
        using OfflineStream stream = recognizer.CreateStream();
        stream.AcceptWaveform(16000, samples);
        recognizer.Decode(stream);

        string text = stream.Result.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (text == "。")
            return;
        Recognized?.Invoke(text);
    }
}
