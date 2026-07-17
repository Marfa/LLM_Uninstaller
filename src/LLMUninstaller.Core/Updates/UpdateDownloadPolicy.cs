using System.Security.Cryptography;

namespace LLMUninstaller.Core.Updates;

public static class UpdateDownloadPolicy
{
    public static bool IsAllowedDownloadUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host;
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var expectedPrefix = $"/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/";
            return uri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase);
        }

        // GitHub CDN hosts for release binaries (objects / release-assets).
        return host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseSha256Digest(string? digest, out string hex)
    {
        hex = "";
        if (string.IsNullOrWhiteSpace(digest))
            return false;

        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var value = digest[prefix.Length..].Trim();
        if (value.Length != 64)
            return false;

        foreach (var c in value)
        {
            var isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!isHex)
                return false;
        }

        hex = value.ToLowerInvariant();
        return true;
    }

    public static string ComputeSha256Hex(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeFileSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputeSha256Hex(stream);
    }

    public static string SanitizeAssetFileName(string? assetName)
    {
        var name = (assetName ?? "").Trim().Replace('\\', '/');
        name = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "update.bin";
        if (name.Contains("..", StringComparison.Ordinal))
            return "update.bin";
        return name;
    }
}
