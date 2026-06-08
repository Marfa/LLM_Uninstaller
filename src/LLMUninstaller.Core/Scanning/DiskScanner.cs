using LLMUninstaller.Core.Constants;

namespace LLMUninstaller.Core.Scanning;

public static class DiskScanner
{
    private static readonly HashSet<string> VisitedPaths = new(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<(string Path, string? Owner)> ScanDrives(
        IReadOnlyList<string> drives,
        CancellationToken cancellationToken)
    {
        VisitedPaths.Clear();

        foreach (var drive in drives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(drive))
                continue;

            foreach (var dirName in StandardPaths.AdditionalSearchDirectoryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IEnumerable<string> matches;
                try
                {
                    matches = FindDirectoriesByName(drive, dirName, maxDepth: 4, cancellationToken).ToList();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    continue;
                }

                foreach (var match in matches)
                {
                    if (VisitedPaths.Add(match))
                        yield return (match, null);
                }
            }
        }
    }

    private static IEnumerable<string> FindDirectoriesByName(
        string root,
        string dirName,
        int maxDepth,
        CancellationToken cancellationToken,
        int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
            yield break;

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(root);
        }
        catch
        {
            yield break;
        }

        foreach (var subdir in subdirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(subdir);
            if (name.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                yield return subdir;

            foreach (var nested in FindDirectoriesByName(subdir, dirName, maxDepth, cancellationToken, currentDepth + 1))
                yield return nested;
        }
    }
}
