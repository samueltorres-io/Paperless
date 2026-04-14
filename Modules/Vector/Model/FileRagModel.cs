using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using Paperless.Configuration;

namespace Paperless.Modules.Vector.Model;

public class FileRagModel : DataContext
{
    public FileRagModel() : base() { }

    protected override string GetSchema() => @"
        CREATE TABLE IF NOT EXISTS chunks (
            id TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            chunk_index INTEGER NOT NULL,
            content TEXT NOT NULL,
            embedding BLOB NOT NULL,
            file_modified_utc TEXT NOT NULL,
            created_at TEXT DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_chunks_filepath ON chunks(file_path);
    ";

    public void UpsertChunk(DocumentChunk chunk)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO chunks 
            (id, file_path, chunk_index, content, embedding, file_modified_utc)
            VALUES ($id, $file_path, $chunk_index, $content, $embedding, $file_modified_utc);
        ";

        cmd.Parameters.AddWithValue("$id", chunk.Id);
        cmd.Parameters.AddWithValue("$file_path", chunk.FilePath);
        cmd.Parameters.AddWithValue("$chunk_index", chunk.ChunkIndex);
        cmd.Parameters.AddWithValue("$content", chunk.Content);
        cmd.Parameters.AddWithValue("$embedding", ToBytes(chunk.Embedding));
        cmd.Parameters.AddWithValue("$file_modified_utc", chunk.FileModifiedUtc.ToString("o"));

        cmd.ExecuteNonQuery();
    }

    public List<DocumentChunk> GetChunksByFile(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM chunks WHERE file_path = $file_path";
        cmd.Parameters.AddWithValue("$file_path", filePath);

        using var reader = cmd.ExecuteReader();

        var result = new List<DocumentChunk>();

        while (reader.Read())
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public void DeleteByFilePath(string filePath)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks WHERE file_path = $file_path";
        cmd.Parameters.AddWithValue("$file_path", filePath);

        cmd.ExecuteNonQuery();
    }

    public List<DocumentChunk> GetAllChunks()
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM chunks";

        using var reader = cmd.ExecuteReader();

        var list = new List<DocumentChunk>();

        while (reader.Read())
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public List<(DocumentChunk chunk, double score)> SearchSimilar(float[] query, int topK = 3)
    {
        var allChunks = GetAllChunks();

        return allChunks
            .Select(c => (chunk: c, score: CosineSimilarity(query, c.Embedding)))
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Where(x => x.score > 0.3)
            .ToList();
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-10);
    }

    private DocumentChunk Map(SqliteDataReader reader)
    {
        return new DocumentChunk
        {
            Id = reader.GetString(0),
            FilePath = reader.GetString(1),
            ChunkIndex = reader.GetInt32(2),
            Content = reader.GetString(3),
            Embedding = FromBytes((byte[])reader["embedding"]),
            FileModifiedUtc = DateTime.Parse(reader.GetString(5)),
            CreatedAt = DateTime.Parse(reader.GetString(6))
        };
    }

    private byte[] ToBytes(float[] vec) => MemoryMarshal.AsBytes(vec.AsSpan()).ToArray();

    private float[] FromBytes(byte[] data) => MemoryMarshal.Cast<byte, float>(data).ToArray();
}