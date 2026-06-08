namespace LLMUninstaller.Core.Models;

public sealed class DeleteOptions
{
    public bool UseRecycleBin { get; init; } = true;
    public bool AllowProtectedPaths { get; init; }
    public bool SkipConfirmation { get; init; }
}

public sealed class DeleteResult
{
    public required string Path { get; init; }
    public bool Success { get; init; }
    public long FreedBytes { get; init; }
    public string? ErrorMessage { get; init; }
}
