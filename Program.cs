using System;
using System.Threading.Tasks;
using Paperless.Modules.Ollama;

internal class Program
{
    static async Task Main(string[] args)
    {
        
        // 1. Carregar options do appsettings.json (ou direto)
        var options = new OllamaOptions
        {
            BaseUrl = "http://localhost:11434",
            Model = "phi3:mini",
            EmbeddingModel = "nomic-embed-text",
            TimeoutSeconds = 120,
        };

        // 2. Criar o cliente
        var ollama = new OllamaClient(options);

        // 3. Health check
        if (!await ollama.HealthCheckAsync())
        {
            Console.WriteLine("Ollama não está rodando! Execute 'ollama serve'.");
            return;
        }

        // 4. Chat
        var resposta = await ollama.ChatAsync([
            ChatMessage.System("Você é um assistente local."),
            ChatMessage.User("Olá, tudo bem?"),
        ]);

        // 5. Embedding (para o RAG depois)
        float[] vetor = await ollama.EmbedAsync("texto para vetorizar");

    }
}