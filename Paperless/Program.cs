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

        Console.WriteLine("Verificando conexão com Ollama...");

        if (!await ollama.HealthCheckAsync())
        {
            Console.WriteLine("Ollama não está rodando!");
            Console.WriteLine("Execute 'ollama serve' e tente novamente.");
            return;
        }

        Console.WriteLine("Ollama conectado.\n");

        /* ══════════════ 5. Carregar system prompt ══════════════ */

        var systemPrompt = LoadSystemPrompt(settings);

        /* ══════════════ 6. Iniciar FileIndexer ══════════════ */

        var userFolder = settings.Storage.GetFullUserFolderPath();

        using var indexer = new FileIndexer(ollama, ragModel, userFolder);
        await indexer.StartAsync();
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
        Console.WriteLine("\nAté mais!");
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
            Console.Write("\n> ");

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

                /* Chat com RAG */
                Console.WriteLine();
                var answer = await chat.AskAsync(trimmed, ct);
                Console.WriteLine(answer);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
        }
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
                Console.WriteLine("Sessão limpa.");
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
            Console.WriteLine("Uso: /todo <add|list|done|remove> [args]");
            Console.WriteLine("     /help para detalhes.");
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
                Console.WriteLine($"Ação desconhecida: {action}");
                Console.WriteLine("Ações: add, list, done, remove");
                break;
        }
    }

    private static void HandleTodoAdd(string[] parts, TodoManager todo)
    {
        // /todo add "título" "descrição" prioridade
        // /todo add "título" prioridade
        // /todo add "título"
        if (parts.Length < 3)
        {
            Console.WriteLine("Uso: /todo add \"título\" [\"descrição\"] [1-5]");
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
        Console.WriteLine($" ✓ Tarefa criada: {task}");
    }

    private static void HandleTodoList(string[] parts, TodoManager todo)
    {
        int? filter = null;

        if (parts.Length >= 3 && int.TryParse(parts[2], out int p))
            filter = p;

        var tasks = todo.ListTasks(filter);

        if (tasks.Count == 0)
        {
            Console.WriteLine(" Nenhuma tarefa encontrada.");
            return;
        }

        Console.WriteLine();
        foreach (var t in tasks)
            Console.WriteLine(t);
        Console.WriteLine();
        Console.WriteLine($" {tasks.Count} tarefa(s)");
    }

    private static void HandleTodoDone(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("Uso: /todo done <id>");
            return;
        }

        var result = todo.CompleteTask(parts[2]);

        if (result is null)
            Console.WriteLine($" Tarefa '{parts[2]}' não encontrada.");
        else
            Console.WriteLine($" ✓ Concluída: {result}");
    }

    private static void HandleTodoRemove(string[] parts, TodoManager todo)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("Uso: /todo remove <id>");
            return;
        }

        if (todo.DeleteTask(parts[2]))
            Console.WriteLine($" ✓ Tarefa '{parts[2]}' removida.");
        else
            Console.WriteLine($" Tarefa '{parts[2]}' não encontrada.");
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
        Console.WriteLine("╔═══════════════════════════════════════╗");
        Console.WriteLine("║       Paperless — Assistente CLI      ║");
        Console.WriteLine("║         100% offline com Ollama       ║");
        Console.WriteLine("╚═══════════════════════════════════════╝");
        Console.WriteLine("  /help para ver os comandos disponíveis");
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("  Comandos:");
        Console.WriteLine("    /todo add \"título\" [\"descrição\"] [1-5]  — criar tarefa");
        Console.WriteLine("    /todo list [1-5]                        — listar tarefas");
        Console.WriteLine("    /todo done <id>                         — concluir tarefa");
        Console.WriteLine("    /todo remove <id>                       — remover tarefa");
        Console.WriteLine("    /status                                 — estatísticas do índice");
        Console.WriteLine("    /clear                                  — limpar sessão");
        Console.WriteLine("    /exit                                   — sair");
        Console.WriteLine();
        Console.WriteLine("  Qualquer outro texto é enviado como pergunta ao assistente.");
        Console.WriteLine();
    }

    private static void PrintStatus(FileRagModel ragModel, SessionManager session)
    {
        var (files, chunks) = ragModel.GetStats();

        Console.WriteLine();
        Console.WriteLine($"  Arquivos indexados: {files}");
        Console.WriteLine($"  Chunks no banco:    {chunks}");
        Console.WriteLine($"  Sessão ativa:       {(session.IsExpired ? "não (expirada)" : "sim")}");

        if (session.HasContext)
            Console.WriteLine($"  Contexto sessão:    {Truncate(session.Summary, 80)}");

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
                Console.WriteLine($"System prompt carregado de: {path}");
                return File.ReadAllText(path).Trim();
            }

            Console.WriteLine($"Arquivo de prompt não encontrado: {value} (usando padrão)");
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
