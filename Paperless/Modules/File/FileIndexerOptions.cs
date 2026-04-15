namespace Paperless.Modules.File;

/// <summary>
/// Configurações do FileIndexer.
/// </summary>
public sealed class FileIndexerOptions
{
    /// <summary>Tamanho alvo de cada chunk em caracteres.</summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>Sobreposição entre chunks consecutivos em caracteres.</summary>
    public int Overlap { get; set; } = 100;

    /// <summary>
    /// Tempo de espera (ms) após o último evento antes de processar.
    /// Evita múltiplas re-indexações quando o editor salva várias vezes.
    /// </summary>
    public int DebounceMs { get; set; } = 20_000;

    /// <summary>
    /// Extensões de arquivo suportadas para indexação.
    /// Apenas arquivos de texto — sem imagens, vídeos ou binários.
    /// </summary>
    public HashSet<string> SupportedExtensions { get; set; } =
    [
        // Docs / plain text
        ".txt", ".text", ".md", ".markdown", ".mdx", ".rst", ".adoc", ".asciidoc",
        ".org", ".tex", ".bib", ".rtf",

        // Data / structured text
        ".json", ".jsonc", ".jsonl", ".ndjson", ".csv", ".tsv", ".psv",
        ".xml", ".xsd", ".xsl", ".xslt", ".svg",
        ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".config", ".properties",
        ".env", ".dotenv", ".editorconfig",

        // Logs
        ".log", ".out",

        // Source code
        ".cs", ".csx", ".vb", ".fs", ".fsx",
        ".c", ".h", ".cc", ".cpp", ".cxx", ".hpp", ".hh", ".hxx",
        ".java", ".kt", ".kts", ".groovy", ".gradle",
        ".py", ".pyi", ".ipynb", ".rb", ".php", ".go", ".rs",
        ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx",
        ".swift", ".scala", ".lua", ".pl", ".pm", ".r", ".jl",

        // Web / templates
        ".html", ".htm", ".css", ".scss", ".sass", ".less",
        ".vue", ".svelte", ".astro",
        ".hbs", ".handlebars", ".mustache",

        // Shell / scripts
        ".sh", ".bash", ".zsh", ".fish", ".ps1", ".psm1", ".bat", ".cmd",
        ".make", ".mk", ".cmake", ".ninja",

        // Infra / CI
        ".dockerfile", ".tf", ".tfvars", ".hcl", ".nomad",
        ".gitignore", ".gitattributes", ".gitmodules",

        // Text-based query / notes
        ".sql", ".graphql", ".gql"
    ];

    /// <summary>
    /// Verifica se o arquivo possui extensão suportada.
    /// </summary>
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
}
