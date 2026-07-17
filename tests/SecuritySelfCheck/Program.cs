using System.Security.Cryptography;
using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Detection;
using LLMUninstaller.Core.Updates;
using LLMUninstaller.Core.Utilities;

// Minimal assert-based check for Critical/High security guards.
// Run: dotnet run --project tests/SecuritySelfCheck

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception("FAIL: " + message);
    Console.WriteLine("ok: " + message);
}

var root = Path.Combine(Path.GetTempPath(), "llmu_sec_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Assert(PathHelper.TryJoinUnderRoot(root, "blob.bin", out var okPath) &&
           PathHelper.IsUnderRoot(root, okPath),
        "safe join stays under root");

    Assert(!PathHelper.TryJoinUnderRoot(root, @"..\escape.bin", out _),
        "reject parent segment");

    Assert(!PathHelper.TryJoinUnderRoot(root, Path.Combine(Path.GetTempPath(), "abs.bin"), out _),
        "reject absolute second segment");

    Assert(!PathHelper.TryJoinUnderRoot(root, "a/b", out _),
        "reject nested separators in file name");

    Assert(UpdateDownloadPolicy.IsAllowedDownloadUrl(
            "https://github.com/Marfa/LLM_Uninstaller/releases/download/v1.0.2/LLMUninstaller.exe"),
        "allow github release URL for this repo");

    Assert(!UpdateDownloadPolicy.IsAllowedDownloadUrl(
            "https://evil.example/LLMUninstaller.exe"),
        "reject foreign host");

    Assert(!UpdateDownloadPolicy.IsAllowedDownloadUrl(
            "https://github.com/other/repo/releases/download/v1/x.exe"),
        "reject other github repo");

    Assert(UpdateDownloadPolicy.IsAllowedDownloadUrl(
            "https://objects.githubusercontent.com/github-production-release-asset/1/x"),
        "allow githubusercontent CDN");

    Assert(UpdateDownloadPolicy.TryParseSha256Digest(
            "sha256:adc3709de776e89c93d5dc9580ccbdf1dcdaeb5776f4c14415d4ef5ab69ce8f4",
            out var hex) && hex.Length == 64,
        "parse sha256 digest");

    Assert(!UpdateDownloadPolicy.TryParseSha256Digest("md5:deadbeef", out _),
        "reject non-sha256 digest");

    var sample = Path.Combine(root, "sample.bin");
    File.WriteAllBytes(sample, "hello"u8.ToArray());
    var expected = Convert.ToHexString(SHA256.HashData("hello"u8.ToArray())).ToLowerInvariant();
    Assert(UpdateDownloadPolicy.ComputeFileSha256Hex(sample) == expected,
        "file sha256 matches");

    Assert(UpdateDownloadPolicy.SanitizeAssetFileName(@"C:\evil\..\LLMUninstaller.exe") ==
           "LLMUninstaller.exe",
        "asset name is filename-only");

    var blobs = Path.Combine(root, "blobs");
    Directory.CreateDirectory(blobs);
    Assert(LLMUninstaller.Core.Scanning.OllamaDetector.DigestToBlobPath(blobs, "sha256:abc") is { } blob &&
           PathHelper.IsUnderRoot(blobs, blob),
        "ollama digest maps under blobs");
    Assert(LLMUninstaller.Core.Scanning.OllamaDetector.DigestToBlobPath(blobs, @"sha256:..\windows\system32") is null,
        "ollama digest traversal rejected");
    Assert(LLMUninstaller.Core.Scanning.HuggingFaceDetector.BlobHashToPath(blobs, "../x") is null,
        "hf hash traversal rejected");

    var sysRoot = Path.Combine(root, "SystemArea");
    Directory.CreateDirectory(sysRoot);
    var inside = Path.Combine(sysRoot, "nested");
    Directory.CreateDirectory(inside);
    Assert(PathHelper.IsUnderRoot(sysRoot, inside),
        "protected root prefix matches children");
    var sibling = Path.Combine(root, "SystemAreaX", "nested");
    Directory.CreateDirectory(sibling);
    Assert(!PathHelper.IsUnderRoot(sysRoot, sibling),
        "protected root prefix rejects sibling directory names");

    var sizeOnlyDir = Path.Combine(root, "size_only");
    Directory.CreateDirectory(sizeOnlyDir);
    File.WriteAllBytes(Path.Combine(sizeOnlyDir, "data.bin"), new byte[1024]);
    Assert(!ModelDetector.IsModelDirectory(sizeOnlyDir),
        "model dir requires large model file not size alone");

    var modelDir = Path.Combine(root, "model_dir");
    Directory.CreateDirectory(modelDir);
    File.WriteAllBytes(
        Path.Combine(modelDir, "weights.gguf"),
        new byte[ModelDetector.LargeFileThresholdBytes + 1]);
    Assert(ModelDetector.IsModelDirectory(modelDir),
        "model dir accepts directory with large model file");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
}

Console.WriteLine("SecuritySelfCheck passed.");
