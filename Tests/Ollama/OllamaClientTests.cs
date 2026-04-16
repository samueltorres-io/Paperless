using Xunit;
using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Paperless.Configuration;
using Paperless.Modules.Ollama;
using Paperless.Modules.Ollama.Dto;

namespace Paperless.Tests.Ollama;

public class OllamaClientTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly OllamaClient _client;
    private readonly OllamaOptions _options;
    private readonly HttpClient _httpClient;

    public OllamaClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _options = new OllamaOptions
        {
            BaseUrl = "http://localhost:11434",
            Model = "test-model",
            EmbeddingModel = "test-embed",
            TimeoutSeconds = 10,
        };

        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/")
        };

        _client = new OllamaClient(_httpClient, _options);
    }

    public void Dispose() => _httpClient.Dispose();

    private void SetupResponse(HttpStatusCode status, object? body = null)
    {
        var json = body is not null
            ? JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : "";

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private void SetupException<TException>(TException ex) where TException : Exception
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(ex);
    }

    [Fact]
    public async Task HealthCheck_WhenOllamaIsUp_ShouldReturnTrue()
    {
        SetupResponse(HttpStatusCode.OK);

        var result = await _client.HealthCheckAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task HealthCheck_WhenOllamaIsDown_ShouldReturnFalse()
    {
        SetupException(new HttpRequestException("Connection refused"));

        var result = await _client.HealthCheckAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HealthCheck_WhenTimeout_ShouldReturnFalse()
    {
        SetupException(new TaskCanceledException("Timeout"));

        var result = await _client.HealthCheckAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HealthCheck_WhenServerError_ShouldReturnFalse()
    {
        SetupResponse(HttpStatusCode.InternalServerError);

        var result = await _client.HealthCheckAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task ChatAsync_WithValidResponse_ShouldReturnContent()
    {
        var responseBody = new OllamaChatResponse
        {
            Model = "test-model",
            Message = new ChatMessage("assistant", "Olá, mundo!"),
            Done = true,
        };
        SetupResponse(HttpStatusCode.OK, responseBody);

        var messages = new[] { ChatMessage.User("Oi") };
        var result = await _client.ChatAsync(messages);

        Assert.Equal("Olá, mundo!", result);
    }

    [Fact]
    public async Task ChatAsync_ShouldTrimResponse()
    {
        var responseBody = new OllamaChatResponse
        {
            Model = "test-model",
            Message = new ChatMessage("assistant", "  espaços  "),
            Done = true,
        };
        SetupResponse(HttpStatusCode.OK, responseBody);

        var result = await _client.ChatAsync([ChatMessage.User("X")]);

        Assert.Equal("espaços", result);
    }

    [Fact]
    public async Task ChatAsync_WithEmptyResponse_ShouldThrow()
    {
        var responseBody = new OllamaChatResponse
        {
            Model = "test-model",
            Message = new ChatMessage("assistant", ""),
            Done = true,
        };
        SetupResponse(HttpStatusCode.OK, responseBody);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.ChatAsync([ChatMessage.User("X")]));
    }

    [Fact]
    public async Task ChatAsync_WithHttpError_ShouldThrow()
    {
        SetupResponse(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.ChatAsync([ChatMessage.User("X")]));
    }

    [Fact]
    public async Task ChatAsync_WhenConnectionRefused_ShouldThrowInvalidOperation()
    {
        SetupException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.ChatAsync([ChatMessage.User("X")]));
    }

    [Fact]
    public async Task ChatAsync_SendsCorrectRequestPayload()
    {
        HttpRequestMessage? capturedRequest = null;

        var responseBody = new OllamaChatResponse
        {
            Model = "test-model",
            Message = new ChatMessage("assistant", "OK"),
            Done = true,
        };

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8, "application/json"),
            });

        await _client.ChatAsync([ChatMessage.User("teste")]);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Contains("api/chat", capturedRequest.RequestUri!.ToString());

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.Contains("\"model\"", body);
        Assert.Contains("\"stream\":false", body);
        Assert.Contains("\"messages\"", body);
    }

    [Fact]
    public async Task EmbedAsync_WithValidResponse_ShouldReturnEmbedding()
    {
        var responseBody = new OllamaEmbeddingResponse
        {
            Embedding = [0.1f, 0.2f, 0.3f],
        };
        SetupResponse(HttpStatusCode.OK, responseBody);

        var result = await _client.EmbedAsync("texto teste");

        Assert.Equal(3, result.Length);
        Assert.Equal(0.1f, result[0], precision: 5);
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyEmbedding_ShouldThrow()
    {
        var responseBody = new OllamaEmbeddingResponse { Embedding = [] };
        SetupResponse(HttpStatusCode.OK, responseBody);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.EmbedAsync("texto"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmbedAsync_WithEmptyText_ShouldThrowArgumentException(string? text)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.EmbedAsync(text!));
    }

    [Fact]
    public async Task EmbedAsync_WithHttpError_ShouldThrow()
    {
        SetupResponse(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.EmbedAsync("texto"));
    }
}
