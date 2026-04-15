namespace Paperless.Modules.Session;

/// <summary>
/// Gerencia a sessão de conversa em memória com TTL.
///
/// Armazena um resumo compacto da conversa (não o histórico completo)
/// para manter o contexto entre interações sem estourar a janela do modelo.
/// Expira após o TTL configurado sem interação, limpando o contexto.
///
/// Thread-safe: todas as leituras e escritas são protegidas por lock.
/// </summary>
public sealed class SessionManager
{
    private readonly int _ttlMinutes;
    private readonly object _lock = new();

    private DateTime _lastInteraction;
    private string _summary;

    public SessionManager(int ttlMinutes = 10)
    {
        if (ttlMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(ttlMinutes),
                "TTL must be greater than zero!");

        _ttlMinutes = ttlMinutes;
        _summary = string.Empty;
        _lastInteraction = DateTime.UtcNow;
    }

    /// <summary>
    /// Indica se a sessão expirou por falta de interação.
    /// </summary>
    public bool IsExpired
    {
        get
        {
            lock (_lock)
                return (DateTime.UtcNow - _lastInteraction).TotalMinutes > _ttlMinutes;
        }
    }

    /// <summary>
    /// Indica se há contexto de conversa armazenado.
    /// </summary>
    public bool HasContext
    {
        get
        {
            lock (_lock)
                return (DateTime.UtcNow - _lastInteraction).TotalMinutes <= _ttlMinutes
                    && !string.IsNullOrWhiteSpace(_summary);
        }
    }

    /// <summary>
    /// Retorna o resumo atual. String vazia se expirou ou não há contexto.
    /// </summary>
    public string Summary
    {
        get
        {
            lock (_lock)
                return (DateTime.UtcNow - _lastInteraction).TotalMinutes > _ttlMinutes
                    ? string.Empty
                    : _summary;
        }
    }

    /// <summary>
    /// Atualiza o resumo da sessão e renova o TTL.
    /// </summary>
    public void UpdateSummary(string summary)
    {
        lock (_lock)
        {
            _summary = summary ?? string.Empty;
            _lastInteraction = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Renova o TTL sem alterar o resumo.
    /// </summary>
    public void Touch()
    {
        lock (_lock)
            _lastInteraction = DateTime.UtcNow;
    }

    /// <summary>
    /// Limpa o contexto e reinicia a sessão.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _summary = string.Empty;
            _lastInteraction = DateTime.UtcNow;
        }
    }
}
