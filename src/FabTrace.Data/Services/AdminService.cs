using System.Security.Cryptography;

using FabTrace.Contracts;
using FabTrace.Core.Security;
using FabTrace.Core.Services;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Services;

public sealed class AdminService(FabTraceDbContext db) : IAdminService
{
    public async Task<IssueApiKeyResponse?> IssueApiKeyAsync(string machineCode, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .SingleOrDefaultAsync(m => m.Code == machineCode, cancellationToken);

        if (machine is null)
        {
            return null;
        }

        // Generate 32 bytes of secure cryptographic entropy (64 hex characters).
        var rawKey = $"key_{machine.Code.ToLowerInvariant()}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        machine.ApiKeyHash = ApiKeyHasher.Hash(rawKey);

        await db.SaveChangesAsync(cancellationToken);

        return new IssueApiKeyResponse(machine.Code, rawKey, DateTime.UtcNow);
    }
}