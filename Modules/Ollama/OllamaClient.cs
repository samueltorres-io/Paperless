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

    public OllamaClient(string? baseUrl = null)
    {

        baseUrl = baseUrl ?? _options.BaseUrl;

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/");
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
