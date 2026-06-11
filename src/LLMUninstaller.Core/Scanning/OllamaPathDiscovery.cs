namespace LLMUninstaller.Core.Scanning;

/// <summary>
/// Finds Ollama models directories in non-standard locations (Docker volumes, disk scan).
/// </summary>
public static class OllamaPathDiscovery
{
    private static readonly string[] RelativeCandidates =
    [
        "models",
        @".ollama\models",
        @"_data\models",
        @"_data\.ollama\models"
    ];

    public static IEnumerable<(string Path, string Owner)> DiscoverInDirectory(string root, string owner)
    {
        if (!Directory.Exists(root))
            yield break;

        if (OllamaDetector.IsOllamaModelsPath(root))
        {
            yield return (root, owner);
            yield break;
        }

        foreach (var relative in RelativeCandidates)
        {
            var candidate = Path.Combine(root, relative);
            if (OllamaDetector.IsOllamaModelsPath(candidate))
                yield return (candidate, owner);
        }
    }
}
