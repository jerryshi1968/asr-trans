using System.IO;
using System.Text.Json;

namespace SystemSubtitleTranslator;

public sealed class AsrOptions
{
    static readonly string[] SupportedLanguages = ["auto", "zh", "en", "ja", "ko", "yue"];

    public string Language { get; set; } = "en";
    public bool UseInverseTextNormalization { get; set; } = true;
    public int NumThreads { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
    public float VadThreshold { get; set; } = 0.4f;
    public float VadMinSilenceDuration { get; set; } = 0.3f;
    public float VadMinSpeechDuration { get; set; } = 0.25f;
    public string ConfigurationSource { get; set; } = "未找到配置文件";

    public static AsrOptions Load()
    {
        AsrOptions options = new();
        List<string> loadedPaths = [];

        foreach (string path in AppConfigurationPaths.GetConfigPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
                continue;

            using FileStream stream = File.OpenRead(path);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(stream, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });

            if (settings?.Asr == null)
                continue;

            ApplyNonEmpty(options, settings.Asr);
            loadedPaths.Add(path);
        }

        options.Language = NormalizeLanguage(options.Language);
        options.NumThreads = Math.Max(1, options.NumThreads);
        if (loadedPaths.Count > 0)
            options.ConfigurationSource = string.Join("; ", loadedPaths);
        return options;
    }

    static void ApplyNonEmpty(AsrOptions target, AsrOptions source)
    {
        if (!string.IsNullOrWhiteSpace(source.Language))
            target.Language = source.Language;
        target.UseInverseTextNormalization = source.UseInverseTextNormalization;
        if (source.NumThreads > 0)
            target.NumThreads = source.NumThreads;
        if (source.VadThreshold > 0)
            target.VadThreshold = source.VadThreshold;
        if (source.VadMinSilenceDuration > 0)
            target.VadMinSilenceDuration = source.VadMinSilenceDuration;
        if (source.VadMinSpeechDuration > 0)
            target.VadMinSpeechDuration = source.VadMinSpeechDuration;
    }

    static string NormalizeLanguage(string language)
    {
        string normalized = language.Trim().ToLowerInvariant();
        return SupportedLanguages.Contains(normalized) ? normalized : "en";
    }

    sealed class AppSettings
    {
        public AsrOptions? Asr { get; set; }
    }
}
