using System.Security.Claims;
using System.Text.Encodings.Web;

using FabTrace.Contracts;
using FabTrace.Core.Security;
using FabTrace.Data;

using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FabTrace.Api.Auth;

/// <summary>
/// Identifies the calling machine from its X-Api-Key header.
/// </summary>
/// <remarks>
/// ASP.NET Core calls this for each request. On success the request carries a
/// user whose claims hold the machine's id and code, and <c>[Authorize]</c> lets
/// it through. Controllers read the machine from those claims, never from the
/// payload, so a machine cannot report data as a different machine.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FabTraceDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string MachineIdClaim = "machine_id";
    public const string MachineCodeClaim = "machine_code";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiConventions.ApiKeyHeader, out var header))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = header.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Empty API key.");
        }

        // Compare hashes: the database only holds the hash, and the lookup is a
        // seek on the unique index over ApiKeyHash.
        var hash = ApiKeyHasher.Hash(apiKey);
        var machine = await db.Machines
            .Where(m => m.ApiKeyHash == hash && m.IsActive)
            .Select(m => new { m.MachineId, m.Code })
            .SingleOrDefaultAsync(Context.RequestAborted);

        if (machine is null)
        {
            return AuthenticateResult.Fail("Unknown or inactive API key.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(MachineIdClaim, machine.MachineId.ToString()),
            new Claim(MachineCodeClaim, machine.Code),
        ], SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class MachinePrincipalExtensions
{
    public static int GetMachineId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.MachineIdClaim)
            ?? throw new InvalidOperationException("The request is not authenticated as a machine."));

    public static string? GetMachineCode(this ClaimsPrincipal user) =>
        user.FindFirstValue(ApiKeyAuthenticationHandler.MachineCodeClaim);
}