using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace SystemSubtitleTranslator;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    readonly SubtitlePipeline pipeline;
    readonly List<SubtitleRecord> sessionRecords = [];
    string sourceText = "等待系统声音...";
    string translationText = "DeepSeek 翻译会显示在这里。";
    string statusText = "未启动";
    string footerText = "在 appsettings.json 中设置 DeepSeek:ApiKey 后可启用翻译；未设置时仍可本地识别原文。";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppDisplayName { get; } = $"系统声音实时字幕 v{GetAppVersion()}";

    public string SourceText
    {
        get => sourceText;
        set => SetField(ref sourceText, value);
    }

    public string TranslationText
    {
        get => translationText;
        set => SetField(ref translationText, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetField(ref statusText, value);
    }

    public string FooterText
    {
        get => footerText;
        set => SetField(ref footerText, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        pipeline = new SubtitlePipeline();
        pipeline.StatusChanged += message => Dispatcher.Invoke(() => StatusText = message);
        pipeline.Recognized += text => Dispatcher.Invoke(() => {
            SourceText = text;
            sessionRecords.Add(new SubtitleRecord(DateTime.Now, text));
        });
        pipeline.Translated += (source, translation) => Dispatcher.Invoke(() => {
            TranslationText = translation;
            SubtitleRecord? record = sessionRecords.LastOrDefault(item => item.SourceText == source && string.IsNullOrWhiteSpace(item.TranslationText));
            if (record != null)
                record.TranslationText = translation;
        });
        pipeline.TranslationSkipped += reason => Dispatcher.Invoke(() => FooterText = reason);
    }

    async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        sessionRecords.Clear();
        SourceText = "正在初始化 SenseVoice...";
        TranslationText = "等待识别结果...";
        FooterText = $"{pipeline.AsrModeText}；{pipeline.TranslationModeText}";

        try
        {
            await pipeline.StartAsync();
        }
        catch (Exception ex)
        {
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText = $"启动失败: {ex.Message}";
        }
    }

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        pipeline.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        AskToSaveSession();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        pipeline.Dispose();
        base.OnClosing(e);
    }

    void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void AskToSaveSession()
    {
        if (sessionRecords.Count == 0)
            return;

        MessageBoxResult result = MessageBox.Show(
            this,
            "是否需要把从开始到停止之间的识别文字（英文和中文）保存到一个文本文件中？",
            "保存字幕记录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        SaveFileDialog dialog = new() {
            Title = "保存字幕记录",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"subtitles-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        File.WriteAllText(dialog.FileName, BuildSessionText(), Encoding.UTF8);
        FooterText = $"已保存字幕记录: {dialog.FileName}";
        OpenWithNotepad(dialog.FileName);
    }

    string BuildSessionText()
    {
        StringBuilder builder = new();
        builder.AppendLine("系统声音字幕记录");
        builder.AppendLine($"翻译模式: {pipeline.TranslationModeText}");
        builder.AppendLine($"保存时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        foreach (SubtitleRecord record in sessionRecords)
        {
            builder.AppendLine($"[{record.Time:HH:mm:ss}]");
            builder.AppendLine($"原文: {record.SourceText}");
            builder.AppendLine($"中文: {(string.IsNullOrWhiteSpace(record.TranslationText) ? "(无翻译)" : record.TranslationText)}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    void OpenWithNotepad(string fileName)
    {
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName = "notepad.exe",
                ArgumentList = { fileName },
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            FooterText = $"已保存字幕记录，但打开记事本失败: {ex.Message}";
        }
    }

    sealed class SubtitleRecord(DateTime time, string sourceText)
    {
        public DateTime Time { get; } = time;
        public string SourceText { get; } = sourceText;
        public string? TranslationText { get; set; }
    }

    static string GetAppVersion()
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString(3)
               ?? "1.0.0";
    }
}
