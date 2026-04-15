# Gerenciamento de arquivos

### 3. FileIndexer.cs (Watcher + Indexação Incremental)
 
**Responsabilidade:** Monitorar a pasta do usuário e manter o banco vetorial sincronizado.
 
**Fluxo de Indexação Inicial (startup):**
```
1. Listar todos os arquivos suportados na pasta
2. Para cada arquivo:
   a. Checar se já existe no SQLite com mesmo modified_date
   b. Se NÃO existe ou mudou → chunkar + vetorizar + upsert
   c. Se existe e NÃO mudou → pular (já indexado)
3. Remover do SQLite arquivos que não existem mais na pasta
```
 
**Fluxo de Atualização em Tempo Real:**
```
FileSystemWatcher
    ├── OnCreated  → indexar novo arquivo
    ├── OnChanged  → deletar chunks antigos → reindexar
    ├── OnDeleted  → deletar chunks do arquivo
    └── OnRenamed  → deletar path antigo → indexar path novo
```
 
**Debounce importante:**
- O `FileSystemWatcher` dispara MÚLTIPLOS eventos por uma única edição
- Implementar debounce de 2 segundos: agrupar eventos do mesmo arquivo
- Usar `System.Timers.Timer` ou `CancellationTokenSource` com delay
 
```csharp
// Debounce simples com Dictionary<string, CancellationTokenSource>
private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();
 
private void OnFileChanged(string filePath)
{
    if (_pending.TryRemove(filePath, out var oldCts))
        oldCts.Cancel();
 
    var cts = new CancellationTokenSource();
    _pending[filePath] = cts;
 
    Task.Delay(2000, cts.Token).ContinueWith(t =>
    {
        if (!t.IsCanceled)
        {
            _pending.TryRemove(filePath, out _);
            ReindexFile(filePath);
        }
    });
}
```
 
**Arquivos suportados:** `.txt`, `.md`, `.json`, `.csv`, `.log`, `.cs`, `.java`, `.py`, `.xml`
 
---
 
### 4. TextChunker.cs
 
**Responsabilidade:** Dividir arquivos em chunks menores para vetorização.
 
**Estratégia simples (suficiente para v1):**
- Chunk size: ~500 caracteres com overlap de 100
- Separar por parágrafos primeiro (`\n\n`), depois por linhas se necessário
- Cada chunk guarda: file_path, chunk_index, conteúdo
 
```
Arquivo (2000 chars)
  → Chunk 0: chars 0-500
  → Chunk 1: chars 400-900    (overlap de 100)
  → Chunk 2: chars 800-1300
  → Chunk 3: chars 1200-1700
  → Chunk 4: chars 1600-2000
```
 
**Para arquivos pequenos (<500 chars):** chunk único, sem split.