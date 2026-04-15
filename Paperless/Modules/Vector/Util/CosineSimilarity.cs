namespace Paperless.Modules.Vector.Util;

public static class CosineSimilarity
{
    public static double Compute(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException(
                $"Vetores com dimensões diferentes: a={a.Length}, b={b.Length}");

        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);

        return denominator < 1e-10 ? 0.0 : dot / denominator;
    }

    public static List<(int Index, double Score)> Rank(
        float[] query,
        IList<float[]> candidates,
        int topK = 3,
        double minScore = 0.3)
    {
        return candidates
            .Select((emb, idx) => (Index: idx, Score: Compute(query, emb)))
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }
}