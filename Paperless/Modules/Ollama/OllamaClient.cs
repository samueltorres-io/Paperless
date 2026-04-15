using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paperless.Configuration;
using Paperless.Modules.Ollama.Dto;

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

    public OllamaClient(HttpClient http, OllamaOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (_http.BaseAddress == null)
        {
            _http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    /* Health Check — verifica se o Ollama está respondendo */
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CreateTimeoutCts(_options.TimeoutSeconds, cancellationToken);
            using var response = await _http.GetAsync("", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /* Chat — Envia mensagem e recebe resposta do modelo (sem streaming) */
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

        using var cts = CreateTimeoutCts(_options.TimeoutSeconds, cancellationToken);
        using var httpResponse = await PostJsonAsync("api/chat", request, cts.Token);

        await EnsureSuccessOrThrowAsync(httpResponse, "api/chat", cts.Token);

        var result = await httpResponse.Content
            .ReadFromJsonAsync<OllamaChatResponse>(_jsonOptions, cts.Token);

        if (string.IsNullOrWhiteSpace(result?.Message?.Content))
            throw new InvalidOperationException("Ollama returned an empty response!");

        return result.Message.Content.Trim();
    }

    /* Embed — Gera vetor de embedding para um texto */
    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Embedding text cannot be empty!", nameof(text));

        var request = new OllamaEmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Prompt = text,
        };

        using var cts = CreateTimeoutCts(_options.TimeoutSeconds, cancellationToken);
        using var httpResponse = await PostJsonAsync("api/embeddings", request, cts.Token);

        await EnsureSuccessOrThrowAsync(httpResponse, "api/embeddings", cts.Token);

        var result = await httpResponse.Content
            .ReadFromJsonAsync<OllamaEmbeddingResponse>(_jsonOptions, cts.Token);

        if (result?.Embedding == null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama returned an empty embedding!");

        return result.Embedding;
    }

    /* Helpers */
    private async Task<HttpResponseMessage> PostJsonAsync<T>(
        string endpoint,
        T payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.PostAsJsonAsync(endpoint, payload, _jsonOptions, cancellationToken);
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
                $"Ollama did not respond within {_options.TimeoutSeconds}s (timeout). " +
                "This may happen while the model is loading.", ex);
        }
    }

    private static async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response, string endpoint, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch { /* ignore */ }

        var details = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase
            : body.Trim();

        throw new InvalidOperationException(
            $"Ollama request to '{endpoint}' failed with HTTP " +
            $"{(int)response.StatusCode} ({response.StatusCode}). " +
            $"Details: {details}");
    }

    private static CancellationTokenSource CreateTimeoutCts(
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutSeconds > 0)
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }
}