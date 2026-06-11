using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Detection;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Utilities;

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
                pathsToScan.Add((ollamaPath, "Ollama"));

            // Hugging Face Hub special handling
            var hfHubPath = Path.Combine(userProfile, ".cache", "huggingface", "hub");
            if (HuggingFaceDetector.IsHuggingFaceHubPath(hfHubPath))
                pathsToScan.Add((hfHubPath, "Hugging Face"));

            // Docker volumes for Open WebUI / Ollama
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
                foreach (var discovered in OllamaPathDiscovery.DiscoverInDirectory(path, owner ?? "Ollama"))
                {
                    if (!pathsToScan.Any(p => p.Path.Equals(discovered.Path, StringComparison.OrdinalIgnoreCase)))
                        pathsToScan.Add(discovered);
                }

                foreach (var hfHub in HuggingFaceDetector.DiscoverHubPaths(path))
                {
                    if (!pathsToScan.Any(p => p.Path.Equals(hfHub, StringComparison.OrdinalIgnoreCase)))
                        pathsToScan.Add((hfHub, "Hugging Face"));
                }

                if (!pathsToScan.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    pathsToScan.Add((path, owner));
            }
        }

        PathHelper.RemoveAncestorPaths(pathsToScan, p => p.Path);

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
            if (OllamaDetector.IsOllamaModelsPath(path))
            {
                await ScanOllamaModelsPathAsync(path, owner, results, cancellationToken);
                return;
            }

            if (HuggingFaceDetector.IsHuggingFaceHubPath(path))
            {
                ScanHuggingFaceHubPath(path, owner, results, cancellationToken);
                return;
            }

            var normalizedPath = PathHelper.NormalizeDirectoryPath(path);
            foreach (var hfHub in HuggingFaceDetector.DiscoverHubPaths(path))
            {
                if (PathHelper.NormalizeDirectoryPath(hfHub).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_foundPaths.Add(hfHub))
                    ScanHuggingFaceHubPath(hfHub, owner, results, cancellationToken);
            }

            if (path.Contains("huggingface", StringComparison.OrdinalIgnoreCase))
                return;

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

    private Task ScanOllamaModelsPathAsync(
        string ollamaModelsPath,
        string? owner,
        List<ModelInfo> results,
        CancellationToken cancellationToken)
    {
        foreach (var entry in OllamaDetector.EnumerateOllamaModels(ollamaModelsPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_foundPaths.Add(entry.Path))
                continue;

            results.Add(OllamaModelInfoFactory.Create(entry, owner));
        }

        return Task.CompletedTask;
    }

    private void ScanHuggingFaceHubPath(
        string hubPath,
        string? owner,
        List<ModelInfo> results,
        CancellationToken cancellationToken)
    {
        foreach (var entry in HuggingFaceDetector.EnumerateModels(hubPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_foundPaths.Add(entry.Path))
                continue;

            results.Add(HuggingFaceModelInfoFactory.Create(entry, owner));
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
                    var isOllama = name.Contains("ollama", StringComparison.OrdinalIgnoreCase);
                    var isOpenWebUi = name.Contains("open-webui", StringComparison.OrdinalIgnoreCase);

                    if (!isOllama && !isOpenWebUi)
                        continue;

                    var owner = isOllama ? "Ollama (Docker)" : "Open WebUI (Docker)";
                    var discoveredOllama = false;

                    foreach (var discovered in OllamaPathDiscovery.DiscoverInDirectory(volume, owner))
                    {
                        pathsToScan.Add(discovered);
                        discoveredOllama = true;
                    }

                    if (!discoveredOllama && isOpenWebUi)
                        pathsToScan.Add((volume, owner));
                }
            }
            catch
            {
                // Docker volumes may be inaccessible
            }
        }, cancellationToken);
    }
}
