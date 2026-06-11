using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Scanning;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Detection;

public static class ModelDetector
{
    public const long LargeFileThresholdBytes = 500L * 1024 * 1024; // 500 MB
    public const long LargeDirectoryThresholdBytes = 1024L * 1024 * 1024; // 1 GB

    public static bool IsModelDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return false;

        long totalSize = 0;
        var hasLargeModelFile = false;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;

                    var ext = info.Extension;
                    if (ModelExtensions.IsModelExtension(ext) && info.Length > LargeFileThresholdBytes)
                        hasLargeModelFile = true;
                }
                catch
                {
                    // skip inaccessible files
                }
            }
        }
        catch
        {
            return false;
        }

        return hasLargeModelFile || totalSize > LargeDirectoryThresholdBytes;
    }

    public static bool IsModelFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            var info = new FileInfo(filePath);
            return ModelExtensions.IsModelExtension(info.Extension) &&
                   info.Length > LargeFileThresholdBytes;
        }
        catch
        {
            return false;
        }
    }

    public static ModelInfo? CreateModelInfo(string path, string? ownerApplication = null)
    {
        var isFile = File.Exists(path);
        var isDir = Directory.Exists(path);

        if (!isFile && !isDir)
            return null;

        if (isFile && !IsModelFile(path))
            return null;

        if (isDir && OllamaDetector.IsOllamaModelsPath(path))
            return null;

        if (isDir && HuggingFaceDetector.IsHuggingFaceHubPath(path))
            return null;

        if (isDir && HuggingFaceDetector.IsHuggingFaceBlobsPath(path))
            return null;

        if (isDir && HuggingFaceDetector.IsHuggingFaceModelPath(path))
            return null;

        if (isDir && !IsModelDirectory(path))
            return null;

        var size = PathHelper.GetSize(path);
        var (lastAccess, lastModified) = PathHelper.GetTimestamps(path);
        var modelType = DetermineType(path);

        return new ModelInfo
        {
            Name = ModelNameResolver.Resolve(path),
            FullPath = Path.GetFullPath(path),
            SizeBytes = size,
            Type = modelType,
            OwnerApplication = ownerApplication,
            LastAccessTime = lastAccess,
            LastModifiedTime = lastModified,
            IsProtectedPath = ProtectedPaths.IsProtected(path),
            IsDirectory = isDir
        };
    }

    private static ModelType DetermineType(string path)
    {
        if (File.Exists(path))
        {
            var extType = ModelExtensions.ClassifyExtension(Path.GetExtension(path));
            if (extType != ModelType.Unknown)
                return extType;
        }

        return ModelExtensions.ClassifyByPath(path);
    }
}
