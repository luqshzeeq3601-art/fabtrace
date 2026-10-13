using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{Roles.Engineer},{Roles.Admin}")]
public class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("yield")]
    public async Task<ActionResult<IReadOnlyList<YieldReportItemDto>>> GetYieldReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? machineCode,
        CancellationToken cancellationToken)
    {
        var items = await reports.GetYieldReportAsync(from, to, machineCode, cancellationToken);
        return Ok(items);
    }

    [HttpGet("downtime")]
    public async Task<ActionResult<IReadOnlyList<MachineDowntimeItemDto>>> GetDowntimeReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? machineCode,
        CancellationToken cancellationToken)
    {
        var items = await reports.GetDowntimeReportAsync(from, to, machineCode, cancellationToken);
        return Ok(items);
    }

    [HttpGet("oee")]
    public async Task<ActionResult<IReadOnlyList<OeeReportItemDto>>> GetOeeReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? machineCode,
        CancellationToken cancellationToken)
    {
        var items = await reports.GetOeeReportAsync(from, to, machineCode, cancellationToken);
        return Ok(items);
    }

    [HttpGet("defects")]
    public async Task<ActionResult<IReadOnlyList<DefectParetoItemDto>>> GetDefectPareto(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? machineCode,
        CancellationToken cancellationToken)
    {
        var items = await reports.GetDefectParetoAsync(from, to, machineCode, cancellationToken);
        return Ok(items);
    }

    [HttpGet("root-cause")]
    public async Task<ActionResult<RootCauseAnalysisDto>> GetRootCauseAnalysis(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        var result = await reports.GetRootCauseAnalysisAsync(from, to, cancellationToken);
        return Ok(result);
    }
}