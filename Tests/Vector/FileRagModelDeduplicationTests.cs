using Xunit;
using Paperless.Modules.Vector.Entity;
using Paperless.Modules.Vector.Model;

namespace Paperless.Tests.Vector;

public class FileRagModelDeduplicationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FileRagModel _model;

    public FileRagModelDeduplicationTests()
    {
        _dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dedup_test_{Guid.NewGuid():N}.db");
        _model = new FileRagModel(_dbPath);
    }

    public void Dispose()
    {
        try { System.IO.File.Delete(_dbPath); }
        catch { /* cleanup */ }
    }

    private static DocumentChunk MakeChunk(
        string filePath, int index, string content, float[] embedding)
    {
        return new DocumentChunk
        {
            Id = DocumentChunk.BuildId(filePath, index),
            FilePath = filePath,
            ChunkIndex = index,
            Content = content,
            Embedding = embedding,
            FileModifiedUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public void FindEmbeddingByContent_WhenContentExists_ShouldReturnEmbedding()
    {
        float[] expectedEmb = new float[] { 0.1f, 0.2f, 0.3f };
        _model.UpsertChunk(MakeChunk("a.txt", 0, "conteúdo duplicado", expectedEmb));

        var result = _model.FindEmbeddingByContent("conteúdo duplicado");

        Assert.NotNull(result);
        Assert.Equal(expectedEmb.Length, result!.Length);
        for (int i = 0; i < expectedEmb.Length; i++)
            Assert.Equal(expectedEmb[i], result[i], precision: 5);
    }

    [Fact]
    public void FindEmbeddingByContent_WhenContentNotExists_ShouldReturnNull()
    {
        Assert.Null(_model.FindEmbeddingByContent("inexistente"));
    }

    [Fact]
    public void FindEmbeddingByContent_WithEmptyDb_ShouldReturnNull()
    {
        Assert.Null(_model.FindEmbeddingByContent("qualquer coisa"));
    }

    [Fact]
    public void FindEmbeddingByContent_ShouldMatchExactContent()
    {
        float[] emb = new float[] { 1f, 2f };
        _model.UpsertChunk(MakeChunk("a.txt", 0, "texto exato", emb));

        /* Conteúdo parecido mas diferente */
        Assert.Null(_model.FindEmbeddingByContent("texto exat"));
        Assert.Null(_model.FindEmbeddingByContent("texto exato "));
        Assert.Null(_model.FindEmbeddingByContent("Texto exato"));

        /* Conteúdo idêntico */
        Assert.NotNull(_model.FindEmbeddingByContent("texto exato"));
    }

    [Fact]
    public void FindEmbeddingByContent_DuplicateInMultipleFiles_ShouldReturnOne()
    {
        float[] emb1 = new float[] { 1f, 0f };
        float[] emb2 = new float[] { 0f, 1f };

        _model.UpsertChunk(MakeChunk("a.txt", 0, "mesmo conteúdo", emb1));
        _model.UpsertChunk(MakeChunk("b.txt", 0, "mesmo conteúdo", emb2));

        var result = _model.FindEmbeddingByContent("mesmo conteúdo");

        /* Deve retornar um dos dois (LIMIT 1) */
        Assert.NotNull(result);
        Assert.Equal(2, result!.Length);
    }
}
