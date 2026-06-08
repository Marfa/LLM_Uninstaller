namespace LLMUninstaller.Core.Scanning;

/// <summary>
/// Detects Ollama installation by presence of blobs and manifests folders.
/// </summary>
public static class OllamaDetector
{
    public static bool IsOllamaModelsPath(string path)
    {
        if (!Directory.Exists(path))
            return false;

        var hasBlobs = Directory.Exists(Path.Combine(path, "blobs"));
        var hasManifests = Directory.Exists(Path.Combine(path, "manifests"));

        return hasBlobs && hasManifests;
    }

    public static IEnumerable<string> EnumerateOllamaModels(string ollamaModelsPath)
    {
        if (!IsOllamaModelsPath(ollamaModelsPath))
            yield break;

        // Ollama stores models as blob references; treat the whole models dir as one unit
        // but also enumerate manifest-named model groups
        var manifestsPath = Path.Combine(ollamaModelsPath, "manifests");
        if (!Directory.Exists(manifestsPath))
        {
            yield return ollamaModelsPath;
            yield break;
        }

        foreach (var registryDir in Directory.EnumerateDirectories(manifestsPath))
        {
            foreach (var manifestFile in Directory.EnumerateFiles(registryDir))
            {
                var modelName = Path.GetFileName(manifestFile);
                yield return manifestFile;
            }
        }

        // Also report blobs directory size as aggregate
        var blobsPath = Path.Combine(ollamaModelsPath, "blobs");
        if (Directory.Exists(blobsPath))
            yield return blobsPath;
    }
}
