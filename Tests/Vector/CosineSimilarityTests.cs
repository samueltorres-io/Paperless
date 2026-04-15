using Paperless.Modules.Vector.Util;

namespace Paperless.Tests.Vector;

public class CosineSimilarityTests
{

    [Fact]
    public void Compute_IdenticalVectors_ShouldReturn1()
    {
        float[] v = [1f, 2f, 3f];

        var score = CosineSimilarity.Compute(v, v);

        Assert.Equal(1.0, score, precision: 6);
    }

    [Fact]
    public void Compute_OppositeVectors_ShouldReturnNegative1()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [-1f, 0f, 0f];

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(-1.0, score, precision: 6);
    }

    [Fact]
    public void Compute_OrthogonalVectors_ShouldReturn0()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(0.0, score, precision: 6);
    }

    [Fact]
    public void Compute_ScaledVectors_ShouldReturnSameAsOriginal()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [2f, 4f, 6f]; // 2 * a

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(1.0, score, precision: 5);
    }

    [Fact]
    public void Compute_ZeroVector_ShouldReturn0()
    {
        float[] a = [1f, 2f, 3f];
        float[] zero = [0f, 0f, 0f];

        var score = CosineSimilarity.Compute(a, zero);

        Assert.Equal(0.0, score, precision: 6);
    }

    [Fact]
    public void Compute_DifferentDimensions_ShouldThrowArgumentException()
    {
        float[] a = [1f, 2f];
        float[] b = [1f, 2f, 3f];

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Compute(a, b));
    }

    [Fact]
    public void Compute_KnownVectors_ShouldReturnExpectedValue()
    {
        // cos([1,0], [1,1]) = 1 / sqrt(2) ≈ 0.7071
        float[] a = [1f, 0f];
        float[] b = [1f, 1f];

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(1.0 / Math.Sqrt(2.0), score, precision: 4);
    }

    [Fact]
    public void Compute_SingleDimension_ShouldWork()
    {
        float[] a = [5f];
        float[] b = [3f];

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(1.0, score, precision: 6);
    }

    [Fact]
    public void Compute_NegativeValues_ShouldWork()
    {
        float[] a = [-1f, -2f, -3f];
        float[] b = [-1f, -2f, -3f];

        var score = CosineSimilarity.Compute(a, b);

        Assert.Equal(1.0, score, precision: 6);
    }

    [Fact]
    public void Rank_ShouldReturnTopKResults()
    {
        float[] query = [1f, 0f, 0f];
        var candidates = new List<float[]>
        {
            [1f, 0f, 0f],   // score = 1.0
            [0f, 1f, 0f],   // score = 0.0
            [0.7f, 0.7f, 0f], // score ≈ 0.71
            [0.9f, 0.1f, 0f], // score ≈ 0.99
        };

        var results = CosineSimilarity.Rank(query, candidates, topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].Index);
        Assert.Equal(3, results[1].Index);
    }

    [Fact]
    public void Rank_ShouldFilterBelowMinScore()
    {
        float[] query = [1f, 0f];
        var candidates = new List<float[]>
        {
            [1f, 0f],   // score = 1.0
            [0f, 1f],   // score = 0.0
        };

        var results = CosineSimilarity.Rank(query, candidates, topK: 10, minScore: 0.5);

        Assert.Single(results);
        Assert.Equal(0, results[0].Index);
    }

    [Fact]
    public void Rank_WithEmptyCandidates_ShouldReturnEmpty()
    {
        float[] query = [1f, 0f];
        var candidates = new List<float[]>();

        var results = CosineSimilarity.Rank(query, candidates);

        Assert.Empty(results);
    }

    [Fact]
    public void Rank_ShouldOrderByScoreDescending()
    {
        float[] query = [1f, 0f, 0f];
        var candidates = new List<float[]>
        {
            [0.5f, 0.5f, 0f],
            [0.9f, 0.1f, 0f],
            [0.7f, 0.3f, 0f],
        };

        var results = CosineSimilarity.Rank(query, candidates, topK: 3, minScore: 0.0);

        Assert.True(results[0].Score >= results[1].Score);
        Assert.True(results[1].Score >= results[2].Score);
    }

    [Fact]
    public void Rank_DefaultTopK_ShouldBe3()
    {
        float[] query = [1f, 0f];
        var candidates = Enumerable.Range(0, 10)
            .Select(i => new float[] { 1f, i * 0.01f })
            .ToList();

        var results = CosineSimilarity.Rank(query, candidates, minScore: 0.0);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Rank_AllBelowMinScore_ShouldReturnEmpty()
    {
        float[] query = [1f, 0f];
        var candidates = new List<float[]> { [0f, 1f] };

        var results = CosineSimilarity.Rank(query, candidates, minScore: 0.5);

        Assert.Empty(results);
    }
}
