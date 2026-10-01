using EnterpriseGenAI.Core.Api.Modules.Chats.Domain;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Chats.Domain;

public sealed class ChatTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = "Test chat";
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var chat = new Chat(id, title, createdAt);

        // Assert
        Assert.Equal(id, chat.Id);
        Assert.Equal(title, chat.Title);
        Assert.Equal(createdAt, chat.CreatedAt);
    }
}