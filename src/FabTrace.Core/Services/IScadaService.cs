using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Provides live SCADA dashboard status for machines, active alarms, and shift throughput.
/// </summary>
public interface IScadaService
{
    Task<LiveFloorSummaryDto> GetFloorSummaryAsync(CancellationToken cancellationToken = default);

    Task<LiveMachineStatusDto?> GetMachineLiveStatusAsync(string machineCode, CancellationToken cancellationToken = default);
}