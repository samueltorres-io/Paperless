using Xunit;
using Paperless.Modules.Vector.Entity;
using Paperless.Modules.Vector.Model;

namespace Paperless.Tests.Vector;

public class FileRagModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FileRagModel _model;

    public FileRagModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"paperless_test_{Guid.NewGuid():N}.db");
        _model = new FileRagModel(_dbPath);
    }

    public void Dispose()
    {
        try { System.IO.File.Delete(_dbPath); }
        catch { /* cleanup */ }
    }

    private static DocumentChunk MakeChunk(
        string filePath = "docs/test.txt",
        int index = 0,
        string content = "chunk content",
        float[]? embedding = null)
    {
        embedding ??= [0.1f, 0.2f, 0.3f];
        return new DocumentChunk
        {
            Id = DocumentChunk.BuildId(filePath, index),
            FilePath = filePath,
            ChunkIndex = index,
            Content = content,
            Embedding = embedding,
            FileModifiedUtc = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public void Constructor_ShouldCreateDatabaseFile()
    {
        Assert.True(System.IO.File.Exists(_dbPath));
    }

    [Fact]
    public void Constructor_ShouldCreateSchema()
    {
        var count = _model.CountAllChunks();
        Assert.Equal(0, count);
    }

    [Fact]
    public void UpsertChunk_ShouldInsertNewChunk()
    {
        var chunk = MakeChunk();

        _model.UpsertChunk(chunk);

        var loaded = _model.GetChunkById(chunk.Id);
        Assert.NotNull(loaded);
        Assert.Equal(chunk.Content, loaded!.Content);
    }

    [Fact]
    public void UpsertChunk_ShouldUpdateExistingChunk()
    {
        var chunk = MakeChunk(content: "v1");
        _model.UpsertChunk(chunk);

        chunk.Content = "v2";
        _model.UpsertChunk(chunk);

        var loaded = _model.GetChunkById(chunk.Id);
        Assert.Equal("v2", loaded!.Content);
        Assert.Equal(1, _model.CountAllChunks());
    }

    [Fact]
    public void UpsertChunk_ShouldPreserveEmbedding()
    {
        float[] emb = [1.5f, -2.3f, 0.0f, 4.1f];
        var chunk = MakeChunk(embedding: emb);

        _model.UpsertChunk(chunk);

        var loaded = _model.GetChunkById(chunk.Id);
        Assert.Equal(emb.Length, loaded!.Embedding.Length);
        for (int i = 0; i < emb.Length; i++)
            Assert.Equal(emb[i], loaded.Embedding[i], precision: 5);
    }

    [Fact]
    public void GetChunkById_WithNonexistentId_ShouldReturnNull()
    {
        Assert.Null(_model.GetChunkById("nao:existe"));
    }

    [Fact]
    public void GetChunksByFile_ShouldReturnOnlyMatchingFile()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("a.txt", 1));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        var chunks = _model.GetChunksByFile("a.txt");

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.Equal("a.txt", c.FilePath));
    }

    [Fact]
    public void GetChunksByFile_ShouldOrderByChunkIndex()
    {
        _model.UpsertChunk(MakeChunk("f.txt", 2));
        _model.UpsertChunk(MakeChunk("f.txt", 0));
        _model.UpsertChunk(MakeChunk("f.txt", 1));

        var chunks = _model.GetChunksByFile("f.txt");

        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[1].ChunkIndex);
        Assert.Equal(2, chunks[2].ChunkIndex);
    }

    [Fact]
    public void GetChunksByFile_WithNonexistentFile_ShouldReturnEmpty()
    {
        Assert.Empty(_model.GetChunksByFile("inexistente.txt"));
    }

    [Fact]
    public void DeleteByFilePath_ShouldRemoveAllChunksOfFile()
    {
        _model.UpsertChunk(MakeChunk("target.txt", 0));
        _model.UpsertChunk(MakeChunk("target.txt", 1));
        _model.UpsertChunk(MakeChunk("keep.txt", 0));

        int deleted = _model.DeleteByFilePath("target.txt");

        Assert.Equal(2, deleted);
        Assert.Empty(_model.GetChunksByFile("target.txt"));
        Assert.Single(_model.GetChunksByFile("keep.txt"));
    }

    [Fact]
    public void DeleteByFilePath_WithNonexistentFile_ShouldReturn0()
    {
        Assert.Equal(0, _model.DeleteByFilePath("nope.txt"));
    }

    [Fact]
    public void DeleteById_ShouldRemoveSpecificChunk()
    {
        var chunk = MakeChunk();
        _model.UpsertChunk(chunk);

        Assert.True(_model.DeleteById(chunk.Id));
        Assert.Null(_model.GetChunkById(chunk.Id));
    }

    [Fact]
    public void DeleteById_WithNonexistentId_ShouldReturnFalse()
    {
        Assert.False(_model.DeleteById("fake:99"));
    }

    [Fact]
    public void UpsertChunks_ShouldInsertMultipleInTransaction()
    {
        var chunks = Enumerable.Range(0, 5)
            .Select(i => MakeChunk("batch.txt", i, $"chunk {i}"))
            .ToList();

        _model.UpsertChunks(chunks);

        Assert.Equal(5, _model.CountChunksByFile("batch.txt"));
    }

    [Fact]
    public void UpsertChunks_WithEmptyList_ShouldNotThrow()
    {
        _model.UpsertChunks([]);

        Assert.Equal(0, _model.CountAllChunks());
    }

    [Fact]
    public void ReplaceFileChunks_ShouldDeleteOldAndInsertNew()
    {
        _model.UpsertChunk(MakeChunk("replace.txt", 0, "antigo 0"));
        _model.UpsertChunk(MakeChunk("replace.txt", 1, "antigo 1"));

        var newChunks = new[]
        {
            MakeChunk("replace.txt", 0, "novo 0"),
            MakeChunk("replace.txt", 1, "novo 1"),
            MakeChunk("replace.txt", 2, "novo 2"),
        };

        _model.ReplaceFileChunks("replace.txt", newChunks);

        var chunks = _model.GetChunksByFile("replace.txt");
        Assert.Equal(3, chunks.Count);
        Assert.Equal("novo 0", chunks[0].Content);
    }

    [Fact]
    public void ReplaceFileChunks_ShouldNotAffectOtherFiles()
    {
        _model.UpsertChunk(MakeChunk("other.txt", 0, "intocado"));
        _model.UpsertChunk(MakeChunk("replace.txt", 0, "antigo"));

        _model.ReplaceFileChunks("replace.txt", [MakeChunk("replace.txt", 0, "novo")]);

        var other = _model.GetChunksByFile("other.txt");
        Assert.Single(other);
        Assert.Equal("intocado", other[0].Content);
    }

    [Fact]
    public void GetAllChunks_ShouldReturnEverything()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        var all = _model.GetAllChunks();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void SearchSimilar_ShouldReturnMostSimilarChunks()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0, "similar", [1f, 0f, 0f]));
        _model.UpsertChunk(MakeChunk("b.txt", 0, "diferente", [0f, 1f, 0f]));
        _model.UpsertChunk(MakeChunk("c.txt", 0, "meio", [0.7f, 0.7f, 0f]));

        float[] query = [1f, 0f, 0f];
        var results = _model.SearchSimilar(query, topK: 2, minScore: 0.0);

        Assert.Equal(2, results.Count);
        Assert.Equal("a.txt", results[0].Chunk.FilePath);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void SearchSimilar_ShouldFilterBelowMinScore()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0, "match", [1f, 0f, 0f]));
        _model.UpsertChunk(MakeChunk("b.txt", 0, "ortogonal", [0f, 1f, 0f]));

        float[] query = [1f, 0f, 0f];
        var results = _model.SearchSimilar(query, topK: 10, minScore: 0.5);

        Assert.Single(results);
        Assert.Equal("a.txt", results[0].Chunk.FilePath);
    }

    [Fact]
    public void SearchSimilar_WhenEmpty_ShouldReturnEmpty()
    {
        var results = _model.SearchSimilar([1f, 0f], topK: 5);

        Assert.Empty(results);
    }

    [Fact]
    public void SearchSimilarInFiles_ShouldOnlySearchSpecifiedFiles()
    {
        _model.UpsertChunk(MakeChunk("include.txt", 0, "sim", [1f, 0f, 0f]));
        _model.UpsertChunk(MakeChunk("exclude.txt", 0, "sim", [1f, 0f, 0f]));

        float[] query = [1f, 0f, 0f];
        var results = _model.SearchSimilarInFiles(query, ["include.txt"], topK: 10, minScore: 0.0);

        Assert.Single(results);
        Assert.Equal("include.txt", results[0].Chunk.FilePath);
    }

    [Fact]
    public void FileExists_WhenIndexed_ShouldReturnTrue()
    {
        _model.UpsertChunk(MakeChunk("indexed.txt", 0));

        Assert.True(_model.FileExists("indexed.txt"));
    }

    [Fact]
    public void FileExists_WhenNotIndexed_ShouldReturnFalse()
    {
        Assert.False(_model.FileExists("missing.txt"));
    }

    [Fact]
    public void GetFileModifiedUtc_WhenIndexed_ShouldReturnDate()
    {
        var chunk = MakeChunk();
        _model.UpsertChunk(chunk);

        var date = _model.GetFileModifiedUtc(chunk.FilePath);

        Assert.NotNull(date);
        Assert.Equal(chunk.FileModifiedUtc.Date, date!.Value.Date);
    }

    [Fact]
    public void GetFileModifiedUtc_WhenNotIndexed_ShouldReturnNull()
    {
        Assert.Null(_model.GetFileModifiedUtc("missing.txt"));
    }

    [Fact]
    public void GetIndexedFilePaths_ShouldReturnDistinctPaths()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("a.txt", 1));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        var paths = _model.GetIndexedFilePaths();

        Assert.Equal(2, paths.Count);
        Assert.Contains("a.txt", paths);
        Assert.Contains("b.txt", paths);
    }

    [Fact]
    public void GetIndexedFilePaths_ShouldBeOrderedAlphabetically()
    {
        _model.UpsertChunk(MakeChunk("z.txt", 0));
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("m.txt", 0));

        var paths = _model.GetIndexedFilePaths();

        Assert.Equal("a.txt", paths[0]);
        Assert.Equal("m.txt", paths[1]);
        Assert.Equal("z.txt", paths[2]);
    }

    [Fact]
    public void CountAllChunks_ShouldReturnCorrectCount()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("a.txt", 1));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        Assert.Equal(3, _model.CountAllChunks());
    }

    [Fact]
    public void CountChunksByFile_ShouldReturnCorrectCount()
    {
        _model.UpsertChunk(MakeChunk("target.txt", 0));
        _model.UpsertChunk(MakeChunk("target.txt", 1));
        _model.UpsertChunk(MakeChunk("other.txt", 0));

        Assert.Equal(2, _model.CountChunksByFile("target.txt"));
        Assert.Equal(1, _model.CountChunksByFile("other.txt"));
        Assert.Equal(0, _model.CountChunksByFile("inexistente.txt"));
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectTotals()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("a.txt", 1));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        var (totalFiles, totalChunks) = _model.GetStats();

        Assert.Equal(2, totalFiles);
        Assert.Equal(3, totalChunks);
    }

    [Fact]
    public void GetStats_WhenEmpty_ShouldReturnZeros()
    {
        var (totalFiles, totalChunks) = _model.GetStats();

        Assert.Equal(0, totalFiles);
        Assert.Equal(0, totalChunks);
    }

    [Fact]
    public void ClearAll_ShouldRemoveAllData()
    {
        _model.UpsertChunk(MakeChunk("a.txt", 0));
        _model.UpsertChunk(MakeChunk("b.txt", 0));

        _model.ClearAll();

        Assert.Equal(0, _model.CountAllChunks());
        Assert.Empty(_model.GetIndexedFilePaths());
    }
}
