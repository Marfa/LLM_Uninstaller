using System.IO;
using System.Text.Json;

namespace LLMUninstaller.Gui;

public sealed class GridColumnSettings
{
    public double? Select { get; set; }
    public double? Name { get; set; }
    public double? Size { get; set; }
    public double? Type { get; set; }
    public double? App { get; set; }
    public double? Modified { get; set; }
    public double? PathColumn { get; set; }

    private static string SettingsFilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LLMUninstaller");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "grid-columns.json");
        }
    }

    public static GridColumnSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<GridColumnSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(GridColumnSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // ignored
        }
    }
}
