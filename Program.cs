using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Paperless.Modules.Ollama;

internal class Program
{
    static async Task Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json",
                optional: false,
                reloadOnChange: false)
            .Build();

        var options = configuration.GetSection("Ollama").Get<OllamaOptions>();
        if (options is null)
        {
            Console.WriteLine("Section 'Ollama' not found in appsettings.json!");
            return;
        }

        var ollama = new OllamaClient(options);

        if (!await ollama.HealthCheckAsync())
        {
            Console.WriteLine("Ollama não está rodando! Execute 'ollama serve'.");
            return;
        }

        string skill = configuration["Skill"] ?? "Você é um assistente local.";

        try
        {
            var resposta = await ollama.ChatAsync(
                [
                    ChatMessage.System(skill),
                    ChatMessage.User(Console.ReadLine()!),
                ],
                CancellationToken.None);

            Console.WriteLine(resposta);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Timeout falando com o modelo: {ex.Message}");
        }

        float[] vetor = await ollama.EmbedAsync("texto para vetorizar");
    }
}