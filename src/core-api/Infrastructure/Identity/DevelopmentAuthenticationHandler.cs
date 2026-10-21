using System.Security.Claims;
using System.Text.Encodings.Web;
using EnterpriseGenAI.Core.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Identity;

public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";

    private readonly DevelopmentIdentityOptions _identityOptions;

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<DevelopmentIdentityOptions> identityOptions)
        : base(options, logger, encoder)
    {
        _identityOptions = identityOptions?.Value ?? throw new ArgumentNullException(nameof(identityOptions));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Identity только из серверной конфигурации, заголовки клиента игнорируются.
        var userId = _identityOptions.UserId;
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > 256 || userId.Any(char.IsControl))
        {
            return Task.FromResult(AuthenticateResult.Fail("Параметр DevelopmentIdentity:UserId отсутствует или является недопустимым."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Development User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}