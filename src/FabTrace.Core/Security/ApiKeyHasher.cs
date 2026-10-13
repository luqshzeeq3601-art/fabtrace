using System.Security.Cryptography;
using System.Text;

namespace FabTrace.Core.Security;

/// <summary>
/// Turns an API key into the value stored in the database, so a leaked database
/// does not leak usable keys.
/// </summary>
/// <remarks>
/// Plain SHA-256 is enough here because an API key is long and random. Passwords
/// are different: people choose guessable ones, so they need a slow, salted hash
/// (ASP.NET Core Identity does that for user logins).
/// </remarks>
public static class ApiKeyHasher
{
    public const int HashSizeBytes = 32;

    public static byte[] Hash(string apiKey) => SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
}