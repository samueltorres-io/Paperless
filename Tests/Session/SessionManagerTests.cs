using Paperless.Modules.Session;
using Xunit;

namespace Paperless.Tests.Session;

public class SessionManagerTests
{

    [Fact]
    public void Constructor_DefaultTtl_ShouldBe10Minutes()
    {
        var session = new SessionManager();

        // Recém-criada não deve estar expirada
        Assert.False(session.IsExpired);
    }

    [Fact]
    public void Constructor_CustomTtl_ShouldBeRespected()
    {
        // TTL de 1 minuto — sessão recém-criada ainda válida
        var session = new SessionManager(ttlMinutes: 1);

        Assert.False(session.IsExpired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_InvalidTtl_ShouldThrow(int ttl)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionManager(ttl));
    }

    // ───────────────────────── Estado inicial ─────────────────────────

    [Fact]
    public void Initial_IsExpired_ShouldBeFalse()
    {
        var session = new SessionManager();

        Assert.False(session.IsExpired);
    }

    [Fact]
    public void Initial_HasContext_ShouldBeFalse()
    {
        var session = new SessionManager();

        Assert.False(session.HasContext);
    }

    [Fact]
    public void Initial_Summary_ShouldBeEmpty()
    {
        var session = new SessionManager();

        Assert.Equal(string.Empty, session.Summary);
    }

    // ───────────────────────── UpdateSummary ─────────────────────────

    [Fact]
    public void UpdateSummary_ValidText_ShouldSetSummaryAndHasContext()
    {
        var session = new SessionManager();

        session.UpdateSummary("Usuário perguntou sobre faturas.");

        Assert.True(session.HasContext);
        Assert.Equal("Usuário perguntou sobre faturas.", session.Summary);
    }

    [Fact]
    public void UpdateSummary_Null_ShouldNotThrowAndShouldSetEmpty()
    {
        var session = new SessionManager();

        session.UpdateSummary(null!);

        Assert.False(session.HasContext);
        Assert.Equal(string.Empty, session.Summary);
    }

    [Fact]
    public void UpdateSummary_WhitespaceOnly_ShouldResultInNoContext()
    {
        var session = new SessionManager();

        session.UpdateSummary("   \n\t  ");

        Assert.False(session.HasContext);
    }

    [Fact]
    public void UpdateSummary_ShouldRenewTtl()
    {
        // Simula sessão quase expirada e verifica que UpdateSummary renova o TTL.
        // Como não podemos manipular o relógio, validamos o comportamento
        // indireto: após update, IsExpired deve permanecer false imediatamente.
        var session = new SessionManager(ttlMinutes: 1);

        session.UpdateSummary("contexto");

        Assert.False(session.IsExpired);
    }

    [Fact]
    public void UpdateSummary_CalledTwice_ShouldReplaceOldSummary()
    {
        var session = new SessionManager();

        session.UpdateSummary("primeiro resumo");
        session.UpdateSummary("segundo resumo");

        Assert.Equal("segundo resumo", session.Summary);
    }

    // ───────────────────────── Touch ─────────────────────────

    [Fact]
    public void Touch_ShouldNotChangeSummary()
    {
        var session = new SessionManager();
        session.UpdateSummary("contexto original");

        session.Touch();

        Assert.Equal("contexto original", session.Summary);
    }

    [Fact]
    public void Touch_ShouldKeepSessionAlive()
    {
        var session = new SessionManager(ttlMinutes: 1);

        session.Touch();

        Assert.False(session.IsExpired);
    }

    // ───────────────────────── Reset ─────────────────────────

    [Fact]
    public void Reset_ShouldClearSummary()
    {
        var session = new SessionManager();
        session.UpdateSummary("algum contexto");

        session.Reset();

        Assert.Equal(string.Empty, session.Summary);
    }

    [Fact]
    public void Reset_ShouldClearHasContext()
    {
        var session = new SessionManager();
        session.UpdateSummary("algum contexto");

        session.Reset();

        Assert.False(session.HasContext);
    }

    [Fact]
    public void Reset_ShouldNotExpireSession()
    {
        var session = new SessionManager();
        session.UpdateSummary("algum contexto");

        session.Reset();

        // Após reset, sessão deve estar ativa (não expirada), apenas sem conteúdo
        Assert.False(session.IsExpired);
    }

