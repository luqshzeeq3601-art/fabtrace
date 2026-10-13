using System.Security.Claims;

using FabTrace.Api.Auth;
using FabTrace.Contracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    IUserTokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.UserName!, (IReadOnlyList<string>)roles);

        return Ok(new LoginResponse(user.UserName!, (IReadOnlyList<string>)roles, token));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = UserTokenAuthenticationHandler.SchemeName)]
    public ActionResult<UserProfileDto> GetCurrentUser()
    {
        var userName = User.Identity?.Name ?? "Unknown";
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return Ok(new UserProfileDto(userName, roles));
    }
}