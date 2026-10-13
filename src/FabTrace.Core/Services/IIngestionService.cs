using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Stores data sent by machines. Every method is idempotent: sending the same
/// item again never creates a second row. <c>machineId</c> always comes from the
/// authenticated API key, not from the payload.
/// </summary>
public interface IIngestionService
{
    Task<IngestResponse> IngestInspectionResultsAsync(
        int machineId, IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken);

    Task<IngestResponse> IngestAlarmsAsync(
        int machineId, IReadOnlyList<AlarmDto> alarms, CancellationToken cancellationToken);

    Task<ClearAlarmOutcome> ClearAlarmAsync(
        int machineId, Guid eventId, DateTimeOffset clearedAt, CancellationToken cancellationToken);

    Task<IngestResponse> IngestMachineStatesAsync(
        int machineId, IReadOnlyList<MachineStateEventDto> events, CancellationToken cancellationToken);
}

public enum ClearAlarmOutcome
{
    /// <summary>The alarm is cleared, by this call or an earlier one.</summary>
    Cleared,

    /// <summary>This machine has no alarm with that id (yet: the raise may still be in transit).</summary>
    NotFound,

    /// <summary>The clear time is earlier than the raise time.</summary>
    ClearedBeforeRaised,
}