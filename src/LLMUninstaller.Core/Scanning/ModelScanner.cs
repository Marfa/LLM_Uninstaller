using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Detection;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Logging;

namespace LLMUninstaller.Core.Scanning;

public sealed class ModelScanner
{
    private readonly IAppLogger _logger;
    private readonly HashSet<string> _foundPaths = new(StringComparer.OrdinalIgnoreCase);

    public ModelScanner(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelInfo>> ScanAsync(ScanOptions options)
    {
        _foundPaths.Clear();
        var results = new List<ModelInfo>();
        var pathsToScan = new List<(string Path, string? Owner)>();

        if (options.ScanStandardPaths)
        {
            foreach (var location in StandardPaths.GetStandardLocations())
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                if (location.IsPattern)
                {
                    if (Directory.Exists(location.Path))
                        pathsToScan.Add((location.Path, location.OwnerApplication));
                }
                else if (Directory.Exists(location.Path))
                {
                    pathsToScan.Add((location.Path, location.OwnerApplication));
                }
            }

            // ComfyUI subdirectories
            foreach (var (basePath, owner) in pathsToScan.Where(p => p.Owner == "ComfyUI").ToList())
            {
                foreach (var sub in StandardPaths.ComfyUiSubdirectories)
                {
                    var subPath = Path.Combine(basePath, sub);
                    if (Directory.Exists(subPath))
                        pathsToScan.Add((subPath, owner));
                }
            }

            // Ollama special handling
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var ollamaPath = Path.Combine(userProfile, ".ollama", "models");
            if (OllamaDetector.IsOllamaModelsPath(ollamaPath))
            {
                foreach (var ollamaModel in OllamaDetector.EnumerateOllamaModels(ollamaPath))
                    pathsToScan.Add((ollamaModel, "Ollama"));
            }

            // Docker volumes for Open WebUI
            await ScanDockerVolumesAsync(pathsToScan, options.CancellationToken);
        }

        var standardFound = pathsToScan.Any(p => Directory.Exists(p.Path));
        if (!standardFound && options.ScanAdditionalDisks)
        {
            foreach (var (path, owner) in DiskScanner.ScanDrives(options.AdditionalDrives, options.CancellationToken))
                pathsToScan.Add((path, owner));
        }
        else if (options.ScanAdditionalDisks)
        {
            // Supplement standard paths with disk scan for missed locations
            foreach (var (path, owner) in DiskScanner.ScanDrives(options.AdditionalDrives, options.CancellationToken))
            {
                if (!pathsToScan.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    pathsToScan.Add((path, owner));
            }
        }

        var scanned = 0;
        foreach (var (path, owner) in pathsToScan)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            scanned++;

            options.Progress?.Report(new ScanProgress
            {
                CurrentPath = path,
                PathsScanned = scanned,
                ModelsFound = results.Count
            });

            try
            {
                await ScanPathAsync(path, owner, results, options.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Ошибка сканирования: {path}", ex.Message);
            }
        }

        var sorted = results.OrderByDescending(m => m.SizeBytes).ToList();

        foreach (var model in sorted)
            await _logger.LogFoundModelAsync(model);

        return sorted;
    }

    private async Task ScanPathAsync(
        string path,
        string? owner,
        List<ModelInfo> results,
        CancellationToken cancellationToken)
    {
        if (!_foundPaths.Add(path))
            return;

        if (Directory.Exists(path))
        {
            // Check if the directory itself is a model
            var dirModel = ModelDetector.CreateModelInfo(path, owner);
            if (dirModel != null)
            {
                results.Add(dirModel);
                return;
            }

            // Scan children for individual model files/directories
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                await _logger.LogErrorAsync($"Нет доступа: {path}", ex.Message);
                return;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_foundPaths.Add(entry))
                    continue;

                var model = ModelDetector.CreateModelInfo(entry, owner);
                if (model != null)
                    results.Add(model);
            }
        }
        else if (File.Exists(path))
        {
            var model = ModelDetector.CreateModelInfo(path, owner);
            if (model != null)
                results.Add(model);
        }
    }

    private static Task ScanDockerVolumesAsync(
        List<(string Path, string? Owner)> pathsToScan,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var programData = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
            var dockerVolumesPath = Path.Combine(programData, "Docker", "volumes");

            if (!Directory.Exists(dockerVolumesPath))
                return;

            try
            {
                foreach (var volume in Directory.EnumerateDirectories(dockerVolumesPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var name = Path.GetFileName(volume);
                    if (name.Contains("open-webui", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        pathsToScan.Add((volume, "Open WebUI (Docker)"));
                    }
                }
            }
            catch
            {
                // Docker volumes may be inaccessible
            }
        }, cancellationToken);
    }
}
