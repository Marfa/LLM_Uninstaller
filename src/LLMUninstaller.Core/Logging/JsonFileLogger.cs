using System.Text.Json;
using LLMUninstaller.Core.Models;

namespace LLMUninstaller.Core.Logging;

public sealed class JsonFileLogger : IAppLogger
{
    private readonly string _logFilePath;
    private readonly List<LogEntry> _entries = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private long _nextId = 1;

    public JsonFileLogger(string? logFilePath = null)
    {
        _logFilePath = logFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LLMUninstaller", "logs.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        LoadExisting();
    }

    private void LoadExisting()
    {
        if (!File.Exists(_logFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_logFilePath);
            var existing = JsonSerializer.Deserialize<List<LogEntry>>(json);
            if (existing != null)
            {
                _entries.AddRange(existing);
                _nextId = existing.Count > 0 ? existing.Max(e => e.Id) + 1 : 1;
            }
        }
        catch
        {
            // start fresh
        }
    }

    public async Task LogFoundModelAsync(ModelInfo model)
    {
        await AddEntryAsync("found", model.FullPath,
            JsonSerializer.Serialize(new { model.Type, model.OwnerApplication, model.SizeBytes }),
            model.SizeBytes);
    }

    public async Task LogDeletedModelAsync(ModelInfo model, long freedBytes)
    {
        await AddEntryAsync("deleted", model.FullPath,
            JsonSerializer.Serialize(new { model.Type, FreedBytes = freedBytes }),
            freedBytes);
    }

    public async Task LogErrorAsync(string context, string message) =>
        await AddEntryAsync("error", context, message, null);

    public Task<IReadOnlyList<LogEntry>> GetEntriesAsync() =>
        Task.FromResult<IReadOnlyList<LogEntry>>(_entries.AsReadOnly());

    private async Task AddEntryAsync(string eventType, string path, string? details, long? sizeBytes)
    {
        await _lock.WaitAsync();
        try
        {
            _entries.Add(new LogEntry
            {
                Id = _nextId++,
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Path = path,
                Details = details,
                SizeBytes = sizeBytes
            });

            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_logFilePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }
}
