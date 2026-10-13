using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FabTrace.Api.Auth;

public interface IUserTokenService
{
    string CreateToken(string userName, IReadOnlyList<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
}

public sealed class UserTokenService : IUserTokenService
{
    private readonly byte[] _secretKey;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    public UserTokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredKey = configuration["Auth:TokenSigningKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            _secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        }
        else if (environment.IsDevelopment())
        {
            // Ephemeral high-entropy random key for local development and test runs;
            // generated once per host lifetime in memory, so no secrets are committed.
            _secretKey = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException("Auth:TokenSigningKey configuration is required in non-development environments.");
        }
    }

    private record TokenPayload(string Sub, IReadOnlyList<string> Roles, long Exp);

    public string CreateToken(string userName, IReadOnlyList<string> roles)
    {
        var exp = DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds();
        var payload = new TokenPayload(userName, roles, exp);
        var json = JsonSerializer.Serialize(payload);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        using var hmac = new HMACSHA256(_secretKey);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signatureBase64 = Convert.ToBase64String(signatureBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        return $"{payloadBase64}.{signatureBase64}";
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        var payloadPart = parts[0];
        var signaturePart = parts[1];

        using var hmac = new HMACSHA256(_secretKey);
        var expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart));
        var expectedSigBase64 = Convert.ToBase64String(expectedSig)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signaturePart),
            Encoding.UTF8.GetBytes(expectedSigBase64)))
        {
            return null;
        }

        try
        {
            var padded = payloadPart.Replace("-", "+").Replace("_", "/");
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var payload = JsonSerializer.Deserialize<TokenPayload>(json);

            if (payload is null)
            {
                return null;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.Exp)
            {
                return null;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, payload.Sub),
                new(ClaimTypes.NameIdentifier, payload.Sub),
            };

            foreach (var role in payload.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, UserTokenAuthenticationHandler.SchemeName);
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }
}