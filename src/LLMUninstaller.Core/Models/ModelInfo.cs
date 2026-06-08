namespace LLMUninstaller.Core.Models;

public sealed class ModelInfo
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public ModelType Type { get; init; }
    public string? OwnerApplication { get; init; }
    public DateTime LastAccessTime { get; init; }
    public DateTime LastModifiedTime { get; init; }
    public bool IsProtectedPath { get; init; }
    public bool IsDirectory { get; init; }

    public string FormattedSize => Utilities.SizeFormatter.Format(SizeBytes);
}
