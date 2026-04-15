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
using Paperless.Modules.Vector.Model;

internal class Program
{
    static async Task Main(string[] args)
    {
        /* ══════════════ 1. Configuração ══════════════ */

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var settings = config.Get<AppSettings>() ?? new AppSettings();

        /* ══════════════ 2. Criar estrutura de pastas ══════════════ */

        EnsureDirectories(settings.Storage);

        /* ══════════════ 3. Inicializar serviços ══════════════ */

        using var ollama = new OllamaClient(settings.Ollama);

        var ragModel = new FileRagModel(settings.Storage.GetFullDatabasePath());
        var todoRepo = new TodoRepository(settings.Storage.GetFullTasksPath());
        var todoManager = new TodoManager(todoRepo);
        var session = new SessionManager(ttlMinutes: 10);

        /* ══════════════ 4. Health check do Ollama ══════════════ */

        using (var healthSpinner = new Spinner("Verificando conexão com Ollama"))
        {
            if (!await ollama.HealthCheckAsync())
            {
                healthSpinner.Fail("Ollama não está rodando!");
                Console.WriteLine("  Execute 'ollama serve' e tente novamente.");
                return;
            }
            healthSpinner.Succeed("Ollama conectado");
        }

        /* ══════════════ 5. Carregar system prompt ══════════════ */

        var systemPrompt = LoadSystemPrompt(settings);

        /* ══════════════ 6. Iniciar FileIndexer ══════════════ */

        var userFolder = settings.Storage.GetFullUserFolderPath();

        using var indexer = new FileIndexer(ollama, ragModel, userFolder);

        using (var indexSpinner = new Spinner("Indexando arquivos"))
        {
            await indexer.StartAsync();
            indexSpinner.Succeed("Arquivos indexados");
        }

        Console.WriteLine();

        /* ══════════════ 7. Iniciar ChatService ══════════════ */

        var chat = new ChatService(ollama, ragModel, session, systemPrompt);

        /* ══════════════ 8. REPL ══════════════ */

        PrintBanner();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await ReplLoopAsync(chat, todoManager, ragModel, session, cts.Token);

        /* ══════════════ 9. Shutdown ══════════════ */

        indexer.Stop();
        Console.WriteLine("\n  Até mais! 👋");
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
            Console.Write("\n  ❯ ");
            Console.ForegroundColor = ConsoleColor.Cyan;

            string? input;
            try
            {
                input = Console.ReadLine();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            finally
            {
                Console.ResetColor();
            }

            if (input is null)
                break;

            var trimmed = input.Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

            try
            {
                /* Comandos especiais */
                if (trimmed.StartsWith('/'))
                {
                    var handled = await HandleCommandAsync(trimmed, todo, ragModel, session);

                    if (handled == CommandResult.Exit)
                        break;

                    if (handled == CommandResult.Handled)
                        continue;

                    /* CommandResult.NotHandled → tratar como pergunta normal */
                }

                /* Chat com RAG — com spinner de fases */
                Console.WriteLine();
                await AskWithSpinnerAsync(chat, trimmed, ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n  ⚠ Operação cancelada.");
                break;
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"\n  ⏱ Timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  ✗ Erro: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Executa a pergunta ao ChatService exibindo um spinner com fases
    /// progressivas enquanto aguarda a resposta.
    /// </summary>
    private static async Task AskWithSpinnerAsync(
        ChatService chat, string question, CancellationToken ct)
    {
        // Cria um CTS vinculado para poder cancelar o spinner
        // independentemente quando a resposta chegar.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var phases = new[]
        {
            ("🔍", "Analisando pergunta..."),
            ("📚", "Buscando contexto relevante..."),
            ("🧠", "Planejando resposta..."),
            ("✍️",  "Gerando resposta..."),
        };

        // Inicia o spinner de fases em background
        var spinnerTask = RunPhasedSpinnerAsync(phases, linkedCts.Token);

        string answer;
        try
        {
            // Executa a chamada real — aqui é onde o Ollama processa
            answer = await chat.AskAsync(question, ct);
        }
        finally
        {
            // Para o spinner assim que a resposta chegar (ou erro)
            linkedCts.Cancel();

            try { await spinnerTask; }
            catch (OperationCanceledException) { /* esperado */ }

            // Limpa a linha do spinner
            ClearCurrentLine();
        }

        // Exibe a resposta formatada
        PrintAnswer(answer);
    }

    /// <summary>
    /// Exibe um spinner animado que progride por fases ao longo do tempo.
    /// Cada fase tem um ícone e uma mensagem descritiva.
    /// </summary>
    private static async Task RunPhasedSpinnerAsync(
        (string icon, string text)[] phases,
        CancellationToken ct)
    {
        var spinChars = new[] { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };
        int spinIdx = 0;
        int phaseIdx = 0;
        int tick = 0;

        // Cada fase dura ~2.5s (25 ticks × 100ms)
        const int ticksPerPhase = 25;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (icon, text) = phases[phaseIdx];
                var spinChar = spinChars[spinIdx % spinChars.Length];

                ClearCurrentLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"  {spinChar} {icon} {text}");
                Console.ResetColor();

                await Task.Delay(100, ct);

                spinIdx++;
                tick++;

                // Avança de fase (exceto a última, que fica girando)
                if (tick >= ticksPerPhase && phaseIdx < phases.Length - 1)
                {
                    phaseIdx++;
                    tick = 0;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Esperado — o spinner foi cancelado porque a resposta chegou
        }
    }

    /// <summary>
    /// Exibe a resposta do assistente com formatação visual.
    /// </summary>
    private static void PrintAnswer(string answer)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✔ ");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Resposta:\n");
        Console.ResetColor();

        // Imprime com margem lateral para melhor leitura
        var lines = answer.Split('\n');
        foreach (var line in lines)
            Console.WriteLine($"  {line}");

        Console.WriteLine();
    }

    /// <summary>
    /// Limpa a linha atual do console (usada pelo spinner).
    /// </summary>
    private static void ClearCurrentLine()
    {
        int currentLine = Console.CursorTop;
        Console.SetCursorPosition(0, currentLine);
        Console.Write(new string(' ', Math.Min(Console.WindowWidth, 120)));
        Console.SetCursorPosition(0, currentLine);
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
                Console.WriteLine("  ✔ Sessão limpa.");
                return Task.FromResult(CommandResult.Handled);

            case "/todo":
                HandleTodoCommand(parts, todo);
                return Task.FromResult(CommandResult.Handled);

            default:
                return Task.FromResult(CommandResult.NotHandled);
        }
    }

