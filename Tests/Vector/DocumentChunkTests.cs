using Paperless.Modules.Vector.Entity;

namespace Paperless.Tests.Vector;

public class DocumentChunkTests
{
    [Fact]
    public void BuildId_ShouldCombineFilePathAndIndex()
    {
        var id = DocumentChunk.BuildId("docs/readme.md", 3);

        Assert.Equal("docs/readme.md:3", id);
    }

    [Fact]
    public void BuildId_WithZeroIndex_ShouldWork()
    {
        var id = DocumentChunk.BuildId("file.txt", 0);

        Assert.Equal("file.txt:0", id);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var chunk = new DocumentChunk
        {
            Id = "test:0",
            FilePath = "test.txt",
            ChunkIndex = 0,
            Content = "Conteúdo do chunk",
            Embedding = [0.1f, 0.2f],
            FileModifiedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        };

        Assert.Equal("test:0", chunk.Id);
        Assert.Equal("test.txt", chunk.FilePath);
        Assert.Equal(0, chunk.ChunkIndex);
        Assert.Equal("Conteúdo do chunk", chunk.Content);
        Assert.Equal(2, chunk.Embedding.Length);
    }
}
