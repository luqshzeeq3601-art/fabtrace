using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/scada")]
public class ScadaController(IScadaService scada) : ControllerBase
{
    [HttpGet("live")]
    public async Task<ActionResult<LiveFloorSummaryDto>> GetLiveSummary(CancellationToken cancellationToken) =>
        await scada.GetFloorSummaryAsync(cancellationToken);

    [HttpGet("machines/{machineCode}")]
    public async Task<ActionResult<LiveMachineStatusDto>> GetMachineLiveStatus(
        string machineCode, CancellationToken cancellationToken)
    {
        var status = await scada.GetMachineLiveStatusAsync(machineCode, cancellationToken);
        return status is null ? NotFound() : status;
    }
}