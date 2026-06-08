namespace LLMUninstaller.Core.Models;

public sealed class ScanOptions
{
    public bool ScanStandardPaths { get; init; } = true;
    public bool ScanAdditionalDisks { get; init; } = true;
    public IReadOnlyList<string> AdditionalDrives { get; init; } = ["C:", "D:", "E:"];
    public IProgress<ScanProgress>? Progress { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public sealed class ScanProgress
{
    public required string CurrentPath { get; init; }
    public int PathsScanned { get; init; }
    public int ModelsFound { get; init; }
}
