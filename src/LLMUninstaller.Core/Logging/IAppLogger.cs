using LLMUninstaller.Core.Models;

namespace LLMUninstaller.Core.Logging;

public interface IAppLogger
{
    Task LogFoundModelAsync(ModelInfo model);
    Task LogDeletedModelAsync(ModelInfo model, long freedBytes);
    Task LogErrorAsync(string context, string message);
    Task<IReadOnlyList<LogEntry>> GetEntriesAsync();
}

public sealed class LogEntry
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = "";
    public string Path { get; init; } = "";
    public string? Details { get; init; }
    public long? SizeBytes { get; init; }
}
