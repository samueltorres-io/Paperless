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
    private readonly OllamaClient _ollama;
    private readonly FileRagModel _ragModel;
    private readonly SessionManager _session;
    private readonly string _systemPrompt;

    /* Configurações de busca RAG */
    private const int RagTopK = 3;
    private const double RagMinScore = 0.3;

    public ChatService(
        OllamaClient ollama,
        FileRagModel ragModel,
        SessionManager session,
        string systemPrompt)
    {
        _ollama = ollama;
        _ragModel = ragModel;
        _session = session;
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Processa a pergunta do usuário e retorna a resposta do modelo.
    /// </summary>
    public async Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        /* 1 — Sessão expirou? Limpa contexto */
        if (_session.IsExpired)
            _session.Reset();

        _session.Touch();

        /* 2 — Gerar embedding da pergunta */
        var queryEmbedding = await _ollama.EmbedAsync(question, ct);

        /* 3 — Buscar chunks similares (RAG) */
        var ragResults = _ragModel.SearchSimilar(queryEmbedding, RagTopK, RagMinScore);

        /* 4 — Montar prompt */
        var userPrompt = BuildUserPrompt(question, ragResults, _session.Summary);

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(_systemPrompt),
            ChatMessage.User(userPrompt),
        };

        /* 5 — Obter resposta do modelo */
        var answer = await _ollama.ChatAsync(messages, ct);

        /* 6 — Atualizar resumo da sessão (fire-and-forget tolerante a falha) */
        try
        {
            await UpdateSessionSummaryAsync(question, answer, ct);
        }
        catch
        {
            /* Se falhar, a sessão continua com o resumo anterior. */
        }

        return answer;
    }

    /// <summary>
    /// Monta o prompt do usuário combinando RAG + sessão + pergunta.
    /// </summary>
    private static string BuildUserPrompt(
        string question,
        List<(Vector.Entity.DocumentChunk Chunk, double Score)> ragResults,
        string sessionSummary)
    {
        var sb = new StringBuilder();

        /* Contexto RAG */
        if (ragResults.Count > 0)
        {
            sb.AppendLine("[Relevant context from your files]");

            foreach (var (chunk, score) in ragResults)
            {
                sb.AppendLine($"--- {chunk.FilePath} (chunk {chunk.ChunkIndex}) ---");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }
        }

        /* Contexto de sessão */
        if (!string.IsNullOrWhiteSpace(sessionSummary))
        {
            sb.AppendLine("[Previous conversation context]");
            sb.AppendLine(sessionSummary);
            sb.AppendLine();
        }

        /* Pergunta do usuário */
        sb.AppendLine("[User question]");
        sb.AppendLine(question);

        return sb.ToString();
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
        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}
