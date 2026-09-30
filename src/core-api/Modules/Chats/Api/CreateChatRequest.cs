using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Modules.Chats.Api;

public sealed class CreateChatRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;
}