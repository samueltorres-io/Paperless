namespace Paperless.Modules.Ollama;

public interface IOllamaClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<string> ChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default);
}