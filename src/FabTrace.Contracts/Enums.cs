using System.Text.Json.Serialization;

namespace FabTrace.Contracts;

// Enums travel as text in JSON ("Pass", not 1) so payloads are readable in logs,
// and are stored as tinyint in SQL Server. The numeric values are part of the
// database contract: never renumber them, only append.

[JsonConverter(typeof(JsonStringEnumConverter<Verdict>))]
public enum Verdict : byte
{
    Pass = 1,
    Fail = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<MachineState>))]
public enum MachineState : byte
{
    Running = 1,
    Idle = 2,
    Down = 3,
    Setup = 4,
    Maintenance = 5,
}

[JsonConverter(typeof(JsonStringEnumConverter<AlarmSeverity>))]
public enum AlarmSeverity : byte
{
    Info = 1,
    Warning = 2,
    Critical = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<IngestStatus>))]
public enum IngestStatus
{
    /// <summary>Stored for the first time.</summary>
    Created,

    /// <summary>Already stored earlier; nothing changed. Safe for the sender to forget.</summary>
    Duplicate,

    /// <summary>Not stored because the data is wrong; resending will not help.</summary>
    Rejected,
}