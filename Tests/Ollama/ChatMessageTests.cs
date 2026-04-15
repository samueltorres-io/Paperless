using Xunit;
using Paperless.Modules.Ollama;

namespace Paperless.Tests.Ollama;

public class ChatMessageTests
{
    [Fact]
    public void System_ShouldSetRoleToSystem()
    {
        var msg = ChatMessage.System("Você é um assistente.");

        Assert.Equal("system", msg.Role);
        Assert.Equal("Você é um assistente.", msg.Content);
    }

    [Fact]
    public void User_ShouldSetRoleToUser()
    {
        var msg = ChatMessage.User("Olá!");

        Assert.Equal("user", msg.Role);
        Assert.Equal("Olá!", msg.Content);
    }

    [Fact]
    public void Assistant_ShouldSetRoleToAssistant()
    {
        var msg = ChatMessage.Assistant("Resposta aqui.");

        Assert.Equal("assistant", msg.Role);
        Assert.Equal("Resposta aqui.", msg.Content);
    }

    [Fact]
    public void DefaultConstructor_ShouldInitializeWithEmptyStrings()
    {
        var msg = new ChatMessage();

        Assert.Equal(string.Empty, msg.Role);
        Assert.Equal(string.Empty, msg.Content);
    }

    [Fact]
    public void ParameterizedConstructor_ShouldSetBothFields()
    {
        var msg = new ChatMessage("custom_role", "custom_content");

        Assert.Equal("custom_role", msg.Role);
        Assert.Equal("custom_content", msg.Content);
    }

    [Fact]
    public void Properties_ShouldBeSettableViaObjectInitializer()
    {
        var msg = new ChatMessage
        {
            Role = "user",
            Content = "teste",
        };

        Assert.Equal("user", msg.Role);
        Assert.Equal("teste", msg.Content);
    }
}
