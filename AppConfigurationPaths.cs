using System.IO;

namespace SystemSubtitleTranslator;

public static class AppConfigurationPaths
{
    public static string[] GetConfigPaths()
    {
        string appDataConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemSubtitleTranslator",
            "appsettings.local.json");

        return [
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"),
            appDataConfigPath
        ];
    }
}
