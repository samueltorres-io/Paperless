using Paperless.Modules.Chat;
using Paperless.Modules.Ollama;
using Paperless.Modules.Session;
using Paperless.Modules.Vector.Entity;
using Paperless.Modules.Vector.Model;
using Xunit;

namespace Paperless.Tests.Chat;

public class ChatServiceTests
{
    // ═══════════════════════ Fakes ═══════════════════════

    /// <summary>
    /// Fake do OllamaClient: controla o que EmbedAsync e ChatAsync retornam.
    /// Registra chamadas para asserção nos testes.
    /// </summary>
    private sealed class FakeOllama : IOllamaClient
    {
        public float[] EmbedResult { get; set; } = [0.1f, 0.2f, 0.3f];
        public string ChatResult { get; set; } = "Resposta padrão do modelo.";
        public bool ThrowOnChat { get; set; } = false;
        public bool ThrowOnEmbed { get; set; } = false;

        public List<string> ChatInputs { get; } = [];
        public int EmbedCallCount { get; private set; }
        public int ChatCallCount { get; private set; }

        // Índice da chamada de chat que deve lançar (null = todas)
        public int? ThrowOnChatCall { get; set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            EmbedCallCount++;

            if (ThrowOnEmbed)
                throw new HttpRequestException("Ollama unreachable");

            return Task.FromResult(EmbedResult);
        }

        public Task<string> ChatAsync(IList<ChatMessage> messages, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ChatCallCount++;
            ChatInputs.Add(messages.Last().Content);

            if (ThrowOnChat && (ThrowOnChatCall is null || ThrowOnChatCall == ChatCallCount))
                throw new HttpRequestException("Ollama unreachable");

            return Task.FromResult(ChatResult);
        }
    }

    /// <summary>
    /// Fake do FileRagModel: retorna resultados configuráveis.
    /// </summary>
    private sealed class FakeRagModel : IFileRagModel
    {
        public List<(DocumentChunk Chunk, double Score)> Results { get; set; } = [];

        public List<(DocumentChunk Chunk, double Score)> SearchSimilar(
            float[] embedding, int topK, double minScore) => Results;
    }

    /// <summary>
    /// Fake do SessionManager: registra chamadas e controla estado.
    /// </summary>
    private sealed class FakeSession : ISessionManager
    {
        public bool IsExpired { get; set; } = false;
        public bool HasContext { get; set; } = false;
        public string Summary { get; set; } = string.Empty;

        public int ResetCount { get; private set; }
        public int TouchCount { get; private set; }
        public List<string> SummaryUpdates { get; } = [];

        public void Reset()   { ResetCount++; IsExpired = false; HasContext = false; Summary = string.Empty; }
        public void Touch()   { TouchCount++; }
        public void UpdateSummary(string summary)
        {
            SummaryUpdates.Add(summary);
            Summary = summary;
            HasContext = !string.IsNullOrWhiteSpace(summary);
        }
    }

    // ═══════════════════════ Factory ═══════════════════════

    private static (ChatService service, FakeOllama ollama, FakeRagModel rag, FakeSession session)
        Build(string systemPrompt = "Você é um assistente.")
    {
        var ollama  = new FakeOllama();
        var rag     = new FakeRagModel();
        var session = new FakeSession();
        var service = new ChatService(ollama, rag, session, systemPrompt);
        return (service, ollama, rag, session);
    }

    // ═══════════════════════ Construtor ═══════════════════════

