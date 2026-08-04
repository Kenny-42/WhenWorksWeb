using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// A test-only authentication scheme that authenticates a request as whatever user id/roles are carried in
/// the <c>X-Test-UserId</c>/<c>X-Test-Roles</c> request headers, instead of a real Identity cookie.
/// </summary>
/// <remarks>
/// This stands in for the login/cookie-issuing mechanics of ASP.NET Core Identity's scaffolded UI (out of
/// scope to modify or deeply exercise per CLAUDE.md), not for authorization. <c>[Authorize]</c>/
/// <c>[Authorize(Roles = "Admin")]</c> policy evaluation against the resulting <see cref="ClaimsPrincipal"/>
/// is completely real and unmodified — this only changes how the principal gets attached to the request, the
/// same category of swap as <see cref="CustomWebApplicationFactory"/> replacing the database provider.
/// Registered with <c>DefaultChallengeScheme</c>/<c>DefaultForbidScheme</c> still pointed at Identity's real
/// cookie scheme, so unauthenticated/unauthorized requests still redirect exactly as production does — only
/// <c>DefaultAuthenticateScheme</c> is overridden.
/// </remarks>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userId) || string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
