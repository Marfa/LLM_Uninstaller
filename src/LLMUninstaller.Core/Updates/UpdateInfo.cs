namespace LLMUninstaller.Core.Updates;

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? AssetName { get; init; }
}

public sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public UpdateInfo? Update { get; init; }
    public string? ErrorMessage { get; init; }
}
