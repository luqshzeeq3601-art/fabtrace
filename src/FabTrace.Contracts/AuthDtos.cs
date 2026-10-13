using System.ComponentModel.DataAnnotations;

namespace FabTrace.Contracts;

public record LoginRequest
{
    [Required]
    public required string UserName { get; init; }

    [Required]
    public required string Password { get; init; }
}

public record LoginResponse(
    string UserName,
    IReadOnlyList<string> Roles,
    string Token);

public record UserProfileDto(
    string UserName,
    IReadOnlyList<string> Roles);