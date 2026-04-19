using Spectre.Console;

namespace Paperless.Modules.Setup;

/// <summary>
/// Orquestra a sequência de verificações pré-execução com UI do Spectre.Console:
///   1. Ollama instalado?          se não → oferece instalar
///   2. Servidor rodando?          se não → instrui o usuário
///   3. Modelos necessários?       se não → pull com barra de progresso
///
/// Retorna true se o ambiente ficou 100% pronto, false caso contrário.
/// </summary>
public sealed class SetupWizard
{
    private const string Primary = "#5B8DEF";
    private const string Accent  = "#06D6A0";
    private const string Warn    = "#FFD166";
    private const string Danger  = "#EF476F";
    private const string Muted   = "grey62";

    private readonly OllamaBootstrap _bootstrap;
    private readonly string _chatModel;
    private readonly string _embeddingModel;

    public SetupWizard(OllamaBootstrap bootstrap, string chatModel, string embeddingModel)
    {
        _bootstrap = bootstrap;
        _chatModel = chatModel;
        _embeddingModel = embeddingModel;
    }

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        AnsiConsole.Write(
            new Rule($"[{Primary} bold]Setup[/]")
                .LeftJustified()
                .RuleStyle(Muted));
        AnsiConsole.WriteLine();

        if (!await EnsureBinaryAsync(ct))        return false;
        if (!await EnsureServerAsync(ct))        return false;
        if (!await EnsureModelsAsync(ct))        return false;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Ambiente pronto.");
        AnsiConsole.Write(new Rule().RuleStyle(Muted));
        return true;
    }

    // ────────────────────────── Etapa 1: Binário ──────────────────────────

    private async Task<bool> EnsureBinaryAsync(CancellationToken ct)
    {
        bool installed = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle($"{Primary} bold")
            .StartAsync("Verificando instalação do Ollama...", async _ =>
            {
                installed = await _bootstrap.IsBinaryInstalledAsync();
            });

        if (installed)
        {
            AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Ollama instalado");
            return true;
        }

        AnsiConsole.MarkupLine($"  [{Warn}]⚠[/] Ollama não encontrado no sistema.");

        var confirm = AnsiConsole.Prompt(
            new ConfirmationPrompt($"  Deseja instalar automaticamente?")
            {
                DefaultValue = true,
            });

        if (!confirm)
        {
            AnsiConsole.MarkupLine(
                $"  [{Muted}]Instale manualmente em [bold]https://ollama.com[/] e rode o Paperless de novo.[/]");
            return false;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{Muted}]Executando o instalador oficial (pode solicitar sua senha)...[/]");
        AnsiConsole.WriteLine();

        var ok = await _bootstrap.InstallAsync(ct);
        AnsiConsole.WriteLine();

        if (!ok)
        {
            AnsiConsole.MarkupLine($"  [{Danger}]✗[/] A instalação do Ollama falhou. Veja os logs acima.");
            return false;
        }

        AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Ollama instalado com sucesso");
        return true;
    }

    // ────────────────────────── Etapa 2: Servidor ──────────────────────────

    private async Task<bool> EnsureServerAsync(CancellationToken ct)
    {
        bool running = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle($"{Primary} bold")
            .StartAsync("Verificando servidor Ollama...", async _ =>
            {
                // Linux/macOS com instalador oficial já sobe um systemd service.
                // macOS app e Windows sobem o servidor junto com o instalador.
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (await _bootstrap.IsServerRunningAsync(ct))
                    {
                        running = true;
                        return;
                    }
                    await Task.Delay(1000, ct);
                }
            });

        if (running)
        {
            AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Servidor Ollama rodando");
            return true;
        }

        AnsiConsole.MarkupLine($"  [{Danger}]✗[/] Servidor Ollama não está respondendo.");
        AnsiConsole.MarkupLine(
            $"  [{Muted}]Abra outro terminal, execute [bold]ollama serve[/] e rode o Paperless de novo.[/]");
        return false;
    }

    // ────────────────────────── Etapa 3: Modelos ──────────────────────────

    private async Task<bool> EnsureModelsAsync(CancellationToken ct)
    {
        foreach (var model in new[] { _chatModel, _embeddingModel })
        {
            bool has = false;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle($"{Primary} bold")
                .StartAsync($"Verificando modelo {model}...", async _ =>
                {
                    has = await _bootstrap.IsModelInstalledAsync(model, ct);
                });

            if (has)
            {
                AnsiConsole.MarkupLine(
                    $"  [{Accent}]✓[/] Modelo [bold]{Markup.Escape(model)}[/] disponível");
                continue;
            }

            AnsiConsole.MarkupLine(
                $"  [{Warn}]⚠[/] Modelo [bold]{Markup.Escape(model)}[/] não encontrado.");

            var confirm = AnsiConsole.Prompt(
                new ConfirmationPrompt($"  Baixar agora?") { DefaultValue = true });

            if (!confirm)
            {
                AnsiConsole.MarkupLine(
                    $"  [{Muted}]Baixe depois com: [bold]ollama pull {Markup.Escape(model)}[/][/]");
                return false;
            }

            var ok = await PullWithProgressAsync(model, ct);
            if (!ok)
            {
                AnsiConsole.MarkupLine(
                    $"  [{Danger}]✗[/] Falha ao baixar [bold]{Markup.Escape(model)}[/]");
                return false;
            }

            AnsiConsole.MarkupLine(
                $"  [{Accent}]✓[/] Modelo [bold]{Markup.Escape(model)}[/] pronto");
        }

        return true;
    }

    /// <summary>
    /// Chama /api/pull e renderiza a barra de progresso nativa do Spectre,
    /// com tamanho baixado, velocidade e ETA. O total pode mudar ao longo do
    /// pull (cada "layer" tem seu próprio total), então atualizamos dinamicamente.
    /// </summary>
    private async Task<bool> PullWithProgressAsync(string model, CancellationToken ct)
    {
        bool success = false;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadedColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
            })
            .StartAsync(async progressCtx =>
            {
                var task = progressCtx.AddTask(
                    $"[bold]{Markup.Escape(model)}[/]",
                    maxValue: 1);

                string lastStatus = "";
                long   lastTotal  = 0;

                var handler = new Progress<PullProgress>(p =>
                {
                    if (p.Total > 0)
                    {
                        if (p.Total != lastTotal)
                        {
                            task.MaxValue = p.Total;
                            lastTotal = p.Total;
                        }
                        task.Value = p.Completed;
                    }

                    if (p.Status != lastStatus)
                    {
                        task.Description =
                            $"[bold]{Markup.Escape(model)}[/] " +
                            $"[{Muted}]· {Markup.Escape(p.Status)}[/]";
                        lastStatus = p.Status;
                    }
                });

                success = await _bootstrap.PullModelAsync(model, handler, ct);

                if (success && task.MaxValue > 0)
                    task.Value = task.MaxValue;
            });

        return success;
    }
}