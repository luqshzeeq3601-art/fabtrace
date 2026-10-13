using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(IAdminService admin) : ControllerBase
{
    [HttpPost("machines/{machineCode}/api-key")]
    public async Task<ActionResult<IssueApiKeyResponse>> IssueApiKey(
        string machineCode, CancellationToken cancellationToken)
    {
        var response = await admin.IssueApiKeyAsync(machineCode, cancellationToken);
        return response is null
            ? NotFound($"Machine '{machineCode}' was not found.")
            : response;
    }
}