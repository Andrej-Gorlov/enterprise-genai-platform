namespace EnterpriseGenAI.Core.Api.Modules.Chats.Api;

public sealed record ChatResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt);