using Paperless.Modules.File;

namespace Paperless.Tests.File;

public class FileIndexerOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveCorrectValues()
    {
        var opts = new FileIndexerOptions();

        Assert.Equal(500, opts.ChunkSize);
        Assert.Equal(100, opts.Overlap);
        Assert.Equal(20_000, opts.DebounceMs);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".json")]
    [InlineData(".cs")]
    [InlineData(".py")]
    [InlineData(".csv")]
    [InlineData(".log")]
    [InlineData(".xml")]
    [InlineData(".yaml")]
    [InlineData(".sql")]
    [InlineData(".sh")]
    public void IsSupported_TextExtensions_ShouldReturnTrue(string ext)
    {
        var opts = new FileIndexerOptions();

        Assert.True(opts.IsSupported($"arquivo{ext}"));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".mp4")]
    [InlineData(".mp3")]
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".zip")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    public void IsSupported_BinaryExtensions_ShouldReturnFalse(string ext)
    {
        var opts = new FileIndexerOptions();

        Assert.False(opts.IsSupported($"arquivo{ext}"));
    }

    [Fact]
    public void IsSupported_ShouldBeCaseInsensitive()
    {
        var opts = new FileIndexerOptions();

        Assert.True(opts.IsSupported("README.MD"));
        Assert.True(opts.IsSupported("script.PY"));
        Assert.True(opts.IsSupported("data.JSON"));
    }

    [Fact]
    public void IsSupported_FullPath_ShouldWork()
    {
        var opts = new FileIndexerOptions();

        Assert.True(opts.IsSupported("/home/user/projeto/src/main.cs"));
        Assert.False(opts.IsSupported("/home/user/fotos/img.png"));
    }

    [Fact]
    public void IsSupported_NoExtension_ShouldReturnFalse()
    {
        var opts = new FileIndexerOptions();

        Assert.False(opts.IsSupported("Makefile"));
    }

    [Fact]
    public void SupportedExtensions_ShouldBeMutable()
    {
        var opts = new FileIndexerOptions();
        opts.SupportedExtensions.Add(".custom");

        Assert.True(opts.IsSupported("file.custom"));
    }
}
