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
}
