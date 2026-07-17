using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Constants;

public static class ProtectedPaths
{
    private static readonly string[] SystemRoots = BuildSystemRoots();

    private static string[] BuildSystemRoots()
    {
        var roots = new List<string>();

        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        var publicFolder = Environment.GetEnvironmentVariable("PUBLIC");
        if (string.IsNullOrWhiteSpace(publicFolder))
        {
            publicFolder = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "..",
                "Public"));
        }

        AddRoot(roots, publicFolder);

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddRoot(List<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            roots.Add(PathHelper.NormalizeDirectoryPath(path));
        }
        catch
        {
            // ignore invalid roots on non-Windows hosts
        }
    }

    public static bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var full = Path.GetFullPath(path);
            return SystemRoots.Any(root => PathHelper.IsUnderRoot(root, full));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsProtectedIncludingReparseTarget(string path) =>
        IsProtected(path) || IsProtected(PathHelper.ResolveReparseTarget(path));

    public static string GetProtectionReason(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            foreach (var root in SystemRoots)
            {
                if (!PathHelper.IsUnderRoot(root, full))
                    continue;

                var label = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return string.IsNullOrEmpty(label)
                    ? "Путь находится в защищённой системной области"
                    : $"Путь находится в защищённой системной области: {label}";
            }
        }
        catch
        {
            // ignored
        }

        return "Защищённый системный путь";
    }
}
