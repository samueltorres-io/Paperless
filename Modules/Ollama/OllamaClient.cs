using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Paperless.Modules.Ollama;

public sealed class OllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(string baseUrl = "http://localhost:11434")
    {
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
}
