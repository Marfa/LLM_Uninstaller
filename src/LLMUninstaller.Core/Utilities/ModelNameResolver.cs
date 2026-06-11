using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Detection;
using LLMUninstaller.Core.Scanning;

namespace LLMUninstaller.Core.Utilities;

public static class ModelNameResolver
{
    public static string Resolve(string path)
    {
        if (File.Exists(path))
            return Path.GetFileNameWithoutExtension(path);

        if (!Directory.Exists(path))
            return Path.GetFileName(path);

        var ollamaName = TryResolveOllamaName(path);
        if (ollamaName != null)
            return ollamaName;

        var hfName = TryResolveHuggingFaceName(path);
        if (hfName != null)
            return hfName;

        var largestModel = FindLargestModelFile(path);
        if (largestModel != null)
            return Path.GetFileNameWithoutExtension(largestModel);

        var singleChildName = TryResolveSingleModelChildName(path);
        if (singleChildName != null)
            return singleChildName;

        var parentName = new DirectoryInfo(path).Name;
        if (!IsGenericFolderName(parentName))
            return parentName;

        return parentName;
    }

    private static string? TryResolveOllamaName(string path)
    {
        var normalized = path.Replace('/', '\\');

        var manifestsIdx = normalized.IndexOf(@"\manifests\", StringComparison.OrdinalIgnoreCase);
        if (manifestsIdx < 0)
            return null;

        if (!File.Exists(path))
            return null;

        var manifestsRoot = normalized[..manifestsIdx] + @"\manifests";
        return OllamaDetector.ResolveNameFromManifestPath(path, manifestsRoot);
    }

    private static string? TryResolveHuggingFaceName(string path)
    {
        var normalized = path.Replace('/', '\\');

        // models--meta-llama--Llama-2-7b-hf
        var modelsIdx = normalized.IndexOf("models--", StringComparison.OrdinalIgnoreCase);
        if (modelsIdx >= 0)
        {
            var segment = normalized[modelsIdx..];
            var end = segment.IndexOf('\\');
            var token = end > 0 ? segment[..end] : segment;
            return HuggingFaceTokenToName(token);
        }

        // hub\models\org\model
        if (normalized.Contains(@"\huggingface\", StringComparison.OrdinalIgnoreCase))
        {
            var parts = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("models", StringComparison.OrdinalIgnoreCase) &&
                    i + 2 < parts.Length)
                {
                    return $"{parts[i + 1]}/{parts[i + 2]}";
                }
            }
        }

        return null;
    }

    private static string HuggingFaceTokenToName(string token)
    {
        if (!token.StartsWith("models--", StringComparison.OrdinalIgnoreCase))
            return token;

        var body = token["models--".Length..];
        var parts = body.Split("--", 2);
        return parts.Length == 2 ? $"{parts[0]}/{parts[1]}" : body.Replace("--", "/");
    }

    private static string? FindLargestModelFile(string directory)
    {
        string? bestPath = null;
        long bestSize = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (!ModelExtensions.IsModelExtension(info.Extension))
                        continue;
                    if (info.Length < ModelDetector.LargeFileThresholdBytes / 10)
                        continue;

                    if (info.Length > bestSize)
                    {
                        bestSize = info.Length;
                        bestPath = file;
                    }
                }
                catch
                {
                    // skip
                }
            }
        }
        catch
        {
            return null;
        }

        return bestPath;
    }

    private static string? TryResolveSingleModelChildName(string directory)
    {
        try
        {
            var childDirs = Directory.EnumerateDirectories(directory).ToList();
            if (childDirs.Count != 1)
                return null;

            var childName = Path.GetFileName(childDirs[0]);
            if (IsGenericFolderName(childName))
                return null;

            return childName;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsGenericFolderName(string name) =>
        name.Equals("models", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("blobs", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("checkpoints", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("hub", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("llm", StringComparison.OrdinalIgnoreCase);
}
