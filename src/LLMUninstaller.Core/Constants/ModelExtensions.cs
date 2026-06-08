using LLMUninstaller.Core.Models;

namespace LLMUninstaller.Core.Constants;

public static class ModelExtensions
{
    private static readonly HashSet<string> LlmExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gguf", ".bin", ".safetensors", ".pth", ".pt" };

    private static readonly HashSet<string> DiffusionExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ckpt", ".safetensors", ".onnx" };

    private static readonly HashSet<string> EmbeddingExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gguf", ".bin", ".safetensors" };

    public static readonly HashSet<string> AllExtensions = new(StringComparer.OrdinalIgnoreCase);

    static ModelExtensions()
    {
        foreach (var ext in LlmExtensions) AllExtensions.Add(ext);
        foreach (var ext in DiffusionExtensions) AllExtensions.Add(ext);
        foreach (var ext in EmbeddingExtensions) AllExtensions.Add(ext);
    }

    public static bool IsModelExtension(string extension) =>
        AllExtensions.Contains(extension);

    public static ModelType ClassifyExtension(string extension)
    {
        if (LlmExtensions.Contains(extension) && !DiffusionExtensions.Contains(extension) && !EmbeddingExtensions.Contains(extension))
            return ModelType.LLM;

        var inLlm = LlmExtensions.Contains(extension);
        var inDiffusion = DiffusionExtensions.Contains(extension);
        var inEmbedding = EmbeddingExtensions.Contains(extension);

        if (inDiffusion && !inEmbedding && !inLlm)
            return ModelType.Diffusion;

        if (inEmbedding && !inDiffusion)
            return ModelType.Embedding;

        if (inLlm)
            return ModelType.LLM;

        if (inDiffusion)
            return ModelType.Diffusion;

        return ModelType.Unknown;
    }

    public static ModelType ClassifyByPath(string path)
    {
        var lower = path.ToLowerInvariant();

        if (lower.Contains("embedding") || lower.Contains("embed"))
            return ModelType.Embedding;

        if (lower.Contains("checkpoint") || lower.Contains("diffusion") ||
            lower.Contains("vae") || lower.Contains("lora") || lower.Contains("unet") ||
            lower.Contains("comfyui"))
            return ModelType.Diffusion;

        return ModelType.LLM;
    }
}
