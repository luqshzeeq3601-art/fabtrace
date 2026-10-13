using System.ComponentModel.DataAnnotations;

namespace FabTrace.Contracts;

public record IssueApiKeyRequest
{
    [Required]
    public required string MachineCode { get; init; }
}

public record IssueApiKeyResponse(
    string MachineCode,
    string RawApiKey,
    DateTime IssuedAt);