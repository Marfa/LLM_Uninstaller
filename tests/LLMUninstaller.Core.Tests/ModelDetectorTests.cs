using LLMUninstaller.Core.Constants;
using Xunit;
using LLMUninstaller.Core.Detection;
using LLMUninstaller.Core.Models;

namespace LLMUninstaller.Core.Tests;

public class ModelDetectorTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\models", true)]
    [InlineData(@"C:\Program Files\Ollama\models", true)]
    [InlineData(@"C:\Users\Public\models", true)]
    [InlineData(@"C:\Users\John\.ollama\models", false)]
    [InlineData(@"D:\AI\models", false)]
    public void ProtectedPaths_DetectsSystemDirectories(string path, bool expected)
    {
        Assert.Equal(expected, ProtectedPaths.IsProtected(path));
    }

    [Theory]
    [InlineData(".gguf", ModelType.LLM)]
    [InlineData(".ckpt", ModelType.Diffusion)]
    [InlineData(".onnx", ModelType.Diffusion)]
    [InlineData(".xyz", ModelType.Unknown)]
    public void ModelExtensions_ClassifiesCorrectly(string extension, ModelType expected)
    {
        Assert.Equal(expected, ModelExtensions.ClassifyExtension(extension));
    }

    [Theory]
    [InlineData(@"C:\ComfyUI\models\checkpoints\model.safetensors", ModelType.Diffusion)]
    [InlineData(@"C:\AI\embeddings\model.bin", ModelType.Embedding)]
    [InlineData(@"C:\models\llama.gguf", ModelType.LLM)]
    public void ModelExtensions_ClassifiesByPath(string path, ModelType expected)
    {
        Assert.Equal(expected, ModelExtensions.ClassifyByPath(path));
    }
}
