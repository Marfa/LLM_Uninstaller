namespace LLMUninstaller.Core.Utilities;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        if (bytes < 0) return "0 B";

        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex <= 1
            ? $"{size:F0} {Units[unitIndex]}"
            : $"{size:F2} {Units[unitIndex]}";
    }
}
