using System.Text;

namespace Paperless.Modules.File;

/// <summary>
/// Divide textos em chunks menores para vetorização.
/// Respeita fronteiras naturais (parágrafo → linha → espaço)
/// e aplica overlap entre chunks consecutivos para preservar contexto.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Divide o texto em chunks com tamanho alvo e overlap.
    /// Arquivos menores que chunkSize resultam em chunk único.
    /// </summary>
    public static List<string> Chunk(string text, int chunkSize = 500, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (overlap >= chunkSize)
            throw new ArgumentException(
                $"Overlap ({overlap}) must be less than chunkSize ({chunkSize}).");

        text = NormalizeWhitespace(text);

        if (text.Length <= chunkSize)
            return [text];

        var chunks = new List<string>();
        int position = 0;

        while (position < text.Length)
        {
            int remaining = text.Length - position;

            /* Último pedaço — cabe inteiro */
            if (remaining <= chunkSize)
            {
                AddIfNotEmpty(chunks, text[position..]);
                break;
            }

            int idealEnd = position + chunkSize;
            int breakAt = FindBreakPoint(text, position, idealEnd);

            AddIfNotEmpty(chunks, text[position..breakAt]);

            /* Avança com overlap; garante progresso mínimo */
            int next = breakAt - overlap;
            position = next > position ? next : breakAt;
        }

        return chunks;
    }

    /// <summary>
    /// Procura o melhor ponto de quebra perto do final ideal do chunk.
    /// Prioridade: parágrafo (\n\n) > linha (\n) > espaço > corte bruto.
    /// </summary>
    private static int FindBreakPoint(string text, int start, int idealEnd)
    {
        if (idealEnd >= text.Length)
            return text.Length;

        int searchFrom = idealEnd - 1;
        int searchLen = idealEnd - start;

        /* Parágrafo */
        int idx = text.LastIndexOf("\n\n", searchFrom, searchLen);
        if (idx > start)
            return idx + 2;

        /* Linha */
        idx = text.LastIndexOf('\n', searchFrom, searchLen);
        if (idx > start)
            return idx + 1;

        /* Espaço */
        idx = text.LastIndexOf(' ', searchFrom, searchLen);
        if (idx > start)
            return idx + 1;

        /* Sem fronteira natural — corta no limite */
        return idealEnd;
    }

    /// <summary>
    /// Normaliza espaços em branco excessivos sem destruir a estrutura.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }

    private static void AddIfNotEmpty(List<string> chunks, string text)
    {
        var trimmed = text.Trim();

        if (!string.IsNullOrWhiteSpace(trimmed))
            chunks.Add(trimmed);
    }
}
