namespace LLMUninstaller.Core.Updates;

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? AssetName { get; init; }
    /// <summary>Lowercase hex SHA-256 of the release asset (no sha256: prefix).</summary>
    public required string Sha256Hex { get; init; }
}

public sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public UpdateInfo? Update { get; init; }
    public string? ErrorMessage { get; init; }
}
