using System.Text.Json;
using LLMUninstaller.Core.Models;

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

    public static IEnumerable<OllamaModelEntry> EnumerateOllamaModels(string ollamaModelsPath)
    {
        if (!IsOllamaModelsPath(ollamaModelsPath))
            yield break;

        var manifestsPath = Path.Combine(ollamaModelsPath, "manifests");
        if (!Directory.Exists(manifestsPath))
            yield break;

        var blobsPath = Path.Combine(ollamaModelsPath, "blobs");
        var foundManifest = false;

        foreach (var manifestFile in Directory.EnumerateFiles(manifestsPath, "*", SearchOption.AllDirectories))
        {
            foundManifest = true;
            var name = ResolveNameFromManifestPath(manifestFile, manifestsPath);
            var size = GetManifestSizeBytes(manifestFile, blobsPath);
            var modified = File.GetLastWriteTime(manifestFile);

            yield return new OllamaModelEntry(name, manifestFile, size, modified);
        }

        // Legacy fallback when manifests exist but are empty
        if (!foundManifest && Directory.Exists(blobsPath))
        {
            var info = new DirectoryInfo(blobsPath);
            yield return new OllamaModelEntry(
                "Ollama models (blobs)",
                blobsPath,
                GetDirectorySize(blobsPath),
                info.LastWriteTime);
        }
    }

    public static bool IsOllamaManifestPath(string path) =>
        File.Exists(path) && TryGetOllamaModelsRoot(path, out _);

    public static bool TryGetOllamaModelsRoot(string manifestPath, out string modelsRoot)
    {
        modelsRoot = string.Empty;
        var normalized = Path.GetFullPath(manifestPath).Replace('/', '\\');
        var manifestsIdx = normalized.IndexOf(@"\manifests\", StringComparison.OrdinalIgnoreCase);
        if (manifestsIdx < 0)
            return false;

        modelsRoot = normalized[..manifestsIdx];
        return IsOllamaModelsPath(modelsRoot);
    }

    public static IReadOnlyList<string> GetManifestDigests(string manifestPath)
    {
        var digests = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            CollectDigests(doc.RootElement, digests);
        }
        catch
        {
            // ignored
        }

        return digests;
    }

    public static HashSet<string> CollectReferencedDigests(string manifestsPath, string? excludeManifestPath = null)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(manifestsPath))
            return referenced;

        foreach (var manifest in Directory.EnumerateFiles(manifestsPath, "*", SearchOption.AllDirectories))
        {
            if (excludeManifestPath != null &&
                manifest.Equals(excludeManifestPath, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var digest in GetManifestDigests(manifest))
                referenced.Add(digest);
        }

        return referenced;
    }

    public static string DigestToBlobPath(string blobsPath, string digest) =>
        Path.Combine(blobsPath, digest.Replace(":", "-", StringComparison.Ordinal));

    public static string ResolveNameFromManifestPath(string manifestPath, string manifestsRoot)
    {
        var relative = Path.GetRelativePath(manifestsRoot, manifestPath);
        var parts = relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length - 2; i++)
        {
            if (parts[i].Equals("library", StringComparison.OrdinalIgnoreCase))
                return $"{parts[i + 1]}:{parts[i + 2]}";
        }

        if (parts.Length >= 2)
            return $"{parts[^2]}:{parts[^1]}";

        return Path.GetFileName(manifestPath);
    }

    private static long GetManifestSizeBytes(string manifestPath, string blobsPath)
    {
        var fromJson = TryGetManifestSizeFromJson(manifestPath);
        if (fromJson > 0)
            return fromJson;

        return TryGetManifestSizeFromBlobs(manifestPath, blobsPath);
    }

    private static long TryGetManifestSizeFromJson(string manifestPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            long total = 0;

            if (root.TryGetProperty("config", out var config) &&
                config.TryGetProperty("size", out var configSize))
                total += configSize.GetInt64();

            if (root.TryGetProperty("layers", out var layers))
            {
                foreach (var layer in layers.EnumerateArray())
                {
                    if (layer.TryGetProperty("size", out var layerSize))
                        total += layerSize.GetInt64();
                }
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static long TryGetManifestSizeFromBlobs(string manifestPath, string blobsPath)
    {
        long total = 0;

        foreach (var digest in GetManifestDigests(manifestPath))
            total += GetBlobSize(blobsPath, digest);

        return total;
    }

    private static void CollectDigests(JsonElement element, List<string> digests)
    {
        if (element.TryGetProperty("config", out var config) &&
            config.TryGetProperty("digest", out var configDigest))
        {
            var value = configDigest.GetString();
            if (!string.IsNullOrEmpty(value))
                digests.Add(value);
        }

        if (element.TryGetProperty("layers", out var layers))
        {
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.TryGetProperty("digest", out var digest))
                {
                    var value = digest.GetString();
                    if (!string.IsNullOrEmpty(value))
                        digests.Add(value);
                }
            }
        }
    }

    private static long GetBlobSize(string blobsPath, string digest)
    {
        if (string.IsNullOrEmpty(digest))
            return 0;

        var blobPath = DigestToBlobPath(blobsPath, digest);

        try
        {
            return File.Exists(blobPath) ? new FileInfo(blobPath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch
        {
            return 0;
        }
    }
}

public sealed record OllamaModelEntry(
    string Name,
    string Path,
    long SizeBytes,
    DateTime LastModified);
