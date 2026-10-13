using FabTrace.Contracts;

namespace FabTrace.Core.Entities;

// Production data: created by machines as they run. These tables grow without limit,
// so they use bigint keys. All timestamps are UTC.

public class Lot
{
    public int LotId { get; set; }
    public required string LotNumber { get; set; }
    public required string ProductCode { get; set; }

    /// <summary>Time of the earliest result received for this lot.</summary>
    public DateTime StartedAt { get; set; }

    public List<Unit> Units { get; set; } = [];
}

public class Unit
{
    public long UnitId { get; set; }
    public required string SerialNumber { get; set; }

    public int LotId { get; set; }
    public Lot? Lot { get; set; }

    public List<InspectionResult> Results { get; set; } = [];
}

public class InspectionResult
{
    public long InspectionResultId { get; set; }

    /// <summary>Idempotency key chosen by the machine. Unique, so a resend cannot create a second row.</summary>
    public Guid ResultId { get; set; }

    public long UnitId { get; set; }
    public Unit? Unit { get; set; }

    public int ProcessStepId { get; set; }
    public ProcessStep? ProcessStep { get; set; }

    public int MachineId { get; set; }
    public Machine? Machine { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int OperatorId { get; set; }
    public Operator? Operator { get; set; }

    public Verdict Verdict { get; set; }
    public string? DefectCode { get; set; }

    /// <summary>When the machine finished the unit (machine clock).</summary>
    public DateTime InspectedAt { get; set; }

    /// <summary>When the server stored it (server clock). A large gap means the machine was offline.</summary>
    public DateTime ReceivedAt { get; set; }

    public int CycleTimeMs { get; set; }

    public List<TestReading> Readings { get; set; } = [];
}

public class TestReading
{
    public long TestReadingId { get; set; }
    public long InspectionResultId { get; set; }
    public required string ParameterName { get; set; }
    public double Value { get; set; }
    public double? LowerLimit { get; set; }
    public double? UpperLimit { get; set; }
    public required string Unit { get; set; }
}

public class Alarm
{
    public long AlarmId { get; set; }
    public Guid EventId { get; set; }

    public int MachineId { get; set; }
    public Machine? Machine { get; set; }

    public required string AlarmCode { get; set; }
    public AlarmSeverity Severity { get; set; }
    public required string Message { get; set; }
    public DateTime RaisedAt { get; set; }

    /// <summary>Null while the alarm is still active.</summary>
    public DateTime? ClearedAt { get; set; }
}

/// <summary>
/// A state transition. There is no end time: a state lasts until the machine's
/// next event, so two rows can never disagree about when a state ended.
/// </summary>
public class MachineStateEvent
{
    public long MachineStateEventId { get; set; }
    public Guid EventId { get; set; }

    public int MachineId { get; set; }
    public Machine? Machine { get; set; }

    public MachineState State { get; set; }
    public DateTime StartedAt { get; set; }
    public string? ReasonCode { get; set; }
}