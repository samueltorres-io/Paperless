namespace Paperless.Modules.Ollama;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:0.5b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 120;
}