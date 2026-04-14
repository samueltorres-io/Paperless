using System.Text.Json.Serialization;

namespace Paperless.Modules.Ollama;

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    public ChatMessage() { }

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public static ChatMessage System(string content) => new("system", content);
    public static ChatMessage User(string content) => new("user", content);
    public static ChatMessage Assistant(string content) => new("assistant", content);
}