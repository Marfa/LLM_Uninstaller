using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LLMUninstaller.Core.Updates;

public sealed class UpdateChecker
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "LLMUninstaller" } }
    };

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await Http.GetFromJsonAsync<GitHubRelease>(
                AppInfo.ReleasesApiUrl, cancellationToken);

            if (release?.TagName == null)
                return new UpdateCheckResult { UpdateAvailable = false };

            var latestVersion = release.TagName.TrimStart('v', 'V');
            if (!IsNewerVersion(latestVersion, AppInfo.Version))
                return new UpdateCheckResult { UpdateAvailable = false };

            var asset = release.Assets?
                .FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.Contains("LLMUninstaller", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets?.FirstOrDefault(a =>
                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets?.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (asset?.BrowserDownloadUrl == null)
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    ErrorMessage = "Release found but no downloadable asset"
                };

            return new UpdateCheckResult
            {
                UpdateAvailable = true,
                Update = new UpdateInfo
                {
                    Version = latestVersion,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    AssetName = asset.Name,
                    ReleaseNotes = release.Body
                }
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { ErrorMessage = ex.Message };
        }
    }

    public static bool IsNewerVersion(string remote, string current)
    {
        if (!Version.TryParse(NormalizeVersion(remote), out var remoteVer))
            return false;
        if (!Version.TryParse(NormalizeVersion(current), out var currentVer))
            return true;
        return remoteVer > currentVer;
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('-')[0].Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0",
            2 => $"{parts[0]}.{parts[1]}.0",
            _ => version
        };
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
