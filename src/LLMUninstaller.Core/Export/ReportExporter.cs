using System.Globalization;
using System.Text;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Export;

public static class ReportExporter
{
    public static async Task ExportCsvAsync(IReadOnlyList<ModelInfo> models, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,FullPath,SizeBytes,SizeFormatted,Type,OwnerApplication,LastAccessTime,LastModifiedTime,IsProtectedPath");

        foreach (var m in models)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(m.Name),
                EscapeCsv(m.FullPath),
                m.SizeBytes.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(m.FormattedSize),
                EscapeCsv(m.Type.ToString()),
                EscapeCsv(m.OwnerApplication ?? ""),
                EscapeCsv(m.LastAccessTime.ToString("O")),
                EscapeCsv(m.LastModifiedTime.ToString("O")),
                m.IsProtectedPath.ToString()));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
