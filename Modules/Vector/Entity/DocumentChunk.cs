namespace Paperless.Modules.Vector.Entity;

public class DocumentChunk
{
    public string Id { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = default!;
    public float[] Embedding { get; set; } = default!;
    public DateTime FileModifiedUtc { get; set; }
    public DateTime CreatedAt { get; set; }

    public static string BuildId(string filePath, int chunkIndex)
        => $"{filePath}:{chunkIndex}";
}