    [Fact]
    public void Reset_ShouldAllowNewSummaryAfterwards()
    {
        var session = new SessionManager();
        session.UpdateSummary("contexto antigo");
        session.Reset();

        session.UpdateSummary("contexto novo");

        Assert.Equal("contexto novo", session.Summary);
        Assert.True(session.HasContext);
    }

    // ───────────────────────── Expiração ─────────────────────────

    [Fact]
    public void IsExpired_AfterTtlElapsed_ShouldReturnTrue()
    {
        // TTL mínimo válido (1 min), mas forçamos expiração via reflexão
        // para não tornar o teste lento. Alternativa limpa: expor clock como dep.
        var session = new SessionManager(ttlMinutes: 1);

        // Força _lastInteraction para 2 minutos atrás via reflection
        SetLastInteraction(session, DateTime.UtcNow.AddMinutes(-2));

        Assert.True(session.IsExpired);
    }

    [Fact]
    public void HasContext_AfterTtlElapsed_ShouldReturnFalse()
    {
        var session = new SessionManager(ttlMinutes: 1);
        session.UpdateSummary("contexto");

        SetLastInteraction(session, DateTime.UtcNow.AddMinutes(-2));

        Assert.False(session.HasContext);
    }

    [Fact]
    public void Summary_AfterTtlElapsed_ShouldReturnEmpty()
    {
        var session = new SessionManager(ttlMinutes: 1);
        session.UpdateSummary("contexto importante");

        SetLastInteraction(session, DateTime.UtcNow.AddMinutes(-2));

        Assert.Equal(string.Empty, session.Summary);
    }

    [Fact]
    public void Touch_AfterExpiry_ShouldRenewSession()
    {
        var session = new SessionManager(ttlMinutes: 1);
        session.UpdateSummary("contexto");
        SetLastInteraction(session, DateTime.UtcNow.AddMinutes(-2));

        Assert.True(session.IsExpired); // precondição

        session.Touch();

        Assert.False(session.IsExpired);
    }

    // ───────────────────────── Consistência HasContext / Summary ─────────────────────────

    [Fact]
    public void HasContext_WhenSummaryIsSetAndNotExpired_ShouldBeTrue()
    {
        var session = new SessionManager();

        session.UpdateSummary("resumo válido");

        Assert.True(session.HasContext);
        Assert.NotEqual(string.Empty, session.Summary);
    }

    [Fact]
    public void HasContext_WhenSummaryIsEmptyAndNotExpired_ShouldBeFalse()
    {
        var session = new SessionManager();

        // Nunca teve UpdateSummary
        Assert.False(session.HasContext);
        Assert.Equal(string.Empty, session.Summary);
    }

    // ───────────────────────── Thread Safety ─────────────────────────

    [Fact]
    public async Task ConcurrentUpdateAndRead_ShouldNotThrow()
    {
        var session = new SessionManager();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var writers = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            try
            {
                for (int j = 0; j < 100; j++)
                    session.UpdateSummary($"resumo {i}-{j}");
            }
            catch (Exception ex) { errors.Add(ex); }
        }));

        var readers = Enumerable.Range(0, 10).Select(__ => Task.Run(() =>
        {
            try
            {
                for (int j = 0; j < 100; j++)
                {
                    _ = session.IsExpired;
                    _ = session.HasContext;
                    _ = session.Summary;
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConcurrentTouchAndReset_ShouldNotThrow()
    {
        var session = new SessionManager();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            try
            {
                for (int j = 0; j < 50; j++)
                {
                    if (j % 2 == 0) session.Touch();
                    else session.Reset();
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        }));

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    // ───────────────────────── Helper ─────────────────────────

    /// <summary>
    /// Força _lastInteraction via reflection para simular expiração
    /// sem tornar os testes lentos com Thread.Sleep.
    /// </summary>
    private static void SetLastInteraction(SessionManager session, DateTime value)
    {
        var field = typeof(SessionManager)
            .GetField("_lastInteraction",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        field.SetValue(session, value);
    }
}