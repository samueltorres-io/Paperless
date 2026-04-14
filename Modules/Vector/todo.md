### 2. VectorStore.cs (SQLite + Cosseno em C#)
 
**Responsabilidade:** Armazenar e buscar chunks vetorizados.

**Schema SQLite:**
```sql
CREATE TABLE IF NOT EXISTS chunks (
    id TEXT PRIMARY KEY,           -- "filepath:chunk_index"
    file_path TEXT NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding BLOB NOT NULL,       -- float[] serializado
    file_modified_utc TEXT NOT NULL,
    created_at TEXT DEFAULT (datetime('now'))
);
 
CREATE INDEX IF NOT EXISTS idx_chunks_filepath ON chunks(file_path);
```
 
**Operações:**
- `UpsertChunk(chunk)` — INSERT OR REPLACE
- `DeleteByFilePath(path)` — remove todos os chunks de um arquivo
- `SearchSimilar(queryEmbedding, topK=3)` — busca os K chunks mais próximos
 
**Busca por similaridade (sem sqlite-vss):**
```csharp
// Carregar todos os embeddings em memória
// Calcular cosseno contra o query embedding
// Retornar top-K ordenado por score
 
public List<(DocumentChunk chunk, double score)> SearchSimilar(float[] query, int topK = 3)
{
    var allChunks = LoadAllChunks(); // SELECT * FROM chunks
    return allChunks
        .Select(c => (chunk: c, score: CosineSimilarity.Compute(query, c.Embedding)))
        .OrderByDescending(x => x.score)
        .Take(topK)
        .Where(x => x.score > 0.3) // threshold mínimo
        .ToList();
}
```
 
**Serialização do embedding:**
```csharp
// float[] → byte[] (para BLOB)
byte[] ToBytes(float[] vec) => MemoryMarshal.AsBytes(vec.AsSpan()).ToArray();
 
// byte[] → float[] (do BLOB)
float[] FromBytes(byte[] data) => MemoryMarshal.Cast<byte, float>(data).ToArray();
```
 
**NuGet necessário:** `Microsoft.Data.Sqlite` (leve, ~200KB)
