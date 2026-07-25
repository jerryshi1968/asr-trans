using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemSubtitleTranslator;

public sealed class DeepSeekTranslator
{
    static readonly HttpClient HttpClient = new() {
        BaseAddress = new Uri("https://api.deepseek.com/")
    };

    readonly DeepSeekOptions options = DeepSeekOptions.Load();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);
    public string Model => options.Model;
    public string ConfigurationSource => options.ConfigurationSource;

    public async Task<string> TranslateToChineseAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return text;

        using HttpRequestMessage request = new(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new DeepSeekRequest(
            options.Model,
            [
                new DeepSeekMessage("system", "你是字幕翻译器。把用户给出的语音识别文本翻译成自然、简洁的中文。只输出中文译文，不要解释。"),
                new DeepSeekMessage("user", text)
            ],
            0.2
        ));

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");

        DeepSeekResponse? parsed = JsonSerializer.Deserialize<DeepSeekResponse>(body);
        string? translated = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return string.IsNullOrWhiteSpace(translated) ? text : translated.Trim();
    }

    sealed record DeepSeekRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<DeepSeekMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    sealed record DeepSeekMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    sealed record DeepSeekResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<DeepSeekChoice>? Choices);

    sealed record DeepSeekChoice(
        [property: JsonPropertyName("message")] DeepSeekMessage? Message);

    sealed class DeepSeekOptions
    {
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "deepseek-chat";
        public string ConfigurationSource { get; set; } = "未找到配置文件";

        public static DeepSeekOptions Load()
        {
            DeepSeekOptions options = new();
            List<string> loadedPaths = [];

            foreach (string path in AppConfigurationPaths.GetConfigPaths().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    continue;

                using FileStream stream = File.OpenRead(path);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(stream, new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                });

                if (settings?.DeepSeek == null)
                    continue;

                ApplyNonEmpty(options, settings.DeepSeek);
                loadedPaths.Add(path);
            }

            if (string.IsNullOrWhiteSpace(options.Model))
                options.Model = "deepseek-chat";
            if (loadedPaths.Count > 0)
                options.ConfigurationSource = string.Join("; ", loadedPaths);
            return options;
        }

        static void ApplyNonEmpty(DeepSeekOptions target, DeepSeekOptions source)
        {
            if (!string.IsNullOrWhiteSpace(source.ApiKey))
                target.ApiKey = source.ApiKey;
            if (!string.IsNullOrWhiteSpace(source.Model))
                target.Model = source.Model;
        }
    }

    sealed class AppSettings
    {
        public DeepSeekOptions? DeepSeek { get; set; }
    }
}
