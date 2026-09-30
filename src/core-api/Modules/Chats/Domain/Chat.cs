namespace EnterpriseGenAI.Core.Api.Modules.Chats.Domain;

public sealed class Chat
{
    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private Chat()
    {
    }

    public Chat(Guid id, string title, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        CreatedAt = createdAt;
    }
}