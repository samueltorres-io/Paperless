using Paperless.Modules.ToDo;

namespace Paperless.Tests.ToDo;

public class TodoManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TodoManager _manager;

    public TodoManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"paperless_mgr_{Guid.NewGuid():N}");
        var filePath = Path.Combine(_tempDir, "tasks.json");
        var repo = new TodoRepository(filePath);
        _manager = new TodoManager(repo);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup */ }
    }

    [Fact]
    public void CreateTask_WithValidData_ShouldReturnTask()
    {
        var task = _manager.CreateTask("Nova tarefa", "Detalhes", 3);

        Assert.NotNull(task);
        Assert.Equal("Nova tarefa", task.Title);
        Assert.Equal("Detalhes", task.Description);
        Assert.Equal(3, task.Priority);
        Assert.False(task.IsComplete);
    }

    [Fact]
    public void CreateTask_ShouldPersist()
    {
        _manager.CreateTask("Persistida", null, null);

        var all = _manager.ListTasks();
        Assert.Single(all);
        Assert.Equal("Persistida", all[0].Title);
    }

    [Fact]
    public void CreateTask_WithNullDescription_ShouldDefaultToEmpty()
    {
        var task = _manager.CreateTask("T", null, null);

        Assert.Equal(string.Empty, task.Description);
    }

    [Fact]
    public void CreateTask_WithNullPriority_ShouldDefaultTo1()
    {
        var task = _manager.CreateTask("T", null, null);

        Assert.Equal(1, task.Priority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTask_WithInvalidTitle_ShouldThrow(string? badTitle)
    {
        Assert.Throws<ArgumentException>(() =>
            _manager.CreateTask(badTitle!, null, null));
    }

    [Fact]
    public void CreateTask_MultipleTasks_ShouldAllPersist()
    {
        _manager.CreateTask("A", null, 1);
        _manager.CreateTask("B", null, 2);
        _manager.CreateTask("C", null, 3);

        Assert.Equal(3, _manager.ListTasks().Count);
    }

    [Fact]
    public void ListTasks_WhenEmpty_ShouldReturnEmptyList()
    {
        var tasks = _manager.ListTasks();

        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public void ListTasks_ShouldOrderByPriorityDescending()
    {
        _manager.CreateTask("Low", null, 1);
        _manager.CreateTask("High", null, 5);
        _manager.CreateTask("Medium", null, 3);

        var tasks = _manager.ListTasks();

        Assert.Equal(5, tasks[0].Priority);
        Assert.Equal(3, tasks[1].Priority);
        Assert.Equal(1, tasks[2].Priority);
    }

    [Fact]
    public void ListTasks_WithPriorityFilter_ShouldReturnOnlyMatching()
    {
        _manager.CreateTask("Low 1", null, 1);
        _manager.CreateTask("High 1", null, 5);
        _manager.CreateTask("Low 2", null, 1);

        var lowTasks = _manager.ListTasks(priorityFilter: 1);

        Assert.Equal(2, lowTasks.Count);
        Assert.All(lowTasks, t => Assert.Equal(1, t.Priority));
    }

    [Fact]
    public void ListTasks_WithNonexistentPriority_ShouldReturnEmpty()
    {
        _manager.CreateTask("T", null, 1);

        var result = _manager.ListTasks(priorityFilter: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void ListTasks_CompleteTasks_ShouldAppearAfterIncomplete()
    {
        var t1 = _manager.CreateTask("Feita", null, 3);
        _manager.CreateTask("Pendente", null, 3);
        _manager.CompleteTask(t1.Id);

        var tasks = _manager.ListTasks();

        Assert.False(tasks[0].IsComplete);
        Assert.True(tasks[1].IsComplete);
    }

    [Fact]
    public void CompleteTask_WithValidId_ShouldMarkAsComplete()
    {
        var created = _manager.CreateTask("Fazer", null, 1);

        var completed = _manager.CompleteTask(created.Id);

        Assert.NotNull(completed);
        Assert.True(completed!.IsComplete);
    }

    [Fact]
    public void CompleteTask_ShouldPersistChange()
    {
        var created = _manager.CreateTask("Fazer", null, 1);
        _manager.CompleteTask(created.Id);

        var loaded = _manager.GetTask(created.Id);

        Assert.True(loaded!.IsComplete);
    }

    [Fact]
    public void CompleteTask_WithInvalidId_ShouldReturnNull()
    {
        var result = _manager.CompleteTask("inexistente");

        Assert.Null(result);
    }

    [Fact]
    public void CompleteTask_ShouldBeCaseInsensitive()
    {
        var created = _manager.CreateTask("T", null, 1);

        var completed = _manager.CompleteTask(created.Id.ToUpper());

        Assert.NotNull(completed);
        Assert.True(completed!.IsComplete);
    }

    [Fact]
    public void UpdateTask_Title_ShouldUpdateOnlyTitle()
    {
        var created = _manager.CreateTask("Original", "Desc", 2);

        var updated = _manager.UpdateTask(created.Id, title: "Novo");

        Assert.NotNull(updated);
        Assert.Equal("Novo", updated!.Title);
        Assert.Equal("Desc", updated.Description);
        Assert.Equal(2, updated.Priority);
    }

    [Fact]
    public void UpdateTask_Description_ShouldUpdateOnlyDescription()
    {
        var created = _manager.CreateTask("T", "Antiga", 2);

        var updated = _manager.UpdateTask(created.Id, description: "Nova desc");

        Assert.Equal("T", updated!.Title);
        Assert.Equal("Nova desc", updated.Description);
    }

    [Fact]
    public void UpdateTask_Priority_ShouldUpdateOnlyPriority()
    {
        var created = _manager.CreateTask("T", "D", 1);

        var updated = _manager.UpdateTask(created.Id, priority: 5);

        Assert.Equal(5, updated!.Priority);
        Assert.Equal("T", updated.Title);
    }

    [Fact]
    public void UpdateTask_AllFields_ShouldUpdateAll()
    {
        var created = _manager.CreateTask("T", "D", 1);

        var updated = _manager.UpdateTask(created.Id, "Novo", "Nova", 4);

        Assert.Equal("Novo", updated!.Title);
        Assert.Equal("Nova", updated.Description);
        Assert.Equal(4, updated.Priority);
    }

    [Fact]
    public void UpdateTask_WithInvalidId_ShouldReturnNull()
    {
        Assert.Null(_manager.UpdateTask("nope", title: "X"));
    }

    [Fact]
    public void UpdateTask_ShouldPersist()
    {
        var created = _manager.CreateTask("Antes", null, 1);
        _manager.UpdateTask(created.Id, title: "Depois");

        var loaded = _manager.GetTask(created.Id);
        Assert.Equal("Depois", loaded!.Title);
    }

    [Fact]
    public void DeleteTask_WithValidId_ShouldReturnTrue()
    {
        var created = _manager.CreateTask("Delete me", null, 1);

        Assert.True(_manager.DeleteTask(created.Id));
    }

    [Fact]
    public void DeleteTask_ShouldRemoveFromList()
    {
        var created = _manager.CreateTask("Delete me", null, 1);
        _manager.DeleteTask(created.Id);

        Assert.Empty(_manager.ListTasks());
    }

    [Fact]
    public void DeleteTask_WithInvalidId_ShouldReturnFalse()
    {
        Assert.False(_manager.DeleteTask("inexistente"));
    }

    [Fact]
    public void DeleteTask_ShouldNotAffectOtherTasks()
    {
        var a = _manager.CreateTask("A", null, 1);
        var b = _manager.CreateTask("B", null, 2);

        _manager.DeleteTask(a.Id);

        var remaining = _manager.ListTasks();
        Assert.Single(remaining);
        Assert.Equal("B", remaining[0].Title);
    }

    [Fact]
    public void DeleteTask_ShouldBeCaseInsensitive()
    {
        var created = _manager.CreateTask("T", null, 1);

        Assert.True(_manager.DeleteTask(created.Id.ToUpper()));
        Assert.Empty(_manager.ListTasks());
    }

    [Fact]
    public void GetTask_WithValidId_ShouldReturnTask()
    {
        var created = _manager.CreateTask("Busca", "desc", 3);

        var found = _manager.GetTask(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal("Busca", found.Title);
    }

    [Fact]
    public void GetTask_WithInvalidId_ShouldReturnNull()
    {
        Assert.Null(_manager.GetTask("naoexiste"));
    }

    [Fact]
    public void GetTask_ShouldBeCaseInsensitive()
    {
        var created = _manager.CreateTask("T", null, 1);

        Assert.NotNull(_manager.GetTask(created.Id.ToUpper()));
    }
}
