using Xunit;
using Paperless.Configuration;

namespace Paperless.Tests.Configuration;

public class AppSettingsTests
{

    [Fact]
    public void AppSettings_ShouldHaveDefaultSubSections()
    {
        var settings = new AppSettings();

        Assert.NotNull(settings.Environment);
        Assert.NotNull(settings.Storage);
        Assert.NotNull(settings.Assistant);
        Assert.NotNull(settings.Ollama);
    }

    [Fact]
    public void EnvironmentSettings_DefaultOS_ShouldBeLinux()
    {
        var env = new EnvironmentSettings();

        Assert.Equal("linux", env.OS);
    }

    [Fact]
    public void StorageSettings_DefaultBaseFolder_ShouldBePaperless()
    {
        var storage = new StorageSettings();

        Assert.Equal("Paperless", storage.BaseFolder);
    }

    [Fact]
    public void StorageSettings_DefaultTasksFilePath_ShouldBeSystemTasksJson()
    {
        var storage = new StorageSettings();

        Assert.Equal("system/tasks.json", storage.TasksFilePath);
    }

    [Fact]
    public void GetFullTasksPath_ShouldCombineUserProfileBaseFolderAndTasksFile()
    {
        var storage = new StorageSettings
        {
            BaseFolder = "MyFolder",
            TasksFilePath = "system/tasks.json",
        };

        var path = storage.GetFullTasksPath();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, "MyFolder", "system/tasks.json");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetFullTasksPath_WithDefaults_ShouldWork()
    {
        var storage = new StorageSettings();

        var path = storage.GetFullTasksPath();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, "Paperless", "system/tasks.json");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void AssistantSettings_DefaultSystemPrompt_ShouldBeSet()
    {
        var assistant = new AssistantSettings();

        Assert.Equal("You are a local assistant!", assistant.SystemPrompt);
    }

    [Fact]
    public void OllamaOptions_ShouldHaveCorrectDefaults()
    {
        var options = new OllamaOptions();

        Assert.Equal("http://localhost:11434", options.BaseUrl);
        Assert.Equal("qwen3.5:0.8b", options.Model);
        Assert.Equal("nomic-embed-text", options.EmbeddingModel);
        Assert.Equal(120, options.TimeoutSeconds);
    }

    [Fact]
    public void OllamaOptions_ShouldBeMutable()
    {
        var options = new OllamaOptions
        {
            BaseUrl = "http://custom:1234",
            Model = "llama3",
            EmbeddingModel = "custom-embed",
            TimeoutSeconds = 60,
        };

        Assert.Equal("http://custom:1234", options.BaseUrl);
        Assert.Equal("llama3", options.Model);
        Assert.Equal("custom-embed", options.EmbeddingModel);
        Assert.Equal(60, options.TimeoutSeconds);
    }
}
