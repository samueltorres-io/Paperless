using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using Paperless.Modules.Ollama.Dto;
using System.Text;
using System.Net;
using System.Threading;

namespace Paperless.Modules.Ollama;

public sealed class OllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public OllamaClient(OllamaOptions options)
    {
        _options = options;
        _http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /* Ollama Helth Check */
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CreateTimeoutCts(_options.TimeoutSeconds, cancellationToken);
            using var response = await _http.GetAsync("", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /* Chat -> Envia msm e recebe respost do modelo (sem streaming) */
    public async Task<string> ChatAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages.ToList(),
            Stream = false,
        };

        using var httpResponse = await PostJsonAsync("api/chat", request, _options.TimeoutSeconds, cancellationToken);

        await EnsureSuccessOrThrowAsync(httpResponse, "api/chat");

        var result = await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponse>(_jsonOptions, cancellationToken);

        if (result is null || string.IsNullOrWhiteSpace(result.Message.Content))
            throw new InvalidOperationException("Ollama return the empty response!");

        return result.Message.Content.Trim();
    }

    /* Embed -> Gera vetor de embedding para um texto */
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Embedding text cannot be empty!", nameof(text));

        var request = new OllamaEmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Prompt = text,
        };

        using var httpResponse = await PostJsonAsync("api/embeddings", request, _options.TimeoutSeconds, cancellationToken);

        await EnsureSuccessOrThrowAsync(httpResponse, "api/embeddings");

        var result = await httpResponse.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(_jsonOptions, cancellationToken);

        if (result is null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama return the empty embedding!");

        return result.Embedding;
    }

    /* Helpers */
    private async Task<HttpResponseMessage> PostJsonAsync<T>(
        string endpoint,
        T payload,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var cts = CreateTimeoutCts(timeoutSeconds, cancellationToken);
            return await _http.PostAsync(endpoint, content, cts.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not connect to Ollama at {_options.BaseUrl}. " +
                "Check that the service is running ('ollama serve').", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Ollama did not respond within {timeoutSeconds}s (timeout). " +
                "This may happen while the model is loading or if it's too slow for the current hardware.", ex);
        }
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string endpoint)
    {
        if (response.IsSuccessStatusCode) return;

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch { /* ignore */ }

        var details = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
        throw new InvalidOperationException(
            $"Ollama request to '{endpoint}' failed with HTTP {(int)response.StatusCode} ({response.StatusCode}). " +
            $"Details: {details}");
    }

    private static CancellationTokenSource CreateTimeoutCts(int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (timeoutSeconds <= 0)
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }
}
