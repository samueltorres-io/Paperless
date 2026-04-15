using System.Text.Json.Serialization;

namespace Paperless.Modules.Ollama.Dto;

public sealed class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }
}