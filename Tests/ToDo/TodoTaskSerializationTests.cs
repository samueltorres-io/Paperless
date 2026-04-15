using Xunit;
using System.Text.Json;
using Paperless.Modules.ToDo;

namespace Paperless.Tests.ToDo;

public class TodoTaskSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void TodoTask_ShouldRoundtripThroughJson()
    {
        var original = new TodoTask("Comprar café", "Marca especial", 3);
        original.CompleteTask();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TodoTask>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized!.Id);
        Assert.Equal("Comprar café", deserialized.Title);
        Assert.Equal("Marca especial", deserialized.Description);
        Assert.Equal(3, deserialized.Priority);
        Assert.True(deserialized.IsComplete);
    }

    [Fact]
    public void TodoTaskList_ShouldRoundtripThroughJson()
    {
        var tasks = new List<TodoTask>
        {
            new("A", "Desc A", 1),
            new("B", "Desc B", 5),
        };

        var json = JsonSerializer.Serialize(tasks, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<List<TodoTask>>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("A", deserialized[0].Title);
        Assert.Equal("B", deserialized[1].Title);
    }

    [Fact]
    public void EmptyList_ShouldDeserializeCorrectly()
    {
        var json = "[]";

        var deserialized = JsonSerializer.Deserialize<List<TodoTask>>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized!);
    }

    [Fact]
    public void TodoTask_ShouldDeserializeFromExternalJson()
    {
        var json = """
        {
            "id": "a1b2c3",
            "title": "Revisar PR do backend",
            "description": "Desenvolver os testes unitários para o módulo de RAG",
            "isComplete": false,
            "priority": 4
        }
        """;

        var task = JsonSerializer.Deserialize<TodoTask>(json, JsonOptions);

        Assert.NotNull(task);
        Assert.Equal("a1b2c3", task!.Id);
        Assert.Equal("Revisar PR do backend", task.Title);
        Assert.Equal(4, task.Priority);
        Assert.False(task.IsComplete);
    }
}
