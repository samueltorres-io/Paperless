using System.Text;
using Paperless.Modules.Ollama;
using Paperless.Modules.Session;
using Paperless.Modules.Vector.Model;

namespace Paperless.Modules.Chat;

/// <summary>
/// Orquestra o fluxo completo de uma pergunta:
///   1. Verifica sessão (expira → limpa contexto)
///   2. Gera embedding da pergunta
///   3. Busca top-K chunks similares (RAG)
///   4. Monta prompt: system + contexto RAG + resumo sessão + pergunta
///   5. Envia para o Ollama e obtém resposta
///   6. Atualiza o resumo da sessão (chamada extra ao LLM)
/// </summary>
public sealed class ChatService
{
    private readonly IOllamaClient _ollama;
    private readonly IFileRagModel _ragModel;
    private readonly ISessionManager _session;
    private readonly string _systemPrompt;

    /* Configurações de busca RAG */
    private const int RagTopK = 3;
    private const double RagMinScore = 0.3;

    public ChatService(
        IOllamaClient ollama,
        IFileRagModel ragModel,
        ISessionManager session,
        string systemPrompt)
    {
        _ollama       = ollama       ?? throw new ArgumentNullException(nameof(ollama));
        _ragModel     = ragModel     ?? throw new ArgumentNullException(nameof(ragModel));
        _session      = session      ?? throw new ArgumentNullException(nameof(session));
        _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
    }

    /// <summary>
    /// Processa a pergunta do usuário e retorna a resposta do modelo.
    /// </summary>
    public async Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("A pergunta não pode ser vazia.", nameof(question));

        /* 1 — Sessão expirou? Limpa contexto */
        if (_session.IsExpired)
            _session.Reset();
        else
            _session.Touch();

        /* 2 — Gerar embedding da pergunta */
        var queryEmbedding = await _ollama.EmbedAsync(question, ct);

        /* 3 — Buscar chunks similares (RAG) */
        var ragResults = _ragModel.SearchSimilar(queryEmbedding, RagTopK, RagMinScore);

        /* 4 — Montar prompt (inclui substituição do {context} do system prompt) */
        var contextBlock = BuildContextBlock(ragResults, _session.Summary);
        var systemPrompt = _systemPrompt;

        if (systemPrompt.Contains("{context}", StringComparison.Ordinal))
            systemPrompt = systemPrompt.Replace("{context}", contextBlock);

        var userPrompt = BuildUserPrompt(question, contextBlock);

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(systemPrompt),
            ChatMessage.User(userPrompt),
        };

        /* 5 — Obter resposta do modelo */
        var answer = await _ollama.ChatAsync(messages, ct);

        /* 6 — Atualizar resumo da sessão (tolerante a falha, mas não a cancelamento) */
        try
        {
            await UpdateSessionSummaryAsync(question, answer, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            /* Falha no resumo não impede retornar a resposta ao usuário. */
        }

        return answer;
    }

    /// <summary>
    /// Monta o prompt do usuário combinando RAG + sessão + pergunta.
    /// </summary>
    private static string BuildUserPrompt(string question, string contextBlock)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(contextBlock))
        {
            sb.AppendLine(contextBlock);
            sb.AppendLine();
        }

        sb.AppendLine("[User question]");
        sb.AppendLine(question);

        return sb.ToString();
    }

    private static string BuildContextBlock(
        List<(Vector.Entity.DocumentChunk Chunk, double Score)> ragResults,
        string sessionSummary)
    {
        var sb = new StringBuilder();

        if (ragResults.Count > 0)
        {
            sb.AppendLine("[Relevant context from your files]");

            foreach (var (chunk, _) in ragResults)
            {
                sb.AppendLine($"--- {chunk.FilePath} (chunk {chunk.ChunkIndex}) ---");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionSummary))
        {
            sb.AppendLine("[Previous conversation context]");
            sb.AppendLine(sessionSummary);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Pede ao modelo para gerar um resumo atualizado da conversa.
    /// O resumo anterior + última troca → resumo compacto.
    /// </summary>
    private async Task UpdateSessionSummaryAsync(
        string question, string answer, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the conversation so far in 2-3 concise sentences.");
        sb.AppendLine("Keep only the essential context needed for follow-up questions.");
        sb.AppendLine("Respond ONLY with the summary, nothing else.");

        if (_session.HasContext)
        {
            sb.AppendLine();
            sb.AppendLine($"Previous summary: {_session.Summary}");
        }

        sb.AppendLine();
        sb.AppendLine($"User: {question}");
        sb.AppendLine($"Assistant: {Truncate(answer, 300)}");

        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a summarizer. Output only the summary."),
            ChatMessage.User(sb.ToString()),
        };

        var summary = await _ollama.ChatAsync(messages, ct);
        _session.UpdateSummary(summary);
    }

    /// <summary>
    /// Trunca texto para não estourar o contexto do resumo.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
