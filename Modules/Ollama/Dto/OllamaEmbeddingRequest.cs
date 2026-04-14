using System.Text.Json.Serialization;

namespace Paperless.Modules.Ollama.Dto;

public sealed class OllamaEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
}