using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Executes reporting queries and stored procedures for yield, downtime, OEE, and root cause analysis.
/// </summary>
public interface IReportService
{
    Task<IReadOnlyList<YieldReportItemDto>> GetYieldReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MachineDowntimeItemDto>> GetDowntimeReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OeeReportItemDto>> GetOeeReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DefectParetoItemDto>> GetDefectParetoAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default);

    Task<RootCauseAnalysisDto> GetRootCauseAnalysisAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}