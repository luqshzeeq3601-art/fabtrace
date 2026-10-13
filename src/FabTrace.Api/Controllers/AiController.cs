using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Roles = $"{Roles.Engineer},{Roles.Admin}")]
public class AiController(IAiRootCauseAssistant assistant) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<AiRootCauseResponseDto>> Analyze(
        [FromBody] AiRootCauseQueryDto query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
        {
            return BadRequest("Question cannot be empty.");
        }

        var result = await assistant.AnalyzeAsync(query, cancellationToken);
        return Ok(result);
    }
}