namespace Paperless.Modules.Ollama;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3.5:0.8b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 120;
}