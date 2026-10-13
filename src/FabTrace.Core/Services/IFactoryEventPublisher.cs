using FabTrace.Contracts;

namespace FabTrace.Core.Services;

/// <summary>
/// Broadcasts factory floor events to real-time subscribers (e.g. SignalR).
/// </summary>
public interface IFactoryEventPublisher
{
    Task PublishMachineStateChangedAsync(string machineCode, MachineState state, DateTime startedAt, string? reasonCode, CancellationToken cancellationToken = default);

    Task PublishAlarmRaisedAsync(LiveAlarmDto alarm, CancellationToken cancellationToken = default);

    Task PublishAlarmClearedAsync(string machineCode, Guid eventId, DateTime clearedAt, CancellationToken cancellationToken = default);

    Task PublishInspectionResultAsync(string machineCode, Verdict verdict, string unitSerial, string lotNumber, DateTime inspectedAt, CancellationToken cancellationToken = default);
}