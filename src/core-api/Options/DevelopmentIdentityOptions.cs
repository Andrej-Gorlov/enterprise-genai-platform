using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Options;

public sealed class DevelopmentIdentityOptions
{
    public const string SectionName = "DevelopmentIdentity";

    /// <summary>
    /// Явное разрешение Development Authentication.
    /// Включается только вместе с Environment = Development.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Стабильный тестовый идентификатор пользователя (серверная конфигурация, не клиент).
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;
}