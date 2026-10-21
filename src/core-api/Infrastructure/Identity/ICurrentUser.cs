namespace EnterpriseGenAI.Core.Api.Infrastructure.Identity;

/// <summary>
/// Доверенная идентичность текущего пользователя из authentication pipeline.
/// Не читать identity из произвольных HTTP-заголовков.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Идентификатор аутентифицированного пользователя (совпадает с Document.OwnerId).
    /// null, если пользователь не аутентифицирован или claim отсутствует/некорректен.
    /// </summary>
    string? UserId { get; }

    bool IsAuthenticated { get; }
}