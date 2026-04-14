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
                reloadOnChange: true)
            .Build();
        
        
        var options = configuration.GetSection("Ollama").Get<OllamaOptions>();
        if (options is null)
        {
            Console.WriteLine("Section 'Ollama' not found into appsettings.json file!");
            return;
        }

        // 2. Criar o cliente
        var ollama = new OllamaClient(options);

        // 3. Health check
        if (!await ollama.HealthCheckAsync())
        {
            Console.WriteLine("Ollama não está rodando! Execute 'ollama serve'.");
            return;
        }

        // 4. Chat
        try
        {
            var resposta = await ollama.ChatAsync(
                [
                    ChatMessage.System("Você é um assistente local."),
                    ChatMessage.User(Console.ReadLine()),
                ],
                CancellationToken.None);

            Console.WriteLine(resposta);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Timeout falando com o modelo: {ex.Message}");
        }

        // 5. Embedding (para o RAG depois)
        float[] vetor = await ollama.EmbedAsync("texto para vetorizar");

    }
}