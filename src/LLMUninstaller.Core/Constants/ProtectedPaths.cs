namespace LLMUninstaller.Core.Constants;

public static class ProtectedPaths
{
    private static readonly string[] ProtectedSegments =
    [
        "windows",
        "program files",
        "program files (x86)",
        "programdata",
        @"users\public"
    ];

    public static bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant();

        foreach (var segment in ProtectedSegments)
        {
            if (normalized.Contains(segment, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string GetProtectionReason(string path)
    {
        var normalized = Path.GetFullPath(path).ToLowerInvariant();

        foreach (var segment in ProtectedSegments)
        {
            if (normalized.Contains(segment, StringComparison.Ordinal))
                return $"Путь находится в защищённой системной области: {segment}";
        }

        return "Защищённый системный путь";
    }
}
