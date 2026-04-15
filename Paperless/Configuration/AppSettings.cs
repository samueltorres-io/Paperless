namespace Paperless.Configuration;

public sealed class AppSettings
{
    public EnvironmentSettings Environment { get; set; } = new();
    public StorageSettings Storage { get; set; } = new();
    public AssistantSettings Assistant { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class EnvironmentSettings
{
    public string OS { get; set; } = "linux";
}

public sealed class StorageSettings
{
    public string BaseFolder { get; set; } = "Paperless";
    public string TasksFilePath { get; set; } = "system/tasks.json";
    public string DatabaseFilePath { get; set; } = "system/paperless.db";
    public string UserFolderPath { get; set; } = "data/";

    public string GetFullTasksPath()
        => Path.Combine(BaseFolder, TasksFilePath);

    public string GetFullDatabasePath()
        => Path.Combine(BaseFolder, DatabaseFilePath);

    public string GetFullUserFolderPath()
        => Path.Combine(BaseFolder, UserFolderPath);
}

public sealed class AssistantSettings
{
    public string SystemPrompt { get; set; } = "You are a local assistant!";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3.5:0.8b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 120;
}