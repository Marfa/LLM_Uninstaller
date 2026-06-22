using System.Diagnostics;
using System.IO.Compression;
using System.Text;

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
        var currentExe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "LLMUninstaller.exe");

        var tempDir = Path.Combine(Path.GetTempPath(), "LLMUninstaller_update");
        Directory.CreateDirectory(tempDir);

        var downloadPath = Path.Combine(tempDir, update.AssetName ?? "update.exe");

        using (var response = await Http.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
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

        var newExePath = downloadPath;

        if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(downloadPath, tempDir, overwriteFiles: true);
            var extracted = Directory.GetFiles(tempDir, "LLMUninstaller.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (extracted == null)
                throw new InvalidOperationException("LLMUninstaller.exe not found in update archive");
            newExePath = extracted;
        }

        var stagedExe = Path.Combine(tempDir, "LLMUninstaller_new.exe");
        File.Copy(newExePath, stagedExe, overwrite: true);

        LaunchUpdateScript(stagedExe, currentExe);
    }

    /// <summary>
    /// cmd.exe reads .bat files in the system OEM code page, which breaks paths with
    /// non-ASCII characters. PowerShell -EncodedCommand uses UTF-16LE and handles any path.
    /// </summary>
    private static void LaunchUpdateScript(string stagedExe, string currentExe)
    {
        var script = $"""
            Start-Sleep -Seconds 2
            Move-Item -LiteralPath '{EscapePowerShellSingleQuoted(stagedExe)}' -Destination '{EscapePowerShellSingleQuoted(currentExe)}' -Force
            Start-Process -LiteralPath '{EscapePowerShellSingleQuoted(currentExe)}'
            """;

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
