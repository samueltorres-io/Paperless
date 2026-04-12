using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paperless.Modules.ToDo;

public class TodoRepository
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public TodoRepository(string filepath)
    {
        _filePath = filepath;
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, "[]");
    }

    /* Lê o JSON e transforma em List<TodoTask> */
    public List<TodoTask> LoadAll()
    {
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<TodoTask>>(json, JsonOptions) ?? [];
    }

    /* Transforam a lista em JSON e salva em arquivo */
    public void SaveAll(List<TodoTask> tasks)
    {
        var json = JsonSerializer.Serialize(tasks, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}