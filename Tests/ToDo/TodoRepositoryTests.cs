using Xunit;
using Paperless.Modules.ToDo;

namespace Paperless.Tests.ToDo;

public class TodoRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly TodoRepository _repo;

    public TodoRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"paperless_test_{Guid.NewGuid():N}");
        _filePath = Path.Combine(_tempDir, "tasks.json");
        _repo = new TodoRepository(_filePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void Constructor_ShouldCreateDirectoryAndFile()
    {
        Assert.True(Directory.Exists(_tempDir));
        Assert.True(System.IO.File.Exists(_filePath));
    }

    [Fact]
    public void Constructor_ShouldInitializeWithEmptyArray()
    {
        var content = System.IO.File.ReadAllText(_filePath);
        Assert.Equal("[]", content);
    }

    [Fact]
    public void Constructor_WithExistingFile_ShouldNotOverwrite()
    {
        var task = new TodoTask("Existente", "desc", 2);
        _repo.SaveAll([task]);

        // Cria novo repositório apontando para o mesmo arquivo
        var repo2 = new TodoRepository(_filePath);
        var loaded = repo2.LoadAll();

        Assert.Single(loaded);
        Assert.Equal("Existente", loaded[0].Title);
    }

    [Fact]
    public void LoadAll_WhenEmpty_ShouldReturnEmptyList()
    {
        var tasks = _repo.LoadAll();

        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public void LoadAll_AfterSave_ShouldReturnSavedTasks()
    {
        var original = new List<TodoTask>
        {
            new("Tarefa 1", "Desc 1", 1),
            new("Tarefa 2", "Desc 2", 3),
        };

        _repo.SaveAll(original);
        var loaded = _repo.LoadAll();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Tarefa 1", loaded[0].Title);
        Assert.Equal("Tarefa 2", loaded[1].Title);
    }

    [Fact]
    public void SaveAll_ShouldPersistAllFields()
    {
        var task = new TodoTask("Título", "Descrição", 4);
        task.CompleteTask();

        _repo.SaveAll([task]);
        var loaded = _repo.LoadAll().Single();

        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal("Título", loaded.Title);
        Assert.Equal("Descrição", loaded.Description);
        Assert.Equal(4, loaded.Priority);
        Assert.True(loaded.IsComplete);
    }

    [Fact]
    public void SaveAll_WithEmptyList_ShouldClearFile()
    {
        _repo.SaveAll([new TodoTask("X", "", 1)]);
        Assert.Single(_repo.LoadAll());

        _repo.SaveAll([]);
        Assert.Empty(_repo.LoadAll());
    }

    [Fact]
    public void SaveAll_ShouldWriteIndentedJson()
    {
        _repo.SaveAll([new TodoTask("T", "D", 1)]);
        var json = System.IO.File.ReadAllText(_filePath);

        Assert.Contains("\n", json);
        Assert.Contains("  ", json);
    }

    [Fact]
    public void SaveAll_ShouldUseCamelCasePropertyNames()
    {
        _repo.SaveAll([new TodoTask("T", "D", 1)]);
        var json = System.IO.File.ReadAllText(_filePath);

        Assert.Contains("\"title\"", json);
        Assert.Contains("\"description\"", json);
        Assert.Contains("\"isComplete\"", json);
        Assert.Contains("\"priority\"", json);
    }

    [Fact]
    public void Roundtrip_MultipleTasks_ShouldPreserveOrder()
    {
        var tasks = Enumerable.Range(1, 10)
            .Select(i => new TodoTask($"Tarefa {i}", $"Desc {i}", (i % 5) + 1))
            .ToList();

        _repo.SaveAll(tasks);
        var loaded = _repo.LoadAll();

        Assert.Equal(tasks.Count, loaded.Count);
        for (int i = 0; i < tasks.Count; i++)
        {
            Assert.Equal(tasks[i].Id, loaded[i].Id);
            Assert.Equal(tasks[i].Title, loaded[i].Title);
        }
    }

    [Fact]
    public void ConcurrentSaves_ShouldNotCorruptFile()
    {
        var threads = new List<Thread>();

        for (int i = 0; i < 10; i++)
        {
            int idx = i;
            var thread = new Thread(() =>
            {
                var task = new TodoTask($"Concurrent {idx}", "", 1);
                var list = _repo.LoadAll();
                list.Add(task);
                _repo.SaveAll(list);
            });
            threads.Add(thread);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        var result = _repo.LoadAll();
        Assert.NotNull(result);
        Assert.True(result.Count > 0);
    }

    [Fact]
    public void Constructor_WithDeepPath_ShouldCreateAllDirectories()
    {
        var deepPath = Path.Combine(_tempDir, "a", "b", "c", "tasks.json");
        var repo = new TodoRepository(deepPath);

        Assert.True(System.IO.File.Exists(deepPath));
    }
}