    [Fact]
    public void Constructor_NullOllama_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatService(null!, new FakeRagModel(), new FakeSession(), "prompt"));
    }

    [Fact]
    public void Constructor_NullRagModel_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatService(new FakeOllama(), null!, new FakeSession(), "prompt"));
    }

    [Fact]
    public void Constructor_NullSession_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatService(new FakeOllama(), new FakeRagModel(), null!, "prompt"));
    }

    [Fact]
    public void Constructor_NullSystemPrompt_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatService(new FakeOllama(), new FakeRagModel(), new FakeSession(), null!));
    }

    // ═══════════════════════ Validação de entrada ═══════════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task AskAsync_InvalidQuestion_ShouldThrowArgumentException(string? question)
    {
        var (service, _, _, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AskAsync(question!, CancellationToken.None));
    }

    [Fact]
    public async Task AskAsync_InvalidQuestion_ShouldNotCallOllama()
    {
        var (service, ollama, _, _) = Build();

        try { await service.AskAsync("", CancellationToken.None); } catch { }

        Assert.Equal(0, ollama.EmbedCallCount);
        Assert.Equal(0, ollama.ChatCallCount);
    }

    // ═══════════════════════ Fluxo principal ═══════════════════════

    [Fact]
    public async Task AskAsync_ValidQuestion_ShouldReturnModelAnswer()
    {
        var (service, ollama, _, _) = Build();
        ollama.ChatResult = "A resposta do modelo.";

        var result = await service.AskAsync("Qual é a capital do Brasil?");

        Assert.Equal("A resposta do modelo.", result);
    }

    [Fact]
    public async Task AskAsync_ShouldCallEmbedOnce()
    {
        var (service, ollama, _, _) = Build();

        await service.AskAsync("pergunta");

        Assert.Equal(1, ollama.EmbedCallCount);
    }

    [Fact]
    public async Task AskAsync_ShouldCallChatTwice_MainAndSummary()
    {
        // ChatAsync é chamado: 1x para resposta principal + 1x para resumo da sessão
        var (service, ollama, _, _) = Build();

        await service.AskAsync("pergunta");

        Assert.Equal(2, ollama.ChatCallCount);
    }

    // ═══════════════════════ Sessão — expiração ═══════════════════════

    [Fact]
    public async Task AskAsync_SessionExpired_ShouldCallReset()
    {
        var (service, _, _, session) = Build();
        session.IsExpired = true;

        await service.AskAsync("pergunta");

        Assert.Equal(1, session.ResetCount);
    }

    [Fact]
    public async Task AskAsync_SessionExpired_ShouldNotCallTouch()
    {
        // Quando a sessão expira, Reset() já renova o TTL; Touch() não deve ser chamado
        var (service, _, _, session) = Build();
        session.IsExpired = true;

        await service.AskAsync("pergunta");

        Assert.Equal(0, session.TouchCount);
    }

    [Fact]
    public async Task AskAsync_SessionActive_ShouldCallTouch()
    {
        var (service, _, _, session) = Build();
        session.IsExpired = false;

        await service.AskAsync("pergunta");

        Assert.Equal(1, session.TouchCount);
    }

    [Fact]
    public async Task AskAsync_SessionActive_ShouldNotCallReset()
    {
        var (service, _, _, session) = Build();
        session.IsExpired = false;

        await service.AskAsync("pergunta");

        Assert.Equal(0, session.ResetCount);
    }

    // ═══════════════════════ Sessão — resumo ═══════════════════════

    [Fact]
    public async Task AskAsync_ShouldUpdateSessionSummaryAfterAnswer()
    {
        var (service, ollama, _, session) = Build();
        ollama.ChatResult = "resumo gerado";

        await service.AskAsync("pergunta");

        Assert.Single(session.SummaryUpdates);
        Assert.Equal("resumo gerado", session.SummaryUpdates[0]);
    }

    [Fact]
    public async Task AskAsync_SessionHasContext_ShouldIncludePreviousSummaryInSummarizePrompt()
    {
        var (service, ollama, _, session) = Build();
        session.HasContext = true;
        session.Summary = "Usuário perguntou sobre faturas anteriormente.";

        await service.AskAsync("nova pergunta");

        // A segunda chamada ao Chat (resumo) deve incluir o contexto anterior
        var summaryPrompt = ollama.ChatInputs[1]; // índice 1 = chamada de resumo
        Assert.Contains("Usuário perguntou sobre faturas anteriormente.", summaryPrompt);
    }

    // ═══════════════════════ Tolerância a falha no resumo ═══════════════════════

    [Fact]
    public async Task AskAsync_SummaryUpdateFails_ShouldStillReturnAnswer()
    {
        var (service, ollama, _, _) = Build();
        ollama.ChatResult = "resposta principal";

        // Segunda chamada ao Chat (resumo) vai lançar
        ollama.ThrowOnChatCall = 2;
        ollama.ThrowOnChat = true;

        var result = await service.AskAsync("pergunta");

        Assert.Equal("resposta principal", result);
    }

    [Fact]
    public async Task AskAsync_SummaryUpdateFails_ShouldNotUpdateSession()
    {
        var (service, ollama, _, session) = Build();
        ollama.ThrowOnChatCall = 2;
        ollama.ThrowOnChat = true;

        await service.AskAsync("pergunta");

        Assert.Empty(session.SummaryUpdates);
    }

    // ═══════════════════════ Cancelamento ═══════════════════════

    [Fact]
    public async Task AskAsync_CancelledToken_ShouldThrowOperationCanceledException()
    {
        var (service, _, _, _) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AskAsync("pergunta", cts.Token));
    }

    [Fact]
    public async Task AskAsync_CancelledDuringSummary_ShouldPropagateAndNotSwallow()
    {
        // CancellationToken cancelado durante a etapa de resumo deve propagar,
        // não ser silenciado pelo catch genérico.
        var (service, ollama, _, _) = Build();
        using var cts = new CancellationTokenSource();

        int callCount = 0;
        ollama.ChatResult = "resposta";

        // Cancela na segunda chamada (resumo)
        // Sobrescrevemos com uma versão que cancela no segundo ChatAsync
        var cancellingOllama = new CancelOnSecondCallOllama(cts);
        var svc = new ChatService(cancellingOllama, new FakeRagModel(), new FakeSession(), "prompt");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.AskAsync("pergunta", cts.Token));
    }

    // ═══════════════════════ RAG — contexto no prompt ═══════════════════════

    [Fact]
    public async Task AskAsync_WithRagResults_ShouldIncludeChunkContentInPrompt()
    {
        var (service, ollama, rag, _) = Build();

        rag.Results =
        [
            (new DocumentChunk
             {
                 FilePath = "docs/manual.txt",
                 ChunkIndex = 0,
                 Content = "O prazo de entrega é de 5 dias úteis."
             }, 0.9)
        ];

        await service.AskAsync("Qual é o prazo?");

        var mainPrompt = ollama.ChatInputs[0];
        Assert.Contains("O prazo de entrega é de 5 dias úteis.", mainPrompt);
        Assert.Contains("docs/manual.txt", mainPrompt);
    }

    [Fact]
    public async Task AskAsync_WithNoRagResults_ShouldNotIncludeRagSection()
    {
        var (service, ollama, rag, _) = Build();
        rag.Results = [];

        await service.AskAsync("pergunta qualquer");

        var mainPrompt = ollama.ChatInputs[0];
        Assert.DoesNotContain("[Relevant context from your files]", mainPrompt);
    }

    [Fact]
    public async Task AskAsync_WithSessionSummary_ShouldIncludeSummaryInPrompt()
    {
        var (service, ollama, _, session) = Build();
        session.HasContext = true;
        session.Summary = "Contexto da conversa anterior.";

        await service.AskAsync("continuando...");

        var mainPrompt = ollama.ChatInputs[0];
        Assert.Contains("Contexto da conversa anterior.", mainPrompt);
        Assert.Contains("[Previous conversation context]", mainPrompt);
    }

    [Fact]
    public async Task AskAsync_WithNoSessionContext_ShouldNotIncludeContextSection()
    {
        var (service, ollama, _, session) = Build();
        session.HasContext = false;
        session.Summary = string.Empty;

        await service.AskAsync("primeira pergunta");

        var mainPrompt = ollama.ChatInputs[0];
        Assert.DoesNotContain("[Previous conversation context]", mainPrompt);
    }

    // ═══════════════════════ Helper — fake para cancelamento ═══════════════════════

    private sealed class CancelOnSecondCallOllama(CancellationTokenSource cts) : IOllamaClient
    {
        private int _chatCalls = 0;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 0.1f });

        public Task<string> ChatAsync(IList<ChatMessage> messages, CancellationToken ct = default)
        {
            _chatCalls++;
            if (_chatCalls == 2) cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("resposta");
        }
    }
}