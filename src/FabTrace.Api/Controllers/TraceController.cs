using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

/// <summary>
/// Read-back endpoints for traceability and machine master data lookup.
/// Requires an authenticated user session.
/// </summary>
[Authorize]
[ApiController]
[Route("api")]
public class TraceController(ITraceQueryService queries) : ControllerBase
{
    [HttpGet("machines")]
    public Task<IReadOnlyList<MachineDto>> GetMachines(CancellationToken cancellationToken) =>
        queries.GetMachinesAsync(cancellationToken);

    [HttpGet("lots/{lotNumber}")]
    public async Task<ActionResult<LotSummaryDto>> GetLot(string lotNumber, CancellationToken cancellationToken)
    {
        var lot = await queries.GetLotAsync(lotNumber, cancellationToken);
        return lot is null ? NotFound() : lot;
    }

    [HttpGet("units/{serialNumber}")]
    public async Task<ActionResult<UnitTraceDto>> GetUnit(string serialNumber, CancellationToken cancellationToken)
    {
        var unit = await queries.GetUnitAsync(serialNumber, cancellationToken);
        return unit is null ? NotFound() : unit;
    }
}