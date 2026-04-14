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
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };
    }

    /* Ollama Helth Check */
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var response = await _http.GetAsync("");
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
    public async Task<string> ChatAsync(IEnumerable<ChatMessage> messages)
    {
        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages.ToList(),
            Stream = false,
        };

        using var httpResponse = await PostJsonAsync("api/chat", request);

        httpResponse.EnsureSuccessStatusCode();

        var result = await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponse>(_jsonOptions);

        if (result is null || string.IsNullOrWhiteSpace(result.Message.Content))
            throw new InvalidOperationException("Ollama return the empty response!");

        return result.Message.Content.Trim();
    }

    /* Embed -> Gera vetor de embedding para um texto */
    public async Task<float[]> EmbedAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Embedding text cannot be empty!", nameof(text));

        var request = new OllamaEmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Prompt = text,
        };

        using var httpResponse = await PostJsonAsync("api/embeddings", request);

        httpResponse.EnsureSuccessStatusCode();

        var result = await httpResponse.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(_jsonOptions);

        if (result is null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama return the empty embedding!");

        return result.Embedding;
    }

    /* Helpers */
    private async Task<HttpResponseMessage> PostJsonAsync<T>(string endpoint, T payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            return await _http.PostAsync(endpoint, content);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not connect to Ollama at {_options.BaseUrl}. " +
                "Check that the service is running ('ollama serve').", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(
                $"Ollama did not respond within {_options.TimeoutSeconds}s. " +
                "The model may be loading for the first time.", ex);
        }
    }
}
