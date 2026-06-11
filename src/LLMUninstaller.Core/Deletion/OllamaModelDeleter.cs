using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Scanning;
using LLMUninstaller.Core.Utilities;
using Microsoft.VisualBasic.FileIO;

namespace LLMUninstaller.Core.Deletion;

public static class OllamaModelDeleter
{
    public static async Task<DeleteResult> DeleteAsync(
        ModelInfo model,
        DeleteOptions options,
        IAppLogger logger)
    {
        var manifestPath = model.FullPath;

        if (!OllamaDetector.TryGetOllamaModelsRoot(manifestPath, out var modelsRoot))
        {
            var msg = "Не удалось определить каталог Ollama";
            await logger.LogErrorAsync($"Удаление: {manifestPath}", msg);
            return new DeleteResult { Path = manifestPath, Success = false, ErrorMessage = msg };
        }

        var manifestsPath = Path.Combine(modelsRoot, "manifests");
        var blobsPath = Path.Combine(modelsRoot, "blobs");
        var modelDigests = OllamaDetector.GetManifestDigests(manifestPath);
        var stillReferenced = OllamaDetector.CollectReferencedDigests(manifestsPath, manifestPath);

        var blobsToDelete = modelDigests
            .Where(digest => !stillReferenced.Contains(digest))
            .Select(digest => OllamaDetector.DigestToBlobPath(blobsPath, digest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();

        var pathsToDelete = new List<string> { manifestPath };
        pathsToDelete.AddRange(blobsToDelete);

        if (ProtectedPaths.IsProtected(manifestPath) && !options.AllowProtectedPaths)
        {
            var msg = ProtectedPaths.GetProtectionReason(manifestPath);
            await logger.LogErrorAsync($"Удаление: {manifestPath}", msg);
            return new DeleteResult { Path = manifestPath, Success = false, ErrorMessage = msg };
        }

        foreach (var path in pathsToDelete)
        {
            if (!PathHelper.PathExists(path))
                continue;

            if (ProtectedPaths.IsProtected(path) && !options.AllowProtectedPaths)
            {
                var msg = ProtectedPaths.GetProtectionReason(path);
                await logger.LogErrorAsync($"Удаление: {manifestPath}", msg);
                return new DeleteResult { Path = manifestPath, Success = false, ErrorMessage = msg };
            }

            if (!PathHelper.HasWriteAccess(path))
            {
                var msg = "Недостаточно прав для удаления";
                await logger.LogErrorAsync($"Удаление: {path}", msg);
                return new DeleteResult { Path = manifestPath, Success = false, ErrorMessage = msg };
            }
        }

        var freedBytes = pathsToDelete.Sum(PathHelper.GetSize);

        try
        {
            DeletePath(manifestPath, options.UseRecycleBin);

            foreach (var blobPath in blobsToDelete)
                DeletePath(blobPath, options.UseRecycleBin);

            await logger.LogDeletedModelAsync(model, freedBytes);

            return new DeleteResult
            {
                Path = manifestPath,
                Success = true,
                FreedBytes = freedBytes
            };
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync($"Удаление: {manifestPath}", ex.Message);
            return new DeleteResult
            {
                Path = manifestPath,
                Success = false,
                FreedBytes = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    private static void DeletePath(string path, bool useRecycleBin)
    {
        if (useRecycleBin)
        {
            if (File.Exists(path))
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else if (Directory.Exists(path))
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
