using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Deletion;

public sealed class CursorCacheCleaner
{
    private readonly IAppLogger _logger;

    // Safe-ish subset of Cursor's workspace cache.
    // We intentionally DO NOT delete state.vscdb (main IDE state).
    private static readonly string[] DirectoriesToDelete =
    [
        "anysphere.cursor-agent-worker",
        "anysphere.cursor-retrieval"
    ];

    private static readonly string[] FilesToDelete =
    [
        "conversation-search.db"
    ];

    // Claude Desktop (based on existing folder on the machine).
    // We only clear typical cache dirs to avoid breaking auth/state.
    private static readonly string ClaudeLocalAppDataRootRelative = "Claude-3p";

    private static readonly string[] ClaudeDirectoriesToDelete =
    [
        "Cache",
        "Code Cache",
        "GPUCache",
        "ShaderCache"
    ];

    public CursorCacheCleaner(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<AppCacheClearResult> ClearGlobalStorageAsync(
        CancellationToken cancellationToken = default)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            return Task.FromResult(new AppCacheClearResult());

        var globalStorageRoot = Path.Combine(appData, "Cursor", "User", "globalStorage");
        return ClearUnderRootAsync(globalStorageRoot, cancellationToken);
    }

    private async Task<AppCacheClearResult> ClearUnderRootAsync(
        string globalStorageRoot,
        CancellationToken cancellationToken)
    {
        var result = new AppCacheClearResult();

        if (!PathHelper.PathExists(globalStorageRoot))
            return result;

        var rootFull = PathHelper.NormalizeDirectoryPath(globalStorageRoot);

        foreach (var dirRel in DirectoriesToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirFull = Path.Combine(globalStorageRoot, dirRel);
            if (!PathHelper.IsUnderRoot(rootFull, dirFull))
                continue;

            if (!Directory.Exists(dirFull))
                continue;

            result.FreedBytes += PathHelper.GetSize(dirFull);

            try
            {
                Directory.Delete(dirFull, recursive: true);
                result.DeletedPaths.Add(dirFull);
            }
            catch (Exception ex)
            {
                result.Errors++;
                await _logger.LogErrorAsync(
                    "Cursor cache cleanup (directory)",
                    $"{dirFull}: {ex.Message}");
            }
        }

        foreach (var fileRel in FilesToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileFull = Path.Combine(globalStorageRoot, fileRel);
            if (!PathHelper.IsUnderRoot(rootFull, fileFull))
                continue;

            if (!File.Exists(fileFull))
                continue;

            result.FreedBytes += new FileInfo(fileFull).Length;

            try
            {
                File.Delete(fileFull);
                result.DeletedPaths.Add(fileFull);
            }
            catch (Exception ex)
            {
                result.Errors++;
                await _logger.LogErrorAsync(
                    "Cursor cache cleanup (file)",
                    $"{fileFull}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<AppCacheClearResult> ClearClaudeCacheAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new AppCacheClearResult();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return result;

        var claudeRoot = Path.Combine(localAppData, ClaudeLocalAppDataRootRelative);
        if (!PathHelper.PathExists(claudeRoot))
            return result;

        var rootFull = PathHelper.NormalizeDirectoryPath(claudeRoot);

        foreach (var dirRel in ClaudeDirectoriesToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirFull = Path.Combine(claudeRoot, dirRel);
            if (!PathHelper.IsUnderRoot(rootFull, dirFull))
                continue;

            if (!Directory.Exists(dirFull))
                continue;

            result.FreedBytes += PathHelper.GetSize(dirFull);

            try
            {
                Directory.Delete(dirFull, recursive: true);
                result.DeletedPaths.Add(dirFull);
            }
            catch (Exception ex)
            {
                result.Errors++;
                await _logger.LogErrorAsync(
                    "Claude cache cleanup (directory)",
                    $"{dirFull}: {ex.Message}");
            }
        }

        return result;
    }

    public IReadOnlyList<CacheItem> GetCursorCacheItems()
    {
        var items = new List<CacheItem>();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            return items;

        var globalStorageRoot = Path.Combine(appData, "Cursor", "User", "globalStorage");
        if (!PathHelper.PathExists(globalStorageRoot))
            return items;

        foreach (var dirRel in DirectoriesToDelete)
        {
            var dirFull = Path.Combine(globalStorageRoot, dirRel);
            if (!Directory.Exists(dirFull))
                continue;

            items.Add(new CacheItem
            {
                Name = $"Cursor: {dirRel}",
                FullPath = dirFull,
                OwnerApplication = "Cursor",
                IsDirectory = true
            });
        }

        foreach (var fileRel in FilesToDelete)
        {
            var fileFull = Path.Combine(globalStorageRoot, fileRel);
            if (!File.Exists(fileFull))
                continue;

            items.Add(new CacheItem
            {
                Name = $"Cursor: {fileRel}",
                FullPath = fileFull,
                OwnerApplication = "Cursor",
                IsDirectory = false
            });
        }

        return items;
    }

    public IReadOnlyList<CacheItem> GetClaudeCacheItems()
    {
        var items = new List<CacheItem>();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return items;

        var claudeRoot = Path.Combine(localAppData, ClaudeLocalAppDataRootRelative);
        if (!PathHelper.PathExists(claudeRoot))
            return items;

        foreach (var dirRel in ClaudeDirectoriesToDelete)
        {
            var dirFull = Path.Combine(claudeRoot, dirRel);
            if (!Directory.Exists(dirFull))
                continue;

            items.Add(new CacheItem
            {
                Name = $"Claude: {dirRel}",
                FullPath = dirFull,
                OwnerApplication = "Claude",
                IsDirectory = true
            });
        }

        return items;
    }
}

public sealed class AppCacheClearResult
{
    public long FreedBytes { get; set; }
    public int Errors { get; set; }
    public List<string> DeletedPaths { get; init; } = [];
}

public sealed class CacheItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required string OwnerApplication { get; init; }
    public bool IsDirectory { get; init; }
}

