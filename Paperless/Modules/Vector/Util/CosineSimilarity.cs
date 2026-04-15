namespace Paperless.Modules.Vector.Util;

public static class CosineSimilarity
{
    public static double Compute(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Dimensões incompatíveis: {a.Length} vs {b.Length}");

        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            float valA = a[i];
            float valB = b[i];
            dot   += (double)valA * valB;
            normA += (double)valA * valA;
            normB += (double)valB * valB;
        }

        double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);

        if (denominator <= 0.0) return 0.0;

        double result = dot / denominator;

        return Math.Clamp(result, -1.0, 1.0);
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