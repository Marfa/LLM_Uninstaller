namespace LLMUninstaller.Core.Utilities;

public static class PathHelper
{
    public static bool PathExists(string path) =>
        Directory.Exists(path) || File.Exists(path);

    public static bool HasWriteAccess(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }

            if (Directory.Exists(path))
            {
                var testFile = Path.Combine(path, $".llmuninstaller_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return true;
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static long GetSize(string path)
    {
        if (File.Exists(path))
            return new FileInfo(path).Length;

        if (!Directory.Exists(path))
            return 0;

        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f =>
            {
                try { return new FileInfo(f).Length; }
                catch { return 0L; }
            });
    }

    public static (DateTime LastAccess, DateTime LastModified) GetTimestamps(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return (info.LastAccessTime, info.LastWriteTime);
            }

            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                return (info.LastAccessTime, info.LastWriteTime);
            }
        }
        catch
        {
            // ignored
        }

        return (DateTime.MinValue, DateTime.MinValue);
    }

    public static string GetDisplayName(string path) =>
        File.Exists(path) ? Path.GetFileName(path) : new DirectoryInfo(path).Name;

    /// <summary>
    /// Removes paths that are strict ancestors of other paths in the list,
    /// keeping the more specific (child) locations only.
    /// </summary>
    public static void RemoveAncestorPaths<T>(List<T> paths, Func<T, string> getPath)
    {
        var normalized = paths
            .Select(p => (Item: p, Path: NormalizeDirectoryPath(getPath(p))))
            .ToList();

        paths.RemoveAll(item =>
        {
            var current = NormalizeDirectoryPath(getPath(item));
            return normalized.Any(other =>
                !other.Path.Equals(current, StringComparison.OrdinalIgnoreCase) &&
                other.Path.StartsWith(current + "\\", StringComparison.OrdinalIgnoreCase));
        });
    }

    public static string NormalizeDirectoryPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// True when <paramref name="candidate"/> resolves inside <paramref name="root"/>
    /// (or is the root itself). Uses full paths and a trailing-separator prefix check
    /// so sibling names like "blobsX" do not match "blobs".
    /// </summary>
    public static bool IsUnderRoot(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
            return false;

        var rootFull = NormalizeDirectoryPath(root);
        var candidateFull = Path.GetFullPath(candidate);
        if (candidateFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = rootFull + Path.DirectorySeparatorChar;
        return candidateFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Joins a single path segment under <paramref name="root"/>. Rejects absolute paths,
    /// parent segments, and anything that would escape the root after GetFullPath.
    /// </summary>
    public static bool TryJoinUnderRoot(string root, string fileName, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        if (fileName is "." or ".." ||
            fileName.Contains("..", StringComparison.Ordinal) ||
            fileName.Contains('/') ||
            fileName.Contains('\\'))
            return false;

        // Path.Combine ignores root when the second arg is rooted (absolute/UNC).
        if (Path.IsPathRooted(fileName))
            return false;

        var combined = Path.GetFullPath(Path.Combine(root, fileName));
        if (!IsUnderRoot(root, combined))
            return false;

        fullPath = combined;
        return true;
    }

    /// <summary>
    /// Resolves reparse points (junctions/symlinks) to their ultimate target path.
    /// Returns the original path when it is not a reparse point or resolution fails.
    /// </summary>
    public static string ResolveReparseTarget(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var target = info.ResolveLinkTarget(true);
                    if (target != null)
                        return target.FullName;
                }
            }
            else if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var target = info.ResolveLinkTarget(true);
                    if (target != null)
                        return target.FullName;
                }
            }
        }
        catch
        {
            // ignored
        }

        return path;
    }
}
