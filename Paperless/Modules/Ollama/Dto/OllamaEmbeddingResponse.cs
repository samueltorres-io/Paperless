using System.Text.Json.Serialization;

namespace Paperless.Modules.Ollama.Dto;

public sealed class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];
}