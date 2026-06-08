namespace LLMUninstaller.Core.Constants;

public sealed record SearchLocation(string Path, string OwnerApplication, bool IsPattern = false);

public static class StandardPaths
{
    public static IReadOnlyList<SearchLocation> GetStandardLocations()
    {
        var locations = new List<SearchLocation>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Ollama
        locations.Add(new($"{userProfile}\\.ollama\\models", "Ollama"));

        // LM Studio
        locations.Add(new($"{userProfile}\\.lmstudio\\models", "LM Studio"));
        locations.Add(new($"{userProfile}\\.cache\\lm-studio", "LM Studio"));

        // Hugging Face
        locations.Add(new($"{userProfile}\\.cache\\huggingface", "Hugging Face"));
        locations.Add(new($"{userProfile}\\.cache\\huggingface\\hub", "Hugging Face"));

        // GPT4All
        locations.Add(new($"{localAppData}\\nomic.ai\\GPT4All", "GPT4All"));
        locations.Add(new($"{appData}\\nomic.ai\\GPT4All", "GPT4All"));

        // Jan
        locations.Add(new($"{appData}\\Jan\\data\\models", "Jan"));

        // Open WebUI
        locations.Add(new($"{userProfile}\\open-webui", "Open WebUI"));

        // ComfyUI subdirectories (pattern-based, resolved per drive)
        foreach (var drive in GetAvailableDrives())
        {
            locations.Add(new($"{drive}\\ComfyUI\\models", "ComfyUI", IsPattern: true));
            locations.Add(new($"{drive}\\text-generation-webui\\models", "Text Generation WebUI", IsPattern: true));
            locations.Add(new($"{drive}\\KoboldCpp\\models", "KoboldCpp", IsPattern: true));
            locations.Add(new($"{drive}\\koboldcpp\\models", "KoboldCpp", IsPattern: true));
            locations.Add(new($"{drive}\\llama.cpp\\models", "llama.cpp", IsPattern: true));
        }

        return locations;
    }

    public static IReadOnlyList<string> ComfyUiSubdirectories { get; } =
    [
        "checkpoints", "clip", "vae", "loras", "unet", "diffusion_models", "llm"
    ];

    public static IReadOnlyList<string> AdditionalSearchDirectoryNames { get; } =
    [
        "models", "llm", "ai", "ollama", "huggingface", "checkpoints", "diffusion_models"
    ];

    private static IEnumerable<string> GetAvailableDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.Name.TrimEnd('\\'));
}
