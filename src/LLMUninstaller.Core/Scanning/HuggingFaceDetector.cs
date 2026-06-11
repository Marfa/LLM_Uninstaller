using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Scanning;

/// <summary>
/// Detects Hugging Face Hub cache: models--* folders with shared hub/blobs storage.
/// </summary>
public static class HuggingFaceDetector
{
    public static bool IsHuggingFaceHubPath(string path)
    {
        if (!Directory.Exists(path))
            return false;

        var normalized = PathHelper.NormalizeDirectoryPath(path);
        return normalized.EndsWith(@"\huggingface\hub", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHuggingFaceBlobsPath(string path)
    {
        if (!Directory.Exists(path))
            return false;

        var parent = Directory.GetParent(path);
        return parent != null &&
               IsHuggingFaceHubPath(parent.FullName) &&
               Path.GetFileName(path).Equals("blobs", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHuggingFaceModelPath(string path)
    {
        if (!Directory.Exists(path))
            return false;

        if (!Path.GetFileName(path).StartsWith("models--", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryGetHubRoot(path, out _);
    }

    public static IEnumerable<string> DiscoverHubPaths(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        if (IsHuggingFaceHubPath(root))
            yield return root;

        var hub = Path.Combine(root, "hub");
        if (IsHuggingFaceHubPath(hub))
            yield return hub;
    }

    public static bool TryGetHubRoot(string modelOrHubPath, out string hubRoot)
    {
        hubRoot = string.Empty;
        var normalized = Path.GetFullPath(modelOrHubPath).Replace('/', '\\');

        var hubIdx = normalized.IndexOf(@"\huggingface\hub", StringComparison.OrdinalIgnoreCase);
        if (hubIdx < 0)
            return false;

        hubRoot = normalized[..(hubIdx + @"\huggingface\hub".Length)];
        return Directory.Exists(hubRoot);
    }

    public static IEnumerable<HuggingFaceModelEntry> EnumerateModels(string hubPath)
    {
        if (!IsHuggingFaceHubPath(hubPath))
            yield break;

        var blobsPath = Path.Combine(hubPath, "blobs");

        IEnumerable<string> modelDirs;
        try
        {
            modelDirs = Directory.EnumerateDirectories(hubPath)
                .Where(d => Path.GetFileName(d).StartsWith("models--", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            yield break;
        }

        foreach (var modelDir in modelDirs)
        {
            var name = ResolveModelName(modelDir);
            var size = CalculateModelSize(modelDir, blobsPath);
            if (size <= 0)
                size = PathHelper.GetSize(modelDir);

            if (size <= 0)
                continue;

            var modified = Directory.GetLastWriteTime(modelDir);
            yield return new HuggingFaceModelEntry(name, modelDir, size, modified);
        }
    }

    public static string ResolveModelName(string modelsDir)
    {
        var token = Path.GetFileName(modelsDir);
        if (!token.StartsWith("models--", StringComparison.OrdinalIgnoreCase))
            return token;

        var body = token["models--".Length..];
        var parts = body.Split("--", 2);
        return parts.Length == 2 ? $"{parts[0]}/{parts[1]}" : body.Replace("--", "/");
    }

    public static HashSet<string> CollectModelBlobHashes(string modelsDir, string hubBlobsPath)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var snapshotsPath = Path.Combine(modelsDir, "snapshots");

        if (!Directory.Exists(snapshotsPath))
            return hashes;

        try
        {
            foreach (var file in Directory.EnumerateFiles(snapshotsPath, "*", SearchOption.AllDirectories))
            {
                var hash = TryGetBlobHashFromSnapshotFile(file, hubBlobsPath);
                if (hash != null)
                    hashes.Add(hash);
            }
        }
        catch
        {
            // ignored
        }

        return hashes;
    }

    public static HashSet<string> CollectAllReferencedBlobHashes(string hubPath, string? excludeModelsDir = null)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blobsPath = Path.Combine(hubPath, "blobs");

        if (!Directory.Exists(hubPath))
            return referenced;

        try
        {
            foreach (var modelDir in Directory.EnumerateDirectories(hubPath))
            {
                if (!Path.GetFileName(modelDir).StartsWith("models--", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (excludeModelsDir != null &&
                    modelDir.Equals(excludeModelsDir, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var hash in CollectModelBlobHashes(modelDir, blobsPath))
                    referenced.Add(hash);
            }
        }
        catch
        {
            // ignored
        }

        return referenced;
    }

    public static string BlobHashToPath(string hubBlobsPath, string blobHash) =>
        Path.Combine(hubBlobsPath, blobHash);

    private static long CalculateModelSize(string modelsDir, string hubBlobsPath)
    {
        var blobHashes = CollectModelBlobHashes(modelsDir, hubBlobsPath);
        long total = 0;

        foreach (var hash in blobHashes)
            total += GetBlobSize(hubBlobsPath, hash);

        total += GetInlineSnapshotSize(modelsDir);
        return total;
    }

    private static long GetInlineSnapshotSize(string modelsDir)
    {
        var snapshotsPath = Path.Combine(modelsDir, "snapshots");
        if (!Directory.Exists(snapshotsPath))
            return 0;

        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(snapshotsPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;

                    total += info.Length;
                }
                catch
                {
                    // skip
                }
            }
        }
        catch
        {
            return total;
        }

        return total;
    }

    private static string? TryGetBlobHashFromSnapshotFile(string filePath, string hubBlobsPath)
    {
        try
        {
            var info = new FileInfo(filePath);

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                var target = info.ResolveLinkTarget(true);
                if (target != null)
                    return ExtractBlobHash(target.FullName, hubBlobsPath);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? ExtractBlobHash(string path, string hubBlobsPath)
    {
        var normalized = Path.GetFullPath(path).Replace('/', '\\');
        var blobsNormalized = Path.GetFullPath(hubBlobsPath).Replace('/', '\\');

        if (normalized.StartsWith(blobsNormalized, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(normalized);

        var blobsIdx = normalized.LastIndexOf(@"\blobs\", StringComparison.OrdinalIgnoreCase);
        if (blobsIdx >= 0)
        {
            var afterBlobs = normalized[(blobsIdx + @"\blobs\".Length)..];
            var slash = afterBlobs.IndexOf('\\');
            return slash > 0 ? afterBlobs[..slash] : afterBlobs;
        }

        return null;
    }

    private static long GetBlobSize(string hubBlobsPath, string hash)
    {
        try
        {
            var path = BlobHashToPath(hubBlobsPath, hash);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}

public sealed record HuggingFaceModelEntry(
    string Name,
    string Path,
    long SizeBytes,
    DateTime LastModified);
