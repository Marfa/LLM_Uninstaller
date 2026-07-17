using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Updates;

public sealed class UpdateInstaller
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "LLMUninstaller" } }
    };

    public async Task InstallUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!UpdateDownloadPolicy.IsAllowedDownloadUrl(update.DownloadUrl))
            throw new InvalidOperationException("Update download URL is not allowed");

        if (string.IsNullOrWhiteSpace(update.Sha256Hex) || update.Sha256Hex.Length != 64)
            throw new InvalidOperationException("Update SHA-256 digest is missing");

        var currentExe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "LLMUninstaller.exe");

        // Unique dir reduces shared-%TEMP% TOCTOU vs a fixed folder name.
        var tempDir = Path.Combine(Path.GetTempPath(), "LLMUninstaller_update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var assetName = UpdateDownloadPolicy.SanitizeAssetFileName(update.AssetName);
        if (!PathHelper.TryJoinUnderRoot(tempDir, assetName, out var downloadPath))
            throw new InvalidOperationException("Update asset name is invalid");

        using (var response = await Http.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var finalUri = response.RequestMessage?.RequestUri?.AbsoluteUri ?? update.DownloadUrl;
            if (!UpdateDownloadPolicy.IsAllowedDownloadUrl(finalUri))
                throw new InvalidOperationException("Update download redirected to a disallowed host");

            var total = response.Content.Headers.ContentLength ?? -1;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(downloadPath);

            var buffer = new byte[81920];
            long read = 0;
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                read += bytesRead;
                if (total > 0)
                    progress?.Report((double)read / total * 100);
            }
        }

        var actualHash = UpdateDownloadPolicy.ComputeFileSha256Hex(downloadPath);
        if (!actualHash.Equals(update.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Update file hash does not match the release digest");

        var newExePath = downloadPath;

        if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(downloadPath, tempDir, overwriteFiles: true);
            var extracted = Directory.GetFiles(tempDir, "LLMUninstaller.exe", SearchOption.AllDirectories)
                .FirstOrDefault(path => PathHelper.IsUnderRoot(tempDir, path));
            if (extracted == null)
                throw new InvalidOperationException("LLMUninstaller.exe not found in update archive");
            newExePath = extracted;
        }

        if (!PathHelper.IsUnderRoot(tempDir, newExePath))
            throw new InvalidOperationException("Update executable escaped the staging directory");

        var stagedExe = Path.Combine(tempDir, "LLMUninstaller_new.exe");
        File.Copy(newExePath, stagedExe, overwrite: true);
        var stagedHash = UpdateDownloadPolicy.ComputeFileSha256Hex(stagedExe);

        LaunchUpdateScript(stagedExe, currentExe, stagedHash);
    }

    /// <summary>
    /// cmd.exe reads .bat files in the system OEM code page, which breaks paths with
    /// non-ASCII characters. PowerShell -EncodedCommand uses UTF-16LE and handles any path.
    /// </summary>
    private static void LaunchUpdateScript(string stagedExe, string currentExe, string stagedSha256Hex)
    {
        var parentPid = Environment.ProcessId;
        var psStaged = EscapePowerShellSingleQuoted(stagedExe);
        var psCurrent = EscapePowerShellSingleQuoted(currentExe);
        var hash = stagedSha256Hex.ToLowerInvariant();

        var script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            $"try {{ Wait-Process -Id {parentPid} -ErrorAction SilentlyContinue }} catch {{ }}",
            "",
            "$deadline = (Get-Date).AddMinutes(1)",
            "while ((Get-Date) -lt $deadline) {",
            "    try {",
            $"        $stream = [System.IO.File]::Open('{psCurrent}', [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)",
            "        $stream.Dispose()",
            "        break",
            "    } catch {",
            "        Start-Sleep -Milliseconds 250",
            "    }",
            "}",
            "",
            $"$expected = '{hash}'",
            $"$actual = (Get-FileHash -LiteralPath '{psStaged}' -Algorithm SHA256).Hash.ToLowerInvariant()",
            "if ($actual -ne $expected) { exit 1 }",
            "",
            $"Move-Item -LiteralPath '{psStaged}' -Destination '{psCurrent}' -Force",
            $"Start-Process -LiteralPath '{psCurrent}'",
        });

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''");
}
