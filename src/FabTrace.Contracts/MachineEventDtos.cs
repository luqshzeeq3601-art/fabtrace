using System.ComponentModel.DataAnnotations;

namespace FabTrace.Contracts;

/// <summary>An alarm raised by the machine. It is cleared later with a separate call.</summary>
public sealed record AlarmDto
{
    public required Guid EventId { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string AlarmCode { get; init; }

    [EnumDataType(typeof(AlarmSeverity))]
    public required AlarmSeverity Severity { get; init; }

    [StringLength(FieldLengths.Message)]
    public string Message { get; init; } = "";

    public required DateTimeOffset RaisedAt { get; init; }
}

public sealed record ClearAlarmRequest
{
    public required DateTimeOffset ClearedAt { get; init; }
}

/// <summary>
/// The machine entered a new state. Only transitions are sent; how long a state
/// lasted is the gap until the machine's next event.
/// </summary>
public sealed record MachineStateEventDto
{
    public required Guid EventId { get; init; }

    [EnumDataType(typeof(MachineState))]
    public required MachineState State { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    [StringLength(FieldLengths.Code)]
    public string? ReasonCode { get; init; }
}