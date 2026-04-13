namespace Paperless.Modules.Ollama;

// Migrar JSON

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "phi3:mini";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 120;
}