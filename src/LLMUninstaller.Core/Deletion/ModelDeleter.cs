using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Utilities;
using Microsoft.VisualBasic.FileIO;

namespace LLMUninstaller.Core.Deletion;

public sealed class ModelDeleter
{
    private readonly IAppLogger _logger;

    public ModelDeleter(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<DeleteResult> DeleteAsync(ModelInfo model, DeleteOptions options)
    {
        var path = model.FullPath;

        if (!PathHelper.PathExists(path))
        {
            var msg = "Файл или каталог не существует";
            await _logger.LogErrorAsync($"Удаление: {path}", msg);
            return new DeleteResult { Path = path, Success = false, ErrorMessage = msg };
        }

        if (ProtectedPaths.IsProtected(path) && !options.AllowProtectedPaths)
        {
            var msg = ProtectedPaths.GetProtectionReason(path);
            await _logger.LogErrorAsync($"Удаление: {path}", msg);
            return new DeleteResult { Path = path, Success = false, ErrorMessage = msg };
        }

        if (!PathHelper.HasWriteAccess(path))
        {
            var msg = "Недостаточно прав для удаления";
            await _logger.LogErrorAsync($"Удаление: {path}", msg);
            return new DeleteResult { Path = path, Success = false, ErrorMessage = msg };
        }

        var freedBytes = PathHelper.GetSize(path);

        try
        {
            if (options.UseRecycleBin)
                SendToRecycleBin(path);
            else
                PermanentDelete(path);

            await _logger.LogDeletedModelAsync(model, freedBytes);

            return new DeleteResult
            {
                Path = path,
                Success = true,
                FreedBytes = freedBytes
            };
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Удаление: {path}", ex.Message);
            return new DeleteResult
            {
                Path = path,
                Success = false,
                FreedBytes = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IReadOnlyList<DeleteResult>> DeleteManyAsync(
        IEnumerable<ModelInfo> models,
        DeleteOptions options)
    {
        var results = new List<DeleteResult>();
        foreach (var model in models)
            results.Add(await DeleteAsync(model, options));
        return results;
    }

    private static void SendToRecycleBin(string path)
    {
        if (File.Exists(path))
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        if (Directory.Exists(path))
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    private static void PermanentDelete(string path)
    {
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
