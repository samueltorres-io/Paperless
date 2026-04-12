/* Modules/ToDo/TodoRepository.cs */

namespace Paperless.Modules.ToDo;

public class TodoRepository
{
    private readonly string _filepath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public TodoRepository(string filepath)
    {
        _filepath = filepath;
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        var directory = Path.GetDirectoryName(_filepath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, "[]");
    }
}