    // ───────────────────────── /todo ─────────────────────────

    private static void HandleTodoCommand(string[] parts, TodoManager todo)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("  Uso: /todo <add|list|done|remove> [args]");
            Console.WriteLine("       /help para detalhes.");
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
                Console.WriteLine($"  Ação desconhecida: {action}");
                Console.WriteLine("  Ações: add, list, done, remove");
                break;
        }
    }

    private static void HandleTodoAdd(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("  Uso: /todo add \"título\" [\"descrição\"] [1-5]");
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
        Console.WriteLine($"  ✔ Tarefa criada: {task}");
    }

    private static void HandleTodoList(string[] parts, TodoManager todo)
    {
        int? filter = null;

        if (parts.Length >= 3 && int.TryParse(parts[2], out int p))
            filter = p;

        var tasks = todo.ListTasks(filter);

        if (tasks.Count == 0)
        {
            Console.WriteLine("  Nenhuma tarefa encontrada.");
            return;
        }

        Console.WriteLine();
        foreach (var t in tasks)
            Console.WriteLine($"  {t}");
        Console.WriteLine();
        Console.WriteLine($"  {tasks.Count} tarefa(s)");
    }

    private static void HandleTodoDone(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("  Uso: /todo done <id>");
            return;
        }

        var result = todo.CompleteTask(parts[2]);

        if (result is null)
            Console.WriteLine($"  ✗ Tarefa '{parts[2]}' não encontrada.");
        else
            Console.WriteLine($"  ✔ Concluída: {result}");
    }

    private static void HandleTodoRemove(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("  Uso: /todo remove <id>");
            return;
        }

        if (todo.DeleteTask(parts[2]))
            Console.WriteLine($"  ✔ Tarefa '{parts[2]}' removida.");
        else
            Console.WriteLine($"  ✗ Tarefa '{parts[2]}' não encontrada.");
    }

    // ═══════════════════════ Parsing ═══════════════════════

    /// <summary>
    /// Divide o input em partes respeitando aspas.
    /// "/todo add "título com espaço" 3" → ["/todo", "add", "título com espaço", "3"]
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
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═══════════════════════════════════════════╗");
        Console.WriteLine("║       📄 Paperless — Assistente CLI       ║");
        Console.WriteLine("║          100% offline com Ollama          ║");
        Console.WriteLine("╚═══════════════════════════════════════════╝");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  /help para ver os comandos disponíveis");
        Console.ResetColor();
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╭─── Comandos ─────────────────────────────────────╮");
        Console.ResetColor();
        Console.WriteLine("  │                                                  │");
        Console.WriteLine("  │  /todo add \"título\" [\"desc\"] [1-5]  → criar      │");
        Console.WriteLine("  │  /todo list [1-5]                   → listar     │");
        Console.WriteLine("  │  /todo done <id>                    → concluir   │");
        Console.WriteLine("  │  /todo remove <id>                  → remover    │");
        Console.WriteLine("  │  /status                            → estatísticas│");
        Console.WriteLine("  │  /clear                             → limpar     │");
        Console.WriteLine("  │  /exit                              → sair       │");
        Console.WriteLine("  │                                                  │");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  │  Qualquer outro texto → pergunta ao assistente   │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╰─────────────────────────────────────────────────╯");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintStatus(FileRagModel ragModel, SessionManager session)
    {
        var (files, chunks) = ragModel.GetStats();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  📁 Arquivos indexados: ");
        Console.ResetColor();
        Console.WriteLine(files);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  🧩 Chunks no banco:    ");
        Console.ResetColor();
        Console.WriteLine(chunks);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  🔗 Sessão ativa:       ");
        Console.ResetColor();
        Console.WriteLine(session.IsExpired ? "não (expirada)" : "sim");

        if (session.HasContext)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  💬 Contexto: {Truncate(session.Summary, 70)}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    // ═══════════════════════ Helpers ═══════════════════════

    /// <summary>
    /// Carrega o system prompt. Se o valor em config apontar para um arquivo
    /// (.md, .txt), lê o conteúdo do arquivo. Caso contrário, usa o valor direto.
    /// </summary>
    private static string LoadSystemPrompt(AppSettings settings)
    {
        var value = settings.Assistant.SystemPrompt;

        if (string.IsNullOrWhiteSpace(value))
            return "You are a helpful local assistant.";

        /* Se parece um arquivo, tenta carregar */
        if (value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
         || value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            /* Tenta caminho relativo ao BaseDirectory */
            var path = Path.Combine(AppContext.BaseDirectory, value);

            if (!File.Exists(path))
            {
                /* Tenta caminho relativo ao BaseFolder */
                path = Path.Combine(settings.Storage.BaseFolder, value);
            }

            if (File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  System prompt carregado de: {path}");
                Console.ResetColor();
                return File.ReadAllText(path).Trim();
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Arquivo de prompt não encontrado: {value} (usando padrão)");
            Console.ResetColor();
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

// ═══════════════════════ Spinner ═══════════════════════

/// <summary>
/// Spinner animado reutilizável para operações de inicialização.
/// Uso: using var s = new Spinner("Carregando..."); ... s.Succeed("OK");
/// </summary>
internal sealed class Spinner : IDisposable
{
    private static readonly char[] _frames = { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };

    private readonly string _text;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _animationTask;
    private bool _finished;

    public Spinner(string text)
    {
        _text = text;
        _animationTask = AnimateAsync(_cts.Token);
    }

    private async Task AnimateAsync(CancellationToken ct)
    {
        int i = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = _frames[i++ % _frames.Length];
                ClearLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"  {frame} {_text}");
                Console.ResetColor();
                await Task.Delay(80, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Succeed(string message)
    {
        Stop();
        ClearLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✔ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void Fail(string message)
    {
        Stop();
        ClearLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private void Stop()
    {
        if (_finished) return;
        _finished = true;
        _cts.Cancel();
        try { _animationTask.Wait(); } catch { }
    }

    private static void ClearLine()
    {
        int top = Console.CursorTop;
        Console.SetCursorPosition(0, top);
        Console.Write(new string(' ', Math.Min(Console.WindowWidth, 120)));
        Console.SetCursorPosition(0, top);
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}