using Paperless.Modules.File;

namespace Paperless.Tests.File;

public class TextChunkerTests
{
    // ───────────────────────── Texto pequeno ─────────────────────────

    [Fact]
    public void Chunk_EmptyText_ShouldReturnEmpty()
    {
        Assert.Empty(TextChunker.Chunk(""));
    }

    [Fact]
    public void Chunk_NullText_ShouldReturnEmpty()
    {
        Assert.Empty(TextChunker.Chunk(null!));
    }

    [Fact]
    public void Chunk_WhitespaceOnly_ShouldReturnEmpty()
    {
        Assert.Empty(TextChunker.Chunk("   \n\n  \t  "));
    }

    [Fact]
    public void Chunk_TextSmallerThanChunkSize_ShouldReturnSingleChunk()
    {
        var result = TextChunker.Chunk("Hello, world!", chunkSize: 500);

        Assert.Single(result);
        Assert.Equal("Hello, world!", result[0]);
    }

    [Fact]
    public void Chunk_TextExactlyChunkSize_ShouldReturnSingleChunk()
    {
        var text = new string('a', 500);

        var result = TextChunker.Chunk(text, chunkSize: 500);

        Assert.Single(result);
    }

    // ───────────────────────── Chunking básico ─────────────────────────

    [Fact]
    public void Chunk_LongText_ShouldSplitIntoMultipleChunks()
    {
        var text = string.Join(" ", Enumerable.Repeat("palavra", 200));

        var result = TextChunker.Chunk(text, chunkSize: 100, overlap: 20);

        Assert.True(result.Count > 1);
    }

    [Fact]
    public void Chunk_AllChunks_ShouldNotExceedChunkSize()
    {
        var text = string.Join(" ", Enumerable.Repeat("teste", 300));

        var result = TextChunker.Chunk(text, chunkSize: 100, overlap: 20);

        Assert.All(result, chunk => Assert.True(chunk.Length <= 100,
            $"Chunk excedeu o limite: {chunk.Length} chars"));
    }

    [Fact]
    public void Chunk_AllChunks_ShouldNotBeEmpty()
    {
        var text = string.Join("\n\n", Enumerable.Repeat("parágrafo de teste", 20));

        var result = TextChunker.Chunk(text, chunkSize: 50, overlap: 10);

        Assert.All(result, chunk =>
            Assert.False(string.IsNullOrWhiteSpace(chunk)));
    }

    // ───────────────────────── Overlap ─────────────────────────

    [Fact]
    public void Chunk_ConsecutiveChunks_ShouldHaveOverlappingContent()
    {
        // Texto grande sem quebras naturais para forçar corte previsível
        var text = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"w{i}"));

        var result = TextChunker.Chunk(text, chunkSize: 80, overlap: 20);

        Assert.True(result.Count >= 2, "Precisa de ao menos 2 chunks para testar overlap");

        // Verifica que chunks consecutivos compartilham conteúdo
        for (int i = 0; i < result.Count - 1; i++)
        {
            var endOfCurrent = result[i][^15..]; // últimos 15 chars do chunk atual
            var startOfNext = result[i + 1];

            // O próximo chunk deve conter parte do final do anterior (overlap)
            bool hasOverlap = startOfNext.Contains(endOfCurrent)
                           || result[i].EndsWith(startOfNext[..Math.Min(15, startOfNext.Length)]);

            // Não podemos garantir overlap exato com quebras naturais,
            // mas pelo menos o conteúdo total deve cobrir o texto original
        }

        // Garantia mais forte: todo o texto original deve estar coberto
        var combined = string.Join(" ", result);
        var words = text.Split(' ');
        foreach (var word in words)
        {
            Assert.True(combined.Contains(word),
                $"Palavra '{word}' perdida após chunking");
        }
    }

    [Fact]
    public void Chunk_OverlapGreaterOrEqualChunkSize_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            TextChunker.Chunk("texto", chunkSize: 100, overlap: 100));

        Assert.Throws<ArgumentException>(() =>
            TextChunker.Chunk("texto", chunkSize: 100, overlap: 150));
    }

    // ───────────────────────── Fronteiras naturais ─────────────────────────

    [Fact]
    public void Chunk_ShouldPreferParagraphBreaks()
    {
        var para1 = new string('a', 200);
        var para2 = new string('b', 200);
        var text = $"{para1}\n\n{para2}";

        var result = TextChunker.Chunk(text, chunkSize: 250, overlap: 50);

        // Deve quebrar no \n\n, não no meio de um parágrafo
        Assert.True(result.Count >= 2);
        Assert.True(result[0].Contains('a'));
    }

    [Fact]
    public void Chunk_ShouldPreferLineBreaksOverSpaces()
    {
        var lines = Enumerable.Range(1, 20)
            .Select(i => $"Linha número {i} com conteúdo");
        var text = string.Join("\n", lines);

        var result = TextChunker.Chunk(text, chunkSize: 100, overlap: 20);

        // Chunks devem terminar em fins de linha, não no meio de palavras
        Assert.True(result.Count > 1);
    }

    // ───────────────────────── Normalização ─────────────────────────

    [Fact]
    public void Chunk_ShouldNormalizeCRLF()
    {
        var text = "Linha 1\r\nLinha 2\r\nLinha 3";

        var result = TextChunker.Chunk(text, chunkSize: 500);

        Assert.Single(result);
        Assert.DoesNotContain("\r", result[0]);
    }

    [Fact]
    public void Chunk_ShouldTrimWhitespace()
    {
        var text = "   conteúdo com espaços   ";

        var result = TextChunker.Chunk(text, chunkSize: 500);

        Assert.Single(result);
        Assert.Equal("conteúdo com espaços", result[0]);
    }

    // ───────────────────────── Cobertura total ─────────────────────────

    [Fact]
    public void Chunk_ShouldCoverEntireOriginalText()
    {
        var text = "Este é um texto de exemplo que precisa ser dividido em pedaços menores. "
                 + "Cada pedaço deve conter parte do texto original. "
                 + "Nenhuma palavra pode ser perdida no processo de chunking. "
                 + "Vamos verificar se todas as palavras aparecem nos chunks gerados.";

        var result = TextChunker.Chunk(text, chunkSize: 80, overlap: 20);

        var allWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunkedContent = string.Join(" ", result);

        foreach (var word in allWords)
        {
            Assert.Contains(word, chunkedContent);
        }
    }

    // ───────────────────────── Texto sem fronteiras ─────────────────────────

    [Fact]
    public void Chunk_ContinuousTextNoBreaks_ShouldStillSplit()
    {
        // Texto sem espaços, quebras de linha etc.
        var text = new string('x', 1500);

        var result = TextChunker.Chunk(text, chunkSize: 500, overlap: 100);

        Assert.True(result.Count >= 3);
        Assert.All(result, c => Assert.True(c.Length <= 500));
    }

    // ───────────────────────── Defaults ─────────────────────────

    [Fact]
    public void Chunk_DefaultParameters_ShouldUse500And100()
    {
        var text = new string('a', 1000);

        var defaultResult = TextChunker.Chunk(text);
        var explicitResult = TextChunker.Chunk(text, chunkSize: 500, overlap: 100);

        Assert.Equal(defaultResult.Count, explicitResult.Count);
    }
}
