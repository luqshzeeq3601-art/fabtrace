namespace FabTrace.Contracts;

public record LiveAlarmDto(
    Guid EventId,
    string MachineCode,
    string AlarmCode,
    AlarmSeverity Severity,
    string Message,
    DateTime RaisedAt);

public record LiveMachineStatusDto(
    string MachineCode,
    string MachineName,
    string MachineType,
    string Line,
    bool IsActive,
    MachineState CurrentState,
    DateTime StateStartedAt,
    string? StateReasonCode,
    IReadOnlyList<LiveAlarmDto> ActiveAlarms,
    int ShiftUnitsInspected,
    int ShiftUnitsPassed,
    int ShiftUnitsFailed,
    double YieldPercent,
    string? CurrentLotNumber,
    string? CurrentRecipeCode,
    string? CurrentOperatorBadge);

public record LiveFloorSummaryDto(
    int TotalMachines,
    int RunningCount,
    int IdleCount,
    int DownCount,
    int OtherCount,
    int ActiveAlarmsCount,
    int CriticalAlarmsCount,
    int TotalShiftInspected,
    int TotalShiftPassed,
    int TotalShiftFailed,
    double OverallYieldPercent,
    IReadOnlyList<LiveMachineStatusDto> Machines);

public record MachineStateBroadcastDto(
    string MachineCode,
    MachineState State,
    DateTime StartedAt,
    string? ReasonCode);

public record AlarmClearedBroadcastDto(
    string MachineCode,
    Guid EventId,
    DateTime ClearedAt);

public record InspectionResultBroadcastDto(
    string MachineCode,
    Verdict Verdict,
    string UnitSerial,
    string LotNumber,
    DateTime InspectedAt);