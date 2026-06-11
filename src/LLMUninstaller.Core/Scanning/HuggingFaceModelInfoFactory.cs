using LLMUninstaller.Core.Constants;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Core.Scanning;

public static class HuggingFaceModelInfoFactory
{
    public static ModelInfo Create(HuggingFaceModelEntry entry, string? ownerApplication)
    {
        var (lastAccess, lastModified) = PathHelper.GetTimestamps(entry.Path);
        var size = entry.SizeBytes > 0 ? entry.SizeBytes : PathHelper.GetSize(entry.Path);

        return new ModelInfo
        {
            Name = entry.Name,
            FullPath = Path.GetFullPath(entry.Path),
            SizeBytes = size,
            Type = ModelType.LLM,
            OwnerApplication = ownerApplication,
            LastAccessTime = lastAccess == DateTime.MinValue ? entry.LastModified : lastAccess,
            LastModifiedTime = lastModified == DateTime.MinValue ? entry.LastModified : lastModified,
            IsProtectedPath = ProtectedPaths.IsProtected(entry.Path),
            IsDirectory = true
        };
    }
}
