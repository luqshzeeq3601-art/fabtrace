using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Admin operations for machine and key management.
/// </summary>
public interface IAdminService
{
    Task<IssueApiKeyResponse?> IssueApiKeyAsync(string machineCode, CancellationToken cancellationToken = default);
}