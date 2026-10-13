using FabTrace.Api.Hubs;
using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.SignalR;

namespace FabTrace.Api.Services;

/// <summary>
/// Broadcasts domain events to all connected SignalR clients on FactoryHub.
/// </summary>
public sealed class SignalRFactoryEventPublisher(IHubContext<FactoryHub> hubContext) : IFactoryEventPublisher
{
    public Task PublishMachineStateChangedAsync(
        string machineCode, MachineState state, DateTime startedAt, string? reasonCode, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            "MachineStateChanged",
            new MachineStateBroadcastDto(machineCode, state, startedAt, reasonCode),
            cancellationToken);

    public Task PublishAlarmRaisedAsync(
        LiveAlarmDto alarm, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("AlarmRaised", alarm, cancellationToken);

    public Task PublishAlarmClearedAsync(
        string machineCode, Guid eventId, DateTime clearedAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            "AlarmCleared",
            new AlarmClearedBroadcastDto(machineCode, eventId, clearedAt),
            cancellationToken);

    public Task PublishInspectionResultAsync(
        string machineCode, Verdict verdict, string unitSerial, string lotNumber, DateTime inspectedAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            "InspectionResultReported",
            new InspectionResultBroadcastDto(machineCode, verdict, unitSerial, lotNumber, inspectedAt),
            cancellationToken);
}