using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Paperless.Configuration;
using Paperless.Modules.Chat;
using Paperless.Modules.File;
using Paperless.Modules.Ollama;
using Paperless.Modules.Session;
using Paperless.Modules.ToDo;
using Paperless.Modules.Setup;
using Spectre.Console;

internal class Program
{

    private const string Primary = "#5B8DEF";
    private const string Accent  = "#06D6A0";
    private const string Warn    = "#FFD166";
    private const string Danger  = "#EF476F";
    private const string Muted   = "grey62";

    static async Task<int> Main(string[] args)
    {

        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "--version":
                case "-v":
                    PrintVersion();
                    return 0;

                case "--help":
                case "-h":
                    PrintCliHelp();
                    return 0;
            }
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        /* ══════════════ 1. Configuração ══════════════ */

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var settings = config.Get<AppSettings>() ?? new AppSettings();

        /* ══════════════ 2. Estrutura de pastas ══════════════ */

        EnsureDirectories(settings.Storage);

        /* ══════════════ 3. Banner ══════════════ */

        PrintBanner();

        /* ══════════════ 4. Serviços ══════════════ */

        using var ollamaHttp = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
        };

        var bootstrap = new OllamaBootstrap(ollamaHttp, settings.Ollama.BaseUrl);
        var wizard    = new SetupWizard(
            bootstrap,
            settings.Ollama.Model,
            settings.Ollama.EmbeddingModel);

        if (!await wizard.RunAsync()) return 1;

        /* ══════════════ 5. Health check do Ollama ══════════════ */

        var ollama = new OllamaClient(ollamaHttp, settings.Ollama);
        var ragModel = new FileRagModel(settings.Storage.GetFullDatabasePath());
        var todoRepo = new TodoRepository(settings.Storage.GetFullTasksPath());
        var todoManager = new TodoManager(todoRepo);
        var session = new SessionManager(ttlMinutes: 10);

        /* ══════════════ 6. System prompt ══════════════ */

        var systemPrompt = LoadSystemPrompt(settings);

        /* ══════════════ 7. FileIndexer ══════════════ */

        var userFolder = settings.Storage.GetFullUserFolderPath();
        using var indexer = new FileIndexer(ollama, ragModel, userFolder);

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse($"{Primary} bold"))
            .StartAsync("Indexando arquivos locais...", async _ =>
            {
                await indexer.StartAsync();
            });

        var (files, chunks) = ragModel.GetStats();
        AnsiConsole.MarkupLine(
            $"  [{Accent}]✓[/] Indexação concluída " +
            $"[{Muted}]· {files} arquivo(s) · {chunks} chunk(s)[/]");

        /* ══════════════ 8. ChatService ══════════════ */

        var chat = new ChatService(ollama, ragModel, session, systemPrompt);

        /* ══════════════ 9. Separador + dica ══════════════ */

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle(Style.Parse(Muted)));
        AnsiConsole.MarkupLine(
            $"  [{Muted}]Pronto. Digite [bold]/help[/] para ver os comandos disponíveis.[/]");
        AnsiConsole.Write(new Rule().RuleStyle(Style.Parse(Muted)));

        /* ══════════════ 10. REPL ══════════════ */

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await ReplLoopAsync(chat, todoManager, ragModel, session, cts.Token);

        /* ══════════════ 11. Shutdown ══════════════ */

        indexer.Stop();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{Primary}]Até mais! 👋[/]");
        return 0;
    }

    // ═══════════════════════ REPL ═══════════════════════

    private static async Task ReplLoopAsync(
        ChatService chat,
        TodoManager todo,
        FileRagModel ragModel,
        SessionManager session,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Markup($"  [{Primary} bold]❯[/] ");

            string? input;
            try
            {
                input = Console.ReadLine();
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (input is null)
                break;

            var trimmed = input.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            try
            {
                /* Comandos especiais (começam com /) */
                if (trimmed.StartsWith('/'))
                {
                    var handled = await HandleCommandAsync(trimmed, todo, ragModel, session);

                    if (handled == CommandResult.Exit)
                        break;

                    if (handled == CommandResult.Handled)
                        continue;

                    /* NotHandled → cai para o chat */
                }

                /* Chat com RAG */
                AnsiConsole.WriteLine();
                await AskWithSpinnerAsync(chat, trimmed, ct);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine($"  [{Warn}]⚠[/] Operação cancelada.");
                break;
            }
            catch (TimeoutException ex)
            {
                AnsiConsole.MarkupLine(
                    $"  [{Warn}]⏱ Timeout:[/] {Markup.Escape(ex.Message)}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"  [{Danger}]✗ Erro:[/] {Markup.Escape(ex.Message)}");
            }
        }
    }

    /// <summary>
    /// Executa a pergunta exibindo um status com fases progressivas.
    /// O Spectre.Status já cuida do spinner, da animação e da limpeza da linha —
    /// basta chamar ctx.Status() para avançar de fase.
    /// </summary>
    private static async Task AskWithSpinnerAsync(
        ChatService chat, string question, CancellationToken ct)
    {
        // Cada fase tem um ícone + texto. O Markup.Escape aqui não é necessário
        // porque nós controlamos as strings.
        var phases = new[]
        {
            "Analisando pergunta...",
            "Buscando contexto relevante...",
            "Planejando resposta...",
            "Gerando resposta...",
        };

        string answer = string.Empty;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse($"{Primary} bold"))
            .StartAsync(phases[0], async ctx =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                var phaseTask = Task.Run(async () =>
                {
                    int idx = 0;
                    while (!linkedCts.Token.IsCancellationRequested
                        && idx < phases.Length - 1)
                    {
                        try { await Task.Delay(2500, linkedCts.Token); }
                        catch (OperationCanceledException) { break; }

                        idx++;
                        ctx.Status(phases[idx]);
                        ctx.Refresh();
                    }
                });

                try
                {
                    // Executa a chamada real ao LLM
                    answer = await chat.AskAsync(question, ct);
                }
                finally
                {
                    linkedCts.Cancel();
                    try { await phaseTask; } catch { /* esperado */ }
                }
            });

        PrintAnswer(answer);
    }

    /// <summary>
    /// Exibe a resposta do assistente dentro de um painel com borda arredondada.
    /// O Markup.Escape é essencial: respostas do LLM podem conter [ ] que o
    /// Spectre interpretaria como tags de markup e quebraria a renderização.
    /// </summary>
    private static void PrintAnswer(string answer)
    {
        var panel = new Panel(new Markup(Markup.Escape(answer)))
            .Header($" [{Accent} bold]✓ Resposta[/] ", Justify.Left)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(Muted))
            .Padding(1, 0, 1, 0)
            .Expand();

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    // ═══════════════════════ Comandos ═══════════════════════

    private static Task<CommandResult> HandleCommandAsync(
        string input,
        TodoManager todo,
        FileRagModel ragModel,
        SessionManager session)
    {
        var parts = ParseCommand(input);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/exit" or "/quit" or "/q":
                return Task.FromResult(CommandResult.Exit);

            case "/help":
                PrintHelp();
                return Task.FromResult(CommandResult.Handled);

            case "/status":
                PrintStatus(ragModel, session);
                return Task.FromResult(CommandResult.Handled);

            case "/clear":
                session.Reset();
                AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Sessão limpa.");
                return Task.FromResult(CommandResult.Handled);

            case "/todo":
                HandleTodoCommand(parts, todo);
                return Task.FromResult(CommandResult.Handled);

            /*
            case "/setup":
                var rerun = new SetupWizard(
                    bootstrap,
                    settings.Ollama.Model,
                    settings.Ollama.EmbeddingModel);
                await rerun.RunAsync();
                return CommandResult.Handled;
            */

            default:
                return Task.FromResult(CommandResult.NotHandled);
        }
    }

    // ───────────────────────── /todo ─────────────────────────

    private static void HandleTodoCommand(string[] parts, TodoManager todo)
    {
        if (parts.Length < 2)
        {
            AnsiConsole.MarkupLine(
                $"  [{Muted}]Uso: [bold]/todo <add|list|done|remove>[/] [args][/]");
            AnsiConsole.MarkupLine($"  [{Muted}]Digite [bold]/help[/] para detalhes.[/]");
            return;
        }

        var action = parts[1].ToLowerInvariant();

        switch (action)
        {
            case "add":
                HandleTodoAdd(parts, todo);
                break;
            case "list" or "ls":
                HandleTodoList(parts, todo);
                break;
            case "done":
                HandleTodoDone(parts, todo);
                break;
            case "remove" or "rm" or "delete":
                HandleTodoRemove(parts, todo);
                break;
            default:
                AnsiConsole.MarkupLine(
                    $"  [{Danger}]✗[/] Ação desconhecida: [bold]{Markup.Escape(action)}[/]");
                AnsiConsole.MarkupLine($"  [{Muted}]Ações: add, list, done, remove[/]");
                break;
        }
    }

    private static void HandleTodoAdd(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            AnsiConsole.MarkupLine(
                $"  [{Muted}]Uso: [bold]/todo add \"título\" [\"descrição\"] [1-5][/][/]");
            return;
        }

        string title = parts[2];
        string? description = null;
        int? priority = null;

        for (int i = 3; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int p) && p >= 1 && p <= 5)
                priority = p;
            else
                description = parts[i];
        }

        var task = todo.CreateTask(title, description, priority);
        AnsiConsole.MarkupLine($"  [{Accent}]✓[/] Tarefa criada: {Markup.Escape(task.ToString() ?? "")}");
    }

    private static void HandleTodoList(string[] parts, TodoManager todo)
    {
        int? filter = null;

        if (parts.Length >= 3 && int.TryParse(parts[2], out int p))
            filter = p;

        var tasks = todo.ListTasks(filter);

        if (tasks.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  [{Muted}]Nenhuma tarefa encontrada.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse(Muted))
            .Title($"[{Primary} bold]Tarefas{(filter.HasValue ? $" · prioridade {filter}" : "")}[/]")
            .AddColumn(new TableColumn($"[bold]Tarefa[/]").LeftAligned());

        foreach (var t in tasks)
            table.AddRow(Markup.Escape(t?.ToString() ?? ""));

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [{Muted}]{tasks.Count} tarefa(s)[/]");
        AnsiConsole.WriteLine();
    }

    private static void HandleTodoDone(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            AnsiConsole.MarkupLine($"  [{Muted}]Uso: [bold]/todo done <id>[/][/]");
            return;
        }

        var result = todo.CompleteTask(parts[2]);

        if (result is null)
            AnsiConsole.MarkupLine(
                $"  [{Danger}]✗[/] Tarefa [bold]{Markup.Escape(parts[2])}[/] não encontrada.");
        else
            AnsiConsole.MarkupLine(
                $"  [{Accent}]✓[/] Concluída: {Markup.Escape(result.ToString() ?? "")}");
    }

    private static void HandleTodoRemove(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            AnsiConsole.MarkupLine($"  [{Muted}]Uso: [bold]/todo remove <id>[/][/]");
            return;
        }

        if (todo.DeleteTask(parts[2]))
            AnsiConsole.MarkupLine(
                $"  [{Accent}]✓[/] Tarefa [bold]{Markup.Escape(parts[2])}[/] removida.");
        else
            AnsiConsole.MarkupLine(
                $"  [{Danger}]✗[/] Tarefa [bold]{Markup.Escape(parts[2])}[/] não encontrada.");
    }

    // ═══════════════════════ Parsing ═══════════════════════

    /// <summary>
    /// Divide o input em partes respeitando aspas.
    /// /todo add "título com espaço" 3  →  ["/todo", "add", "título com espaço", "3"]
    /// </summary>
    private static string[] ParseCommand(string input)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts.ToArray();
    }

    // ═══════════════════════ UI ═══════════════════════

    private static void PrintBanner()
    {
        AnsiConsole.WriteLine();

        AnsiConsole.Write(
            new FigletText("Paperless")
                .LeftJustified()
                .Color(Color.FromHex(Primary)));

        AnsiConsole.MarkupLine(
            $"  [{Muted}]Seu assistente pessoal · 100% offline · zero nuvem[/]");

        AnsiConsole.WriteLine();
    }

    private static void PrintHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse(Muted))
            .Title($"[{Primary} bold]Comandos Disponíveis[/]")
            .AddColumn(new TableColumn("[bold]Comando[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Descrição[/]").LeftAligned());

        table.AddRow($"[{Primary}]/todo add[/] \"título\" [[\"desc\"]] [[1-5]]", "Cria uma nova tarefa");
        table.AddRow($"[{Primary}]/todo list[/] [[1-5]]",                       "Lista tarefas (filtro opcional)");
        table.AddRow($"[{Primary}]/todo done[/] <id>",                          "Marca como concluída");
        table.AddRow($"[{Primary}]/todo remove[/] <id>",                        "Remove uma tarefa");
        table.AddEmptyRow();
        table.AddRow($"[{Primary}]/status[/]",                                  "Estatísticas do sistema");
        table.AddRow($"[{Primary}]/clear[/]",                                   "Limpa a sessão atual");
        table.AddRow($"[{Primary}]/help[/]",                                    "Mostra esta ajuda");
        table.AddRow($"[{Primary}]/exit[/]",                                    "Sair do Paperless");
        table.AddEmptyRow();
        table.AddRow(
            $"[{Muted}]qualquer outro texto[/]",
            $"[{Muted}]é enviado como pergunta ao assistente[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void PrintStatus(FileRagModel ragModel, SessionManager session)
    {
        var (files, chunks) = ragModel.GetStats();
        var sessionActive = !session.IsExpired;

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(4))
            .AddColumn(new GridColumn());

        grid.AddRow($"[{Primary}]📁 Arquivos indexados[/]", $"[bold]{files}[/]");
        grid.AddRow($"[{Primary}]🧩 Chunks no banco[/]",    $"[bold]{chunks}[/]");
        grid.AddRow(
            $"[{Primary}]🔗 Sessão ativa[/]",
            sessionActive
                ? $"[{Accent}]sim[/]"
                : $"[{Muted}]não (expirada)[/]");

        if (session.HasContext)
        {
            grid.AddRow(
                $"[{Primary}]💬 Contexto[/]",
                $"[{Muted}]{Markup.Escape(Truncate(session.Summary, 70))}[/]");
        }

        var panel = new Panel(grid)
            .Header($" [{Primary} bold]Status[/] ", Justify.Left)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(Muted))
            .Padding(1, 0, 1, 0);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void PrintVersion()
    {
        // Assembly.GetName().Version pega o que foi passado em
        // -p:Version=... no dotnet publish. Fallback para "dev" quando
        // rodando via `dotnet run` (nenhuma versão gravada).
        var asm = typeof(Program).Assembly;
        var version = asm.GetName().Version;

        // Se veio "0.0.0.0", é porque nada foi setado — exibe "dev".
        var versionString = (version is null || version.ToString() == "0.0.0.0")
            ? "dev"
            : version.ToString(3);

        Console.WriteLine($"paperless {versionString}");
    }

    private static void PrintCliHelp()
    {
        Console.WriteLine("paperless — assistente pessoal 100% offline");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  paperless              Inicia o REPL interativo");
        Console.WriteLine("  paperless --version    Mostra a versão");
        Console.WriteLine("  paperless --help       Mostra esta ajuda");
        Console.WriteLine();
        Console.WriteLine("No REPL, use /help para ver os comandos disponíveis.");
    }

    // ═══════════════════════ Helpers ═══════════════════════

    /// <summary>
    /// Carrega o system prompt. Se apontar para .md/.txt, lê do arquivo.
    /// Caso contrário usa o valor direto.
    /// </summary>
    private static string LoadSystemPrompt(AppSettings settings)
    {
        var value = settings.Assistant.SystemPrompt;

        if (string.IsNullOrWhiteSpace(value))
            return "You are a helpful local assistant.";

        if (value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
         || value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.Combine(AppContext.BaseDirectory, value);

            if (!File.Exists(path))
                path = Path.Combine(settings.Storage.BaseFolder, value);

            if (File.Exists(path))
            {
                AnsiConsole.MarkupLine(
                    $"  [{Muted}]System prompt carregado de: {Markup.Escape(path)}[/]");
                return File.ReadAllText(path).Trim();
            }

            AnsiConsole.MarkupLine(
                $"  [{Warn}]⚠[/] Arquivo de prompt não encontrado: " +
                $"[bold]{Markup.Escape(value)}[/] [{Muted}](usando padrão)[/]");
            return "You are a helpful local assistant.";
        }

        return value;
    }

    /// <summary>
    /// Garante que as pastas necessárias existem.
    /// </summary>
    private static void EnsureDirectories(StorageSettings storage)
    {
        var folders = new[]
        {
            storage.BaseFolder,
            Path.GetDirectoryName(storage.GetFullTasksPath()),
            Path.GetDirectoryName(storage.GetFullDatabasePath()),
            storage.GetFullUserFolderPath(),
        };

        foreach (var folder in folders)
        {
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "...";

    private enum CommandResult
    {
        Handled,
        NotHandled,
        Exit,
    }
}