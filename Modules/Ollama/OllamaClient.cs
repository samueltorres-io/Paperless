using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

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

    // `POST http://localhost:11434/api/chat` — gerar respostas
    public Task<string> ChatAsync(IEnumerable<ChatMessage> messages);

    // `POST http://localhost:11434/api/embeddings` — gerar vetores
    public Task<float[]> EmbedAsync(string text);
}
