using System.Collections.Concurrent;
using Paperless.Modules.Ollama;
using Paperless.Modules.Vector.Entity;
using Paperless.Modules.Vector.Model;

namespace Paperless.Modules.File;

/// <summary>
/// Monitora a pasta do usuário e mantém o banco vetorial sincronizado.
///
/// Ciclo de vida:
///   1. StartAsync() → scan inicial + ativa FileSystemWatcher
///   2. Eventos de arquivo → debounce 20s → indexação/remoção
///   3. Stop() → desativa watcher e cancela operações pendentes
///
/// Deduplicação:
///   Antes de chamar o Ollama para gerar embedding, verifica se já
///   existe um chunk com conteúdo idêntico no banco. Se existir,
///   reutiliza o vetor.
/// </summary>
public sealed class FileIndexer : IDisposable
{
    private readonly OllamaClient _ollama;
    private readonly FileRagModel _ragModel;
    private readonly FileIndexerOptions _options;
    private readonly string _watchPath;
    private readonly CancellationTokenSource _cts = new();

    private FileSystemWatcher? _watcher;
    private bool _disposed;

    /// <summary>
    /// Operações pendentes por arquivo (debounce).
    /// Chave = caminho absoluto do arquivo.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    public FileIndexer(
        OllamaClient ollama,
        FileRagModel ragModel,
        string watchPath,
        FileIndexerOptions? options = null)
    {
        _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
        _ragModel = ragModel ?? throw new ArgumentNullException(nameof(ragModel));
        _options = options ?? new FileIndexerOptions();

        _watchPath = Path.GetFullPath(watchPath);

        if (!Directory.Exists(_watchPath))
            Directory.CreateDirectory(_watchPath);
    }

    /// <summary>
    /// Executa o scan inicial (bloqueante) e depois ativa o watcher.
    /// Chamar uma única vez na inicialização do app.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[FileIndexer] Indexando pasta: {_watchPath}");

        await InitialScanAsync(ct);
        StartWatcher();

        var (files, chunks) = _ragModel.GetStats();
        Console.WriteLine($"[FileIndexer] Pronto — {files} arquivo(s), {chunks} chunk(s) indexados.");
        Console.WriteLine($"[FileIndexer] Monitorando mudanças (debounce: {_options.DebounceMs / 1000}s)...");
    }

    /// <summary>
    /// Para o watcher e cancela todas as operações pendentes.
    /// </summary>
    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
        }

        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    // ═══════════════════════ Scan Inicial ═══════════════════════

    /// <summary>
    /// Varre a pasta, indexa arquivos novos/modificados
    /// e remove do banco arquivos que não existem mais.
    /// </summary>
    private async Task InitialScanAsync(CancellationToken ct)
    {
        var currentFiles = GetSupportedFiles();
        var currentRelPaths = currentFiles
            .Select(f => GetRelativePath(f))
            .ToHashSet(StringComparer.Ordinal);

        int indexed = 0, skipped = 0, removed = 0;

        /* Indexar novos ou modificados */
        foreach (var fullPath in currentFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relPath = GetRelativePath(fullPath);
            var fileModified = System.IO.File.GetLastWriteTimeUtc(fullPath);
            var storedModified = _ragModel.GetFileModifiedUtc(relPath);

            if (storedModified.HasValue && storedModified.Value >= fileModified)
            {
                skipped++;
                continue;
            }

            try
            {
                await IndexFileAsync(fullPath, ct);
                indexed++;
                Console.WriteLine($"[FileIndexer]   ✓ {relPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileIndexer]   ✗ {relPath}: {ex.Message}");
            }
        }

        /* Remover órfãos — arquivos no banco que não existem mais */
        var indexedPaths = _ragModel.GetIndexedFilePaths();

        foreach (var path in indexedPaths)
        {
            if (!currentRelPaths.Contains(path))
            {
                _ragModel.DeleteByFilePath(path);
                removed++;
                Console.WriteLine($"[FileIndexer]   ⊘ {path} (removido do índice)");
            }
        }

        if (indexed > 0 || removed > 0)
            Console.WriteLine($"[FileIndexer] Scan: {indexed} indexado(s), {skipped} inalterado(s), {removed} removido(s).");
    }

    // ═══════════════════════ FileSystemWatcher ═══════════════════════

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(_watchPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.CreationTime,
        };

        _watcher.Created += (_, e) => ScheduleEvent(e.FullPath, FileAction.Upsert);
        _watcher.Changed += (_, e) => ScheduleEvent(e.FullPath, FileAction.Upsert);
        _watcher.Deleted += (_, e) => ScheduleEvent(e.FullPath, FileAction.Delete);
        _watcher.Renamed += (_, e) => OnRenamed(e.OldFullPath, e.FullPath);

        _watcher.Error += (_, e) =>
            Console.WriteLine($"[FileIndexer] Erro no watcher: {e.GetException().Message}");

        _watcher.EnableRaisingEvents = true;
    }

    // ═══════════════════════ Debounce ═══════════════════════

    /// <summary>
    /// Agenda um evento com debounce.
    /// Se outro evento chegar para o mesmo arquivo dentro do intervalo,
    /// o anterior é cancelado e o timer reinicia.
    /// </summary>
    private void ScheduleEvent(string fullPath, FileAction action)
    {
        if (!_options.IsSupported(fullPath))
            return;

        /* Cancela evento anterior para o mesmo arquivo */
        if (_pending.TryRemove(fullPath, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _pending[fullPath] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_options.DebounceMs, cts.Token);
                _pending.TryRemove(fullPath, out _);

                await ProcessEventAsync(fullPath, action, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                /* Debounced — evento substituído por um mais recente */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileIndexer] Erro processando {GetRelativePath(fullPath)}: {ex.Message}");
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    private void OnRenamed(string oldFullPath, string newFullPath)
    {
        bool oldSupported = _options.IsSupported(oldFullPath);
        bool newSupported = _options.IsSupported(newFullPath);

        /* Renomeou de suportado para não-suportado → tratar como deleção */
        if (oldSupported && !newSupported)
        {
            ScheduleEvent(oldFullPath, FileAction.Delete);
            return;
        }

        /* Renomeou de não-suportado para suportado → tratar como criação */
        if (!oldSupported && newSupported)
        {
            ScheduleEvent(newFullPath, FileAction.Upsert);
            return;
        }

        /* Ambos suportados → deletar path antigo, indexar novo */
        if (oldSupported && newSupported)
        {
            var relOld = GetRelativePath(oldFullPath);
            _ragModel.DeleteByFilePath(relOld);
            Console.WriteLine($"[FileIndexer] Renomeado: {relOld} → {GetRelativePath(newFullPath)}");

            ScheduleEvent(newFullPath, FileAction.Upsert);
        }
    }

    // ═══════════════════════ Processamento ═══════════════════════

    private async Task ProcessEventAsync(string fullPath, FileAction action, CancellationToken ct = default)
    {
        var relPath = GetRelativePath(fullPath);

        switch (action)
        {
            case FileAction.Delete:
                int removed = _ragModel.DeleteByFilePath(relPath);
                if (removed > 0)
                    Console.WriteLine($"[FileIndexer] Removido: {relPath} ({removed} chunks)");
                break;

            case FileAction.Upsert:
                if (!System.IO.File.Exists(fullPath))
                    return;

                await IndexFileAsync(fullPath, ct);
                Console.WriteLine($"[FileIndexer] Indexado: {relPath}");
                break;
        }
    }

    // ═══════════════════════ Indexação (Pipeline RAG) ═══════════════════════

    /// <summary>
    /// Pipeline completo: lê arquivo → chunka → vetoriza (com dedup) → persiste.
    /// Usa ReplaceFileChunks para substituição atômica.
    /// </summary>
    private async Task IndexFileAsync(string fullPath, CancellationToken ct)
    {
        var content = await System.IO.File.ReadAllTextAsync(fullPath, ct);

        if (string.IsNullOrWhiteSpace(content))
            return;

        var relPath = GetRelativePath(fullPath);
        var fileModified = System.IO.File.GetLastWriteTimeUtc(fullPath);

        var textChunks = TextChunker.Chunk(content, _options.ChunkSize, _options.Overlap);
        var docChunks = new List<DocumentChunk>(textChunks.Count);

        for (int i = 0; i < textChunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var chunkContent = textChunks[i];

            /* Deduplicação: reutiliza embedding se conteúdo idêntico já existe */
            float[] embedding = await GetOrCreateEmbeddingAsync(chunkContent, ct);

            docChunks.Add(new DocumentChunk
            {
                Id = DocumentChunk.BuildId(relPath, i),
                FilePath = relPath,
                ChunkIndex = i,
                Content = chunkContent,
                Embedding = embedding,
                FileModifiedUtc = fileModified,
            });
        }

        /* Substituição atômica: deleta chunks antigos e insere novos em transação */
        _ragModel.ReplaceFileChunks(relPath, docChunks);
    }

    /// <summary>
    /// Verifica se já existe um chunk com conteúdo idêntico no banco.
    /// Se existir, reutiliza o embedding (evita chamada ao Ollama).
    /// Se não existir, gera um novo embedding.
    /// </summary>
    private async Task<float[]> GetOrCreateEmbeddingAsync(string content, CancellationToken ct)
    {
        var existing = _ragModel.FindEmbeddingByContent(content);

        if (existing is not null)
            return existing;

        return await _ollama.EmbedAsync(content, ct);
    }

    // ═══════════════════════ Helpers ═══════════════════════

    /// <summary>
    /// Lista todos os arquivos suportados na pasta monitorada.
    /// </summary>
    private List<string> GetSupportedFiles()
    {
        return Directory
            .EnumerateFiles(_watchPath, "*", SearchOption.AllDirectories)
            .Where(f => _options.IsSupported(f))
            .ToList();
    }

    /// <summary>
    /// Converte caminho absoluto em relativo à pasta monitorada.
    /// O banco armazena apenas paths relativos para portabilidade.
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_watchPath, fullPath);
    }

    // ═══════════════════════ Dispose ═══════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        _cts.Cancel();
        /* Fallback --> */Stop();
        _watcher?.Dispose();
        _disposed = true;
        _cts.Dispose();
    }

    private enum FileAction
    {
        Upsert,
        Delete,
    }
}
