using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>Read-only lookups. Phase 3 grows this into full traceability and reports.</summary>
public interface ITraceQueryService
{
    Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken cancellationToken);

    Task<LotSummaryDto?> GetLotAsync(string lotNumber, CancellationToken cancellationToken);

    Task<UnitTraceDto?> GetUnitAsync(string serialNumber, CancellationToken cancellationToken);
}