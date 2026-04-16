namespace Paperless.Modules.Vector.Model;

public interface IFileRagModel
{
    List<(DocumentChunk Chunk, double Score)> SearchSimilar(
        float[] embedding, int topK, double minScore);
}