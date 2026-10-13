using System.Net.Http.Headers;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FabTrace.Api.Auth;

/// <summary>
/// Authenticates interactive users via Authorization: Bearer <token>.
/// </summary>
public sealed class UserTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserTokenService tokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UserBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? rawToken = null;

        if (Request.Headers.TryGetValue("Authorization", out var headerVal))
        {
            if (AuthenticationHeaderValue.TryParse(headerVal, out var authHeader) &&
                string.Equals(authHeader.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(authHeader.Parameter))
            {
                rawToken = authHeader.Parameter;
            }
        }
        else if (Request.Path.StartsWithSegments("/hubs/factory", StringComparison.OrdinalIgnoreCase) &&
                 Request.Query.TryGetValue("access_token", out var queryToken) &&
                 !string.IsNullOrWhiteSpace(queryToken))
        {
            rawToken = queryToken;
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = tokenService.ValidateToken(rawToken);
        if (principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired user token."));
        }

        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}