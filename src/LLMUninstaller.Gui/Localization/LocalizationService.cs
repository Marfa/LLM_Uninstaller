using System.IO;
using System.Text.Json;

namespace LLMUninstaller.Gui.Localization;

public enum AppLanguage
{
    Russian,
    English
}

public sealed class LocalizationService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LLMUninstaller", "settings.json");

    private AppLanguage _current = AppLanguage.Russian;

    public event Action? LanguageChanged;

    public AppLanguage Current => _current;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings?.Language is "en" or "english")
                _current = AppLanguage.English;
        }
        catch
        {
            // use default
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var settings = new AppSettings
            {
                Language = _current == AppLanguage.English ? "en" : "ru"
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // ignore
        }
    }

    public void ToggleLanguage()
    {
        _current = _current == AppLanguage.Russian ? AppLanguage.English : AppLanguage.Russian;
        Save();
        LanguageChanged?.Invoke();
    }

    public string Get(string key) => Strings.Get(key, _current);

    private sealed class AppSettings
    {
        public string Language { get; set; } = "ru";
    }
}
