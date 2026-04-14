using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
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

    // `POST http://localhost:11434/api/embeddings` — gerar vetores
    public Task<float[]> EmbedAsync(string text);

    /* Helpers */
    private Task<HttpResponseMessage> PostJsonAsync<T>(string relativePath, T payload)
    {
        return _http.PostAsJsonAsync(relativePath, payload, _jsonOptions);
    }
}
