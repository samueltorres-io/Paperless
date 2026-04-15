using Paperless.Modules.ToDo;

namespace Paperless.Tests.ToDo;

public class TodoTaskTests
{

    [Fact]
    public void NewTask_ShouldGenerateId_With8Chars()
    {
        var task = new TodoTask("Título", "Descrição", 3);

        Assert.NotNull(task.Id);
        Assert.Equal(8, task.Id.Length);
    }

    [Fact]
    public void NewTask_ShouldSetPropertiesCorrectly()
    {
        var task = new TodoTask("Comprar café", "Marca premium", 2);

        Assert.Equal("Comprar café", task.Title);
        Assert.Equal("Marca premium", task.Description);
        Assert.Equal(2, task.Priority);
        Assert.False(task.IsComplete);
    }

    [Fact]
    public void NewTask_TwoInstances_ShouldHaveDifferentIds()
    {
        var a = new TodoTask("A", "", 1);
        var b = new TodoTask("B", "", 1);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void JsonConstructor_ShouldPreserveAllFields()
    {
        var task = new TodoTask("abc123", "Título", "Desc", true, 5);

        Assert.Equal("abc123", task.Id);
        Assert.Equal("Título", task.Title);
        Assert.Equal("Desc", task.Description);
        Assert.True(task.IsComplete);
        Assert.Equal(5, task.Priority);
    }

    [Fact]
    public void UpdateTitle_WithValidTitle_ShouldUpdate()
    {
        var task = new TodoTask("Antigo", "", 1);

        task.UpdateTitle("Novo título");

        Assert.Equal("Novo título", task.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateTitle_WithInvalidTitle_ShouldThrowArgumentException(string? badTitle)
    {
        var task = new TodoTask("Ok", "", 1);

        Assert.Throws<ArgumentException>(() => task.UpdateTitle(badTitle!));
    }

    [Fact]
    public void UpdateDescription_WithValidText_ShouldUpdate()
    {
        var task = new TodoTask("T", "antiga", 1);

        task.UpdateDescription("nova descrição");

        Assert.Equal("nova descrição", task.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_WithInvalidText_ShouldThrowArgumentException(string? badDesc)
    {
        var task = new TodoTask("T", "ok", 1);

        Assert.Throws<ArgumentException>(() => task.UpdateDescription(badDesc!));
    }

    [Fact]
    public void CompleteTask_ShouldSetIsCompleteToTrue()
    {
        var task = new TodoTask("T", "", 1);
        Assert.False(task.IsComplete);

        task.CompleteTask();

        Assert.True(task.IsComplete);
    }

    [Fact]
    public void CompleteTask_CalledTwice_ShouldRemainComplete()
    {
        var task = new TodoTask("T", "", 1);

        task.CompleteTask();
        task.CompleteTask();

        Assert.True(task.IsComplete);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void UpdatePriority_WithValidValue_ShouldUpdate(int priority)
    {
        var task = new TodoTask("T", "", 1);

        task.UpdatePriority(priority);

        Assert.Equal(priority, task.Priority);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void UpdatePriority_WithInvalidValue_ShouldThrowOutOfRange(int badPriority)
    {
        var task = new TodoTask("T", "", 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => task.UpdatePriority(badPriority));
    }

    [Theory]
    [InlineData(1, "low")]
    [InlineData(2, "medium")]
    [InlineData(3, "high")]
    [InlineData(4, "urgent")]
    [InlineData(5, "critical")]
    public void ToString_ShouldContainPriorityLabel(int priority, string expectedLabel)
    {
        var task = new TodoTask("Tarefa X", "", priority);

        var result = task.ToString();

        Assert.Contains(expectedLabel, result);
        Assert.Contains("Tarefa X", result);
    }

    [Fact]
    public void ToString_WhenIncomplete_ShouldShowOpenCircle()
    {
        var task = new TodoTask("T", "", 1);

        Assert.Contains("○", task.ToString());
    }

    [Fact]
    public void ToString_WhenComplete_ShouldShowCheckMark()
    {
        var task = new TodoTask("T", "", 1);
        task.CompleteTask();

        Assert.Contains("✓", task.ToString());
    }

    [Fact]
    public void ToString_ShouldContainId()
    {
        var task = new TodoTask("T", "", 1);

        Assert.Contains(task.Id, task.ToString());
    }
}
