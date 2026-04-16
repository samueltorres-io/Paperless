using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using Paperless.Configuration;
using Paperless.Modules.Vector.Entity;
using Paperless.Modules.Vector.Util;

namespace Paperless.Modules.Vector.Model;

public class FileRagModel : DataContext, IFileRagModel
{
    /// <summary>
    /// Recebe o caminho completo do banco SQLite.
    /// Ex: Path.Combine(baseFolder, "system/paperless.db")
    /// </summary>
    public FileRagModel(string databasePath) : base(databasePath) { }

    protected override string GetSchema() => @"
        CREATE TABLE IF NOT EXISTS chunks (
            id              TEXT PRIMARY KEY,
            file_path       TEXT NOT NULL,
            chunk_index     INTEGER NOT NULL,
            content         TEXT NOT NULL,
            embedding       BLOB NOT NULL,
            file_modified_utc TEXT NOT NULL,
            created_at      TEXT DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_chunks_filepath ON chunks(file_path);
    ";

    /// <summary>
    /// Insere ou atualiza um único chunk (INSERT OR REPLACE).
    /// </summary>
    public void UpsertChunk(DocumentChunk chunk)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO chunks
            (id, file_path, chunk_index, content, embedding, file_modified_utc)
            VALUES ($id, $fp, $ci, $ct, $emb, $fmu);
        ";

        AddChunkParams(cmd, chunk);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Retorna um chunk pelo Id ("filepath:chunk_index") ou null se não existir.
    /// </summary>
    public DocumentChunk? GetChunkById(string id)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM chunks WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>
    /// Retorna todos os chunks de um arquivo, ordenados por chunk_index.
    /// </summary>
    public List<DocumentChunk> GetChunksByFile(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM chunks WHERE file_path = $fp ORDER BY chunk_index";
        cmd.Parameters.AddWithValue("$fp", filePath);

        return ReadAll(cmd);
    }

    /// <summary>
    /// Remove todos os chunks de um arquivo. Retorna quantas linhas foram deletadas.
    /// </summary>
    public int DeleteByFilePath(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks WHERE file_path = $fp";
        cmd.Parameters.AddWithValue("$fp", filePath);

        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Remove um chunk específico pelo Id. Retorna true se deletou.
    /// </summary>
    public bool DeleteById(string id)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Insere/atualiza vários chunks em uma única transação.
    /// Reutiliza parâmetros para evitar alocação repetida.
    /// </summary>
    public void UpsertChunks(IEnumerable<DocumentChunk> chunks)
    {
        using var conn = GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT OR REPLACE INTO chunks
                (id, file_path, chunk_index, content, embedding, file_modified_utc)
                VALUES ($id, $fp, $ci, $ct, $emb, $fmu);
            ";

            var pId  = cmd.Parameters.Add("$id",  SqliteType.Text);
            var pFp  = cmd.Parameters.Add("$fp",  SqliteType.Text);
            var pCi  = cmd.Parameters.Add("$ci",  SqliteType.Integer);
            var pCt  = cmd.Parameters.Add("$ct",  SqliteType.Text);
            var pEmb = cmd.Parameters.Add("$emb", SqliteType.Blob);
            var pFmu = cmd.Parameters.Add("$fmu", SqliteType.Text);

            foreach (var chunk in chunks)
            {
                pId.Value  = chunk.Id;
                pFp.Value  = chunk.FilePath;
                pCi.Value  = chunk.ChunkIndex;
                pCt.Value  = chunk.Content;
                pEmb.Value = ToBytes(chunk.Embedding);
                pFmu.Value = chunk.FileModifiedUtc.ToString("o");

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Substitui atomicamente todos os chunks de um arquivo:
    /// deleta os antigos → insere os novos, tudo em uma transação.
    /// Ideal para re-indexação de arquivo modificado.
    /// </summary>
    public void ReplaceFileChunks(string filePath, IEnumerable<DocumentChunk> newChunks)
    {
        using var conn = GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            using (var delCmd = conn.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM chunks WHERE file_path = $fp";
                delCmd.Parameters.AddWithValue("$fp", filePath);
                delCmd.ExecuteNonQuery();
            }

            using (var insCmd = conn.CreateCommand())
            {
                insCmd.Transaction = tx;
                insCmd.CommandText = @"
                    INSERT INTO chunks
                    (id, file_path, chunk_index, content, embedding, file_modified_utc)
                    VALUES ($id, $fp, $ci, $ct, $emb, $fmu);
                ";

                var pId  = insCmd.Parameters.Add("$id",  SqliteType.Text);
                var pFp  = insCmd.Parameters.Add("$fp",  SqliteType.Text);
                var pCi  = insCmd.Parameters.Add("$ci",  SqliteType.Integer);
                var pCt  = insCmd.Parameters.Add("$ct",  SqliteType.Text);
                var pEmb = insCmd.Parameters.Add("$emb", SqliteType.Blob);
                var pFmu = insCmd.Parameters.Add("$fmu", SqliteType.Text);

                foreach (var chunk in newChunks)
                {
                    pId.Value  = chunk.Id;
                    pFp.Value  = chunk.FilePath;
                    pCi.Value  = chunk.ChunkIndex;
                    pCt.Value  = chunk.Content;
                    pEmb.Value = ToBytes(chunk.Embedding);
                    pFmu.Value = chunk.FileModifiedUtc.ToString("o");

                    insCmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Carrega todos os chunks do banco.
    /// </summary>
    public List<DocumentChunk> GetAllChunks()
    {
        using var conn = GetConnection();
        conn.Open();

        using (var command = new SqliteCommand("PRAGMA busy_timeout = 5000;", conn)) 
        {
            command.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM chunks";
        
        return ReadAll(cmd);
    }

    /// <summary>
    /// Busca os K chunks mais similares ao embedding de consulta.
    /// Filtra abaixo do threshold ANTES de pegar o top-K.
    /// </summary>
    public List<(DocumentChunk Chunk, double Score)> SearchSimilar(
        float[] queryEmbedding,
        int topK = 3,
        double minScore = 0.3)
    {
        var allChunks = GetAllChunks();

        return allChunks
            .Select(c => (Chunk: c, Score: CosineSimilarity.Compute(queryEmbedding, c.Embedding)))
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Busca similar restrita a arquivos específicos.
    /// Carrega apenas os chunks dos arquivos indicados (evita carregar o banco inteiro).
    /// </summary>
    public List<(DocumentChunk Chunk, double Score)> SearchSimilarInFiles(
        float[] queryEmbedding,
        IEnumerable<string> filePaths,
        int topK = 3,
        double minScore = 0.3)
    {
        var chunks = new List<DocumentChunk>();

        using var conn = GetConnection();
        conn.Open();

        foreach (var fp in filePaths)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM chunks WHERE file_path = $fp";
            cmd.Parameters.AddWithValue("$fp", fp);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                chunks.Add(Map(reader));
        }

        return chunks
            .Select(c => (Chunk: c, Score: CosineSimilarity.Compute(queryEmbedding, c.Embedding)))
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Busca o embedding de um chunk que possua conteúdo idêntico.
    /// Usado para deduplicação: se o conteúdo já foi vetorizado antes,
    /// reutiliza o embedding existente sem chamar o Ollama novamente.
    /// Retorna null se nenhum chunk com o mesmo conteúdo existir.
    /// </summary>
    public float[]? FindEmbeddingByContent(string content)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT embedding FROM chunks WHERE content = $ct LIMIT 1";
        cmd.Parameters.AddWithValue("$ct", content);

        var result = cmd.ExecuteScalar();
        return result is byte[] bytes ? FromBytes(bytes) : null;
    }

    /// <summary>
    /// Verifica se um arquivo já foi indexado.
    /// </summary>
    public bool FileExists(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM chunks WHERE file_path = $fp";
        cmd.Parameters.AddWithValue("$fp", filePath);

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Retorna a data de modificação armazenada para um arquivo (usa o primeiro chunk).
    /// Null se o arquivo não estiver indexado.
    /// </summary>
    public DateTime? GetFileModifiedUtc(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT file_modified_utc FROM chunks WHERE file_path = $fp LIMIT 1";
        cmd.Parameters.AddWithValue("$fp", filePath);

        var result = cmd.ExecuteScalar();
        return result is string s ? DateTime.Parse(s) : null;
    }

    /// <summary>
    /// Lista distinta de caminhos de arquivo indexados.
    /// </summary>
    public List<string> GetIndexedFilePaths()
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT file_path FROM chunks ORDER BY file_path";

        using var reader = cmd.ExecuteReader();
        var paths = new List<string>();

        while (reader.Read())
            paths.Add(reader.GetString(0));

        return paths;
    }

    /// <summary>
    /// Total de chunks no banco.
    /// </summary>
    public long CountAllChunks()
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunks";

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Quantos chunks um arquivo tem.
    /// </summary>
    public int CountChunksByFile(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunks WHERE file_path = $fp";
        cmd.Parameters.AddWithValue("$fp", filePath);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Resumo: total de arquivos indexados + total de chunks.
    /// </summary>
    public (int TotalFiles, long TotalChunks) GetStats()
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                COUNT(DISTINCT file_path),
                COUNT(*)
            FROM chunks
        ";

        using var reader = cmd.ExecuteReader();
        reader.Read();

        return (reader.GetInt32(0), reader.GetInt64(1));
    }

    /// <summary>
    /// Apaga todos os dados do banco (truncate).
    /// </summary>
    public void ClearAll()
    {
        using var conn = GetConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks";
        cmd.ExecuteNonQuery();
    }

    private static DocumentChunk Map(SqliteDataReader reader)
    {
        return new DocumentChunk
        {
            Id              = reader.GetString(0),
            FilePath        = reader.GetString(1),
            ChunkIndex      = reader.GetInt32(2),
            Content         = reader.GetString(3),
            Embedding       = FromBytes((byte[])reader.GetValue(4)),
            FileModifiedUtc = reader.GetDateTime(5), 
            CreatedAt       = reader.GetDateTime(6)
        };
    }

    private static List<DocumentChunk> ReadAll(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<DocumentChunk>(256); 

        while (reader.Read())
        {
            list.Add(Map(reader));
        }

        return list;
    }

    private static void AddChunkParams(SqliteCommand cmd, DocumentChunk chunk)
    {
        cmd.Parameters.AddWithValue("$id",  chunk.Id);
        cmd.Parameters.AddWithValue("$fp",  chunk.FilePath);
        cmd.Parameters.AddWithValue("$ci",  chunk.ChunkIndex);
        cmd.Parameters.AddWithValue("$ct",  chunk.Content);
        cmd.Parameters.AddWithValue("$emb", ToBytes(chunk.Embedding));
        cmd.Parameters.AddWithValue("$fmu", chunk.FileModifiedUtc.ToString("o"));
    }

    private static byte[] ToBytes(float[] vec)
        => MemoryMarshal.AsBytes(vec.AsSpan()).ToArray();

    private static float[] FromBytes(byte[] data)
        => MemoryMarshal.Cast<byte, float>(data).ToArray();
}