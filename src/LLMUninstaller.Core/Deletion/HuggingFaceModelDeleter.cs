using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Scanning;
using LLMUninstaller.Core.Utilities;
using Microsoft.VisualBasic.FileIO;

namespace LLMUninstaller.Core.Deletion;

public static class HuggingFaceModelDeleter
{
    public static async Task<DeleteResult> DeleteAsync(
        ModelInfo model,
        DeleteOptions options,
        IAppLogger logger)
    {
        var modelsDir = model.FullPath;

        if (!HuggingFaceDetector.TryGetHubRoot(modelsDir, out var hubRoot))
        {
            var msg = "Не удалось определить каталог Hugging Face Hub";
            await logger.LogErrorAsync($"Удаление: {modelsDir}", msg);
            return new DeleteResult { Path = modelsDir, Success = false, ErrorMessage = msg };
        }

        var blobsPath = Path.Combine(hubRoot, "blobs");
        var modelBlobHashes = HuggingFaceDetector.CollectModelBlobHashes(modelsDir, blobsPath);
        var stillReferenced = HuggingFaceDetector.CollectAllReferencedBlobHashes(hubRoot, modelsDir);

        var blobsToDelete = modelBlobHashes
            .Where(hash => !stillReferenced.Contains(hash))
            .Select(hash => HuggingFaceDetector.BlobHashToPath(blobsPath, hash))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();

        if (ProtectedPaths.IsProtected(modelsDir) && !options.AllowProtectedPaths)
        {
            var msg = ProtectedPaths.GetProtectionReason(modelsDir);
            await logger.LogErrorAsync($"Удаление: {modelsDir}", msg);
            return new DeleteResult { Path = modelsDir, Success = false, ErrorMessage = msg };
        }

        var pathsToDelete = new List<string> { modelsDir };
        pathsToDelete.AddRange(blobsToDelete);

        foreach (var path in pathsToDelete)
        {
            if (!PathHelper.PathExists(path))
                continue;

            if (ProtectedPaths.IsProtected(path) && !options.AllowProtectedPaths)
            {
                var msg = ProtectedPaths.GetProtectionReason(path);
                await logger.LogErrorAsync($"Удаление: {modelsDir}", msg);
                return new DeleteResult { Path = modelsDir, Success = false, ErrorMessage = msg };
            }

            if (!PathHelper.HasWriteAccess(path))
            {
                var msg = "Недостаточно прав для удаления";
                await logger.LogErrorAsync($"Удаление: {path}", msg);
                return new DeleteResult { Path = modelsDir, Success = false, ErrorMessage = msg };
            }
        }

        var freedBytes = pathsToDelete.Sum(PathHelper.GetSize);

        try
        {
            DeletePath(modelsDir, options.UseRecycleBin);

            foreach (var blobPath in blobsToDelete)
                DeletePath(blobPath, options.UseRecycleBin);

            await logger.LogDeletedModelAsync(model, freedBytes);

            return new DeleteResult
            {
                Path = modelsDir,
                Success = true,
                FreedBytes = freedBytes
            };
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync($"Удаление: {modelsDir}", ex.Message);
            return new DeleteResult
            {
                Path = modelsDir,
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
