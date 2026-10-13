using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[ApiController]
[Route("api/maintenance/predictive")]
[Authorize(Roles = $"{Roles.Engineer},{Roles.Admin}")]
public sealed class PredictiveMaintenanceController(IPredictiveMaintenanceService service) : ControllerBase
{
    [HttpGet("machine/{machineCode}")]
    public async Task<ActionResult<PredictiveMaintenanceReportDto>> AnalyzeParameter(
        string machineCode,
        [FromQuery] string parameterName = "ContactResistance",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var toUtc = to ?? DateTime.UtcNow;
        var fromUtc = from ?? toUtc.AddDays(-30);

        try
        {
            var report = await service.AnalyzeParameterAsync(machineCode, parameterName, fromUtc, toUtc, cancellationToken);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("fleet-summary")]
    public async Task<ActionResult<IReadOnlyList<MachineHealthSummaryDto>>> GetFleetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var toUtc = to ?? DateTime.UtcNow;
        var fromUtc = from ?? toUtc.AddDays(-30);

        var summary = await service.GetFleetHealthSummaryAsync(fromUtc, toUtc, cancellationToken);
        return Ok(summary);
    }
}