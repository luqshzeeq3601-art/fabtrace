using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Performs time-series anomaly detection and predictive maintenance forecasting using ML.NET.
/// </summary>
public interface IPredictiveMaintenanceService
{
    Task<PredictiveMaintenanceReportDto> AnalyzeParameterAsync(
        string machineCode,
        string parameterName,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MachineHealthSummaryDto>> GetFleetHealthSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}