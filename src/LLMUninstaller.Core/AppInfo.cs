namespace LLMUninstaller.Core;

public static class AppInfo
{
    public const string Version = "1.0.0";
    public const string GitHubOwner = "Marfa";
    public const string GitHubRepo = "LLM_Uninstaller";
    public const string SourceCodeUrl = "https://github.com/Marfa/LLM_Uninstaller";
    public const string DonationUrl = "https://www.donationalerts.com/r/themarfa";
    public const string CryptoDonationUrl = "https://nowpayments.io/donation/themarfa";
    public static string ReleasesApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
