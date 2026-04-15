using System.Text.Json;
using Paperless.Modules.Ollama;
using Paperless.Modules.Ollama.Dto;

namespace Paperless.Tests.Ollama;

public class OllamaDtoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void ChatRequest_ShouldSerializeWithCorrectPropertyNames()
    {
        var request = new OllamaChatRequest
        {
            Model = "phi3",
            Messages = [ChatMessage.User("Olá")],
            Stream = false,
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"model\"", json);
        Assert.Contains("\"messages\"", json);
        Assert.Contains("\"stream\"", json);
        Assert.Contains("\"phi3\"", json);
    }

    [Fact]
    public void ChatRequest_StreamDefault_ShouldBeFalse()
    {
        var request = new OllamaChatRequest();

        Assert.False(request.Stream);
    }

    [Fact]
    public void ChatResponse_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "model": "phi3",
            "message": { "role": "assistant", "content": "Resposta" },
            "done": true,
            "total_duration": 123456,
            "eval_count": 42
        }
        """;

        var response = JsonSerializer.Deserialize<OllamaChatResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("phi3", response!.Model);
        Assert.Equal("assistant", response.Message.Role);
        Assert.Equal("Resposta", response.Message.Content);
        Assert.True(response.Done);
        Assert.Equal(123456L, response.TotalDuration);
        Assert.Equal(42, response.EvalCount);
    }

    [Fact]
    public void ChatResponse_WithMissingOptionalFields_ShouldDeserialize()
    {
        var json = """
        {
            "model": "test",
            "message": { "role": "assistant", "content": "OK" },
            "done": true
        }
        """;

        var response = JsonSerializer.Deserialize<OllamaChatResponse>(json);

        Assert.NotNull(response);
        Assert.Null(response!.TotalDuration);
        Assert.Null(response.EvalCount);
    }

    [Fact]
    public void EmbeddingRequest_ShouldSerializeWithCorrectPropertyNames()
    {
        var request = new OllamaEmbeddingRequest
        {
            Model = "nomic-embed-text",
            Prompt = "texto para vetorizar",
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"model\"", json);
        Assert.Contains("\"prompt\"", json);
        Assert.Contains("\"nomic-embed-text\"", json);
    }

    [Fact]
    public void EmbeddingResponse_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "embedding": [0.1, 0.2, 0.3, 0.4]
        }
        """;

        var response = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(json);

        Assert.NotNull(response);
        Assert.Equal(4, response!.Embedding.Length);
        Assert.Equal(0.1f, response.Embedding[0], precision: 5);
        Assert.Equal(0.4f, response.Embedding[3], precision: 5);
    }

    [Fact]
    public void EmbeddingResponse_DefaultEmbedding_ShouldBeEmptyArray()
    {
        var response = new OllamaEmbeddingResponse();

        Assert.NotNull(response.Embedding);
        Assert.Empty(response.Embedding);
    }

    [Fact]
    public void ChatMessage_ShouldRoundtripThroughJson()
    {
        var original = ChatMessage.System("Prompt de sistema");

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("system", deserialized!.Role);
        Assert.Equal("Prompt de sistema", deserialized.Content);
    }

    [Fact]
    public void ChatMessage_JsonPropertyNames_ShouldBeLowercase()
    {
        var msg = ChatMessage.User("teste");

        var json = JsonSerializer.Serialize(msg);

        Assert.Contains("\"role\"", json);
        Assert.Contains("\"content\"", json);
        // Não deve ter PascalCase
        Assert.DoesNotContain("\"Role\"", json);
        Assert.DoesNotContain("\"Content\"", json);
    }
}
